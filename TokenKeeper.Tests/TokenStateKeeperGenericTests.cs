using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TokenKeeper.Tests
{
    [TestClass]
    public class TokenStateKeeperGenericTests
    {
        #region Value Type Tests

        [TestMethod]
        public void PrimitiveValueType_IntOperations_MaintainsCorrectState()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<int>();

            // Act - Seed with integers
            for (int i = 1; i <= 5; i++)
            {
                keeper.Seed(i.ToString(), i * 10);
            }

            // Act - Modify values
            keeper.Stage("1", "101", 15);
            keeper.Stage("2", "102", 25);
            keeper.Stage("3", null, 0); // Delete
            keeper.Commit();

            // Assert
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(5, snapshots.Count, "All tokens should be in the snapshot, including deleted ones");

            // Verify active tokens
            Assert.IsTrue(keeper.TryGetSnapshot("101", out var snapshot1));
            Assert.AreEqual(15, snapshot1.CurrentValue);
            Assert.AreEqual(10, snapshot1.InitialValue);

            // Verify deleted token
            var deletedSnapshot = snapshots.FirstOrDefault(s => s.InitialHash == "3" && s.CurrentHash == null);
            Assert.IsNotNull(deletedSnapshot, "Deleted token should be in snapshot");
            Assert.IsNull(deletedSnapshot.CurrentValue, "Deleted token should have null CurrentValue");
        }

        [TestMethod]
        public void StructValueType_PointOperations_MaintainsCorrectState()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<Point>();

            // Act - Seed points
            keeper.Seed("1", new Point(10, 20));
            keeper.Seed("2", new Point(30, 40));
            keeper.Seed("3", new Point(50, 60));

            // Act - Modify points
            keeper.Stage("1", "101", new Point(15, 25));
            keeper.Stage("2", null, default); // Delete
            keeper.Commit();

            // Assert
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(3, snapshots.Count, "All tokens should be in snapshot");

            // Verify updated point
            Assert.IsTrue(keeper.TryGetSnapshot("101", out var snapshot1));
            Assert.AreEqual(15, snapshot1.CurrentValue.X);
            Assert.AreEqual(25, snapshot1.CurrentValue.Y);
            Assert.AreEqual(10, snapshot1.InitialValue.X);
            Assert.AreEqual(20, snapshot1.InitialValue.Y);

            // Verify deleted point
            var deletedSnapshot = snapshots.FirstOrDefault(s => s.InitialHash == "2" && s.CurrentHash == null);
            Assert.IsNotNull(deletedSnapshot, "Deleted token should be in snapshot");
        }

        [TestMethod]
        public void NullableValueType_Operations_HandledCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<int?>();

            // Act - Seed with nullable values
            keeper.Seed("1", 10);
            keeper.Seed("2", null);
            keeper.Seed("3", 30);

            // Act - Modify values
            keeper.Stage("1", "101", null); // Change to null
            keeper.Stage("2", "102", 20);   // Change from null
            keeper.Commit();

            // Assert
            Assert.IsTrue(keeper.TryGetSnapshot("101", out var snapshot1));
            Assert.IsNull(snapshot1.CurrentValue);
            Assert.AreEqual(10, snapshot1.InitialValue);

            Assert.IsTrue(keeper.TryGetSnapshot("102", out var snapshot2));
            Assert.AreEqual(20, snapshot2.CurrentValue);
            Assert.IsNull(snapshot2.InitialValue);
        }

        #endregion

        #region Reference Type Tests

        [TestMethod]
        public void StringType_TextOperations_HandlesCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();

            // Act - Seed with strings
            keeper.Seed("1", "Hello");
            keeper.Seed("2", string.Empty);
            keeper.Seed("3", null);
            keeper.Seed("4", "World");

            // Act - Modify values
            keeper.Stage("1", "101", "Hello Modified");
            keeper.Stage("2", "102", "No Longer Empty");
            keeper.Stage("3", "103", "No Longer Null");
            keeper.Stage("4", null, null); // Delete
            keeper.Commit();

            // Assert
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(4, snapshots.Count);

            // Verify modifications
            Assert.IsTrue(keeper.TryGetSnapshot("101", out var snapshot1));
            Assert.AreEqual("Hello Modified", snapshot1.CurrentValue);
            Assert.AreEqual("Hello", snapshot1.InitialValue);

            Assert.IsTrue(keeper.TryGetSnapshot("102", out var snapshot2));
            Assert.AreEqual("No Longer Empty", snapshot2.CurrentValue);
            Assert.AreEqual(string.Empty, snapshot2.InitialValue);

            Assert.IsTrue(keeper.TryGetSnapshot("103", out var snapshot3));
            Assert.AreEqual("No Longer Null", snapshot3.CurrentValue);
            Assert.IsNull(snapshot3.InitialValue);

            // Verify deleted token
            var deletedToken = snapshots.FirstOrDefault(s => s.InitialHash == "4" && s.CurrentHash == null);
            Assert.IsNotNull(deletedToken);
        }

        [TestMethod]
        public void ComplexReferenceType_DocumentItemOperations_PreservesAllData()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<DocumentItem>();

            // Create document items
            var now = DateTime.Now;
            var doc1 = new DocumentItem
            {
                Id = "doc1",
                Title = "Document 1",
                Content = "Content 1",
                LastModified = now.AddDays(-1),
                Version = 1
            };

            var doc2 = new DocumentItem
            {
                Id = "doc2",
                Title = "Document 2",
                Content = "Content 2",
                LastModified = now.AddDays(-2),
                Version = 1
            };

            // Act - Seed documents
            keeper.Seed("1", doc1);
            keeper.Seed("2", doc2);

            // Act - Modify documents
            var modifiedDoc1 = new DocumentItem
            {
                Id = "doc1",
                Title = "Updated Document 1",
                Content = "Updated Content 1",
                LastModified = now,
                Version = 2
            };

            keeper.Stage("1", "101", modifiedDoc1);
            keeper.Commit();

            // Assert
            Assert.IsTrue(keeper.TryGetSnapshot("101", out var snapshot));

            // Verify all fields were updated correctly
            Assert.AreEqual("Updated Document 1", snapshot.CurrentValue.Title);
            Assert.AreEqual("Updated Content 1", snapshot.CurrentValue.Content);
            Assert.AreEqual(2, snapshot.CurrentValue.Version);

            // Verify initial values preserved
            Assert.AreEqual("Document 1", snapshot.InitialValue.Title);
            Assert.AreEqual("Content 1", snapshot.InitialValue.Content);
            Assert.AreEqual(1, snapshot.InitialValue.Version);
        }

        [TestMethod]
        public void CollectionType_ListOperations_MaintainsCorrectData()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<List<string>>();

            // Act - Seed with lists
            keeper.Seed("1", new List<string> { "apple", "banana", "cherry" });
            keeper.Seed("2", new List<string> { "dog", "cat", "bird" });
            keeper.Seed("3", new List<string>());

            // Act - Modify lists
            keeper.Stage("1", "101", new List<string> { "apple", "pear", "orange" });
            keeper.Stage("2", null, null); // Delete
            keeper.Stage("3", "103", new List<string> { "item1", "item2" });
            keeper.Commit();

            // Assert
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(3, snapshots.Count);

            // Verify first list was updated correctly
            Assert.IsTrue(keeper.TryGetSnapshot("101", out var snapshot1));
            Assert.AreEqual(3, snapshot1.CurrentValue.Count);
            Assert.AreEqual("apple", snapshot1.CurrentValue[0]);
            Assert.AreEqual("pear", snapshot1.CurrentValue[1]);
            Assert.AreEqual("orange", snapshot1.CurrentValue[2]);

            // Verify initial values
            Assert.AreEqual("banana", snapshot1.InitialValue[1]);
            Assert.AreEqual("cherry", snapshot1.InitialValue[2]);

            // Verify empty list was updated
            Assert.IsTrue(keeper.TryGetSnapshot("103", out var snapshot3));
            Assert.AreEqual(2, snapshot3.CurrentValue.Count);
            Assert.AreEqual(0, snapshot3.InitialValue.Count);
        }

        [TestMethod]
        public void NestedObjects_ComplexStructure_PreservesAllData()
        {
            // Define test classes
            var department = new
            {
                Name = "Engineering",
                Manager = "Alice",
                Employees = new[]
                {
                    new { Name = "Bob", Age = 30, Address = new { City = "Seattle", State = "WA" } },
                    new { Name = "Charlie", Age = 25, Address = new { City = "Portland", State = "OR" } }
                }
            };

            var keeper = TokenStateKeeperProvider.Create<object>();

            // Act - Seed department
            keeper.Seed("dept1", department);

            // Modified department with structural changes
            var updatedDepartment = new
            {
                Name = "Engineering",
                Manager = "David", // Changed manager
                Employees = new[] // Changed employee list
                {
                    new { Name = "Bob", Age = 31, Address = new { City = "Redmond", State = "WA" } }, // Changed address and age
                    new { Name = "Eve", Age = 28, Address = new { City = "Bellevue", State = "WA" } }  // New employee
                }
            };

            // Act - Update department
            keeper.Stage("dept1", "dept2", updatedDepartment);
            keeper.Commit();

            // Assert
            Assert.IsTrue(keeper.TryGetSnapshot("dept2", out var snapshot));
            var currentDept = snapshot.CurrentValue;
            var initialDept = snapshot.InitialValue;

            // This test relies on dynamic access which can't be statically checked
            // but should still be valid at runtime for anonymous types
            dynamic current = currentDept;
            dynamic initial = initialDept;

            // Verify updated data
            Assert.AreEqual("David", current.Manager);
            Assert.AreEqual(2, current.Employees.Length);
            Assert.AreEqual("Bob", current.Employees[0].Name);
            Assert.AreEqual(31, current.Employees[0].Age);
            Assert.AreEqual("Redmond", current.Employees[0].Address.City);
            Assert.AreEqual("Eve", current.Employees[1].Name);

            // Verify initial data preserved
            Assert.AreEqual("Alice", initial.Manager);
            Assert.AreEqual(2, initial.Employees.Length);
            Assert.AreEqual("Bob", initial.Employees[0].Name);
            Assert.AreEqual(30, initial.Employees[0].Age);
            Assert.AreEqual("Seattle", initial.Employees[0].Address.City);
            Assert.AreEqual("Charlie", initial.Employees[1].Name);
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void NullValues_InReferenceTypes_HandledProperly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<DocumentItem>();

            // Seed with non-null value
            var doc1 = new DocumentItem
            {
                Id = "doc1",
                Title = "Document 1",
                Content = "Content 1"
            };
            keeper.Seed("1", doc1);

            // Update with null values in some fields
            var partialDoc = new DocumentItem
            {
                Id = "doc1",
                Title = null, // Nulled out
                Content = "Updated Content"
            };
            keeper.Stage("1", "2", partialDoc);
            keeper.Commit();

            // Assert
            keeper.TryGetSnapshot("2", out var snapshot);
            Assert.IsNotNull(snapshot.CurrentValue);
            Assert.IsNull(snapshot.CurrentValue.Title);
            Assert.AreEqual("Updated Content", snapshot.CurrentValue.Content);
            Assert.AreEqual("Document 1", snapshot.InitialValue.Title);
        }

        [TestMethod]
        public void EmptyCollections_HandledCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<List<int>>();

            // Act - Seed with empty and non-empty collections
            keeper.Seed("1", new List<int>());
            keeper.Seed("2", new List<int> { 1, 2, 3 });

            // Act - Update: empty to non-empty and vice versa
            keeper.Stage("1", "101", new List<int> { 10, 20 });
            keeper.Stage("2", "102", new List<int>());
            keeper.Commit();

            // Assert
            keeper.TryGetSnapshot("101", out var snapshot1);
            Assert.AreEqual(2, snapshot1.CurrentValue.Count);
            Assert.AreEqual(0, snapshot1.InitialValue.Count);

            keeper.TryGetSnapshot("102", out var snapshot2);
            Assert.AreEqual(0, snapshot2.CurrentValue.Count);
            Assert.AreEqual(3, snapshot2.InitialValue.Count);
        }

        [TestMethod]
        public void DefaultValues_HandledCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<Point>();

            // Act - Seed with default struct
            keeper.Seed("1", default(Point));

            // Act - Update to non-default
            keeper.Stage("1", "101", new Point(10, 20));
            keeper.Commit();

            // Assert
            keeper.TryGetSnapshot("101", out var snapshot);
            Assert.AreEqual(10, snapshot.CurrentValue.X);
            Assert.AreEqual(20, snapshot.CurrentValue.Y);
            Assert.AreEqual(0, snapshot.InitialValue.X);
            Assert.AreEqual(0, snapshot.InitialValue.Y);
        }

        #endregion

        #region Advanced Scenarios

        [TestMethod]
        public void ConcurrentOperations_WithGenericType_ThreadSafe()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<Point>();
            const int iterations = 1000;

            // Seeds in parallel
            Parallel.For(0, iterations, i =>
            {
                keeper.Seed(i.ToString(), new Point(i, i * 2));
            });

            // Modifications in parallel (updating every even-indexed item)
            Parallel.For(0, iterations, i =>
            {
                if (i % 2 == 0)
                    keeper.Stage(i.ToString(), (i + iterations).ToString(), new Point(i * 2, i * 3));
            });

            keeper.Commit();

            // Assert
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(iterations, snapshots.Count);

            // Count modified tokens (ones where current hash != initial hash)
            int modified = snapshots.Count(s => !string.Equals(s.CurrentHash, s.InitialHash));
            Assert.AreEqual(iterations / 2, modified);

            // Verify a sample of the modified tokens
            for (int i = 0; i < 10; i += 2)
            {
                var newHash = (i + iterations).ToString();
                Assert.IsTrue(keeper.TryGetSnapshot(newHash, out var snapshot));
                Assert.AreEqual(i * 2, snapshot.CurrentValue.X);
                Assert.AreEqual(i * 3, snapshot.CurrentValue.Y);
            }
        }

        [TestMethod]
        public void LargeDatasets_Performance_ReasonableTime()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();
            const int count = 10000;

            // Act - Seed many tokens
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                keeper.Seed(i.ToString(), $"Value{i}");
            }
            var seedTime = sw.ElapsedMilliseconds;

            // Act - Stage many updates
            sw.Restart();
            for (int i = 0; i < count; i++)
            {
                keeper.Stage(i.ToString(), (i + count).ToString(), $"Updated{i}");
            }
            var stageTime = sw.ElapsedMilliseconds;

            // Act - Commit
            sw.Restart();
            keeper.Commit();
            var commitTime = sw.ElapsedMilliseconds;

            // Act - Get snapshots
            sw.Restart();
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            var snapshotTime = sw.ElapsedMilliseconds;

            // Assert - Verify correct results
            Assert.AreEqual(count, snapshots.Count, "All tokens should be in snapshot");

            // Assert - Performance is reasonable
            Console.WriteLine($"Performance: Seed={seedTime}ms, Stage={stageTime}ms, Commit={commitTime}ms, GetSnapshot={snapshotTime}ms");

            // These thresholds are somewhat arbitrary and might need adjustment based on environment
            Assert.IsTrue(commitTime < 5000, "Commit should complete within 5 seconds");
            Assert.IsTrue(snapshotTime < 1000, "GetFullCurrentSnapshot should complete within 1 second");
        }

        [TestMethod]
        public void MultipleSeeds_SameHash_OnlyFirstSucceeds()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();

            // Act - First seed
            var result1 = keeper.Seed("same-hash", "First Value");

            // Act - Try to seed again with same hash
            var result2 = keeper.Seed("same-hash", "Second Value");

            // Assert
            Assert.AreEqual(TokenOpResult.Success, result1);
            Assert.AreEqual(TokenOpResult.DuplicateHash, result2);

            // Get snapshot and verify only first seed succeeded
            Assert.IsTrue(keeper.TryGetSnapshot("same-hash", out var snapshot));
            Assert.AreEqual("First Value", snapshot.CurrentValue);
        }

        [TestMethod]
        public void DeleteAndReinsert_SameHash_HandledCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();

            // Act - Seed initial value
            keeper.Seed("hash1", "Initial Value");

            // Act - Delete the token
            keeper.Stage("hash1", null, null);
            keeper.Commit();

            // Act - Reinsert with same hash
            var result = keeper.Stage(null, "hash1", "New Value");
            keeper.Commit();

            // Assert
            Assert.AreEqual(TokenOpResult.Success, result);

            // Get snapshot and verify new value
            Assert.IsTrue(keeper.TryGetSnapshot("hash1", out var snapshot));
            Assert.AreEqual("New Value", snapshot.CurrentValue);
            Assert.IsNull(snapshot.InitialHash);

            // Both tokens should be in the snapshot (deleted + reinserted)
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(2, snapshots.Count);

            // Original token should be marked as deleted
            var deletedToken = snapshots.FirstOrDefault(s =>
                s.InitialHash == "hash1" && s.CurrentHash == null);
            Assert.IsNotNull(deletedToken);

            // New token should be present with correct value
            var newToken = snapshots.FirstOrDefault(s =>
                s.CurrentHash == "hash1" && s.InitialHash == null);
            Assert.IsNotNull(newToken);
            Assert.AreEqual("New Value", newToken.CurrentValue);
        }

        [TestMethod]
        public void MultiStepOperations_HistoryPreserved()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();

            // Act - Seed initial value
            keeper.Seed("1", "Step 1");

            // Act - Series of updates
            keeper.Stage("1", "2", "Step 2");
            keeper.Commit();

            keeper.Stage("2", "3", "Step 3");
            keeper.Commit();

            keeper.Stage("3", "4", "Step 4");
            keeper.Commit();

            keeper.Stage("4", "5", "Step 5");
            keeper.Commit();

            // Assert - Final state is correct
            Assert.IsTrue(keeper.TryGetSnapshot("5", out var finalSnapshot));
            Assert.AreEqual("Step 5", finalSnapshot.CurrentValue);
            Assert.AreEqual("Step 1", finalSnapshot.InitialValue);
            Assert.AreEqual("4", finalSnapshot.PreviousHash);
            Assert.AreEqual("Step 4", finalSnapshot.PreviousValue);

            // Full history through diffs
            var diffs = keeper.GetFullDiff().ToList();
            Assert.AreEqual(1, diffs.Count);

            var fullDiff = diffs.First();
            Assert.AreEqual("1", fullDiff.LeftHash);
            Assert.AreEqual("5", fullDiff.RightHash);
            Assert.AreEqual("Step 1", fullDiff.LeftValue);
            Assert.AreEqual("Step 5", fullDiff.RightValue);
        }

        #endregion

        #region Original Test Methods (For Backward Compatibility)

        [TestMethod]
        public void DocumentItemTokenKeeper_PerformsMultipleOperations_Correctly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<DocumentItem>();

            // Create initial documents
            for (int i = 1; i <= 5; i++)
            {
                var doc = new DocumentItem
                {
                    Id = $"doc{i}",
                    Title = $"Document {i}",
                    Content = $"Content for document {i}",
                    LastModified = DateTime.Now.AddDays(-i),
                    Version = 1
                };

                keeper.Seed(i.ToString(), doc);
            }

            // Act - Perform various operations

            // 1. Update document 1
            var updatedDoc1 = new DocumentItem
            {
                Id = "doc1",
                Title = "Updated Document 1",
                Content = "Updated content for document 1",
                LastModified = DateTime.Now,
                Version = 2
            };
            keeper.Stage("1", "101", updatedDoc1);

            // 2. Delete document 2
            keeper.Stage("2", null, null);

            // 3. Insert new document
            var newDoc = new DocumentItem
            {
                Id = "doc6",
                Title = "New Document 6",
                Content = "Content for new document",
                LastModified = DateTime.Now,
                Version = 1
            };
            keeper.Stage(null, "106", newDoc);

            // Commit all changes
            keeper.Commit();

            // Assert - Verify final state

            // Get all documents
            var allDocs = keeper.GetFullCurrentSnapshot().ToList();

            // System preserves deleted tokens in snapshot
            // Should have 6 documents: 3 unchanged + 1 updated + 1 new + 1 deleted = 6
            Assert.AreEqual(6, allDocs.Count);

            // Count active documents (with non-null CurrentHash)
            var activeDocuments = allDocs.Count(d => d.CurrentHash != null);
            Assert.AreEqual(5, activeDocuments, "Should have 5 active (non-deleted) documents");

            // Check specific documents
            Assert.IsTrue(keeper.TryGetSnapshot("101", out var doc1Snapshot));
            Assert.AreEqual("Updated Document 1", doc1Snapshot.CurrentValue.Title);
            Assert.AreEqual(2, doc1Snapshot.CurrentValue.Version);

            // Document 2 should still be in the snapshot but marked as deleted
            var deletedDoc = allDocs.FirstOrDefault(d => d.InitialHash == "2" && d.CurrentHash == null);
            Assert.IsNotNull(deletedDoc, "Deleted document should exist in snapshot");
            Assert.IsNull(deletedDoc.CurrentValue, "Deleted document should have null CurrentValue");

            // Document 6 should exist
            Assert.IsTrue(keeper.TryGetSnapshot("106", out var doc6Snapshot));
            Assert.AreEqual("New Document 6", doc6Snapshot.CurrentValue.Title);

            // Check diffs
            var committedDiff = keeper.GetCommittedDiff().ToList();
            Assert.AreEqual(3, committedDiff.Count); // Update + delete + insert
        }

        [TestMethod]
        public void TokenStateKeeper_BatchOperations_HandledCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<int>();

            // Act - Add many items
            for (int i = 1; i <= 100; i++)
            {
                keeper.Seed(i.ToString(), i * 10);
            }

            // Update every other item
            for (int i = 1; i <= 100; i += 2)
            {
                keeper.Stage(i.ToString(), (i + 1000).ToString(), i * 20);
            }

            keeper.Commit();

            // Delete every fourth item
            for (int i = 4; i <= 100; i += 4)
            {
                keeper.Stage(i.ToString(), null, 0);
            }

            keeper.Commit();

            // Assert
            var allItems = keeper.GetFullCurrentSnapshot().ToList();

            // System keeps deleted tokens in the snapshot
            // Should have 100 items: 75 active + 25 deleted = 100
            Assert.AreEqual(100, allItems.Count);

            // Count active items
            var activeItems = allItems.Count(i => i.CurrentHash != null);
            Assert.AreEqual(75, activeItems, "Should have 75 active (non-deleted) items");

            // Check some specific values
            Assert.IsTrue(keeper.TryGetSnapshot("1001", out var item1));
            Assert.AreEqual(20, item1.CurrentValue);

            Assert.IsTrue(keeper.TryGetSnapshot("2", out var item2));
            Assert.AreEqual(20, item2.CurrentValue);

            // Item 4 should be deleted but still in the snapshot
            var deletedItem = allItems.FirstOrDefault(i => i.InitialHash == "4" && i.CurrentHash == null);
            Assert.IsNotNull(deletedItem, "Deleted item should exist in snapshot");

            // TryGetSnapshot should not return deleted tokens
            Assert.IsFalse(keeper.TryGetSnapshot("4", out _), "TryGetSnapshot should not return deleted tokens");
        }

        #endregion
    }
}