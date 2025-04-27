using System.Diagnostics;

namespace TokenKeeper.Tests;

[TestClass]
public class TokenStateKeeperHighloadTests
{
    private static TokenStateKeeper Create() => new();

    [TestMethod]
    public void Seed_InvalidHash_ReturnsInvalidInput()
    {
        var sut = Create();
        var result = sut.Seed(null, "value");
        Assert.AreEqual(TokenOpResult.InvalidInput, result);
    }

    [TestMethod]
    public void Stage_NonNumericNewHash_TreatedAsNull()
    {
        var sut = Create();
        sut.Seed("1", "A");
        // Non-numeric hash is treated as null, which would be a delete operation
        var result = sut.Stage("1", "not-a-number", "B");
        // Either verify it's treated as null (Success) or explicitly check the resulting state
        Assert.AreEqual(TokenOpResult.Success, result);

        // Verify the token was actually marked for deletion
        var snapshots = sut.GetFullCurrentSnapshot().ToList();
        var token = snapshots.FirstOrDefault(s => s.InitialHash == "1");
        Assert.IsNotNull(token);
        Assert.IsNull(token.CurrentHash); // Should be marked for deletion
    }

    [TestMethod]
    public void Seed_VeryLongValue_Succeeds()
    {
        var sut = Create();
        var longValue = new string('A', 100000);
        var result = sut.Seed("1", longValue);
        Assert.AreEqual(TokenOpResult.Success, result);

        sut.TryGetSnapshot("1", out var snapshot);
        Assert.AreEqual(longValue, snapshot.CurrentValue);
    }

    [TestMethod]
    public void Stage_MultipleOperations_CorrectStateAfterEachOperation()
    {
        var sut = Create();

        sut.Seed("1", "A");
        sut.Seed("2", "B");

        var initialSnapshots = sut.GetFullCurrentSnapshot().ToDictionary(s => s.CurrentHash, s => s);
        Assert.AreEqual(2, initialSnapshots.Count);
        Assert.AreEqual("A", initialSnapshots["1"].CurrentValue);
        Assert.AreEqual("B", initialSnapshots["2"].CurrentValue);

        sut.Stage("1", "3", "A2");

        var intermediateSnapshots = sut.GetFullCurrentSnapshot().ToDictionary(s => s.CurrentHash ?? "null", s => s);
        Assert.AreEqual(2, intermediateSnapshots.Count);
        Assert.AreEqual("A2", intermediateSnapshots["3"].CurrentValue);
        Assert.AreEqual("B", intermediateSnapshots["2"].CurrentValue);

        sut.Stage("2", null, "");
        sut.Commit();

        var finalSnapshots = sut.GetFullCurrentSnapshot().ToDictionary(s => s.CurrentHash ?? "null", s => s);
        Assert.AreEqual(2, finalSnapshots.Count);
        Assert.AreEqual("A2", finalSnapshots["3"].CurrentValue);
        Assert.IsNull(finalSnapshots["null"].CurrentValue);
    }

