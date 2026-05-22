using System.Linq;
using Bogus;
using Tamp;
using Tamp.SqlPackage;
using Xunit;

namespace Tamp.SqlPackage.Tests;

public sealed class SqlPackageTests
{
    private static readonly string FakeToolPath = OperatingSystem.IsWindows() ? "C:\\fake\\sqlpackage.exe" : "/fake/sqlpackage";

    private static Tool FakeTool() => new(AbsolutePath.Create(FakeToolPath));

    private static Secret FakeConn(string name = "TARGET_CONN")
        => new(name, "Server=tcp:fake.database.windows.net;Database=fake;User Id=fake;Password=p@ss;");

    // ---- Publish ----

    [Fact]
    public void Publish_Emits_Action_And_Source_And_Connection()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("artifacts/Db.dacpac")
            .SetTargetConnectionString(FakeConn()));

        Assert.Contains("/Action:Publish", plan.Arguments);
        Assert.Contains("/SourceFile:artifacts/Db.dacpac", plan.Arguments);
        Assert.Contains(plan.Arguments, a => a.StartsWith("/TargetConnectionString:", StringComparison.Ordinal));
    }

    [Fact]
    public void Publish_Action_Comes_First()
    {
        // sqlpackage doesn't care about token order, but emitting /Action: first is the
        // canonical readable shape and makes log-scraping deterministic.
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn()));

        Assert.Equal("/Action:Publish", plan.Arguments[0]);
    }

    [Fact]
    public void Publish_Without_SourceFile_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SqlPackage.Publish(FakeTool(), s => s
            .SetTargetConnectionString(FakeConn())));
        Assert.Contains("SourceFile", ex.Message);
    }

    [Fact]
    public void Publish_Without_Any_Target_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")));
        Assert.Contains("Target", ex.Message);
    }

    [Fact]
    public void Publish_With_ServerName_Requires_DatabaseName()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetServerName("sql-prod.db.local")));
        Assert.Contains("TargetDatabaseName", ex.Message);
    }

    [Fact]
    public void Publish_With_ServerName_And_DatabaseName_Emits_Both()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetServerName("sql-prod.db.local")
            .SetTargetDatabaseName("AppDb"));

        Assert.Contains("/TargetServerName:sql-prod.db.local", plan.Arguments);
        Assert.Contains("/TargetDatabaseName:AppDb", plan.Arguments);
        Assert.DoesNotContain(plan.Arguments, a => a.StartsWith("/TargetConnectionString:", StringComparison.Ordinal));
    }

    [Fact]
    public void Publish_Connection_String_Tracked_As_Secret()
    {
        var conn = FakeConn("PROD_CONN");
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(conn));

        Assert.Contains(plan.Secrets, x => ReferenceEquals(x, conn));
    }

    [Fact]
    public void Publish_AccessToken_Tracked_As_Secret_And_Emitted()
    {
        var token = new Secret("AAD_TOKEN", "eyJ0eXAi...redacted...");
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetServerName("sql-prod.db.local")
            .SetTargetDatabaseName("AppDb")
            .SetAccessToken(token));

        Assert.Contains(plan.Secrets, x => ReferenceEquals(x, token));
        Assert.Contains(plan.Arguments, a => a.StartsWith("/AccessToken:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true, "/OverwriteFiles:True")]
    [InlineData(false, "/OverwriteFiles:False")]
    public void Publish_OverwriteFiles_Emits_Token(bool value, string expected)
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn())
            .SetOverwriteFiles(value));

        Assert.Contains(expected, plan.Arguments);
    }

    [Fact]
    public void Publish_OverwriteFiles_Defaults_Omitted()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn()));
        // sqlpackage's default is True; omitting lets the CLI decide.
        Assert.DoesNotContain(plan.Arguments, a => a.StartsWith("/OverwriteFiles:", StringComparison.Ordinal));
    }

    [Fact]
    public void Publish_Properties_Emit_As_p_Args()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn())
            .SetProperty("BlockOnPossibleDataLoss", "false")
            .SetProperty("CreateNewDatabase", "true"));

        Assert.Contains("/p:BlockOnPossibleDataLoss=false", plan.Arguments);
        Assert.Contains("/p:CreateNewDatabase=true", plan.Arguments);
    }

    [Fact]
    public void Publish_Variables_Emit_As_v_Args()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn())
            .SetVariable("Env", "prod")
            .SetVariable("Tenant", "acme"));

        Assert.Contains("/v:Env=prod", plan.Arguments);
        Assert.Contains("/v:Tenant=acme", plan.Arguments);
    }

    [Fact]
    public void Publish_DeployScriptPath_And_DeployReportPath_Emit()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn())
            .SetDeployScriptPath("artifacts/deploy.sql")
            .SetDeployReportPath("artifacts/deploy.xml"));

        Assert.Contains("/DeployScriptPath:artifacts/deploy.sql", plan.Arguments);
        Assert.Contains("/DeployReportPath:artifacts/deploy.xml", plan.Arguments);
    }

    [Fact]
    public void Publish_Honors_Working_Directory()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn())
            .SetWorkingDirectory("/tmp/build"));

        Assert.Equal("/tmp/build", plan.WorkingDirectory);
    }

    [Fact]
    public void Publish_ObjectInit_Equivalent_To_Fluent()
    {
        var conn = FakeConn();
        var fluent = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(conn)
            .SetProperty("BlockOnPossibleDataLoss", "false"));

        var settings = new PublishSettings
        {
            SourceFile = "a.dacpac",
            TargetConnectionString = conn,
        };
        settings.Properties["BlockOnPossibleDataLoss"] = "false";
        var objInit = SqlPackage.Publish(FakeTool(), settings);

        Assert.Equal(fluent.Arguments, objInit.Arguments);
    }

    // ---- Extract ----

    [Fact]
    public void Extract_Emits_Action_And_TargetFile_And_Source()
    {
        var plan = SqlPackage.Extract(FakeTool(), s => s
            .SetTargetFile("artifacts/snapshot.dacpac")
            .SetSourceConnectionString(FakeConn("SOURCE")));

        Assert.Equal("/Action:Extract", plan.Arguments[0]);
        Assert.Contains("/TargetFile:artifacts/snapshot.dacpac", plan.Arguments);
        Assert.Contains(plan.Arguments, a => a.StartsWith("/SourceConnectionString:", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_Without_TargetFile_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => SqlPackage.Extract(FakeTool(), s => s
            .SetSourceConnectionString(FakeConn())));
    }

    [Fact]
    public void Extract_Without_Source_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => SqlPackage.Extract(FakeTool(), s => s
            .SetTargetFile("a.dacpac")));
    }

    [Fact]
    public void Extract_Source_Connection_Tracked_As_Secret()
    {
        var conn = FakeConn("EXTRACT_FROM");
        var plan = SqlPackage.Extract(FakeTool(), s => s
            .SetTargetFile("a.dacpac")
            .SetSourceConnectionString(conn));

        Assert.Contains(plan.Secrets, x => ReferenceEquals(x, conn));
    }

    // ---- Script ----

    [Fact]
    public void Script_Emits_Action_Source_Output_And_Target()
    {
        var plan = SqlPackage.Script(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetOutputPath("artifacts/deploy.sql")
            .SetTargetConnectionString(FakeConn()));

        Assert.Equal("/Action:Script", plan.Arguments[0]);
        Assert.Contains("/SourceFile:a.dacpac", plan.Arguments);
        Assert.Contains("/OutputPath:artifacts/deploy.sql", plan.Arguments);
        Assert.Contains(plan.Arguments, a => a.StartsWith("/TargetConnectionString:", StringComparison.Ordinal));
    }

    [Fact]
    public void Script_Without_OutputPath_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SqlPackage.Script(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn())));
        Assert.Contains("OutputPath", ex.Message);
    }

    // ---- Cross-cutting ----

    [Fact]
    public void Quiet_Switch_Emits_When_True()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn())
            .SetQuiet());
        Assert.Contains("/Quiet:True", plan.Arguments);
    }

    [Fact]
    public void Quiet_Switch_Omitted_When_False()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn()));
        Assert.DoesNotContain(plan.Arguments, a => a.StartsWith("/Quiet:", StringComparison.Ordinal));
    }

    [Fact]
    public void Diagnostics_Switch_And_File_Emit()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn())
            .SetDiagnostics()
            .SetDiagnosticsFile("artifacts/sqlpackage.log"));

        Assert.Contains("/Diagnostics:True", plan.Arguments);
        Assert.Contains("/DiagnosticsFile:artifacts/sqlpackage.log", plan.Arguments);
    }

    [Fact]
    public void Profile_Emits_When_Set()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn())
            .SetProfile("publish.profile.xml"));

        Assert.Contains("/Profile:publish.profile.xml", plan.Arguments);
    }

    [Fact]
    public void Executable_Is_Tool_Path()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn()));
        Assert.Equal(FakeToolPath, plan.Executable);
    }

    [Fact]
    public void Environment_Variables_Propagate()
    {
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile("a.dacpac")
            .SetTargetConnectionString(FakeConn())
            .SetEnvironmentVariable("FOO", "bar"));

        Assert.True(plan.Environment.TryGetValue("FOO", out var v) && v == "bar");
    }

    // ---- Boundary / fuzz ----

    [Theory]
    [InlineData("path with spaces/Db.dacpac")]
    [InlineData("artifacts/Δb-π.dacpac")]
    [InlineData("artifacts/Db'with'quotes.dacpac")]
    public void Publish_SourceFile_Roundtrips_Through_Arg_Token(string path)
    {
        // sqlpackage takes /Name:Value as a single token; the runner is responsible for
        // shell-escaping when it spawns the process. We just need to confirm the value
        // appears verbatim in the token.
        var plan = SqlPackage.Publish(FakeTool(), s => s
            .SetSourceFile(path)
            .SetTargetConnectionString(FakeConn()));

        Assert.Contains(plan.Arguments, a => a == $"/SourceFile:{path}");
    }

    [Fact]
    public void Bulk_Properties_All_Emit()
    {
        // Bogus-driven volume sanity: a publish settings with 50 random /p:Name=Value pairs
        // round-trips every pair into the args list, in any order.
        var faker = new Faker();
        var pairs = Enumerable.Range(0, 50)
            .Select(_ => (Name: faker.Hacker.Noun() + faker.Random.AlphaNumeric(4), Value: faker.Random.Word()))
            .GroupBy(p => p.Name).Select(g => g.First()) // de-dup random collisions
            .ToList();

        var plan = SqlPackage.Publish(FakeTool(), s =>
        {
            s.SetSourceFile("a.dacpac").SetTargetConnectionString(FakeConn());
            foreach (var (name, value) in pairs) s.SetProperty(name, value);
        });

        foreach (var (name, value) in pairs)
        {
            Assert.Contains($"/p:{name}={value}", plan.Arguments);
        }
    }
}
