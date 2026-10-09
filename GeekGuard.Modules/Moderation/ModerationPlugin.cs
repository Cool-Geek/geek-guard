namespace GeekGuard.Modules.Moderation;

/// <summary>
/// Admin commands: warn, mute, kick, ban and their reverses, as slash commands or as keyword replies
/// ("بن", "سکوت ۲ ساعت"…). Also owns the warning counts that other plugins add to.
/// </summary>
public sealed class ModerationPlugin : IGeekGuardPlugin
{
    public const string Id = "moderation";

    public PluginManifest Manifest { get; } = new(Id, "مدیریت اعضا", "Moderation", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<WarnStore>();
        services.AddSingleton<WarningService>();
        services.AddGroupMessageHandler<ModerationHandler>();
        services.AddPluginMigrations(Id, GetType());
    }
}

/// <summary>The penalty applied when a member reaches the warning limit.</summary>
public enum LimitAction
{
    Mute = 0,
    Kick = 1,
    Ban = 2,
}

/// <summary>Per-group moderation settings, stored as JSON. New fields need a sensible default.</summary>
public sealed class ModerationSettings
{
    /// <summary>Warnings before the penalty applies.</summary>
    public int WarnLimit { get; set; } = 3;

    /// <summary>What happens when the limit is reached.</summary>
    public LimitAction ActionAtLimit { get; set; } = LimitAction.Mute;

    /// <summary>How long the mute at the limit lasts.</summary>
    public int MuteHoursAtLimit { get; set; } = 24;
}
