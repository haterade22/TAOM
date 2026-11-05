using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Localization;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// Every player-facing line of the Long Defeat HUD and panel, each a whole localized sentence so a translator can reorder its
/// variables. The key is the text before the first closing brace; <c>LongDefeatHudStringsTests</c> pins that each one is registered in
/// <c>taom_module_strings.xml</c> and seeded in the 12 language files. Key names and the clock (B, 1:30) are not text.
/// </summary>
internal static class LongDefeatHudText
{
    internal static string Wave(RunPhase phase, int wave)
    {
        switch (phase)
        {
            case RunPhase.Preparation:
                return new TextObject("{=TAOM_LD_Hud_Preparation}Preparation").ToString();
            case RunPhase.Wave:
                return new TextObject("{=TAOM_LD_Hud_Wave}Wave {WAVE}").SetTextVariable("WAVE", wave).ToString();
            case RunPhase.Intermission:
                return new TextObject("{=TAOM_LD_Hud_WaveCleared}Wave {WAVE} cleared").SetTextVariable("WAVE", wave).ToString();
            default:
                return new TextObject("{=TAOM_LD_Hud_RunOver}The run is over").ToString();
        }
    }

    /// <summary>The wave line with the lord's class after it; the wave line alone when he plays none. <paramref name="rawClassName"/> is the class's <c>{=KEY}English</c> name.</summary>
    internal static string WithClass(string waveText, string rawClassName) => rawClassName.Length == 0
        ? waveText
        : new TextObject("{=TAOM_LD_Hud_WaveClass}{WAVE}, {CLASS}").SetTextVariable("WAVE", waveText).SetTextVariable("CLASS", OfferName(rawClassName)).ToString();

    internal static string Gold(int gold) => new TextObject("{=TAOM_LD_Hud_Gold}Gold: {GOLD}").SetTextVariable("GOLD", gold).ToString();

    internal static string Attackers(int count) => new TextObject("{=TAOM_LD_Hud_Attackers}Attackers: {COUNT}").SetTextVariable("COUNT", count).ToString();

    internal static string Defenders(int count) => new TextObject("{=TAOM_LD_Hud_Defenders}Defenders: {COUNT}").SetTextVariable("COUNT", count).ToString();

    /// <summary>The HUD strip's speed line. It names the key, as the hint line names B and H.</summary>
    internal static string Speed(int speed) => new TextObject("{=TAOM_LD_Hud_Speed}Speed: {SPEED}x   M  Next speed").SetTextVariable("SPEED", speed).ToString();

    internal static string SpeedTitle() => new TextObject("{=TAOM_LD_Hud_SpeedTitle}Game speed").ToString();

    internal static string SpeedButton(int speed) => new TextObject("{=TAOM_LD_Hud_SpeedButton}{SPEED}x").SetTextVariable("SPEED", speed).ToString();

    internal static string Keep() => new TextObject("{=TAOM_LD_Hud_Keep}The keep is under attack").ToString();

    internal static string Hint(RunPhase phase)
    {
        switch (phase)
        {
            case RunPhase.Preparation:
            case RunPhase.Intermission:
                return new TextObject("{=TAOM_LD_Hud_HintOpen}B  Shop   H  Sound the horn and start the wave early").ToString();
            case RunPhase.Wave:
                return new TextObject("{=TAOM_LD_Hud_HintWave}Hold the walls. The shop opens between waves.").ToString();
            default:
                return string.Empty;
        }
    }

    /// <summary>The hint line: the phase's own, and between waves a pointer to the open boon pick, since the panel never opens by itself for one.</summary>
    internal static string Hint(RunView view)
    {
        var hint = Hint(view.Phase);
        return view.ShopOpen && view.BoonOffer.Count > 0
            ? hint + "   " + new TextObject("{=TAOM_LD_Hud_BoonAwaits}A boon awaits: press B").ToString()
            : hint;
    }

    /// <summary>The HUD strip's affix line: the wave's affixes during a wave, the next wave's (with its attackers' raised health and damage) between waves.</summary>
    internal static string AffixLine(RunView view)
    {
        switch (view.Phase)
        {
            case RunPhase.Wave:
                return view.WaveAffixes.Count == 0
                    ? string.Empty
                    : new TextObject("{=TAOM_LD_Hud_Affixes}Affixes: {NAMES}").SetTextVariable("NAMES", AffixNames(view.WaveAffixes)).ToString();
            case RunPhase.Preparation:
            case RunPhase.Intermission:
                var parts = new List<string>();
                if (view.NextAffixes.Count > 0)
                    parts.Add(new TextObject("{=TAOM_LD_Hud_NextAffixes}Next wave: {NAMES}").SetTextVariable("NAMES", AffixNames(view.NextAffixes)).ToString());
                if (IsRaised(view.NextHealthFactor) || IsRaised(view.NextDamageFactor))
                    parts.Add(new TextObject("{=TAOM_LD_Hud_NextScale}Next wave attackers: health {HEALTH}%, damage {DAMAGE}%")
                        .SetTextVariable("HEALTH", Percent(view.NextHealthFactor)).SetTextVariable("DAMAGE", Percent(view.NextDamageFactor)).ToString());
                return string.Join("   ", parts);
            default:
                return string.Empty;
        }
    }

