namespace CoolGeek.PersianText;

/// <summary>
/// Finds banned words in text, even when disguised: "ت.ب.ل.ی.غ", "تبلیـــغ", "تبليغ" (Arabic ي)
/// and "casssino" all match "تبلیغ" / "casino". Build one filter per word list and reuse it.
/// </summary>
/// <remarks>
/// Words of <see cref="MinSubstringLength"/> letters or more match anywhere, even inside longer words or split
/// by dots and spaces. Shorter words match only as whole words, so a short banned word does not hit every
/// innocent word that happens to contain it.
/// </remarks>
public sealed class WordFilter
{
    /// <summary>Words at least this long are matched inside other text; shorter ones only as whole words.</summary>
    public const int MinSubstringLength = 4;

    private readonly Entry[] _entries;

    private sealed record Entry(string Original, string Collapsed, string Squashed, string PaddedWord, bool MatchInside);

    public WordFilter(IEnumerable<string> words)
    {
        _entries = words
            .Select(w => (Original: w, Normalized: NormalizedText.From(w)))
            .Where(w => !w.Normalized.IsEmpty)
            .Select(w => new Entry(
                w.Original,
                w.Normalized.Collapsed,
                w.Normalized.Squashed,
                " " + NormalizedText.PunctuationToSpace(w.Normalized.Collapsed) + " ",
                w.Normalized.Squashed.Length >= MinSubstringLength))
            .ToArray();
    }

    /// <summary>Number of usable words in the filter.</summary>
    public int Count => _entries.Length;

    /// <summary>Returns the first banned word (as originally given) found in the text, or null.</summary>
    public string? FindMatch(NormalizedText text)
    {
        if (text.IsEmpty) return null;

        foreach (var entry in _entries)
        {
            var hit = entry.MatchInside
                ? text.Collapsed.Contains(entry.Collapsed, StringComparison.Ordinal)
                  || text.Squashed.Contains(entry.Squashed, StringComparison.Ordinal)
                : text.PaddedWords.Contains(entry.PaddedWord, StringComparison.Ordinal);

            if (hit) return entry.Original;
        }
        return null;
    }

    /// <inheritdoc cref="FindMatch(NormalizedText)"/>
    public string? FindMatch(string? raw) => FindMatch(NormalizedText.From(raw));
}
