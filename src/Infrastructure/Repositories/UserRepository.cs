using AutoMapper;
using jobAgentApi.Application.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DomainApplicationUser = jobAgentApi.Domain.Entities.ApplicationUser;
using InfrastructureApplicationUser = jobAgentApi.Infrastructure.Entities.ApplicationUser;

namespace jobAgentApi.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UserManager<InfrastructureApplicationUser> _userManager;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;

    public UserRepository(
        UserManager<InfrastructureApplicationUser> userManager,
        AppDbContext dbContext,
        IMapper mapper)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<bool> AddAsync(DomainApplicationUser entity)
    {
        if (entity is null)
        {
            return false;
        }

        var user = _mapper.Map<InfrastructureApplicationUser>(entity);
        EnsureUserName(user);
        var result = await _userManager.CreateAsync(user);

        if (result.Succeeded)
        {
            entity.Id = user.Id;
        }

        return result.Succeeded;
    }

    public async Task<bool> DeleteAsync(DomainApplicationUser entity)
    {

        if (entity is null)
        {
            return false;
        }

        var user = await FindIdentityUserAsync(entity);
        if (user is null)
        {
            return false;
        }

        var result = await _userManager.DeleteAsync(user);

        return result.Succeeded;
    }

    public async Task<IEnumerable<DomainApplicationUser>> GetAllAsync()
    {
        var users = await _userManager.Users.ToListAsync();
        return _mapper.Map<IEnumerable<DomainApplicationUser>>(users);
    }

    public async Task<DomainApplicationUser?> GetByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user is null ? null : _mapper.Map<DomainApplicationUser>(user);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> UpdateAsync(DomainApplicationUser entity)
    {
        if (entity is null)
        {
            return false;
        }

        var existingUser = await FindIdentityUserAsync(entity);
        if (existingUser is null)
        {
            return false;
        }

        _mapper.Map(entity, existingUser);
        EnsureUserName(existingUser);

        var result = await _userManager.UpdateAsync(existingUser);

        return result.Succeeded;
    }

    public async Task<bool> CheckPasswordAsync(DomainApplicationUser user, string password)
    {
        var identityUser = await FindIdentityUserAsync(user);
        return identityUser is not null && await _userManager.CheckPasswordAsync(identityUser, password);
    }

    public async Task<bool> CreateWithPasswordAsync(DomainApplicationUser user, string password)
    {
        if (user is null)
        {
            return false;
        }

        var identityUser = _mapper.Map<InfrastructureApplicationUser>(user);
        EnsureUserName(identityUser);

        var result = await _userManager.CreateAsync(identityUser, password);

        if (result.Succeeded)
        {
            user.Id = identityUser.Id;
        }

        return result.Succeeded;
    }

    public async Task<bool> ChangePasswordAsync(DomainApplicationUser user, string currentPassword, string newPassword)
    {
        var identityUser = await FindIdentityUserAsync(user);
        if (identityUser is null)
        {
            return false;
        }

        var result = await _userManager.ChangePasswordAsync(identityUser, currentPassword, newPassword);
        return result.Succeeded;
    }

    public async Task<string> GeneratePasswordResetTokenAsync(DomainApplicationUser user)
    {
        var identityUser = await FindIdentityUserAsync(user)
            ?? throw new InvalidOperationException("Usuario nao encontrado para gerar token de redefinicao.");

        return await _userManager.GeneratePasswordResetTokenAsync(identityUser);
    }

    public async Task<DomainApplicationUser?> GetByIdAsync(Guid id)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? null : _mapper.Map<DomainApplicationUser>(user);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _userManager.Users.AnyAsync(u => u.Id == id);
    }

    private async Task<InfrastructureApplicationUser?> FindIdentityUserAsync(DomainApplicationUser user)
    {
        if (user.Id != Guid.Empty)
        {
            var byId = await _userManager.FindByIdAsync(user.Id.ToString());
            if (byId is not null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return await _userManager.FindByEmailAsync(user.Email);
        }

        return null;
    }

    private static void EnsureUserName(InfrastructureApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.UserName))
        {
            user.UserName = user.Email;
        }
    }
}