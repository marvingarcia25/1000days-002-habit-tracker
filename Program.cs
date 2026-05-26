using HabitTracker.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSingleton<HabitStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.MapGet("/api/habits", (HabitStore store) =>
{
    var result = store.GetAll().Select(h => new
    {
        id = h.Id,
        name = h.Name,
        createdAt = h.CreatedAt,
        streak = store.CalculateStreak(h),
        todayDone = store.IsTodayDone(h)
    });
    return Results.Ok(result);
});

app.MapPost("/api/habits", (CreateHabitRequest req, HabitStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Trim().Length > 100)
        return Results.BadRequest(new { error = "Name is required and must be 100 characters or less." });

    HabitTracker.Models.Habit habit;
    try { habit = store.Add(req.Name); }
    catch (InvalidOperationException) { return Results.BadRequest(new { error = "Maximum habit limit reached." }); }
    return Results.Created($"/api/habits/{habit.Id}", new
    {
        id = habit.Id,
        name = habit.Name,
        createdAt = habit.CreatedAt,
        streak = 0,
        todayDone = false
    });
});

app.MapPost("/api/habits/{id:guid}/complete", (Guid id, HabitStore store) =>
{
    var habit = store.TryGet(id);
    if (habit is null)
        return Results.NotFound();

    var todayDone = store.ToggleToday(habit);
    return Results.Ok(new
    {
        id = habit.Id,
        todayDone,
        streak = store.CalculateStreak(habit)
    });
});

app.MapDelete("/api/habits/{id:guid}", (Guid id, HabitStore store) =>
{
    return store.Delete(id) ? Results.NoContent() : Results.NotFound();
});

app.Run();

record CreateHabitRequest(string? Name);

public partial class Program { }
