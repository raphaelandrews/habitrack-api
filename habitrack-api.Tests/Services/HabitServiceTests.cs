using Habitrack.Api.Dtos;
using Habitrack.Api.Models;
using Habitrack.Api.Repositories;
using Habitrack.Api.Services;
using Habitrack.Api.UnitOfWork;
using Moq;

namespace Habitrack.Api.Tests.Services;

public class HabitServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IHabitRepository> _habitRepoMock;
    private readonly HabitService _service;

    public HabitServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _habitRepoMock = new Mock<IHabitRepository>();

        _uowMock.Setup(u => u.Habits).Returns(_habitRepoMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

        _service = new HabitService(_uowMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsDtoWithCorrectData()
    {
        var request = new CreateHabitRequest
        {
            Name = "Exercício",
            Description = "30 min de corrida",
            WeeklyFrequency = 5,
            Color = "#FF0000"
        };
        var userId = Guid.NewGuid();

        Habit? captured = null;
        _habitRepoMock
            .Setup(r => r.Add(It.IsAny<Habit>()))
            .Callback<Habit>(h => captured = h);

        var result = await _service.CreateAsync(request, userId);

        Assert.NotNull(result);
        Assert.Equal("Exercício", result.Name);
        Assert.Equal(5, result.WeeklyFrequency);
        Assert.Equal("#FF0000", result.Color);
        Assert.Equal(0, result.CurrentStreak);
        Assert.Equal(0, result.BestStreak);
        Assert.Equal(0, result.TotalCompletions);
        Assert.False(result.IsActive == false && result.IsActive != true); // IsActive should be true by default

        Assert.NotNull(captured);
        Assert.Equal(userId, captured!.UserId);

        _habitRepoMock.Verify(r => r.Add(It.IsAny<Habit>()), Times.Once());
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task CreateAsync_UsesDefaultColorWhenNotProvided()
    {
        var request = new CreateHabitRequest
        {
            Name = "Leitura",
            WeeklyFrequency = 3,
            Color = null
        };
        var userId = Guid.NewGuid();

        Habit? captured = null;
        _habitRepoMock
            .Setup(r => r.Add(It.IsAny<Habit>()))
            .Callback<Habit>(h => captured = h);

        var result = await _service.CreateAsync(request, userId);

        Assert.Equal("#3B82F6", captured!.Color);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingHabit_ReturnsDtoWithStreaks()
    {
        var habitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var habit = new Habit
        {
            Id = habitId,
            UserId = userId,
            Name = "Exercício",
            WeeklyFrequency = 7,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Completions = new List<HabitCompletion>
            {
                new HabitCompletion
                {
                    Id = Guid.NewGuid(),
                    HabitId = habitId,
                    UserId = userId,
                    Date = today,
                    CompletedAt = DateTime.UtcNow
                }
            }
        };

        _habitRepoMock
            .Setup(r => r.GetByIdWithCompletionsAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(habit);

        var result = await _service.GetByIdAsync(habitId, userId);

        Assert.NotNull(result);
        Assert.Equal(habitId, result!.Id);
        Assert.Equal(1, result.CurrentStreak);
        Assert.Equal(1, result.BestStreak);
        Assert.Equal(1, result.TotalCompletions);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var habitId = Guid.NewGuid();

        _habitRepoMock
            .Setup(r => r.GetByIdWithCompletionsAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Habit?)null);

        var result = await _service.GetByIdAsync(habitId, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_DifferentUser_ReturnsNull()
    {
        var habitId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _habitRepoMock
            .Setup(r => r.GetByIdWithCompletionsAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Habit
            {
                Id = habitId,
                UserId = ownerId,
                Name = "Test",
                Completions = new List<HabitCompletion>()
            });

        var result = await _service.GetByIdAsync(habitId, otherUserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMappedDtos()
    {
        var userId = Guid.NewGuid();
        var habits = new List<Habit>
        {
            new Habit
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Habit 1",
                IsActive = true,
                Completions = new List<HabitCompletion>()
            },
            new Habit
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Habit 2",
                IsActive = true,
                Completions = new List<HabitCompletion>()
            }
        };

        _habitRepoMock
            .Setup(r => r.GetActiveByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(habits);

        var results = await _service.GetAllAsync(userId);

        Assert.Equal(2, results.Length);
        Assert.Contains(results, r => r.Name == "Habit 1");
        Assert.Contains(results, r => r.Name == "Habit 2");
    }

    [Fact]
    public async Task UpdateAsync_ExistingHabit_AppliesChanges()
    {
        var habitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var habit = new Habit
        {
            Id = habitId,
            UserId = userId,
            Name = "Old Name",
            Description = "Old Desc",
            WeeklyFrequency = 3,
            Color = "#000000"
        };

        _habitRepoMock
            .Setup(r => r.GetByIdAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(habit);

        _habitRepoMock
            .Setup(r => r.GetCompletionsAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HabitCompletion>());

        var request = new UpdateHabitRequest
        {
            Name = "New Name",
            WeeklyFrequency = 5
        };

        var result = await _service.UpdateAsync(habitId, request, userId);

        Assert.NotNull(result);
        Assert.Equal("New Name", result!.Name);
        Assert.Equal(5, result.WeeklyFrequency);
        Assert.Equal("Old Desc", result.Description); // not updated
        Assert.Equal("#000000", result.Color); // not updated

        _habitRepoMock.Verify(r => r.Update(It.IsAny<Habit>()), Times.Once());
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNull()
    {
        _habitRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Habit?)null);

        var result = await _service.UpdateAsync(Guid.NewGuid(), new UpdateHabitRequest(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ExistingHabit_SetsInactive()
    {
        var habitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var habit = new Habit
        {
            Id = habitId,
            UserId = userId,
            Name = "To Delete",
            IsActive = true
        };

        _habitRepoMock
            .Setup(r => r.GetByIdAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(habit);

        var result = await _service.DeleteAsync(habitId, userId);

        Assert.True(result);
        Assert.False(habit.IsActive);
        Assert.NotNull(habit.EndedAt);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        _habitRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Habit?)null);

        var result = await _service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task MarkCompleteAsync_ValidRequest_CreatesCompletion()
    {
        var habitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 13);

        _habitRepoMock
            .Setup(r => r.GetByIdAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Habit { Id = habitId, UserId = userId, Name = "Test" });

        _habitRepoMock
            .Setup(r => r.HasCompletionOnDateAsync(habitId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.MarkCompleteAsync(habitId, new CreateCompletionRequest { Date = date }, userId);

        Assert.NotNull(result);
        Assert.Equal(date, result!.Date);

        _habitRepoMock.Verify(r => r.AddCompletion(It.IsAny<HabitCompletion>()), Times.Once());
    }

    [Fact]
    public async Task MarkCompleteAsync_AlreadyCompleted_ReturnsExisting()
    {
        var habitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 13);
        var existing = new HabitCompletion
        {
            Id = Guid.NewGuid(),
            HabitId = habitId,
            UserId = userId,
            Date = date,
            CompletedAt = DateTime.UtcNow
        };

        _habitRepoMock
            .Setup(r => r.GetByIdAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Habit { Id = habitId, UserId = userId });

        _habitRepoMock
            .Setup(r => r.HasCompletionOnDateAsync(habitId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _habitRepoMock
            .Setup(r => r.GetCompletionsAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HabitCompletion> { existing });

        var result = await _service.MarkCompleteAsync(habitId, new CreateCompletionRequest { Date = date }, userId);

        Assert.NotNull(result);
        Assert.Equal(existing.Id, result!.Id);

        _habitRepoMock.Verify(r => r.AddCompletion(It.IsAny<HabitCompletion>()), Times.Never());
    }

    [Fact]
    public async Task UnmarkCompleteAsync_ExistingCompletion_RemovesIt()
    {
        var habitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 13);
        var completion = new HabitCompletion { Id = Guid.NewGuid(), HabitId = habitId, Date = date };

        _habitRepoMock
            .Setup(r => r.GetByIdAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Habit { Id = habitId, UserId = userId });

        _habitRepoMock
            .Setup(r => r.GetCompletionsAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HabitCompletion> { completion });

        var result = await _service.UnmarkCompleteAsync(habitId, date, userId);

        Assert.True(result);
        _habitRepoMock.Verify(r => r.RemoveCompletion(completion), Times.Once());
    }

    [Fact]
    public async Task UnmarkCompleteAsync_NoCompletion_ReturnsFalse()
    {
        var habitId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _habitRepoMock
            .Setup(r => r.GetByIdAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Habit { Id = habitId, UserId = userId });

        _habitRepoMock
            .Setup(r => r.GetCompletionsAsync(habitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HabitCompletion>());

        var result = await _service.UnmarkCompleteAsync(habitId, new DateOnly(2026, 1, 1), userId);

        Assert.False(result);
    }
}
