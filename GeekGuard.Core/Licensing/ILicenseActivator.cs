namespace GeekGuard.Core.Licensing;

/// <summary>What the shop said about an activation code.</summary>
public enum ActivationOutcome
{
    /// <summary>The code is valid and is now tied to this group (or already was).</summary>
    Valid,

    /// <summary>No such code.</summary>
    NotFound,

    /// <summary>The code was already activated in another group.</summary>
    UsedElsewhere,

    /// <summary>The code was cancelled (e.g. refunded).</summary>
    Revoked,

    /// <summary>The shop could not be reached or is not configured; nothing changed.</summary>
    Unavailable,
}

/// <summary>A code the shop accepted: what it unlocks and for how long.</summary>
/// <param name="Code">The code in its standard form, e.g. "GF-7Q2K-91XM-ZP4D".</param>
/// <param name="IsPlugin">False: Pro for the whole group. True: only <paramref name="PluginId"/>.</param>
public sealed record LicenseGrant(string Code, bool IsPlugin, string? PluginId, int Days, string? ProductTitle);

public sealed record ActivationAnswer(ActivationOutcome Outcome, LicenseGrant? Grant = null);

/// <summary>
/// Asks the shop (Geek Forge) whether a code may be used in a group, and ties it to that group.
/// Asking again for the same group returns the same grant, so a retry is safe.
/// </summary>
public interface ILicenseActivator
{
    Task<ActivationAnswer> ActivateAsync(string code, long chatId, string chatTitle, long telegramUserId, CancellationToken ct = default);
}
