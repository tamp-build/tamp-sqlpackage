# Tamp.SqlPackage

> Typed wrappers for the `sqlpackage` CLI (Microsoft.SqlPackage): `.dacpac` Publish / Extract / Script for SQL Server and Azure SQL. Cross-platform — `sqlpackage` ships as a dotnet tool and runs on Windows, macOS, and Linux.

| Package | Status |
|---|---|
| `Tamp.SqlPackage` | 0.1.0 (initial) |

## Install

```bash
dotnet add package Tamp.SqlPackage
```

Multi-targets net8 / net9 / net10. Requires `sqlpackage` on PATH (install with `dotnet tool install -g Microsoft.SqlPackage`).

## Quick start — publish a .dacpac

```csharp
using Tamp;
using Tamp.SqlPackage;

class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [FromPath("sqlpackage")] readonly Tool SqlPackage = null!;
    [Parameter] readonly Secret TargetConn = null!; // sourced from env or .tamp/secrets

    Target Publish => _ => _.Executes(() => SqlPackage.Publish(SqlPackage, s => s
        .SetSourceFile("artifacts/MyDb.dacpac")
        .SetTargetConnectionString(TargetConn)
        .SetProperty("BlockOnPossibleDataLoss", "false")));
}
```

## Verb surface (v1)

| Verb | Wraps | Required |
|---|---|---|
| `Publish` | `/Action:Publish` | `SourceFile`, target (connection string OR server+database) |
| `Extract` | `/Action:Extract` | `TargetFile`, source (connection string OR server+database) |
| `Script` | `/Action:Script` | `SourceFile`, `OutputPath`, target (connection string OR server+database) |

Held for v2: `Export`, `Import`, `DeployReport`, `DriftReport`. File an issue if you need one.

## Common knobs (every verb)

- **Connection vs. server/database split** — supply either a full `Secret` connection string OR a plain server name + database name. Connection strings are always tracked as `Secret` and redacted from logs / plan emission.
- **Properties** — `/p:Name=Value` repeatable, set via `.SetProperty(name, value)`.
- **SqlCmd variables** — `/v:Name=Value` repeatable, set via `.SetVariable(name, value)`.
- **Access token** — `/AccessToken:` for Azure AD token-based auth, accepts a `Secret`.
- **Profile** — `/Profile:publish.profile.xml` for DAC Publish Profile.
- **Output controls** — `OverwriteFiles`, `Quiet`, `Diagnostics`, `DiagnosticsFile`.

## Settings authoring — fluent or object-init

Both styles produce identical `CommandPlan`s. Fluent is canonical in docs / `tamp init` templates:

```csharp
// Fluent
SqlPackage.Publish(SqlPackage, s => s
    .SetSourceFile("a.dacpac")
    .SetTargetConnectionString(conn));

// Object-init
SqlPackage.Publish(SqlPackage, new PublishSettings
{
    SourceFile = "a.dacpac",
    TargetConnectionString = conn,
});
```

## Cross-platform note

`sqlpackage` ships as the `Microsoft.SqlPackage` dotnet global tool and runs on Windows, macOS, and Linux. Argument format is Windows-style `/Name:Value` single-token (not POSIX `--name value` pairs); each flag is one process arg.

## License

MIT — see [LICENSE](LICENSE).
