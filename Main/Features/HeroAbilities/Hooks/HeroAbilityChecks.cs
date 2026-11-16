using System;
using System.Globalization;
using TAOM.Core.Validation;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// The #1359 [Check] ids that tools/ingame_checks/1359.toml reads, and the verdict each gives. Pure: the mission logic,
/// the host and the caster read the engine, these turn the numbers into pass, fail or skip. A float gate is written as
/// the requirement, so a NaN fails it.
/// </summary>
internal static class HeroAbilityChecks
{
    internal const string GateId = "taom.hero-abilities.gate";
    internal const string FiredId = "taom.hero-abilities.fired";
    internal const string StatsId = "taom.hero-abilities.stats-refreshed";

    // A stat refresh that moved the swing speed by this share of what the buff wants took effect.
    private const float StatsTolerance = 0.01f;

    internal static CheckVerdict Gate(bool longDefeat, bool combat, bool config, int kits)
    {
        var detail = $"long_defeat={Bit(longDefeat)} combat={Bit(combat)} config={Bit(config)} kits={Num(kits)}";
        if (!combat)
            return new CheckVerdict(CheckResult.Fail, detail);
        return config && kits >= 1
            ? new CheckVerdict(CheckResult.Pass, detail)
            : new CheckVerdict(CheckResult.Skip, "reason=config-off " + detail);
    }

    // contactSeconds is the number of one-second passes in which the hero could decide and the kit's easiest enemy
    // trigger held by the host's own count (HeroAbilityService.TryContactRule), apart from the sensor's.
    internal static CheckVerdict Fired(string heroId, int fired, int contactSeconds, int allies, int shocked, bool owned)
    {
        var detail = $"hero={heroId} fired={Num(fired)} contact_s={Num(contactSeconds)} allies={Num(allies)} shocked={Num(shocked)} owned={Bit(owned)}";
        if (fired >= 1)
            return new CheckVerdict(CheckResult.Pass, detail);
        if (!owned)
            return new CheckVerdict(CheckResult.Skip, "reason=not-owned " + detail);
        return contactSeconds >= HeroAbilityService.ContactSecondsToJudge
            ? new CheckVerdict(CheckResult.Fail, detail)
            : new CheckVerdict(CheckResult.Skip, "reason=no-contact " + detail);
    }

    internal static CheckVerdict Stats(string abilityId, string troopId, float ratio, float want)
    {
        var detail = $"ability={abilityId} troop={troopId} ratio={F3(ratio)} want={F3(want)}";
        var ok = want > 0f && Math.Abs(ratio / want - 1f) <= StatsTolerance;
        return LongDefeatChecks.Verdict(ok, detail);
    }

    internal static CheckVerdict NoStatsSample(int grants) =>
        new(CheckResult.Skip, $"reason=no-swing-grant grants={Num(grants)}");

    // The swing speed factor the models should apply after the grant over the one before it.
    internal static float SwingRatio(RaceAbilityEffects? before, RaceAbilityEffects? after) =>
        RaceAbilityService.Times(1f, after?.SwingSpeedPercent ?? 0f) / RaceAbilityService.Times(1f, before?.SwingSpeedPercent ?? 0f);

    // A grant that does not move the swing speed proves nothing about the refresh.
    internal static bool IsStatsSample(float want) => FiniteFloatValidator.IsFinite(want) && Math.Abs(want - 1f) > 0.001f;

    private static string Bit(bool value) => value ? "1" : "0";

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string F3(float value) => value.ToString("F3", CultureInfo.InvariantCulture);
}
