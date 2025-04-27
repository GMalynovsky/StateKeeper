namespace TokenKeeper.Tests
{
    [TestClass]
    public partial class TokenStateKeeperGenericTests
    {
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

            // UPDATED EXPECTATION: System preserves deleted tokens in snapshot
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

            // UPDATED EXPECTATION: System keeps deleted tokens in the snapshot
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
    }
}