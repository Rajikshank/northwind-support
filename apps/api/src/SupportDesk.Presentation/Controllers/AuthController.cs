using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Abstractions;
namespace SupportDesk.Presentation.Controllers;
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthentication auth) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        if (result is null) return Problem(statusCode: 401, title: "Sign-in failed", detail: "Invalid email or password.");
        return Ok(result);
    }
    [HttpGet("me")]
    public async Task<ActionResult<SignedInAgent>> Me(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await auth.MeAsync(id, ct);
        return user is null ? Unauthorized() : Ok(user);
    }
}
