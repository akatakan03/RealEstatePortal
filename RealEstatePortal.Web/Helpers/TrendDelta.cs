using System.Globalization;

namespace RealEstatePortal.Web.Helpers;

/// A week-over-week change, ready to render.
///
/// The rule this encodes is an accessibility one: direction is carried by the arrow *and* the
/// wording, never by colour alone. That only holds if every tile computes it the same way,
/// which is why it lives here rather than as a local function in each view.
public readonly record struct TrendDelta(string Arrow, string Css, string Text)
{
    private const double NoiseFloor = 0.5;   // below half a percent, call it flat

    public static TrendDelta Between(int current, int previous)
    {
        var inv = CultureInfo.InvariantCulture;

        // Nothing to compare against: a percentage off zero is either meaningless or infinite,
        // so say what actually happened instead.
        if (previous == 0)
            return current == 0
                ? new TrendDelta("→", "flat", "no change")
                : new TrendDelta("▲", "up", "new this week");

        var pct = (current - previous) * 100.0 / previous;
        if (Math.Abs(pct) < NoiseFloor)
            return new TrendDelta("→", "flat", "level");

        return pct > 0
            ? new TrendDelta("▲", "up", $"{pct.ToString("0.#", inv)}%")
            : new TrendDelta("▼", "down", $"{Math.Abs(pct).ToString("0.#", inv)}%");
    }
}
