using Serilog;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using jobAgentApi.Application;
using jobAgentApi.Infrastructure;
using jobAgentApi.WebApi.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using jobAgentApi.Infrastructure.Utils;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3001" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// OpenTelemetry - Tracing + Metrics (Prometheus)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
        resource.AddService("jobAgentApi.WebApi"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();

        if (builder.Environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();

        if (builder.Environment.IsDevelopment())
        {
            metrics.AddRuntimeInstrumentation();
        }

        metrics.AddPrometheusExporter();
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "jobAgentApi", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"{token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = TokenHelpers.GetTokenValidationParameters(builder.Configuration);
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.UseHttpsRedirection();
app.MapControllers();
app.UseAuthentication();
app.UseAuthorization();

// Prometheus scraping endpoint: /metrics
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();
