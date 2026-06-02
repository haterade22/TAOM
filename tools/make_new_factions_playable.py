#!/usr/bin/env python3
"""Make the 3 new factions playable in the CC faction map by replacing their
factions.json entries with full content (bonuses matching the authored cultural feats).

Goblins + Misty Mountain Orcs use their own new feats; Lindon (Culture.rivendell) mirrors
the Imladris card because it shares Rivendell's culture and therefore its feats — honest.

Preserves the rest of factions.json byte-for-byte (regex-replaces each entry block).
After running, run:  python tools/harvest_factionmap_strings.py
"""
import argparse, json, re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FJSON = ROOT / "Main" / "_Module" / "ModuleData" / "factionmap" / "factions.json"


def K(slug, field, default):
    return f"{{=taom_faction_{slug}_{field}}}{default}"


def bonus(slug, n, default, positive):
    return {"text": K(slug, f"bonus_{n}", default), "positive": positive}


def named(slug, kind, n, name, desc):
    return {"name": K(slug, f"{kind}_{n}_name", name),
            "description": K(slug, f"{kind}_{n}_desc", desc)}


ENTRIES = {
    "goblins_of_goblin_town": {
        "name": K("goblins_of_goblin_town", "name", "Goblins of Goblin Town"),
        "color": "#282820FF", "playable": True, "game_faction": "goblin", "side": "evil",
        "description": K("goblins_of_goblin_town", "desc",
            "The teeming goblin-warrens beneath the High Pass of the Misty Mountains. Since the fall of the Great Goblin the tribes have multiplied without number — vast, ill-armed swarms that win by sheer weight and breed faster than any foe can cull them. They run swiftest over the snows of their mountain home, but such hordes are ever ravenous."),
        "image": "faction_goblins_of_goblin_town",
        "traits": [K("goblins_of_goblin_town", "trait_0", "Goblin-town Warrens"),
                   K("goblins_of_goblin_town", "trait_1", "Spawn of the High Pass"),
                   K("goblins_of_goblin_town", "trait_2", "Mountain-pass Ambushers"),
                   K("goblins_of_goblin_town", "trait_3", "Numberless Swarms")],
        "bonuses": [
            bonus("goblins_of_goblin_town", 0, "+40% party size limit (Goblin Swarm)", True),
            bonus("goblins_of_goblin_town", 1, "+25% village volunteer respawn rate (Endless Spawn)", True),
            bonus("goblins_of_goblin_town", 2, "+10% party speed on snow (Tunnel-Runners)", True),
            bonus("goblins_of_goblin_town", 3, "+20% party food consumption (Ravenous Swarm)", False)],
        "special_units": [
            named("goblins_of_goblin_town", "su", 0, "Goblin-town Spearman", "Massed spear-fodder of the High Pass warrens."),
            named("goblins_of_goblin_town", "su", 1, "Cave-goblin Skirmisher", "Numberless bow-armed rabble of the tunnels."),
            named("goblins_of_goblin_town", "su", 2, "Goblin Brute", "Heavier shock-goblin of the deep warrens.")],
        "perks": [
            named("goblins_of_goblin_town", "perk", 0, "Goblin Swarm", "+40% party size and +25% village volunteer respawn — goblins are weak but numberless."),
            named("goblins_of_goblin_town", "perk", 1, "Tunnel-Runners", "+10% party speed on snow — goblins know every pass and tunnel of the Misty Mountains."),
            named("goblins_of_goblin_town", "perk", 2, "Ravenous Swarm", "Such a horde devours everything — +20% party food consumption.")],
        "strengths": [K("goblins_of_goblin_town", "strength_0", "Overwhelming numbers"),
                      K("goblins_of_goblin_town", "strength_1", "Cheap, endlessly-respawning hordes"),
                      K("goblins_of_goblin_town", "strength_2", "Fast over snow and mountain"),
                      K("goblins_of_goblin_town", "strength_3", "Swarm tactics bury stronger foes")],
        "weaknesses": [K("goblins_of_goblin_town", "weakness_0", "Individually weak troops"),
                       K("goblins_of_goblin_town", "weakness_1", "Ravenous — strains every supply line"),
                       K("goblins_of_goblin_town", "weakness_2", "Hated by all Free Peoples")],
        "difficulty": 5,
    },
    "goblins_of_blue_craig": {
        "name": K("goblins_of_blue_craig", "name", "Goblins of Blue Craig"),
        "color": "#38485AFF", "playable": True, "game_faction": "goblin", "side": "evil",
        "description": K("goblins_of_blue_craig", "desc",
            "A distinct goblin host of the Blue Mountains (Ered Luin) in the far west, hard by the Grey Havens of Lindon — a realm apart from their Misty Mountains cousins of Goblin Town. Same numberless, ill-armed swarms: weak alone, overwhelming in the mass, and ravenous on the march. They trouble the Elves of the western shore."),
        "image": "faction_goblins_of_blue_craig",
        "traits": [K("goblins_of_blue_craig", "trait_0", "Crags of Blue Craig"),
                   K("goblins_of_blue_craig", "trait_1", "Goblins of the Blue Mountains"),
                   K("goblins_of_blue_craig", "trait_2", "Bane of the Grey Havens"),
                   K("goblins_of_blue_craig", "trait_3", "Numberless Swarms")],
        "bonuses": [
            bonus("goblins_of_blue_craig", 0, "+40% party size limit (Goblin Swarm)", True),
            bonus("goblins_of_blue_craig", 1, "+25% village volunteer respawn rate (Endless Spawn)", True),
            bonus("goblins_of_blue_craig", 2, "+10% party speed on snow (Tunnel-Runners)", True),
            bonus("goblins_of_blue_craig", 3, "+20% party food consumption (Ravenous Swarm)", False)],
        "special_units": [
            named("goblins_of_blue_craig", "su", 0, "Crag Goblin Skirmisher", "Numberless bow-armed rabble of the high crags."),
            named("goblins_of_blue_craig", "su", 1, "Blue Craig Brute", "Heavier shock-goblin of the warrens."),
            named("goblins_of_blue_craig", "su", 2, "Goblin Spear-thrall", "Cheap massed spear-fodder.")],
        "perks": [
            named("goblins_of_blue_craig", "perk", 0, "Goblin Swarm", "+40% party size and +25% village volunteer respawn — weak but numberless."),
            named("goblins_of_blue_craig", "perk", 1, "Tunnel-Runners", "+10% party speed on snow — goblins know every crag and tunnel."),
            named("goblins_of_blue_craig", "perk", 2, "Ravenous Swarm", "The horde devours everything — +20% party food consumption.")],
        "strengths": [K("goblins_of_blue_craig", "strength_0", "Overwhelming numbers"),
                      K("goblins_of_blue_craig", "strength_1", "Cheap, endlessly-respawning hordes"),
                      K("goblins_of_blue_craig", "strength_2", "Fast over snow and crag"),
                      K("goblins_of_blue_craig", "strength_3", "Swarm tactics bury stronger foes")],
        "weaknesses": [K("goblins_of_blue_craig", "weakness_0", "Individually weak troops"),
                       K("goblins_of_blue_craig", "weakness_1", "Ravenous — strains every supply line"),
                       K("goblins_of_blue_craig", "weakness_2", "Hated by all Free Peoples")],
        "difficulty": 5,
    },
    "kingdom_of_moria": {
        "name": K("kingdom_of_moria", "name", "Orcs of the Misty Mountains"),
        "color": "#604830FF", "playable": True, "game_faction": "mistymountainorcs", "side": "evil",
        "description": K("kingdom_of_moria", "desc",
            "The orc-host that holds the deep places of the Misty Mountains — the black pits of Moria, won from the Dwarves, and the cold peaks from Caradhras to Methedras. Larger and better-armed than the goblin rabble of the High Pass, they muster vast war-hosts for almost no influence and march hungry but tireless across the snows."),
        "image": "faction_kingdom_of_moria",
        "traits": [K("kingdom_of_moria", "trait_0", "Lords of Khazad-dûm"),
                   K("kingdom_of_moria", "trait_1", "Orcs of the Deep"),
                   K("kingdom_of_moria", "trait_2", "Plunderers of the Dwarrowdelf"),
                   K("kingdom_of_moria", "trait_3", "Cold-peak War-host")],
        "bonuses": [
            bonus("kingdom_of_moria", 0, "-40% army influence cost (Orc Horde)", True),
            bonus("kingdom_of_moria", 1, "+30% party size limit (Mountain Host)", True),
            bonus("kingdom_of_moria", 2, "+10% party speed on snow (Mountain-Bred)", True),
            bonus("kingdom_of_moria", 3, "+15% party food consumption (Hungry Host)", False)],
        "special_units": [
            named("kingdom_of_moria", "su", 0, "Moria Orc Warrior", "Heavier orc infantry of the deep halls."),
            named("kingdom_of_moria", "su", 1, "Moria Pit-archer", "Bow-orc of the deep halls and high tunnels."),
            named("kingdom_of_moria", "su", 2, "Black Pit Marauder", "Raider bred in the dark of Khazad-dûm.")],
        "perks": [
            named("kingdom_of_moria", "perk", 0, "Orc Horde", "Army recruitment costs 40% less influence — the orc-host needs no consensus to march."),
            named("kingdom_of_moria", "perk", 1, "Mountain Host", "+30% party size and +10% speed on snow — large war-hosts bred to the cold peaks."),
            named("kingdom_of_moria", "perk", 2, "Hungry Host", "A war-host of this size eats greedily — +15% party food consumption.")],
        "strengths": [K("kingdom_of_moria", "strength_0", "Vast cheap war-hosts"),
                      K("kingdom_of_moria", "strength_1", "Almost no influence to muster armies"),
                      K("kingdom_of_moria", "strength_2", "Snow-marching mobility"),
                      K("kingdom_of_moria", "strength_3", "Hold the deep places of Moria")],
        "weaknesses": [K("kingdom_of_moria", "weakness_0", "Weaker troops than Men or Elves"),
                       K("kingdom_of_moria", "weakness_1", "Hungry hordes drain provisions"),
                       K("kingdom_of_moria", "weakness_2", "Universally hated by the Free Peoples")],
        "difficulty": 4,
    },
    "high_kingdom_of_lindon": {
        "name": K("high_kingdom_of_lindon", "name", "High Kingdom of Lindon"),
        "color": "#50A090FF", "playable": True, "game_faction": "rivendell", "side": "free",
        "description": K("high_kingdom_of_lindon", "desc",
            "The green land west of the Blue Mountains, last realm of the High Elves on the shores of Middle-earth. From the Grey Havens of Mithlond the white ships sail into the West. Círdan the Shipwright, eldest of the Eldar, keeps the fading light of the Noldor — wise, few, and each blade irreplaceable. Lindon shares the Noldor strength of Imladris."),
        "image": "faction_high_kingdom_of_lindon",
        "traits": [K("high_kingdom_of_lindon", "trait_0", "Círdan the Shipwright"),
                   K("high_kingdom_of_lindon", "trait_1", "The Grey Havens"),
                   K("high_kingdom_of_lindon", "trait_2", "Ships into the West"),
                   K("high_kingdom_of_lindon", "trait_3", "Last of the High Elves")],
        "bonuses": [
            bonus("high_kingdom_of_lindon", 0, "+35% army influence award (Elven Wisdom)", True),
            bonus("high_kingdom_of_lindon", 1, "+20% village hearth growth (The Last Homely House)", True),
            bonus("high_kingdom_of_lindon", 2, "+10% party speed in forest (Woodland Grace)", True),
            bonus("high_kingdom_of_lindon", 3, "-15% party food consumption (Elven Frugality)", True),
            bonus("high_kingdom_of_lindon", 4, "+0.5 settlement loyalty per day", True),
            bonus("high_kingdom_of_lindon", 5, "+25% army influence cost (Elven Pride)", False)],
        "special_units": [
            named("high_kingdom_of_lindon", "su", 0, "Noldor Blade-master", "Legendary first-age veteran of the Eldar."),
            named("high_kingdom_of_lindon", "su", 1, "Falathrim Mariner-warden", "Sea-elf guardian of the Havens."),
            named("high_kingdom_of_lindon", "su", 2, "Mithlond Sentinel", "Heavy Noldor guardian of the Grey Havens.")],
        "perks": [
            named("high_kingdom_of_lindon", "perk", 0, "Elven Wisdom", "+35% influence awarded on army success — the Wise know how to lead."),
            named("high_kingdom_of_lindon", "perk", 1, "The Last Homely House", "+20% village hearth growth and -15% party food consumption."),
            named("high_kingdom_of_lindon", "perk", 2, "Woodland Grace", "+10% party speed in forest and +0.5 settlement loyalty per day.")],
        "strengths": [K("high_kingdom_of_lindon", "strength_0", "Elite warriors of legend"),
                      K("high_kingdom_of_lindon", "strength_1", "Wise leadership reaps greater influence from victory"),
                      K("high_kingdom_of_lindon", "strength_2", "Frugal, self-sufficient hosts"),
                      K("high_kingdom_of_lindon", "strength_3", "Hearth-rich, loyal havens")],
        "weaknesses": [K("high_kingdom_of_lindon", "weakness_0", "Elven Pride raises influence costs"),
                       K("high_kingdom_of_lindon", "weakness_1", "Fading people — few replacements"),
                       K("high_kingdom_of_lindon", "weakness_2", "Cannot expand far from the Havens")],
        "difficulty": 2,
    },
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="write factions.json (default: dry-run)")
    args = ap.parse_args()
    text = FJSON.read_text(encoding="utf-8")
    for slug, obj in ENTRIES.items():
        val = json.dumps(obj, indent=2, ensure_ascii=False)
        val = val.replace("\n", "\n  ")  # indent continuation lines to entry depth
        block = f'  "{slug}": {val}'
        pat = r'  "' + re.escape(slug) + r'": \{.*?\n  \}'
        new_text, n = re.subn(pat, lambda _: block, text, count=1, flags=re.DOTALL)
        if n != 1:
            raise RuntimeError(f"could not replace entry {slug} (matched {n})")
        text = new_text
    json.loads(text)  # validate (never write invalid JSON)
    if not args.apply:
        print(f"DRY RUN — {len(ENTRIES)} entries would be made playable. Re-run with --apply to write.")
        return
    # repo-tracked file: git history is the backup (no .bak, unlike the external TAOM_Map settlements)
    FJSON.write_text(text, encoding="utf-8")
    print(f"Updated {len(ENTRIES)} faction entries to playable; factions.json valid JSON.")
    print("Now run: python tools/harvest_factionmap_strings.py")


if __name__ == "__main__":
    main()
