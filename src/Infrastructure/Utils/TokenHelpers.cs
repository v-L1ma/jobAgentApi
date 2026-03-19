using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace jobAgentApi.Infrastructure.Utils;

public static class TokenHelpers
{
    public static TokenValidationParameters GetTokenValidationParameters(IConfiguration configuration)
    {
        var tokenKey = System.Text.Encoding.UTF8.GetBytes(configuration["JwtSettings:SecretKey"] ?? string.Empty);

        return new TokenValidationParameters()
        {

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateAudience = true,
            ValidAudience = configuration["JwtSettings:Audience"],
            ValidateIssuer = true,
            ValidIssuer = configuration["JwtSettings:Issuer"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(tokenKey)
        };
    }
}