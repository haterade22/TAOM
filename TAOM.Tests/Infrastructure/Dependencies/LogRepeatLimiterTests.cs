using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// The table behind PatchShield's rate-limited swallow line (maintainer decision D16): the first
/// occurrence of a line is written in full, each repeat is counted, and nothing is ever dropped.
/// </summary>
[TestClass]
public class LogRepeatLimiterTests
{
    private static Dictionary<string, long> Counts(LogRepeatLimiter limiter) =>
        limiter.Repeated().ToDictionary(kv => kv.Key, kv => kv.Value);

    [TestMethod]
    public void ShouldWrite_FirstOccurrence_WritesTheLine()
    {
        var limiter = new LogRepeatLimiter(capacity: 8);

        Assert.IsTrue(limiter.ShouldWrite("line a"));
    }

    [TestMethod]
    public void ShouldWrite_RepeatedLine_IsNotWrittenAndEveryRepeatIsCounted()
    {
        var limiter = new LogRepeatLimiter(capacity: 8);

        Assert.IsTrue(limiter.ShouldWrite("line a"));
        Assert.IsFalse(limiter.ShouldWrite("line a"));
        Assert.IsFalse(limiter.ShouldWrite("line a"));
        Assert.IsFalse(limiter.ShouldWrite("line a"));

        CollectionAssert.AreEqual(
            new[] { new KeyValuePair<string, long>("line a", 3) }, limiter.Repeated().ToArray());
    }

    [TestMethod]
    public void ShouldWrite_DifferentLines_AreEachWrittenAndCountedSeparately()
    {
        // A line carries the exception message, so two messages are two lines: counting them together would
        // hide the second one's text.
        var limiter = new LogRepeatLimiter(capacity: 8);

        Assert.IsTrue(limiter.ShouldWrite("line a"));
        Assert.IsTrue(limiter.ShouldWrite("line b"));
        Assert.IsFalse(limiter.ShouldWrite("line b"));
        Assert.IsFalse(limiter.ShouldWrite("line a"));
        Assert.IsFalse(limiter.ShouldWrite("line a"));

        var counts = Counts(limiter);
        Assert.AreEqual(2, counts.Count);
        Assert.AreEqual(2L, counts["line a"]);
        Assert.AreEqual(1L, counts["line b"]);
    }

    [TestMethod]
    public void Repeated_NoLineRepeated_IsEmpty()
    {
        // A line seen once and written is already in the log in full; the session summary adds nothing for it.
        var limiter = new LogRepeatLimiter(capacity: 8);
        limiter.ShouldWrite("line a");
        limiter.ShouldWrite("line b");

        Assert.AreEqual(0, limiter.Repeated().Count);
    }

    [TestMethod]
    public void Repeated_SeveralLines_ListsTheMostRepeatedFirstThenByText()
    {
        var limiter = new LogRepeatLimiter(capacity: 8);
        foreach (var line in new[] { "b", "b", "a", "a", "c", "c", "c", "c", "d" }) limiter.ShouldWrite(line);

        var listed = limiter.Repeated().Select(kv => kv.Key + "=" + kv.Value).ToArray();

        // c repeated 3 times, a and b once each (ordered by text), d never repeated.
        CollectionAssert.AreEqual(new[] { "c=3", "a=1", "b=1" }, listed);
    }

    [TestMethod]
    public void ShouldWrite_TableFull_ANewLineIsStillWrittenInFullEveryTime()
    {
        // The table is bounded because a line's text includes the exception message. A line that does not fit is
        // written every time, as it was before any limit existed: nothing is dropped.
        var limiter = new LogRepeatLimiter(capacity: 2);
        Assert.IsTrue(limiter.ShouldWrite("line a"));
        Assert.IsTrue(limiter.ShouldWrite("line b"));

        Assert.IsTrue(limiter.ShouldWrite("line c"));
        Assert.IsTrue(limiter.ShouldWrite("line c"));
        Assert.IsTrue(limiter.ShouldWrite("line c"));

        Assert.AreEqual(0, limiter.Repeated().Count, "an untracked line has no count to report");
    }

