using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Repositories
{
    public interface IUserRepository : IRepositoryBase<ApplicationUser>
    {
        Task<ApplicationUser?> GetByEmailAsync(string email);
        Task<bool> CheckPasswordAsync(ApplicationUser user, string password);
        Task<bool> CreateWithPasswordAsync(ApplicationUser user, string password);
        Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user);
    }
}