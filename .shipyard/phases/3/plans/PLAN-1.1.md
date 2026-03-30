---
phase: test-infrastructure-sqlite-slice
plan: "1.1"
wave: 1
dependencies: []
must_haves:
  - IntegrationTests.sln with correct Visual Studio format
  - IntegrationTests.csproj dual-targeting net8.0 and net48
  - MSTest SDK references (Microsoft.NET.Test.Sdk, MSTest.TestAdapter, MSTest.TestFramework)
  - DotNetWorkQueue 0.9.13 and DotNetWorkQueue.Transport.SQLite 0.9.13 NuGet references
  - SampleShared HintPath references for both target frameworks
  - App.config with EnableCompression=true, EnableEncryption=true, trace/metrics/chaos=false
  - System.Configuration.ConfigurationManager package for App.config reading
  - Project restores and builds for both target frameworks
files_touched:
  - Source/Samples/IntegrationTests/IntegrationTests.sln
  - Source/Samples/IntegrationTests/IntegrationTests.csproj
  - Source/Samples/IntegrationTests/App.config
tdd: false
---

# Plan 1.1: Create IntegrationTests Project Infrastructure

## Context

Phase 3 creates an MSTest integration test project as a vertical slice ending with passing
SQLite tests. This first plan establishes the build infrastructure: the solution file, csproj
with all required NuGet packages and SampleShared HintPath, and the App.config that controls
SharedConfiguration behavior. No test code yet -- just a project that restores and builds.

The project lives at `Source/Samples/IntegrationTests/` (csproj directly in this folder,
matching the pattern where each transport's projects sit directly in their subfolders).

## Dependencies

- Phase 2 must be complete (SampleShared built at 0.9.13).
- No dependencies on other Phase 3 plans.

## Key Design Decisions

1. **SampleShared HintPath**: The IntegrationTests folder is at `Source/Samples/IntegrationTests/`,
   which is a sibling of `Source/Samples/SampleShared/`. The relative path from IntegrationTests
   to SampleShared is `..\..\SampleShared\` -- wait, no. Both are at depth
   `Source/Samples/{folder}/`. So the relative path is `..\SampleShared\bin\Debug\{tfm}\SampleShared.dll`.
   This matches the pattern used by all transport projects (e.g., SQLiteConsumer uses
   `..\..\SampleShared\bin\Debug\net8.0\SampleShared.dll` because it's at
   `Source/Samples/SQLite/SQLiteConsumer/` -- two levels up). IntegrationTests is at
   `Source/Samples/IntegrationTests/` -- only one level up from Samples, so the HintPath is
   `..\SampleShared\bin\Debug\net8.0\SampleShared.dll`.

2. **App.config**: SharedConfiguration's static constructor reads ConfigurationManager.AppSettings.
   The test project's App.config sets EnableCompression=true, EnableEncryption=true (to exercise
   the full serialize/compress/encrypt pipeline), and everything else false. SharedConfiguration
   will pick these up automatically when the test assembly loads.

3. **NuGet packages**: Include only what's needed for this phase (SQLite transport). Future phases
   will add LiteDb, Redis, SQL Server, PostgreSQL packages. Keep the csproj minimal to reduce
   restore time and potential conflicts.

4. **No tracesettings.json or metricsettings.json needed**: With EnableTrace=false and
   EnableMetrics=false in App.config, those files are never read.

## Tasks

<task id="1" files="Source/Samples/IntegrationTests/IntegrationTests.sln" tdd="false">
  <action>
    Create `Source/Samples/IntegrationTests/IntegrationTests.sln` in Visual Studio Solution
    File Format Version 12.00 with a single project entry pointing to `IntegrationTests.csproj`.
    Use project type GUID `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}` (C#). Generate a fresh
    GUID for the project (e.g., `{B1C2D3E4-F5A6-7890-ABCD-EF1234567890}`) and a fresh
    SolutionGuid. Include standard Debug|Any CPU and Release|Any CPU solution configuration
    platforms.

    Follow the exact format from `Source/Samples/SampleShared/SampleShared.sln`:
    - Header: `Microsoft Visual Studio Solution File, Format Version 12.00`
    - `# Visual Studio Version 17`
    - `VisualStudioVersion = 17.0.31903.59`
    - `MinimumVisualStudioVersion = 10.0.40219.1`
    - Project/EndProject block
    - Global sections: SolutionConfigurationPlatforms, ProjectConfigurationPlatforms,
      SolutionProperties, ExtensibilityGlobals
  </action>
  <verify>
    dotnet sln "Source/Samples/IntegrationTests/IntegrationTests.sln" list
  </verify>
  <done>
    `dotnet sln list` shows IntegrationTests.csproj as the only project in the solution.
  </done>
</task>

