using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Common.Interfaces;
using RealEstatePortal.Infrastructure.Identity;

namespace RealEstatePortal.Web.Api;

public record LoginApiRequest(string Email, string Password);
public record TokenResponse(string Token);

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthApiController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _tokens;

    public AuthApiController(UserManager<ApplicationUser> userManager, IJwtTokenService tokens)
    {
        _userManager = userManager;
        _tokens = tokens;
    }

    /// <summary>Exchange email + password for a JWT bearer token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Login(LoginApiRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokens.CreateToken(user.Id, user.Email!, roles);
        return Ok(new TokenResponse(token));
    }

    /// <summary>Returns the caller's identity — proves a JWT is accepted. JWT scheme only.</summary>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value)
        });
    }
}