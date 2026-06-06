# day2_HabitTracker — Habit Tracker

Track daily habits and watch your streaks grow.

An ASP.NET Razor Pages app backed by a thread-safe in-memory store. Add habits, tick them off for the day, and it works out your current consecutive-day streak — broken the moment you miss a day.

## What it does

- Add and delete habits (name validated, capped at 500)
- Toggle today's completion per habit
- Current streak per habit — consecutive days, anchored to today (or yesterday until the day's over), reset by any gap

## Stack

- ASP.NET Core (Razor Pages, .NET 8)
- In-memory `HabitStore` — singleton, lock-guarded
- xUnit tests + GitHub Actions deploy workflow

## Running it

```
dotnet run
```

## Tests

```
dotnet test
```

Covers the store (add/validate/toggle/streak) plus API and end-to-end tests.

---

Day 2 of building a small thing every day.
