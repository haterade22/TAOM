using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>One XML type's result in the live harness: both sides' time, what each side threw, and the comparison text when both merged.</summary>
internal sealed class LiveTypeOutcome
{
    public LiveTypeOutcome(string id, double engineMs, double fastMs, Exception? engineError = null,
                           Exception? fastError = null, string? difference = null)
    {
        Id = id;
        EngineMs = engineMs;
        FastMs = fastMs;
        EngineError = engineError;
        FastError = fastError;
        Difference = difference;
    }

    public string Id { get; }

    public double EngineMs { get; }

    public double FastMs { get; }

    /// <summary>What the engine's own <c>CreateMergedXmlFile</c> threw for this type; null when it merged.</summary>
    public Exception? EngineError { get; }

    /// <summary>What the fast path threw for this type; null when it merged.</summary>
    public Exception? FastError { get; }

    /// <summary>The first-difference report when both sides merged and the documents differ; null when they are identical.</summary>
    public string? Difference { get; }
}

/// <summary>
/// What the live harness (XmlMergeLiveEquivalenceTests) accepts, as pure rules so LiveGateRulesTests can fail each one
/// without the game (Codex review of plan 042, 2026-10-03). The harness fails the run on any message these return.
///
/// <para><b>Equivalence.</b> Both sides must merge every type and produce the same document. An engine exception on a
/// type of the live install is a failure, even when the fast path throws the same exception type: the game loads these
/// types without one, so either the harness's lists differ from the game's or the fast path diverges (when it merged
/// where the engine threw, it accepts input the engine rejects). The message and the table row say what each side did.
/// Exception parity is the job of the fixtures in XmlMergeEngineEquivalenceTests.</para>
///
/// <para><b>Inventory.</b> Every module the order names must exist, except FastMode in the default order (the v1.5.3
/// install has no FastMode folder). A module asked for through TAOM_XMLMERGE_MODULES that is absent is a failure, as is
/// a heavy type missing from the merged types.</para>
///
/// <para><b>Speed.</b> The plan's bar: the four heavy types' fast total is at most
/// <see cref="MaxHeavyFastShare"/> of their engine total, measured in one process on one host, engine first for each
/// type after a GC. The test host runs the engine path about twice as slowly as the offline prototype did and the fast
/// path at about the prototype's speed (cause not isolated), so the ratio here reads about half the prototype's: 16.5%
/// against 27 to 35%. The bar's margin is therefore about threefold on this host's ratio but about 1.4 to 1.9 times on
/// the prototype's terms, and whether to assert a tighter bar is open for the maintainer (feature doc, "Timing
/// policy"). Absolute engine times are printed, never asserted. On a module list far smaller than TAOM's the bar can
/// fail with nothing wrong, because there is little quadratic cost to save: read the table.</para>
/// </summary>
internal static class LiveGateRules
{
    /// <summary>The four types the speed bar covers (the plan's step 7a; the biggest merges of a campaign load).</summary>
    internal static readonly IReadOnlyList<string> HeavyTypes = new[] { "NPCCharacters", "Items", "EquipmentRosters", "GameText" };

    /// <summary>The plan's bar: the heavy types' fast total may be at most this share of their engine total.</summary>
    internal const double MaxHeavyFastShare = 0.5;

    /// <summary>The messages for requested modules the game folder does not have; empty when the inventory is complete.</summary>
    internal static IReadOnlyList<string> CheckModules(IReadOnlyList<string> missing, bool explicitOrder)
    {
        var absent = explicitOrder
            ? missing.ToList()
            : missing.Where(m => !LiveMergeListBuilder.OptionalDefaultModules.Contains(m, StringComparer.Ordinal)).ToList();
        if (absent.Count == 0)
            return Array.Empty<string>();

        string names = string.Join(", ", absent);
        return new[]
        {
            explicitOrder
                ? LiveMergeListBuilder.ModulesVariable + " names module(s) with no SubModule.xml under Modules: " + names
                : "the default module order names module(s) with no SubModule.xml under Modules: " + names,
        };
    }

    /// <summary>The messages for the per-type outcomes: equivalence, the heavy-type inventory and the speed bar.</summary>
    internal static IReadOnlyList<string> CheckTypes(IReadOnlyList<LiveTypeOutcome> outcomes)
    {
        var failures = new List<string>();
        foreach (var outcome in outcomes)
        {
            if (outcome.EngineError != null)
            {
                // The fast side is the deciding fact: "fast merged" means the fast path takes input the engine rejects,
                // which in the game loads a type where vanilla would throw.
                string fastSide = outcome.FastError == null
                    ? "merged, so it accepts what the engine rejected"
                    : "threw " + outcome.FastError.GetType().Name + ": " + outcome.FastError.Message;
                failures.Add(outcome.Id + ": the engine threw " + outcome.EngineError.GetType().Name + ": " +
                             outcome.EngineError.Message + "; the fast path " + fastSide + "; a type the game loads " +
                             "must merge, so the harness's lists differ from the game's, or the fast path diverges " +
                             "(exception parity is XmlMergeEngineEquivalenceTests' job)");
            }
            else if (outcome.FastError != null)
            {
                failures.Add(outcome.Id + ": the engine merged, the fast path threw " + outcome.FastError.GetType().Name +
                             ": " + outcome.FastError.Message);
            }
            else if (outcome.Difference != null)
            {
                failures.Add(outcome.Difference);
            }
        }

        foreach (string heavy in HeavyTypes.Where(id => outcomes.All(o => o.Id != id)))
        {
            failures.Add("heavy type " + heavy + " is not among the merged types: the module order or the game folder is " +
                         "wrong, so the speed bar does not cover it");
        }

        var heavyOutcomes = outcomes.Where(o => HeavyTypes.Contains(o.Id)).ToList();
        bool everyHeavyTypeMerged = heavyOutcomes.Count == HeavyTypes.Count &&
                                    heavyOutcomes.All(o => o.EngineError == null && o.FastError == null);
        if (everyHeavyTypeMerged)
        {
            var (engineMs, fastMs) = HeavyTotals(outcomes);
            if (fastMs > MaxHeavyFastShare * engineMs)
            {
                failures.Add(string.Format(CultureInfo.InvariantCulture,
                    "heavy types ({0}): the fast path took {1:0} ms against the engine's {2:0} ms, {3:0.0}%; " +
                    "the plan's bar is at most {4:0}%",
                    string.Join(", ", HeavyTypes), fastMs, engineMs, 100 * fastMs / engineMs, 100 * MaxHeavyFastShare));
            }
        }
        return failures;
    }

    /// <summary>Engine and fast milliseconds summed over the heavy types present in <paramref name="outcomes"/>.</summary>
    internal static (double EngineMs, double FastMs) HeavyTotals(IReadOnlyList<LiveTypeOutcome> outcomes)
    {
        var heavy = outcomes.Where(o => HeavyTypes.Contains(o.Id)).ToList();
        return (heavy.Sum(o => o.EngineMs), heavy.Sum(o => o.FastMs));
    }
}
