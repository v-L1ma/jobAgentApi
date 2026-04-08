using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using jobAgentApi.Infrastructure.Entities;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Infrastructure
{
    public class AppDbContext : IdentityDbContext<Infrastructure.Entities.ApplicationUser, Infrastructure.Entities.Roles, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<JobApplication> JobApplications { get; set; }
        public DbSet<Plataform> Plataforms { get; set; }
        public DbSet<PlataformConfiguration> PlataformConfigurations { get; set; }
        public DbSet<Preference> Preferences { get; set; }
        public DbSet<Questoes> Questions { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<JobAnalysis> JobAnalysis { get; set; }
        public DbSet<UserCv> UserCvs { get; set; }
        public DbSet<GeneratedCv> GeneratedCvs { get; set; }
        public DbSet<UserPreferences> UserPreferences { get; set; }
        
        public DbSet<JobEvaluation> JobEvaluations { get; set; }
        public DbSet<CvEvaluation> CvEvaluations { get; set; }
        public DbSet<SearchQuery> SearchQueries { get; set; }
        public DbSet<UserSearchQuery> UserSearchQueries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Job>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Analysis)
                      .WithOne()
                      .HasForeignKey<JobAnalysis>(a => a.JobId);
            });

            modelBuilder.Entity<JobAnalysis>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<UserCv>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<JobEvaluation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId);
            });

            modelBuilder.Entity<CvEvaluation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.GeneratedCv).WithMany().HasForeignKey(e => e.GeneratedCvId);
            });

            modelBuilder.Entity<GeneratedCv>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<UserPreferences>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Configuração para campos de array/lista dependendo do provider
            });

            modelBuilder.Entity<SearchQuery>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.NormalizedHash);
            });

            modelBuilder.Entity<UserSearchQuery>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.SearchQueryId });
            });
        }
    }
}
