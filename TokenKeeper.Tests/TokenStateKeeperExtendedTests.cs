namespace TokenKeeper.Tests;


[TestClass]
public class TokenStateKeeperExtendedTests
{
    private static TokenStateKeeper Create() => new();

    [TestMethod]
    public void Seed_InvalidHashFormat_ReturnsInvalidInput()
    {
        var sut = Create();
        var result = sut.Seed("not-a-number", "value");
        Assert.AreEqual(TokenOpResult.InvalidInput, result);
    }

    [TestMethod]
    public void Stage_InvalidHashFormat_ReturnsInvalidInput()
    {
        var sut = Create();
        var result = sut.Stage("not-a-number", "123", "value");
        Assert.AreEqual(TokenOpResult.InvalidInput, result);
    }

    [TestMethod]
    public void Stage_EmptyString_TreatedAsNull()
    {
        var sut = Create();
        sut.Seed("1", "A");
        var result = sut.Stage("1", "", "");
        Assert.AreEqual(TokenOpResult.Success, result);
        Assert.IsNull(sut.GetUncommittedDiff().Single().RightHash);
    }

    [TestMethod]
    public void TryGetSnapshot_AfterMultipleOperations_ReturnsCorrectState()
    {
        var sut = Create();

        // Initial state
        sut.Seed("1", "A");

        // First update
        sut.Stage("1", "2", "B");
        sut.Commit();

        // Second update
        sut.Stage("2", "3", "C");
        sut.Commit();

        // Check snapshot
        var success = sut.TryGetSnapshot("3", out var snapshot);

        Assert.IsTrue(success);
        Assert.AreEqual("1", snapshot.InitialHash);
        Assert.AreEqual("2", snapshot.PreviousHash);
        Assert.AreEqual("3", snapshot.CurrentHash);
        Assert.AreEqual("A", snapshot.InitialValue);
        Assert.AreEqual("B", snapshot.PreviousValue);
        Assert.AreEqual("C", snapshot.CurrentValue);
    }

    [TestMethod]
    public void TryGetSnapshot_ForDeletedToken_ReturnsFalse()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Stage("1", null, "");
        sut.Commit();

