using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TCGTrading.IdentityService.Models;

namespace TCGTrading.IdentityService.Pages.Account;

public class LogoutModel(
    SignInManager<ApplicationUser> signInManager,
    IIdentityServerInteractionService interaction) : PageModel
{
    public string? PostLogoutRedirectUri { get; set; }

    public async Task<IActionResult> OnGetAsync(string? logoutId = null)
    {
        await signInManager.SignOutAsync();

        var logout = await interaction.GetLogoutContextAsync(logoutId);
        PostLogoutRedirectUri = logout?.PostLogoutRedirectUri;

        if (PostLogoutRedirectUri != null)
            return Redirect(PostLogoutRedirectUri);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? logoutId = null)
        => await OnGetAsync(logoutId);
}
