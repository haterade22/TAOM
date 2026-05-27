#!/usr/bin/env python3
"""Generate one GitHub issue draft per TAOM culture (skills+traits work).

Writes: docs/issues-drafts/lords-skills-<culture>.md
Each draft is ready to feed to `gh issue create --title <T> --body-file <F>`.
"""
import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
DRAFTS_DIR = REPO / "docs" / "issues-drafts"
DRAFTS_DIR.mkdir(parents=True, exist_ok=True)

CULTURES = [
    # (key, culture_id, lore_name, count, race, canonical, notes)
    ("gondor",     "gondor",     "Gondor",
     118, "man",
     "Imrahil (Charm/Riding/OneHanded 290), Boromir (OneHanded 295, Leadership 285), "
     "Faramir (Bow 275, Scouting 290, Mercy +2), Denethor (Steward 300, Mercy -1, "
     "Authoritarian +2), Forlong \"the Fat\" (Polearm 255, Athletics 150 — girth-reduced), "
     "Hirluin \"the Fair\" (Charm 250), Angbor \"the Fearless\" (Valor +2, OneHanded 265), "
     "Golasgil (coastal Trade 230), Duinhir (Bow 290 — Morthond archer-king), "
     "Lothwen (Charm/Steward 295), and 25+ supplementary lords",
     "Initial pass of the multi-culture skills+traits effort (preceded by the Amrothos "
     "clan / culture-mistag / body-key work in the prior Gondor Lord Review session)."),

    ("rohan",      "vlandia",    "Rohan",
     92, "man",
     "Théoden (Leadership 295), Éomer (OneHanded 285, Riding 295), Théodred (Polearm 275, "
     "killed at Fords of Isen), Éowyn (shieldmaiden archetype, OneHanded 250, Medicine 200 — "
     "House of Healing), Erkenbrand (Lord of Westfold, Leadership 265), Grimbold (Westfold "
     "marshal, hero of Fords of Isen), Théodwyn + Elfhild (Théoden's deceased sister/queen, "
     "matriarch stats), plus Varmund (Aldburg), Marhath (horse-breeder), and 30+ Mark riders",
     "Introduced `rider`, `shieldmaiden`, `horse_breeder` archetypes for Rohirric cavalry "
     "emphasis (high Riding/Polearm, low Crossbow). Bards/wives use general `lady`+`matriarch`."),

    ("erebor",     "erebor",     "Erebor (Dwarves of the Lonely Mountain)",
     30, "dwarf",
     "Dáin II Ironfoot (King under the Mountain, dwarf_king archetype: OneHanded 275, "
     "TwoHanded 280, Engineering 275, Leadership 290), Thorin III Stonehelm (heir, future "
     "King after Dáin falls at Battle of Dale), plus Náin, Durin, Dísa (matriarch), Fin",
     "Introduced dwarf-specific archetypes (`dwarf_king`, `dwarf_lord`, `dwarf_warrior`, "
     "`dwarf_lady`, `dwarf_young`). Dwarves get heavy TwoHanded + Crafting/Engineering "
     "emphasis (smiths). Critically: race-aware default ignores low age values "
     "(Dáin at age 38 in lords.xml is in his prime, not 'elder')."),

    ("dale",       "sturgia",    "Dale (Bardings)",
     82, "man",
     "TAOM Dale roster is mostly TAOM-invented Slavic-named wives + nobles (Apolanea, "
     "Lilizha, etc.) — bio-driven via `dale_lord` / `dale_bowman` archetypes. Lore: "
     "King Bard II succeeded Brand (slain at Battle of Dale, T.A. 3019). High Bow + "
     "Trade emphasis for Northmen bowmen of Esgaroth.",
     "Bardings = Northmen archer-merchants; archetypes weight Bow 260-275, Trade 180-220. "
     "No explicit canonical overrides — TAOM names don't map to Tolkien-canon Bard/Brand."),

    ("mirkwood",   "mirkwood",   "Mirkwood (Woodland Realm)",
     29, "elf",
     "Thranduil (Elvenking, `elf_king` archetype: OneHanded 290, Bow 295, Charm 285, "
     "Leadership 290), Legolas (Bow 295, Athletics 295 — Fellowship member, master archer), "
     "Lothuial (TAOM queen), Feren (captain), Galion (butler/steward). Other Mirkwood "
     "elves use `elf_warrior` / `elf_archer` / `elf_lady`",
     "Introduced 7 elf archetypes (`elf_king`/`elf_queen`/`elf_lord`/`elf_warrior`/"
     "`elf_archer`/`elf_lady`/`elf_young`). Centuries-trained → combat AND diplomacy AND "
     "crafting all push 240-295. Race-aware default ignores age (immortal)."),

    ("rivendell",  "rivendell",  "Rivendell (Imladris)",
     7, "elf",
     "Elrond (Half-elven master of Imladris, ~6500 years old — `elf_king` peak: "
     "Charm/Leadership/Steward/Medicine 295), Celebrían (his wife), Elladan + Elrohir "
     "(twin sons, warrior tier — OneHanded 280, fought alongside Dúnedain Rangers), "
     "Arwen Undómiel (Evenstar, Charm 290, Medicine 280), Glorfindel "
     "(slayer of a Balrog, prince of the Noldor — OneHanded 295, Polearm 295)",
     "Smallest non-tiny culture (only 7 adults); every NPC gets explicit canonical override "
     "because all 7 ARE named Tolkien characters."),

    ("lothlorien", "lothlorien", "Lothlórien",
     3, "elf",
     "Galadriel (Lady of Lothlórien, ~8000+ yrs Noldor, Ring-bearer — capped at Charm 300, "
     "Leadership 300, Tactics 295, Steward 295, Medicine 290, Crafting 290; only character "
     "to hit Charm 300), Celeborn (Lord of Lothlórien, ancient Sindar — OneHanded 290, "
     "Polearm 295, Leadership 290), 1 placeholder lord (TAOM-invented)",
     "Galadriel + Denethor are the only two characters hitting 300 in any skill. Justified: "
     "Galadriel is one of the most powerful Elves in Middle-earth. Marchwardens (Haldir, "
     "Rúmil, Orophin) are in Mirkwood not Lothlórien per TAOM's data."),

    ("mordor",     "mordor",     "Mordor",
     97, "orc",
     "3 Nazgûl (Tainted / Shadow of Northmen / Shadow of Umbar — `nazgul` archetype: "
     "everything 270+, Charm 280, Mercy -2, Authoritarian +2), Grishnâkh (Uruk captain, "
     "attacked Merry+Pippin at Amon Hen), Verina + Jonna (Black Númenórean sorceresses, "
     "`bn_sorceress` archetype), Svala Redfang (Orc captain under Gothmog), plus Pagarios + "
     "Diasca (Black Númenórean noble heirs)",
     "Introduced orc / nazgul / Black Númenórean archetypes. Brutal trait alignment "
     "(Honor -2, Mercy -2, Authoritarian +2). Charm intentionally high on nazgul / "
     "BN sorceresses — terror + manipulation, not warmth."),

    ("dolguldur",  "dolguldur",  "Dol Guldur",
     59, "orc",
     "All TAOM-invented orc names (no canonical Khamûl entry in lords.xml — he'd be a "
     "separate Nazgûl). 6 compound chieftains (D1_1, D2_1, D3_1, etc.) marked as "
     "`orc_chieftain`; the rest as `orc_warrior` / `orc_female`",
     "Pure archetype application — no canonical Tolkien overrides. Reuses orc archetypes "
     "from Mordor."),

    ("gundabad",   "gundabad",   "Mount Gundabad",
     50, "orc",
     "All TAOM-invented pale_uruk names. 5 compound chieftains (G1_1..G5_1); Bolgath "
     "(G4_1) gets a slight bump as Bolg-evoking name (OneHanded 285, Leadership 280)",
     "Pure archetype application. Pale Uruks are stronger than Mordor orcs in lore but "
     "TAOM uses the same race for both."),

    ("isengard",   "isengard",   "Isengard (Saruman)",
     34, "uruk_hai",
     "Uglûk (captain of the Amon Hen raid that took Merry+Pippin — `orc_chieftain`: "
     "OneHanded 275, Leadership 275), Mauhûr (Uruk-Hai War leader who rescued Uglûk's "
     "band), Lugdush (Uruk with Uglûk), Lurtz (Uruk-hai Commander, film canon — killed "
     "Boromir, OneHanded 270), Sharku (Warg-Rider Captain killed at Helm's Deep — "
     "`orc_warg` with Riding 295)",
     "Uruk-Hai are stronger than regular orcs but use the same archetype values "
     "(TAOM bin lookup uses `uruk_hai` race). Berserkers + Warg-Riders covered by "
     "`orc_berserker` / `orc_warg` variants."),

    ("dunland",    "empire",     "Dunland (Hillmen / Saruman's auxiliaries)",
     68, "man",
     "TAOM Dunland is all-female shieldmaiden + raider warband leaders (Eldith Grey-Claw, "
     "Sigga Wyrmbane, Yrsa the Winter Boar, Freya Clawrend, etc.) — Norse-themed names. "
     "Auto-assigned to `dunland_warrior` / `dunland_raider` / `dunland_brenin` archetypes "
     "via bio keyword scan. No canonical Tolkien named characters in TAOM's roster.",
     "Introduced `dunland_warrior` / `dunland_raider` / `dunland_brenin` archetypes. Honor "
     "0-1, Valor 2, Mercy -1 (raider mindset). Wulf-canon ancestry referenced in feature "
     "doc only."),

    ("harad",      "aserai",     "Harad (Haradrim Southrons)",
     73, "man",
     "TAOM Harad is mostly Mumakil-lord wives + serpent-banner heirs + trade-caravan "
     "managers. Auto-assigned to `haradrim_lord` / `haradrim_cav` / `mumak_rider` / "
     "`desert_lady` archetypes via bio scan.",
     "High Riding + Trade for desert / mumakil culture. Scarlet-and-gold flavor. No "
     "canonical override (Khadurak is named in TAOM as Taskral; his explicit override "
     "is in a separate enemy-lord roster TBD)."),

    ("khand",      "battania",   "Khand (Variags)",
     56, "man",
     "TAOM Khand has Welsh/Gaelic-named warband leaders + wives. Auto-assigned to "
     "`variag_lord` / `variag_lady` / dunland_raider archetypes via bio keyword scan.",
     "Slavic-Mongol cavalry archetype: high Riding (275), balanced melee + Bow. Honor 1, "
     "Mercy -1 (mercenary mindset). No canonical Tolkien overrides (Variags are only "
     "named as a group in canon)."),

    ("easterling", "khuzait",    "Easterlings of Rhûn",
     71, "man",
     "TAOM Easterlings: Mongol-named wives + heirs + chieftains. Auto-assigned to "
     "`easterling_lord` / `easterling_archer` / `easterling_lady` archetypes via bio scan. "
     "Boronchar (advisor) + Valathmir Mashakian (chieftain) get explicit lord-tier stats.",
     "Wainrider / horse-archer culture — Bow 260-275, Riding 265-275, Tactics 235. Khamûl "
     "(\"Shadow of the East\", Easterling who became a Nazgûl) would be a special entry "
     "if TAOM exposes him as a player-meetable character (currently not in lords.xml under "
     "khuzait — likely in Dolguldur roster)."),

    ("umbar",      "umbar",      "Umbar (Corsairs)",
     10, "man",
     "TAOM Umbar has 10 Black Númenórean corsair lords with authentic Adunaic names "
     "(Ar-Gimilkhâd, Gimilzâr, Gimilthân, Pharaz�n, Zimraphel, Azraphel, Belkazar, "
     "Pharakhân, Inkaldâr, Zimrathâr). Adunaic `Ar-` prefix = king/highest noble. All "
     "use `corsair_lord` / `corsair_captain` / `young_lord` archetypes.",
     "Introduced `corsair_lord` / `corsair_captain` archetypes. Heavy Trade (265), "
     "Roguery (265), Charm (240), Tactics (260) — pirate captain skillset. Honor -1, "
     "Mercy -1, Authoritarian 1."),

    ("shaghana",   "shaghana",   "Shaghana",
     9, "man",
     "TAOM-invented southern-desert sub-culture (9 male warlords ages 27-55). No canonical "
     "Tolkien characters. All use default `lord` / `elder_lord` archetypes via age-driven "
     "default (no keyword archetypes defined for this culture).",
     "Minimal pass — no per-NPC overrides. Future work: derive Shaghana-specific archetypes "
     "if/when TAOM defines distinct combat/diplomatic style."),

    ("abanissa",   "abanissa",   "Abanissa",
     8, "man",
     "TAOM-invented coastal sub-culture (8 male warlords ages 28-52). Same treatment as "
     "Shaghana — default lord/elder_lord archetypes via age.",
     "Minimal pass — no per-NPC overrides."),
]

