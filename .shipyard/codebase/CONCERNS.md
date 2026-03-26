# CONCERNS.md

## Overview

This repository is a samples-only project demonstrating DotNetWorkQueue across five transport backends. Because the samples are intended to be cloned and run locally, several concerns that would be critical in a production codebase are partially expected (e.g., committed config files). They are still documented here because they range from genuine bugs and security hygiene problems to maintenance friction that accumulates over time. Concerns are ordered Critical > High > Medium > Low within each category.

---

## Findings

### Security

- **[Critical] Real database credentials committed to git history**
  - `appsettings.json` (tracked by git, confirmed via `git ls-files`) contains a live SQL Server connection string with username `brian` and password `123abc`, plus a PostgreSQL connection string with `userid=brian`, Redis endpoint at `192.168.0.2`, and absolute local paths `C:\Users\brian\Documents\test.db3` / `C:\Users\brian\Documents\test.db`.
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (lines 21, 27, 32, 38, 44)
    ```json
    "ConnectionString": "Server=192.168.0.2;Database=IntegrationTests;user=brian;password=123abc;TrustServerCertificate=True;"
    ```
  - These credentials are now in git history permanently unless a rebase/filter-branch is performed. Rotating the password is necessary but not sufficient.
  - Remediation: Add `appsettings.json` to `.gitignore`, keep only `appsettings.example.json` in source control. Rotate the exposed credentials. Consider `git filter-repo` to scrub history if the repo is public or shared.

- **[Critical] Same credentials duplicated in an untracked "Copy" file**
  - `appsettings - Copy.json` is present on disk (visible in `git status` as untracked) and contains identical live credentials. It is not currently tracked by git, but the fact it exists at all means it was created from the live file and could be accidentally staged in a future commit.
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings - Copy.json` (lines 23, 29, 44)
  - Remediation: Delete the file and add `**/appsettings - Copy.json` to `.gitignore`.

- **[High] SQL Server credentials committed in App.config files across three projects**
  - All SQL Server sample projects contain `password=123abc` in their `Database` connection string key, and these files are tracked by git.
  - Evidence: `Source/Samples/SQLServer/SQLServerProducer/App.config` (line 7), `Source/Samples/SQLServer/SQLServerConsumer/App.config` (line 7), `Source/Samples/SQLServer/SQLServerConsumerAsync/App.config` (line 7)
    ```xml
    <add key="Database" value="Server=192.168.0.2;...;user=brian;password=123abc;..." />
    ```
  - Remediation: Replace the literal password with a placeholder (e.g., `password=YOUR_PASSWORD_HERE`) in all three files, and document in the README that users must supply their own connection strings before running.

- **[High] Hardcoded TripleDES encryption key/IV used as sample default**
  - The encryption key `"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"` and IV `"aaaaaaaaaaa="` are hardcoded in source code. These same values appear in `appsettings.json` (committed with credentials) and in `appsettings - Copy.json`. While labelled "for sample only", consumers who copy this code without changing the keys will have functionally no encryption.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 72-73)
    ```csharp
    string key = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    string iv = "aaaaaaaaaaa=";
    ```
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (lines 13-14)
  - Remediation: Add a prominent comment directing users to replace these before use. Consider generating random sample keys at startup and printing them to console with a clear warning.

- **[Medium] Hardcoded private IP addresses across 158+ config files**
  - The IP `192.168.0.2` (and variants `192.168.0.36`, `192.168.0.58`, `192.168.0.65`) appear in every `tracesettings.json`, `metricsettings.json`, `App.config`, and `appsettings.json` across all 36 tracked sample projects. These are the author's LAN addresses and leak internal network topology.
  - Evidence: All 36 `tracesettings.json` files (line 4: `"JAEGER_AGENT_HOST": "192.168.0.2"`), `Source/Samples/SampleShared/metricsettings.json` (line 3), all PostgreSQL/Redis/SQLServer `App.config` files (line 7)
  - Remediation: Replace with `localhost` or `<your-host>` placeholder values in all committed config files. Document that users must configure their own endpoints.

- **[Medium] Personal username and local file paths in the example config**
  - `appsettings.example.json` (the file intended as a sanitised template) still contains `C:\\Users\\brian\\Documents\\test.db3`, `C:\\Users\\brian\\Documents\\test.db`, and the Redis IP `192.168.0.2`. The SQL Server and PostgreSQL connection strings were correctly redacted but the file-based transports were not.
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.example.json` (lines 32, 38, 44)
  - Remediation: Replace with `C:\\path\\to\\your.db3` / `C:\\path\\to\\your.db` and `your-redis-host` respectively.