    private static string AffixNames(IReadOnlyList<AffixLabel> affixes) => string.Join(", ", affixes.Select(a => new TextObject(a.Name).ToString()));

    private static bool IsRaised(float factor) => factor > 1.0005f && !float.IsInfinity(factor);

    private static int Percent(float factor) => IsRaised(factor) ? (int)Math.Round(factor * 100f) : 100;

    internal static string PickTitle() => new TextObject("{=TAOM_LD_Hud_PickTitle}Choose a boon").ToString();

    internal static string Reroll(int banked) => new TextObject("{=TAOM_LD_Hud_Reroll}Reroll ({COUNT})").SetTextVariable("COUNT", banked).ToString();

    internal static string BoonOffered() => new TextObject("{=TAOM_LD_Notice_BoonOffered}A boon is on offer: press B.").ToString();

    internal static string Category(BoonCategory category)
    {
        switch (category)
        {
            case BoonCategory.Class:
                return new TextObject("{=TAOM_LD_Hud_CategoryClass}Class").ToString();
            case BoonCategory.Infantry:
                return new TextObject("{=TAOM_LD_Hud_CategoryInfantry}Infantry").ToString();
            case BoonCategory.Archers:
                return new TextObject("{=TAOM_LD_Hud_CategoryArchers}Archers").ToString();
            case BoonCategory.Cavalry:
                return new TextObject("{=TAOM_LD_Hud_CategoryCavalry}Cavalry").ToString();
            case BoonCategory.Fortress:
                return new TextObject("{=TAOM_LD_Hud_CategoryFortress}Fortress").ToString();
            case BoonCategory.WarChest:
                return new TextObject("{=TAOM_LD_Hud_CategoryWarChest}War chest").ToString();
            default:
                return new TextObject("{=TAOM_LD_Hud_CategoryLegend}Legend").ToString();
        }
    }

    /// <summary>The refusal line for a boon the run turned down; empty for Ok.</summary>
    internal static string Refusal(PickResult result)
    {
        switch (result)
        {
            case PickResult.Ok:
                return string.Empty;
            case PickResult.NotOpen:
                return new TextObject("{=TAOM_LD_Notice_BoonClosed}A boon is taken between waves.").ToString();
            case PickResult.NoOffer:
                return new TextObject("{=TAOM_LD_Notice_NoBoon}No boon awaits.").ToString();
            case PickResult.NotOffered:
                return new TextObject("{=TAOM_LD_Notice_BoonNotOffered}That boon is not on offer.").ToString();
            default:
                return new TextObject("{=TAOM_LD_Notice_RunOver}The run is over.").ToString();
        }
    }

    /// <summary>The refusal line for a reroll the run turned down; empty for Ok.</summary>
    internal static string Refusal(RerollResult result)
    {
        switch (result)
        {
            case RerollResult.Ok:
                return string.Empty;
            case RerollResult.NotOpen:
                return new TextObject("{=TAOM_LD_Notice_BoonClosed}A boon is taken between waves.").ToString();
            case RerollResult.NoOffer:
                return new TextObject("{=TAOM_LD_Notice_NoBoon}No boon awaits.").ToString();
            case RerollResult.NoReroll:
                return new TextObject("{=TAOM_LD_Notice_NoReroll}No reroll is banked.").ToString();
            default:
                return new TextObject("{=TAOM_LD_Notice_RunOver}The run is over.").ToString();
        }
    }

    internal static string OfferName(string configName) => new TextObject(configName).ToString();

    internal static string PackSize(int size) => new TextObject("{=TAOM_LD_Hud_PackSize}Pack of {SIZE}").SetTextVariable("SIZE", size).ToString();

    internal static string Price(int price) => new TextObject("{=TAOM_LD_Hud_Price}{PRICE} gold").SetTextVariable("PRICE", price).ToString();

    internal static string ShopTitle() => new TextObject("{=TAOM_LD_Hud_ShopTitle}Shop").ToString();

