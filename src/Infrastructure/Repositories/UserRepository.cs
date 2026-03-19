using jobAgentApi.Application.Repositories;
using jobAgentApi.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace jobAgentApi.Infrastructure.Repositories;

public class UserRepository : IRepositoryBase<ApplicationUser>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRepository(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<bool> AddAsync(ApplicationUser entity)
    {
        var result = await _userManager.CreateAsync(entity);

        return result.Succeeded;
    }

    public async Task<bool> DeleteAsync(ApplicationUser entity)
    {
        var result = await _userManager.DeleteAsync(entity);

        return result.Succeeded;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        return user is not null;
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllAsync()
    {
        return await _userManager.Users.ToListAsync();
    }

    public async Task<ApplicationUser?> GetByIdAsync(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());

        return user;
    }

    public Task SaveChangesAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<bool> UpdateAsync(ApplicationUser entity)
    {
        var result = await _userManager.UpdateAsync(entity);

        return result.Succeeded;
    }

    public async Task<bool> CheckPasswordAsync(ApplicationUser user, string password){
        return await _userManager.CheckPasswordAsync(user, password);
    }
}