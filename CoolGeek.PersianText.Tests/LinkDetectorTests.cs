namespace CoolGeek.PersianText.Tests;

public class LinkDetectorTests
{
    [Theory]
    [InlineData("join https://example.com now")]
    [InlineData("http://x.y")]
    [InlineData("www.site.ir")]
    [InlineData("t.me/spam_channel")]
    [InlineData("telegram.me/joinchat/abc")]
    [InlineData("T . Me / spam")]
    [InlineData("t(.)me/x")]
    [InlineData("t[dot]me/abc")]
    [InlineData("t نقطه me/abc")]
    [InlineData("تی دات می / کانال")]
    [InlineData("visit shop.xyz today")]
    [InlineData("sale on example.com")]
    [InlineData("𝐭.𝐦𝐞/fake")]
    [InlineData("عضو شو t . me/abc")]
    [InlineData("لینک: +AbCdEfGh12345")]
    [InlineData("tg://resolve?domain=x")]
    public void Detects_plain_and_disguised_links(string text) =>
        Assert.True(LinkDetector.ContainsLink(text));

    [Theory]
    [InlineData("سلام دوستان خوبید؟")]
    [InlineData("the meeting is at 5.30")]
    [InlineData("ساعت ۱۰ میام")]
    [InlineData("I like t-shirts")]
    [InlineData("e.g. this is fine")]
    [InlineData("میخوام بیام")]
    [InlineData("version 2.0 released")]
    [InlineData("تی‌شرت دات کام نیست")]
    [InlineData("phone: +98 912")]
    [InlineData("")]
    public void Ignores_ordinary_text(string text) =>
        Assert.False(LinkDetector.ContainsLink(text));

    [Theory]
    [InlineData("عضو @spam_channel شو", true)]
    [InlineData("@abcd is too short", false)]   // usernames have at least 5 characters
    [InlineData("mail me at info@bcdef.com", false)]
    [InlineData("@CoolGeek_Channel", true)]
    public void Detects_usernames(string text, bool expected) =>
        Assert.Equal(expected, LinkDetector.ContainsUsername(text));
}
