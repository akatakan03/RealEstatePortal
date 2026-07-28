using RealEstatePortal.Application.Common.Models;

namespace RealEstatePortal.Application.Common.Interfaces;

// Turns a free-text search sentence ("Kadıköy'de balkonlu 3+1 daireler") into a structured filter
// via an LLM. Returns null when the feature isn't available (no API key) or the call fails — the
// caller then falls back to a plain keyword search, so search never breaks on this being off.
public interface INaturalLanguageSearchParser
{
    Task<ParsedSearchFilter?> ParseAsync(string text, CancellationToken cancellationToken = default);
}
