using System.Collections.Generic;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The siege engines a player destroys come back for the next wave (#1160). Each wave start restores them before the Hammer writes their hit points, and a wave start that restored one writes
/// <c>engine-restore</c>; that wave's clear (or the end of a run inside it) writes <c>engine-use</c> from a read of the field. The test option <c>break_engines</c> destroys them at one clear, when a wave
/// follows it, so an unattended run proves the restore: the bot never attacks an engine.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    // The engines this wave's start brought back, until its clear judges them.
    private IReadOnlyList<LdEngineState>? _restoredEngines;
    private int _restoredWave;

    private void RestoreEngines(int wave)
    {
        var restore = _field.RestoreEngines();
        if (restore.Restored.Count == 0 && restore.Problem == null)
            return;

        var verdict = LongDefeatEngineChecks.Restored(wave, restore);
        LongDefeatChecks.Record(_outcomes, LongDefeatEngineChecks.RestoreId, "w" + wave, verdict);
        _logger.LogInfo($"{Tag}[Engines] w{wave} restored {verdict.Detail}");
        if (restore.Restored.Count == 0)
            return;

        _restoredEngines = restore.Restored;
        _restoredWave = wave;
    }

    /// <summary>The use check of the engines this wave brought back: once, at its clear or at the end of a run inside it.</summary>
    private void JudgeEngineUse(bool runEnded)
    {
        var restored = _restoredEngines;
        _restoredEngines = null;
        if (restored == null)
            return;

        var verdict = LongDefeatEngineChecks.Used(_restoredWave, restored, _field.ReadEngines(), runEnded);
        LongDefeatChecks.Record(_outcomes, LongDefeatEngineChecks.UseId, "w" + _restoredWave, verdict);
        _logger.LogInfo($"{Tag}[Engines] w{_restoredWave} use {verdict.Detail}");
    }

    /// <summary>The test option: breaks the engines at the clear it names, only when the run goes on, since no later wave could prove the restore.</summary>
    private void BreakEnginesAt(int clears)
    {
        if (_plan.Options.BreakEnginesAtClear != clears || _run.Result != null)
            return;

        var broken = _field.BreakEngines();
        _logger.LogInfo($"{Tag}[Engines] break_engines: {broken} engine(s) destroyed at clear {clears}");
    }
}
