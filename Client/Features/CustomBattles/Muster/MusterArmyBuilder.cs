using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.CustomBattles.Muster;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Client.Features.CustomBattles.Muster;

/// <summary>
/// Rebuilds the two Custom Battle sides exactly as the muster screen set them. CustomBattleHelper.GetCustomBattleParties
/// spreads the army-size slider over the chosen troops by formation percentages; this keeps each side's commander(s),
/// then adds every unit with its exact number, the captain taken out of the number. The map, siege engines, time and
/// season stay the screen's. The roles to hand out in battle go to <see cref="CustomBattleMuster.Plan"/>.
/// Boundary code (CustomBattleCombatant, client-only per ADR-013); game-tested per ADR-008.
/// </summary>
internal static class MusterArmyBuilder
{
    internal static void Apply(ref CustomBattleCombatant[] result)
    {
        CustomBattleMuster.Plan = new List<CustomBattleMuster.Role>();
        CustomBattleMuster.Applied = false;
        try
        {
            var spec = CustomBattleMuster.Pending;
            CustomBattleMuster.Pending = null;
            if (spec == null || result == null || result.Length < 2) return;
            var problems = spec.Validate();
            if (problems.Count > 0) { CustomBattleMuster.Log("spec refused: " + string.Join("; ", problems)); return; }
            result[0] = Rebuild(result[0], spec.Player, false);
            result[1] = Rebuild(result[1], spec.Enemy, true);
            CustomBattleMuster.Applied = true;
        }
        catch (Exception e) { CustomBattleMuster.Log("rebuild failed, the screen's armies stay: " + e); }
    }

    private static CustomBattleCombatant Rebuild(CustomBattleCombatant old, ArmySpec.SideSpec side, bool isEnemy)
    {
        if (old == null || side == null || side.Units == null || side.Units.Count == 0) return old!;
        var culture = side.Faction != null ? MBObjectManager.Instance.GetObject<BasicCultureObject>(side.Faction) : old.BasicCulture;
        var banner = BuildBanner(old, side, culture);
        var rebuilt = new CustomBattleCombatant(old.Name, culture, banner) { Side = old.Side };
        var chars = old.Characters.ToList();
        var keep = new List<BasicCharacterObject>();
        if (chars.Count > 0) keep.Add(chars[0]);
        if (old.General != null && old.General != chars.FirstOrDefault()) keep.Add(old.General);
        var general = old.General ?? keep.FirstOrDefault();
        // the enemy's leader belongs to the enemy's faction; the player's own character stays (the game ties the player to it)
        var chosen = side.Commander != null ? MBObjectManager.Instance.GetObject<BasicCharacterObject>(side.Commander) : null;
        if (isEnemy && keep.Count > 0 && chosen != null && chosen != keep[0])
        {
            if (general == keep[0]) general = chosen;
            keep[0] = chosen;
        }
        else if (isEnemy && keep.Count > 0 && keep[0].Culture != culture)
        {
            var cmd = CustomBattleMuster.CommanderOf(culture);
            if (cmd != null) { if (general == keep[0]) general = cmd; keep[0] = cmd; }
        }
        foreach (var k in keep) rebuilt.AddCharacter(k, 1);
        rebuilt.SetGeneral(general);
        var lines = new List<string>();
        foreach (var u in side.Units)
        {
            var troop = MBObjectManager.Instance.GetObject<BasicCharacterObject>(u.Troop);
            BasicCharacterObject? captain = null;
            string how = "";
            if (u.Captain) captain = CaptainResolver.Resolve(troop, out how);
            int soldiers = u.Count - (captain != null ? 1 : 0);
            rebuilt.AddCharacter(troop, soldiers);
            if (captain != null) rebuilt.AddCharacter(captain, 1);
            // a hero is one man and carries no role
            if (troop.IsHero) { lines.Add("hero " + troop.StringId); continue; }
            if (u.Banner || u.Horn || captain != null)
                CustomBattleMuster.Plan.Add(new CustomBattleMuster.Role { Side = old.Side, Unit = troop, Captain = captain, Banner = u.Banner, Horn = u.Horn });
            lines.Add($"{troop.StringId} x{u.Count} = {soldiers} soldiers" + (captain != null ? $" + captain {captain.StringId} [{how}]" : ""));
        }
        CustomBattleMuster.Log($"{(isEnemy ? "enemy" : "player")} side ({culture?.StringId}, {old.Side}, commander {keep.FirstOrDefault()?.StringId}): {rebuilt.NumberOfHealthyMembers} men: {string.Join("; ", lines)}");
        return rebuilt;
    }

    private static Banner BuildBanner(CustomBattleCombatant old, ArmySpec.SideSpec side, BasicCultureObject culture)
    {
        // the screen's banner exactly, the kingdom's own colours and layers included
        var exact = MusterBannerDesign.ExactCode(side.BannerCode);
        if (exact != null) return new Banner(exact);
        if (side.BannerCode != null) CustomBattleMuster.Log($"banner code '{side.BannerCode}' does not parse, the colours and emblem are used");
        var banner = old.Banner;
        if (side.Faction != null && culture != old.BasicCulture) banner = new Banner(culture.Banner, culture.Color, culture.Color2);
        // a banner takes only colours of the game's palette (Banner.ChangePrimaryColor ignores any other): snap to the nearest
        var c1 = BannerPalette.Nearest(ArmySpec.ParseColor(side.Color1));
        var c2 = BannerPalette.Nearest(ArmySpec.ParseColor(side.Color2));
        if (c1 != null || c2 != null)
        {
            banner = new Banner(banner);
            if (c1 != null) banner.ChangePrimaryColor(c1.Value);
            if (c2 != null) banner.ChangeIconColors(c2.Value);
        }
        if (side.BannerIcon != null)
        {
            banner = new Banner(banner);
            banner.SetIconMeshId(side.BannerIcon.Value);
        }
        return banner;
    }
}
