using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Common.Behaviours;

public class PerformanceBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly Stopwatch _timer = new();
    private readonly ILogger<TRequest> _logger;
    private readonly IUser _user;

    public PerformanceBehaviour(ILogger<TRequest> logger, IUser user)
    {
        _logger = logger;
        _user = user;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _timer.Start();
        var response = await next();
        _timer.Stop();

        var elapsedMs = _timer.ElapsedMilliseconds;
        if (elapsedMs > 500)
        {
            var requestName = typeof(TRequest).Name;
            var userId = _user.Id ?? string.Empty;
            // Log the request type and timing only — never the body (may contain PII).
            _logger.LogWarning(
                "Long-running request: {Name} ({ElapsedMs} ms) by {UserId}",
                requestName, elapsedMs, userId);
        }

        return response;
    }
}