        var result = sut.TryGetSnapshot("1", out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Prune_RemovesUnusedHashesFromPool()
    {
        var sut = Create();

        // Seed and update to create unused hash
        sut.Seed("1", "A");
        sut.Stage("1", "2", "B");
        sut.Commit();

        // Update again to make hash "1" unused
        sut.Stage("2", "3", "C");
        sut.Commit();

        // Verify we can reuse hash "1"
        var result = sut.Seed("1", "New");
        Assert.AreEqual(TokenOpResult.Success, result);
    }

    [TestMethod]
    public void GetFullDiff_ComplexChain_CorrectlyTracksChanges()
    {
        var sut = Create();

        // Initialize tokens
        sut.Seed("1", "A");
        sut.Seed("2", "B");

        // First batch of changes
        sut.Stage("1", "3", "A2");
        sut.Stage("2", null, "");
        sut.Stage(null, "4", "C");
        sut.Commit();

        // Second batch of changes
        sut.Stage("3", "5", "A3");
        sut.Stage(null, "6", "D");
        sut.Stage("4", null, "");
        sut.Commit();

        // Third batch with reuse of deleted hash
        sut.Stage(null, "4", "C2");
        sut.Commit();

        // Check diff
        var fullDiff = sut.GetFullDiff().ToList();

        // Expected diffs: 1->5, 2->null, null->6, null->4
        Assert.AreEqual(4, fullDiff.Count);

        // Check token 1's transformation (now 5)
        var token1Diff = fullDiff.FirstOrDefault(d => d.LeftHash == "1" && d.RightHash == "5");
        Assert.IsNotNull(token1Diff);
        Assert.AreEqual("A", token1Diff.LeftValue);
        Assert.AreEqual("A3", token1Diff.RightValue);

        // Check token 2's deletion
        var token2Diff = fullDiff.FirstOrDefault(d => d.LeftHash == "2" && d.RightHash == null);
        Assert.IsNotNull(token2Diff);

        // Check token 6's insertion
        var token6Diff = fullDiff.FirstOrDefault(d => d.LeftHash == null && d.RightHash == "6");
        Assert.IsNotNull(token6Diff);

        // Check token 4's re-insertion
        var token4Diff = fullDiff.FirstOrDefault(d => d.LeftHash == null && d.RightHash == "4");
        Assert.IsNotNull(token4Diff);
    }

    [TestMethod]
    public void ConcurrentReads_DuringWrites_ThreadSafety()
    {
        var sut = Create();
        const int iterations = 1000;
        const int readThreads = 4;

        // Pre-populate with some data
        for (var i = 0; i < 100; i++)
        {
            sut.Seed(i.ToString(), $"Value{i}");
        }

        // Flag to control threads
        var continueRunning = true;

        // Start read threads
        var readerTasks = new List<Task>();
        for (var t = 0; t < readThreads; t++)
        {
            readerTasks.Add(Task.Run(() => {
                while (continueRunning)
                {
                    // Randomly select read operations
                    switch (new Random().Next(4))
                    {
                        case 0:
                            sut.TryGetSnapshot(new Random().Next(150).ToString(), out _);
                            break;
                        case 1:
                            sut.GetCommittedDiff().ToList();
                            break;
                        case 2:
                            sut.GetUncommittedDiff().ToList();
                            break;
                        case 3:
                            sut.GetFullDiff().ToList();
                            break;
                    }
                    Thread.Sleep(1);
                }
            }));
        }

        // Perform write operations
        for (var i = 0; i < iterations; i++)
        {
            switch (i % 4)
            {
                case 0:
                    sut.Seed((100 + i).ToString(), $"NewValue{i}");
                    break;
                case 1:
                    sut.Stage(new Random().Next(100).ToString(), (100 + i).ToString(), $"Changed{i}");
                    break;
                case 2:
                    sut.Stage(new Random().Next(100).ToString(), null, "");
                    break;
                case 3:
                    sut.Commit();
                    break;
            }
        }

        // Stop read threads
        continueRunning = false;
        Task.WaitAll(readerTasks.ToArray(), TimeSpan.FromSeconds(5));

        // Final check - no exceptions should have occurred and system should be stable
        var finalSnapshot = sut.GetFullCurrentSnapshot().ToList();
        Assert.IsTrue(finalSnapshot.Count > 0, "Should have tokens after concurrent operations");
    }

    [TestMethod]
    public void DiscardsAndCommits_AlternatingSeveral_ConsistentState()
    {
        var sut = Create();

        // Initial data
        for (var i = 0; i < 5; i++)
        {
            sut.Seed(i.ToString(), $"Value{i}");
        }

        // Operation sequence: stage, discard, stage, commit, stage, stage, discard

        // First stage
        sut.Stage("0", "10", "Changed0");
        sut.Stage("1", null, "");

        // Discard first changes
        sut.Discard();

        // Second stage
        sut.Stage("2", "12", "Changed2");
        sut.Stage("3", "13", "Changed3");

        // Commit second changes
        sut.Commit();

        // Third stage
        sut.Stage("0", "20", "Changed0Again");
        sut.Stage("12", null, "");

        // Fourth stage (some should fail due to already staged)
        var result = sut.Stage("0", "30", "ShouldFail");
        Assert.AreEqual(TokenOpResult.AlreadyStaged, result);

        // Discard third and fourth changes
        sut.Discard();

        // Check final state
        var finalSnapshot = sut.GetFullCurrentSnapshot().ToList();

        // Verify token 0 is unchanged (Value0)
        var token0 = finalSnapshot.FirstOrDefault(s => s.CurrentHash == "0");
        Assert.IsNotNull(token0);
        Assert.AreEqual("Value0", token0.CurrentValue);

        // Verify token 2 was changed to 12
        var token12 = finalSnapshot.FirstOrDefault(s => s.CurrentHash == "12");
        Assert.IsNotNull(token12);
        Assert.AreEqual("Changed2", token12.CurrentValue);

        // Verify token 3 was changed to 13
        var token13 = finalSnapshot.FirstOrDefault(s => s.CurrentHash == "13");
        Assert.IsNotNull(token13);
        Assert.AreEqual("Changed3", token13.CurrentValue);
    }
}