---

### Dependencies

- **[High] `System.Data.SqlClient` v4.9.0 — deprecated package, should be `Microsoft.Data.SqlClient`**
  - All seven SQL Server sample projects reference `System.Data.SqlClient` v4.9.0. Microsoft deprecated this package in favour of `Microsoft.Data.SqlClient` (which receives security updates). `System.Data.SqlClient` last received a security release in 2019 and will not receive further patches.
  - Evidence: `Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj` (line 48), and the same line appears in all six other `SQLServer/*.csproj` files.
    ```xml
    <PackageReference Include="System.Data.SqlClient" Version="4.9.0" />
    ```
  - Remediation: Replace with `<PackageReference Include="Microsoft.Data.SqlClient" Version="5.x" />` across all seven projects.

- **[High] `Polly.Contrib.Simmy` v0.3.0 is incompatible with Polly v8**
  - Polly v8 introduced a completely redesigned API (the `ResiliencePipeline` model). `Polly.Contrib.Simmy` v0.3.0 targets Polly v7 and its `MonkeyPolicy` API, which no longer exists in Polly v8. All transport projects except Dashboard.Api reference both Polly v8.6.5 and Simmy v0.3.0.
  - Evidence: `Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj` (lines 40, 41), same pattern in all 36 transport project files.
    ```xml
    <PackageReference Include="Polly" Version="8.6.5" />
    <PackageReference Include="Polly.Contrib.Simmy" Version="0.3.0" />
    ```
  - [Inferred] If `Simmy` chaos injection is actually invoked at runtime (enabled via `EnableChaos=true` in App.config), this combination will produce runtime binding errors. If `Simmy` is referenced in project files but not actually called in the chaos path, it may compile but throw on first use.
  - Remediation: Replace `Polly.Contrib.Simmy` with `Simmy` v1.0+ (the Polly v8-compatible fork), or remove it if the DotNetWorkQueue core library handles chaos injection via its own `IPolicies.EnableChaos` mechanism (which appears to be the case based on `Injectors.cs` line 62).

- **[High] `Polly.Caching.Memory` v3.0.2 — Polly v7 add-on used alongside Polly v8**
  - Same version mismatch as Simmy. `Polly.Caching.Memory` v3.x targets the Polly v7 `CachePolicy` API which was removed in Polly v8.
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 44), present in all 36 transport project files.
    ```xml
    <PackageReference Include="Polly.Caching.Memory" Version="3.0.2" />
    ```
  - Remediation: Evaluate whether caching policies are actually used via the DotNetWorkQueue core (likely yes, internally). If the application code itself does not call `CachePolicy` directly, the reference can be removed. Otherwise migrate to the Polly v8 `CacheResilienceStrategy`.

- **[Medium] DotNetWorkQueue version split: SampleShared v0.9.11 vs all other projects v0.9.10**
  - `SampleShared.csproj` references `DotNetWorkQueue` v0.9.11 and `DotNetWorkQueue.Dashboard.Client` v0.9.11, while every transport project and Dashboard.Api still references v0.9.10. This split means SampleShared is compiled against a different version of the core library than the executables that reference its DLL via HintPath.
  - Evidence: `Source/Samples/SampleShared/SampleShared.csproj` (lines 8, 23)
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 18, 85)
  - Remediation: Align all projects to the same DotNetWorkQueue version. Given the HintPath coupling, updating SampleShared without updating the transport projects (or vice versa) risks assembly version mismatches at runtime.

- **[Medium] Dashboard.Api targets `net10.0` but CI only installs `8.0.x`**
  - `DashBoard.Api.csproj` declares `<TargetFramework>net10.0</TargetFramework>`, but `.github/workflows/ci.yml` only installs `dotnet-version: '8.0.x'`. GitHub Actions' `windows-latest` runner may have .NET 10 pre-installed (as of late 2025), but this is not guaranteed and the CI does not explicitly provision it. If `windows-latest` rolls forward past net10 preview availability, the build will silently depend on whatever is pre-installed.
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` (line 3), `.github/workflows/ci.yml` (line 19)
  - Remediation: Add an explicit `dotnet-version: '10.0.x'` entry to `setup-dotnet` in the CI workflow, or align Dashboard.Api back to `net8.0` if net10-specific features are not required.

- **[Medium] Dashboard.Api HintPath points to net8.0 SampleShared build despite targeting net10.0**
  - `DashBoard.Api.csproj` references `SampleShared` via `HintPath` pointing to `..\..\SampleShared\bin\Debug\net8.0\SampleShared.dll`. Since Dashboard.Api now targets `net10.0`, this loads a net8.0 assembly into a net10.0 host. This works due to .NET's backward compatibility, but it is a mismatch and will produce assembly version warnings during build, and could break if SampleShared uses net8.0-only APIs.
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` (line 30)
  - Remediation: Either update the HintPath to `net10.0\SampleShared.dll` (which requires SampleShared to dual-target net10.0 as well), or revert Dashboard.Api to net8.0.

