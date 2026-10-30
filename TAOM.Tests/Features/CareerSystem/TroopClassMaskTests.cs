using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// #769 E9: the class a troop's default formation puts it in, and the authored <c>troop_class</c> text
/// that selects classes. The mapper is pinned over every <see cref="FormationClass"/> value, so an engine
/// bump that adds a formation fails here instead of silently landing on None: the expected table must name
/// every value the installed engine defines, and a value it does not name is the failure.
/// </summary>
[TestClass]
public class TroopClassMaskTests
{
    private static readonly Dictionary<FormationClass, TroopClassMask> Expected = new Dictionary<FormationClass, TroopClassMask>
    {
        [FormationClass.Infantry] = TroopClassMask.Infantry,
        [FormationClass.HeavyInfantry] = TroopClassMask.Infantry,
        [FormationClass.Ranged] = TroopClassMask.Ranged,
        [FormationClass.Cavalry] = TroopClassMask.Cavalry,
        [FormationClass.LightCavalry] = TroopClassMask.Cavalry,
        [FormationClass.HeavyCavalry] = TroopClassMask.Cavalry,
        [FormationClass.HorseArcher] = TroopClassMask.Ranged | TroopClassMask.Cavalry,
        [FormationClass.Skirmisher] = TroopClassMask.Infantry | TroopClassMask.Ranged,
        [FormationClass.General] = TroopClassMask.None,
        [FormationClass.Bodyguard] = TroopClassMask.None,
        [FormationClass.Unset] = TroopClassMask.None,
        [FormationClass.NumberOfAllFormationsWithUnset] = TroopClassMask.None,
    };

    [TestMethod]
    public void FromFormationClass_EveryEnumValue_MapsToItsClass()
    {
        foreach (FormationClass formation in Enum.GetValues(typeof(FormationClass)))
        {
            Assert.IsTrue(Expected.TryGetValue(formation, out var expected),
                $"{formation} ({(int)formation}) is a FormationClass value this table does not name: decide its troop class here and in TroopClassMapper");
            Assert.AreEqual(expected, TroopClassMapper.FromFormationClass(formation), $"{formation} ({(int)formation})");
        }
    }

    [TestMethod]
    public void ExpectedTable_NamesNoValueTheEngineLacks()
    {
        // The other direction of the tripwire: a removed or renumbered formation leaves a stale row.
        var defined = new HashSet<FormationClass>((FormationClass[])Enum.GetValues(typeof(FormationClass)));
        foreach (var formation in Expected.Keys)
            Assert.IsTrue(defined.Contains(formation), $"{formation} is no longer a FormationClass value");
    }

    [TestMethod]
    public void FromFormationClass_OutOfRangeValue_IsNone()
    {
        Assert.AreEqual(TroopClassMask.None, TroopClassMapper.FromFormationClass((FormationClass)99));
        Assert.AreEqual(TroopClassMask.None, TroopClassMapper.FromFormationClass((FormationClass)(-1)));
    }

    [TestMethod]
    public void FromCharacter_NoCharacter_IsNone()
    {
        // The damage boundary and the counters pass whatever the AttackInformation snapshot holds: a hit with
        // no attacker (fall damage, a missile whose shooter is gone) must read as the army-wide reading, not throw.
        Assert.AreEqual(TroopClassMask.None, TroopClassMapper.FromCharacter(null));
    }

    [TestMethod]
    public void All_IsExactlyTheThreeClasses()
    {
        Assert.AreEqual(TroopClassMask.Infantry | TroopClassMask.Ranged | TroopClassMask.Cavalry, TroopClassMask.All);
    }

    [DataTestMethod]
    [DataRow("Infantry", TroopClassMask.Infantry)]
    [DataRow("infantry", TroopClassMask.Infantry)]
    [DataRow("RANGED", TroopClassMask.Ranged)]
    [DataRow("Cavalry", TroopClassMask.Cavalry)]
    [DataRow("Infantry,Ranged", TroopClassMask.Infantry | TroopClassMask.Ranged)]
    [DataRow("Infantry|Ranged", TroopClassMask.Infantry | TroopClassMask.Ranged)]
    [DataRow("Ranged | Cavalry", TroopClassMask.Ranged | TroopClassMask.Cavalry)]
    [DataRow("All", TroopClassMask.All)]
    [DataRow("all", TroopClassMask.All)]
    [DataRow("Infantry, All", TroopClassMask.All)]
    [DataRow("  cavalry ,  ranged ", TroopClassMask.Cavalry | TroopClassMask.Ranged)]
    [DataRow("Infantry, Ranged, Cavalry", TroopClassMask.All)]
    [DataRow("Infantry,Infantry", TroopClassMask.Infantry)]
    public void TryParse_KnownNames_ReturnsTheUnion(string text, TroopClassMask expected)
    {
        Assert.IsTrue(TroopClassMapper.TryParse(text, out var mask));
        Assert.AreEqual(expected, mask);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(",")]
    [DataRow("None")]
    [DataRow("Infantri")]
    [DataRow("Infantry,Archers")]
    [DataRow("Infantry Ranged")]
    [DataRow("Infantry,,Ranged")]
    [DataRow("Infantry|")]
    [DataRow("1")]
    [DataRow("7")]
    public void TryParse_UnknownOrEmpty_Fails(string text)
    {
        Assert.IsFalse(TroopClassMapper.TryParse(text, out var mask));
        Assert.AreEqual(TroopClassMask.None, mask);
    }

    [TestMethod]
    public void TryParse_Null_Fails()
    {
        Assert.IsFalse(TroopClassMapper.TryParse(null, out _));
    }
}
