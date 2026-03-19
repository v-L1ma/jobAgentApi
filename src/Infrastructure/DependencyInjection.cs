using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
using jobAgentApi.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace jobAgentApi.Infrastructure;

public static class DependencyInjection
{
        public static IServiceCollection AddInfrastructure(
                this IServiceCollection services,
                IConfiguration configuration)
        {
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                services.AddDbContext<AppDbContext>(options =>
                {
                        options.UseNpgsql(connectionString);
                });

                services.AddScoped<IUnitOfWork, Repositories.UnitOfWork>();
                services.AddSingleton<ITokenService, TokenService>();
                
                services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<AppDbContext>();

                return services;
        }
}
