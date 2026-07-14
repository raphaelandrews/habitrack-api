using System.Security.Claims;
using Habitrack.Api.Dtos;
using Habitrack.Api.Filters;
using Habitrack.Api.Services;

namespace Habitrack.Api.Endpoints;

public static class HabitsModule
{
    public static void MapHabitEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/habits")
            .WithTags("Habits")
            .RequireAuthorization();

        group.MapGet("/", GetAll);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create)
            .AddEndpointFilter<FluentValidationFilter<CreateHabitRequest>>();
        group.MapPut("/{id:guid}", Update)
            .AddEndpointFilter<FluentValidationFilter<UpdateHabitRequest>>();
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{habitId:guid}/completions", MarkComplete);
        group.MapDelete("/{habitId:guid}/completions/{date}", UnmarkComplete);
        group.MapGet("/{habitId:guid}/completions", GetCompletions);
    }

    private static async Task<IResult> GetAll(HabitService service, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        return Results.Ok(await service.GetAllAsync(userId, ct));
    }

    private static async Task<IResult> GetById(Guid id, HabitService service, HttpContext http, CancellationToken ct)
    {
        var habit = await service.GetByIdAsync(id, GetUserId(http), ct);
        return habit is null ? Results.NotFound() : Results.Ok(habit);
    }

    private static async Task<IResult> Create(CreateHabitRequest request, HabitService service,
        HttpContext http, CancellationToken ct)
    {
        var habit = await service.CreateAsync(request, GetUserId(http), ct);
        return Results.Created($"/api/habits/{habit.Id}", habit);
    }

    private static async Task<IResult> Update(Guid id, UpdateHabitRequest request, HabitService service,
        HttpContext http, CancellationToken ct)
    {
        var habit = await service.UpdateAsync(id, request, GetUserId(http), ct);
        return habit is null ? Results.NotFound() : Results.Ok(habit);
    }

    private static async Task<IResult> Delete(Guid id, HabitService service, HttpContext http, CancellationToken ct)
    {
        return await service.DeleteAsync(id, GetUserId(http), ct)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static async Task<IResult> MarkComplete(Guid habitId, CreateCompletionRequest request,
        HabitService service, HttpContext http, CancellationToken ct)
    {
        var completion = await service.MarkCompleteAsync(habitId, request, GetUserId(http), ct);
        return completion is null
            ? Results.NotFound()
            : Results.Created($"/api/habits/{habitId}/completions/{completion.Date}", completion);
    }

    private static async Task<IResult> UnmarkComplete(Guid habitId, DateOnly date,
        HabitService service, HttpContext http, CancellationToken ct)
    {
        return await service.UnmarkCompleteAsync(habitId, date, GetUserId(http), ct)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static async Task<IResult> GetCompletions(Guid habitId, HabitService service, HttpContext http,
        CancellationToken ct)
    {
        return Results.Ok(await service.GetCompletionsAsync(habitId, GetUserId(http), ct));
    }

    private static Guid GetUserId(HttpContext http)
    {
        var claim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated");

        return Guid.Parse(claim);
    }
}
