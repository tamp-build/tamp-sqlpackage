namespace Tamp.SqlPackage;

/// <summary>
/// Common knobs shared by every sqlpackage verb. Connection-string handling, secret
/// collection, and the standard <c>/Quiet</c> / <c>/Diagnostics</c> / <c>/OverwriteFiles</c>
/// switches all live here. Per-verb subclasses contribute the <c>/Action:</c> token
/// and verb-specific source/target arguments.
/// </summary>
/// <remarks>
/// sqlpackage uses Windows-style <c>/Name:Value</c> single-token arguments, NOT
/// POSIX <c>--name value</c> pairs. Each flag is one element in <see cref="CommandPlan.Arguments"/>.
/// </remarks>
public abstract class SqlPackageSettingsBase
{
    /// <summary>Working directory for the spawned <c>sqlpackage</c> process.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Per-invocation environment variables.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    /// <summary>Suppress detailed feedback (<c>/Quiet:True</c>). Default false.</summary>
    public bool Quiet { get; set; }

    /// <summary>Emit diagnostic logs to the console (<c>/Diagnostics:True</c>). Default false.</summary>
    public bool Diagnostics { get; set; }

    /// <summary>Path for diagnostic log file (<c>/DiagnosticsFile:</c>).</summary>
    public string? DiagnosticsFile { get; set; }

    /// <summary>Overwrite existing output files (<c>/OverwriteFiles:</c>). Default true (CLI default).</summary>
    public bool? OverwriteFiles { get; set; }

    /// <summary>Path to a DAC Publish Profile (<c>/Profile:</c>).</summary>
    public string? Profile { get; set; }

    /// <summary>
    /// Action-specific properties (<c>/p:Name=Value</c>). Repeatable.
    /// </summary>
    public Dictionary<string, string> Properties { get; } = new();

    /// <summary>
    /// Action-specific SqlCmd variables (<c>/v:Name=Value</c>). Repeatable.
    /// </summary>
    public Dictionary<string, string> Variables { get; } = new();

    /// <summary>
    /// Azure AD access token for token-based auth (<c>/AccessToken:</c>). Tracked as
    /// <see cref="Secret"/> so it's redacted from logs / plan emission.
    /// </summary>
    public Secret? AccessToken { get; set; }

    /// <summary>Subclasses produce the <c>/Action:</c> token plus verb-specific args.</summary>
    protected abstract IEnumerable<string> BuildVerbArguments();

    /// <summary>Subclasses extending the secret list (typically connection strings).</summary>
    protected virtual IEnumerable<Secret> CollectSecrets()
    {
        if (AccessToken is not null) yield return AccessToken;
    }

    /// <summary>Per-verb validation hook. Throw <see cref="InvalidOperationException"/> on missing required args.</summary>
    protected virtual void Validate() { }

    internal CommandPlan ToCommandPlan(Tool tool)
    {
        Validate();

        var args = new List<string>();
        args.AddRange(BuildVerbArguments());

        if (!string.IsNullOrEmpty(Profile)) args.Add($"/Profile:{Profile}");
        if (OverwriteFiles is bool ovw) args.Add($"/OverwriteFiles:{(ovw ? "True" : "False")}");
        if (Quiet) args.Add("/Quiet:True");
        if (Diagnostics) args.Add("/Diagnostics:True");
        if (!string.IsNullOrEmpty(DiagnosticsFile)) args.Add($"/DiagnosticsFile:{DiagnosticsFile}");
        if (AccessToken is not null) args.Add($"/AccessToken:{AccessToken.Reveal()}");

        foreach (var kv in Properties) args.Add($"/p:{kv.Key}={kv.Value}");
        foreach (var kv in Variables) args.Add($"/v:{kv.Key}={kv.Value}");

        return new CommandPlan
        {
            Executable = tool.Executable.Value,
            Arguments = args,
            Environment = new Dictionary<string, string>(EnvironmentVariables),
            WorkingDirectory = WorkingDirectory ?? tool.WorkingDirectory,
            Secrets = CollectSecrets().ToList(),
        };
    }
}

