// Application/Services/TokenService.cs
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    // ✅ Added 'position' parameter — distinct from 'role'. Role gates
    // permissions (Admin/PDO/Viewer); Position is the real job title
    // ("Project Development Officer I") that belongs on signature lines.
    public string GenerateToken(string userName, string fullName, string position, string role, List<int>? jurisdictionCodes = null)
    {
        var key = new SymmetricSecurityKey(
                          Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(
                          double.Parse(_config["Jwt:ExpiryHours"] ?? "8"));

        var claims = new List<Claim>
            {
                new(ClaimTypes.Name,            userName),
                new("FullName",                 fullName),
                new("Position",                 position),   // ✅ new custom claim
                new(ClaimTypes.Role,            role),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

        // ✅ Embed jurisdiction codes for PDO users
        // Stored as a single comma-separated claim — avoids issuing many claims
        if (role == "PDO" && jurisdictionCodes?.Any() == true)
            claims.Add(new Claim("jurisdictions",
            string.Join(",", jurisdictionCodes)));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}