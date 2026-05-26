using System.Collections.Concurrent;
using HabitTracker.Models;

namespace HabitTracker.Services;

/// <summary>
/// Thread-safe in-memory store for habits. Registered as a singleton.
/// A private lock guards all HashSet reads/writes because HashSet is not thread-safe.
/// </summary>
public class HabitStore
{
    private readonly ConcurrentDictionary<Guid, Habit> _habits = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates and stores a new habit with the given name.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null, empty/whitespace, or longer than 100 characters.
    /// </exception>
    public Habit Add(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Habit name is required.", nameof(name));

        if (name.Trim().Length > 100)
            throw new ArgumentException("Habit name must be 100 characters or less.", nameof(name));

        if (_habits.Count >= 500)
            throw new InvalidOperationException("Maximum of 500 habits reached.");

        var habit = new Habit { Name = name.Trim() };
        _habits[habit.Id] = habit;
        return habit;
    }

    /// <summary>Returns all habits as a read-only list.</summary>
    public IReadOnlyList<Habit> GetAll() => _habits.Values.ToList();

    /// <summary>O(1) lookup by id. Returns null if not found.</summary>
    public Habit? TryGet(Guid id) => _habits.TryGetValue(id, out var h) ? h : null;

    /// <summary>Thread-safe check of whether today is in the habit's CompletedDates.</summary>
    public bool IsTodayDone(Habit habit)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        lock (_lock)
        {
            return habit.CompletedDates.Contains(today);
        }
    }

    /// <summary>
    /// Toggles today's completion for the given habit.
    /// Returns true if today was added (habit is now complete), false if it was removed.
    /// </summary>
    public bool ToggleToday(Habit habit)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        lock (_lock)
        {
            if (habit.CompletedDates.Contains(today))
            {
                habit.CompletedDates.Remove(today);
                return false;
            }

            habit.CompletedDates.Add(today);
            return true;
        }
    }

    /// <summary>
    /// Removes the habit with the given id.
    /// Returns true if found and removed, false if no habit with that id exists.
    /// </summary>
    public bool Delete(Guid id) => _habits.TryRemove(id, out _);

    /// <summary>
    /// Calculates the current consecutive-day streak for the given habit.
    /// The streak is anchored to today if completed today, or yesterday if the
    /// day is not yet over. A gap of two or more days resets the streak to zero.
    /// </summary>
    public int CalculateStreak(Habit habit)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);

        DateOnly anchor;
        lock (_lock)
        {
            if (habit.CompletedDates.Contains(today))
                anchor = today;
            else if (habit.CompletedDates.Contains(yesterday))
                anchor = yesterday;
            else
                return 0;

            var streak = 1;
            var cursor = anchor.AddDays(-1);
            while (habit.CompletedDates.Contains(cursor))
            {
                streak++;
                cursor = cursor.AddDays(-1);
            }
            return streak;
        }
    }
}