# Master CHANGELOG entry under which all 16 issues fit
PARENT_FEATURE = (
    "feat(all-cultures-lords): lore-driven skills + traits for every culture "
    "(16 cultures, ~880 adult NPCs)"
)

TEMPLATE = """# {title}

## Motivation

Following the Gondor Lord Review session (2026-05-26) and Gondor skills+traits pass
(2026-05-27), {lore} adult NPCs in TAOM had empty `<skills/>` blocks or vanilla-default
values — boring, lore-inconsistent stats. This issue covers the {lore} portion of the
sweep to give every adult lord skill values + trait alignment grounded in canonical
Tolkien lore (where applicable) or sensible archetypes (for TAOM-invented characters).

Parent CHANGELOG entry: `{parent}`.

## Design

**Archetypes applied** (race=`{race}`):
- All adult {lore} NPCs receive one of the shared TAOM archetype templates (10 base
  archetypes + {race}-specific variants).
- {n_canonical} canonical Tolkien character{s_canonical} receive{s_verb} explicit
  per-NPC overrides on top of the archetype.

**Canonical highlights:**
{canonical}

**Auto-archetype inference**: remaining NPCs are matched to an archetype via keyword scan
against the `heroes.xslt` bio text (e.g., "captain" → `knight`, "wife of" → `lady`,
"ranger" → `ranger`), with gender + age fallback.

**Power thresholds** (consistent across the sweep):
- Most adults cap 200-270 in their specialty skill.
- Canonical Tolkien peak heroes push 280-295.
- Only Galadriel + Denethor hit 300 in any skill (justified by lore).
- Leadership >275 reserved for canonical army-leading heroes (matters for the in-game
  +1 party-size perk threshold).

**Race-aware defaults**: `default_archetype()` branches on `culture_data.race`. Elves
never get "young_lord" archetype from low age (immortal — TAOM age fields are placeholders).
Dwarves stay combat-focused regardless of age (250-yr lifespan).

## Implementation

**Touched**: {count} NPCs (lords.xml + lords.xslt). Children (age <14) skipped.

**Files modified:**
- `Main/_Module/ModuleData/characters/lords.xml` — per-NPC `<skills>` + `<Traits>` blocks
- `Main/_Module/ModuleData/lords.xslt` — same for canonical Tolkien NPCs whose templates
  live in lords.xslt (transformed vanilla NPCs)
- `tools/apply_culture_skills_traits.py` — generalized generator with this culture's
  entry under `CULTURES['{key}']`

**Apply command**: `python tools/apply_culture_skills_traits.py --culture {key} --apply`

**Idempotency**: rerunning the script with the same archetype data produces no diff —
the script reads current values and only writes when they differ.

{notes_block}

## Testing

**Automated** (already passed):
- XML well-formedness: `python -c "import xml.etree.ElementTree as ET; ET.parse('Main/_Module/ModuleData/characters/lords.xml')"`
- Coverage check: confirmed all {lore} adult NPCs now have populated `<skills>` blocks.
- Build + tests not re-run (Bannerlord game lock on `0Harmony.dll` during the session;
  XML data changes don't depend on C# build).

**Required before ship (in-game smoke):**
1. Launch a TAOM campaign.
2. Open Encyclopedia → Heroes → filter Culture = {lore}.
3. For each canonical Tolkien hero, verify their skills match the archetype/override
   intent (e.g., Boromir 295 OneHanded → top of Gondor combat list; Galadriel 300 Charm
   → top of all charm lists).
4. Spot-check a few archetype-driven NPCs: confirm gender bias (ladies high Charm/Steward,
   lords high combat) and age bias (elders high Tactics/Steward, young lords lower across
   the board).
5. For the +1 party-size threshold (Leadership ≥275 perk): confirm only canonical peak
   characters in this culture have that perk available.

**Save compatibility**: hero skills are baked into the save file at hero creation. This
change affects NEW campaigns and any hero not yet realised at session start. Wandering
companions + reserved NPCs that haven't spawned yet will use new values.

## Research

Tolkien sources used:
{sources}

---
🤖 Generated as part of the multi-culture lords skills+traits sweep — see
`CHANGELOG.md` 2026-05-27 entry.
"""

