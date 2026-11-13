using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The opening split of the defenders (Mike, 2026-11-12): in each calm, once the garrison or a pack is in, the field splits the defenders into four groups and holds them at their places; at the
/// first contact of the wave (a side's count of attackers at the walls rose above its count at the hold, <see cref="DefenderSplit.Contact"/>) it hands them back to the siege AI, and the <c>split</c> line says whether they
/// stood at their places. A fortress row with no <c>split</c> block, or a director with no fortification adapter (the director's tests), plays as before.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private readonly SplitHold _splitHold = new();
    private IReadOnlyList<int> _closeAtHold = Array.Empty<int>();
    private float _splitHeldSeconds;

    // Set when the engine hands every formation to the AI under a hold: the lord's fall (Mission.OnAgentRemoved calls Team.DelegateCommandToAI after the behaviours run, so never split inside
    // that callback) and a new body (AdoptMain). The next split tick splits again.
    private string? _resplitWhy;

    // A pack can muster inside one tick, so the batches this tick spawned count as mustering, as the soldiers still to come do.
    private void TickSplit(float dt, bool spawnedDefenders)
    {
        if (_plan.Split is not { } shares || _fort == null)
            return;

        if (_splitHold.Held)
            _splitHeldSeconds += dt;

        if (_resplitWhy is { } taken)
        {
            _resplitWhy = null;
            if (_splitHold.Held)
                Split(shares, taken, false);
        }

        var phase = _run.Phase;
        var close = _splitHold.Held && phase == RunPhase.Wave ? _fort.ReadCloseAttackers() : null;
        var contact = close != null && DefenderSplit.Contact(_closeAtHold, close);
        switch (_splitHold.Next(phase, spawnedDefenders || _run.PendingDefenders > 0, contact))
        {
            case SplitStep.Split:
                Split(shares, phase.ToString().ToLowerInvariant(), true);
                break;
            case SplitStep.Resplit:
                Split(shares, "a pack mustered", false);
                break;
            case SplitStep.Release:
                ReleaseSplit(close!);
                break;
        }
    }

    // A fresh split starts the hold's clock and takes the close attacker counts after it drops the engine's 5 s cache: at a clear the cache can still hold the last wave's counts, which
    // as a baseline hid the next contact and, after a quick horn, released the hold at once. A split again inside the same hold (a pack, a new body, the lord's fall) keeps both, so a
    // count that rose before it still counts as contact.
    private void Split(SplitShares shares, string why, bool fresh)
    {
        var result = _fort!.SplitDefenders(shares);
        if (!result.Made)
        {
            _splitHold.Drop();
            _logger.LogWarning($"{Tag}[Split] not split ({why}): {result.Problem}");
            LongDefeatChecks.Record(_outcomes, LongDefeatSplitChecks.SplitId, SplitKey(), LongDefeatSplitChecks.NoPlaces(SplitWave(), result.Problem));
            return;
        }

        if (fresh)
        {
            _splitHeldSeconds = 0f;
            _fort.RefreshCloseAttackers();
            _closeAtHold = _fort.ReadCloseAttackers();
        }

        // A pair the scene gave no places for stays with the AI, and the check fails for it: a fortress row with a split block owes all four groups.
        var missing = MissingPairs(result);
        if (missing.Length > 0)
        {
            _logger.LogWarning($"{Tag}[Split] {why}: no places for the {missing}; that pair stays with the AI");
            LongDefeatChecks.Record(_outcomes, LongDefeatSplitChecks.SplitId, SplitKey(), LongDefeatSplitChecks.NoPlaces(SplitWave(), "no places for the " + missing));
        }

        _logger.LogInfo($"{Tag}[Split] {why}: wall {result.Wall} at {At(result.WallAt)}, gate {result.Gate} at {At(result.GateAt)}, "
            + $"lower archers {result.LowArchers} at {At(result.LowAt)}, high archers {result.HighArchers} at {At(result.HighAt)}; close {string.Join(",", _closeAtHold)}");
    }

    private void ReleaseSplit(IReadOnlyList<int> close)
    {
        var groups = _fort!.ReadSplit();
        var release = _fort.ReleaseSplit();
        _logger.LogInfo($"{Tag}[Split] contact in wave {_run.Wave}: close {string.Join(",", _closeAtHold)} to {string.Join(",", close)}; {Stand(groups)}; "
            + $"{release.Released} group(s) to the AI, {release.KeptByPlayer} kept on the player's order");
        LongDefeatChecks.Record(_outcomes, LongDefeatSplitChecks.SplitId, SplitKey(), LongDefeatSplitChecks.Stand(SplitWave(), groups, _splitHeldSeconds, release, close));
    }

    // The stalemate charge hands every formation to the AI itself (OrderCharge), so a hold still on then ends with a skip and no release call.
    private void DropSplitForCharge()
    {
        if (!_splitHold.Held || _fort == null)
            return;

        var groups = _fort.ReadSplit();
        _splitHold.Drop();
        LongDefeatChecks.Record(_outcomes, LongDefeatSplitChecks.SplitId, SplitKey(), LongDefeatSplitChecks.Closed(SplitWave(), groups, "charge"));
    }

    // A hold no contact ended: the wave cleared or the run ended while no sampled count rose. The groups go back to the AI, the line is a skip, and the next calm splits afresh.
    private void CloseSplit(int wave, string reason)
    {
        if (!_splitHold.Held || _fort == null)
            return;

        var groups = _fort.ReadSplit();
        _fort.ReleaseSplit();
        _splitHold.Drop();
        _logger.LogInfo($"{Tag}[Split] held to the end of wave {wave} with no contact ({reason}): {Stand(groups)}");
        LongDefeatChecks.Record(_outcomes, LongDefeatSplitChecks.SplitId, "w" + wave, LongDefeatSplitChecks.Closed(wave, groups, reason));
    }

    private void NoteEngineTookSplit(string why) => _resplitWhy = why;


    private void LogSplitStand(string when)
    {
        if (_splitHold.Held && _fort != null)
            _logger.LogInfo($"{Tag}[Split] {when}: {Stand(_fort.ReadSplit())}");
    }

    private string SplitKey() => "w" + SplitWave();

    private int SplitWave() => _run.Phase == RunPhase.Wave ? _run.Wave : _run.Wave + 1;

    private static string Stand(IReadOnlyList<LdSplitGroup> groups) =>
        groups.Count == 0 ? "no group held" : string.Join(", ", groups.Select(g => $"{g.Name} {g.Count} median {F1(g.Median)} m high {g.High}"));

    private static string MissingPairs(LdSplitResult r)
    {
        var foot = r.WallAt == null || r.GateAt == null;
        var archers = r.LowAt == null || r.HighAt == null;
        return foot && archers ? "infantry and archers" : foot ? "infantry" : archers ? "archers" : string.Empty;
    }

    private static string At(LdPoint? p) => p is { } v ? $"({F1(v.X)}, {F1(v.Y)}, {F1(v.Z)})" : "none";
}
