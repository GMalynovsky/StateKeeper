using System;
using System.Linq;
using System.Threading.Tasks;

namespace TokenKeeper.Tests
{
    [TestClass]
    public class TokenStateKeeperPruneTests
    {
        private static TokenStateKeeper Create() => new();

        [TestMethod]
        public void Prune_PreservesTokenHistory_AfterMultipleUpdates()
        {
            var sut = Create();

            // Initial setup
            sut.Seed("1", "A");

            // Multiple modifications
            sut.Stage("1", "2", "B");
            sut.Commit();
            sut.Stage("2", "3", "C");
            sut.Commit();
            sut.Stage("3", "4", "D");
            sut.Commit();

            // Verify snapshot correctly maintains all history
            bool exists = sut.TryGetSnapshot("4", out var snapshot);

            Assert.IsTrue(exists);
            Assert.AreEqual("1", snapshot.InitialHash);
            Assert.AreEqual("A", snapshot.InitialValue);
            Assert.AreEqual("3", snapshot.PreviousHash);
            Assert.AreEqual("C", snapshot.PreviousValue);
            Assert.AreEqual("4", snapshot.CurrentHash);
            Assert.AreEqual("D", snapshot.CurrentValue);
        }

        [TestMethod]
        public void Prune_KeepsDeletedTokenInfo_ForHistoricalReference()
        {
            var sut = Create();

            // Initial setup
            sut.Seed("1", "A");

            // Modify and delete
            sut.Stage("1", "2", "B");
            sut.Commit();
            sut.Stage("2", null, "");
            sut.Commit();

            // Get all snapshots
            var snapshots = sut.GetFullCurrentSnapshot().ToList();

            // The token should still be in the snapshots, but marked as deleted
            var deletedToken = snapshots.SingleOrDefault();
            Assert.IsNotNull(deletedToken);
            Assert.AreEqual("1", deletedToken.InitialHash);
            Assert.AreEqual("2", deletedToken.PreviousHash);
            Assert.IsNull(deletedToken.CurrentHash);
            Assert.AreEqual("A", deletedToken.InitialValue);
            Assert.AreEqual("B", deletedToken.PreviousValue);
            Assert.IsNull(deletedToken.CurrentValue);
        }

        [TestMethod]
        public void TokenLifecycle_HashReuseAfterDeletion_WorksWithStage()
        {
            var sut = Create();

            // Initial setup
            sut.Seed("1", "A");

            // Delete the token
            sut.Stage("1", null, "");
            sut.Commit();

            // Reuse the hash with Stage (proper pattern)
            var result = sut.Stage(null, "1", "New");
            Assert.AreEqual(TokenOpResult.Success, result);
            sut.Commit();

            // Verify both tokens exist in snapshots
            var snapshots = sut.GetFullCurrentSnapshot().ToList();

            // Should have deleted token and new token
            Assert.AreEqual(2, snapshots.Count);

            // Verify the deleted token
            var deletedToken = snapshots.FirstOrDefault(s => s.CurrentHash == null && s.InitialHash == "1");
            Assert.IsNotNull(deletedToken);
            Assert.AreEqual("A", deletedToken.InitialValue);

            // Verify the new token
            var newToken = snapshots.FirstOrDefault(s => s.CurrentHash == "1" && s.InitialHash == null);
            Assert.IsNotNull(newToken);
            Assert.AreEqual("New", newToken.CurrentValue);

            // Verify we can get the new token by hash
            bool exists = sut.TryGetSnapshot("1", out var snapshot);
            Assert.IsTrue(exists);
            Assert.AreEqual("New", snapshot.CurrentValue);
        }

