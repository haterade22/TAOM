using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;

namespace TAOM.Client.Features.CustomBattles.Muster
{
    /// <summary>
    /// A unit's captain (user 2026-11-07): "the upgraded version of the unit selected, and if there is no upgraded version
    /// you just get a different elite unit, with the closest weapon type - so if there is only 1 elite unit with 2 handed
    /// swords, but there is a 2handed elite unit with an axe, then that could be the captain".
    /// Upgrades come from the NPCCharacter XML of the loaded modules (read only; a Custom Battle character does not carry
    /// them: BasicCharacterObject has no upgrade targets, v1.5.7). Elite = same culture, not a hero, a higher level than the unit; closest = same weapon class, then same weapon
    /// family (two-handed, one-handed, polearm, bow, crossbow, thrown), then ranged with ranged, mounted with mounted.
    /// </summary>
    internal static class CaptainResolver
    {
        private static Dictionary<string, List<string>>? _upgrades;
        private static Dictionary<string, string> _moduleOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);        // troop id -> the module whose XML defines it
        private static HashSet<string> _inTree = new HashSet<string>(StringComparer.OrdinalIgnoreCase);                     // troops that upgrade or are upgraded into

        private static readonly HashSet<string> VanillaModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle", "BirthAndDeath", "Multiplayer" };

