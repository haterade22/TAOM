using TAOM.Core.Logging;

namespace TAOM.Features.CompanionTactics.BattleActionBar;

/// <summary>
/// The #970 [Check] ids that tools/ingame_checks/970.toml reads, and the attach check's verdict at mission end (#1072).
/// In Main so the service can name the stance id and TAOM.Tests can call the classifier without the game.
/// </summary>
internal static class BattleActionBarChecks
{
    internal const string AttachId = "taom.battle-action-bar.attach";
    internal const string StanceId = "taom.battle-action-bar.stance";

    internal enum AttachEnd { None, SkipSettingOff, FailNeverAttached }

    // enabledBattleTickSeen: a screen tick in Battle mode found the setting on. A mission that never reached Battle
    // mode proves nothing: the attach runs only on a Battle-mode tick.
    internal static AttachEnd ClassifyAtEnd(bool fieldBattle, bool attachedOnce, bool enabledBattleTickSeen,
        bool endedInBattle, bool enabledAtEnd)
    {
        if (!fieldBattle || attachedOnce) return AttachEnd.None;
        if (enabledBattleTickSeen) return AttachEnd.FailNeverAttached;
        return endedInBattle && !enabledAtEnd ? AttachEnd.SkipSettingOff : AttachEnd.None;
    }

    // The two verdict mappings live here so a test can pin which result each case writes. A null log writes nothing.
    internal static void RecordAttach(IOutcomeLog? log, string key, bool movieLoaded)
    {
        if (movieLoaded)
            log?.Pass(AttachId, key, "mission=" + key);
        else
            log?.Fail(AttachId, key, "reason=movie-not-loaded mission=" + key);
    }

    internal static void RecordEnd(IOutcomeLog? log, string key, AttachEnd end)
    {
        if (end == AttachEnd.FailNeverAttached)
            log?.Fail(AttachId, key, "reason=never-attached mission=" + key);
        else if (end == AttachEnd.SkipSettingOff)
            log?.Skip(AttachId, key, "reason=setting-off mission=" + key);
    }
}
