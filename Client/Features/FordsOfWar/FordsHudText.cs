using TAOM.Features.FordsOfWar.Domain;
using TaleWorlds.Localization;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// Every player-facing line of the Fords of War command panel, each a whole localized sentence so a translator can reorder its
/// variables (build-spec 6). The key is the text before the first closing brace; <c>FordsHudStringsTests</c> pins that each one is
/// registered in <c>taom_module_strings.xml</c> and seeded in the 12 language files. Key names and numbers (F1, Z, 01:15) are not
/// text and stay in the view models.
/// </summary>
internal static class FordsHudText
{
    internal static string Gold(int gold) => new TextObject("{=TAOM_FOW_Hud_Gold}Gold: {GOLD}").SetTextVariable("GOLD", gold).ToString();

    internal static string Forming() => new TextObject("{=TAOM_FOW_Hud_Forming}The armies are forming up").ToString();

    internal static string Lane(int lane) => new TextObject("{=TAOM_FOW_Hud_Lane}Lane {LANE}").SetTextVariable("LANE", lane).ToString();

    internal static string LaneCounts(int own, int enemy) =>
        new TextObject("{=TAOM_FOW_Hud_LaneCounts}{OWN} v {ENEMY}").SetTextVariable("OWN", own).SetTextVariable("ENEMY", enemy).ToString();

    internal static string TroopsTitle(int lane) =>
        new TextObject("{=TAOM_FOW_Hud_TroopsTitle}Troops for lane {LANE}").SetTextVariable("LANE", lane).ToString();

    internal static string OrdersTitle(int lane) =>
        new TextObject("{=TAOM_FOW_Hud_OrdersTitle}Orders for lane {LANE}").SetTextVariable("LANE", lane).ToString();

    internal static string HeroesTitle() => new TextObject("{=TAOM_FOW_Hud_HeroesTitle}Heroes").ToString();

    internal static string TowersTitle() => new TextObject("{=TAOM_FOW_Hud_TowersTitle}Towers").ToString();

    internal static string PointsTitle() => new TextObject("{=TAOM_FOW_Hud_PointsTitle}Capture points").ToString();

    /// <summary>The holder line of a capture point as the commander sees it: yours, the enemy's, or nobody's yet.</summary>
    internal static string PointOwner(int number, MatchSide? owner, MatchSide own)
    {
        var template = owner == null ? "{=TAOM_FOW_Hud_PointNeutral}Point {POINT}: neutral"
            : owner == own ? "{=TAOM_FOW_Hud_PointOwn}Point {POINT}: yours"
            : "{=TAOM_FOW_Hud_PointEnemy}Point {POINT}: enemy";
        return new TextObject(template).SetTextVariable("POINT", number).ToString();
    }

    internal static string PointTaking(bool byYou, int percent) =>
        new TextObject(byYou ? "{=TAOM_FOW_Hud_PointYouTaking}You are taking it: {PERCENT}%" : "{=TAOM_FOW_Hud_PointEnemyTaking}The enemy is taking it: {PERCENT}%")
            .SetTextVariable("PERCENT", percent).ToString();

    /// <summary>A capture that is decaying because nobody, or the point's owner, stands on the point.</summary>
    internal static string PointSlipping(bool byYou, int percent) =>
        new TextObject(byYou ? "{=TAOM_FOW_Hud_PointYouSlipping}Your capture is slipping back: {PERCENT}%" : "{=TAOM_FOW_Hud_PointEnemySlipping}The enemy capture is slipping back: {PERCENT}%")
            .SetTextVariable("PERCENT", percent).ToString();

    internal static string PointContested() => new TextObject("{=TAOM_FOW_Hud_PointContested}Contested").ToString();

    internal static string KeysHint() =>
        new TextObject("{=TAOM_FOW_Hud_KeysHint}1-4 lane   F1-F8 troops   H hero   Z Push   X Hold   C Fall back   WASD pan   Q E turn   wheel zoom").ToString();

    internal static string PackSize(int size) => new TextObject("{=TAOM_FOW_Hud_PackSize}Pack of {SIZE}").SetTextVariable("SIZE", size).ToString();

    internal static string Price(int price) => new TextObject("{=TAOM_FOW_Hud_Price}{PRICE} gold").SetTextVariable("PRICE", price).ToString();

    internal static string HeroReady() => new TextObject("{=TAOM_FOW_Hud_HeroReady}Ready to field").ToString();

    internal static string HeroFielded() => new TextObject("{=TAOM_FOW_Hud_HeroFielded}In the field").ToString();

    internal static string HeroFallen(int seconds) =>
        new TextObject("{=TAOM_FOW_Hud_HeroFallen}Fallen, back in {SECONDS} s").SetTextVariable("SECONDS", seconds).ToString();

    internal static string HeroField(int price) =>
        new TextObject("{=TAOM_FOW_Hud_HeroField}Field for {PRICE} gold").SetTextVariable("PRICE", price).ToString();

    internal static string HeroBuyback(int price) =>
        new TextObject("{=TAOM_FOW_Hud_HeroBuyback}Buy back for {PRICE} gold").SetTextVariable("PRICE", price).ToString();

    internal static string Cooldown(int seconds) =>
        new TextObject("{=TAOM_FOW_Hud_Cooldown}{SECONDS} s").SetTextVariable("SECONDS", seconds).ToString();

