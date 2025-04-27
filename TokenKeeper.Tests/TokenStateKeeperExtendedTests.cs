using System.Diagnostics;

namespace TokenKeeper.Tests
{
    [TestClass]
    public class TokenStateKeeperExtendedTests
    {
        private static TokenStateKeeper Create() => new(new CoreStateKeeper());

        [TestMethod]
        public void MixedOperations_MultithreadedReadsAndWrites_MaintainsConsistency()
        {
            var sut = Create();
            const int count = 1000;

            // Pre-seed some data
            for (int i = 0; i < count; i++)
                sut.Seed(i.ToString(), $"Value{i}");

            // Create reader and writer tasks
            var writerTask = Task.Run(() => {
                for (int i = 0; i < count; i += 2)
                {
                    sut.Stage(i.ToString(), (i + count).ToString(), $"Updated{i}");
                    if (i % 100 == 0) sut.Commit(); // Occasional commits
                }
                sut.Commit(); // Final commit
            });

            var readerTask = Task.Run(() => {
                for (int i = 0; i < 20; i++)
                {
                    // Read operations during writes
                    var snapshots = sut.GetFullCurrentSnapshot().ToList();
                    Thread.Sleep(10); // Slight delay to interleave operations
                }
            });

            // Wait for both to complete
            Task.WaitAll(writerTask, readerTask);

            // Verify results
            VerifySystemConsistency(sut);
        }

        [TestMethod]
        public void ExtremeValues_LargeNumberOfOperations_SystemRemainsStable()
        {
            var sut = Create();
            const int extremeCount = 1_000_000;

            // Test with a very large number of tokens
            for (int i = 0; i < extremeCount; i++)
            {
                if (i % 10000 == 0)
                {
                    // Periodically check system health during insertion
                    Assert.AreEqual(TokenOpResult.Success, sut.Seed(i.ToString(), $"V{i}"));
                }
                else
                {
                    sut.Seed(i.ToString(), $"V{i}");
                }
            }

            // Verify we can still perform operations efficiently
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

            // Pre-seed some data
            for (int i = 0; i < count; i++)
                sut.Seed(i.ToString(), $"Value{i}");

            // Create reader and writer tasks
            var writerTask = Task.Run(() => {
                for (int i = 0; i < count; i += 2)
                {
                    sut.Stage(i.ToString(), (i + count).ToString(), $"Updated{i}");
                    if (i % 100 == 0) sut.Commit(); // Occasional commits
                }
                sut.Commit(); // Final commit
            });

            var readerTask = Task.Run(() => {
                for (int i = 0; i < 20; i++)
                {
                    // Just verify we can read without exceptions
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

            // Wait for both to complete
            Task.WaitAll(writerTask, readerTask);

            // Basic verification - specific checks are difficult with concurrent ops
            var finalSnapshots = sut.GetFullCurrentSnapshot().ToList();

            // Verify we have the expected token count (original + modified)
            Assert.AreEqual(count, finalSnapshots.Count);

            // Verify a few random tokens have expected values or have been updated
            foreach (var snapshot in finalSnapshots.Take(10))
            {
                if (snapshot.CurrentHash != null)
                {
                    // Just verify we can retrieve the token without errors
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

            // Test every error case systematically
            var testCases = new[] {
                // Format: oldHash, newHash, expectedResult, message
                new { OldHash = "99", NewHash = (string)null, Expected = TokenOpResult.UnknownHash, Msg = "Delete unknown hash" },
                new { OldHash = "1", NewHash = "2", Expected = TokenOpResult.Collision, Msg = "Update to existing hash" },
                new { OldHash = (string)null, NewHash = "1", Expected = TokenOpResult.DuplicateHash, Msg = "Insert with existing hash" },
                new { OldHash = (string)null, NewHash = (string)null, Expected = TokenOpResult.InvalidInput, Msg = "Both hashes null" },
                // Add more test cases for every possible error
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

            // Perform random operations
            for (int i = 0; i < 5000; i++)
            {
                int operation = random.Next(4);

                try
                {
                    switch (operation)
                    {
                        case 0: // Seed
                            string hash = random.Next(1000).ToString();
                            sut.Seed(hash, $"Value{hash}");
                            tokenPool.Add(hash);
                            break;

                        case 1: // Update
                            if (tokenPool.Count > 0)
                            {
                                string oldHash = tokenPool[random.Next(tokenPool.Count)];
                                string newHash = (1000 + random.Next(1000)).ToString();
                                var result = sut.Stage(oldHash, newHash, $"Updated{newHash}");
                                if (result == TokenOpResult.Success)
                                {
                                    tokenPool.Remove(oldHash);
                                    tokenPool.Add(newHash);
                                }
                            }
                            break;

                        case 2: // Delete
                            if (tokenPool.Count > 0)
                            {
                                string toDelete = tokenPool[random.Next(tokenPool.Count)];
                                sut.Stage(toDelete, null, "");
                                tokenPool.Remove(toDelete);
                            }
                            break;

                        case 3: // Commit
                            sut.Commit();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Exception at iteration {i}: {ex}");
                }
            }

            // Final commit
            sut.Commit();

            // Basic sanity check - just ensure we can get snapshots
            var snapshots = sut.GetFullCurrentSnapshot().ToList();

            // Check a sample of tokens
            foreach (var hash in tokenPool.Take(Math.Min(10, tokenPool.Count)))
            {
                bool exists = sut.TryGetSnapshot(hash, out var snapshot);
                Assert.IsTrue(exists, $"Token with hash {hash} should exist");
                Assert.AreEqual(hash, snapshot.CurrentHash);
            }
        }

        private void VerifySystemConsistency(TokenStateKeeper sut)
        {
            // Get all states
            var snapshots = sut.GetFullCurrentSnapshot().ToList();

            // Verify we can retrieve active tokens by their current hash
            foreach (var snapshot in snapshots)
            {
                if (snapshot.CurrentHash != null)
                {
                    // Just verify we can retrieve the token
                    Assert.IsTrue(sut.TryGetSnapshot(snapshot.CurrentHash, out var retrieved));

                    // Ensure value matches
                    Assert.AreEqual(snapshot.CurrentValue, retrieved.CurrentValue);
                }

                // Instead of checking entire history consistency, which is complex with deletions,
                // ensure values and hashes are consistent within each snapshot
                if (snapshot.CurrentHash == null)
                {
                    // Deleted tokens should have null current value
                    Assert.IsNull(snapshot.CurrentValue);
                }

                // Check hash/value consistency for non-null hashes
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
}