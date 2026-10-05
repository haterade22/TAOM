using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.SiegeForces.Hooks;
using TAOM.Features.SiegeForces.Models;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// The siege troop picker against the installed engine. Three things are pinned, because each fails silently:
/// the two patch TARGETS and the parameter NAMES Harmony binds the prefixes by; every engine member the prefix helpers,
/// both adapters and the model reference (a member that stops resolving fails when its method is first compiled, before
/// any try inside it can run, and PatchShield swallows that at the patched method and skips the original: for
/// <c>InitWithSinglePhase</c> that is no spawn phases in every battle; lessons/harmony-il.md "A patch's own try/catch
/// cannot survive a JIT-time member-resolution failure"); and the enum values the compiled code folds into literals.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SiegeForcesBindingTests
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // --- the two patch targets ---------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void StartSiegeMission_IsTheOnlyPublicStaticVoidOverload_WithAParameterNamedSettlement()
    {
        var overloads = typeof(PlayerSiege).GetMethods(AnyMember).Where(m => m.Name == "StartSiegeMission").ToList();
        Assert.AreEqual(1, overloads.Count, "a second overload would make the patch target ambiguous");

        var method = overloads.Single();
        Assert.IsTrue(method.IsPublic && method.IsStatic);
        Assert.AreEqual(typeof(void), method.ReturnType);
        CollectionAssert.AreEqual(new[] { "settlement" }, method.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(typeof(Settlement), method.GetParameters()[0].ParameterType);
        Assert.IsTrue(method.GetParameters()[0].IsOptional, "the engine's default (null) is what MenuHelper relies on");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void StartSiegeMissionPrefix_BindsItsParameterByName_AndReturnsBool()
    {
        var target = typeof(PlayerSiege).GetMethod("StartSiegeMission", AnyMember)!;

        AssertPrefixBindsByName(typeof(Patch102_StartSiegeMissionPicker), target, typeof(bool));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void InitWithSinglePhase_IsTheOnlyPublicInstanceVoidOverload_WithTheSevenParametersTheDesignNames()
    {
        var overloads = typeof(DefaultBattleMissionAgentSpawnLogic).GetMethods(AnyMember)
            .Where(m => m.Name == "InitWithSinglePhase").ToList();
        Assert.AreEqual(1, overloads.Count, "a second overload would make the patch target ambiguous");

        var method = overloads.Single();
        Assert.IsTrue(method.IsPublic && !method.IsStatic);
        Assert.AreEqual(typeof(void), method.ReturnType);
        // Harmony binds the four ints by NAME: the names are not API, so a rename is a legal silent engine change.
        CollectionAssert.AreEqual(
            new[]
            {
                "defenderTotalSpawn", "attackerTotalSpawn", "defenderInitialSpawn", "attackerInitialSpawn",
                "spawnDefenders", "spawnAttackers", "spawnSettings",
            },
            method.GetParameters().Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool) },
            method.GetParameters().Take(6).Select(p => p.ParameterType).ToArray());
        Assert.IsTrue(method.GetParameters()[6].ParameterType.IsByRef, "the MissionSpawnSettings parameter is `in`");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnTotalsPrefix_BindsItsParametersByName_AndReturnsVoid()
    {
        var target = typeof(DefaultBattleMissionAgentSpawnLogic).GetMethod("InitWithSinglePhase", AnyMember)!;

        AssertPrefixBindsByName(typeof(Patch102_SpawnTotalsFit), target, typeof(void));

        // The prefix must take the four totals by reference, or its writes never reach the original.
        var prefix = typeof(Patch102_SpawnTotalsFit).GetMethod("Prefix")!;
        foreach (var name in new[] { "defenderTotalSpawn", "attackerTotalSpawn", "defenderInitialSpawn", "attackerInitialSpawn" })
            Assert.IsTrue(prefix.GetParameters().Single(p => p.Name == name).ParameterType.IsByRef, $"{name} must be ref");
        Assert.AreEqual(typeof(object), prefix.GetParameters().Single(p => p.Name == "__instance").ParameterType,
            "__instance is an object: the prefix body names no engine type");
    }

    // --- the model slot ----------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheModelSlot_HasOneAbstractMethod_WithTheSixParametersTheOverrideTakes()
    {
        var abstractMethod = typeof(TroopSupplierProbabilityModel).GetMethod("EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization");
        Assert.IsNotNull(abstractMethod);
        Assert.IsTrue(abstractMethod!.IsAbstract);

        var expected = new[]
        {
            typeof(MapEventParty), typeof(FlattenedTroopRoster), typeof(bool), typeof(int), typeof(bool),
            typeof(List<(FlattenedTroopRosterElement, MapEventParty, float)>),
        };
        CollectionAssert.AreEqual(expected, abstractMethod.GetParameters().Select(p => p.ParameterType).ToArray());

        var ours = typeof(TaomTroopSupplierProbabilityModel).GetMethod(abstractMethod.Name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNotNull(ours, "TaomTroopSupplierProbabilityModel does not declare the override");
        CollectionAssert.AreEqual(expected, ours!.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.AreSame(abstractMethod, ours.GetBaseDefinition(), "the override must be of the slot's method, not a shadow");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheModel_ExtendsTheDefaultModel_SoEveryOtherRuleStaysVanillas()
    {
        Assert.AreEqual(typeof(DefaultTroopSupplierProbabilityModel), typeof(TaomTroopSupplierProbabilityModel).BaseType);
        var baseMethod = typeof(DefaultTroopSupplierProbabilityModel).GetMethod("EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization");
        Assert.IsTrue(baseMethod is { IsVirtual: true, IsFinal: false }, "the default model's method must stay overridable and callable as base");
    }

    // --- the screen --------------------------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OpenTroopSelection_HasTheSixParameterShapeTheAdapterCalls()
    {
        var method = typeof(MenuContext).GetMethod("OpenTroopSelection");
        Assert.IsNotNull(method, "MenuContext.OpenTroopSelection no longer resolves");
        CollectionAssert.AreEqual(
            new[] { typeof(TroopRoster), typeof(TroopRoster), typeof(Func<CharacterObject, bool>), typeof(Action<TroopRoster>), typeof(int), typeof(int) },
            method!.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.IsNotNull(typeof(MenuContext).GetProperty("Handler"), "the adapter refuses to open the screen without a Handler");
        Assert.AreEqual(typeof(IMenuContextHandler), typeof(MenuContext).GetProperty("Handler")!.PropertyType);
    }

    // --- every member the helpers, adapters and model reference -----------------------------------------------------

    private static PropertyInfo Property(Type type, string name, Type expected)
    {
        var property = type.GetProperty(name, AnyMember);
        Assert.IsNotNull(property, $"{type.FullName}.{name} no longer resolves");
        Assert.AreEqual(expected, property!.PropertyType, $"{type.Name}.{name} changed type");
        Assert.IsNotNull(property.GetGetMethod(true), $"{type.Name}.{name} lost its getter");
        return property;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheAdapterAndHelperMembers_StillResolveWithTheirTypes()
    {
        Property(typeof(Campaign), "Current", typeof(Campaign));
        Property(typeof(Campaign), "MainParty", typeof(MobileParty));
        Property(typeof(Campaign), "CurrentMenuContext", typeof(MenuContext));
        Property(typeof(MobileParty), "MapEvent", typeof(MapEvent));
        Property(typeof(MobileParty), "Army", typeof(Army));
        Property(typeof(MobileParty), "Party", typeof(PartyBase));
        Property(typeof(MobileParty), "IsGarrison", typeof(bool));
        Property(typeof(Army), "LeaderParty", typeof(MobileParty));
        Property(typeof(MapEvent), "IsSiegeAssault", typeof(bool));
        Property(typeof(MapEvent), "PlayerSide", typeof(BattleSideEnum));
        Property(typeof(MapEventParty), "Party", typeof(PartyBase));
        Property(typeof(PartyBase), "Id", typeof(string));
        Property(typeof(PartyBase), "MobileParty", typeof(MobileParty));
        Property(typeof(PartyBase), "MemberRoster", typeof(TroopRoster));
        Property(typeof(PartyBase), "Side", typeof(BattleSideEnum));
        Property(typeof(PlayerSiege), "BesiegedSettlement", typeof(Settlement));
        Property(typeof(Settlement), "CurrentSiegeState", typeof(Settlement.SiegeState));
        Property(typeof(Settlement), "OwnerClan", typeof(Clan));
        Property(typeof(Clan), "PlayerClan", typeof(Clan));
        Property(typeof(CharacterObject), "IsHero", typeof(bool));
        Property(typeof(CharacterObject), "IsPlayerCharacter", typeof(bool));
        Property(typeof(CharacterObject), "Race", typeof(int));
        Property(typeof(CharacterObject), "StringId", typeof(string));
        Property(typeof(FlattenedTroopRosterElement), "Troop", typeof(CharacterObject));
        Property(typeof(DefaultBattleMissionAgentSpawnLogic), "PlayerSide", typeof(BattleSideEnum));
        Property(typeof(TroopRosterElement), "Number", typeof(int));
        Property(typeof(TroopRosterElement), "WoundedNumber", typeof(int));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheAdapterMethodsAndFields_StillResolveWithTheirShapes()
    {
        var partiesOnSide = typeof(MapEvent).GetMethod("PartiesOnSide", new[] { typeof(BattleSideEnum) });
        Assert.IsNotNull(partiesOnSide, "MapEvent.PartiesOnSide(BattleSideEnum)");
        Assert.AreEqual(typeof(MBReadOnlyList<MapEventParty>), partiesOnSide!.ReturnType);

        var getTroopRoster = typeof(TroopRoster).GetMethod("GetTroopRoster", Type.EmptyTypes);
        Assert.IsNotNull(getTroopRoster, "TroopRoster.GetTroopRoster()");
        Assert.AreEqual(typeof(MBList<TroopRosterElement>), getTroopRoster!.ReturnType);

        var dummy = typeof(TroopRoster).GetMethod("CreateDummyTroopRoster", Type.EmptyTypes);
        Assert.IsTrue(dummy is { IsStatic: true } && dummy.ReturnType == typeof(TroopRoster), "TroopRoster.CreateDummyTroopRoster()");

        var addToCounts = typeof(TroopRoster).GetMethod("AddToCounts",
            new[] { typeof(CharacterObject), typeof(int), typeof(bool), typeof(int), typeof(int), typeof(bool), typeof(int) });
        Assert.IsNotNull(addToCounts, "TroopRoster.AddToCounts(character, count, insertAtFront, woundedCount, xpChange, removeDepleted, index)");
        CollectionAssert.AreEqual(
            new[] { "character", "count", "insertAtFront", "woundedCount", "xpChange", "removeDepleted", "index" },
            addToCounts!.GetParameters().Select(p => p.Name).ToArray(),
            "the adapter passes woundedCount by name");

        var character = typeof(TroopRosterElement).GetField("Character");
        Assert.IsTrue(character != null && character.FieldType == typeof(CharacterObject), "TroopRosterElement.Character is a public field");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheEnumValuesTheCompiledCodeFoldsIntoLiterals_AreTheInstalledEnginesValues()
    {
        // `x == BattleSideEnum.Attacker` is folded to the constant at COMPILE time, and the service keys its per-side
        // counts on the ints 0 and 1. Parse the installed enum at run time: a renumbering fails here, not silently.
        Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(typeof(BattleSideEnum), "Defender")));
        Assert.AreEqual(1, Convert.ToInt32(Enum.Parse(typeof(BattleSideEnum), "Attacker")));
        Assert.AreEqual(-1, Convert.ToInt32(Enum.Parse(typeof(BattleSideEnum), "None")), "None is not a side the service counts");
        Assert.AreEqual(0, Convert.ToInt32(Enum.Parse(typeof(Settlement.SiegeState), "OnTheWalls")));
    }

    // --- everything the picker's own methods reference compiles against the installed engine -------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void EveryMethodTheFeatureOwnsThatTouchesTheEngine_CompilesAgainstTheInstalledEngine()
    {
        // RuntimeHelpers.PrepareMethod forces the JIT, which resolves every member a body references. A drifted member
        // throws here instead of at the first assault (lessons/harmony-il.md: a body's own try cannot catch it).
        // RequiresGameIL, like MapFrameProfilerBindingTests.Patch101Bodies_CompileAgainstInstalledEngine: the claim is
        // about the INSTALLED engine, and CI's binding gate runs against the metadata-only reference assemblies.
        var prepared = 0;
        foreach (var type in new[]
                 {
                     typeof(SiegeForcesAdapter), typeof(ReadyListWindow), typeof(TaomTroopSupplierProbabilityModel),
                     typeof(Patch102_StartSiegeMissionPicker), typeof(Patch102_SpawnTotalsFit),
                 })
            prepared += PrepareAll(type);

        Assert.IsTrue(prepared >= 30, $"expected to compile every method of the five types and their closures, compiled {prepared}");
    }

    private static int PrepareAll(Type type)
    {
        const BindingFlags declared = AnyMember | BindingFlags.DeclaredOnly;
        var count = 0;
        foreach (var method in type.GetMethods(declared))
        {
            if (method.IsAbstract || method.ContainsGenericParameters) continue;
            RuntimeHelpers.PrepareMethod(method.MethodHandle);
            count++;
        }

        foreach (var ctor in type.GetConstructors(declared))
        {
            if (ctor.IsStatic || ctor.ContainsGenericParameters) continue;
            RuntimeHelpers.PrepareMethod(ctor.MethodHandle);
            count++;
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            count += PrepareAll(nested);
        return count;
    }

    // --- helpers -----------------------------------------------------------------------------------------------

    private static void AssertPrefixBindsByName(Type patchType, MethodBase target, Type expectedReturn)
    {
        var prefix = patchType.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(prefix, $"{patchType.Name} has no public static Prefix");
        Assert.AreEqual(expectedReturn, prefix!.ReturnType);

        foreach (var parameter in prefix.GetParameters())
        {
            if (parameter.Name == "__instance")
            {
                Assert.IsTrue(parameter.ParameterType.IsAssignableFrom(target.DeclaringType), "__instance cannot hold the target's type");
                continue;
            }

            var original = target.GetParameters().SingleOrDefault(p => p.Name == parameter.Name);
            Assert.IsNotNull(original, $"the prefix parameter '{parameter.Name}' names no parameter of {target.DeclaringType!.Name}.{target.Name}: Harmony would hand it null/default");
            var patchType2 = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType;
            var originalType = original!.ParameterType.IsByRef ? original.ParameterType.GetElementType() : original.ParameterType;
            Assert.AreEqual(originalType, patchType2, $"parameter '{parameter.Name}' changed type");
        }
    }
}
