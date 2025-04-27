using System.Diagnostics;

namespace TokenKeeper.Tests;

[TestClass]
public class TokenStateKeeperExtendedTests
{
    private static TokenStateKeeper Create() => new();

    [TestMethod]
    public void MixedOperations_MultithreadedReadsAndWrites_MaintainsConsistency()
    {
        var sut = Create();
        const int count = 1000;

        for (var i = 0; i < count; i++)
            sut.Seed(i.ToString(), $"Value{i}");

        var writerTask = Task.Run(() =>
        {
            for (var i = 0; i < count; i += 2)
            {
                sut.Stage(i.ToString(), (i + count).ToString(), $"Updated{i}");
                if (i % 100 == 0) sut.Commit(); // Occasional commits
            }
            sut.Commit();
        });

        var readerTask = Task.Run(() =>
        {
            for (var i = 0; i < 20; i++)
            {
                var snapshots = sut.GetFullCurrentSnapshot().ToList();
                Thread.Sleep(10); // Slight delay to interleave operations
            }
        });

        Task.WaitAll(writerTask, readerTask);

        VerifySystemConsistency(sut);
    }

    [TestMethod]
    public void ExtremeValues_LargeNumberOfOperations_SystemRemainsStable()
    {
        var sut = Create();
        const int extremeCount = 1_000_000;

        for (var i = 0; i < extremeCount; i++)
        {
            if (i % 10000 == 0)
            {
                Assert.AreEqual(TokenOpResult.Success, sut.Seed(i.ToString(), $"V{i}"));
            }
            else
            {
                sut.Seed(i.ToString(), $"V{i}");
            }
        }

        var sw = Stopwatch.StartNew();
        var sample = sut.GetFullCurrentSnapshot().Take(100).ToList();
        sw.Stop();

        Assert.IsTrue(sw.ElapsedMilliseconds < 1000,
            "Retrieving sample should be fast even with many tokens");
        Assert.AreEqual(100, sample.Count);
    }

    [TestMethod]
    public void MixedOperations_MultithreadedReadsAndWrites_BasicConsistencyCheck()
    {
        var sut = Create();
        const int count = 1000;

        for (var i = 0; i < count; i++)
            sut.Seed(i.ToString(), $"Value{i}");

        var writerTask = Task.Run(() =>
        {
            for (var i = 0; i < count; i += 2)
            {
                sut.Stage(i.ToString(), (i + count).ToString(), $"Updated{i}");
                if (i % 100 == 0) sut.Commit();
            }
            sut.Commit();
        });

        var readerTask = Task.Run(() =>
        {
            for (var i = 0; i < 20; i++)
            {
                try
                {
                    var snapshots = sut.GetFullCurrentSnapshot().ToList();
                    var diffs = sut.GetFullDiff().ToList();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Exception during concurrent read: {ex}");
                }
                Thread.Sleep(10);
            }
        });

        Task.WaitAll(writerTask, readerTask);

        var finalSnapshots = sut.GetFullCurrentSnapshot().ToList();

        Assert.AreEqual(count, finalSnapshots.Count);

        foreach (var snapshot in finalSnapshots.Take(10))
        {
            if (snapshot.CurrentHash != null)
            {
                Assert.IsTrue(sut.TryGetSnapshot(snapshot.CurrentHash, out _));
            }
        }
    }

    [TestMethod]
    public void AllStageOperationErrorCases_ReturnsCorrectResults()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Seed("2", "B");

        var testCases = new[] {
            // Format: oldHash, newHash, expectedResult, message
            new { OldHash = (string?)null, NewHash = (string?)null, Expected = TokenOpResult.InvalidInput, Msg = "Both hashes null" },
            new { OldHash = (string?)null, NewHash = (string ?)"1", Expected = TokenOpResult.DuplicateHash, Msg = "Insert with existing hash" },
            new { OldHash = (string?)"1", NewHash = (string ?)"2", Expected = TokenOpResult.Collision, Msg = "Update to existing hash" },
            new { OldHash = (string?)"99", NewHash = (string?)null, Expected = TokenOpResult.UnknownHash, Msg = "Delete unknown hash" },
        };

        foreach (var test in testCases)
        {
            var result = sut.Stage(test.OldHash, test.NewHash, "value");
            Assert.AreEqual(test.Expected, result, test.Msg);
        }
    }

    [TestMethod]
    public void FuzzTest_RandomOperations_BasicConsistencyCheck()
    {
        var sut = Create();
        var random = new Random(42);
        var tokenPool = new List<string>();

        for (var i = 0; i < 5000; i++)
        {
            var operation = random.Next(4);

            try
            {
                switch (operation)
                {
                    case 0:
                        var hash = random.Next(1000).ToString();
                        sut.Seed(hash, $"Value{hash}");
                        tokenPool.Add(hash);
                        break;

                    case 1:
                        if (tokenPool.Count > 0)
                        {
                            var oldHash = tokenPool[random.Next(tokenPool.Count)];
                            var newHash = (1000 + random.Next(1000)).ToString();
                            var result = sut.Stage(oldHash, newHash, $"Updated{newHash}");
                            if (result == TokenOpResult.Success)
                            {
                                tokenPool.Remove(oldHash);
                                tokenPool.Add(newHash);
                            }
                        }
                        break;

                    case 2:
                        if (tokenPool.Count > 0)
                        {
                            var toDelete = tokenPool[random.Next(tokenPool.Count)];
                            sut.Stage(toDelete, null, "");
                            tokenPool.Remove(toDelete);
                        }
                        break;

                    case 3:
                        sut.Commit();
                        break;
                }
            }
            catch (Exception ex)
            {
                Assert.Fail($"Exception at iteration {i}: {ex}");
            }
        }

        sut.Commit();

        foreach (var hash in tokenPool.Take(Math.Min(10, tokenPool.Count)))
        {
            var exists = sut.TryGetSnapshot(hash, out var snapshot);
            Assert.IsTrue(exists, $"Token with hash {hash} should exist");
            Assert.AreEqual(hash, snapshot.CurrentHash);
        }
    }

    private static void VerifySystemConsistency(TokenStateKeeper sut)
    {
        var snapshots = sut.GetFullCurrentSnapshot().ToList();

        foreach (var snapshot in snapshots)
        {
            if (snapshot.CurrentHash != null)
            {
                Assert.IsTrue(sut.TryGetSnapshot(snapshot.CurrentHash, out var retrieved));

                Assert.AreEqual(snapshot.CurrentValue, retrieved.CurrentValue);
            }

            if (snapshot.CurrentHash == null)
            {
                Assert.IsNull(snapshot.CurrentValue);
            }

            if (snapshot.InitialHash != null)
            {
                Assert.IsNotNull(snapshot.InitialValue);
            }

            if (snapshot.PreviousHash != null)
            {
                Assert.IsNotNull(snapshot.PreviousValue);
            }
        }
    }
}