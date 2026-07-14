using RealEstatePortal.Application.Common.Models;

namespace RealEstatePortal.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserEmailAsync(string userId, CancellationToken cancellationToken = default);
    Task<AgentProfileDto?> GetAgentProfileAsync(string userId, CancellationToken cancellationToken = default);
}