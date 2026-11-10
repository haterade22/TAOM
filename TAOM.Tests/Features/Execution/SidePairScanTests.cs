using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.Execution;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Execution;

/// <summary>
/// #957: a side is resolved from the id and the culture of ONE entity. The pair is built from engine objects in
/// exactly one place (<c>FactionSides</c>) and carried whole as <c>SideIds</c>, so no feature can zip a kingdom
/// with another entity's culture. This scan reads the IL of TAOM.dll, so it sees what the compiler emitted:
/// a target-typed <c>new(</c> in any position (an argument, a return), an object initializer, a <c>with</c>
/// expression, a call inside a lambda or an iterator. Callers are keyed by their OUTERMOST type, because a
/// lambda or an iterator body lives in a compiler-generated nested type. A new two-argument
/// <c>ResolveSide</c> call or a <c>SideIds</c> built elsewhere fails here until it is moved onto the pair or
/// listed with the reason it is correct, and an entry nothing calls any more fails too.
/// </summary>
[TestClass]
public class SidePairScanTests
{
    private static bool _gameLoaded;

    /// <summary>
    /// Types that may call <c>ResolveSide(id, culture)</c> with two arguments (on the interface or on the concrete
    /// service), and why each pair is the id and the culture of one entity.
    /// </summary>
    private static readonly Dictionary<string, string> TwoArgumentCallers = new(StringComparer.Ordinal)
    {
        ["TAOM.Features.Execution.AlignmentServiceExtensions"] = "the extension that resolves a SideIds pair",
        ["TAOM.Features.CaravanTrade.CaravanTradeService"] = "each side is a faction's id with that faction's own culture (the CanTradeWith patch passes IFaction.StringId and IFaction.Culture)",
        ["TAOM.Features.FireAndTribute.FireAndTributeService"] = "a kingdom snapshot's id with the culture of that same kingdom (IKingdomWarSnapshotAdapter reads both from one Kingdom), and in IsOwnSide one MobileParty.MapFaction's StringId with that same faction's Culture (zipped in TaomMobilePartyAIModel.IsSpared; FactionSides.Of would allocate per call on the AI worker), and in HeldByOtherSide and DestroyerText one map faction's StringId with that faction's Culture, read together in RaidHostAdapter.TryGetAnchorOwner and FireAndTributeBehavior.ReadDestroyer",
        ["TAOM.Features.EyeOfSauron.EyeOfSauronService"] = "the player's kingdom with that kingdom's culture (PlayerKingdomCultureId)",
        ["TAOM.Features.Namegiving.NamegivingService"] = "the map faction id with the culture the snapshot took from the same faction",
        ["TAOM.Features.RealmBorders.RealmBorderService"] = "a realm id with CultureOfRealm, the culture of that same realm",
        ["TAOM.Features.WarOfTheRingMomentum.MomentumEnrollmentService"] = "a kingdom's id with GetKingdomCultureId of that same kingdom",
        ["TAOM.Features.WarChronicle.Events.ChronicleOutcomeService"] = "a kingdom id with GetKingdomCultureId of that kingdom",
        ["TAOM.Features.WarChronicle.Ledger.WarLedgerService"] = "a kingdom row's id with the culture of that same kingdom",
        ["TAOM.Features.WarChronicle.Rally.RallyService"] = "a kingdom row's id with the culture of that same kingdom",
        ["TAOM.Features.WarPledge.WarPledgeService"] = "a kingdom's id with GetKingdomCultureId of that same kingdom (CanJoin: a realm sided through its culture cannot pledge)",
    };

    /// <summary>The types that may construct or copy a <c>SideIds</c>: the one engine-to-pair adapter and the record itself.</summary>
    private static readonly Dictionary<string, string> SideIdsBuilders = new(StringComparer.Ordinal)
    {
        ["TAOM.Adapters.FactionSides"] = "builds the pair from an engine faction, hero, character or the player",
        ["TAOM.Features.Execution.SideIds"] = "the record: its static None and its compiler-made copy",
    };

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static bool IsTwoArgumentResolveSide(MethodBase m)
        => m.Name == "ResolveSide"
           && (m.DeclaringType == typeof(IAlignmentService) || m.DeclaringType == typeof(AlignmentService))
           && m.GetParameters().Length == 2;

    private static bool IsSideIdsBuild(MethodBase m)
        => m.DeclaringType == typeof(SideIds) && (m.IsConstructor || m.Name == "<Clone>$");

    private static string Outermost(Type? type)
    {
        while (type?.DeclaringType != null)
            type = type.DeclaringType;
        return type?.FullName ?? "?";
    }

    /// <summary>The outermost type of every method in the assemblies whose IL calls a method <paramref name="isBanned"/> accepts.</summary>
    private static SortedSet<string> OutermostCallers(IEnumerable<Assembly> assemblies, Func<MethodBase, bool> isBanned,
        out List<string> unreadable, out int scanned)
    {
        var callers = new SortedSet<string>(StringComparer.Ordinal);
        unreadable = new List<string>();
        scanned = 0;
        foreach (var method in assemblies.SelectMany(IlCallScanner.EnumerateMethods))
        {
            byte[]? il;
            try
            {
                il = method.GetMethodBody()?.GetILAsByteArray();
            }
            catch (Exception ex)
            {
                unreadable.Add($"{method.DeclaringType?.FullName}.{method.Name}: {ex.GetType().Name}");
                continue;
            }
            if (il == null)
                continue;
            scanned++;
            if (IlCallScanner.ExtractCalledMethods(method, il).Any(isBanned))
                callers.Add(Outermost(method.DeclaringType));
        }

        return callers;
    }