/// <summary>Fluent setters for the common knobs.</summary>
public static class SqlPackageSettingsBaseExtensions
{
    public static T SetWorkingDirectory<T>(this T s, string? cwd) where T : SqlPackageSettingsBase { s.WorkingDirectory = cwd; return s; }
    public static T SetQuiet<T>(this T s, bool v = true) where T : SqlPackageSettingsBase { s.Quiet = v; return s; }
    public static T SetDiagnostics<T>(this T s, bool v = true) where T : SqlPackageSettingsBase { s.Diagnostics = v; return s; }
    public static T SetDiagnosticsFile<T>(this T s, string? path) where T : SqlPackageSettingsBase { s.DiagnosticsFile = path; return s; }
    public static T SetOverwriteFiles<T>(this T s, bool v = true) where T : SqlPackageSettingsBase { s.OverwriteFiles = v; return s; }
    public static T SetProfile<T>(this T s, string? path) where T : SqlPackageSettingsBase { s.Profile = path; return s; }
    public static T SetAccessToken<T>(this T s, Secret token) where T : SqlPackageSettingsBase { s.AccessToken = token; return s; }
    public static T SetEnvironmentVariable<T>(this T s, string name, string value) where T : SqlPackageSettingsBase { s.EnvironmentVariables[name] = value; return s; }
    public static T SetProperty<T>(this T s, string name, string value) where T : SqlPackageSettingsBase { s.Properties[name] = value; return s; }
    public static T SetVariable<T>(this T s, string name, string value) where T : SqlPackageSettingsBase { s.Variables[name] = value; return s; }
}

/// <summary>
/// Settings for <c>sqlpackage /Action:Publish</c> — apply a .dacpac to a target database.
/// Source is a .dacpac file (or .bacpac for some paths); target is a live connection.
/// </summary>
public sealed class PublishSettings : SqlPackageSettingsBase
{
    /// <summary>Source .dacpac (<c>/SourceFile:</c>). Required.</summary>
    public string? SourceFile { get; set; }

    /// <summary>Target connection string (<c>/TargetConnectionString:</c>). Required unless TargetServerName + TargetDatabaseName are set.</summary>
    public Secret? TargetConnectionString { get; set; }

    /// <summary>Target server (<c>/TargetServerName:</c>). Alternative to <see cref="TargetConnectionString"/>.</summary>
    public string? TargetServerName { get; set; }

    /// <summary>Target database name (<c>/TargetDatabaseName:</c>). Required when using TargetServerName.</summary>
    public string? TargetDatabaseName { get; set; }

    /// <summary>Output the resolved deployment script alongside the publish (<c>/DeployScriptPath:</c>).</summary>
    public string? DeployScriptPath { get; set; }

    /// <summary>Output the deployment report XML alongside the publish (<c>/DeployReportPath:</c>).</summary>
    public string? DeployReportPath { get; set; }

    public PublishSettings SetSourceFile(string path) { SourceFile = path; return this; }
    public PublishSettings SetTargetConnectionString(Secret connStr) { TargetConnectionString = connStr; return this; }
    public PublishSettings SetTargetServerName(string server) { TargetServerName = server; return this; }
    public PublishSettings SetTargetDatabaseName(string db) { TargetDatabaseName = db; return this; }
    public PublishSettings SetDeployScriptPath(string? path) { DeployScriptPath = path; return this; }
    public PublishSettings SetDeployReportPath(string? path) { DeployReportPath = path; return this; }

    protected override void Validate()
    {
        if (string.IsNullOrEmpty(SourceFile))
            throw new InvalidOperationException("SourceFile is required for Publish (set via SetSourceFile).");
        if (TargetConnectionString is null && string.IsNullOrEmpty(TargetServerName))
            throw new InvalidOperationException("Publish requires either TargetConnectionString or TargetServerName.");
        if (TargetConnectionString is null && string.IsNullOrEmpty(TargetDatabaseName))
            throw new InvalidOperationException("Publish with TargetServerName also requires TargetDatabaseName.");
    }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "/Action:Publish";
        yield return $"/SourceFile:{SourceFile}";
        if (TargetConnectionString is not null)
        {
            yield return $"/TargetConnectionString:{TargetConnectionString.Reveal()}";
        }
        else
        {
            yield return $"/TargetServerName:{TargetServerName}";
            yield return $"/TargetDatabaseName:{TargetDatabaseName}";
        }
        if (!string.IsNullOrEmpty(DeployScriptPath)) yield return $"/DeployScriptPath:{DeployScriptPath}";
        if (!string.IsNullOrEmpty(DeployReportPath)) yield return $"/DeployReportPath:{DeployReportPath}";
    }

    protected override IEnumerable<Secret> CollectSecrets()
    {
        foreach (var s in base.CollectSecrets()) yield return s;
        if (TargetConnectionString is not null) yield return TargetConnectionString;
    }
}

/// <summary>
/// Settings for <c>sqlpackage /Action:Extract</c> — produce a .dacpac from a live database.
/// Source is a live connection; target is the .dacpac output file.
/// </summary>
public sealed class ExtractSettings : SqlPackageSettingsBase
{
    /// <summary>Output .dacpac path (<c>/TargetFile:</c>). Required.</summary>
    public string? TargetFile { get; set; }

    /// <summary>Source connection string (<c>/SourceConnectionString:</c>). Required unless SourceServerName + SourceDatabaseName are set.</summary>
    public Secret? SourceConnectionString { get; set; }

