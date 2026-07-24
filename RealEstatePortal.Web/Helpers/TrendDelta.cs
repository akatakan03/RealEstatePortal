namespace RealEstatePortal.Web.Helpers;

/// A week-over-week change, ready to render.
///
/// The rule this encodes is an accessibility one: direction is carried by the arrow *and* the
/// wording, never by colour alone. That only holds if every tile computes it the same way,
/// which is why it lives here rather than as a local function in each view.
///
/// Text is either a wording or a percentage. The wordings are English, which is also how the
/// resource file is keyed, so a view renders it as L[delta.Text] and gets the translation. A
/// percentage has no entry and the localizer hands the string straight back — so the same call
/// covers both cases without the view having to tell them apart.
public readonly record struct TrendDelta(string Arrow, string Css, string Text)
{
    private const double NoiseFloor = 0.5;   // below half a percent, call it flat

    public static TrendDelta Between(int current, int previous)
    {
        // Nothing to compare against: a percentage off zero is either meaningless or infinite,
        // so say what actually happened instead.
        if (previous == 0)
            return current == 0
                ? new TrendDelta("→", "flat", "no change")
                : new TrendDelta("▲", "up", "all new this week");

        var pct = (current - previous) * 100.0 / previous;
        if (Math.Abs(pct) < NoiseFloor)
            return new TrendDelta("→", "flat", "level");

        // Formatted in the current culture: this number is read, not parsed, and a Turkish
        // visitor expects 12,3% where an English one expects 12.3%.
        return pct > 0
            ? new TrendDelta("▲", "up", $"{pct.ToString("0.#")}%")
            : new TrendDelta("▼", "down", $"{Math.Abs(pct).ToString("0.#")}%");
    }
}
