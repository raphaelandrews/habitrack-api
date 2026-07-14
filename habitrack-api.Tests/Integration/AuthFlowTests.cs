using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Habitrack.Api.Data;
using Habitrack.Api.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Habitrack.Api.Tests.Integration;

public class AuthFlowTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public AuthFlowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql("Host=localhost;Database=habitrack_test;Username=postgres;Password=postgres"));
            });
        });
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FullAuthFlow_Register_Login_AccessProtectedEndpoint()
    {
        // ─── Step 1: Register ───
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = "flowtest@example.com",
            Password = "Senha@123"
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registerResult);
        Assert.NotNull(registerResult!.Token);
        Assert.Equal("flowtest@example.com", registerResult.User.Email);

        // ─── Step 2: Login ───
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "flowtest@example.com",
            Password = "Senha@123"
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginResult);
        Assert.NotNull(loginResult!.Token);

        // ─── Step 3: Access protected endpoint ───
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult.Token);

        var habitsResponse = await _client.GetAsync("/api/habits");

        Assert.Equal(HttpStatusCode.OK, habitsResponse.StatusCode);
        var habits = await habitsResponse.Content.ReadFromJsonAsync<HabitDto[]>();
        Assert.NotNull(habits);
        Assert.Empty(habits!);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = "dupe@example.com",
            Password = "Senha@123"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = "dupe@example.com",
            Password = "AnotherPass@456"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = "loginfail@example.com",
            Password = "Correct@123"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "loginfail@example.com",
            Password = "WrongPass@456"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetHabits_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/habits");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndListHabits_FullCrudFlow()
    {
        // Register and get token
        var authResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = "crudtest@example.com",
            Password = "Senha@123"
        });
        var auth = await authResponse.Content.ReadFromJsonAsync<AuthResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Token);

        // Create habit
        var createResponse = await _client.PostAsJsonAsync("/api/habits", new
        {
            Name = "Correr",
            Description = "5km no parque",
            WeeklyFrequency = 5,
            Color = "#22C55E"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(created);
        Assert.Equal("Correr", created!.Name);

        // List habits
        var listResponse = await _client.GetAsync("/api/habits");
        var habits = await listResponse.Content.ReadFromJsonAsync<HabitDto[]>();
        Assert.Single(habits!);

        // Get by id
        var getResponse = await _client.GetAsync($"/api/habits/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // Update
        var updateResponse = await _client.PutAsJsonAsync($"/api/habits/{created.Id}", new
        {
            Name = "Correr 10km"
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<HabitDto>();
        Assert.Equal("Correr 10km", updated!.Name);

        // Mark completion
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var completeResponse = await _client.PostAsJsonAsync(
            $"/api/habits/{created.Id}/completions",
            new { Date = today });

        Assert.Equal(HttpStatusCode.Created, completeResponse.StatusCode);

        // Soft delete
        var deleteResponse = await _client.DeleteAsync($"/api/habits/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify soft-deleted (returned with IsActive=false)
        var getAfterDelete = await _client.GetAsync($"/api/habits/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getAfterDelete.StatusCode);
        var deleted = await getAfterDelete.Content.ReadFromJsonAsync<HabitDto>();
        Assert.NotNull(deleted);
        Assert.False(deleted!.IsActive);
    }
}
