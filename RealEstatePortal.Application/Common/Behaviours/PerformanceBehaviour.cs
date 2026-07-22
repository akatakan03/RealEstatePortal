using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Application.Common.Behaviours;

public class PerformanceBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
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
        // Local per-call timer — no shared state, correct regardless of this behaviour's lifetime.
        var timer = Stopwatch.StartNew();
        var response = await next();
        timer.Stop();

        var elapsedMs = timer.ElapsedMilliseconds;
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