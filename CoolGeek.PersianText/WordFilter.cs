namespace CoolGeek.PersianText;

/// <summary>
/// Finds banned words in text, even when disguised: "ت.ب.ل.ی.غ", "تبلیـــغ", "تبليغ" (Arabic ي)
/// and "casssino" all match "تبلیغ" / "casino". Build one filter per word list and reuse it.
/// </summary>
/// <remarks>
/// How far a word reaches depends on its length, so short banned words do not hit innocent words that contain them:
/// <list type="bullet">
/// <item>shorter than <see cref="MinPrefixLength"/> letters: whole words only («کس» does not hit «کسی»);</item>
/// <item>from <see cref="MinPrefixLength"/> letters: also at the start of a word, so suffixes are caught
/// («تبلیغ» hits «تبلیغات») but a word that merely contains it is not («کونی» does not hit «مسکونی»);</item>
/// <item>from <see cref="MinSubstringLength"/> letters: anywhere, even split across spaces and dots.</item>
/// </list>
/// Letters spelled out one by one ("ک و ن ی") and repeated letters ("کووووونننیی") are caught at every length.
/// </remarks>
public sealed class WordFilter
{
    /// <summary>Words at least this long also match at the start of a longer word.</summary>
    public const int MinPrefixLength = 4;

    /// <summary>Words at least this long match anywhere in the text.</summary>
    public const int MinSubstringLength = 6;

    private readonly Entry[] _entries;

    private enum Reach { WholeWord, WordStart, Anywhere }

    private sealed record Entry(string Original, string Collapsed, string Squashed, string Padded, Reach Reach);

    public WordFilter(IEnumerable<string> words)
    {
        _entries = words
            .Select(w => (Original: w, Normalized: NormalizedText.From(w)))
            .Where(w => !w.Normalized.IsEmpty)
            .Select(w => new Entry(
                w.Original,
                w.Normalized.Collapsed,
                w.Normalized.Squashed,
                NormalizedText.WordsOnly(w.Normalized.Collapsed),
                w.Normalized.Squashed.Length >= MinSubstringLength ? Reach.Anywhere
                    : w.Normalized.Squashed.Length >= MinPrefixLength ? Reach.WordStart
                    : Reach.WholeWord))
            .Where(e => e.Squashed.Length > 0)
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
            var hit = entry.Reach switch
            {
                Reach.Anywhere => text.Collapsed.Contains(entry.Collapsed, StringComparison.Ordinal)
                                  || text.Squashed.Contains(entry.Squashed, StringComparison.Ordinal),
                Reach.WordStart => HasWord(text, " " + entry.Padded),
                _ => HasWord(text, " " + entry.Padded + " "),
            };

            if (hit) return entry.Original;
        }
        return null;
    }

    private static bool HasWord(NormalizedText text, string needle) =>
        text.PaddedWords.Contains(needle, StringComparison.Ordinal)
        || text.JoinedWords.Contains(needle, StringComparison.Ordinal);

    /// <inheritdoc cref="FindMatch(NormalizedText)"/>
    public string? FindMatch(string? raw) => FindMatch(NormalizedText.From(raw));
}
