# Security Audit Report — Phase 2

## Executive Summary

**Verdict:** FAIL
**Risk Level:** High

Three categories of findings require attention before this phase ships. Most seriously, developer credentials and a real SQL Server password (`password=123abc`) have been committed to version control across multiple `App.config` files and `appsettings.json` — these must be rotated and replaced with safe placeholders before the commit reaches any public or shared remote. A weak all-zeroes TripleDES encryption key was also committed to `appsettings.json`, which provides no real encryption for any queue payload protected by that key. The version bump and `History.Enabled` refactor are clean and introduce no new vulnerabilities.

### What to Do

| Priority | Finding | Location | Effort | Action |
|----------|---------|----------|--------|--------|
| 1 | Real SQL Server password committed | `SQLServerConsumer/App.config`, `SQLServerConsumerAsync/App.config`, `SQLServerProducer/App.config`, `appsettings.json` | Trivial | Rotate `password=123abc` credential immediately; replace with placeholder `password=yourpassword` in all files |
| 2 | All-zeroes TripleDES key committed | `appsettings.json:13-14` | Trivial | Replace key/IV with the original placeholder strings `your-base64-key-here` / `your-base64-iv-here` |
| 3 | Developer-specific connection strings committed | All 36 `App.config` files (PostgreSQL, SQLServer, Redis, SQLite, LiteDB) | Small | Reset to `localhost`-based placeholder strings matching the originals |
| 4 | Untracked backup file with live credentials | `appsettings - Copy.json` (untracked) | Trivial | Delete this file before committing; add `*Copy*.json` to `.gitignore` |
| 5 | `EnableHistory` default is now `false` on non-Redis transports | All consumer `Program.cs` files | Advisory | Confirm by design; document in changelog that History requires `EnableHistory=true` in `App.config` |

### Themes

- Developer test environment state (real IPs, real credentials, real DB names) leaked into sample configuration files that are committed to a public repository.
- The config-file changes were not part of the stated Phase 2 scope, indicating they are accidental working-directory contamination from local testing.

---

## Detailed Findings

### Critical

_No directly exploitable runtime vulnerabilities were introduced by the version bump or code changes. The findings below are classified High because they constitute credential exposure in version control._

### Important

**[I1] Cleartext SQL Server password committed to version control**
- **Location:** `Source/Samples/SQLServer/SQLServerConsumer/App.config:7`, `SQLServerConsumerAsync/App.config:7`, `SQLServerProducer/App.config:7`, `DashBoard.Api/DashBoard.Api/appsettings.json:20`
- **Description:** The `Database` connection string in three SQL Server `App.config` files and `appsettings.json` was changed from the safe placeholder `password=yourpassword` to a real credential: `user=brian;password=123abc`. This value is now in the git working tree and will enter the commit history if pushed.
- **Impact:** Anyone with read access to the repository (including any future public fork or CI log) can obtain working credentials for the SQL Server at `192.168.0.2`. Even if the server is on a private LAN today, committed credentials are effectively permanent once pushed. (CWE-312: Cleartext Storage of Sensitive Information, CWE-798: Use of Hard-coded Credentials)
- **Remediation:** Immediately rotate the `123abc` password on the database server. Replace all three `App.config` entries and the `appsettings.json` entry with the original `localhost`/`password=yourpassword` placeholders. Verify the credential does not appear in any other tracked file.
- **Evidence:**
  ```xml
  <add key="Database" value="Server=192.168.0.2;Application Name=IntegrationTesting;
       Database=IntegrationTests;user=brian;password=123abc;max pool size=500;
       TrustServerCertificate=True;" />
  ```

**[I2] All-zeroes TripleDES key and IV committed to appsettings.json**
- **Location:** `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json:13-14`
- **Description:** The TripleDES interceptor `Key` and `IV` fields were changed from the documentation placeholder strings (`your-base64-key-here`, `your-base64-iv-here`) to the value `"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"` (key) and `"aaaaaaaaaaa="` (IV). These decode to all-zero bytes — a well-known degenerate case for block ciphers that provides no cryptographic strength. The same values appear in the untracked `appsettings - Copy.json`.
- **Impact:** Any queue payload using the TripleDES interceptor with this key is trivially decryptable by anyone who reads the config file. The original placeholders were safe precisely because they were non-functional; these values are functional but useless cryptographically. (CWE-321: Use of Hard-coded Cryptographic Key)
- **Remediation:** Restore `Key` and `IV` to their original placeholder strings. The sample documentation should direct users to generate a real key with a provided script or `openssl` command rather than filling in static values.
- **Evidence:**
  ```json
  "TripleDes": {
    "Enabled": true,
    "Key": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "IV": "aaaaaaaaaaa="
  }
  ```

