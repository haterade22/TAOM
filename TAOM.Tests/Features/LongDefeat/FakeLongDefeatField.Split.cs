using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The opening split's half of the field fake: a split records the shares and answers what a test set, the lanes answer the levels a test set (copied, as the real read makes a new list), the held
/// groups are reported until a release, and a release answers what a test set.
/// </summary>
internal sealed partial class FakeLongDefeatField
{
    private bool _splitHeld;

    /// <summary>What the next split answers. The default is a made split of 40 soldiers.</summary>
    public LdSplitResult SplitAnswer { get; set; } = new(string.Empty, 21, 9, 7, 3,
        new LdPoint(226.8f, 147.7f, 9.9f), new LdPoint(168.9f, 221.4f, 20.7f), new LdPoint(228.7f, 132.4f, 1.7f), new LdPoint(155.5f, 206.2f, 29.7f));

    /// <summary>The level of each siege lane, as the next read reports it.</summary>
    public List<int> LaneLevels { get; } = new() { 0, 0, 0 };

    /// <summary>The held groups as a read reports them while a split is held.</summary>
    public List<LdSplitGroup> SplitGroups { get; } = new()
    {
        new LdSplitGroup("wall", 21, 6.5f, 18, false),
        new LdSplitGroup("gate", 9, 3.2f, 0, false),
        new LdSplitGroup("low", 7, 12.0f, 2, true),
        new LdSplitGroup("high", 3, 8.4f, 3, true),
    };

    public LdSplitRelease ReleaseAnswer { get; set; } = new(4, 0);

    public List<SplitShares> Splits { get; } = new();

    public int LaneReads { get; private set; }

    public LdSplitResult SplitDefenders(SplitShares shares)
    {
        Calls.Add("Split");
        Splits.Add(shares);
        _splitHeld = SplitAnswer.Made;
        return SplitAnswer;
    }

    public IReadOnlyList<int> ReadLaneLevels()
    {
        LaneReads++;
        return LaneLevels.ToList();
    }

    public IReadOnlyList<LdSplitGroup> ReadSplit() => _splitHeld ? SplitGroups.ToList() : Array.Empty<LdSplitGroup>();

    public LdSplitRelease ReleaseSplit()
    {
        Calls.Add("ReleaseSplit");
        _splitHeld = false;
        return ReleaseAnswer;
    }
}
