using System.Diagnostics;

namespace TokenKeeper.Tests;

[TestClass]
public class TokenStateKeeperTests
{
    private static TokenStateKeeper Create() => new(new CoreStateKeeper());

    [TestMethod]
    public void Seed_DuplicateHash_ReturnsDuplicate()
    {
        var sut = Create();
        Assert.AreEqual(TokenOpResult.Success, sut.Seed("1", "A"));
        Assert.AreEqual(TokenOpResult.DuplicateHash, sut.Seed("1", "B"));
    }

    [TestMethod]
    public void StageInsert_UncommittedDiffContainsNewToken()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Stage(null, "2", "B");
        var diff = sut.GetUncommittedDiff().Single();
        Assert.IsNull(diff.LeftHash);
        Assert.AreEqual("2", diff.RightHash);
    }

    [TestMethod]
    public void StageDelete_UnknownHash_ReturnsUnknown()
    {
        var sut = Create();
        var res = sut.Stage("99", null, "X");
        Assert.AreEqual(TokenOpResult.UnknownHash, res);
    }

    [TestMethod]
    public void StageModify_HashCollision_ReturnsCollision()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Seed("2", "B");
        var res = sut.Stage("1", "2", "A->B");
        Assert.AreEqual(TokenOpResult.Collision, res);
    }

    [TestMethod]
    public void StageSameTokenTwice_ReturnsAlreadyStaged()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Stage("1", "3", "A*");
        var res = sut.Stage("1", "4", "A**");
        Assert.AreEqual(TokenOpResult.AlreadyStaged, res);
    }

    [TestMethod]
    public void Commit_MovesDiffToCommitted()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Stage("1", "2", "A*");
        sut.Commit();
        Assert.AreEqual(1, sut.GetCommittedDiff().Count());
    }

    [TestMethod]
    public void FullDiff_FallsBackToInitial()
    {
        var sut = Create();
        sut.Seed("5", "Z");
        sut.Stage("5", null, "");
        sut.Commit();
        var diff = sut.GetFullDiff().Single();
        Assert.AreEqual("5", diff.LeftHash);
        Assert.IsNull(diff.RightHash);
    }

    [TestMethod]
    public void Discard_ClearsUncommitted()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Stage("1", "2", "A*");
        sut.Discard();
        Assert.AreEqual(0, sut.GetUncommittedDiff().Count());
    }

    [TestMethod]
    public void DeleteThenReinsert_SameHashAllowed()
    {
        var sut = Create();
        sut.Seed("10", "X");
        sut.Stage("10", null, "");
        sut.Commit();
        var res = sut.Stage(null, "10", "X2");
        Assert.AreEqual(TokenOpResult.Success, res);
    }

    [TestMethod]
    public void Stage_BothHashesNull_ReturnsInvalid()
    {
        var sut = Create();
        var res = sut.Stage(null, null, "Y");
        Assert.AreEqual(TokenOpResult.InvalidInput, res);
    }

    [TestMethod]
    public void TryGetSnapshot_Unknown_ReturnsFalse()
    {
        var sut = Create();
        Assert.IsFalse(sut.TryGetSnapshot("123", out _));
    }

    [TestMethod]
    public void Commit_WithEmptyStaging_NoDiff()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Commit();
        Assert.AreEqual(0, sut.GetCommittedDiff().Count());
    }

    [TestMethod]
    public void MixedBatch_ModifyDeleteInsert_DiffCountThree()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Seed("2", "B");
        sut.Seed("3", "C");
        sut.Stage("1", "11", "A*");
        sut.Stage("2", null, "");
        sut.Stage(null, "12", "D");
        sut.Commit();
        Assert.AreEqual(3, sut.GetCommittedDiff().Count());
    }

    [TestMethod]
    public void ParallelStage_1000Tokens_DoesNotThrow()
    {
        var sut = Create();
        const int n = 1000;
        for (var i = 0; i < n; i++) sut.Seed(i.ToString(), $"V{i}");
        Parallel.For(0, n, i => sut.Stage(i.ToString(), (i + n).ToString(), $"V{i}*"));
        sut.Commit();
        Assert.AreEqual(n, sut.GetCommittedDiff().Count());
    }

    [TestMethod]
    public void Stress_Commit100k_UnderTwoSeconds()
    {
        var sut = Create();
        const int count = 100_000;
        for (var i = 0; i < count; i++) sut.Seed(i.ToString(), $"V{i}");
        for (var i = 0; i < count; i++) sut.Stage(i.ToString(), (i + count).ToString(), $"V{i}*");
        var sw = Stopwatch.StartNew();
        sut.Commit();
        sw.Stop();
        Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(2));
        Assert.AreEqual(count, sut.GetCommittedDiff().Count());
    }

    [TestMethod]
    public void InsertThenCommit_ShowsInsertInCommittedDiff()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Stage(null, "3", "C");
        sut.Commit();
        var diff = sut.GetCommittedDiff().Single(d => d.RightHash == "3");
        Assert.IsNull(diff.LeftHash);
        Assert.AreEqual("3", diff.RightHash);
    }

    [TestMethod]
    public void UncommittedDiff_InsertModifyDelete_AllReported()
    {
        var sut = Create();
        sut.Seed("1", "A");
        sut.Seed("2", "B");
        sut.Stage("1", "10", "A*");
        sut.Stage("2", null, "");
        sut.Stage(null, "11", "C");
        Assert.AreEqual(3, sut.GetUncommittedDiff().Count());
    }

    [TestMethod]
    public void ParallelStage_10000Tokens_DiffMatches()
    {
        var sut = Create();
        const int n = 10_000;
        for (var i = 0; i < n; i++) sut.Seed(i.ToString(), $"V{i}");
        Parallel.For(0, n, i => sut.Stage(i.ToString(), (i + n).ToString(), $"V{i}*"));
        sut.Commit();
        Assert.AreEqual(n, sut.GetCommittedDiff().Count());
    }

    [TestMethod]
    public void Delete_RemovesHashMapping_AllowsReSeed()
    {
        var sut = Create();
        sut.Seed("42", "X");
        sut.Stage("42", null, "");
        sut.Commit();
        var res = sut.Seed("42", "Y");
        Assert.AreEqual(TokenOpResult.Success, res);
    }

    [TestMethod]
    public void Concurrent_StageAndCommit_MultiThreadIntegrity()
    {
        var sut = Create();
        const int threads = 8;
        const int perThread = 5000;
        var seedCounter = 0;

        Parallel.For(0, threads, _ =>
        {
            var start = Interlocked.Add(ref seedCounter, perThread) - perThread;
            for (var i = 0; i < perThread; i++) sut.Seed((start + i).ToString(), "I");
        });

        Parallel.For(0, threads, t =>
        {
            var offset = t * perThread;
            for (var i = 0; i < perThread; i++) sut.Stage((offset + i).ToString(), (offset + i + 50_000).ToString(), "M");
        });

        sut.Commit();
        Assert.AreEqual(threads * perThread, sut.GetCommittedDiff().Count());
    }
}