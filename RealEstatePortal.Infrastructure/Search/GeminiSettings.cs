namespace RealEstatePortal.Infrastructure.Search;

// Bound from the "Gemini" configuration section. The API key is a free Google AI Studio key,
// supplied per environment via configuration/secrets — without it the natural-language parser
// stays off and search falls back to plain keyword matching.
public class GeminiSettings
{
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public string? ApiKey { get; set; }

    // A fast, free-tier model is plenty for this extraction task; left configurable so a model
    // change is a settings edit, not a redeploy. "gemini-flash-latest" tracks the current free
    // fast model (the dated gemini-2.0-flash free tier has been zeroed on new keys).
    public string Model { get; set; } = "gemini-flash-latest";
}
