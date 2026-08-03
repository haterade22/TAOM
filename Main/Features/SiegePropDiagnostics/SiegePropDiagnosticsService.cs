using System.Collections.Generic;
using System.Globalization;
using TAOM.Features.SiegePropDiagnostics.Models;

namespace TAOM.Features.SiegePropDiagnostics;

/// <summary>
/// Classifies why an in-mission resupply prop is unusable, and renders the scene's props as log
/// lines. Pure logic over primitive snapshots — no engine types, no logging, fully unit-tested.
///
/// The engine reports none of these faults: a prop with no usable standing point simply keeps its
/// focus on the machine root, and <c>StonePile.GetDescriptionText</c> returns null for anything not
/// tagged <c>ammopickup</c>, so the player gets a blank prompt and a dead key with nothing written
/// anywhere. This class exists to turn that silence into one line per prop.
/// </summary>
public class SiegePropDiagnosticsService : ISiegePropDiagnosticsService
{
    /// <summary>
    /// <c>UsableMachine.GetValidVacantReachableStandingPointForAgent</c> requires the point's
    /// resolved ground height to be within this of the agent's.
    /// </summary>
    private const float GroundHeightLimit = 1.5f;

    private readonly ISiegePropDiagnosticsSettingsProvider _settings;

    public SiegePropDiagnosticsService(ISiegePropDiagnosticsSettingsProvider settings)
    {
        _settings = settings;
    }

    public SiegePropDiagnosis Diagnose(SiegePropSnapshot snapshot)
    {
        var isPile = snapshot.Kind == SiegePropKind.RockPile;

        // --- Scene-data faults. These outrank the probe: a pile with a null given item cannot
        // really be usable, whatever a single probe happened to return.
        if (snapshot.StandingPointCount <= 0)
            return SiegePropDiagnosis.NoStandingPoints;

        // Only StonePile needs ammopickup-tagged points; AmmoBarrelBase iterates every standing
        // point, and vanilla's arrow_barrel tags none of them.
        if (isPile && snapshot.AmmoPickupPointCount <= 0)
            return SiegePropDiagnosis.NoAmmoPickupPoints;

        // Barrels hand out no item, so GivenItemId is meaningless for them.
        if (isPile && !snapshot.GivenItemResolves)
            return SiegePropDiagnosis.ItemIdUnresolved;

        // --- Objective prop state.
        if (snapshot.MachineIsDisabled)
            return SiegePropDiagnosis.MachineDisabled;

        if (isPile && snapshot.AmmoCount <= 0)
            return SiegePropDiagnosis.AmmoExhausted;

        var relevantPoints = RelevantPointCount(snapshot);
        if (relevantPoints > 0 && snapshot.DeactivatedPointCount >= relevantPoints)
            return SiegePropDiagnosis.AllPointsDeactivated;

        // --- The engine's own verdict. Anything past here means the probe failed.
        if (snapshot.PlayerProbeValid)
            return SiegePropDiagnosis.Healthy;

        // Checked before the disabled-point count because being mounted is what disables them.
        if (snapshot.PlayerIsMounted)
            return SiegePropDiagnosis.PlayerMounted;

        if (relevantPoints > 0 && snapshot.DisabledForPlayerPointCount >= relevantPoints)
            return SiegePropDiagnosis.AllPointsDisabledForPlayer;

        if (relevantPoints > 0 && snapshot.OccupiedPointCount >= relevantPoints)
            return SiegePropDiagnosis.AllPointsOccupied;

        // Positive requirements, so a NaN from the engine fails the gate rather than passing it
        // (csharp-architecture.md, engine-float decision gates).
        if (IsFiniteAtLeast(snapshot.NearestGroundHeightDelta, GroundHeightLimit))
            return SiegePropDiagnosis.GroundHeightMismatch;

        if (IsFiniteGreaterThan(snapshot.NearestPointDistanceSquared, snapshot.InteractionDistanceSquared))
            return SiegePropDiagnosis.PlayerOutOfRange;

        return SiegePropDiagnosis.UnknownProbeFailure;
    }

    public IReadOnlyList<string> BuildReport(
        string sceneName, bool isSiegeBattle, IReadOnlyList<SiegePropSnapshot> snapshots)
    {
        var lines = new List<string>();
        if (!_settings.IsEnabled) return lines;

        lines.Add($"[SiegeProps] scene='{sceneName}' siege={isSiegeBattle} props={snapshots.Count}");

        if (snapshots.Count == 0)
        {
            lines.Add("[SiegeProps] this scene has no resupply props at all — "
                      + "any rock piles or barrels you can see are meshes with no script attached.");
            return lines;
        }

        int piles = 0, barrels = 0, usable = 0, faults = 0;

        foreach (var snapshot in snapshots)
        {
            if (snapshot.Kind == SiegePropKind.RockPile) piles++;
            else if (snapshot.Kind == SiegePropKind.AmmoBarrel) barrels++;

            var diagnosis = Diagnose(snapshot);
            if (diagnosis == SiegePropDiagnosis.Healthy) usable++;
            else faults++;

            if (_settings.IsVerbose || diagnosis != SiegePropDiagnosis.Healthy)
                lines.Add(Describe(snapshot, diagnosis));
        }

        // Deliberately avoids the word "Healthy" so a non-verbose run contains it only when a prop
        // really was reported as such.
        lines.Add($"[SiegeProps] summary rockPiles={piles} barrels={barrels} ok={usable} faults={faults}");
        return lines;
    }

    private static int RelevantPointCount(SiegePropSnapshot snapshot) =>
        snapshot.Kind == SiegePropKind.RockPile
            ? snapshot.AmmoPickupPointCount
            : snapshot.StandingPointCount;

    private static string Describe(SiegePropSnapshot s, SiegePropDiagnosis diagnosis)
    {
        var item = string.IsNullOrEmpty(s.GivenItemId)
            ? "-"
            : $"{s.GivenItemId}{(s.GivenItemResolves ? "" : " (UNRESOLVED)")}";

        return string.Format(
            CultureInfo.InvariantCulture,
            "[SiegeProps] #{0} {1} '{2}' -> {3} | item={4} ammo={5}/{6} pts={7} ammoPts={8} "
            + "deact={9} disabledForPlayer={10} occupied={11} probe={12} mounted={13} "
            + "distSq={14} reachSq={15} groundDz={16}",
            s.Id, s.ScriptType, s.EntityName, diagnosis, item,
            s.AmmoCount, s.StartingAmmoCount, s.StandingPointCount, s.AmmoPickupPointCount,
            s.DeactivatedPointCount, s.DisabledForPlayerPointCount, s.OccupiedPointCount,
            s.PlayerProbeValid, s.PlayerIsMounted,
            Format(s.NearestPointDistanceSquared), Format(s.InteractionDistanceSquared),
            Format(s.NearestGroundHeightDelta));
    }

    private static string Format(float? value) =>
        value.HasValue ? value.Value.ToString("F2", CultureInfo.InvariantCulture) : "n/a";

    private static bool IsFiniteAtLeast(float? value, float threshold) =>
        value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value)
        && value.Value >= threshold;

    private static bool IsFiniteGreaterThan(float? value, float? threshold)
    {
        if (!value.HasValue || !threshold.HasValue) return false;
        var v = value.Value;
        var t = threshold.Value;
        if (float.IsNaN(v) || float.IsInfinity(v) || float.IsNaN(t) || float.IsInfinity(t)) return false;
        return v > t;
    }
}