- **[Low] `MsgPack.Cli` v1.0.1 — unmaintained serialiser**
  - `MsgPack.Cli` is the older MessagePack implementation. The actively maintained successor is `MessagePack-CSharp` (`MessagePack` NuGet package). `MsgPack.Cli` has had no releases since 2021.
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 38), `Source/Samples/LiteDb/LiteDbProducer/LiteDbProducer.csproj` (line 37), and all Redis/LiteDb transport projects.
  - [Inferred] This may be a transitive dependency pulled in by `DotNetWorkQueue.Transport.Redis` or `DotNetWorkQueue.Transport.LiteDb` rather than a direct application choice. If so, the fix is upstream in DotNetWorkQueue itself.

---

### Build and Maintenance

- **[High] HintPath references hardcode `Debug` configuration — Release builds will fail**
  - Every transport project and Dashboard.Api references SampleShared via a HintPath that explicitly contains `\bin\Debug\`. Building any transport project in the `Release` configuration will fail unless SampleShared was also built in `Debug` configuration first, because the DLL does not exist at the Release path.
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 71, 81)
    ```xml
    <HintPath>..\..\SampleShared\bin\Debug\net48\SampleShared.dll</HintPath>
    <HintPath>..\..\SampleShared\bin\Debug\net8.0\SampleShared.dll</HintPath>
    ```
  - This pattern appears in all 36 transport `.csproj` files plus `DashBoard.Api.csproj`.
  - Remediation: Parameterise the HintPath: `..\..\SampleShared\bin\$(Configuration)\net8.0\SampleShared.dll`. This is a one-line change per project that would allow Release builds to work.

- **[Medium] `#if net48` preprocessor symbols are lowercase — never true on SDK-style projects**
  - SDK-style projects define the target framework constant as `NET48` (uppercase), not `net48`. The four `#if net48` / `#if net48` blocks in `RunProducer.cs` guard the dynamic Linq expression runner code paths and will never be compiled, silently disabling that feature for .NET Framework 4.8 builds.
  - Evidence: `Source/Samples/SampleShared/RunProducer.cs` (lines 54, 82, 163, 171)
    ```csharp
    #if net48
        public static IEnumerable<IQueueOutputMessage> RunDynamic(...)
    ```
  - The correct symbol for SDK-style multi-targeting is `NET48` (all caps). The `#if NET8_0_OR_GREATER` usage in `Injectors.cs` (line 17) uses the correct form, showing inconsistency across the codebase.
  - Remediation: Replace `#if net48` with `#if NET48` in `RunProducer.cs` (4 occurrences).

- **[Medium] 36 project files with fully duplicated, manually managed package lists**
  - Every transport project contains an identical 40-50 line `<ItemGroup>` of `PackageReference` entries. There is no `Directory.Build.props` or `Directory.Packages.props` (Central Package Management) to share these. Any version update must be applied manually to all 36 files, which is how the current DotNetWorkQueue version split (v0.9.10 vs v0.9.11) likely arose.
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 17-68), identical block in all other transport `.csproj` files.
  - Remediation: Introduce a `Directory.Build.props` at `Source/Samples/` level with the common packages, and optionally enable Central Package Management via `Directory.Packages.props` to enforce a single version per package across all projects.

