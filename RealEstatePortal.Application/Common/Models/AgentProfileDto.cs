namespace RealEstatePortal.Application.Common.Models;

public record AgentProfileDto(
    string UserId,
    string? DisplayName,
    string Email,
    string? Bio,
    string? AvatarKey);