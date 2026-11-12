# SqlClient linked-server authentication repro

Minimal console app to compare Microsoft.Data.SqlClient versions outside VS Code. It connects with Integrated authentication, prints the local identity/auth scheme, asks the linked server which identity and database access it sees, then probes one remote object without reading rows.

## Requirements

- macOS with a valid Kerberos ticket (`klist`)
- .NET 9 SDK/runtime to build and run the project
- Network and permissions for a primary SQL Server and a linked server
- An existing Kerberos/delegation environment; no self-contained database fixture is included

The project targets .NET 9 because that SDK was installed in the reporter's environment. The SqlClient package version is independently selectable. The app isolates the relevant connection path but does not exactly recreate the VS Code extension hosts: mssql 1.39 bundled .NET 8, whereas 1.47 used .NET 10. .NET 8 has not been tested with this sample.

## Configure

Run from the repository directory. Set these environment variables in a terminal; do not commit real hostnames or credentials.

```sh
export SQL_SERVER='<primary SQL Server host>'
export SQL_DATABASE='<primary database>'
export SQL_LINKED_SERVER='<linked server name configured on the primary server>'
export SQL_REMOTE_DATABASE='<database on that linked server>'
export SQL_REMOTE_SCHEMA='dbo'
export SQL_REMOTE_TABLE='<remote table>'
export SQL_REMOTE_COLUMN='<column used by the probe>'
```

The queries are read-only. The four-part-name probe uses `TOP (0)` and returns no table rows. The app stops on the first exception; if the linked-server identity query fails, it will not execute the object probe. Output can contain internal hostnames and identities: redact it before sharing publicly.

The connection currently uses encryption with `TrustServerCertificate = true` for this diagnostic sample, bypassing server certificate validation. Review that setting for your environment; it is not production security guidance.

## Run version comparisons

The default package is Microsoft.Data.SqlClient 6.1.5:

```sh
dotnet run
```

Compare against 5.1.7 without editing the project:

```sh
dotnet run -p:SqlClientVersion=5.1.7
```

The program prints the NuGet package version, first-hop auth scheme and identity, remote execution identity/database access, and any error. Keep the primary server, linked target, and other settings identical between package runs. Record the runtime, run order, and any environment changes. Different linked-server targets can behave differently: a passing target does not establish that all paths in a larger script work.

### Run on an existing .NET 10 runtime

The November 12 tests built the net9.0 project and executed its DLL using the .NET 10.0.0 arm64 host already installed for VS Code, with explicit major-version roll-forward. This does not recompile the app for net10.0.

Set `DOTNET10` to the absolute path of your existing .NET 10 `dotnet` executable. Verify the runtime before running:

```sh
export DOTNET10='<absolute path to the .NET 10 dotnet executable>'
"$DOTNET10" --list-runtimes
dotnet build -p:SqlClientVersion=6.1.5
DOTNET_ROLL_FORWARD=Major "$DOTNET10" bin/Debug/net9.0/SqlClientLinkedServerRepro.dll
dotnet build -p:SqlClientVersion=5.1.7
DOTNET_ROLL_FORWARD=Major "$DOTNET10" bin/Debug/net9.0/SqlClientLinkedServerRepro.dll
```

Use a host whose runtime listing matches the runtime you intend to test. The app prints the selected package version, not the actual runtime version.

## Test results

Internal server, database, and account identifiers are intentionally omitted.

### November 12: extension full-script comparison

The original full script succeeded in mssql 1.39.0. After upgrading to 1.47.0 and reloading, the connection check still showed the same primary server/database, first-hop Kerberos, and domain identity, but the script failed. Separate zero-row probes in 1.47 showed the SharedLoanData/ServicingPerson target succeeded, the Vehicle target failed with error 18456 (`NT AUTHORITY\\ANONYMOUS LOGON`), and the DATAMART target failed with error 7314.

The user then downgraded to 1.39.0, reloaded, ran the Vehicle zero-row probe first, and ran the full original script; both succeeded. This is a same-day, same-primary-connection extension-version comparison. The exact local process state, linked-server state, or other transient state that changed remains unknown.

### November 12: standalone package/runtime comparison on the failing Vehicle target

Environment: macOS 27.0.1, M3 MacBook Pro / arm64, primary SQL Server 2019. The standalone sample was pointed at the same failing linked-server target:

| Microsoft.Data.SqlClient NuGet package | .NET 9.0.3 | .NET 10.0.0 |
| --- | --- | --- |
| 5.1.7 | Pass | Pass |
| 6.1.5 | Error 18456: `NT AUTHORITY\\ANONYMOUS LOGON` | Error 18456: `NT AUTHORITY\\ANONYMOUS LOGON` |

All four standalone runs used the same primary server, linked target, and environment-variable settings. First-hop authentication succeeded with `KERBEROS` and the expected domain identity. With 5.1.7, the linked-server identity was also correct, `HAS_DBACCESS = 1`, and the four-part-name `SELECT TOP (0)` probe succeeded. With 6.1.5, the linked-server identity query itself failed with error 18456; the object probe was not reached.

Test order: 6.1.5 then 5.1.7 on .NET 9, followed by 6.1.5 then 5.1.7 on .NET 10. The newer package failed even after the older package had succeeded. No extension, ticket, or server configuration changes were made between these four runs. This is one four-run comparison, not a long-running repeatability test.

The extension comparison and standalone package comparison now both show 1.39/5.1.7 succeeding and 1.47/6.1.5 failing on the Vehicle target. This strongly supports a version-related regression, though the exact delegation defect and any interaction with transient server state remain unproven. The standalone .NET 10 run used a net9.0-targeted DLL with explicit runtime roll-forward; it is not a retargeted .NET 10 build. .NET 8 has not been tested with the sample.

The detailed result is in [SqlClient #5040's November 12 update](https://github.com/dotnet/SqlClient/issues/5040#issuecomment-6837583547); the [companion extension issue](https://github.com/microsoft/vscode-mssql/issues/23256) tracks the extension behavior.

### November 11: earlier target and transient results

Earlier standalone tests used a different linked-server target on .NET 9.0.3. Both packages initially showed an anonymous remote identity, database access `0`, and error 7314. Later that day, both showed the expected remote identity, access `1`, and a successful probe while extension 1.39 was running. The reason for that change is unknown; connection or credential reuse was not ruled out.

Those observations remain valid historical results for that target, but they did not test the Vehicle target that distinguished versions on November 12. See the [earlier issue update](https://github.com/dotnet/SqlClient/issues/5040#issuecomment-6807269935) for that test history.
