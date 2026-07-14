using Habitrack.Api.Dtos;
using Habitrack.Api.Filters;
using Habitrack.Api.Services;

namespace Habitrack.Api.Endpoints;

public static class AuthModule
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", Register)
            .AddEndpointFilter<FluentValidationFilter<RegisterRequest>>();

        group.MapPost("/login", Login)
            .AddEndpointFilter<FluentValidationFilter<LoginRequest>>();
    }

    private static async Task<IResult> Register(RegisterRequest request, AuthService service,
        CancellationToken ct)
    {
        var result = await service.RegisterAsync(request, ct);
        return result is null
            ? Results.Conflict(new { error = "Email already registered" })
            : Results.Ok(result);
    }

    private static async Task<IResult> Login(LoginRequest request, AuthService service,
        CancellationToken ct)
    {
        var result = await service.LoginAsync(request, ct);
        return result is null
            ? Results.Unauthorized()
            : Results.Ok(result);
    }
}
