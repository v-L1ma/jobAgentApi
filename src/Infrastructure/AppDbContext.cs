using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Infrastructure
{
    public class AppDbContext : IdentityDbContext<Entities.ApplicationUser, Entities.Roles, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<JobApplication> JobApplications { get; set; }
        public DbSet<Plataform> Plataforms { get; set; }
        public DbSet<PlataformConfiguration> PlataformConfigurations { get; set; }
        public DbSet<Preference> Preferences { get; set; }
        public DbSet<Questoes> Questions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // TODO: Configurar suas entidades aqui
        }
    }
}
