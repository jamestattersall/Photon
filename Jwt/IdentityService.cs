using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Photon.Jwt;

public class IdentityService(JwtConfiguration config)
{
    private readonly JwtConfiguration _config = config;

    public string GenerateToken(string userName)
    {

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "1453521r"),
            new Claim(JwtRegisteredClaimNames.Email, "onoiew@win.ee"),
            new Claim(JwtRegisteredClaimNames.PreferredUsername, userName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config.Issuer,
            audience: _config.Audience,
            claims: claims,
            expires: DateTime.Now.AddDays(_config.ExpireDays),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}