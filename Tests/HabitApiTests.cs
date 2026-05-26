using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace day2_habit_tracker.Tests;

/// <summary>
/// Integration tests that spin up the full ASP.NET Core pipeline via
/// WebApplicationFactory and exercise the /api/habits endpoints.
/// These tests share a single factory instance to avoid re-creating the
/// host on every test, but each test operates on a fresh in-memory store
/// because the DI container scope is reset per-request.
///
/// NOTE: The production Program class must declare
///   public partial class Program {}
/// at the bottom of Program.cs so this assembly can reference it.
/// </summary>
public class HabitApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public HabitApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static StringContent JsonBody(object payload) =>
        new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

    /// <summary>
    /// POST /api/habits and assert 201 Created, returning the deserialized body.
    /// </summary>
    private async Task<HabitDto> CreateHabitAsync(string name)
    {
        var response = await _client.PostAsync("/api/habits", JsonBody(new { name }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<HabitDto>(JsonOptions);
        Assert.NotNull(dto);
        return dto!;
    }

    // -------------------------------------------------------------------------
    // 1. GetHabits_EmptyStore_ReturnsEmptyArray
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetHabits_EmptyStore_ReturnsEmptyArray()
    {
        // Act
        var response = await _client.GetAsync("/api/habits");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var habits = await response.Content.ReadFromJsonAsync<HabitDto[]>(JsonOptions);
        Assert.NotNull(habits);
        Assert.Empty(habits!);
    }

    // -------------------------------------------------------------------------
    // 2. PostHabit_ValidName_Returns201
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostHabit_ValidName_Returns201()
    {
        // Act
        var response = await _client.PostAsync("/api/habits", JsonBody(new { name = "Morning Run" }));

        // Assert — status
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Assert — body shape
        var dto = await response.Content.ReadFromJsonAsync<HabitDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto!.Id);
        Assert.Equal("Morning Run", dto.Name);
        Assert.Equal(0, dto.Streak);
        Assert.False(dto.TodayDone);
    }

    // -------------------------------------------------------------------------
    // 3. PostHabit_EmptyName_Returns400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostHabit_EmptyName_Returns400()
    {
        // Act
        var response = await _client.PostAsync("/api/habits", JsonBody(new { name = "" }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // 4. PostHabit_NullName_Returns400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostHabit_NullName_Returns400()
    {
        // Act – send an explicit JSON null for the name property
        var response = await _client.PostAsync("/api/habits", JsonBody(new { name = (string?)null }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // 5. PostHabit_NameTooLong_Returns400
    //    Names longer than 100 characters should be rejected.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostHabit_NameTooLong_Returns400()
    {
        // Arrange – 101 characters
        var tooLong = new string('A', 101);

        // Act
        var response = await _client.PostAsync("/api/habits", JsonBody(new { name = tooLong }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // 6. CompleteHabit_ValidId_Returns200WithTodayDoneTrue
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CompleteHabit_ValidId_Returns200WithTodayDoneTrue()
    {
        // Arrange – create a habit first
        var created = await CreateHabitAsync("Evening Walk");

        // Act
        var response = await _client.PostAsync($"/api/habits/{created.Id}/complete", null);

        // Assert – status
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert – body reports todayDone = true
        var dto = await response.Content.ReadFromJsonAsync<HabitDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.True(dto!.TodayDone);
    }

    // -------------------------------------------------------------------------
    // 7. CompleteHabit_InvalidId_Returns404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CompleteHabit_InvalidId_Returns404()
    {
        // Arrange
        var randomId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/habits/{randomId}/complete", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // 8. DeleteHabit_ValidId_Returns204
    //    After deletion the habit must no longer appear in GET /api/habits.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteHabit_ValidId_Returns204()
    {
        // Arrange
        var created = await CreateHabitAsync("Yoga");

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/habits/{created.Id}");

        // Assert – 204 No Content
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Assert – habit is gone from the list
        var listResponse = await _client.GetAsync("/api/habits");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var habits = await listResponse.Content.ReadFromJsonAsync<HabitDto[]>(JsonOptions);
        Assert.NotNull(habits);
        Assert.DoesNotContain(habits!, h => h.Id == created.Id);
    }

    // -------------------------------------------------------------------------
    // 9. DeleteHabit_InvalidId_Returns404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteHabit_InvalidId_Returns404()
    {
        // Arrange
        var randomId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/habits/{randomId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // 10. CompleteHabit_Twice_TogglesBack
    //     First POST marks complete (todayDone=true).
    //     Second POST toggles it back (todayDone=false).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CompleteHabit_Twice_TogglesBack()
    {
        // Arrange
        var created = await CreateHabitAsync("Floss");

        // Act – first toggle
        var firstResponse = await _client.PostAsync($"/api/habits/{created.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var afterFirst = await firstResponse.Content.ReadFromJsonAsync<HabitDto>(JsonOptions);
        Assert.NotNull(afterFirst);
        Assert.True(afterFirst!.TodayDone);

        // Act – second toggle (should undo the first)
        var secondResponse = await _client.PostAsync($"/api/habits/{created.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var afterSecond = await secondResponse.Content.ReadFromJsonAsync<HabitDto>(JsonOptions);
        Assert.NotNull(afterSecond);

        // Assert
        Assert.False(afterSecond!.TodayDone);
    }

    // -------------------------------------------------------------------------
    // 11. PostHabit_100CharName_Returns201
    //     A name of exactly 100 characters is at the boundary and must be accepted.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostHabit_100CharName_Returns201()
    {
        // Arrange – exactly 100 characters
        var name = new string('B', 100);

        // Act
        var response = await _client.PostAsync("/api/habits", JsonBody(new { name }));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<HabitDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(name, dto!.Name);
    }

    // -------------------------------------------------------------------------
    // 12. PostHabit_101CharName_Returns400
    //     A name of 101 characters exceeds the limit and must be rejected.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostHabit_101CharName_Returns400()
    {
        // Arrange – 101 characters (one over the boundary)
        var tooLong = new string('C', 101);

        // Act
        var response = await _client.PostAsync("/api/habits", JsonBody(new { name = tooLong }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // 13. CreatedThenDeleted_HabitAbsentFromGetList
    //     After creation and deletion the habit must not appear in GET /api/habits.
    //     (Complements test 8 by asserting the list contents explicitly after delete.)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreatedThenDeleted_HabitAbsentFromGetList()
    {
        // Arrange – create a habit to be deleted
        var created = await CreateHabitAsync("Temporary Habit");

        // Act – delete it
        var deleteResponse = await _client.DeleteAsync($"/api/habits/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Act – fetch the full list
        var listResponse = await _client.GetAsync("/api/habits");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var habits = await listResponse.Content.ReadFromJsonAsync<HabitDto[]>(JsonOptions);

        // Assert – the deleted habit is not in the list
        Assert.NotNull(habits);
        Assert.DoesNotContain(habits!, h => h.Id == created.Id);
    }

    // -------------------------------------------------------------------------
    // 14. PostHabit_WhitespaceName_Returns400
    //     A name that is whitespace only must be rejected with 400 Bad Request.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostHabit_WhitespaceName_Returns400()
    {
        // Act
        var response = await _client.PostAsync("/api/habits", JsonBody(new { name = "   " }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Local DTO record used only for deserialising API responses.
    // Mirrors the shape the API will return; property names are matched
    // case-insensitively via JsonOptions.
    // -------------------------------------------------------------------------

    private sealed record HabitDto(
        Guid Id,
        string Name,
        int Streak,
        bool TodayDone);
}