**[I3] Developer-specific internal IP addresses and database names throughout App.config files**
- **Location:** All 36 `App.config` files across all five transports; `appsettings.json`
- **Description:** Every `App.config` that was changed replaced a `localhost`-based placeholder connection string with a real internal network address (`192.168.0.2`, `192.168.0.58`) and real database/schema names (`IntegrationTests`, `integrationtesting`, `TestR`). These are sample files intended to ship as templates for end users. The changes also introduced a real username (`userid=brian`) in the PostgreSQL strings.
- **Impact:** Leaks internal network topology and service names. A sample repository reader will attempt to connect to these addresses, which either fails (exposing that a private network exists at this range) or succeeds if the port is somehow reachable. The username `brian` identifies an internal account.
- **Remediation:** Revert all `App.config` `Database` connection strings to their original `localhost`-based placeholder values. For PostgreSQL files that had `password=yourpassword` in the original, ensure no password is present in the replacement (the PostgreSQL strings being committed currently omit a password, which is correct for the sample template).
- **Evidence:** Before: `Server=localhost;Port=5432;Database=sampledb;...userid=postgres;password=yourpassword`; After: `Server=192.168.0.2;Port=5432;Database=integrationtesting;...userid=brian`

### Advisory

- `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings - Copy.json` (untracked) — This file contains the same live credentials and weak TripleDES key as `appsettings.json`. It is currently untracked but sits in the repository working directory. Delete it before staging and add a `.gitignore` rule such as `*Copy*.json` or `appsettings.*.json` to prevent accidental future commits.
- `tracesettings.json` (all 36 files) — All files changed the Jaeger agent port from `6831` to `4319`. This is a valid port change (from the legacy UDP compact thrift port to the OTLP gRPC port), not a security concern. The `JAEGER_AGENT_HOST` value (`192.168.0.2`) remains a developer-specific IP that should be reset to `localhost` for the sample template, for the same reason as the database connection strings.
- `SQLServerConsumer/App.config` — `EnableDashboard` was changed from `false` to `true`. This is not a security issue on its own, but if left in the committed file, every user who clones the sample will have dashboard reporting enabled by default, pointing at `https://localhost:32906`. Consider whether `false` is the safer default for a public sample.

---

## Cross-Component Analysis

**History.Enabled refactor is internally consistent.** The removal of `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory` from all 21 consumer `Program.cs` files is uniform across all five transports. Redis is a special case: History is now configured on the transport options object (`RedisBaseTransportOptions.EnableHistory`) in both Redis producer files, which is the correct location per the 0.9.11 API. The `App.config` key `EnableHistory` is still read by `SharedConfiguration` and the value is passed through. The feature toggle works end-to-end; only the call site moved. No transport was inadvertently left without the option wired up.

**Config contamination is the dominant systemic issue.** The code-level changes (Program.cs deletions, csproj version bumps) are clean. Every security concern in this phase originates from developer testing state bleeding into committed configuration files. The pattern is consistent: real IPs replace `localhost`, real credentials replace `yourpassword`, real database names replace `SampleDb`/`sampledb`. This suggests the developer ran a global find-and-replace or committed all modified files without reviewing config diffs. A pre-commit hook that rejects `App.config` or `appsettings.json` changes containing non-`localhost` hostnames or non-placeholder passwords would prevent this class of issue.

**appsettings.json and appsettings - Copy.json are identical in content.** The untracked copy file and the tracked `appsettings.json` share the same live credentials and weak keys, confirming the copy was made during testing. The copy file is not staged but is at risk of accidental commit.

---

## Analysis Coverage

| Area | Checked | Notes |
|------|---------|-------|
| Code Security (OWASP) | Yes | No injection, auth bypass, or deserialization issues in changed code |
| Secrets & Credentials | Yes | Real credentials found in App.config and appsettings.json diffs |
| Dependencies | Yes | 37 packages bumped from 0.9.10 to 0.9.11 (author's own library); no third-party dependency changes |
| Infrastructure as Code | N/A | No IaC files in scope |
| Docker/Container | N/A | No Dockerfiles in scope |
| Configuration | Yes | App.config and appsettings.json changes fully reviewed |

---

## Dependency Status

All changed packages are first-party (authored by the repository owner). No NuGet advisory database entries exist for any DotNetWorkQueue 0.9.x version. Third-party dependencies (LiteDB, OpenTelemetry, App.Metrics, Serilog, Polly, SimpleInjector, Microsoft.* packages) were not changed in this phase.

| Package | Old Version | New Version | Known CVEs | Status |
|---------|------------|------------|-----------|--------|
| DotNetWorkQueue | 0.9.10 | 0.9.11 | None | OK |
| DotNetWorkQueue.Transport.LiteDb | 0.9.10 | 0.9.11 | None | OK |
| DotNetWorkQueue.Transport.PostgreSQL | 0.9.10 | 0.9.11 | None | OK |
| DotNetWorkQueue.Transport.Redis | 0.9.10 | 0.9.11 | None | OK |
| DotNetWorkQueue.Transport.SqlServer | 0.9.10 | 0.9.11 | None | OK |
| DotNetWorkQueue.Transport.SQLite | 0.9.10 | 0.9.11 | None | OK |
| DotNetWorkQueue.Dashboard.Client | 0.9.10 | 0.9.11 | None | OK |

---

## IaC Findings

N/A — No infrastructure-as-code files were modified in this phase.
