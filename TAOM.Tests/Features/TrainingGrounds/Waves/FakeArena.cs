using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>
/// A yard that behaves the way the engine's does where the director reads it: a spawned fighter raises <see cref="ActiveFighters"/>,
/// a return opens a conversation, a spawned player body clears <see cref="PlayerDown"/>. Each seam the director reads is settable
/// and one test moves it (testing-qa.md: a fake that returns 0 hides its seam).
/// </summary>
internal sealed class FakeArena : IArenaWaveArena
{
    private int _active;

    public int FrameCount { get; set; } = 6;

    public bool InConversation { get; set; }

    public bool MissionEnded { get; set; }

    public bool PlayerDown { get; set; }

    public int ActiveFighters => _active;

    public bool AgentRoom { get; set; } = true;

    public int VanillaSpawnedCount { get; set; } = 30;

    public List<PoolTroop> Troops { get; set; } = Enumerable.Range(0, 10).Select(i => new PoolTroop("t3_" + i, 3))
        .Concat(Enumerable.Range(0, 10).Select(i => new PoolTroop("t4_" + i, 4)))
        .Concat(Enumerable.Range(0, 10).Select(i => new PoolTroop("t5_" + i, 5))).ToList();

    public bool StandDownResult { get; set; } = true;

    /// <summary>The stand-down throws, after vanilla's fighters were faded (the fault the director settles).</summary>
    public bool StandDownThrows { get; set; }

    public int Faded { get; set; } = 6;

    public bool SpawnPlayerResult { get; set; } = true;

    /// <summary>The player's spawn throws (after the stand-down went through).</summary>
    public bool SpawnPlayerThrows { get; set; }

    /// <summary>The spawn of a troop with this id fails.</summary>
    public HashSet<string> FailingTroops { get; } = new();

    public bool ReturnResult { get; set; } = true;

    /// <summary>A return that succeeds but leaves the player with no active body.</summary>
    public bool PlayerDownAfterReturn { get; set; }

    /// <summary>A return that succeeds but opens no conversation.</summary>
    public bool ReturnOpensNoConversation { get; set; }

    /// <summary>Everything the director called, in order, for the tests that pin an order.</summary>
    public List<string> Calls { get; } = new();

    public List<(string Troop, int Stage)> Spawns { get; } = new();

    public List<(WaveAnnouncement Kind, int Wave, int Amount)> Announcements { get; } = new();

    public int ReturnCalls { get; private set; }

    /// <summary>What a test wants read at the moment the director asks to return (the debrief flag is set before the call).</summary>
    public Action? OnReturn { get; set; }

    /// <summary>The most fighters that stood at once since the last <see cref="ResetPeak"/>.</summary>
    public int Peak { get; private set; }

    public IReadOnlyList<PoolTroop> TownTroops() => Troops;

    public bool StandVanillaDown(out int faded)
    {
        Calls.Add("standdown");
        if (StandDownThrows)
            throw new InvalidOperationException("the stand-down failed after the fade");
        faded = StandDownResult ? Faded : 0;
        return StandDownResult;
    }

    public bool SpawnPlayer(int stage)
    {
        Calls.Add("player:" + stage);
        if (SpawnPlayerThrows)
            throw new InvalidOperationException("the player's body failed");
        if (SpawnPlayerResult)
            PlayerDown = false;
        return SpawnPlayerResult;
    }

    public bool SpawnFighter(string troopId, int stage)
    {
        Calls.Add("fighter");
        if (!AgentRoom || FailingTroops.Contains(troopId))
            return false;
        Spawns.Add((troopId, stage));
        _active++;
        Peak = Math.Max(Peak, _active);
        return true;
    }

    public bool ReturnToMaster()
    {
        Calls.Add("return");
        ReturnCalls++;
        OnReturn?.Invoke();
        _active = 0;
        if (!ReturnResult)
            return false;
        InConversation = !ReturnOpensNoConversation;
        PlayerDown = PlayerDownAfterReturn;
        return true;
    }

    public void Announce(WaveAnnouncement kind, int wave, int amount) => Announcements.Add((kind, wave, amount));

    public void Kill(int count = 1) => _active = Math.Max(0, _active - count);

    public void ResetPeak() => Peak = _active;
}
