using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.CustomBattles.Muster;

/// <summary>
/// The army a Custom Battle side gets from the muster screen, exactly. Per side: the faction (optional, else the
/// screen's), the banner colours and emblem (optional), the commander, and per unit its number and three roles. A
/// role is taken FROM the unit's number: 50 with banner, captain and horn = 47 soldiers + 1 banner bearer + 1 captain
/// + 1 horn blower (the bearer and the horn blower are soldiers of the unit itself; the captain is its upgrade or the
/// culture's closest elite, CaptainResolver).
/// </summary>
public sealed class ArmySpec
{
    /// <summary>Both sides together: the engine takes no more.</summary>
    public const int MaxTroops = 1000;

    public SideSpec Player = new SideSpec();
    public SideSpec Enemy = new SideSpec();

    public sealed class SideSpec
    {
        public string? Faction;
        public string? Color1;
        public string? Color2;
        public string? Commander;
        public int? BannerIcon;
        /// <summary>The whole banner the screen showed (MusterBannerDesign.Code); preferred over the colours and emblem.</summary>
        public string? BannerCode;
        public List<UnitSpec> Units = new List<UnitSpec>();
    }

    public sealed class UnitSpec
    {
        public string Troop = "";
        public int Count;
        public bool Banner;
        public bool Captain;
        public bool Horn;
    }

    public List<string> Validate()
    {
        var problems = new List<string>();
        int total = 0;
        foreach (var (name, side) in new[] { ("player", Player), ("enemy", Enemy) })
        {
            if (side == null) continue;
            if (side.Commander != null && MBObjectManager.Instance.GetObject<BasicCharacterObject>(side.Commander) == null)
                problems.Add($"{name}: no commander '{side.Commander}'");
            if (side.Faction != null && MBObjectManager.Instance.GetObject<BasicCultureObject>(side.Faction) == null)
                problems.Add($"{name}: no faction '{side.Faction}'");
            foreach (var u in side.Units ?? new List<UnitSpec>())
            {
                var who = MBObjectManager.Instance.GetObject<BasicCharacterObject>(u.Troop ?? "");
                if (who == null) problems.Add($"{name}: no troop '{u.Troop}'");
                else if (who.IsHero && u.Count != 1) problems.Add($"{name}: {u.Troop} is a hero, there is only one of him");
                int least = MinimumCount(u.Banner, u.Captain, u.Horn);
                if (u.Count < least) problems.Add($"{name}: {u.Troop} has {u.Count}, needs at least {least} for its roles");
                total += Math.Max(0, u.Count);
            }
        }
        if (total > MaxTroops) problems.Add($"{total} troops, the engine takes at most {MaxTroops}");
        return problems;
    }

    /// <summary>
    /// The fewest men a unit can have for its roles. The bearer and the horn blower are soldiers of the unit, but the
    /// captain is another troop that takes one of the number, so the unit keeps at least one soldier of its own.
    /// </summary>
    public static int MinimumCount(bool banner, bool captain, bool horn) =>
        (captain ? 1 : 0) + Math.Max(1, (banner ? 1 : 0) + (horn ? 1 : 0));

    public static uint? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var s = hex!.Trim().TrimStart('#');
        if (s.Length == 6) s = "FF" + s;
        return uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : (uint?)null;
    }
}