    private static void AssertEquivalent(SortedSet<string> observed, ICollection<string> allowed, List<string> unreadable,
        string what, string fix)
    {
        var violations = observed.Where(c => !allowed.Contains(c)).ToList();
        var stale = allowed.Where(a => !observed.Contains(a)).ToList();
        if (violations.Count == 0 && stale.Count == 0 && unreadable.Count == 0)
            return;

        var sb = new StringBuilder();
        if (violations.Count > 0)
            sb.AppendLine($"{what}, outside the allow-list: {string.Join(", ", violations)}. {fix}");
        if (stale.Count > 0)
            sb.AppendLine($"These allow-list entries no longer {what}; remove them: {string.Join(", ", stale)}");
        if (unreadable.Count > 0)
            sb.AppendLine($"{unreadable.Count} method bodies could not be read, so the scan cannot vouch for them: {string.Join("; ", unreadable.Take(10))}");
        Assert.Fail(sb.ToString());
    }

    private static void RequireScan(int scanned)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        if (scanned < 1000)
            Assert.Inconclusive($"Only {scanned} method bodies scanned (expected thousands): an assembly enumeration problem, not a pass.");
    }

    [TestMethod]
    public void TwoArgumentResolveSide_IsCalledOnlyByTheListedTypes()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var observed = OutermostCallers(TaomAssemblies.All, IsTwoArgumentResolveSide, out var unreadable, out var scanned);
        RequireScan(scanned);

        AssertEquivalent(observed, TwoArgumentCallers.Keys, unreadable, "call ResolveSide(id, culture) with two arguments",
            "Build the pair with FactionSides, carry it as SideIds and resolve it with the extension, or list the type " +
            "in TwoArgumentCallers with the reason its pair is one entity's.");
    }

    [TestMethod]
    public void SideIds_AreBuiltOnlyByFactionSidesAndTheRecord()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var observed = OutermostCallers(TaomAssemblies.All, IsSideIdsBuild, out var unreadable, out var scanned);
        RequireScan(scanned);

        AssertEquivalent(observed, SideIdsBuilders.Keys, unreadable, "construct or copy a SideIds",
            "A SideIds is built from engine objects in FactionSides only, so a kingdom is never zipped with another entity's culture.");
    }

    [TestMethod]
    public void TheScan_SeesEveryShapeItGuards()
    {
        // Guards the guard: the probes below are compiled into this assembly, so the same scan must find each one.
        // If the walker stopped matching a shape, the two tests above would pass vacuously.
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var builds = OutermostCallers(new[] { typeof(SidePairScanTests).Assembly }, IsSideIdsBuild, out _, out var scanned);
        RequireScan(scanned);
        foreach (var probe in new[]
        {
            nameof(SidePairProbeNew), nameof(SidePairProbeTargetTypedArgument), nameof(SidePairProbeTargetTypedLocal),
            nameof(SidePairProbeWith), nameof(SidePairProbeInitializer),
        })
            CollectionAssert.Contains(builds.ToList(), "TAOM.Tests.Features.Execution." + probe, "The SideIds scan missed " + probe);

        var calls = OutermostCallers(new[] { typeof(SidePairScanTests).Assembly }, IsTwoArgumentResolveSide, out _, out _);
        foreach (var probe in new[]
        {
            nameof(SidePairProbeInterfaceCall), nameof(SidePairProbeConcreteCall), nameof(SidePairProbeLambdaCall),
            nameof(SidePairProbeIteratorCall),
        })
            CollectionAssert.Contains(calls.ToList(), "TAOM.Tests.Features.Execution." + probe, "The ResolveSide scan missed " + probe);

        CollectionAssert.DoesNotContain(calls.ToList(), "TAOM.Tests.Features.Execution." + nameof(SidePairProbeExtensionCall),
            "The extension on a SideIds is the sanctioned call, not a two-argument one");
        CollectionAssert.DoesNotContain(builds.ToList(), "TAOM.Tests.Features.Execution." + nameof(SidePairProbeExtensionCall));
    }
}

internal static class SidePairProbeNew
{
    internal static SideIds Build(string a, string b) => new SideIds(a, b);
}

internal static class SidePairProbeTargetTypedArgument
{
    private static void Use(SideIds side) => GC.KeepAlive(side);

    internal static void Build(string a, string b) => Use(new(a, b));
}

internal static class SidePairProbeTargetTypedLocal
{
    internal static SideIds Build(string a, string b)
    {
        SideIds side = new(a, b);
        return side;
    }
}

internal static class SidePairProbeWith
{
    internal static SideIds Copy(SideIds side) => side with { CultureId = string.Empty };
}

internal static class SidePairProbeInitializer
{
    internal sealed class Holder
    {
        public SideIds Side { get; } = new("", "");
    }
}

internal static class SidePairProbeInterfaceCall
{
    internal static FactionSide Resolve(IAlignmentService alignment) => alignment.ResolveSide("k", "c");
}

internal static class SidePairProbeConcreteCall
{
    internal static FactionSide Resolve(AlignmentService alignment) => alignment.ResolveSide("k", "c");
}

internal static class SidePairProbeLambdaCall
{
    internal static Func<FactionSide> Resolve(IAlignmentService alignment, string kingdom) => () => alignment.ResolveSide(kingdom, "c");
}

internal static class SidePairProbeIteratorCall
{
    internal static IEnumerable<FactionSide> Resolve(IAlignmentService alignment)
    {
        yield return alignment.ResolveSide("k", "c");
    }
}

internal static class SidePairProbeExtensionCall
{
    internal static FactionSide Resolve(IAlignmentService alignment, SideIds side) => alignment.ResolveSide(side);
}
