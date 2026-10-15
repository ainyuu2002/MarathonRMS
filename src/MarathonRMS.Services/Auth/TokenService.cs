using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DTOs.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MarathonRMS.Services.Auth;

public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "MarathonRMS.API";
    public string Audience { get; set; } = "MarathonRMS.WebApp";
    /// <summary>Khóa ký HMAC – tối thiểu 32 ký tự. Đặt trong user-secrets, không commit.</summary>
    public string SigningKey { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 60;
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(AppUser user, IEnumerable<string> roles);
}

public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;
    public TokenService(IOptions<JwtSettings> options) => _settings = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateToken(AppUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        if (user.OrganizationId is int orgId)
            claims.Add(new Claim(AppClaims.OrganizationId, orgId.ToString()));

        var expires = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
