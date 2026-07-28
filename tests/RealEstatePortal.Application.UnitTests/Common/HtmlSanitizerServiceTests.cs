using RealEstatePortal.Infrastructure.Html;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Common;

// The sanitizer is the gate that stops stored XSS in listing descriptions, so its allowlist is
// worth pinning down directly: dangerous constructs must vanish, the handful of formatting tags
// must survive, and everything else must be reduced to plain text.
public class HtmlSanitizerServiceTests
{
    private readonly HtmlSanitizerService _sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_ReturnsEmpty_ForBlankInput(string? input)
    {
        _sut.Sanitize(input).ShouldBe(string.Empty);
    }

    [Fact]
    public void Sanitize_RemovesScriptElementAndItsContent()
    {
        var result = _sut.Sanitize("<p>Hello</p><script>alert('xss')</script>");

        // No <script> element (the security guarantee) and, thanks to the pre-pass, none of its
        // text left dangling either. Unwrapping still keeps pasted <div>/<span> prose — see the
        // container test — this only drops script/style blocks whole.
        result.ShouldNotContain("<script", Case.Insensitive);
        result.ShouldNotContain("alert");
        result.ShouldContain("Hello");
    }

    [Fact]
    public void Sanitize_DropsEventHandlerAttributes()
    {
        // <img> is not on the allowlist, so the whole element — and its onerror — must go.
        var result = _sut.Sanitize("<img src=\"x\" onerror=\"alert(1)\">Text");

        result.ShouldNotContain("onerror", Case.Insensitive);
        result.ShouldNotContain("<img", Case.Insensitive);
        result.ShouldContain("Text");
    }

    [Fact]
    public void Sanitize_StripsStyleAndClassAttributes()
    {
        var result = _sut.Sanitize("<p class=\"evil\" style=\"color:red\">Body</p>");

        result.ShouldNotContain("class", Case.Insensitive);
        result.ShouldNotContain("style", Case.Insensitive);
        result.ShouldContain("Body");
    }

    [Fact]
    public void Sanitize_RemovesLinks_NotOnTheSimpleAllowlist()
    {
        var result = _sut.Sanitize("<a href=\"javascript:alert(1)\">click</a>");

        result.ShouldNotContain("href", Case.Insensitive);
        result.ShouldNotContain("javascript", Case.Insensitive);
        result.ShouldContain("click");
    }

    [Fact]
    public void Sanitize_KeepsAllowedFormatting()
    {
        var input = "<p><strong>Bold</strong> and <em>italic</em></p>"
                    + "<ul><li>one</li><li>two</li></ul><ol><li>first</li></ol>";

        var result = _sut.Sanitize(input);

        result.ShouldContain("<strong>Bold</strong>");
        result.ShouldContain("<em>italic</em>");
        result.ShouldContain("<ul>");
        result.ShouldContain("<li>one</li>");
        result.ShouldContain("<ol>");
    }

    [Fact]
    public void Sanitize_UnwrapsDisallowedContainers_KeepingTheirText()
    {
        var result = _sut.Sanitize("<div><span>plain</span> text</div>");

        result.ShouldNotContain("<div", Case.Insensitive);
        result.ShouldNotContain("<span", Case.Insensitive);
        result.ShouldContain("plain");
        result.ShouldContain("text");
    }
}
