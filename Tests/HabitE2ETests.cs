using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace day2_habit_tracker.Tests;

/// <summary>
/// End-to-end tests that exercise full user journeys through the API.
/// Each test creates its own WebApplicationFactory so it gets a completely
/// fresh in-memory HabitStore — no shared state between tests.
/// </summary>
public class HabitE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    // -------------------------------------------------------------------------
    // Factory / client helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a fresh WebApplicationFactory (and thus a fresh HabitStore
    /// singleton) for every call.  Each test calls this once so its state
    /// is completely isolated from every other test.
    /// </summary>
    private static HttpClient CreateFreshClient()
    {
        var factory = new WebApplicationFactory<Program>();
        return factory.CreateClient();
    }

    private static StringContent JsonBody(object payload) =>
        new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

    /// <summary>POST /api/habits, assert 201, return the created DTO.</summary>
    private static async Task<HabitDto> CreateHabitAsync(HttpClient client, string name)
    {
        var response = await client.PostAsync("/api/habits", JsonBody(new { name }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<HabitDto>(JsonOptions);
        Assert.NotNull(dto);
        return dto!;
    }

    /// <summary>GET /api/habits, assert 200, return the full list.</summary>
    private static async Task<HabitDto[]> GetAllHabitsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/habits");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var habits = await response.Content.ReadFromJsonAsync<HabitDto[]>(JsonOptions);
        Assert.NotNull(habits);
        return habits!;
    }

    // -------------------------------------------------------------------------
    // 1. FullFlow_CreateCompleteDelete
    //    POST create → GET list (habit appears) → POST complete → GET list
    //    (todayDone=true, streak=1) → DELETE → GET list (habit gone)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FullFlow_CreateCompleteDelete()
    {
        var client = CreateFreshClient();

        // Step 1: create a habit
        var created = await CreateHabitAsync(client, "Morning Run");
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Morning Run", created.Name);
        Assert.False(created.TodayDone);
        Assert.Equal(0, created.Streak);

        // Step 2: verify habit appears in GET list
        var listAfterCreate = await GetAllHabitsAsync(client);
        var fromList = Assert.Single(listAfterCreate, h => h.Id == created.Id);
        Assert.Equal("Morning Run", fromList.Name);
        Assert.False(fromList.TodayDone);

        // Step 3: complete the habit today
        var completeResponse = await client.PostAsync($"/api/habits/{created.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        // Step 4: GET list — habit must show todayDone=true and streak=1
        var listAfterComplete = await GetAllHabitsAsync(client);
        var afterComplete = Assert.Single(listAfterComplete, h => h.Id == created.Id);
        Assert.True(afterComplete.TodayDone);
        Assert.Equal(1, afterComplete.Streak);

        // Step 5: delete the habit
        var deleteResponse = await client.DeleteAsync($"/api/habits/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Step 6: GET list — habit must be gone
        var listAfterDelete = await GetAllHabitsAsync(client);
        Assert.DoesNotContain(listAfterDelete, h => h.Id == created.Id);
    }

    // -------------------------------------------------------------------------
    // 2. MultipleHabits_EachTrackedIndependently
    //    Create two habits → complete only habit1 → GET list →
    //    assert habit1.todayDone=true, habit2.todayDone=false
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MultipleHabits_EachTrackedIndependently()
    {
        var client = CreateFreshClient();

        // Create two habits
        var habit1 = await CreateHabitAsync(client, "Read 30 Minutes");
        var habit2 = await CreateHabitAsync(client, "Drink Water");

        // Complete only habit1
        var completeResponse = await client.PostAsync($"/api/habits/{habit1.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        // GET full list and locate both habits
        var list = await GetAllHabitsAsync(client);
        Assert.Equal(2, list.Length);

        var h1 = Assert.Single(list, h => h.Id == habit1.Id);
        var h2 = Assert.Single(list, h => h.Id == habit2.Id);

        // habit1 must be done; habit2 must still be pending
        Assert.True(h1.TodayDone);
        Assert.False(h2.TodayDone);

        // Streak independence: completing habit1 must not affect habit2
        Assert.Equal(1, h1.Streak);
        Assert.Equal(0, h2.Streak);
    }

    // -------------------------------------------------------------------------
    // 3. StreakIncrements_AfterCompletion
    //    Create habit → complete → GET → assert streak=1 (just completed today)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StreakIncrements_AfterCompletion()
    {
        var client = CreateFreshClient();

        // Arrange: create a fresh habit (streak must be 0)
        var created = await CreateHabitAsync(client, "Evening Walk");
        Assert.Equal(0, created.Streak);

        // Act: mark it done today
        var completeResponse = await client.PostAsync($"/api/habits/{created.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        // Assert via GET list: streak is now 1
        var list = await GetAllHabitsAsync(client);
        var habit = Assert.Single(list, h => h.Id == created.Id);
        Assert.True(habit.TodayDone);
        Assert.Equal(1, habit.Streak);
    }

    // -------------------------------------------------------------------------
    // 4. ToggleComplete_TwiceReturnsToUndone
    //    Create → complete → complete again → GET list → assert todayDone=false
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ToggleComplete_TwiceReturnsToUndone()
    {
        var client = CreateFreshClient();

        // Create
        var created = await CreateHabitAsync(client, "Meditate");

        // First complete: todayDone → true
        var firstToggle = await client.PostAsync($"/api/habits/{created.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, firstToggle.StatusCode);
        var afterFirst = await firstToggle.Content.ReadFromJsonAsync<HabitDto>(JsonOptions);
        Assert.NotNull(afterFirst);
        Assert.True(afterFirst!.TodayDone);

        // Second complete: toggles back to false
        var secondToggle = await client.PostAsync($"/api/habits/{created.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, secondToggle.StatusCode);
        var afterSecond = await secondToggle.Content.ReadFromJsonAsync<HabitDto>(JsonOptions);
        Assert.NotNull(afterSecond);
        Assert.False(afterSecond!.TodayDone);

        // Confirm via GET list — the list must also show todayDone=false
        var list = await GetAllHabitsAsync(client);
        var habit = Assert.Single(list, h => h.Id == created.Id);
        Assert.False(habit.TodayDone);

        // Streak must be 0 because today is no longer marked complete
        Assert.Equal(0, habit.Streak);
    }

    // -------------------------------------------------------------------------
    // Local DTO used only for deserialising API responses.
    // -------------------------------------------------------------------------

    private sealed record HabitDto(
        Guid Id,
        string Name,
        int Streak,
        bool TodayDone);
}
