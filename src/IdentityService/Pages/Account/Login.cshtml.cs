using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TCGTrading.IdentityService.Models;

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

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

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
