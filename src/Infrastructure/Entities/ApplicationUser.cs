using Microsoft.AspNetCore.Identity;

namespace jobAgentApi.Infrastructure.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string CPF { get; set; } = string.Empty;
    public bool OnboardingCompleted { get; set; }
}