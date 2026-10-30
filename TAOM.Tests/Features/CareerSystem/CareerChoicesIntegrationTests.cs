using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Integration guard against the class of bug behind issues #249/#250: config the content
/// uses but the parser/consumer doesn't support, invisible to synthetic-XML unit tests.
/// Loads the REAL shipped taom_career_choices.xml — the only way to catch a schema drift like
/// the 310 dead <PassiveEffects>-wrapped choices. See
/// docs/reviews/rca-career-partysize-2026-05-29.md + memory feedback_parse_real_config_in_tests.
/// </summary>
[TestClass]
public class CareerChoicesIntegrationTests
{
    private static string ModuleDataPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData"));

    private CareerConfigProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _provider = new CareerConfigProvider(pathService, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void RealChoicesXml_Loads_NonEmpty()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleDataPath, "career_system", "taom_career_choices.xml")),
            $"real taom_career_choices.xml not found under {ModuleDataPath}");
        Assert.IsTrue(_provider.LoadChoices().Count > 100,
            "real taom_career_choices.xml should load the full career choice set");
    }

    // NOTE (Codex review 2026-08-05): a worst-case-cooldown-vs-duration XML invariant test
    // briefly lived here, but it summed only CooldownReduction mutations — Duration
    // mutations push olog_hai's window to 16s against a 15s cooldown, so the invariant is
    // genuinely violated by shipped data. The runtime gate in AbilityActivationController
    // (activation blocked while the window is live, covered by its unit tests) is the real
    // protection; a data invariant here would either miss the case or fail on valid data.

    [TestMethod]
    public void RealChoicesXml_NoPassiveMagnitudeExceedsTheLoaderBound()
    {
        // The loader drops a pip above the bound; a shipped one would silently vanish, so read the raw file.
        var doc = XDocument.Load(Path.Combine(ModuleDataPath, "career_system", "taom_career_choices.xml"));
        var pips = doc.Descendants("PassiveEffect").ToList();
        Assert.IsTrue(pips.Count > 100, "expected the shipped pips");

        var over = pips
            .Select(e => new { E = e, Raw = (string?)e.Attribute("magnitude") ?? (string?)e.Attribute("value") })
            .Where(x => x.Raw != null
                && float.TryParse(x.Raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)
                && Math.Abs(v) > CareerConfigProvider.MaxPassiveMagnitude)
            .Select(x => x.E.ToString())
            .ToList();

        Assert.AreEqual(0, over.Count, string.Join("; ", over));
    }

    [TestMethod]
    public void RealChoicesXml_EveryGroupedChoice_CarriesItsGroupsId()
    {
        // #766: group choices carry no group_id attribute, so GroupId was "" for all of them and the
        // tier gate, keystone exclusivity and GetOwningCareerId never saw a group choice.
        var choices = _provider.LoadChoices().ToDictionary(c => c.Id);
        var groups = _provider.LoadChoiceGroups();
        Assert.IsTrue(groups.Count > 0, "real file should load choice groups");

        var checkedCount = 0;
        var wrong = new System.Collections.Generic.List<string>();
        foreach (var group in groups)
        {
            foreach (var choiceId in group.ChoiceIds)
            {
                checkedCount++;
                if (!choices.TryGetValue(choiceId, out var choice))
                    wrong.Add($"{choiceId} (not loaded)");
                else if (choice.GroupId != group.Id)
                    wrong.Add($"{choiceId} (GroupId='{choice.GroupId}', expected '{group.Id}')");
            }
        }

        Assert.IsTrue(checkedCount > 0, "no grouped choices were checked");
        Assert.AreEqual(0, wrong.Count,
            $"{wrong.Count} of {checkedCount} grouped choices lack their group's id: " + string.Join("; ", wrong));
    }

    [TestMethod]
    public void RealChoicesXml_EveryPassiveChoice_ParsesNonNullPassive()
    {
        // The wrapped-schema bug (#250): 310 <PassiveEffects> choices parsed to no passive
        // because ParseChoice only read a direct <PassiveEffect> child + magnitude=. Every
        // type="Passive" choice MUST yield a parsed PassiveEffect — this asserts it against the
        // real file so any future schema drift fails CI immediately.
        var dead = _provider.LoadChoices()
            .Where(c => c.Type == ChoiceType.Passive && c.Passives.Count == 0)
            .Select(c => c.Id)
            .ToList();

        Assert.AreEqual(0, dead.Count,
            "Every type=\"Passive\" choice must parse to a non-null PassiveEffect. Dead choices: "
            + string.Join(", ", dead));
    }

    [TestMethod]
    public void RealChoicesXml_NoPassiveEffect_DefaultsToSpecialType()
    {
        // Guard against an unrecognized/typo'd type= silently defaulting to PassiveEffectType.Special
        // (the ParseEnum fallback) — a parsed-but-inert passive.
        var special = _provider.LoadChoices()
            .Where(c => c.Passives.Any(p => p.EffectType == PassiveEffectType.Special))
            .Select(c => c.Id)
            .ToList();

        Assert.AreEqual(0, special.Count,
            "No PassiveEffect should fall back to type=Special (unrecognized type=). Offenders: "
            + string.Join(", ", special));
    }

    [TestMethod]
    public void RealChoicesXml_EveryChoice_HoldsAtMostMaxPassivesPerChoice()
    {
        // #769 E8: an option carries up to CareerConfigProvider.MaxPassivesPerChoice passives (direct
        // children and <PassiveEffects> children together). The parser warns about and ignores a third,
        // so a shipped choice past the cap would silently lose its effect. An empty wrapper is a
        // choice that grants nothing, which the non-empty gate above already refuses.
        var xml = XDocument.Load(Path.Combine(ModuleDataPath, "career_system", "taom_career_choices.xml"));
        var overCap = xml.Descendants("Choice")
            .Where(c => c.Elements("PassiveEffect").Count()
                + c.Elements("PassiveEffects").Sum(w => w.Elements("PassiveEffect").Count())
                > CareerConfigProvider.MaxPassivesPerChoice)
            .Select(c => (string?)c.Attribute("id") ?? "(unknown)")
            .ToList();

        Assert.AreEqual(0, overCap.Count,
            $"A choice holds at most {CareerConfigProvider.MaxPassivesPerChoice} passives (the parser ignores extras). "
            + "Offending choices: " + string.Join(", ", overCap));
    }

    // The ids of every choice a career on a redesigned tree (TreeVersion 2 or later) owns: its root and the
    // choices of its groups. A redesigned career is allowed two effects per option; the E8 transition test
    // below speaks only for the original trees.
    internal static System.Collections.Generic.HashSet<string> ChoiceIdsOfRedesignedCareers(
        System.Collections.Generic.IEnumerable<CareerDefinition> careers,
        System.Collections.Generic.IEnumerable<CareerChoiceGroupDefinition> groups)
    {
        var redesigned = careers.Where(c => c.TreeVersion >= 2).ToList();
        var groupIds = new System.Collections.Generic.HashSet<string>(redesigned.SelectMany(c => c.ChoiceGroupIds));
        var ids = new System.Collections.Generic.HashSet<string>(redesigned.Select(c => c.RootChoiceId));
        foreach (var group in groups.Where(g => groupIds.Contains(g.Id)))
            ids.UnionWith(group.ChoiceIds);
        return ids;
    }

    [TestMethod]
    public void ChoiceIdsOfRedesignedCareers_AreTheRootsAndGroupChoicesOfTreeVersionTwoOrLaterOnly()
    {
        var careers = new[]
        {
            new CareerDefinition("old", "", "", "", "", 0, "old_root", null, new[] { "old_group" }),
            new CareerDefinition("new", "", "", "", "", 0, "new_root", null, new[] { "new_group" }, treeVersion: 2),
        };
        var groups = new[]
        {
            new CareerChoiceGroupDefinition("old_group", "old", 1, new[] { "old_a" }),
            new CareerChoiceGroupDefinition("new_group", "new", 1, new[] { "new_a", "new_b" }),
        };

        var ids = ChoiceIdsOfRedesignedCareers(careers, groups);

        CollectionAssert.AreEquivalent(new[] { "new_root", "new_a", "new_b" }, ids.ToArray());
    }

    [TestMethod]
    public void RealChoicesXml_EveryChoiceOfAnOriginalTree_ParsesToTheSamePassiveItDidBeforeMultiEffect()
    {
        // #769 E8 changed the parser from "the first passive" to "up to two". No choice of an original tree
        // (TreeVersion 1) may gain or lose an effect by it: the old reading (a direct child, else the
        // wrapper's first child) is recomputed from the raw XML and must equal the single parsed passive,
        // field for field. A redesigned career (TreeVersion 2 or later) is exempt: it may hold two effects
        // per option, so the first kingdom to ship a two-effect option does not break this test.
        var xml = XDocument.Load(Path.Combine(ModuleDataPath, "career_system", "taom_career_choices.xml"));
        var parsed = _provider.LoadChoices().ToDictionary(c => c.Id);
        var redesigned = ChoiceIdsOfRedesignedCareers(_provider.LoadCareers(), _provider.LoadChoiceGroups());
        var drift = new System.Collections.Generic.List<string>();
        foreach (var el in xml.Descendants("Choice"))
        {
            var id = (string?)el.Attribute("id") ?? "";
            if (redesigned.Contains(id)) continue;
            var old = el.Element("PassiveEffect") ?? el.Element("PassiveEffects")?.Element("PassiveEffect");
            var now = parsed[id].Passives;
            if (old == null)
            {
                if (now.Count != 0) drift.Add($"{id}: gained {now.Count} passive(s)");
                continue;
            }
            if (now.Count != 1) { drift.Add($"{id}: {now.Count} passives, expected 1"); continue; }

            Enum.TryParse<PassiveEffectType>((string?)old.Attribute("type"), true, out var type);
            var magnitudeText = (string?)old.Attribute("magnitude") ?? (string?)old.Attribute("value");
            var magnitude = magnitudeText == null ? 0f : float.Parse(magnitudeText, System.Globalization.CultureInfo.InvariantCulture);
            var maskText = (string?)old.Attribute("attack_type_mask");
            var mask = AttackTypeMask.All;
            if (maskText != null && !AttackTypeMaskMatch.TryParse(maskText, out mask)) mask = AttackTypeMask.None;
            var p = now[0];
            if (p.EffectType != type || p.Magnitude != magnitude || p.AttackTypeMask != mask || p.TroopClass != TroopClassMask.All)
                drift.Add($"{id}: {p.EffectType} {p.Magnitude} {p.AttackTypeMask} {p.TroopClass}, was {type} {magnitude} {mask}");
        }

        Assert.AreEqual(0, drift.Count, "shipped choices changed their passive: " + string.Join("; ", drift));
    }

    [TestMethod]
    public void RealChoicesXml_EveryTroopClass_ParsesAndSitsOnAClassFilteredType()
    {
        // #769 E9, live since the Gondor pilot (C2). An unparseable troop_class DROPS its passive at load
        // (fail closed), and a troop_class on a type no consumer passes a class for is ignored, so a typo or
        // a misplaced attribute must fail here instead of shipping a pip that silently does nothing or
        // widens to the whole army. The classed count guards the scan against going stale.
        var xml = XDocument.Load(Path.Combine(ModuleDataPath, "career_system", "taom_career_choices.xml"));
        var problems = new System.Collections.Generic.List<string>();
        var classed = 0;
        foreach (var el in xml.Descendants("PassiveEffect").Where(e => e.Attribute("troop_class") != null))
        {
            classed++;
            var id = (string?)el.Ancestors("Choice").FirstOrDefault()?.Attribute("id") ?? "(unknown)";
            if (!TroopClassMapper.TryParse((string?)el.Attribute("troop_class"), out _))
                problems.Add($"{id}: troop_class '{(string?)el.Attribute("troop_class")}' does not parse (passive dropped)");
            if (!Enum.TryParse<PassiveEffectType>((string?)el.Attribute("type"), true, out var type)
                || !PassiveEffectConsumers.IsClassFiltered(type))
                problems.Add($"{id}: troop_class on a type that reads no class ({(string?)el.Attribute("type")})");
        }

        Assert.IsTrue(classed > 0, "no shipped pip carries a troop_class: the scan found nothing to check, so it is stale");
        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void RealChoicesXml_EveryAttackTypeMask_SitsOnAMaskGatedType()
    {
        // The loader ignores (with a warning) an attack_type_mask on a type whose consumer is never handed a hit, so
        // a pip that narrows a hit kind on, say, PartySize would ship looking restricted while applying to every
        // hit. Reads the raw file, not the provider's output, which has already applied that rule.
        var xml = XDocument.Load(Path.Combine(ModuleDataPath, "career_system", "taom_career_choices.xml"));
        var problems = new System.Collections.Generic.List<string>();
        var masked = 0;
        foreach (var el in xml.Descendants("PassiveEffect").Where(e => e.Attribute("attack_type_mask") != null))
        {
            masked++;
            var id = (string?)el.Ancestors("Choice").FirstOrDefault()?.Attribute("id") ?? "(unknown)";
            if (!Enum.TryParse<PassiveEffectType>((string?)el.Attribute("type"), true, out var type)
                || !PassiveEffectConsumers.IsMaskGated(type))
                problems.Add($"{id}: attack_type_mask on a type that reads no hit ({(string?)el.Attribute("type")})");
        }

        Assert.IsTrue(masked > 0, "no shipped pip carries an attack_type_mask: the scan found nothing to check, so it is stale");
        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    // The shipped troop pips that carry an attack_type_mask other than All.
    internal static System.Collections.Generic.List<string> MaskedTroopPips(System.Collections.Generic.IEnumerable<CareerChoiceDefinition> choices)
        => choices
            .SelectMany(c => c.Passives
                .Where(p => (p.EffectType == PassiveEffectType.TroopDamage || p.EffectType == PassiveEffectType.TroopResistance)
                    && p.AttackTypeMask != AttackTypeMask.All)
                .Select(p => $"{c.Id} ({p.EffectType} {p.AttackTypeMask})"))
            .ToList();

    [TestMethod]
    public void MaskedTroopPips_FindsOnlyATroopDamageOrResistancePipWithANarrowMask()
    {
        var choices = new[]
        {
            new CareerChoiceDefinition("ward", "g", ChoiceType.Passive, "", "", new[] { new PassiveEffect(PassiveEffectType.TroopResistance, 0.1f, AttackTypeMask.Ranged) }, null),
            new CareerChoiceDefinition("plain", "g", ChoiceType.Passive, "", "", new[] { new PassiveEffect(PassiveEffectType.TroopDamage, 0.1f) }, null),
            new CareerChoiceDefinition("hero", "g", ChoiceType.Passive, "", "", new[] { new PassiveEffect(PassiveEffectType.Damage, 0.1f, AttackTypeMask.Melee) }, null),
        };

        CollectionAssert.AreEqual(new[] { "ward (TroopResistance Ranged)" }, MaskedTroopPips(choices));
    }

    [TestMethod]
    public void RealChoicesXml_NoTroopDamageOrResistancePip_CarriesAnAttackTypeMask()
    {
        // The Active Effects panel (CareerScreenVM) totals a type's magnitudes with no mask, so a narrowed
        // TroopDamage or TroopResistance pip (the arrow ward E9 makes authorable) would read as an all-hit
        // bonus there. Until the panel shows masks, no shipped choice may carry one: teach the panel first.
        var masked = MaskedTroopPips(_provider.LoadChoices());

        Assert.AreEqual(0, masked.Count,
            "A shipped TroopDamage or TroopResistance pip carries an attack_type_mask, which the Active Effects panel "
            + "does not show: the panel must show masks first (CareerScreenVM, CareerEffectDisplayMap). Offenders: "
            + string.Join(", ", masked));
    }

    [TestMethod]
    public void RealChoicesXml_NoPhantomPassiveType_EveryTypeHasConsumer()
    {
        // Phantom-bonus regression guard. Every PassiveEffect type used by a shipped pip MUST
        // have a runtime consumer (PassiveEffectConsumers). A type without one is selectable in
        // the career tree but inert in-game. Six such types shipped as phantoms across ~211 pips
        // (MountChargeDamage / MountHealth / StealthBonus / TroopResistance / Ammo /
        // HeroHealing) before being wired; this pins the shipped XML so a new phantom
        // cannot ship silently again.
        var phantom = _provider.LoadChoices()
            .SelectMany(c => c.Passives.Where(p => !PassiveEffectConsumers.IsConsumed(p.EffectType)).Select(p => $"{c.Id} (type={p.EffectType})"))
            .ToList();

        Assert.AreEqual(0, phantom.Count,
            "Every PassiveEffect type in the shipped XML must have a runtime consumer in "
            + "PassiveEffectConsumers. Phantom (inert) choices: " + string.Join(", ", phantom));
    }

    [TestMethod]
    public void RealChoicesXml_EveryParsedPassive_HasFiniteNonZeroMagnitude()
    {
        // Codex review 2026-05-29 hardening: a malformed value=/magnitude= parses to 0 (ParseFloat
        // default), which still passes the non-null + recognized-type checks but is an inert passive.
        // Assert every parsed magnitude is finite and non-zero against the real file.
        var bad = _provider.LoadChoices()
            .Where(c => c.Passives.Any(p => float.IsNaN(p.Magnitude) || float.IsInfinity(p.Magnitude)
                    || p.Magnitude == 0f))
            .Select(c => c.Id)
            .ToList();

        Assert.AreEqual(0, bad.Count,
            "Every parsed PassiveEffect must have a finite, non-zero magnitude. Offenders: "
            + string.Join(", ", bad));
    }

    [TestMethod]
    public void RealChoicesXml_EveryAttackTypeMask_ParsesAndKindPipsAreKinded()
    {
        // #613: an unparseable attack_type_mask lands on None (inert) with a warning at load. That
        // must never ship: a typo would silently kill the pip. And the five pips that name a kind
        // ("+5% blunt resistance", "+8% cut resistance") must carry that kind now, not All.
        var choices = _provider.LoadChoices();

        var inert = choices
            .Where(c => c.Passives.Any(p => p.AttackTypeMask == AttackTypeMask.None))
            .Select(c => c.Id)
            .ToList();
        Assert.AreEqual(0, inert.Count, "attack_type_mask failed to parse on: " + string.Join(", ", inert));

        var kinded = choices
            .Where(c => c.Passives.Any(p => (p.AttackTypeMask & AttackTypeMaskMatch.KindBits) != AttackTypeMask.None))
            .Select(c => c.Id)
            .OrderBy(id => id)
            .ToList();
        CollectionAssert.AreEquivalent(
            new[]
            {
                "avanc_luth_raider_t2_b_p2",
                "black_uruk_captain_t1_a_p2",
                "black_uruk_captain_t2_a_p3",
                "olog_hai_warchief_t1_a_p2",
                "warden_of_wainwatch_t1_a_p4",   // South Rhovanion's clone of the Stangard pip: "+5% blunt resistance"
                "watchman_of_stangard_t1_a_p4",
            },
            kinded,
            "the set of kind-masked pips moved; re-read their wording before accepting the change");
    }
}
