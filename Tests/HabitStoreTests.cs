using HabitTracker.Models;
using HabitTracker.Services;

namespace day2_habit_tracker.Tests;

public class HabitStoreTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Creates a fresh HabitStore with no pre-seeded data.</summary>
    private static HabitStore CreateStore() => new HabitStore();

    // -------------------------------------------------------------------------
    // 1. NewHabit_HasZeroStreak
    // -------------------------------------------------------------------------

    [Fact]
    public void NewHabit_HasZeroStreak()
    {
        // Arrange
        var store = CreateStore();
        var habit = store.Add("Exercise");

        // Act
        var streak = store.CalculateStreak(habit);

        // Assert
        Assert.Equal(0, streak);
    }

    // -------------------------------------------------------------------------
    // 2. CompleteToday_StreakIsOne
    // -------------------------------------------------------------------------

    [Fact]
    public void CompleteToday_StreakIsOne()
    {
        // Arrange
        var store = CreateStore();
        var habit = store.Add("Read");

        // Act
        habit.CompletedDates.Add(DateOnly.FromDateTime(DateTime.Today));
        var streak = store.CalculateStreak(habit);

        // Assert
        Assert.Equal(1, streak);
    }

    // -------------------------------------------------------------------------
    // 3. CompleteYesterdayOnly_StreakIsOne
    //    Yesterday only (not today) still counts as an active streak of 1
    //    because the day is not yet over.
    // -------------------------------------------------------------------------

    [Fact]
    public void CompleteYesterdayOnly_StreakIsOne()
    {
        // Arrange
        var store = CreateStore();
        var habit = store.Add("Meditate");
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));

        // Act
        habit.CompletedDates.Add(yesterday);
        var streak = store.CalculateStreak(habit);

        // Assert
        Assert.Equal(1, streak);
    }

    // -------------------------------------------------------------------------
    // 4. CompleteTodayAndYesterday_StreakIsTwo
    // -------------------------------------------------------------------------

    [Fact]
    public void CompleteTodayAndYesterday_StreakIsTwo()
    {
        // Arrange
        var store = CreateStore();
        var habit = store.Add("Drink Water");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);

        // Act
        habit.CompletedDates.Add(today);
        habit.CompletedDates.Add(yesterday);
        var streak = store.CalculateStreak(habit);

        // Assert
        Assert.Equal(2, streak);
    }

    // -------------------------------------------------------------------------
    // 5. MissedYesterday_StreakIsZero
    //    Completed 2 days ago but not yesterday or today → broken streak = 0
    // -------------------------------------------------------------------------

    [Fact]
    public void MissedYesterday_StreakIsZero()
    {
        // Arrange
        var store = CreateStore();
        var habit = store.Add("Journal");
        var twoDaysAgo = DateOnly.FromDateTime(DateTime.Today.AddDays(-2));

        // Act
        habit.CompletedDates.Add(twoDaysAgo);
        var streak = store.CalculateStreak(habit);

        // Assert
        Assert.Equal(0, streak);
    }

    // -------------------------------------------------------------------------
    // 6. Toggle_CompleteThenUncomplete_TodayDoneFalse
    //    Calling ToggleToday twice should leave TodayDone as false (toggled off).
    // -------------------------------------------------------------------------

    [Fact]
    public void Toggle_CompleteThenUncomplete_TodayDoneFalse()
    {
        // Arrange
        var store = CreateStore();
        var habit = store.Add("Stretch");

        // Act – first toggle marks today complete
        store.ToggleToday(habit);
        // Act – second toggle un-marks today
        var todayDone = store.ToggleToday(habit);

        // Assert
        Assert.False(todayDone);
        Assert.DoesNotContain(DateOnly.FromDateTime(DateTime.Today), habit.CompletedDates);
    }

    // -------------------------------------------------------------------------
    // 7. Delete_NonExistentId_ReturnsFalse
    // -------------------------------------------------------------------------

    [Fact]
    public void Delete_NonExistentId_ReturnsFalse()
    {
        // Arrange
        var store = CreateStore();
        var randomId = Guid.NewGuid();

        // Act
        var result = store.Delete(randomId);

        // Assert
        Assert.False(result);
    }

    // -------------------------------------------------------------------------
    // 8. Add_EmptyName_ThrowsArgumentException
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_EmptyName_ThrowsArgumentException()
    {
        // Arrange
        var store = CreateStore();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => store.Add(""));
    }

    // -------------------------------------------------------------------------
    // 9. Add_NullName_ThrowsArgumentException
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_NullName_ThrowsArgumentException()
    {
        // Arrange
        var store = CreateStore();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => store.Add(null!));
    }

    // -------------------------------------------------------------------------
    // 10. LongConsecutiveStreak_CountsCorrectly
    //     Seed 30 consecutive days ending today → streak = 30
    // -------------------------------------------------------------------------

    [Fact]
    public void LongConsecutiveStreak_CountsCorrectly()
    {
        // Arrange
        var store = CreateStore();
        var habit = store.Add("Cold Shower");
        var today = DateOnly.FromDateTime(DateTime.Today);

        for (int i = 0; i < 30; i++)
        {
            habit.CompletedDates.Add(today.AddDays(-i));
        }

        // Act
        var streak = store.CalculateStreak(habit);

        // Assert
        Assert.Equal(30, streak);
    }

    // -------------------------------------------------------------------------
    // 11. Add_WhitespaceName_ThrowsArgumentException
    //     A name composed entirely of whitespace must be rejected.
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_WhitespaceName_ThrowsArgumentException()
    {
        // Arrange
        var store = CreateStore();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => store.Add("   "));
    }

    // -------------------------------------------------------------------------
    // 12. Add_100CharName_Succeeds
    //     A name of exactly 100 characters is at the boundary and must be accepted.
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_100CharName_Succeeds()
    {
        // Arrange
        var store = CreateStore();
        var name = new string('A', 100);

        // Act
        var habit = store.Add(name);

        // Assert – no exception, and the habit is stored with the expected name
        Assert.NotNull(habit);
        Assert.Equal(name, habit.Name);
    }

    // -------------------------------------------------------------------------
    // 13. Add_101CharName_ThrowsArgumentException
    //     A name of 101 characters exceeds the limit and must be rejected.
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_101CharName_ThrowsArgumentException()
    {
        // Arrange
        var store = CreateStore();
        var tooLong = new string('A', 101);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => store.Add(tooLong));
    }

    // -------------------------------------------------------------------------
    // 14. TwoHabits_StreaksAreIndependent
    //     Completing habit1 today must not affect habit2's streak.
    // -------------------------------------------------------------------------

    [Fact]
    public void TwoHabits_StreaksAreIndependent()
    {
        // Arrange
        var store = CreateStore();
        var habit1 = store.Add("Habit One");
        var habit2 = store.Add("Habit Two");

        // Act – complete only habit1 today
        store.ToggleToday(habit1);

        // Assert
        Assert.Equal(1, store.CalculateStreak(habit1));
        Assert.Equal(0, store.CalculateStreak(habit2));
    }

    // -------------------------------------------------------------------------
    // 15. ToggleToday_ReturnsTrue_OnFirstCall
    //     The first call to ToggleToday must return true (day added).
    // -------------------------------------------------------------------------

    [Fact]
    public void ToggleToday_ReturnsTrue_OnFirstCall()
    {
        // Arrange
        var store = CreateStore();
        var habit = store.Add("Piano Practice");

        // Act
        var result = store.ToggleToday(habit);

        // Assert
        Assert.True(result);
        Assert.Contains(DateOnly.FromDateTime(DateTime.Today), habit.CompletedDates);
    }
}
