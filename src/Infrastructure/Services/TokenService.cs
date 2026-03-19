using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using jobAgentApi.Application.Abstractions;
using jobAgentApi.Domain.Entities;
using jobAgentApi.Infrastructure.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public class TokenService : ITokenService
{
	private readonly IConfiguration _configuration;
	
	public TokenService(IConfiguration configuration)
	{
		_configuration = configuration;
	}

    public string GenerateToken(ApplicationUser user){
		var jwtSettings = _configuration.GetSection("JwtSettings");
		var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? string.Empty));
		
		var claims = new List<Claim>()
		{
            
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
			new Claim(JwtRegisteredClaimNames.Name, user.Name),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
		};

        int.TryParse(jwtSettings["ExpirationTimeInMinutes"], out var expirationTime);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationTime),
            signingCredentials: new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
	} 

    public string GenerateRefreshToken(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
		var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? string.Empty));
		
		var claims = new List<Claim>()
		{
            
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
		};

        int.TryParse(jwtSettings["RefreshExpirationTimeInMinutes"], out var expirationTime);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationTime),
            signingCredentials: new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<bool> ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var tokenParameters = TokenHelpers.GetTokenValidationParameters(_configuration);

        var validToken = await new JwtSecurityTokenHandler().ValidateTokenAsync(token, tokenParameters);

        return validToken.IsValid;
    }
	
}