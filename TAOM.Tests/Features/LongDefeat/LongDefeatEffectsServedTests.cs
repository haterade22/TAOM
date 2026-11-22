using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The gate against an effect or an option that is on sale before anything carries it out (the cause the Phase 2 and Phase 3 RCAs name, and again
/// docs/reviews/rca-long-defeat-phase4-2026-11-06.md row 15). Every effect the run hands the director is either read by the director or named below with the slice that will serve it, and
/// what an unserved effect would deliver is not for sale: no place while its effects are unserved, and no bot errand the director does not start.
/// </summary>
[TestClass]
public class LongDefeatEffectsServedTests
{
    /// <summary>The effects the pure slices hand out before their engine half exists, each with the slice of phase4-plan.md that serves it. Remove a name in the change that serves it.</summary>
    private static readonly Dictionary<string, string> Unserved = new()
    {
        ["SwapGear"] = "Slice 5",
        ["Errand"] = "Slice 5",
        ["DeployEngines"] = "Slice 7",
    };

    private static string Director()
    {
        var folder = RepoPaths.RepoPath("Main", "Features", "LongDefeat");
        return string.Join("\n", Directory.GetFiles(folder, "LongDefeatDirector*.cs").OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => RepoPaths.StripComments(File.ReadAllText(f))));
    }

    private static bool Reads(string director, string effect) => Regex.IsMatch(director, @"\bfx\." + effect + @"\b");

    [TestMethod]
    public void EveryEffectTheRunHandsOut_IsReadByTheDirector_OrNamedAsUnserved()
    {
        var director = Director();
        var effects = typeof(RunEffects).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
        Assert.IsTrue(effects.Count > 20, "RunEffects was not read");

        foreach (var effect in effects)
        {
            if (Unserved.TryGetValue(effect, out var slice))
                Assert.IsFalse(Reads(director, effect), $"fx.{effect} is read by the director now: remove it from Unserved, with what {slice} was to deliver");
            else
                Assert.IsTrue(Reads(director, effect), $"the run hands out fx.{effect} and no LongDefeatDirector file reads it: serve it, or name it in Unserved with its slice");
        }

        CollectionAssert.IsSubsetOf(Unserved.Keys.ToList(), effects, "a name in Unserved is no effect of RunEffects");
    }

    [TestMethod]
    public void NoPlaceIsForSale_WhileItsEffectIsUnserved()
    {
        var offer = LocationOffer.Offered;

        if (Unserved.ContainsKey("SwapGear"))
            Assert.IsFalse(offer.Armoury, "the armoury is offered, but nothing puts a bought set on the lord");
        if (Unserved.ContainsKey("DeployEngines"))
            Assert.IsFalse(offer.Mangonel || offer.Trebuchet, "an engine is offered, but nothing deploys it");
    }

    [TestMethod]
    public void TheBotsPlaceActions_AreDispatched_OnceAPlaceIsForSale()
    {
        if (LocationOffer.Offered == LocationOffer.None)
            Assert.Inconclusive("no place is for sale yet, so the bot returns none of the place actions; this arms itself with the slice that raises LocationOffer.Offered");

        // With a place on sale ShopBot returns these ahead of the heroes, the upgrades and the horn; a director that drops them stalls the bot for the whole stretch between waves.
        var director = Director();
        foreach (var kind in new[] { BotActionKind.Errand, BotActionKind.EndErrand, BotActionKind.Gear, BotActionKind.Engine })
            Assert.IsTrue(Regex.IsMatch(director, @"(==\s*|case\s+)BotActionKind\." + kind + @"\b"), $"a place is for sale and the director compares no bot action with {kind}, so PlayShop drops it");
    }

    [TestMethod]
    public void OnceTheSwapIsServed_EverySpawnOfTheLordBuildsTheMergedKit()
    {
        if (Unserved.ContainsKey("SwapGear"))
            Assert.Inconclusive("the swap is unserved, so no bought set exists to lose at a respawn; this arms itself when SwapGear leaves Unserved");

        // Serving fx.SwapGear alone puts a bought set on the live body and loses it at the next respawn, which builds the class kit (D35).
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatDirector.Player.cs", stripComments: true), "EffectiveLoadout",
            "SpawnLord still hands the field the class kit: pass the run's EffectiveLoadout");
    }
}