    internal static string Horn() => new TextObject("{=TAOM_LD_Hud_Horn}Sound the horn: start the wave now for bonus gold").ToString();

    internal static string Close() => new TextObject("{=TAOM_LD_Hud_Close}Close").ToString();

    internal static string Leave() => new TextObject("{=TAOM_LD_Hud_Leave}Return to the main menu").ToString();

    /// <summary>The refusal line for a purchase the run turned down; empty for Ok.</summary>
    internal static string Refusal(BuyResult result)
    {
        switch (result)
        {
            case BuyResult.Ok:
                return string.Empty;
            case BuyResult.NotOpen:
                return new TextObject("{=TAOM_LD_Notice_ShopClosed}The shop is closed during a wave.").ToString();
            case BuyResult.UnknownOffer:
                return new TextObject("{=TAOM_LD_Notice_UnknownOffer}That pack is not for sale.").ToString();
            case BuyResult.NoGold:
                return new TextObject("{=TAOM_LD_Notice_NoGold}Not enough gold.").ToString();
            case BuyResult.GarrisonFull:
                return new TextObject("{=TAOM_LD_Notice_GarrisonFull}The garrison is full.").ToString();
            default:
                return new TextObject("{=TAOM_LD_Notice_RunOver}The run is over.").ToString();
        }
    }

    /// <summary>The refusal line for a horn the run turned down; empty for Ok.</summary>
    internal static string Refusal(StartResult result)
    {
        switch (result)
        {
            case StartResult.Ok:
                return string.Empty;
            case StartResult.NotOpen:
                return new TextObject("{=TAOM_LD_Notice_HornClosed}The horn can only sound between waves.").ToString();
            default:
                return new TextObject("{=TAOM_LD_Notice_RunOver}The run is over.").ToString();
        }
    }

    internal static string EndTitle(RunEndReason reason)
    {
        switch (reason)
        {
            case RunEndReason.DefendersDead:
                return new TextObject("{=TAOM_LD_End_DefendersDead}The garrison has fallen").ToString();
            case RunEndReason.KeepLost:
                return new TextObject("{=TAOM_LD_End_KeepLost}The keep has fallen").ToString();
            case RunEndReason.Stalemate:
                return new TextObject("{=TAOM_LD_End_Stalemate}The siege has stalled").ToString();
            case RunEndReason.TargetReached:
                return new TextObject("{=TAOM_LD_End_TargetReached}The fortress held").ToString();
            default:
                return new TextObject("{=TAOM_LD_End_Abandoned}You left the fortress").ToString();
        }
    }

    internal static string EndLines(int wavesCleared, int kills, int lostInClearedWaves, IReadOnlyList<BoonCard>? boons = null, string rawClassName = "") =>
        (rawClassName.Length == 0
            ? string.Empty
            : new TextObject("{=TAOM_LD_End_Class}Class: {CLASS}").SetTextVariable("CLASS", OfferName(rawClassName)).ToString() + "\n") +
        new TextObject("{=TAOM_LD_End_Waves}Waves cleared: {WAVES}").SetTextVariable("WAVES", wavesCleared).ToString() + "\n" +
        new TextObject("{=TAOM_LD_End_Kills}Kills: {KILLS}").SetTextVariable("KILLS", kills).ToString() + "\n" +
        new TextObject("{=TAOM_LD_End_Lost}Defenders lost in cleared waves: {LOST}").SetTextVariable("LOST", lostInClearedWaves).ToString() +
        (boons == null || boons.Count == 0
            ? string.Empty
            : "\n" + new TextObject("{=TAOM_LD_End_Boons}Boons: {NAMES}").SetTextVariable("NAMES", string.Join(", ", boons.Select(b => OfferName(b.Name)))).ToString());

    internal static string ReportFile(string fileName) =>
        new TextObject("{=TAOM_LD_End_Report}Report file, in bin/Win64_Shipping_Client/Logs:").ToString() + "\n" + fileName;

    internal static string Best(int waves, int kills) =>
        new TextObject("{=TAOM_LD_End_Best}Best run: waves cleared {WAVES}, kills {KILLS}").SetTextVariable("WAVES", waves).SetTextVariable("KILLS", kills).ToString();

    internal static string NoBest() => new TextObject("{=TAOM_LD_End_NoBest}No best run yet").ToString();

    /// <summary>Minutes and whole seconds left, rounded up. A timer that is not a positive finite number reads 0:00 (a NaN must not reach the cast).</summary>
    internal static string Clock(float seconds)
    {
        var whole = seconds > 0f && !float.IsInfinity(seconds) ? (int)Math.Ceiling(Math.Min(seconds, 359999f)) : 0;
        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", whole / 60, whole % 60);
    }
}
