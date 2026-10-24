namespace ShipFlow.Infrastructure.Persistence;

/// <summary>Startup database behavior, bound from the <c>Database</c> configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Apply pending EF migrations at startup. Intended for local development only; production
    /// migrations are applied by a deliberate deployment step (see docs/SETUP.md).
    /// </summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Create the platform roles if missing. Idempotent and non-destructive; needed for registration.</summary>
    public bool SeedReferenceData { get; set; } = true;

    /// <summary>Create demo accounts. Honoured only when the host environment is Development.</summary>
    public bool SeedDevelopmentData { get; set; }
}