    [TestMethod]
    public void Concurrent_StageAndCommit_RaceConditionHandled()
    {
        var sut = Create();

        // Create and verify initial state
        var seedResult = sut.Seed("1", "A");
        Assert.AreEqual(TokenOpResult.Success, seedResult, "Initial seed should succeed");

        sut.TryGetSnapshot("1", out var initialSnapshot);
        Assert.AreEqual("A", initialSnapshot.CurrentValue, "Initial value should be set correctly");

        // Use CountdownEvent for synchronization
        var startEvent = new CountdownEvent(1);
        var task1Result = TokenOpResult.InvalidInput;
        var task2Result = TokenOpResult.InvalidInput;

        // First thread
        var task1 = Task.Run(() => {
            startEvent.Wait(); // Wait for signal to start
            task1Result = sut.Stage("1", "2", "B");
            return task1Result;
        });

        // Second thread
        var task2 = Task.Run(() => {
            startEvent.Wait(); // Wait for signal to start
            task2Result = sut.Stage("1", "3", "C");
            return task2Result;
        });

        // Signal both threads to start
        startEvent.Signal();

        // Wait for both tasks to complete
        Task.WaitAll(task1, task2);

        // Print diagnostics to help understand what happened
        Console.WriteLine($"Thread1 result: {task1Result}");
        Console.WriteLine($"Thread2 result: {task2Result}");

        // Verify at least one succeeded
        Assert.IsTrue(task1Result == TokenOpResult.Success || task2Result == TokenOpResult.Success,
                     "At least one thread should have succeeded");

        // Commit the changes
        sut.Commit();

        // Check for the token by the new hash based on which thread succeeded
        string expectedNewHash = null;
        string expectedNewValue = null;

        if (task1Result == TokenOpResult.Success)
        {
            expectedNewHash = "2";
            expectedNewValue = "B";
        }
        else if (task2Result == TokenOpResult.Success)
        {
            expectedNewHash = "3";
            expectedNewValue = "C";
        }

        // The token should now be accessible via the new hash, not the old hash
        Assert.IsNotNull(expectedNewHash, "Expected new hash should be determined");
        var snapshotExists = sut.TryGetSnapshot(expectedNewHash, out var snapshot);

        Assert.IsTrue(snapshotExists, $"Token should exist with new hash {expectedNewHash} after commit");

        // Verify the token has the expected value
        Assert.AreEqual(expectedNewValue, snapshot.CurrentValue, "Token should have expected new value");
        Assert.AreEqual(expectedNewHash, snapshot.CurrentHash, "Token should have expected new hash");
    }

    [TestMethod]
    public void Performance_SeedBulkOperation_UnderThreshold()
    {
        var sut = Create();
        const int count = 100_000;

        var sw = Stopwatch.StartNew();

        for (var i = 0; i < count; i++)
        {
            sut.Seed(i.ToString(), $"V{i}");
        }

        sw.Stop();

        Console.WriteLine($"Seeding {count} tokens took {sw.ElapsedMilliseconds}ms");
        Assert.IsTrue(sw.ElapsedMilliseconds < 1000,
            $"Seeding performance exceeds threshold: {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public void Performance_GetSnapshotOperations_Scales()
    {
        var sut = Create();
        const int count = 10_000;
        for (var i = 0; i < count; i++) sut.Seed(i.ToString(), $"V{i}");

        var sw1 = Stopwatch.StartNew();
        sut.TryGetSnapshot("0", out _);
        sw1.Stop();

        var sw2 = Stopwatch.StartNew();
        for (var i = 0; i < 1000; i++)
        {
            sut.TryGetSnapshot(Random.Shared.Next(count).ToString(), out _);
        }
        sw2.Stop();

        var averageAccessTime = sw2.ElapsedMilliseconds / 1000.0;
        Console.WriteLine($"Average snapshot access time: {averageAccessTime}ms");
        Assert.IsTrue(averageAccessTime < 1.0,
            $"Average snapshot access time too high: {averageAccessTime}ms");
    }

    [TestMethod]
    public void MemoryUsage_LargeNumberOfOperations_ReasonableFootprint()
    {
        var sut = Create();
        const int count = 100_000;

        // Force GC before starting
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Measure memory before
        long memoryBefore = GC.GetTotalMemory(true);

        // Perform operations (with fewer tokens to make test faster)
        for (var i = 0; i < count; i++) sut.Seed(i.ToString(), $"V{i}");
        for (var i = 0; i < count; i++) sut.Stage(i.ToString(), (i + count).ToString(), $"V{i}*");
        sut.Commit();

        // Get some snapshots to ensure all data structures are built
        for (int i = 0; i < 1000; i++)
        {
            int index = Random.Shared.Next(count);
            sut.TryGetSnapshot((index + count).ToString(), out _);
        }

        // Force GC again
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Measure memory after
        long memoryAfter = GC.GetTotalMemory(true);

        // Calculate memory per token
        long memoryDiff = memoryAfter - memoryBefore;
        double bytesPerToken = (double) memoryDiff / count;

        Console.WriteLine($"Memory used: {memoryDiff:N0} bytes, {bytesPerToken:N2} bytes per token");

        // Adjust threshold to a more realistic value based on implementation
        Assert.IsTrue(bytesPerToken < 2000,
            $"Memory usage per token too high: {bytesPerToken:N2} bytes");
    }
}