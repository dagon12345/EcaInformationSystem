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
    public string GenerateToken(
        Guid userId,   // ✅ NEW — needed so chat entities can resolve SenderId reliably
        string userName,
        string fullName, 
        string position, 
        string role, 
        List<int>? jurisdictionCodes = null,
        int? region = null) // ✅ NEW
    {
        var key = new SymmetricSecurityKey(
                          Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(
                          double.Parse(_config["Jwt:ExpiryHours"] ?? "8"));

        var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),  // ✅ NEW — standard claim for "who is this"
                new(ClaimTypes.Name,            userName),
                new("FullName",                 fullName),
                new("Position",                 position),
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

        // ✅ NEW — same pattern as jurisdictions: only add the claim if present,
        // keeps the token lean for users who haven't been assigned a Region yet
        if (region.HasValue)
            claims.Add(new Claim("Region", region.Value.ToString()));

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