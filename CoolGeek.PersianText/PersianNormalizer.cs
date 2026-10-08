using System.Globalization;
using System.Text;

namespace CoolGeek.PersianText;

/// <summary>
/// Turns Persian, Arabic and English text into one canonical form, so the tricks spammers use to dodge
/// filters stop working: Arabic letter variants, invisible characters, diacritics, tatweel (ـ),
/// Persian/Arabic digits, "fancy" Unicode letters (𝐭𝐞𝐱𝐭) and Cyrillic/Greek look-alikes.
/// </summary>
public static class PersianNormalizer
{
    /// <summary>
    /// Canonical form: same letters, lower case, no invisible characters, single spaces, trimmed.
    /// <para>"تبلیــغ‌ات كانال ۱۲" → "تبلیغات کانال 12"</para>
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // NFKC folds compatibility characters: fullwidth and "math bold" letters (𝐭.𝐦𝐞 → t.me),
        // Arabic presentation forms (ﻻ → لا) and similar.
        string text;
        try
        {
            text = input.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            text = input; // invalid surrogate pairs: keep the original rather than fail
        }

        var sb = new StringBuilder(text.Length);
        var lastWasSpace = false;
        foreach (var ch in text)
        {
            // Lower-case first so capital look-alikes (Cyrillic Т) map like their small forms.
            var c = MapChar(char.ToLowerInvariant(ch));
            if (c == Dropped) continue;

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace && sb.Length > 0) sb.Append(' ');
                lastWasSpace = true;
                continue;
            }

            lastWasSpace = false;
            sb.Append(c);
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Keeps only letters and digits: "ت ب ل ی غ" → "تبلیغ", "t.me/x" → "tmex".</summary>
    public static string Squash(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Collapses repeated letters: "سلاااام" → "سلام", "freeee" → "fre". Digits are left alone.</summary>
    public static string CollapseRepeats(string text)
    {
        if (text.Length < 2) return text;

        var sb = new StringBuilder(text.Length);
        var previous = '\0';
        foreach (var c in text)
        {
            if (c == previous && char.IsLetter(c)) continue;
            sb.Append(c);
            previous = c;
        }
        return sb.ToString();
    }

    private const char Dropped = '\0';

    private static char MapChar(char ch)
    {
        switch (ch)
        {
            // Arabic letter variants → Persian
            case 'ي': case 'ى': case 'ئ': return 'ی'; // ي ى ئ → ی
            case 'ك': return 'ک';                                  // ك → ک
            case 'ة': case 'ۀ': case 'ہ': return 'ه';   // ة ۀ ہ → ه
            case 'أ': case 'إ': case 'آ': case 'ٱ': return 'ا'; // أ إ آ ٱ → ا
            case 'ؤ': return 'و';                                  // ؤ → و

            // Invisible characters: zero-width space/non-joiner/joiner, direction marks, word joiner,
            // byte-order mark, soft hyphen, combining grapheme joiner, bidi embeddings and isolates.
            case '​': case '‌': case '‍': case '‎': case '‏':
            case '⁠': case '﻿': case '­': case '͏':
            case '‪': case '‫': case '‬': case '‭': case '‮':
            case '⁦': case '⁧': case '⁨': case '⁩':
            case 'ـ': // tatweel (کشیده)
                return Dropped;

            // Cyrillic and Greek letters that look like Latin ones
            case 'а': return 'a'; // а
            case 'е': return 'e'; // е
            case 'о': case 'ο': return 'o'; // о ο
            case 'р': case 'ρ': return 'p'; // р ρ
            case 'с': return 'c'; // с
            case 'х': case 'χ': return 'x'; // х χ
            case 'у': return 'y'; // у
            case 'і': return 'i'; // і
            case 'т': case 'τ': return 't'; // т τ
            case 'м': return 'm'; // м
            case 'к': case 'κ': return 'k'; // к κ
            case 'ν': return 'v'; // ν
        }

        // Persian ۰-۹ and Arabic ٠-٩ digits → 0-9
        if (ch is >= '۰' and <= '۹') return (char)('0' + (ch - '۰'));
        if (ch is >= '٠' and <= '٩') return (char)('0' + (ch - '٠'));

        // Arabic diacritics (fatha, kasra, tanwin, shadda…) and superscript alef
        if (ch is >= 'ً' and <= 'ٟ' || ch == 'ٰ') return Dropped;

        // Any other combining mark
        if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) return Dropped;

        return ch;
    }
}
