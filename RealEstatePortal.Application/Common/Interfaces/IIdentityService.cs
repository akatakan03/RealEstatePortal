namespace RealEstatePortal.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserEmailAsync(string userId, CancellationToken cancellationToken = default);
}