    internal static string OwnKeep() => new TextObject("{=TAOM_FOW_Hud_KeepOwn}Your keep").ToString();

    internal static string EnemyKeep() => new TextObject("{=TAOM_FOW_Hud_KeepEnemy}Enemy keep").ToString();

    internal static string Order(LaneOrder order)
    {
        switch (order)
        {
            case LaneOrder.Hold:
                return new TextObject("{=TAOM_FOW_Order_Hold}Hold").ToString();
            case LaneOrder.FallBack:
                return new TextObject("{=TAOM_FOW_Order_FallBack}Fall back").ToString();
            default:
                return new TextObject("{=TAOM_FOW_Order_Push}Push").ToString();
        }
    }

    /// <summary>The refusal line for a command the match turned down; empty for Ok.</summary>
    internal static string Refusal(CommandResult result)
    {
        switch (result)
        {
            case CommandResult.Ok:
                return string.Empty;
            case CommandResult.NotEnoughGold:
                return new TextObject("{=TAOM_FOW_Notice_NotEnoughGold}Not enough gold.").ToString();
            case CommandResult.QueueFull:
                return new TextObject("{=TAOM_FOW_Notice_QueueFull}That lane already has packs waiting.").ToString();
            case CommandResult.HeroLimit:
                return new TextObject("{=TAOM_FOW_Notice_HeroLimit}You already field as many heroes as you may.").ToString();
            case CommandResult.HeroAlreadyFielded:
                return new TextObject("{=TAOM_FOW_Notice_HeroFielded}That hero is already in the field.").ToString();
            case CommandResult.HeroNotDead:
                return new TextObject("{=TAOM_FOW_Notice_HeroNotFallen}That hero has not fallen.").ToString();
            case CommandResult.OnCooldown:
                return new TextObject("{=TAOM_FOW_Notice_OnCooldown}That order is still cooling down.").ToString();
            default:
                return new TextObject("{=TAOM_FOW_Notice_Unavailable}That command cannot be given now.").ToString();
        }
    }

    internal static string NoHeroAffordable() =>
        new TextObject("{=TAOM_FOW_Notice_NoHero}No hero you can afford is waiting.").ToString();

    internal static string ResultTitle(bool won) =>
        won ? new TextObject("{=TAOM_FOW_Result_Victory}Victory!").ToString() : new TextObject("{=TAOM_FOW_Result_Defeat}Defeat").ToString();

    internal static string ResultText(bool won) =>
        won ? new TextObject("{=TAOM_FOW_Result_VictoryText}The enemy keep has fallen.").ToString()
            : new TextObject("{=TAOM_FOW_Result_DefeatText}Your keep has fallen.").ToString();

    internal static string ResultButton() => new TextObject("{=TAOM_FOW_Result_Button}Return to the main menu").ToString();

    /// <summary>The feed line for an event, or null when the panel does not list it (the enemy's pack purchases).</summary>
    internal static string? Feed(MatchEvent e, MatchSide own, string subjectName)
    {
        var mine = e.Side == own;
        switch (e.Kind)
        {
            case MatchEventKind.TowerDestroyed:
                return mine ? new TextObject("{=TAOM_FOW_Feed_OwnTower}We lost a tower.").ToString()
                            : new TextObject("{=TAOM_FOW_Feed_EnemyTower}An enemy tower has fallen.").ToString();
            case MatchEventKind.KeepDestroyed:
                return mine ? new TextObject("{=TAOM_FOW_Feed_OwnKeep}Our keep has fallen!").ToString()
                            : new TextObject("{=TAOM_FOW_Feed_EnemyKeep}The enemy keep has fallen!").ToString();
            case MatchEventKind.HeroFielded:
                return Named(mine ? "{=TAOM_FOW_Feed_OwnHeroFielded}{HERO} joins the army." : "{=TAOM_FOW_Feed_EnemyHeroFielded}The enemy fields {HERO}.", subjectName);
            case MatchEventKind.HeroDied:
                return Named(mine ? "{=TAOM_FOW_Feed_OwnHeroDied}{HERO} has fallen." : "{=TAOM_FOW_Feed_EnemyHeroDied}{HERO} of the enemy has fallen.", subjectName);
            case MatchEventKind.HeroRespawned:
                return Named(mine ? "{=TAOM_FOW_Feed_OwnHeroBack}{HERO} returns to the field." : "{=TAOM_FOW_Feed_EnemyHeroBack}{HERO} returns to the enemy ranks.", subjectName);
            case MatchEventKind.PointCaptured:
                return mine ? new TextObject("{=TAOM_FOW_Feed_OwnPoint}We hold a capture point.").ToString()
                            : new TextObject("{=TAOM_FOW_Feed_EnemyPoint}The enemy holds a capture point.").ToString();
            case MatchEventKind.CampCleared:
                return mine ? new TextObject("{=TAOM_FOW_Feed_OwnCamp}We cleared a camp.").ToString()
                            : new TextObject("{=TAOM_FOW_Feed_EnemyCamp}The enemy cleared a camp.").ToString();
            case MatchEventKind.PackBought:
                return mine ? new TextObject("{=TAOM_FOW_Feed_PackBought}Reinforcements sent for {PRICE} gold.").SetTextVariable("PRICE", e.Amount).ToString() : null;
            default:
                return null;
        }
    }

    private static string Named(string template, string name) => new TextObject(template).SetTextVariable("HERO", name).ToString();
}
