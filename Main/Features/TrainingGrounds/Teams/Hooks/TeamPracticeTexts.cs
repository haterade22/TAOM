using TaleWorlds.Localization;

namespace TAOM.Features.TrainingGrounds.Teams.Hooks;

/// <summary>
/// #1528: the Training Master's words in a team fight, as <c>{=KEY}</c> literals the localization harvester registers.
/// The dialogue lines take their text as a plain string, so the two dialogue literals are constants; the win
/// debrief is a TextObject because vanilla's debrief variable holds one. Each keeps vanilla's tokens: the reward
/// line reads the variables vanilla's reward condition sets, the win line the ones its post-fight condition sets.
/// </summary>
internal static class TeamPracticeTexts
{
    private const string CoinIcon = "<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">";

    internal const string Rules =
        "{=taom_tg_team_rules}Here we train the way soldiers fight: in teams. You join one side, and the fighters outlined in blue fight beside you. Your blows can't hurt them, and theirs can't hurt you. Thirty fighters take their turn in the yard, and your side wins when the last fighter of the other side goes down. Sounds like fun, eh?[ib:confident2]";

    internal const string Reward =
        "{=taom_tg_team_reward}If you can beat {OPPONENT_COUNT_1} opponents yourself before going down, you'll earn {PRIZE_1}{GOLD_ICON}. You'll get {PRIZE_2}{GOLD_ICON} for striking down at least {OPPONENT_COUNT_2}, {PRIZE_3}{GOLD_ICON} for {OPPONENT_COUNT_3} and {PRIZE_4}{GOLD_ICON} for {OPPONENT_COUNT_4}. Only the ones you put down yourself count. If your side wins while you're still on your feet, you earn the great prize of the fights, {PRIZE_5}{GOLD_ICON}. Sounds good, eh?[if:convo_nonchalant]";

    /// <summary>
    /// Vanilla's arena reward condition does not set {GOLD_ICON}; the global is not reliable, so every TAOM text that
    /// shows it sets it in the condition that builds the line (as <c>SettlementServicesTexts.SetGoldIcon</c> does).
    /// </summary>
    internal static void SetGoldIcon() => MBTextManager.SetTextVariable("GOLD_ICON", "{=!}" + CoinIcon, false);

    /// <summary>
    /// The debrief of a won team fight. Vanilla's post-fight condition has already set {PRIZE} and {OPPONENT_COUNT}
    /// and paid the gold; this replaces only the words of vanilla's "last one standing" line, which names a
    /// free-for-all.
    /// </summary>
    internal static TextObject WinDebrief() =>
        new("{=taom_tg_team_win}Well fought, {PLAYER.LINK}! Your side held the yard, and you were still standing at the end. You took down {OPPONENT_COUNT} of them yourself, and that earns you the top prize: {PRIZE}{GOLD_ICON}.");
}
