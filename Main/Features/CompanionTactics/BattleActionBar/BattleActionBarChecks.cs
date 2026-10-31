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
        if (enabledBattleTickSeen || (endedInBattle && enabledAtEnd)) return AttachEnd.FailNeverAttached;
        return endedInBattle ? AttachEnd.SkipSettingOff : AttachEnd.None;
    }
}
