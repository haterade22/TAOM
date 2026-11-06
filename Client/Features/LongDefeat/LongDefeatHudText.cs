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
        if (view.Phase == RunPhase.Wave && view.Volleys.Owned)
            return hint + "   " + new TextObject("{=TAOM_LD_Hud_HintVolley}N  Loose the Elven Volley").ToString();

        return view.ShopOpen && view.BoonOffer.Count > 0
            ? hint + "   " + new TextObject("{=TAOM_LD_Hud_BoonAwaits}A boon awaits: press B").ToString()
            : hint;
    }

    /// <summary>How long a withheld volley's reason stays on the strip, in wave seconds.</summary>
    internal const float WithheldShownSeconds = 3f;

    /// <summary>
    /// The HUD strip's volley line, during a wave only and only once the Elven Volley is owned (phase3-plan.md Slice 6): why the last shot could not leave (for three seconds), else ready, else the seconds to
    /// the next use. No attacker in reach is its own line; no owner, no origin and no arrow item are one plain line.
    /// </summary>
    internal static string VolleyLine(RunView view)
    {
        var volleys = view.Volleys;
        if (view.Phase != RunPhase.Wave || !volleys.Owned)
            return string.Empty;

        if (volleys.Withheld is { } why && volleys.WithheldSecondsAgo < WithheldShownSeconds)
        {
            return why == VolleyWithheldReason.NoTargets
                ? new TextObject("{=TAOM_LD_Hud_VolleyNoTargets}No attackers in reach").ToString()
                : new TextObject("{=TAOM_LD_Hud_VolleyWithheld}The volley cannot be loosed now").ToString();
        }

        return volleys.SecondsLeft > 0f
            ? new TextObject("{=TAOM_LD_Hud_VolleyIn}Elven Volley in {SECONDS} s").SetTextVariable("SECONDS", WholeSeconds(volleys.SecondsLeft)).ToString()
            : new TextObject("{=TAOM_LD_Hud_VolleyReady}Elven Volley ready").ToString();
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

    /// <summary>
    /// The HUD strip's commander line, during a wave only: his rank and the ability that is running or due soonest ("Captain: The White Hand Calls in 12 s"), or that he has fallen. Nothing before he
    /// spawns, and nothing once he was felled or left, since the HUD stops showing him then.
    /// </summary>
    internal static string CommanderLine(RunView view)
    {
        if (view.Phase != RunPhase.Wave || view.Commander is not { } commander || !commander.Up)
            return string.Empty;

        var rank = OfferName(commander.RankName);
        switch (commander.Outcome)
        {
            case CommanderOutcome.Killed:
                return new TextObject("{=TAOM_LD_Hud_CommanderFallen}{RANK} has fallen").SetTextVariable("RANK", rank).ToString();
            case CommanderOutcome.Standing:
                var running = commander.Abilities.FirstOrDefault(a => a.Running);
                if (running != null)
                    return new TextObject("{=TAOM_LD_Hud_CommanderActive}{RANK}: {ABILITY}, now").SetTextVariable("RANK", rank).SetTextVariable("ABILITY", OfferName(running.Name)).ToString();

                var next = commander.Abilities.OrderBy(a => a.SecondsLeft).FirstOrDefault();
                return next == null
                    ? rank
                    : new TextObject("{=TAOM_LD_Hud_CommanderNext}{RANK}: {ABILITY} in {SECONDS} s").SetTextVariable("RANK", rank)
                        .SetTextVariable("ABILITY", OfferName(next.Name)).SetTextVariable("SECONDS", WholeSeconds(next.SecondsLeft)).ToString();
            default:
                return string.Empty;
        }
    }

    /// <summary>Seconds rounded up, so a countdown never reads 0 while the ability is still due. Not a positive finite number reads 0.</summary>
    private static int WholeSeconds(float seconds) => seconds > 0f && !float.IsInfinity(seconds) ? (int)Math.Ceiling(Math.Min(seconds, 359999f)) : 0;

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

    internal static string FortTitle() => new TextObject("{=TAOM_LD_Fort_Title}Fortifications").ToString();

    internal static string CareName() => new TextObject("{=TAOM_LD_Fort_Care}Field care").ToString();

    /// <summary>The care row's line: the share of the missing health it gives back (shop.json's, as a whole percent).</summary>
    internal static string CareDetail(float share) => new TextObject("{=TAOM_LD_Fort_CareDetail}Every defender, the lord too, regains {SHARE}% of the health he is missing. Once between waves.")
        .SetTextVariable("SHARE", share > 0f && !float.IsInfinity(share) ? (int)Math.Round(Math.Min(share, 1f) * 100f) : 0).ToString();

    internal static string CareUsed() => new TextObject("{=TAOM_LD_Fort_Used}Used").ToString();

    internal static string GateName() => new TextObject("{=TAOM_LD_Fort_Gate}Reinforce the gate").ToString();

    /// <summary>The gate row's line: the level the next buy brings (the top one when all are owned) and the hit points that level gives, as a whole percent over a new gate's.</summary>
    internal static string GateDetail(int level, int maxLevel, float percent) => new TextObject("{=TAOM_LD_Fort_GateDetail}Level {LEVEL} of {MAX}: the gate holds {PERCENT}% more than a new one. A hurt gate stays as hurt.")
        .SetTextVariable("LEVEL", level).SetTextVariable("MAX", maxLevel)
        .SetTextVariable("PERCENT", percent > 0f && !float.IsInfinity(percent) ? (int)Math.Round(percent) : 0).ToString();

    internal static string GateFull() => new TextObject("{=TAOM_LD_Fort_GateFull}Fully reinforced").ToString();

    internal static string RepairName() => new TextObject("{=TAOM_LD_Fort_Repair}Repair the gate").ToString();

    internal static string RepairDetail() => new TextObject("{=TAOM_LD_Fort_RepairDetail}Mends a broken or hurt gate and shuts it again. The price follows how much of the gate is lost.").ToString();

    internal static string RepairNone() => new TextObject("{=TAOM_LD_Fort_RepairNone}Nothing to repair").ToString();

    /// <summary>The refusal line for a gate or care buy the run turned down; empty for Ok. <c>MaxLevel</c> reads as the gate's, which the gate row's buy answers.</summary>
    internal static string Refusal(FortResult result) => Refusal(result, new TextObject("{=TAOM_LD_Notice_GateFull}The gate is fully reinforced.").ToString());

    /// <summary>The Elven Volley's refusal: the generic lines, and its own wording for a level it does not have (the tower row, Slice 7, passes its own).</summary>
    internal static string RefusalVolley(FortResult result) => Refusal(result, new TextObject("{=TAOM_LD_Notice_VolleyFull}The Elven Volley is fully upgraded.").ToString());

    /// <summary>The refusal line for a fortification the run turned down; empty for Ok. <paramref name="maxLevel"/> is the row's own line for <c>MaxLevel</c>, which the gate, the towers and the volley each answer.</summary>
    private static string Refusal(FortResult result, string maxLevel)
    {
        switch (result)
        {
            case FortResult.Ok:
                return string.Empty;
            case FortResult.NotOpen:
                return new TextObject("{=TAOM_LD_Notice_ShopClosed}The shop is closed during a wave.").ToString();
            case FortResult.NoGold:
                return new TextObject("{=TAOM_LD_Notice_NoGold}Not enough gold.").ToString();
            case FortResult.GarrisonFull:
                return new TextObject("{=TAOM_LD_Notice_GarrisonFull}The garrison is full.").ToString();
            case FortResult.Used:
                return new TextObject("{=TAOM_LD_Notice_CareUsed}Field care is already used between these waves.").ToString();
            case FortResult.MaxLevel:
                return maxLevel;
            case FortResult.NothingToRepair:
                return new TextObject("{=TAOM_LD_Notice_GateWhole}The gate needs no repair.").ToString();
            case FortResult.RunOver:
                return new TextObject("{=TAOM_LD_Notice_RunOver}The run is over.").ToString();
            default:
                return new TextObject("{=TAOM_LD_Notice_FortUnavailable}That is not for sale.").ToString();
        }
    }

    /// <summary>The refusal line for a horn the run turned down; empty for Ok.</summary>
    internal static string VolleyName() => new TextObject("{=TAOM_LD_Fort_Volley}Elven Volley").ToString();

    /// <summary>The volley row's line: the level the next buy brings (the top one when all are owned), its arrows and cooldown, and who aims it.</summary>
    internal static string VolleyDetail(int level, int maxLevel, VolleyLevelPlan? shown)
    {
        var aimed = shown?.Aim == VolleyAimMode.Crosshair;
        var text = aimed
            ? new TextObject("{=TAOM_LD_Fort_VolleyCrosshair}Level {LEVEL} of {MAX}: {ARROWS} arrows every {SECONDS} s, each at an attacker near where you look. Press N during a wave.")
            : new TextObject("{=TAOM_LD_Fort_VolleyAuto}Level {LEVEL} of {MAX}: {ARROWS} arrows every {SECONDS} s, each at an attacker in the densest group near the walls. Press N during a wave.");
        return text.SetTextVariable("LEVEL", level).SetTextVariable("MAX", maxLevel).SetTextVariable("ARROWS", shown?.Arrows ?? 0)
            .SetTextVariable("SECONDS", WholeSeconds(shown?.CooldownSeconds ?? 0f)).ToString();
    }

    internal static string VolleyFull() => new TextObject("{=TAOM_LD_Fort_VolleyFull}Fully upgraded").ToString();

    /// <summary>The refusal line for the key N; empty when the volley is armed (it looses on the next tick) or is not sold here (no shop row exists to send the player to).</summary>
    internal static string Refusal(VolleyArm result)
    {
        switch (result)
        {
            case VolleyArm.Armed:
            case VolleyArm.NotSold:
                return string.Empty;
            case VolleyArm.NotOwned:
                return new TextObject("{=TAOM_LD_Notice_VolleyNotOwned}Buy the Elven Volley between waves first.").ToString();
            case VolleyArm.Cooldown:
                return new TextObject("{=TAOM_LD_Notice_VolleyCooldown}The Elven Volley is not ready yet.").ToString();
            case VolleyArm.NotInWave:
                return new TextObject("{=TAOM_LD_Notice_VolleyNotInWave}The Elven Volley is loosed during a wave.").ToString();
            default:
                return new TextObject("{=TAOM_LD_Notice_RunOver}The run is over.").ToString();
        }
    }

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