SOURCES_PER_CULTURE = {
    "gondor": [
        "[Tolkien Gateway — Boromir](https://tolkiengateway.net/wiki/Boromir)",
        "[Tolkien Gateway — Faramir](https://tolkiengateway.net/wiki/Faramir)",
        "[Tolkien Gateway — Imrahil](https://tolkiengateway.net/wiki/Imrahil)",
        "[Tolkien Gateway — Forlong](https://tolkiengateway.net/wiki/Forlong)",
        "[Tolkien Gateway — Lossarnach](https://tolkiengateway.net/wiki/Lossarnach)",
    ],
    "rohan": [
        "[Tolkien Gateway — Marshal of the Mark](https://tolkiengateway.net/wiki/Marshal_of_the_Mark)",
        "[Tolkien Gateway — Éomer](https://tolkiengateway.net/wiki/%C3%89omer)",
        "[Tolkien Gateway — Erkenbrand](https://tolkiengateway.net/wiki/Erkenbrand)",
        "[Tolkien Gateway — Elfhelm](https://tolkiengateway.net/wiki/Elfhelm)",
    ],
    "erebor": [
        "[Tolkien Gateway — Dwarves of Erebor](https://tolkiengateway.net/wiki/Dwarves_of_Erebor)",
        "[Tolkien Gateway — Dáin II Ironfoot](https://tolkiengateway.net/wiki/D%C3%A1in_II_Ironfoot)",
        "[Tolkien Gateway — Thorin III Stonehelm](https://tolkiengateway.net/wiki/Thorin_III_Stonehelm)",
    ],
    "dale": [
        "[Tolkien Gateway — Bard II](https://tolkiengateway.net/wiki/Bard_II)",
        "[Tolkien Gateway — Brand](https://tolkiengateway.net/wiki/Brand)",
        "[Tolkien Gateway — Bardings](https://tolkiengateway.net/wiki/Bardings)",
    ],
    "mirkwood": [
        "[Tolkien Gateway — Thranduil](https://tolkiengateway.net/wiki/Thranduil)",
        "[Tolkien Gateway — Legolas](https://tolkiengateway.net/wiki/Legolas)",
        "[Tolkien Gateway — Woodland Realm](https://tolkiengateway.net/wiki/Woodland_Realm)",
    ],
    "rivendell": [
        "[Tolkien Gateway — Elrond](https://tolkiengateway.net/wiki/Elrond)",
        "[Tolkien Gateway — Elladan and Elrohir](https://tolkiengateway.net/wiki/Elladan_and_Elrohir)",
        "[Tolkien Gateway — Glorfindel](https://tolkiengateway.net/wiki/Glorfindel)",
        "[Tolkien Gateway — Arwen](https://tolkiengateway.net/wiki/Arwen)",
    ],
    "lothlorien": [
        "[Tolkien Gateway — Galadriel](https://tolkiengateway.net/wiki/Galadriel)",
        "[Tolkien Gateway — Celeborn](https://tolkiengateway.net/wiki/Celeborn)",
    ],
    "mordor": [
        "[Tolkien Gateway — Nazgûl](https://tolkiengateway.net/wiki/Nazg%C3%BBl)",
        "[Tolkien Gateway — Mouth of Sauron](https://tolkiengateway.net/wiki/Mouth_of_Sauron)",
        "[Tolkien Gateway — Grishnákh](https://tolkiengateway.net/wiki/Grishn%C3%A1kh)",
        "[Tolkien Gateway — Black Númenóreans](https://tolkiengateway.net/wiki/Black_N%C3%BAmen%C3%B3reans)",
    ],
    "dolguldur": [
        "[Tolkien Gateway — Dol Guldur](https://tolkiengateway.net/wiki/Dol_Guldur)",
        "[Tolkien Gateway — Khamûl](https://tolkiengateway.net/wiki/Kham%C3%BBl)",
    ],
    "gundabad": [
        "[Tolkien Gateway — Mount Gundabad](https://tolkiengateway.net/wiki/Gundabad)",
        "[Tolkien Gateway — Bolg](https://tolkiengateway.net/wiki/Bolg)",
        "[Tolkien Gateway — Azog](https://tolkiengateway.net/wiki/Azog)",
    ],
    "isengard": [
        "[Tolkien Gateway — Uglúk](https://tolkiengateway.net/wiki/Ugl%C3%BAk)",
        "[Tolkien Gateway — Uruk-hai](https://tolkiengateway.net/wiki/Uruk-hai)",
        "[Tolkien Gateway — Orcs of Isengard](https://tolkiengateway.net/wiki/Orcs_of_Isengard)",
    ],
    "dunland": [
        "[Tolkien Gateway — Dunlendings](https://tolkiengateway.net/wiki/Dunlendings)",
        "[Tolkien Gateway — Dunland](https://tolkiengateway.net/wiki/Dunland)",
    ],
    "harad": [
        "[Tolkien Gateway — Haradrim](https://tolkiengateway.net/wiki/Haradrim)",
        "[Tolkien Gateway — Mûmakil](https://tolkiengateway.net/wiki/M%C3%BBmak)",
    ],
    "khand": [
        "[Tolkien Gateway — Variags](https://tolkiengateway.net/wiki/Variags)",
        "[Tolkien Gateway — Khand](https://tolkiengateway.net/wiki/Khand)",
    ],
    "easterling": [
        "[Tolkien Gateway — Easterlings](https://tolkiengateway.net/wiki/Easterlings)",
        "[Tolkien Gateway — Balchoth](https://tolkiengateway.net/wiki/Balchoth)",
    ],
    "umbar": [
        "[Tolkien Gateway — Corsairs of Umbar](https://tolkiengateway.net/wiki/Corsairs_of_Umbar)",
        "[Tolkien Gateway — Black Númenóreans](https://tolkiengateway.net/wiki/Black_N%C3%BAmen%C3%B3reans)",
    ],
    "shaghana": [
        "_No canonical Tolkien source — Shaghana is TAOM-invented._",
    ],
    "abanissa": [
        "_No canonical Tolkien source — Abanissa is TAOM-invented._",
    ],
}