    [TestMethod]
    public void ShouldWrite_TableFull_ALineAlreadyTrackedIsStillLimited()
    {
        var limiter = new LogRepeatLimiter(capacity: 2);
        limiter.ShouldWrite("line a");
        limiter.ShouldWrite("line b");
        limiter.ShouldWrite("line c");   // does not fit

        Assert.IsFalse(limiter.ShouldWrite("line a"));
        Assert.IsFalse(limiter.ShouldWrite("line a"));

        var counts = Counts(limiter);
        Assert.AreEqual(1, counts.Count);
        Assert.AreEqual(2L, counts["line a"]);
    }

    [TestMethod]
    public void ShouldWrite_ManyThreadsOneLine_WritesItExactlyOnceAndCountsTheRest()
    {
        // A finalizer fires on whichever thread called the patched method (the TWParallel workers included).
        const int threads = 8, perThread = 2000;
        var limiter = new LogRepeatLimiter(capacity: 8);
        int written = 0;

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < perThread; i++)
            {
                if (limiter.ShouldWrite("line a")) Interlocked.Increment(ref written);
            }
        });

        Assert.AreEqual(1, written);
        Assert.AreEqual(threads * perThread - 1L, Counts(limiter)["line a"]);
    }

    [TestMethod]
    public void ShouldWrite_FirstWriteFailed_TheNextOccurrenceWritesAgainAndTheFailedOneIsCounted()
    {
        // DiagLog swallows its own I/O failures, so the writer cannot assume its write landed. A line that never
        // reached the log stays owed until a write succeeds, and the occurrence whose write failed is counted, not lost.
        var limiter = new LogRepeatLimiter(capacity: 8);

        Assert.IsTrue(limiter.ShouldWrite("line a"));
        limiter.WriteFinished("line a", succeeded: false);
        Assert.IsTrue(limiter.ShouldWrite("line a"), "the line is not in the log yet, so this occurrence writes it");
        limiter.WriteFinished("line a", succeeded: true);
        Assert.IsFalse(limiter.ShouldWrite("line a"), "it landed: from here on it is counted");

        CollectionAssert.AreEqual(
            new[] { new KeyValuePair<string, long>("line a", 2) }, limiter.Repeated().ToArray());
    }

    [TestMethod]
    public void Repeated_LineWhoseOnlyWriteFailed_IsListedWithACountOfOneAndALineWrittenOnceIsNot()
    {
        // A line written once is already in the log in full, so nothing is reported for it. A line whose only write
        // failed never reached the log: its occurrence is counted and both reports list it though it never recurred,
        // so the count line the owner writes is then the only copy of its text.
        var limiter = new LogRepeatLimiter(capacity: 8);
        Assert.IsTrue(limiter.ShouldWrite("line a"));
        limiter.WriteFinished("line a", succeeded: false);
        Assert.IsTrue(limiter.ShouldWrite("line b"));
        limiter.WriteFinished("line b", succeeded: true);

        var onlyA = new[] { new KeyValuePair<string, long>("line a", 1) };
        CollectionAssert.AreEqual(onlyA, limiter.Repeated().ToArray(), "the session summary");
        CollectionAssert.AreEqual(onlyA, limiter.TakeUnreported().ToArray(), "the mission-start checkpoint");
    }

    [TestMethod]
    public void ShouldWrite_WriteInFlight_ACompetingOccurrenceIsCountedAndTheLineStillRetriesAfterAFailure()
    {
        var limiter = new LogRepeatLimiter(capacity: 8);

        Assert.IsTrue(limiter.ShouldWrite("line a"));    // one thread starts writing it
        Assert.IsFalse(limiter.ShouldWrite("line a"));   // another must not write it a second time
        limiter.WriteFinished("line a", succeeded: false);
        Assert.IsTrue(limiter.ShouldWrite("line a"), "nobody has written it, so the next occurrence does");
        Assert.IsFalse(limiter.ShouldWrite("line a"), "a competing occurrence during the retry is counted, not written twice");
        limiter.WriteFinished("line a", succeeded: true);

        Assert.AreEqual(3L, Counts(limiter)["line a"], "the two competing occurrences and the failed write");
    }

    [TestMethod]
    public void WriteFinished_LineThatDidNotFit_IsIgnoredAndTheLineStaysWrittenEveryTime()
    {
        var limiter = new LogRepeatLimiter(capacity: 1);
        limiter.ShouldWrite("line a");

        Assert.IsTrue(limiter.ShouldWrite("line b"));        // no room: untracked
        limiter.WriteFinished("line b", succeeded: false);   // nothing to settle, and nothing thrown

        Assert.IsTrue(limiter.ShouldWrite("line b"));
        Assert.AreEqual(0, limiter.Repeated().Count);
    }

    [TestMethod]
    public void ShouldWrite_ManyThreadsFirstWritesFail_WritesItExactlyOnceAndCountsEverythingElse()
    {
        // The first five writes fail, so five attempts are owed again before one lands. However the threads
        // interleave: one write lands, never two are in flight, and every other occurrence is counted.
        const int threads = 8, perThread = 2000, failures = 5;
        var limiter = new LogRepeatLimiter(capacity: 8);
        int attempts = 0, landed = 0;

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < perThread; i++)
            {
                if (!limiter.ShouldWrite("line a")) continue;
                bool succeeded = Interlocked.Increment(ref attempts) > failures;
                if (succeeded) Interlocked.Increment(ref landed);
                limiter.WriteFinished("line a", succeeded);
            }
        });

        Assert.AreEqual(1, landed);
        Assert.AreEqual(failures + 1, attempts, "no write after the one that landed");
        Assert.AreEqual(threads * perThread - 1L, Counts(limiter)["line a"]);
    }

    [TestMethod]
    public void TakeUnreported_RepeatsSinceTheLastCall_ListsEachGrownLineWithItsRunningTotalOnce()
    {
        var limiter = new LogRepeatLimiter(capacity: 8);
        limiter.ShouldWrite("line a");
        limiter.ShouldWrite("line a");   // repeated once
        limiter.ShouldWrite("line b");   // seen once: nothing to report

        var first = limiter.TakeUnreported();
        var second = limiter.TakeUnreported();

        CollectionAssert.AreEqual(new[] { "line a=1" }, first.Select(kv => kv.Key + "=" + kv.Value).ToArray());
        Assert.AreEqual(0, second.Count, "nothing grew since the last call");

        limiter.ShouldWrite("line a");
        limiter.ShouldWrite("line b");
        limiter.ShouldWrite("line b");
        var third = limiter.TakeUnreported();

        // The running total, not the growth, ordered like Repeated(): most repeated first, then by text.
        CollectionAssert.AreEqual(new[] { "line a=2", "line b=2" }, third.Select(kv => kv.Key + "=" + kv.Value).ToArray());
        Assert.AreEqual(2, limiter.Repeated().Count, "taking the unreported lines does not change the full list");
    }

    [TestMethod]
    public void Unreport_LineTakenButNotWritten_IsListedAgainWithItsTotalAndNoOtherLineIs()
    {
        // The owner of a boundary writes what TakeUnreported hands it. A write that did not land leaves the line
        // owed: the next call lists it though it did not grow, and a line whose write landed stays taken.
        var limiter = new LogRepeatLimiter(capacity: 8);
        foreach (var line in new[] { "line a", "line a", "line b", "line b" }) limiter.ShouldWrite(line);
        Assert.AreEqual(2, limiter.TakeUnreported().Count);

        limiter.Unreport("line a");

        var again = limiter.TakeUnreported();
        CollectionAssert.AreEqual(new[] { "line a=1" }, again.Select(kv => kv.Key + "=" + kv.Value).ToArray());
        Assert.AreEqual(0, limiter.TakeUnreported().Count, "taken again, it is settled");
        Assert.AreEqual(2, limiter.Repeated().Count, "giving a line back does not change the full list");
    }

    [TestMethod]
    public void Unreport_LineTheTableDoesNotHold_IsIgnored()
    {
        var limiter = new LogRepeatLimiter(capacity: 1);
        limiter.ShouldWrite("line a");

        limiter.Unreport("line b");   // never tracked: nothing to give back, and nothing thrown

        Assert.AreEqual(0, limiter.TakeUnreported().Count);
    }
}
