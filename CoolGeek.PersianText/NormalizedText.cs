using System.Text;

namespace CoolGeek.PersianText;

/// <summary>
/// A message normalized once, with the derived forms the detectors need computed on first use.
/// Create it once per message and pass it to every check.
/// </summary>
public sealed class NormalizedText
{
    private string? _collapsed;
    private string? _squashed;
    private string? _paddedWords;

    private NormalizedText(string value) => Value = value;

    /// <summary>Normalizes <paramref name="raw"/> with <see cref="PersianNormalizer.Normalize"/>.</summary>
    public static NormalizedText From(string? raw) => new(PersianNormalizer.Normalize(raw));

    public static NormalizedText Empty { get; } = new("");

    /// <summary>The normalized text.</summary>
    public string Value { get; }

    public bool IsEmpty => Value.Length == 0;

    /// <summary><see cref="Value"/> with repeated letters collapsed: "سلاااام" → "سلام".</summary>
    public string Collapsed => _collapsed ??= PersianNormalizer.CollapseRepeats(Value);

    /// <summary><see cref="Collapsed"/> with everything but letters and digits removed.</summary>
    public string Squashed => _squashed ??= PersianNormalizer.Squash(Collapsed);

    /// <summary>
    /// <see cref="Collapsed"/> with punctuation turned into spaces and a space at both ends,
    /// so whole words can be found with a plain search for " word ".
    /// </summary>
    internal string PaddedWords => _paddedWords ??= " " + PunctuationToSpace(Collapsed) + " ";

    internal static string PunctuationToSpace(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text) sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        return sb.ToString();
    }

    public override string ToString() => Value;
}
