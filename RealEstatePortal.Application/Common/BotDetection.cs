namespace RealEstatePortal.Application.Common;

// Best-effort filter for automated traffic, so crawlers and link-preview fetchers don't
// inflate listing view counts. Matches the User-Agent against common bot markers; a missing
// User-Agent is treated as a bot (real browsers always send one).
public static class BotDetection
{
    private static readonly string[] Markers =
    {
        "bot", "crawl", "spider", "slurp",              // googlebot, bingbot, baiduspider, yahoo slurp…
        "facebookexternalhit", "embedly", "quora link", // link/preview fetchers
        "curl", "wget", "python-requests", "scrapy",    // scripted clients
        "httpclient", "headless", "phantomjs",
        "semrush", "ahrefs", "mj12", "dotbot",          // SEO crawlers
        "pingdom", "uptimerobot", "monitoring"          // uptime monitors
    };

    public static bool IsBot(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return true;

        var ua = userAgent.ToLowerInvariant();
        foreach (var marker in Markers)
            if (ua.Contains(marker))
                return true;

        return false;
    }
}
