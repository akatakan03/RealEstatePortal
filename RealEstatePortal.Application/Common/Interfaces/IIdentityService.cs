using RealEstatePortal.Application.Common.Models;

namespace RealEstatePortal.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserEmailAsync(string userId, CancellationToken cancellationToken = default);

    /// The address to write to and the language to write in. Used when composing a notification;
    /// the plain email lookup above is for screens that only list who owns what.
    Task<EmailRecipient?> GetEmailRecipientAsync(string userId, CancellationToken cancellationToken = default);
    Task<AgentProfileDto?> GetAgentProfileAsync(string userId, CancellationToken cancellationToken = default);
}