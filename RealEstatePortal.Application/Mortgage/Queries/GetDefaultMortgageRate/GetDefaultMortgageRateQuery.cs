using MediatR;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Mortgage.Queries.GetDefaultMortgageRate;

// The monthly interest rate (percent) that pre-fills the loan calculator on a listing page.
// Thin wrapper over IMortgageRateService so the Web layer stays on the MediatR seam like every
// other read, and the caching/fallback all live behind the service.
public record GetDefaultMortgageRateQuery : IRequest<decimal>;

public class GetDefaultMortgageRateQueryHandler : IRequestHandler<GetDefaultMortgageRateQuery, decimal>
{
    private readonly IMortgageRateService _rates;

    public GetDefaultMortgageRateQueryHandler(IMortgageRateService rates) => _rates = rates;

    public Task<decimal> Handle(GetDefaultMortgageRateQuery request, CancellationToken cancellationToken)
        => _rates.GetMonthlyRatePercentAsync(cancellationToken);
}
