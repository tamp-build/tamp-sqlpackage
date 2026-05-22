namespace Tamp.SqlPackage;

/// <summary>
/// Typed wrappers for the <c>sqlpackage</c> CLI (<c>Microsoft.SqlPackage</c> dotnet tool).
/// v1 covers the three verbs that hit ~90% of build-pipeline use: Publish, Extract, Script.
/// Export / Import / DeployReport / DriftReport are intentionally deferred to v2.
/// </summary>
/// <remarks>
/// <code>
/// [FromPath("sqlpackage")] readonly Tool SqlPackage = null!;
///
/// Target PublishDacpac => _ => _.Executes(() => SqlPackage.Publish(SqlPackage, s => s
///     .SetSourceFile("artifacts/MyDb.dacpac")
///     .SetTargetConnectionString(SecretFromEnv("DB_CONN"))
///     .SetProperty("BlockOnPossibleDataLoss", "false")));
/// </code>
/// </remarks>
public static class SqlPackage
{
    /// <summary><c>sqlpackage /Action:Publish</c> — apply a .dacpac to a target database.</summary>
    public static CommandPlan Publish(Tool tool, Action<PublishSettings> configure)
        => Build<PublishSettings>(tool, configure);

    /// <summary><c>sqlpackage /Action:Extract</c> — produce a .dacpac from a live database.</summary>
    public static CommandPlan Extract(Tool tool, Action<ExtractSettings> configure)
        => Build<ExtractSettings>(tool, configure);

    /// <summary><c>sqlpackage /Action:Script</c> — produce a deployment T-SQL script without applying.</summary>
    public static CommandPlan Script(Tool tool, Action<ScriptSettings> configure)
        => Build<ScriptSettings>(tool, configure);

    // ---- Object-init overloads (parallel surface; both styles produce identical CommandPlans) ----
    public static CommandPlan Publish(Tool tool, PublishSettings settings) => Plan(tool, settings);
    public static CommandPlan Extract(Tool tool, ExtractSettings settings) => Plan(tool, settings);
    public static CommandPlan Script(Tool tool, ScriptSettings settings) => Plan(tool, settings);

    private static CommandPlan Build<T>(Tool tool, Action<T>? configure) where T : SqlPackageSettingsBase, new()
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        var s = new T();
        configure?.Invoke(s);
        return s.ToCommandPlan(tool);
    }

    private static CommandPlan Plan<T>(Tool tool, T settings) where T : SqlPackageSettingsBase
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        return settings.ToCommandPlan(tool);
    }
}
