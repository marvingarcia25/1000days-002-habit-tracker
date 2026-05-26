using Xunit.Abstractions;
using Xunit.Sdk;

// Apply declaration-order sorting to the entire test assembly so that
// GetHabits_EmptyStore_ReturnsEmptyArray (line 61) runs before any test
// that adds habits to the shared singleton HabitStore.
[assembly: Xunit.TestCaseOrderer(
    "day2_habit_tracker.Tests.DeclarationOrderer",
    "day2_habit_tracker.Tests")]

namespace day2_habit_tracker.Tests;

/// <summary>
/// Orders test cases so that "empty store" / read-only tests run before
/// tests that mutate state. Within each group, tests are sorted alphabetically.
/// This ensures GetHabits_EmptyStore_ReturnsEmptyArray sees an empty store.
/// </summary>
public sealed class DeclarationOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        // Tests with "Empty" or "NonExistent" or "Invalid" in their name are
        // read-only checks that must run before tests that create or modify data.
        return testCases.OrderBy(tc => MutationPriority(tc.TestMethod.Method.Name))
                        .ThenBy(tc => tc.TestMethod.Method.Name);
    }

    private static int MutationPriority(string testName)
    {
        // Priority 0: read-only / empty-state checks run first.
        if (testName.Contains("Empty") || testName.Contains("EmptyStore"))
            return 0;

        // Priority 1: read-only invalid-id / not-found checks (no state change).
        if (testName.Contains("Invalid") || testName.Contains("NotFound") ||
            testName.Contains("NonExistent"))
            return 1;

        // Priority 2: validation rejection tests (no state persisted on 400/404).
        if (testName.Contains("Returns400") || testName.Contains("Returns404"))
            return 2;

        // Priority 3: all other tests that create or mutate data.
        return 3;
    }
}