        [TestMethod]
        public void Prune_MaintainsHistoricalDiffs_AfterComplexOperations()
        {
            var sut = Create();

            // Initial setup
            sut.Seed("1", "A");
            sut.Seed("2", "B");

            // Multiple operations with commits
            sut.Stage("1", "3", "A2");
            sut.Commit();
            sut.Stage("3", "4", "A3");
            sut.Commit();
            sut.Stage("2", null, "");
            sut.Commit();
            sut.Stage(null, "5", "C");
            sut.Commit();

            // Get full diff to see transformations
            var diffs = sut.GetFullDiff().ToList();

            // Expected diffs: 1->4 (original to latest), 2->null (deletion), null->5 (insertion)
            Assert.AreEqual(3, diffs.Count);

            // Check original to latest
            var transform = diffs.FirstOrDefault(d => d.LeftHash == "1" && d.RightHash == "4");
            Assert.IsNotNull(transform);
            Assert.AreEqual("A", transform.LeftValue);
            Assert.AreEqual("A3", transform.RightValue);

            // Check deletion
            var deletion = diffs.FirstOrDefault(d => d.LeftHash == "2" && d.RightHash == null);
            Assert.IsNotNull(deletion);
            Assert.AreEqual("B", deletion.LeftValue);
            Assert.IsNull(deletion.RightValue);

            // Check insertion
            var insertion = diffs.FirstOrDefault(d => d.LeftHash == null && d.RightHash == "5");
            Assert.IsNotNull(insertion);
            Assert.IsNull(insertion.LeftValue);
            Assert.AreEqual("C", insertion.RightValue);
        }

        [TestMethod]
        public void Prune_HandlesLargeNumberOfDeletions_WithoutMemoryLeak()
        {
            var sut = Create();
            const int count = 1000;

            // Create many tokens
            for (int i = 0; i < count; i++)
            {
                sut.Seed(i.ToString(), $"Value{i}");
            }

            // Delete half of them (evens)
            for (int i = 0; i < count; i += 2)
            {
                sut.Stage(i.ToString(), null, "");
            }
            sut.Commit();

            // Insert new tokens with the deleted hashes
            for (int i = 0; i < count; i += 2)
            {
                sut.Stage(null, i.ToString(), $"NewValue{i}");
            }
            sut.Commit();

            // Verify all tokens (old deleted + new inserted) exist
            var snapshots = sut.GetFullCurrentSnapshot().ToList();

            // Should have all original tokens (count) plus the reinserted ones (count/2)
            Assert.AreEqual(count + count / 2, snapshots.Count);

            // Check a few specific tokens

            // Token 0 - should be both deleted and reinserted
            var deletedZero = snapshots.FirstOrDefault(s => s.InitialHash == "0" && s.CurrentHash == null);
            Assert.IsNotNull(deletedZero);

            var newZero = snapshots.FirstOrDefault(s => s.CurrentHash == "0" && s.InitialHash == null);
            Assert.IsNotNull(newZero);
            Assert.AreEqual("NewValue0", newZero.CurrentValue);

            // Token 1 - should be original only
            var originalOne = snapshots.FirstOrDefault(s => s.InitialHash == "1" && s.CurrentHash == "1");
            Assert.IsNotNull(originalOne);
            Assert.AreEqual("Value1", originalOne.CurrentValue);

            // No memory leaks can be directly tested, but this confirms prune handles bulk operations
        }

        [TestMethod]
        public void MultiUpdate_DeleteAndReinsert_MaintainsCorrectSnapshots()
        {
            var sut = Create();

            // Initial setup
            sut.Seed("1", "A");

            // Multiple updates
            sut.Stage("1", "2", "B");
            sut.Commit();
            sut.Stage("2", "3", "C");
            sut.Commit();

            // Delete token
            sut.Stage("3", null, "");
            sut.Commit();

            // Check snapshot with deleted token
            var snapshotsAfterDelete = sut.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(1, snapshotsAfterDelete.Count);

            var deletedToken = snapshotsAfterDelete.Single();
            Assert.AreEqual("1", deletedToken.InitialHash);
            Assert.AreEqual("3", deletedToken.PreviousHash);
            Assert.IsNull(deletedToken.CurrentHash);

            // Reinsert with same hash as original
            sut.Stage(null, "1", "D");
            sut.Commit();

            // There should now be 2 tokens in the snapshot:
            // 1. The deleted token with history 1->2->3->null
            // 2. The new token with hash 1
            var snapshotsAfterReinsert = sut.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(2, snapshotsAfterReinsert.Count);

            // Check that the correct token is returned by TryGetSnapshot
            bool exists = sut.TryGetSnapshot("1", out var snapshot);
            Assert.IsTrue(exists);
            Assert.AreEqual("D", snapshot.CurrentValue);
            Assert.IsNull(snapshot.InitialHash); // It's a new token, not the original
        }

