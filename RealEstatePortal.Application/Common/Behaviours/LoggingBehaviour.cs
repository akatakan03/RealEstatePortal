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
        _logger.LogInformation(
            "Request: {Name} by {UserId} {@Request}",
            requestName, userId, request);
        return Task.CompletedTask;
    }
}