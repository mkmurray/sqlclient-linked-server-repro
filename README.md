# SqlClient linked-server authentication repro

Minimal console app to compare Microsoft.Data.SqlClient versions outside VS Code. It connects with Integrated authentication, prints the local identity/auth scheme, asks the linked server which identity and database access it sees, then probes one remote object without reading rows.

## Requirements

- macOS with a valid Kerberos ticket (`klist`)
- .NET 9 SDK/runtime
- Network and permissions for the SQL Server and linked server

The project targets .NET 9 because that runtime was installed in the reporter's environment. This is not an exact reproduction of the extension hosts, which bundled .NET 8 in mssql 1.39 and .NET 10 in mssql 1.47. The SqlClient package version is independently selectable.

## Configure

Set these environment variables in a terminal. Do not commit real hostnames or credentials in this repository.

```sh
export SQL_SERVER='<primary SQL Server host>'
export SQL_DATABASE='<primary database>'
export SQL_LINKED_SERVER='<linked server name>'
export SQL_REMOTE_DATABASE='<remote database>'
export SQL_REMOTE_SCHEMA='dbo'
export SQL_REMOTE_TABLE='<remote table>'
export SQL_REMOTE_COLUMN='<column used by the probe>'
```

The local test queries are read-only. The four-part-name probe uses `TOP (0)` and returns no table rows.

## Run version comparisons

The default package is Microsoft.Data.SqlClient 6.1.5:

```sh
dotnet run
```

Compare against 5.1.7 without editing the project:

```sh
dotnet run -p:SqlClientVersion=5.1.7
```

The program prints the NuGet package version, first-hop auth scheme and identity, remote execution identity/database access, and any four-part-name error. Run each version with the same environment variables and ticket, and record the time/result.

## Current test result

Tested on macOS 27.0.1 / Apple Silicon with .NET 9.0.3 on 2026-11-11 against the reporter's SQL Server and linked server (host and database names intentionally omitted here). SqlClient 5.1.7 and 6.1.5 both produced the same result under this runtime:

- First hop: `KERBEROS`, expected domain login
- Linked-server hop: `NT AUTHORITY\\ANONYMOUS LOGON`; `HAS_DBACCESS` for the remote database returned `0`
- Four-part-name probe: SQL Server error 7314

This standalone comparison reproduces the current access failure but does not demonstrate a SqlClient-version regression. It uses .NET 9, unlike the .NET 8 and .NET 10 runtimes bundled by the two extension versions. A result from this sample is not directly comparable to the VS Code extension unless the runtime and server-side delegation state are also matched.