        [TestMethod]
        public void StageInsertAfterCommit_CorrectBehavior()
        {
            var sut = Create();

            // Initial operation and commit
            sut.Seed("1", "A");
            sut.Commit();

            // Insert new tokens via Stage, not Seed (proper pattern)
            sut.Stage(null, "2", "B");
            sut.Stage(null, "3", "C");
            sut.Commit();

            // Verify all tokens exist
            var snapshots = sut.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(3, snapshots.Count);

            // Check initial token
            var originalToken = snapshots.FirstOrDefault(s => s.CurrentHash == "1");
            Assert.IsNotNull(originalToken);
            Assert.AreEqual("1", originalToken.InitialHash);
            Assert.AreEqual("A", originalToken.CurrentValue);

            // Check inserted tokens
            var token2 = snapshots.FirstOrDefault(s => s.CurrentHash == "2");
            Assert.IsNotNull(token2);
            Assert.IsNull(token2.InitialHash); // Inserted, not seeded
            Assert.AreEqual("B", token2.CurrentValue);

            var token3 = snapshots.FirstOrDefault(s => s.CurrentHash == "3");
            Assert.IsNotNull(token3);
            Assert.IsNull(token3.InitialHash); // Inserted, not seeded
            Assert.AreEqual("C", token3.CurrentValue);
        }

        [TestMethod]
        public void GetCommittedDiff_AfterPrune_ShowsOnlyLatestChanges()
        {
            var sut = Create();

            // Initial setup
            sut.Seed("1", "A");
            sut.Seed("2", "B");

            // First batch of changes
            sut.Stage("1", "3", "A2");
            sut.Stage("2", "4", "B2");
            sut.Commit();

            // Clear committed diff by getting it
            var firstBatchDiff = sut.GetCommittedDiff().ToList();
            Assert.AreEqual(2, firstBatchDiff.Count);

            // Second batch of changes
            sut.Stage("3", "5", "A3");
            sut.Stage("4", "6", "B3");
            sut.Commit();

            // Get committed diff - should only contain second batch
            var secondBatchDiff = sut.GetCommittedDiff().ToList();
            Assert.AreEqual(2, secondBatchDiff.Count);

            // Verify diff only shows 3->5 and 4->6, not 1->3 or 2->4
            var diff3to5 = secondBatchDiff.FirstOrDefault(d => d.LeftHash == "3" && d.RightHash == "5");
            Assert.IsNotNull(diff3to5);

            var diff4to6 = secondBatchDiff.FirstOrDefault(d => d.LeftHash == "4" && d.RightHash == "6");
            Assert.IsNotNull(diff4to6);
        }

