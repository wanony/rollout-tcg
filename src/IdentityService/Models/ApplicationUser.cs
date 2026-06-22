using Microsoft.AspNetCore.Identity;

namespace TCGTrading.IdentityService.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
