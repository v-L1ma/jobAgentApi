using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Abstractions;

public interface ITokenService
{
    public string GenerateToken(ApplicationUser user);
    public string GenerateRefreshToken(ApplicationUser user);
    public Task<bool> ValidateToken(string token);
}