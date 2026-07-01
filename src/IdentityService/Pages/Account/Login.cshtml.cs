using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TCGTrading.IdentityService.Models;
using TCGTrading.SharedKernel.Demo;

namespace TCGTrading.IdentityService.Pages.Account;

public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IIdentityServerInteractionService interaction) : PageModel
{
    [BindProperty] public string Login { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public bool RememberMe { get; set; }
    [BindProperty] public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        // login_hint=demo@rollout.dev is how the SPA's "Continue as Demo User" button asks for
        // one-click access to the seeded demo account — sign in immediately, skip the form.
        var context = await interaction.GetAuthorizationContextAsync(returnUrl);
        if (context?.LoginHint == DemoSeedData.Email)
        {
            var demoUser = await userManager.FindByEmailAsync(DemoSeedData.Email);
            if (demoUser != null)
            {
                await signInManager.SignInAsync(demoUser, isPersistent: false);
                if (interaction.IsValidReturnUrl(ReturnUrl) || Url.IsLocalUrl(ReturnUrl))
                    return Redirect(ReturnUrl!);
                return Redirect("~/");
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Accept email or username
        var user = await userManager.FindByEmailAsync(Login)
                   ?? await userManager.FindByNameAsync(Login);

        if (user != null)
        {
            var result = await signInManager.PasswordSignInAsync(user, Password, RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                if (interaction.IsValidReturnUrl(ReturnUrl) || Url.IsLocalUrl(ReturnUrl))
                    return Redirect(ReturnUrl!);
                return Redirect("~/");
            }
        }

        ErrorMessage = "Invalid username or password.";
        return Page();
    }
}
