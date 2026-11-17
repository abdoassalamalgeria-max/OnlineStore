using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BookStore.Api.IntegrationTests;

/// <summary>Mints JWTs directly so authorization can be tested without a database.</summary>
internal static class TestTokens
{
    public static string Create(string role, string signingKey = TestSettings.JwtSigningKey, DateTime? expires = null)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TestSettings.JwtIssuer,
            Audience = TestSettings.JwtAudience,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("role", role),
            ]),
            NotBefore = now.AddMinutes(-10),
            IssuedAt = now.AddMinutes(-10),
            Expires = expires ?? now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
        });
    }
}
