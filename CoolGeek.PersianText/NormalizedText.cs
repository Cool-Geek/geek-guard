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
    private string? _joinedWords;

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
    internal string PaddedWords => _paddedWords ??= " " + WordsOnly(Collapsed) + " ";

    /// <summary>
    /// <see cref="PaddedWords"/> with letters spelled out one by one glued back together:
    /// "ت ب ل ی غ" and "ت.ب.ل.ی.غ" → " تبلیغ ". Only runs of two or more single letters are joined,
    /// so an ordinary «و» between two words stays a word of its own.
    /// </summary>
    internal string JoinedWords => _joinedWords ??= " " + JoinSingleLetters(WordsOnly(Collapsed)) + " ";

    private static string JoinSingleLetters(string text)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(tokens.Length);
        var run = new StringBuilder();
        var runLength = 0;

        void Flush()
        {
            if (runLength >= 2) result.Add(PersianNormalizer.CollapseRepeats(run.ToString()));
            else if (runLength == 1) result.Add(run.ToString());
            run.Clear();
            runLength = 0;
        }

        foreach (var token in tokens)
        {
            if (token.Length == 1)
            {
                run.Append(token);
                runLength++;
                continue;
            }
            Flush();
            result.Add(token);
        }
        Flush();
        return string.Join(' ', result);
    }

    /// <summary>The words of <paramref name="text"/> separated by single spaces, punctuation removed.</summary>
    internal static string WordsOnly(string text) =>
        string.Join(' ', PunctuationToSpace(text).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    internal static string PunctuationToSpace(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text) sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        return sb.ToString();
    }

    public override string ToString() => Value;
}
