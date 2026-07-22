using MediatR.Pipeline;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Common.Behaviours;

public class LoggingBehaviour<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;
    private readonly IUser _user;

    public LoggingBehaviour(ILogger<TRequest> logger, IUser user)
    {
        _logger = logger;
        _user = user;
    }

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _user.Id ?? string.Empty;
        // Deliberately log only the request type and user — never the request body, which can
        // hold PII (inquiry contact details, saved-search criteria, notes, etc.).
        _logger.LogInformation("Request: {Name} by {UserId}", requestName, userId);
        return Task.CompletedTask;
    }
}