namespace HabitTracker.Models;

public class Habit
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public HashSet<DateOnly> CompletedDates { get; init; } = new();
}