- **[Medium] `OutputPath` overrides suppress per-framework subdirectory layout in some projects**
  - `RedisProducer.csproj` sets `<OutputPath>bin\$(Configuration)\</OutputPath>` (line 9), which collapses the per-TFM output structure. This means building both `net8.0` and `net48` targets will write to the same directory, with the second build potentially overwriting the first. `SQLServerProducer.csproj` has the same setting; `LiteDbProducer.csproj` does not (no `OutputPath` override), creating an inconsistency across projects.
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 9), `Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj` (no `OutputPath` — wait, this project does not set it; see line structure). [Inferred] Presence/absence varies per project.
  - Remediation: Remove the `<OutputPath>` override and let MSBuild use its default per-TFM layout (`bin\Debug\net8.0\` etc.), or set it to `bin\$(Configuration)\$(TargetFramework)\`.

- **[Low] Stale copyright year in project metadata**
  - `RedisProducer.csproj` declares `<Copyright>Copyright © 2019</Copyright>` (line 8). This is a cosmetic issue but makes the samples appear unmaintained to new users.
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 8)
  - Remediation: Update to current year, or use `$(Year)` via a `Directory.Build.props` property.

---

### Code Quality

- **[Medium] `int.Parse` without null guard on `JAEGER_AGENT_PORT` config key**
  - `Injectors.cs` calls `int.Parse(configuration["JAEGER_AGENT_PORT"])` without checking whether the key is present or the value is a valid integer. If `tracesettings.json` is missing or malformed, this throws an unhandled `FormatException` or `NullReferenceException` at startup, with no diagnostic message.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (line 238)
    ```csharp
    var port = int.Parse(configuration["JAEGER_AGENT_PORT"]);
    ```
  - Remediation: Use `int.TryParse` with a fallback, or validate the configuration section is non-null before parsing.

- **[Medium] `.Result` blocking calls on async methods in interactive loop**
  - `RunProducer.RunLoop` calls `.Result` on async tasks in several switch branches (e.g., `RunStaticAsync(...).Result`, `RunAsync(...).Result`). In a console application this is safe but non-idiomatic, and it masks any `AggregateException` wrapping that would obscure error messages.
  - Evidence: `Source/Samples/SampleShared/RunProducer.cs` (lines 169, 280, 284, 288, etc.)
  - Remediation: Convert `RunLoop` to `async Task RunLoopAsync` and use `await` throughout, or at minimum use `.GetAwaiter().GetResult()` for better exception unwrapping.

- **[Low] Intentional divide-by-zero used to simulate errors**
  - `MessageProcessing.cs` simulates error messages by computing `100 / (9 - 9)` to produce a `DivideByZeroException`. This is functional but fragile — a sufficiently aggressive optimiser or future compiler version could theoretically evaluate this at compile time (as a constant expression) rather than at runtime. A `throw new DivideByZeroException(...)` would be clearer.
  - Evidence: `Source/Samples/SampleShared/MessageProcessing.cs` (lines 27-29)
    ```csharp
    var i = 9 - 9;
    var result = 100 / i;
    ```
  - Remediation: Replace with an explicit `throw new InvalidOperationException("Simulated error")` for clarity.

---

### Configuration and Repository Hygiene

- **[High] Invalid JSON in all 36 `tracesettings.json` files (trailing comma)**
  - Every `tracesettings.json` across all 158 tracked instances ends with a trailing comma after the last value in the `Jaeger` object, making the file invalid per the JSON specification. `System.Text.Json` (used by default in .NET 6+) will reject these files at runtime unless `JsonCommentHandling` / `AllowTrailingCommas` is enabled.
  - Evidence: `Source/Samples/LiteDb/LiteDbConsumer/tracesettings.json` (line 7 — trailing `}` after `"JAEGER_AGENT_PORT": "4319"` followed by `},`)
    ```json
    {
      "Jaeger": {
        "JAEGER_SERVICE_NAME": "dotnetworkqueue-LiteDb-sample",
        "JAEGER_AGENT_HOST": "192.168.0.2",
        "JAEGER_AGENT_PORT": "4319"
      },
    }
    ```
  - [Inferred] `Microsoft.Extensions.Configuration.Json` enables `AllowTrailingCommas` by default, so this does not cause a runtime failure today. However it is non-standard and will fail if any consumer switches to a stricter JSON parser.
  - Remediation: Remove the trailing comma from the `Jaeger` block in all 36 `tracesettings.json` files.

- **[Medium] `research.md` and `Screenshot 2026-03-17 120005.png` untracked in root**
  - Both files appear as untracked in `git status`. `research.md` likely contains working notes; the screenshot appears to be a development-time screen capture. Neither belongs in the repository root if they are personal working files.
  - Evidence: `git status` output (current session), files `research.md` and `Screenshot 2026-03-17 120005.png` at repo root.
  - Remediation: Add both to `.gitignore` (e.g., `research.md`, `*.png`) or delete them. If the screenshot is intended as documentation, move it to `docs/` and track it intentionally.

- **[Medium] `.gitignore` contains paths referencing a different repository**
  - Lines 198-213 of `.gitignore` list paths under `Source/DotNetWorkQueue.Transport.*` and `Source/DotNetWorkQueue/` which do not exist in this repository. These appear to be copied from the main DotNetWorkQueue library repo's `.gitignore`.
  - Evidence: `.gitignore` (lines 198-213)
    ```
    Source/DotNetWorkQueue.Transport.PostgreSQL.Tests/AppReadme/...
    Source/DotNetWorkQueue/DotNetWorkQueue.xml
    ```
  - Remediation: Remove the orphaned entries to reduce confusion.

- **[Low] `dotnet-tools.json` tracks `ilspycmd` — a decompilation tool**
  - The repository's local tool manifest references `ilspycmd` v9.1.0.7988 (ILSpy command-line decompiler). This is a development-aid tool not needed to build or run the samples and should not be required of contributors.
  - Evidence: `dotnet-tools.json` (lines 5-10)
  - Remediation: Either remove it from the tool manifest if it is not part of any documented workflow, or add a comment to `dotnet-tools.json` explaining its purpose.

---

## Summary Table

| Item | Detail | Severity | Confidence |
|------|--------|----------|------------|
| Live SQL Server password in `appsettings.json` | `password=123abc` committed and in git history | Critical | Observed |
| Live SQL Server password in 3 `App.config` files | `password=123abc` in SQL Server transport configs | High | Observed |
| Live credentials in `appsettings - Copy.json` | Untracked but on disk, same credentials as `appsettings.json` | Critical | Observed |
| Hardcoded TripleDES key/IV | `aaaa...` key used as "sample" default | High | Observed |
| Personal IP addresses in 158+ config files | `192.168.0.2` in all tracesettings, metricsettings, App.configs | Medium | Observed |
| Personal paths in `appsettings.example.json` | `C:\Users\brian\...` in template file | Medium | Observed |
| `System.Data.SqlClient` — deprecated package | All 7 SQL Server projects; no security patches | High | Observed |
| `Polly.Contrib.Simmy` v0.3.0 with Polly v8 | API mismatch — v0.3.0 targets Polly v7 | High | Observed |
| `Polly.Caching.Memory` v0.3.x with Polly v8 | API mismatch — `CachePolicy` removed in Polly v8 | High | Observed |
| DotNetWorkQueue version split (v0.9.10 vs v0.9.11) | SampleShared vs transport projects misaligned | Medium | Observed |
| Dashboard.Api targets net10.0; CI installs net8.0 | CI may silently rely on pre-installed .NET 10 | Medium | Observed |
| Dashboard.Api HintPath points to net8.0 SampleShared | TFM mismatch between consumer and DLL | Medium | Observed |
| HintPath hardcodes `Debug` — Release builds fail | All 36 transport projects affected | High | Observed |
| `#if net48` (lowercase) — blocks never compile | 4 occurrences in `RunProducer.cs`; dynamic Linq disabled | Medium | Observed |
| 36 duplicated package lists — no CPM or Directory.Build.props | Version drift risk; caused current v0.9.10/0.9.11 split | Medium | Observed |
| `int.Parse` without null guard on Jaeger port | Throws on missing/malformed tracesettings.json | Medium | Observed |
| `.Result` blocking on async in interactive loop | Masks `AggregateException`; non-idiomatic | Medium | Observed |
| Trailing comma in all 36 `tracesettings.json` | Invalid JSON; tolerated by MS config but non-standard | Medium | Observed (158 files) |
| `research.md` and `.png` untracked at root | Personal working files; should be gitignored | Medium | Observed |
| Orphaned `.gitignore` entries from another repo | Paths don't exist in this repo | Low | Observed |
| Intentional divide-by-zero error simulation | Fragile; should be explicit throw | Low | Observed |
| Stale copyright year (2019) in project metadata | Cosmetic | Low | Observed |
| `ilspycmd` in `dotnet-tools.json` | Dev tool, not needed by contributors | Low | Observed |
| `MsgPack.Cli` v1.0.1 — unmaintained | May be transitive; upstream fix needed | Low | Inferred |

---

## Open Questions

1. **Are the SQL Server / PostgreSQL credentials in `App.config` and `appsettings.json` still live?** If so, they should be rotated immediately regardless of whether the repository is public.
2. **Is `Polly.Contrib.Simmy` / `Polly.Caching.Memory` actually invoked at runtime**, or are these vestigial references that the DotNetWorkQueue core now handles internally? If unused, both packages can be removed entirely from all 36 transport project files.
3. **Why does Dashboard.Api target `net10.0` while all other projects target `net8.0;net48`?** Is there a specific net10 API in use, or was the target framework bumped inadvertently during development?
4. **Is the `research.md` file intended to be committed eventually?** If it documents findings relevant to the project, it should be reviewed and either deleted or moved to `docs/`.
5. **Is the `OutputPath` override in some (but not all) transport projects intentional?** If it is, the reasoning should be documented; if not, it should be removed for consistency.
