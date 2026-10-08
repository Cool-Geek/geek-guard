namespace CoolGeek.PersianText.Tests;

public class PersianNormalizerTests
{
    [Theory]
    [InlineData("كيك", "کیک")]                    // Arabic kaf and yeh
    [InlineData("ةأإؤ", "هااو")]             // teh marbuta, hamza forms
    [InlineData("تبلیــــغ", "تبلیغ")]                           // tatweel
    [InlineData("تبلی‌غات", "تبلیغات")]                     // zero-width non-joiner
    [InlineData("ت​ب‍ل﻿ی‏غ", "تبلیغ")]      // assorted invisible characters
    [InlineData("تَبْلِیغ", "تبلیغ")]                              // diacritics
    [InlineData("۰۹۱۲ ٣٤", "0912 34")]                           // Persian and Arabic digits
    [InlineData("𝐭.𝐦𝐞", "t.me")]                                 // math bold letters
    [InlineData("ｔ．ｍｅ", "t.me")]                               // fullwidth letters
    [InlineData("Тelegrаm", "telegram")]               // Cyrillic capital Т and small а
    [InlineData("HELLO World", "hello world")]                   // lower case
    [InlineData("  a \n\n\t b  ", "a b")]                        // whitespace collapsed and trimmed
    public void Normalize_produces_canonical_text(string input, string expected) =>
        Assert.Equal(expected, PersianNormalizer.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Normalize_handles_empty_input(string? input) =>
        Assert.Equal("", PersianNormalizer.Normalize(input));

    [Theory]
    [InlineData("سلاااام", "سلام")]
    [InlineData("freeee", "fre")]
    [InlineData("1000", "1000")] // digits are not letters
    public void CollapseRepeats_collapses_repeated_letters(string input, string expected) =>
        Assert.Equal(expected, PersianNormalizer.CollapseRepeats(input));

    [Theory]
    [InlineData("ت ب ل ی غ", "تبلیغ")]
    [InlineData("t.me/x", "tmex")]
    public void Squash_keeps_letters_and_digits(string input, string expected) =>
        Assert.Equal(expected, PersianNormalizer.Squash(input));

    [Fact]
    public void NormalizedText_exposes_derived_forms()
    {
        var text = NormalizedText.From("ت.ب.لیـــییغ!");
        Assert.Equal("ت.ب.لیییغ!", text.Value);
        Assert.Equal("ت.ب.لیغ!", text.Collapsed);
        Assert.Equal("تبلیغ", text.Squashed);
    }
}