        /// <summary>
        /// A troop worth offering in the new Custom Battle screen (user 2026-11-07, TAOM units): defined by a non-vanilla
        /// module (TAOM reuses vanilla culture ids, so vanilla Imperial / Vlandian troops otherwise show under Dunland /
        /// Rohan), part of a troop tree or an elite (level 21+), a soldier (not a caravan guard, mercenary, bandit or
        /// villager), not obsolete (TAOM redefines vanilla and Fords of War ids as obsolete, and the troop list hides
        /// them), and not a training dummy.
        /// </summary>
        public static bool IsOffered(BasicCharacterObject c)
        {
            if (c == null || !c.IsSoldier || c.IsObsolete) return false;
            Upgrades();
            var id = c.StringId;
            if (id.IndexOf("dummy", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("practice", StringComparison.OrdinalIgnoreCase) >= 0
                || id.IndexOf("tutorial", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (!_moduleOf.TryGetValue(id, out var module) || VanillaModules.Contains(module)) return false;
            return _inTree.Contains(id) || c.Level >= 21;
        }

        public static BasicCharacterObject? Resolve(BasicCharacterObject? unit, out string how)
        {
            how = "none";
            if (unit == null) return null;
            var all = MBObjectManager.Instance.GetObjectTypeList<BasicCharacterObject>();
            if (Upgrades().TryGetValue(unit.StringId, out var ids))
            {
                var ups = ids.Select(id => MBObjectManager.Instance.GetObject<BasicCharacterObject>(id)).Where(c => c != null && !c.IsObsolete).ToList();
                if (ups.Count > 0)
                {
                    var best = ups.OrderBy(c => Distance(unit, c)).ThenByDescending(c => c.Level).First();
                    how = ups.Count == 1 ? "its upgrade" : $"its closest of {ups.Count} upgrades";
                    return best;
                }
            }
            var elites = all.Where(c => c != unit && !c.IsHero && c.Culture == unit.Culture && c.Level > unit.Level && PrimaryClass(c) != null && IsOffered(c)).ToList();
            if (elites.Count == 0)
            {
                how = "no upgrade and no higher unit in its culture: the unit itself";
                return unit;
            }
            var pick = elites.OrderBy(c => Distance(unit, c)).ThenByDescending(c => c.Level).First();
            how = $"no upgrade; the culture's closest elite (weapon {PrimaryClass(unit)} -> {PrimaryClass(pick)})";
            return pick;
        }

        /// <summary>0 same weapon class, 1 same family, 2 both ranged or both melee, 4 otherwise; +2 mounted vs on foot.</summary>
        private static int Distance(BasicCharacterObject a, BasicCharacterObject b)
        {
            var ca = PrimaryClass(a); var cb = PrimaryClass(b);
            int d = ca == cb ? 0 : Family(ca) == Family(cb) ? 1 : IsRanged(ca) == IsRanged(cb) ? 2 : 4;
            if (Mounted(a) != Mounted(b)) d += 2;
            return d;
        }

        public static WeaponClass? PrimaryClass(BasicCharacterObject c)
        {
            var eq = c?.Equipment;
            if (eq == null) return null;
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                var w = eq[i].Item?.PrimaryWeapon;
                if (w == null) continue;
                var k = w.WeaponClass;
                if (k == WeaponClass.SmallShield || k == WeaponClass.LargeShield || k == WeaponClass.Arrow || k == WeaponClass.Bolt
                    || k == WeaponClass.Banner || k == WeaponClass.Undefined) continue;
                return k;
            }
            return null;
        }

        private static string Family(WeaponClass? k)
        {
            if (k == null) return "none";
            var n = k.ToString();
            if (n.StartsWith("TwoHanded") || k == WeaponClass.Pick) return "2h";
            if (n.Contains("Polearm")) return "polearm";
            if (k == WeaponClass.Bow) return "bow";
            if (k == WeaponClass.Crossbow) return "crossbow";
            if (k == WeaponClass.Javelin || n.StartsWith("Throwing") || k == WeaponClass.Stone) return "thrown";
            return "1h";
        }

        private static bool IsRanged(WeaponClass? k) => Family(k) == "bow" || Family(k) == "crossbow" || Family(k) == "thrown";

        private static bool Mounted(BasicCharacterObject c) => c?.Equipment != null && !c.Equipment[EquipmentIndex.Horse].IsEmpty;

        /// <summary>
        /// troop id -> its upgrade ids, read once from the files each active module registers as NPCCharacters
        /// (XmlResource.XmlInformationList), in the engine's order. Custom Battle's own load also skips registrations
        /// not for CustomGame; this read keeps them, and they add no upgrades today (2026-11-15). A stream per file, not a
        /// DOM load of every ModuleData XML (#1297: about 59 MB on the desktop install, 12 MB of it troop files). It
        /// reads the files as written: no TAOM, TAOM_Map or Armory stylesheet changes upgrade_targets (2026-11-15).
        /// </summary>
        private static Dictionary<string, List<string>> Upgrades()
        {
            if (_upgrades != null) return _upgrades;
            _upgrades = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _moduleOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _inTree = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int files = 0;
            try
            {
                foreach (var info in XmlResource.XmlInformationList)
                {
                    if (info.Id != "NPCCharacters" || !ModuleHelper.IsModuleActive(info.ModuleName)) continue;
                    foreach (var f in FilesOf(ModuleHelper.GetXmlPath(info.ModuleName, info.Name)))
                    {
                        try
                        {
                            using (var reader = new StreamReader(f))
                                ReadCharacters(reader, info.ModuleName, _moduleOf, _upgrades, _inTree);
                            files++;
                        }
                        catch (Exception e) { TAOM.Features.CustomBattles.Muster.CustomBattleMuster.Log($"captain upgrades: {Path.GetFileName(f)} not read: {e.Message}"); }
                    }
                }
            }
            catch (Exception e) { TAOM.Features.CustomBattles.Muster.CustomBattleMuster.Log("captain upgrades: " + e.Message); }
            TAOM.Features.CustomBattles.Muster.CustomBattleMuster.Log($"ARMY captain resolver: {_upgrades.Count} troops with upgrades, from {files} files");
            return _upgrades;
        }

        // the engine's rule (MBObjectManager.GetMergedXmlForManaged): the registered path as a file, else a folder of them
        private static IEnumerable<string> FilesOf(string xmlPath)
        {
            if (File.Exists(xmlPath)) return new[] { xmlPath };
            var folder = xmlPath.Replace(".xml", "");
            return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.xml") : Array.Empty<string>();
        }

        /// <summary>
        /// One NPCCharacters file: each troop's module (the last to define it wins, as in the game) and its upgrades
        /// (every definition's targets joined, as MBObjectManager.MergeElements merges upgrade_targets).
        /// </summary>
        internal static void ReadCharacters(TextReader text, string module, Dictionary<string, string> moduleOf,
            Dictionary<string, List<string>> upgrades, HashSet<string> inTree)
        {
            string? id = null;
            var pending = new List<string>();
            void Commit()
            {
                if (id != null && pending.Count > 0)
                {
                    if (upgrades.TryGetValue(id, out var earlier))
                        pending = earlier.Concat(pending).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    upgrades[id] = pending;
                    inTree.Add(id);
                    foreach (var t in pending) inTree.Add(t);
                }
                pending = new List<string>();
            }

            using var r = XmlReader.Create(text, new XmlReaderSettings { IgnoreComments = true, DtdProcessing = DtdProcessing.Prohibit });
            while (r.Read())
            {
                if (r.NodeType != XmlNodeType.Element) continue;
                if (r.Name == "NPCCharacter")
                {
                    Commit();
                    id = r.GetAttribute("id");
                    if (id != null) moduleOf[id] = module;
                }
                else if (r.Name == "upgrade_target" && id != null)
                {
                    var target = r.GetAttribute("id")?.Replace("NPCCharacter.", "");
                    if (!string.IsNullOrEmpty(target)) pending.Add(target!);
                }
            }
            Commit();
        }
    }
}
