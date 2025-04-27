namespace TokenKeeper.Tests
{
    [TestClass]
    public class TokenStateKeeperGenericTests
    {
        #region Test Models

        // Simple custom value type for testing
        public struct Point3D
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }

            public override string ToString() => $"({X}, {Y}, {Z})";
        }

        // More complex object with references
        public class DocumentItem
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public DateTime Created { get; set; }
            public List<string> Tags { get; set; } = new List<string>();
            public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

            public override string ToString() => $"{Id}: {Title} ({Tags.Count} tags, {Metadata.Count} metadata items)";
        }

        #endregion

        #region Tests for Basic Types

        [TestMethod]
        public void IntTokenKeeper_BasicOperations_Succeed()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<int>();

            // Act
            keeper.Seed("1", 100);
            keeper.Seed("2", 200);
            keeper.Stage("1", "3", 300);
            keeper.Commit();

            // Assert
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(2, snapshots.Count);

            Assert.IsTrue(keeper.TryGetSnapshot("3", out var snapshot));
            Assert.AreEqual(300, snapshot.CurrentValue);
            Assert.AreEqual("1", snapshot.InitialHash);

            Assert.IsTrue(keeper.TryGetSnapshot("2", out var snapshot2));
            Assert.AreEqual(200, snapshot2.CurrentValue);
        }

        [TestMethod]
        public void DoubleTokenKeeper_StagesAndCommits_Correctly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<double?>();

            // Act
            keeper.Seed("1", 1.5);
            keeper.Seed("2", 2.5);
            keeper.Stage("1", "3", 3.5);
            keeper.Stage("2", null, null);
            keeper.Commit();

            // Assert
            var diffs = keeper.GetCommittedDiff().ToList();
            Assert.AreEqual(2, diffs.Count);

            var updateDiff = diffs.FirstOrDefault(d => d.RightHash == "3");
            Assert.IsNotNull(updateDiff);
            Assert.AreEqual("1", updateDiff.LeftHash);
            Assert.AreEqual(1.5, updateDiff.LeftValue);
            Assert.AreEqual(3.5, updateDiff.RightValue);

            var deleteDiff = diffs.FirstOrDefault(d => d.RightHash == null);
            Assert.IsNotNull(deleteDiff);
            Assert.AreEqual("2", deleteDiff.LeftHash);
            Assert.AreEqual(2.5, deleteDiff.LeftValue);
            Assert.IsNull(deleteDiff.RightValue);
        }

        [TestMethod]
        public void DateTimeTokenKeeper_DiffsAndSnapshotsWork_Correctly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<DateTime>();
            var date1 = new DateTime(2023, 1, 1);
            var date2 = new DateTime(2023, 2, 1);
            var date3 = new DateTime(2023, 3, 1);

            // Act
            keeper.Seed("1", date1);
            keeper.Seed("2", date2);

            keeper.Stage("1", "3", date3);
            keeper.Commit();

            // Assert
            Assert.IsTrue(keeper.TryGetSnapshot("3", out var snapshot));
            Assert.AreEqual(date1, snapshot.InitialValue);
            Assert.AreEqual(date3, snapshot.CurrentValue);

            var diffs = keeper.GetFullDiff().ToList();
            Assert.AreEqual(1, diffs.Count); // Only date1 -> date3 changed

            var diff = diffs.First();
            Assert.AreEqual(date1, diff.LeftValue);
            Assert.AreEqual(date3, diff.RightValue);
        }

        #endregion

        #region Tests for Custom Value Types

        [TestMethod]
        public void PointStructTokenKeeper_HandlesStructsCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<Point3D>();
            var point1 = new Point3D { X = 1, Y = 2, Z = 3 };
            var point2 = new Point3D { X = 4, Y = 5, Z = 6 };

            // Act
            keeper.Seed("1", point1);
            keeper.Stage("1", "2", point2);
            keeper.Commit();

            // Assert
            Assert.IsTrue(keeper.TryGetSnapshot("2", out var snapshot));
            Assert.AreEqual(1, snapshot.InitialValue.X);
            Assert.AreEqual(4, snapshot.CurrentValue.X);

            // Create another Point3D with the same values to ensure value equality works
            var point3 = new Point3D { X = 4, Y = 5, Z = 6 };
            keeper.Stage("2", "3", point3);

            var diffs = keeper.GetUncommittedDiff().ToList();
            Assert.AreEqual(1, diffs.Count);
            var diff = diffs.First();
            Assert.AreEqual(4, diff.LeftValue.X);
            Assert.AreEqual(4, diff.RightValue.X);
        }

        #endregion

        #region Tests for Complex Reference Types

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
                Created = DateTime.Now.AddDays(-1),
                Tags = { "draft", "important" },
                Metadata = { { "author", "John Doe" }, { "department", "R&D" } }
            };

            var doc2 = new DocumentItem
            {
                Id = "doc2",
                Title = "Second Document",
                Content = "This is the content of the second document",
                Created = DateTime.Now.AddDays(-2),
                Tags = { "final", "archived" },
                Metadata = { { "author", "Jane Smith" }, { "department", "Marketing" } }
            };

            // Act - Seed initial documents
            keeper.Seed("1", doc1);
            keeper.Seed("2", doc2);

            // Update document 1
            doc1.Title = "Updated First Document";
            doc1.Tags.Add("updated");
            doc1.Metadata["status"] = "in-review";

            keeper.Stage("1", "3", doc1);
            keeper.Commit();

            // Assert
            Assert.IsTrue(keeper.TryGetSnapshot("3", out var snapshot));
            Assert.AreEqual("Updated First Document", snapshot.CurrentValue.Title);
            Assert.AreEqual(3, snapshot.CurrentValue.Tags.Count);
            Assert.IsTrue(snapshot.CurrentValue.Tags.Contains("updated"));
            Assert.AreEqual(3, snapshot.CurrentValue.Metadata.Count);
            Assert.AreEqual("in-review", snapshot.CurrentValue.Metadata["status"]);

            // Initial value should still have original values
            Assert.AreEqual("First Document", snapshot.InitialValue.Title);
            Assert.AreEqual(2, snapshot.InitialValue.Tags.Count);
        }

        [TestMethod]
        public void DocumentItemTokenKeeper_PerformsMultipleOperations_Correctly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<DocumentItem>();
            var baseDate = new DateTime(2023, 1, 1);

            // Create initial documents
            var documents = new List<DocumentItem>();
            for (int i = 0; i < 5; i++)
            {
                documents.Add(new DocumentItem
                {
                    Id = $"doc{i}",
                    Title = $"Document {i}",
                    Content = $"Content for document {i}",
                    Created = baseDate.AddDays(i),
                    Tags = { $"tag{i}", "common" },
                    Metadata = { { "version", "1.0" } }
                });
            }

            // Act - Seed initial documents
            for (int i = 0; i < documents.Count; i++)
            {
                keeper.Seed(i.ToString(), documents[i]);
            }

            // Modify some documents
            documents[0].Title = "Updated Title 0";
            keeper.Stage("0", "10", documents[0]);

            documents[1].Tags.Add("important");
            keeper.Stage("1", "11", documents[1]);

            documents[2].Metadata["status"] = "approved";
            keeper.Stage("2", "12", documents[2]);

            // Delete document 3
            keeper.Stage("3", null, null);

            // Add a new document
            var newDoc = new DocumentItem
            {
                Id = "newDoc",
                Title = "Brand New Document",
                Created = DateTime.Now,
                Tags = { "new", "draft" }
            };
            keeper.Stage(null, "20", newDoc);

            // Commit all changes
            keeper.Commit();

            // Assert
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(5, snapshots.Count); // 4 original docs (one deleted) + 1 new

            // Verify specific updates
            Assert.IsTrue(keeper.TryGetSnapshot("10", out var doc0));
            Assert.AreEqual("Updated Title 0", doc0.CurrentValue.Title);

            Assert.IsTrue(keeper.TryGetSnapshot("11", out var doc1));
            Assert.AreEqual(3, doc1.CurrentValue.Tags.Count);
            Assert.IsTrue(doc1.CurrentValue.Tags.Contains("important"));

            Assert.IsTrue(keeper.TryGetSnapshot("12", out var doc2));
            Assert.AreEqual("approved", doc2.CurrentValue.Metadata["status"]);

            Assert.IsTrue(keeper.TryGetSnapshot("20", out var newDocSnapshot));
            Assert.AreEqual("Brand New Document", newDocSnapshot.CurrentValue.Title);
            Assert.AreEqual(2, newDocSnapshot.CurrentValue.Tags.Count);

            // Check committed diffs
            var diffs = keeper.GetCommittedDiff().ToList();
            Assert.AreEqual(5, diffs.Count); // 3 updates, 1 delete, 1 insert
        }

        [TestMethod]
        public void ComplexObjects_WithInitialNullValues_HandleCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<DocumentItem>();

            // Act - Seed with null
            keeper.Seed("1", null);

            // Create a document
            var doc = new DocumentItem
            {
                Id = "doc1",
                Title = "New Document"
            };

            // Update from null to document
            keeper.Stage("1", "2", doc);
            keeper.Commit();

            // Assert
            Assert.IsTrue(keeper.TryGetSnapshot("2", out var snapshot));
            Assert.IsNull(snapshot.InitialValue);
            Assert.IsNotNull(snapshot.CurrentValue);
            Assert.AreEqual("New Document", snapshot.CurrentValue.Title);

            // Set back to null
            keeper.Stage("2", "3", null);
            keeper.Commit();

            // Assert
            Assert.IsTrue(keeper.TryGetSnapshot("3", out var nullSnapshot));
            Assert.IsNull(nullSnapshot.CurrentValue);
        }

        #endregion

        #region Comparison with Legacy TokenStateKeeper

        [TestMethod]
        public void GenericStringTokenKeeper_MatchesLegacyBehavior()
        {
            // Arrange
            var legacyKeeper = TokenStateKeeperProvider.Create();
            var genericKeeper = TokenStateKeeperProvider.Create<string>();

            // Act - Same operations on both
            legacyKeeper.Seed("1", "Value 1");
            genericKeeper.Seed("1", "Value 1");

            legacyKeeper.Stage("1", "2", "Value 2");
            genericKeeper.Stage("1", "2", "Value 2");

            legacyKeeper.Commit();
            genericKeeper.Commit();

            // Assert - Same results
            legacyKeeper.TryGetSnapshot("2", out var legacySnapshot);
            genericKeeper.TryGetSnapshot("2", out var genericSnapshot);

            Assert.AreEqual(legacySnapshot.InitialHash, genericSnapshot.InitialHash);
            Assert.AreEqual(legacySnapshot.CurrentHash, genericSnapshot.CurrentHash);
            Assert.AreEqual(legacySnapshot.InitialValue, genericSnapshot.InitialValue);
            Assert.AreEqual(legacySnapshot.CurrentValue, genericSnapshot.CurrentValue);

            // Compare diffs
            var legacyDiffs = legacyKeeper.GetFullDiff().ToList();
            var genericDiffs = genericKeeper.GetFullDiff().ToList();

            Assert.AreEqual(legacyDiffs.Count, genericDiffs.Count);
            Assert.AreEqual(legacyDiffs[0].LeftHash, genericDiffs[0].LeftHash);
            Assert.AreEqual(legacyDiffs[0].RightHash, genericDiffs[0].RightHash);
            Assert.AreEqual(legacyDiffs[0].LeftValue, genericDiffs[0].LeftValue);
            Assert.AreEqual(legacyDiffs[0].RightValue, genericDiffs[0].RightValue);
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void EdgeCase_SequentialUpdates_TrackHistoryCorrectly()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<string>();

            // Act - Multiple sequential updates
            keeper.Seed("1", "Version 1");
            keeper.Commit();

            keeper.Stage("1", "2", "Version 2");
            keeper.Commit();

            keeper.Stage("2", "3", "Version 3");
            keeper.Commit();

            keeper.Stage("3", "4", "Version 4");
            keeper.Commit();

            keeper.Stage("4", "5", "Version 5");
            keeper.Commit();

            // Assert - Final snapshot has correct history
            Assert.IsTrue(keeper.TryGetSnapshot("5", out var snapshot));
            Assert.AreEqual("1", snapshot.InitialHash);
            Assert.AreEqual("4", snapshot.PreviousHash);
            Assert.AreEqual("5", snapshot.CurrentHash);
            Assert.AreEqual("Version 1", snapshot.InitialValue);
            Assert.AreEqual("Version 4", snapshot.PreviousValue);
            Assert.AreEqual("Version 5", snapshot.CurrentValue);

            // Check diff history
            var fullDiff = keeper.GetFullDiff().ToList();
            Assert.AreEqual(1, fullDiff.Count); // Only one diff: from initial to current
            Assert.AreEqual("1", fullDiff[0].LeftHash);
            Assert.AreEqual("5", fullDiff[0].RightHash);
            Assert.AreEqual("Version 1", fullDiff[0].LeftValue);
            Assert.AreEqual("Version 5", fullDiff[0].RightValue);
        }

        [TestMethod]
        public void EdgeCase_ConcurrentOperations_MaintainsConsistency()
        {
            // Arrange
            var keeper = TokenStateKeeperProvider.Create<int>();
            const int count = 1000;

            // Seed initial values
            for (int i = 0; i < count; i++)
            {
                keeper.Seed(i.ToString(), i);
            }

            // Act - Parallel updates
            Parallel.For(0, count, i =>
            {
                keeper.Stage(i.ToString(), (i + count).ToString(), i * 2);
            });

            keeper.Commit();

            // Assert
            var snapshots = keeper.GetFullCurrentSnapshot().ToList();
            Assert.AreEqual(count, snapshots.Count);

            // Check a few values
            for (int i = 0; i < 10; i++)
            {
                int index = i * 100; // Sample evenly
                Assert.IsTrue(keeper.TryGetSnapshot((index + count).ToString(), out var snapshot));
                Assert.AreEqual(index, snapshot.InitialValue);
                Assert.AreEqual(index * 2, snapshot.CurrentValue);
            }
        }

        #endregion
    }
}