        [TestMethod]
        public void ComplexOperations_VerifyDiffAndSnapshots_Consistency()
        {
            var sut = Create();

            // Initial setup
            sut.Seed("1", "A");
            sut.Seed("2", "B");
            sut.Seed("3", "C");

            // Batch 1: Update token 1, delete token 2, insert token 4
            sut.Stage("1", "10", "A2");
            sut.Stage("2", null, "");
            sut.Stage(null, "4", "D");
            sut.Commit();

            // Batch 2: Update token 3, reinsert with hash 2
            sut.Stage("3", "30", "C2");
            sut.Stage(null, "2", "B2");
            sut.Commit();

            // Get diffs and snapshots
            var committedDiff = sut.GetCommittedDiff().ToList();
            var fullDiff = sut.GetFullDiff().ToList();
            var snapshots = sut.GetFullCurrentSnapshot().ToList();

            // Verify committed diff (just the latest batch)
            Assert.AreEqual(2, committedDiff.Count);
            Assert.IsTrue(committedDiff.Any(d => d.LeftHash == "3" && d.RightHash == "30"));
            Assert.IsTrue(committedDiff.Any(d => d.LeftHash == null && d.RightHash == "2"));

            // Verify full diff includes all changes
            Assert.AreEqual(5, fullDiff.Count);
            Assert.IsTrue(fullDiff.Any(d => d.LeftHash == "1" && d.RightHash == "10"));
            Assert.IsTrue(fullDiff.Any(d => d.LeftHash == "2" && d.RightHash == null));
            Assert.IsTrue(fullDiff.Any(d => d.LeftHash == "3" && d.RightHash == "30"));
            Assert.IsTrue(fullDiff.Any(d => d.LeftHash == null && d.RightHash == "4"));
            Assert.IsTrue(fullDiff.Any(d => d.LeftHash == null && d.RightHash == "2"));

            // Verify snapshots
            Assert.AreEqual(5, snapshots.Count);

            // Original token 1 (updated)
            var token1 = snapshots.FirstOrDefault(s => s.InitialHash == "1");
            Assert.IsNotNull(token1);
            Assert.AreEqual("10", token1.CurrentHash);
            Assert.AreEqual("A2", token1.CurrentValue);

            // Original token 2 (deleted)
            var token2Deleted = snapshots.FirstOrDefault(s => s.InitialHash == "2" && s.CurrentHash == null);
            Assert.IsNotNull(token2Deleted);

            // Original token 3 (updated)
            var token3 = snapshots.FirstOrDefault(s => s.InitialHash == "3");
            Assert.IsNotNull(token3);
            Assert.AreEqual("30", token3.CurrentHash);
            Assert.AreEqual("C2", token3.CurrentValue);

            // New token 4 (inserted)
            var token4 = snapshots.FirstOrDefault(s => s.CurrentHash == "4");
            Assert.IsNotNull(token4);
            Assert.IsNull(token4.InitialHash);
            Assert.AreEqual("D", token4.CurrentValue);

            // New token 2 (reinserted)
            var token2New = snapshots.FirstOrDefault(s => s.CurrentHash == "2" && s.InitialHash == null);
            Assert.IsNotNull(token2New);
            Assert.AreEqual("B2", token2New.CurrentValue);
        }

        [TestMethod]
        public void Concurrent_StageAndCommit_WithPrune_ThreadSafety()
        {
            var sut = Create();
            const int threadCount = 8;
            const int operationsPerThread = 500;

            // Pre-populate with some tokens
            for (int i = 0; i < 50; i++)
            {
                sut.Seed(i.ToString(), $"Value{i}");
            }

            // Multiple threads performing operations
            Parallel.For(0, threadCount, threadId =>
            {
                var random = new Random(threadId); // Different seed per thread for deterministic randomness

                for (int i = 0; i < operationsPerThread; i++)
                {
                    // Mix of different operations
                    int op = random.Next(6);
                    int tokenId = random.Next(100);

                    switch (op)
                    {
                        case 0: // Insert
                            sut.Stage(null, (50 + threadId * operationsPerThread + i).ToString(), $"Thread{threadId}Value{i}");
                            break;

                        case 1: // Update existing token
                            sut.Stage(tokenId.ToString(), (1000 + threadId * operationsPerThread + i).ToString(), $"Updated{threadId}_{i}");
                            break;

                        case 2: // Delete
                            sut.Stage(tokenId.ToString(), null, "");
                            break;

                        case 3: // Commit
                            sut.Commit();
                            break;

                        case 4: // Get diff
                            sut.GetFullDiff().ToList();
                            break;

                        case 5: // Get snapshot
                            sut.TryGetSnapshot(tokenId.ToString(), out _);
                            break;
                    }
                }
            });

            // Final commit to ensure all operations are applied
            sut.Commit();

            // Verify system integrity by getting snapshots and diffs
            var finalSnapshots = sut.GetFullCurrentSnapshot().ToList();
            var finalDiff = sut.GetFullDiff().ToList();

            // We can't assert exact counts due to randomness, but system should be stable
            Assert.IsTrue(finalSnapshots.Count > 0, "Should have tokens after concurrent operations");

            // Check a few random snapshots for valid data
            foreach (var snapshot in finalSnapshots.Take(10))
            {
                if (snapshot.CurrentHash != null)
                {
                    // Try getting this token directly
                    bool exists = sut.TryGetSnapshot(snapshot.CurrentHash, out var directSnapshot);
                    Assert.IsTrue(exists);
                    Assert.AreEqual(snapshot.CurrentValue, directSnapshot.CurrentValue);
                }
            }
        }
    }
}