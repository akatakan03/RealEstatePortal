namespace RealEstatePortal.Infrastructure.Mortgage;

// Bound from the "Mortgage" configuration section.
public class MortgageSettings
{
    // Used when the live rate can't be fetched (no API key, EVDS down, series returns nothing).
    public decimal DefaultMonthlyRate { get; set; } = 3.0m;

    public EvdsSettings Evds { get; set; } = new();
}

// TCMB EVDS (evds2.tcmb.gov.tr) — the free, official source for the banking sector's weighted
// average housing-loan interest rate. The API key is registered per user and must be supplied
// via configuration/secrets; without it the service simply uses the fallback above.
public class EvdsSettings
{
    public string BaseUrl { get; set; } = "https://evds2.tcmb.gov.tr/service/evds/";

    public string? ApiKey { get; set; }

    // The EVDS series for the weighted-average housing (konut) loan rate. Left configurable
    // because a wrong code should be a settings fix, not a redeploy.
    public string HousingLoanSeries { get; set; } = "TP.KTF12";
}
