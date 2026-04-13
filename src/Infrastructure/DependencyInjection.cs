using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using jobAgentApi.Infrastructure.Entities;
using jobAgentApi.Infrastructure.Services;
using jobAgentApi.Infrastructure.Utils;
using jobAgentApi.Infrastructure.Services.JobScraper;
using jobAgentApi.Infrastructure.Repositories;
using jobAgentApi.Infrastructure.Services.JobScraperQueue;
using jobAgentApi.Application;

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

                services
                        .AddIdentityCore<ApplicationUser>(options =>
                        {
                                options.User.RequireUniqueEmail = true;
                        })
                        .AddRoles<Roles>()
                        .AddEntityFrameworkStores<AppDbContext>()
                        .AddDefaultTokenProviders();

                services.AddAutoMapper(typeof(DependencyInjection).Assembly);

                // Job Scraping Queue Service
                services.AddSingleton<IJobScrapingQueueService, JobScrapingQueueService>();

                services.AddScoped<IJobRepository, JobRepository>();
                services.AddScoped<IUserRepository, Repositories.UserRepository>();
                services.AddScoped<IUserSearchQueryRepository, UserSearchQueryRepository>();
                services.AddScoped<IStatisticsRepository, StatisticsRepository>();
                services.AddScoped<IUnitOfWork, Repositories.UnitOfWork>();
                services.AddSingleton<ITokenService, TokenService>();
                services.AddSingleton<IKeywordNormalizer, KeywordNormalizer>();
                services.AddScoped<ISearchQueryMatcherService, SearchQueryMatcherService>();
                services.AddScoped<ISearchQueryService, SearchQueryService>();
                services.AddScoped<IEmailService, EmailService>();
                services.AddScoped<IStorageService, StorageService>();
                services.AddScoped<IPdfService, PdfService>();
                services.AddScoped<ICvAiService, CvAiService>();

                services.Configure<JobScraperOptions>(configuration.GetSection(JobScraperOptions.SectionName));
                services.AddSingleton<ILinkedInJobScraper, LinkedInJobScraper>();
                services.AddSingleton<IGreenhouseJobScraper, GreenhouseJobScraper>();
                services.AddSingleton<IVagasComBrJobScraper, VagasComBrJobScraper>();
                services.AddSingleton<IPlaywrightBrowserManager, PlaywrightBrowserManager>();

                // GuypJobScraper depende de HttpClient - registrar como Singleton com HttpClient manual
                services.AddSingleton<IGuypJobScraper>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<JobScraperOptions>>();
                    var logger = sp.GetRequiredService<ILogger<GuypJobScraper>>();
                    var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    return new GuypJobScraper(options, logger, httpClient);
                });

                // JobScraperExecutionService é Singleton porque depende de outros Singletons (scrapers)
                // Usa IServiceScopeFactory internamente para acessar IJobRepository (Scoped)
                services.AddSingleton<IJobScraperExecutionService, JobScraperExecutionService>();
                
                // Background Services
                services.AddHostedService<JobScraperBackgroundService>();
                services.AddHostedService<JobScrapingQueueBackgroundService>();

                return services;
        }
}

