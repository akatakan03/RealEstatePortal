using RealEstatePortal.Application.Common;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.Common;

public class BotDetectionTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
    [InlineData("Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)")]
    [InlineData("facebookexternalhit/1.1")]
    [InlineData("curl/8.4.0")]
    [InlineData("python-requests/2.31.0")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FlagsBotsAndMissingUserAgents(string? userAgent)
    {
        BotDetection.IsBot(userAgent).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile Safari/604.1")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119 Safari/537.36")]
    public void AllowsRealBrowsers(string userAgent)
    {
        BotDetection.IsBot(userAgent).ShouldBeFalse();
    }
}
