using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// #1534: every practice weapon set hands out a weapon.
///
/// Vanilla's <c>ArenaPracticeFightMissionController.AddRandomWeapons</c> (:325-338) reads
/// <c>weapon_practice_stage_&lt;n&gt;_&lt;culture id&gt;</c>, picks one of its battle rosters at random and copies the
/// slots Item0 to Item3 to the fighter, the player included. The wave fight (<c>ArenaWaveArena.WeaponSets</c>) reads the
/// same ids. A battle roster with nothing in those four slots sends the fighter into the yard with bare hands, and no load
/// error says so: the South Rhovanion sets shipped that way, identical to Rohan's, whose sets the engine never asks for.
/// </summary>
[TestClass]
public class PracticeWeaponSetTests
{
    private const string Prefix = "weapon_practice_stage_";

    private static readonly string[] WeaponSlots = { "Item0", "Item1", "Item2", "Item3" };

    [TestMethod]
    public void EveryPracticeWeaponSet_HoldsAWeaponInEveryRoster()
    {
        var sets = 0;
        var offenders = new List<string>();
        var characters = Path.Combine(CultureDataFixture.ModuleDataPath(), "characters");

        foreach (var file in Directory.GetFiles(characters, "*.xml").OrderBy(f => f))
        {
            foreach (var character in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                var id = (string)character.Attribute("id") ?? string.Empty;
                if (!id.StartsWith(Prefix))
                    continue;

                sets++;
                var rosters = character.Elements("Equipments").Elements("EquipmentRoster").Where(IsBattleRoster).ToList();
                if (rosters.Count == 0)
                    offenders.Add($"{id} ({Path.GetFileName(file)}): no battle EquipmentRoster");
                for (var i = 0; i < rosters.Count; i++)
                    if (!rosters[i].Elements("equipment").Any(e => WeaponSlots.Contains((string)e.Attribute("slot"))))
                        offenders.Add($"{id} ({Path.GetFileName(file)}): battle roster {i} holds no item in Item0 to Item3");
            }
        }

        Assert.IsTrue(sets >= 78, $"only {sets} practice weapon sets were read (78 shipped on 2026-11-22): the check tested too little");
        Assert.AreEqual(0, offenders.Count, string.Join("\n", offenders));
    }

    /// <summary>
    /// What <c>BattleEquipments</c> keeps, as <c>MBEquipmentRoster.InitEquipment</c> decides it (:88-107): an
    /// <c>equipmentType</c> attribute wins and must read Battle; without one, <c>civilian="true"</c> makes the roster civilian.
    /// A set with no battle roster makes vanilla's <c>list[index]</c> throw when the fight starts.
    /// </summary>
    private static bool IsBattleRoster(XElement roster)
    {
        var type = (string)roster.Attribute("equipmentType");
        if (type != null)
            return type == "Battle";
        return !bool.TryParse((string)roster.Attribute("civilian"), out var civilian) || !civilian;
    }
}
