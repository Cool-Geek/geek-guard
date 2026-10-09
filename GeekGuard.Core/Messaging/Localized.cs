namespace GeekGuard.Core.Messaging;

/// <summary>
/// One piece of user-facing text in both languages. Placeholders follow string.Format: {0}, {1}…
/// </summary>
/// <example><c>static readonly Localized Banned = new("⛔ {0} بن شد.", "⛔ {0} was banned.");</c></example>
public readonly record struct Localized(string Fa, string En)
{
    public string Get(string lang) => lang == Languages.English ? En : Fa;

    public string Format(string lang, params object?[] args) => string.Format(Get(lang), args);
}
