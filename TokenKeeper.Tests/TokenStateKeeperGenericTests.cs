using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenKeeper.Tests
{
    /// <summary>
    /// Tests for generic TokenStateKeeper implementation.
    /// </summary>
    [TestClass]
    public class TokenStateKeeperGenericTests
    {
        #region Test Classes

        /// <summary>
        /// Simple document object for testing complex object storage.
        /// </summary>
        public class DocumentItem
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public DateTime LastModified { get; set; }
            public int Version { get; set; }

            public override bool Equals(object obj)
            {
                if (obj is not DocumentItem other)
                    return false;

                return Id == other.Id &&
                       Title == other.Title &&
                       Content == other.Content &&
                       LastModified.Equals(other.LastModified) &&
                       Version == other.Version;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Id, Title, Content, LastModified, Version);
            }

            public override string ToString()
            {
                return $"Document {Id}: {Title} (v{Version})";
            }
        }

        /// <summary>
        /// Point structure for testing value type storage.
        /// </summary>
        public struct Point
        {
            public int X { get; set; }
            public int Y { get; set; }

            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }

            public override string ToString() => $"({X}, {Y})";
        }

        #endregion

        #region Basic Tests

        [TestMethod]
        public void IntTokenKeeper_BasicOperations_WorkCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<int>();

            // Act & Assert - Seed
            Assert.AreEqual(TokenOpResult.Success, keeper.Seed("1", 100));
            Assert.AreEqual(TokenOpResult.Success, keeper.Seed("2", 200));
            Assert.AreEqual(TokenOpResult.DuplicateHash, keeper.Seed("1", 300));

            // Act & Assert - Stage and commit
            Assert.AreEqual(TokenOpResult.Success, keeper.Stage("1", "3", 300));
            keeper.Commit();

            // Act & Assert - Get snapshot
            Assert.IsTrue(keeper.TryGetSnapshot("3", out var snapshot));
            Assert.AreEqual(300, snapshot.CurrentValue);
            Assert.AreEqual("1", snapshot.InitialHash);
            Assert.AreEqual(100, snapshot.InitialValue);
        }

        [TestMethod]
        public void StringTokenKeeper_HandlesNullValues_Correctly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();

            // Act & Assert - Seed with null
            Assert.AreEqual(TokenOpResult.Success, keeper.Seed("1", null));

            // Verify null was stored
            Assert.IsTrue(keeper.TryGetSnapshot("1", out var snapshot));
            Assert.IsNull(snapshot.CurrentValue);

            // Act & Assert - Update from null to value
            Assert.AreEqual(TokenOpResult.Success, keeper.Stage("1", "2", "Not null now"));
            keeper.Commit();

            // Verify update worked
            Assert.IsTrue(keeper.TryGetSnapshot("2", out var updatedSnapshot));
            Assert.AreEqual("Not null now", updatedSnapshot.CurrentValue);
            Assert.IsNull(updatedSnapshot.InitialValue);

            // Act & Assert - Update from value to null
            Assert.AreEqual(TokenOpResult.Success, keeper.Stage("2", "3", null));
            keeper.Commit();

            // Verify null was stored again
            Assert.IsTrue(keeper.TryGetSnapshot("3", out var finalSnapshot));
            Assert.IsNull(finalSnapshot.CurrentValue);
        }

        [TestMethod]
        public void ValueTypeTokenKeeper_HandlesStructs_Correctly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<Point>();
            var point1 = new Point(1, 2);
            var point2 = new Point(3, 4);

            // Act & Assert - Seed and update
            Assert.AreEqual(TokenOpResult.Success, keeper.Seed("1", point1));
            Assert.AreEqual(TokenOpResult.Success, keeper.Stage("1", "2", point2));
            keeper.Commit();

            // Verify values
            Assert.IsTrue(keeper.TryGetSnapshot("2", out var snapshot));
            Assert.AreEqual(point2.X, snapshot.CurrentValue.X);
            Assert.AreEqual(point2.Y, snapshot.CurrentValue.Y);
            Assert.AreEqual(point1.X, snapshot.InitialValue.X);
            Assert.AreEqual(point1.Y, snapshot.InitialValue.Y);
        }

        #endregion

        #region Complex Object Tests

        [TestMethod]
        public void DocumentItemTokenKeeper_HandlesComplexObjectsCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<DocumentItem>();

            var doc1 = new DocumentItem
            {
                Id = "doc1",
                Title = "First Document",
                Content = "This is the content of the first document",
                LastModified = new DateTime(2023, 1, 1),
                Version = 1
            };

            var doc2 = new DocumentItem
            {
                Id = "doc2",
                Title = "Second Document",
                Content = "This is the content of the second document",
                LastModified = new DateTime(2023, 1, 2),
                Version = 1
            };

            // Act - Seed initial documents
            keeper.Seed("1", doc1);
            keeper.Seed("2", doc2);

            // Act - Update first document
            var updatedDoc1 = new DocumentItem
            {
                Id = "doc1",
                Title = "Updated First Document",
                Content = "This content has been updated",
                LastModified = DateTime.Now,
                Version = 2
            };

            keeper.Stage("1", "3", updatedDoc1);
            keeper.Commit();

            // Assert - Verify original properties are preserved
            Assert.IsTrue(keeper.TryGetSnapshot("3", out var snapshot));

            // Original values should be preserved
            Assert.AreEqual("First Document", snapshot.InitialValue.Title);
            Assert.AreEqual(1, snapshot.InitialValue.Version);

            // Current values should be updated
            Assert.AreEqual("Updated First Document", snapshot.CurrentValue.Title);
            Assert.AreEqual(2, snapshot.CurrentValue.Version);

            // Document ID should remain the same
            Assert.AreEqual("doc1", snapshot.CurrentValue.Id);
        }

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

            // Should have 5 documents: 3 unchanged + 1 updated + 1 new - 1 deleted = 5
            Assert.AreEqual(5, allDocs.Count);

            // Check specific documents
            Assert.IsTrue(keeper.TryGetSnapshot("101", out var doc1Snapshot));
            Assert.AreEqual("Updated Document 1", doc1Snapshot.CurrentValue.Title);
            Assert.AreEqual(2, doc1Snapshot.CurrentValue.Version);

            // Document 2 should not exist in current state
            Assert.IsFalse(keeper.TryGetSnapshot("2", out _));

            // Document 6 should exist
            Assert.IsTrue(keeper.TryGetSnapshot("106", out var doc6Snapshot));
            Assert.AreEqual("New Document 6", doc6Snapshot.CurrentValue.Title);

            // Check diffs
            var committedDiff = keeper.GetCommittedDiff().ToList();
            Assert.AreEqual(3, committedDiff.Count); // Update + delete + insert
        }

        [TestMethod]
        public void TokenStateKeeper_ListsOfObjects_WorkCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<List<Point>>();

            // Create lists of points
            var list1 = new List<Point> { new Point(1, 1), new Point(2, 2), new Point(3, 3) };
            var list2 = new List<Point> { new Point(4, 4), new Point(5, 5) };

            // Act
            keeper.Seed("1", list1);
            keeper.Seed("2", list2);

            // Modify list1
            var modifiedList1 = new List<Point>(list1); // Copy the list
            modifiedList1.Add(new Point(4, 4));

            keeper.Stage("1", "3", modifiedList1);
            keeper.Commit();

            // Assert
            Assert.IsTrue(keeper.TryGetSnapshot("3", out var snapshot));

            // Check original list
            Assert.AreEqual(3, snapshot.InitialValue.Count);

            // Check modified list
            Assert.AreEqual(4, snapshot.CurrentValue.Count);
            Assert.AreEqual(4, snapshot.CurrentValue[3].X);
            Assert.AreEqual(4, snapshot.CurrentValue[3].Y);
        }

        #endregion

        #region Edge Case Tests

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

            // Should have 75 items: 100 - 25 (deleted) = 75
            Assert.AreEqual(75, allItems.Count);

            // Check some specific values
            Assert.IsTrue(keeper.TryGetSnapshot("1001", out var item1));
            Assert.AreEqual(20, item1.CurrentValue);

            Assert.IsTrue(keeper.TryGetSnapshot("2", out var item2));
            Assert.AreEqual(20, item2.CurrentValue);

            // Item 4 should be deleted
            Assert.IsFalse(keeper.TryGetSnapshot("4", out _));
        }

        [TestMethod]
        public void TokenStateKeeper_InvalidInputs_HandledGracefully()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();

            // Act & Assert - Invalid hash
            Assert.AreEqual(TokenOpResult.InvalidInput, keeper.Seed(null, "Test"));
            Assert.AreEqual(TokenOpResult.InvalidInput, keeper.Seed("", "Test"));
            Assert.AreEqual(TokenOpResult.InvalidInput, keeper.Seed("not-a-number", "Test"));

            // Both null hashes should be invalid
            Assert.AreEqual(TokenOpResult.InvalidInput, keeper.Stage(null, null, "Test"));

            // Valid seed then invalid stage
            keeper.Seed("1", "Original");
            Assert.AreEqual(TokenOpResult.UnknownHash, keeper.Stage("99", "2", "New"));
        }

        [TestMethod]
        public void TokenStateKeeper_DeleteThenInsertSameHash_WorksCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();

            // Act - Seed, delete, then insert with same hash
            keeper.Seed("1", "Original");
            keeper.Stage("1", null, null); // Delete
            keeper.Commit();

            var result = keeper.Stage(null, "1", "Reinserted"); // Insert with same hash
            keeper.Commit();

            // Assert
            Assert.AreEqual(TokenOpResult.Success, result);

            // Both the deleted token and new token should exist in the full snapshot
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(2, snapshots.Count);

            // The TryGetSnapshot should return the current active token
            Assert.IsTrue(keeper.TryGetSnapshot("1", out var active));
            Assert.AreEqual("Reinserted", active.CurrentValue);
        }

        #endregion
    }
}