    /// <summary>Source server (<c>/SourceServerName:</c>). Alternative to <see cref="SourceConnectionString"/>.</summary>
    public string? SourceServerName { get; set; }

    /// <summary>Source database name (<c>/SourceDatabaseName:</c>). Required when using SourceServerName.</summary>
    public string? SourceDatabaseName { get; set; }

    public ExtractSettings SetTargetFile(string path) { TargetFile = path; return this; }
    public ExtractSettings SetSourceConnectionString(Secret connStr) { SourceConnectionString = connStr; return this; }
    public ExtractSettings SetSourceServerName(string server) { SourceServerName = server; return this; }
    public ExtractSettings SetSourceDatabaseName(string db) { SourceDatabaseName = db; return this; }

    protected override void Validate()
    {
        if (string.IsNullOrEmpty(TargetFile))
            throw new InvalidOperationException("TargetFile is required for Extract (set via SetTargetFile).");
        if (SourceConnectionString is null && string.IsNullOrEmpty(SourceServerName))
            throw new InvalidOperationException("Extract requires either SourceConnectionString or SourceServerName.");
        if (SourceConnectionString is null && string.IsNullOrEmpty(SourceDatabaseName))
            throw new InvalidOperationException("Extract with SourceServerName also requires SourceDatabaseName.");
    }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "/Action:Extract";
        yield return $"/TargetFile:{TargetFile}";
        if (SourceConnectionString is not null)
        {
            yield return $"/SourceConnectionString:{SourceConnectionString.Reveal()}";
        }
        else
        {
            yield return $"/SourceServerName:{SourceServerName}";
            yield return $"/SourceDatabaseName:{SourceDatabaseName}";
        }
    }

    protected override IEnumerable<Secret> CollectSecrets()
    {
        foreach (var s in base.CollectSecrets()) yield return s;
        if (SourceConnectionString is not null) yield return SourceConnectionString;
    }
}

/// <summary>
/// Settings for <c>sqlpackage /Action:Script</c> — produce a deployment T-SQL script without applying.
/// Source is a .dacpac; target is a live connection (for schema-diff baselining); output is the .sql file.
/// </summary>
public sealed class ScriptSettings : SqlPackageSettingsBase
{
    /// <summary>Source .dacpac (<c>/SourceFile:</c>). Required.</summary>
    public string? SourceFile { get; set; }

    /// <summary>Output .sql script path (<c>/OutputPath:</c>). Required.</summary>
    public string? OutputPath { get; set; }

    /// <summary>Target connection string (<c>/TargetConnectionString:</c>). Required unless TargetServerName + TargetDatabaseName are set.</summary>
    public Secret? TargetConnectionString { get; set; }

    /// <summary>Target server (<c>/TargetServerName:</c>). Alternative to <see cref="TargetConnectionString"/>.</summary>
    public string? TargetServerName { get; set; }

    /// <summary>Target database name (<c>/TargetDatabaseName:</c>). Required when using TargetServerName.</summary>
    public string? TargetDatabaseName { get; set; }

    public ScriptSettings SetSourceFile(string path) { SourceFile = path; return this; }
    public ScriptSettings SetOutputPath(string path) { OutputPath = path; return this; }
    public ScriptSettings SetTargetConnectionString(Secret connStr) { TargetConnectionString = connStr; return this; }
    public ScriptSettings SetTargetServerName(string server) { TargetServerName = server; return this; }
    public ScriptSettings SetTargetDatabaseName(string db) { TargetDatabaseName = db; return this; }

    protected override void Validate()
    {
        if (string.IsNullOrEmpty(SourceFile))
            throw new InvalidOperationException("SourceFile is required for Script (set via SetSourceFile).");
        if (string.IsNullOrEmpty(OutputPath))
            throw new InvalidOperationException("OutputPath is required for Script (set via SetOutputPath).");
        if (TargetConnectionString is null && string.IsNullOrEmpty(TargetServerName))
            throw new InvalidOperationException("Script requires either TargetConnectionString or TargetServerName.");
        if (TargetConnectionString is null && string.IsNullOrEmpty(TargetDatabaseName))
            throw new InvalidOperationException("Script with TargetServerName also requires TargetDatabaseName.");
    }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "/Action:Script";
        yield return $"/SourceFile:{SourceFile}";
        yield return $"/OutputPath:{OutputPath}";
        if (TargetConnectionString is not null)
        {
            yield return $"/TargetConnectionString:{TargetConnectionString.Reveal()}";
        }
        else
        {
            yield return $"/TargetServerName:{TargetServerName}";
            yield return $"/TargetDatabaseName:{TargetDatabaseName}";
        }
    }

    protected override IEnumerable<Secret> CollectSecrets()
    {
        foreach (var s in base.CollectSecrets()) yield return s;
        if (TargetConnectionString is not null) yield return TargetConnectionString;
    }
}
