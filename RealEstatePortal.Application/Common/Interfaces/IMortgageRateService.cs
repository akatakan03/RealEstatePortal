namespace RealEstatePortal.Application.Common.Interfaces;

public interface IMortgageRateService
{
    // The indicative monthly interest rate (as a percent, e.g. 3.25) that seeds the loan
    // calculator. Never throws and always returns a value: when the live source is unavailable
    // or unconfigured it falls back to the configured default.
    Task<decimal> GetMonthlyRatePercentAsync(CancellationToken cancellationToken = default);
}