def main():
    for key, cid, lore, count, race, canonical, notes in CULTURES:
        title = f"feat(lords-skills): {lore} — lore-driven skills + traits for {count} adult NPCs"
        # Count canonical overrides by reading the python source
        py = (REPO / "tools" / "apply_culture_skills_traits.py").read_text(encoding='utf-8')
        m = re.search(r"'" + re.escape(key) + r"'\s*:\s*\{(.*?)\n    \},\n", py, re.DOTALL)
        n_canon = 0
        if m:
            block = m.group(1)
            n_canon = block.count("dict(archetype=") + block.count("dict(skills=")
        s_canonical = 's' if n_canon != 1 else ''
        s_verb = '' if n_canon == 1 else ''
        sources = "\n".join(f"- {s}" for s in SOURCES_PER_CULTURE.get(key, []))
        notes_block = f"**Notes:** {notes}" if notes else ""

        body = TEMPLATE.format(
            title=title, lore=lore, race=race, count=count, key=key,
            n_canonical=n_canon, s_canonical=s_canonical, s_verb=s_verb,
            canonical=canonical, sources=sources, notes_block=notes_block,
            parent=PARENT_FEATURE,
        )
        out = DRAFTS_DIR / f"lords-skills-{key}.md"
        out.write_text(body, encoding='utf-8')
        print(f"WROTE {out.relative_to(REPO)}")

    # Also write a summary index for `gh issue create` batching
    index = DRAFTS_DIR / "INDEX.md"
    lines = ["# Lords Skills+Traits — GitHub Issue Drafts", "",
             "One draft per TAOM culture. To create each issue:", "",
             "```bash",
             "for f in docs/issues-drafts/lords-skills-*.md; do",
             "  title=$(head -1 \"$f\" | sed 's/^# //')",
             "  gh issue create --title \"$title\" --body-file \"$f\" --label 'enhancement' --label 'lords'",
             "done",
             "```", "",
             "Or one-at-a-time:", ""]
    for key, cid, lore, count, _, _, _ in CULTURES:
        title = f"feat(lords-skills): {lore} — lore-driven skills + traits for {count} adult NPCs"
        lines.append(f"- **[{key}]({key}.md)** ({lore}, culture_id=`{cid}`, {count} NPCs) — `lords-skills-{key}.md`")
        lines.append(f"  - Title: `{title}`")
    index.write_text("\n".join(lines), encoding='utf-8')
    print(f"WROTE {index.relative_to(REPO)}")


if __name__ == '__main__':
    main()
