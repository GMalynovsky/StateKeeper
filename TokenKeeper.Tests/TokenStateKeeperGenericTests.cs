using System.Diagnostics;

namespace TokenRepository.Tests
{
    [TestClass]
    public class TokenStateKeeperGenericTests
    {
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
            keeper.Seed("1", department);

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
            keeper.Stage("1", "2", updatedDepartment);
            keeper.Commit();

            // Assert
            Assert.IsTrue(keeper.TryGetSnapshot("2", out var snapshot));
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


        #endregion

        #region Advanced Scenarios

        [TestMethod]
        public void LargeDatasets_Performance_ReasonableTime()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();
            const int count = 10000;

            // Act - Seed many tokens
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < count; i++)
            {
                keeper.Seed(i.ToString(), $"Value{i}");
            }
            var seedTime = sw.ElapsedMilliseconds;

            // Act - Stage many updates
            sw.Restart();
            for (var i = 0; i < count; i++)
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
            var result1 = keeper.Seed("1", "First Value");

            // Act - Try to seed again with same hash
            var result2 = keeper.Seed("1", "Second Value");

            // Assert
            Assert.AreEqual(TokenOpResult.Success, result1);
            Assert.AreEqual(TokenOpResult.DuplicateHash, result2);

            // Get snapshot and verify only first seed succeeded
            Assert.IsTrue(keeper.TryGetSnapshot("1", out var snapshot));
            Assert.AreEqual("First Value", snapshot.CurrentValue);
        }

        [TestMethod]
        public void DeleteAndReinsert_SameHash_HandledCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();

            // Act - Seed initial value
            keeper.Seed("1", "Initial Value");

            // Act - Delete the token
            keeper.Stage("1", null, null);
            keeper.Commit();

            // Act - Reinsert with same hash
            var result = keeper.Stage(null, "1", "New Value");
            keeper.Commit();

            // Assert
            Assert.AreEqual(TokenOpResult.Success, result);

            // Get snapshot and verify new value
            Assert.IsTrue(keeper.TryGetSnapshot("1", out var snapshot));
            Assert.AreEqual("New Value", snapshot.CurrentValue);
            Assert.IsNull(snapshot.InitialHash);

            // Both tokens should be in the snapshot (deleted + reinserted)
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(2, snapshots.Count);

            // Original token should be marked as deleted
            var deletedToken = snapshots.FirstOrDefault(s =>
                s.InitialHash == "1" && s.CurrentHash == null);
            Assert.IsNotNull(deletedToken);

            // New token should be present with correct value
            var newToken = snapshots.FirstOrDefault(s =>
                s.CurrentHash == "1" && s.InitialHash == null);
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
            for (var i = 1; i <= 5; i++)
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

        #endregion
    }
}