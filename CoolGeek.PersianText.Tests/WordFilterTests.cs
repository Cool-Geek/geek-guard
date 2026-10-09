namespace CoolGeek.PersianText.Tests;

public class WordFilterTests
{
    private readonly WordFilter _filter = new(["تبلیغ", "کس", "casino", "عضو شو", "کونی"]);

    [Theory]
    [InlineData("این یک تبلیغ است", "تبلیغ")]
    [InlineData("تبلیغات ویژه", "تبلیغ")]           // 4+ letters: also at the start of a longer word
    [InlineData("ت.ب.ل.ی.غ ویژه", "تبلیغ")]          // split by dots
    [InlineData("ت ب ل ی غ", "تبلیغ")]               // split by spaces
    [InlineData("تبلیــــــغ", "تبلیغ")]              // tatweel
    [InlineData("تبليغ", "تبلیغ")]                   // Arabic yeh
    [InlineData("تبلییییغ", "تبلیغ")]                // repeated letters
    [InlineData("casssino", "casino")]
    [InlineData("CASINO bonus", "casino")]
    [InlineData("برو کس", "کس")]                      // short word, whole
    [InlineData("برو، کس!", "کس")]                    // short word next to punctuation
    [InlineData("برو كس", "کس")]                      // Arabic kaf in the message
    [InlineData("لطفا عضو شو", "عضو شو")]             // phrases
    [InlineData("کووووووونی", "کونی")]                // stretched letters
    [InlineData("کووووونننننی", "کونی")]
    [InlineData("کوووننننیییییی", "کونی")]
    [InlineData("ک و ن ی", "کونی")]                   // spelled out
    [InlineData("ك.و.ن.ي", "کونی")]
    [InlineData("ک و و ن ی", "کونی")]                 // spelled out and stretched
    [InlineData("ک س", "کس")]                         // short words spelled out
    [InlineData("casino-online", "casino")]           // 6+ letters: anywhere
    public void Finds_banned_words_and_disguises(string text, string expected) =>
        Assert.Equal(expected, _filter.FindMatch(text));

    [Theory]
    [InlineData("کسی اینجا هست؟")]                    // short word inside a longer one
    [InlineData("مکس")]
    [InlineData("منطقه مسکونی")]                      // contains «کونی» but is a different word
    [InlineData("ساختمان مسکونی و تجاری")]
    [InlineData("من و تو")]                            // a lone «و» is not glued to anything
    [InlineData("سلام دوستان")]
    [InlineData("")]
    public void Leaves_innocent_text_alone(string text) =>
        Assert.Null(_filter.FindMatch(text));

    [Fact]
    public void Matches_words_given_with_arabic_letters_or_tatweel()
    {
        var filter = new WordFilter(["تبليــغ"]);
        Assert.Equal("تبليــغ", filter.FindMatch("یک تبلیغ"));
    }

    [Fact]
    public void Skips_words_that_normalize_to_nothing()
    {
        var filter = new WordFilter(["", "   ", "‌", "ok"]);
        Assert.Equal(1, filter.Count);
    }
}
