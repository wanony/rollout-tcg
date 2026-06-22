using Microsoft.AspNetCore.Identity;
using TCGTrading.IdentityService.Models;

namespace TCGTrading.IdentityService.Endpoints;

public record RegisterRequest(string UserName, string Email, string Password);

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/account");

        group.MapPost("/register", async (
            RegisterRequest request,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = new ApplicationUser
            {
                UserName = request.UserName,
                Email = request.Email
            };

            var result = await userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
                return Results.ValidationProblem(
                    result.Errors
                        .GroupBy(e => e.Code)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.Description).ToArray()));

            return Results.Created(
                $"/account/{user.Id}",
                new { user.Id, user.UserName, user.Email });
        });

        return app;
    }
}
