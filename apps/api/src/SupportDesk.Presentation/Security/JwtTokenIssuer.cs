using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Security;

public sealed class JwtTokenIssuer(JwtSettings settings, IClock clock) : ITokenIssuer
{
    public AccessToken Issue(SignedInAgent agent)
    {
        var now = clock.UtcNow;
        var expiry = now.AddMinutes(settings.LifetimeMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, agent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("name", agent.FullName)
        ];
        var jwt = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            now,
            expiry,
            new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expiry);
    }
}
