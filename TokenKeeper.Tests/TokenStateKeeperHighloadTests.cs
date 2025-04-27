using System.Diagnostics;

namespace TokenKeeper.Tests;

[TestClass]
public class TokenStateKeeperHighloadTests
{
    private static TokenStateKeeper Create() => new(new CoreStateKeeper());

    [TestMethod]
    public void Seed_InvalidHash_ReturnsInvalidInput()
    {
        var sut = Create();
        var result = sut.Seed(null, "value");
        Assert.AreEqual(TokenOpResult.InvalidInput, result);
    }

    [TestMethod]
    public void Stage_InvalidNewHash_ReturnsInvalidInput()
    {
        var sut = Create();
        sut.Seed("1", "A");
        var result = sut.Stage("1", "not-a-number", "B");
        Assert.AreEqual(TokenOpResult.InvalidInput, result);
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
    public void Concurrent_StageAndCommit_ExactlyOneWins()
    {
        var sut = Create();
        sut.Seed("1", "A");

        var startEvent = new CountdownEvent(1);

        var task1 = Task.Run(() => {
            startEvent.Wait(); // Wait for signal to start
            return sut.Stage("1", "2", "B");
        });

        var task2 = Task.Run(() => {
            startEvent.Wait(); // Wait for signal to start
            return sut.Stage("1", "3", "C");
        });

        startEvent.Signal();

        Task.WaitAll(task1, task2);

        var results = new[] { task1.Result, task2.Result };
        Assert.IsTrue(results.Contains(TokenOpResult.Success));
        Assert.IsTrue(results.Contains(TokenOpResult.AlreadyStaged));

        sut.Commit();
        sut.TryGetSnapshot("1", out var snapshot);

        Assert.IsTrue(snapshot.CurrentHash == "2" || snapshot.CurrentHash == "3");
        Assert.IsTrue(snapshot.CurrentValue == "B" || snapshot.CurrentValue == "C");
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
    public void MemoryUsage_LargeNumberOfOperations_MemoryStable()
    {
        var sut = Create();
        const int count = 100_000;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(true);

        for (var i = 0; i < count; i++) sut.Seed(i.ToString(), $"V{i}");
        for (var i = 0; i < count; i++) sut.Stage(i.ToString(), (i + count).ToString(), $"V{i}*");
        sut.Commit();

        for (var i = 0; i < 1000; i++)
        {
            var index = Random.Shared.Next(count);
            sut.TryGetSnapshot((index + count).ToString(), out _);
        }

        for (var i = 0; i < count; i++) sut.Stage((i + count).ToString(), null, "");
        sut.Commit();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryAfter = GC.GetTotalMemory(true);

        var memoryDiff = memoryAfter - memoryBefore;
        var bytesPerToken = (double) memoryDiff / count;

        Console.WriteLine($"Memory used: {memoryDiff:N0} bytes, {bytesPerToken:N2} bytes per token");

        Assert.IsTrue(bytesPerToken < 500,
            $"Memory usage per token too high: {bytesPerToken:N2} bytes");
    }
}