<task id="2" files="Source/Samples/IntegrationTests/IntegrationTests.csproj" tdd="false">
  <action>
    Create `Source/Samples/IntegrationTests/IntegrationTests.csproj` as an SDK-style project
    with these properties and references:

    **PropertyGroup:**
    - `<TargetFrameworks>net8.0;net48</TargetFrameworks>`
    - `<IsPackable>false</IsPackable>`
    - `<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>`
    - `<PlatformTarget>AnyCPU</PlatformTarget>`

    **PackageReference ItemGroup (unconditional):**
    - `Microsoft.NET.Test.Sdk` Version `17.12.0`
    - `MSTest.TestAdapter` Version `3.7.3`
    - `MSTest.TestFramework` Version `3.7.3`
    - `DotNetWorkQueue` Version `0.9.13`
    - `DotNetWorkQueue.Transport.SQLite` Version `0.9.13`
    - `Serilog` Version `4.3.0`
    - `Serilog.Extensions.Logging` Version `10.0.0`
    - `Serilog.Sinks.Console` Version `6.1.1`
    - `System.Configuration.ConfigurationManager` Version `10.0.1`
    - `Microsoft.Extensions.Logging` Version `10.0.1`
    - `Microsoft.Extensions.Logging.Abstractions` Version `10.0.1`
    - `Newtonsoft.Json` Version `13.0.4`
    - `OpenTelemetry` Version `1.14.0`
    - `OpenTelemetry.Api` Version `1.14.0`
    - `OpenTelemetry.Exporter.Console` Version `1.14.0`
    - `OpenTelemetry.Exporter.OpenTelemetryProtocol` Version `1.14.0`
    - `SimpleInjector` Version `5.5.0`
    - `System.Data.SQLite.Core` Version `1.0.119`
    - `Stub.System.Data.SQLite.Core.NetFramework` Version `1.0.119`
    - `Polly.Caching.Memory` Version `3.0.2`

    **SampleShared HintPath (framework-conditional):**
    ```xml
    <ItemGroup Condition=" '$(TargetFramework)' == 'net48' ">
      <Reference Include="SampleShared">
        <HintPath>..\SampleShared\bin\Debug\net48\SampleShared.dll</HintPath>
      </Reference>
    </ItemGroup>
    <ItemGroup Condition=" '$(TargetFramework)' == 'net8.0' ">
      <Reference Include="SampleShared">
        <HintPath>..\SampleShared\bin\Debug\net8.0\SampleShared.dll</HintPath>
      </Reference>
    </ItemGroup>
    ```

    **App.config copy:**
    ```xml
    <ItemGroup>
      <None Include="App.config" />
    </ItemGroup>
    ```

    NOTE: The HintPath uses `..\SampleShared\...` (one level up) because IntegrationTests is at
    `Source/Samples/IntegrationTests/` and SampleShared is at `Source/Samples/SampleShared/` --
    they are sibling directories under `Source/Samples/`.
  </action>
  <verify>
    dotnet restore "Source/Samples/IntegrationTests/IntegrationTests.sln" && dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --no-restore 2>&1 | tail -5
  </verify>
  <done>
    1. `dotnet restore` succeeds with no package resolution errors.
    2. `dotnet build -c Debug` succeeds for both net8.0 and net48 with 0 errors.
    3. The SampleShared.dll reference resolves (no CS0006 warning about missing assembly).
  </done>
</task>

<task id="3" files="Source/Samples/IntegrationTests/App.config" tdd="false">
  <action>
    Create `Source/Samples/IntegrationTests/App.config` with the following content:

    ```xml
    <?xml version="1.0" encoding="utf-8"?>
    <configuration>
      <startup>
        <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />
      </startup>
      <appSettings>
        <add key="EnableTrace" value="false" />
        <add key="EnableMetrics" value="false" />
        <add key="EnableCompression" value="true" />
        <add key="EnableEncryption" value="true" />
        <add key="EnableChaos" value="false" />
        <add key="EnableDashboard" value="false" />
        <add key="EnableHistory" value="false" />
      </appSettings>
    </configuration>
    ```

    This ensures SharedConfiguration picks up: compression=on, encryption=on, everything
    else=off. The test helper will pass `SharedConfiguration.EnableCompression` and
    `SharedConfiguration.EnableEncryption` to `Injectors.AddInjectors(...)`, exercising the
    full GZip + TripleDES interceptor pipeline without requiring Jaeger, metrics endpoints,
    or chaos policies.
  </action>
  <verify>
    test -f "Source/Samples/IntegrationTests/App.config" && grep -c "EnableCompression.*true" "Source/Samples/IntegrationTests/App.config" && grep -c "EnableTrace.*false" "Source/Samples/IntegrationTests/App.config"
  </verify>
  <done>
    1. App.config exists at `Source/Samples/IntegrationTests/App.config`.
    2. EnableCompression=true and EnableEncryption=true are present.
    3. EnableTrace=false, EnableMetrics=false, EnableChaos=false, EnableDashboard=false, EnableHistory=false are present.
  </done>
</task>

## Verification

```bash
# Prerequisite: SampleShared must already be built
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug

# 1. Solution lists the project
dotnet sln "Source/Samples/IntegrationTests/IntegrationTests.sln" list

# 2. Restore and build succeed
dotnet restore "Source/Samples/IntegrationTests/IntegrationTests.sln"
dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --no-restore

# 3. App.config has correct settings
grep "EnableCompression.*true" "Source/Samples/IntegrationTests/App.config"
grep "EnableEncryption.*true" "Source/Samples/IntegrationTests/App.config"
grep "EnableTrace.*false" "Source/Samples/IntegrationTests/App.config"
```
