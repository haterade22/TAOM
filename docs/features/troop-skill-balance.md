# Troop Skill Balance

## Overview

TAOM keeps every troop's 8 combat skills (Athletics, Riding, OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing) on a single deterministic curve: a **per-(level, group) baseline** plus a **per-culture modifier**. Two tools own this: `tools/rebalance_troops.py` *writes* skills onto the curve, and `tools/analyze_troop_balance.py` *reads* the current state into a per-culture balance overview (HTML + markdown + JSON) without touching anything. This doc covers both, the cultural-modifier system, and the 2026-06-24 full-roster rebaseline.

## Why This Exists

- **Vanilla / ad-hoc behavior:** Troop skills are hand-authored per-`NPCCharacter`, so same-tier units drift apart between cultures and across authoring sessions. Content added after a balance pass (new troops, culture revamps) ships off-curve.
- **TAOM requirement:** Cross-culture parity at each tier (an L26 Gondor infantryman ≈ an L26 culture-X infantryman, shifted only by the culture's deliberate identity), with elven factions intentionally above the line and orc factions below it.
- **Without this:** Post-#212 elites shipped at *half* their tier's skills (e.g. `iron_hills_noble_ironbreaker` at OneHanded 90 where the curve calls for 325), and three cultures (`goblin`, `mistymountainorcs`, `dale`) had no faction identity at all.

## Architecture

### The formula (single source of truth)

`tools/rebalance_troops.py` defines the reference curve:

```
final_skill = BASELINE[group][level][skill] + CULTURAL_MODS[culture].get(skill, 0)
            (then weapon specialization, EQUIPMENT-DRIVEN since 2026-07-13:
             carries crossbow + no bow        -> swap Bow <-> Crossbow
             name has pike/spear/sword/axe/…  -> ±15 flavour shifts (name-based, unchanged)
             carries 2H + no polearm, Pol>2H  -> swap Polearm <-> TwoHanded)
            (then the monotonicity clamp, ROSTER-WIDE since 2026-08-30:
             child[skill] = max(child[skill], parent[skill]) over the whole upgrade DAG)
```

- **`GROUP_BASELINES`** — Infantry / Ranged / Cavalry / HorseArcher tables keyed by the 11 tier levels `{1,6,11,16,21,26,31,36,41,46,51}`. Troops at off-grid levels (e.g. L7, L13) are skipped (no reference).
- **`CULTURAL_MODS`**: per-culture skill deltas applied on top of baseline (the faction's identity). Keyed by the **filename culture** (`troops_<culture>.xml`), with two special cases in `detect_culture`: `iron_hills_*` ids (which live in the erebor file) → `iron_hills`, and `rhun_new` (the Rhûn file) → `rhun`. **Every culture file must have an entry.** Lindon had none until 2026-08-30, so the formula ran against it with a zero modifier: the first `--apply` after that gap opened would have stripped the high-elf tuning off all 30 Lindon troops (27 of the 30 have a Rivendell twin carrying identical skill values). A missing key does not error, it silently rebaselines a whole faction to the bare curve.
- **`SKIP_TROOP_IDS`**: troops excluded from the formula entirely (genuine non-humanoid creatures + hand-tuned bespoke mounts + the hand-tuned Iron Hills noble crossbow line). They still take part in the monotonicity clamp, which only ever raises, so a skip protects a hand-tune without letting it read backwards in the tree.
- **Militia** take the level-21 baseline whatever their real level, and are identified by the ids a culture **binds** to a militia slot in `taom_spcultures.xml` / `spcultures.xslt`, never by name. See "Militia are a binding, not a name" below.

### Equipment-driven weapon specialization (#340/#341, 2026-07-13)

Weapon detection was originally **name-keyword-only** (`crossbow`/`arbalest`/`naffatun` triggered the Bow↔Crossbow swap), which mis-statted every crossbowman named "Sharpshooter/Marksman/Scout/Sniper" (12 troops shipped Bow-top) and left two-hander troops named "Knight/Berserker/Champion" on the polearm-biased baselines (59 troops shipped Polearm-top — Cavalry L41 is 1H 310 / 2H 160 / Pol 340 by design). The fix:

- `taom_schema.build_item_class_registry(moduledata, game_modules)` maps item id → skill class. It must read **both** vanilla `<Item Type="…">` and Armory `<CraftedItem crafting_template="…">` — the entire install has zero `Type="TwoHandedWeapon"` items; every two-hander is a crafted item.
- `rebalance_troops.troop_weapon_classes(npc, item_classes)` collects the classes actually carried (weapon slots Item0–3).
- The Bow↔Crossbow swap fires iff crossbow-carried-and-no-bow (unambiguous: no troop carries both). The `naffatun` keyword is gone — it had wrongly swapped two javelin throwers.
- A total-preserving sanity post-pass swaps Polearm↔TwoHanded when a troop carries a two-hander, no polearm, and Polearm > TwoHanded. Idempotent; mixed carriers untouched; monotonicity unaffected (totals preserved).
- **The writer hard-fails without the game install** (`--game-modules`, default = the standard Steam path) rather than silently degrading to the name heuristic. The read-only analyzer degrades to name-only with a loud warning instead.
- Contract tests: `tools/tests/test_rebalance_equipment.py`.

**Known off-formula residuals** a `--dry-run` will always report: the hand-tuned `gondor_loss_noble{,_veteran,_sergeant,_warden,_captain}` line (5 troops, intentional; do not `--apply` over them without deciding their fate, tracked in #343).

The 34 Mordor/Morannon partial-skill-block troops are **resolved** (2026-08-30, #522). They reported CHANGED on every run and produced no byte change because the writer only rewrites values already present. `insert_missing_skill_entries` now adds the missing element, cloning the shape of the last entry in the same block so both the one-line `<skill id=… value=… />` form and the three-line form survive. This mattered beyond tidiness: `CharacterObject.GetSkillValue` returns 0 for an undeclared skill, so a partial block is a silent zero, and `mordor_uruk_skirmisher -> mordor_uruk_crossbow` read as Bow 130 to 0.

**Hand-tuned, protected via `SKIP_TROOP_IDS`** (these no longer appear as residuals because the tool skips them outright): `iron_hills_noble_scout` / `_sharpshooter` / `_veteran_sharpshooter`, Crossbow 175 / 225 / 275 as of 2026-07-30. The formula derives Crossbow from level and `CULTURAL_MODS['iron_hills']` alone, which gave the noble line exactly the regular `ironpass_*` line's values (130 / 170 / 205) — no edge in the only skill the branch specialises in. If the noble/regular split is ever expressed as a modifier rather than a hand-tune, remove these three ids and let the formula own them again.

**Deferred (#343):** 108 troops carrying only 1H weapons with Polearm strictly top (+46 exact ties) need a 3-way redistribution decision, not a mechanical pair swap.

`tools/analyze_troop_balance.py` **imports these tables verbatim** — it never re-derives the curve, so the "ideal" and the "writer" can never disagree. It compares each troop's actual skills to `calculate_skills(...)` and reports the delta.

### Component diagram

```
GROUP_BASELINES + CULTURAL_MODS  (rebalance_troops.py — the curve)
        |                              |
   rebalance_troops.py            analyze_troop_balance.py
   (WRITES skills via regex,      (READS, imports the curve,
    preserves all formatting)      emits HTML/MD/JSON overview)
        |                              |
  troops_*.xml (16 files)        tools/reports/troop-balance/REPORT.{html,md} + .json
```

## The cultural modifiers

Deltas on top of baseline. Elves run high (elite at everything); orcs run low; men sit mild-positive. Authored to keep same-tier cross-culture parity within band.

| Culture | Ath | Rid | 1H | 2H | Pol | Bow | Xbow | Thr | Identity |
|---|---|---|---|---|---|---|---|---|---|
| rivendell | +35 | +30 | +35 | +40 | +40 | +40 | +40 | +40 | High-elf elite (strongest) |
| **lindon** | **+35** | **+30** | **+35** | **+40** | **+40** | **+40** | **+40** | **+40** | **Rivendell twin, not a culture of its own (see below)** |
| mirkwood | +45 | +5 | +40 | +30 | +30 | +50 | +50 | +50 | Wood-elf archers |
| lothlorien | +35 | +25 | +30 | +25 | +30 | +35 | +35 | +35 | Galadhrim (dead entry — see below) |
| iron_hills | +10 | −5 | +15 | +20 | +20 | · | +5 | +10 | Elite dwarves |
| erebor | +10 | −20 | +10 | +20 | +10 | · | · | +10 | Dwarf heavy infantry |
| **dale** | **+5** | **−10** | **+5** | **+12** | **+25** | **+12** | **+12** | **−5** | **Men of the North — best non-elf polearm + bows** |
| dunland | +20 | −5 | +5 | +5 | · | · | · | +15 | Wild hillmen |
| gondor | +5 | +5 | +10 | +5 | +5 | · | · | −10 | Elite men, balanced |
| umbar | +10 | −15 | +10 | +5 | · | · | · | · | Corsairs |
| rhun | +5 | +18 | · | · | +15 | −10 | −10 | −5 | Easterling cavalry/pikes |
| rohan | −5 | +20 | · | · | +10 | −5 | −10 | +2 | Riders of Rohan |
| harad | · | +15 | +5 | −10 | −5 | +10 | · | · | Southron horse + archers |
| isengard | +10 | +5 | +10 | +15 | +15 | · | +10 | +10 | Uruk-hai, strong |
| **isengard_orthanc** | **+18** | **+5** | **+22** | **+22** | **+20** | · | **+12** | **+12** | **Orthanc guard — Saruman's elite, the best NON-elf line (net +111)** |
| **dolguldur** | **+12** | **−5** | **+15** | **+25** | **+18** | **−5** | **−5** | **+10** | **Elite dark uruks (~Isengard-tier, 2H-heavy)** |
| **mordor_uruk** | **+10** | **−5** | **+12** | **+18** | **+12** | · | · | **+5** | **Mordor Black Uruks — elite line, between Gundabad & Dol Guldur (Bow/Xbow baseline = real archers)** |
| gundabad | +5 | −5 | · | +10 | +5 | −10 | −10 | +5 | Mountain orcs, poor ranged |
| **mistymountainorcs** | **+5** | **−5** | · | **+5** | **+3** | **−10** | **−10** | **+5** | **Cheap orc swarm, just below Gundabad** |
| mordor | −5 | −5 | · | +5 | −5 | −5 | −5 | +5 | Weak orcs |
| **goblin** | **−10** | **−15** | **−8** | **−5** | **−8** | **+15** | **−15** | **−5** | **Throwaway melee swarm — but dangerous archers (Bow +15, above Dale)** |

(Bold = authored/changed in the 2026-06-24/25 rebaseline work.) `detect_culture` id-routes elite sub-lines to their own modifier so they aren't dragged onto their file's base culture curve: `iron_hills_*` (in the erebor file) → `iron_hills`, `mordor_uruk_*` Black Uruks → `mordor_uruk`, `orthanc_*` (Saruman's elite guard in the isengard file) → `isengard_orthanc`. `lothlorien` and `mistymountainorcs` are **dead entries** , Lothlórien fields no troops of its own (full Rivendell reskin: `basic_troop=imladris_recruit`, party templates point at `rivendell_*`), so its troops rebaseline as `rivendell`. Kept as documented intent. `mistymountainorcs` joined it on 2026-08-29 when the three goblin kingdoms were merged onto one tree: `detect_culture` has no id-route for `mistymountainorcs_*`, so the few troops carrying that prefix take the `goblin` modifier.

## The 2026-06-24 rebaseline

Goal: bring every troop back onto the curve after the post-#212 content drift, **without** retuning the baseline numbers themselves (Gondor already matches them exactly — they are the validated standard).

Tooling changes (`rebalance_troops.py`):
1. Authored 3 new modifiers (`goblin`, `mistymountainorcs`, `dale`), each independently proposed from the roster + lore and adversarially balance-checked against peer cultures.
2. Bumped `dolguldur` from near-neutral (net −5) to elite (net +65, 2H-heavy) so Sauron's uruks land at ~Isengard tier — strong enough to contest the bordering elf realms — instead of being nerfed to a weak curve.
3. Fixed the `rhun_new`→`rhun` key mismatch in `detect_culture` (the Easterling modifier had been silently un-applied).
4. Added `SKIP_TROOP_IDS` to exclude `cave_troll` + `harad_elephant_rider` from the formula.
5. Fixed a latent `UnicodeEncodeError` (a `Δ` char in the warning output crashed on Windows cp1252 stdout once enough >100 deltas appeared).

Result: **262 troops rewritten across 11 files** (the under-tuned cultures; dunland/harad/rivendell/rohan/umbar were already on-curve and untouched). The standout corrections were the #212 Erebor / Iron Hills nobles (e.g. `iron_hills_noble_ironbreaker` 580 → 1340). The mount-riders (warg/wolf) rebaselined as cavalry; the elephant rider + cave troll were preserved.

Verification: `validate_moduledata` PASS (zero broken refs); diff perfectly balanced (1,701 insertions / 1,701 deletions = skill-values-only, no structural change); overview outliers **186 → 14** (the 14 are benign Mordor partial-skill-block troops whose *present* skills are on-curve); **780 / 798 troops within ±25 of the formula** (up from 593).

## Upgrade monotonicity: no stat may go down

**The rule: no skill on an upgrade target may sit below the troop it upgrades from.** A player reads the troop tree as a ladder, so an upgrade that lowers a stat reads as a bug however it got there. Vanilla is looser (87 of its 195 edges shed a secondary skill, worst single drop -70), but TAOM had drops down to -200 and 5 edges where the total of all 8 skills fell, so the tighter rule is the one that ships.

`clamp_upgrade_monotonicity` runs after the formula and before the write, over the whole roster at once because a single topological order is what lets a raise propagate down a chain and lets formula-skipped troops lift their children. It spans two directories: all 682 edges inside `troops/` stay within their own file, but the 16 villager edges in `characters/npcs_*.xml` cross into it. It topologically orders the upgrade graph (871 nodes and 698 edges once the villager sources in `characters/npcs_*.xml` are included; acyclicity is asserted, not assumed) and sets `child[skill] = max(child[skill], parent[skill])`. Troops the formula skipped take part using their current values: they can lift a child and be lifted, and since a clamp only raises, a hand-tune is never reverted.

**Two modes, and picking the wrong one rebaselines the roster inside a bug fix.**

| Mode | Base for the clamp | Use when |
|---|---|---|
| `--apply` | the formula result | you actually want a rebaseline, and have reviewed the per-culture deltas |
| `--fix-monotonicity` | what is on disk | you are repairing upgrade ladders and nothing else |
| `--restat <id,id>` | forces the formula for those ids in clamp-only mode | a specific troop was misclassified and needs to come back onto the curve |
| `--dry-run` | (modifier) | preview EITHER mode without writing. It used to be a third mutually exclusive mode, which meant the clamp-only path could only be inspected by letting it write and then reading `git diff` |

`--fix-monotonicity` exists because `--apply` on this roster sweeps up 23 deliberately off-curve troops: the `gondor_loss_noble` line (#343, documented do-not-apply-over), the hand-authored Black Numenorean `mordor_num_*` line, the dwarf ram riders, and `mistymountainorcs_bolgs_ironfang` (which sits in the goblin file and has no `detect_culture` route, so it takes goblin modifiers). None of that belongs in a monotonicity fix. The tell during the 2026-08-30 pass was a per-file diff size: `troops_lindon.xml` came out at 478 lines where the clamp accounted for 30.

The cost is deliberate. A Ranged troop branching off an Infantry parent keeps the parent's Polearm, so group specialization blurs on branch upgrades. Two clamps produce a proficiency the troop cannot use (`mordor_uruk_crossbow` inherits Bow 130 from its bowman parent, which is exactly what vanilla does for `imperial_trained_archer -> imperial_crossbowman`; `sagarun_naffatun` inherits Crossbow 160 though it throws javelins). Both are inert on weapons the troop does not carry.

### `skill_template` beats an inline `<skills>` block outright

`BasicCharacterObject.Deserialize` (v1.4.8, `BasicCharacterObject.cs:337-358`) resolves
`skill_template` first and only calls `DefaultCharacterSkills.Init(childNode)` when that reference
came back **null**:

```csharp
if (childNode.Name == "Skills" || childNode.Name == "skills")
{
    if (mBCharacterSkills == null)      // only when skill_template did NOT resolve
        DefaultCharacterSkills.Init(objectManager, childNode);
}
```

So a character carrying both is declaring two different skill sets and the engine silently takes the
template. Until 2026-08-31 that described 44 militia troops, every one pointing at a vanilla
Calradian SkillSet that SandBoxCore registers: `rivendell_militia_spearman` was authored at 850 total
and delivered 215. None of the tooling knew the attribute existed, so `rebalance_troops.py` had been
rewriting the dead half on every balance pass and `analyze_troop_balance.py` had been reporting it as
the troop's real skills. 17 prison guards in `characters/npcs_*.xml` had the same shape (#523).

The templates are gone from `troops/` and from those prison guards, so the authored values now apply.
A character declaring both is an error in two places: `SKILL_TEMPLATE_SHADOWS_SKILLS` in
`taom_schema.py` and `SkillTemplate_NeverShadowsAnInlineSkillsBlock` in the C# suite. **A troop with
an empty `<skills>` block and a template is the legitimate shape** and is left alone; both gates skip
templated characters when judging an upgrade edge, because their real skills live in a SkillSet these
files cannot see.

### The graph spans two directories

Upgrade sources are not only in `troops/`. Each of the 15 `villager_<culture>` entries in
`characters/npcs_*.xml` upgrades into its culture's tier-1 troop, and the engine treats any character
with a non-empty `UpgradeTargets` array as upgradeable. Gating only `troops/` left 16 edges
unchecked, six of them regressing, while `validate_moduledata.py` reported PASS. Those villagers now
declare their skills explicitly, at exactly the values the vanilla template had been supplying, so
the data is self-describing and every gate can read the whole graph: **698 edges, 682 of them inside
`troops/` and 16 crossing in from `characters/`.** `rebalance_troops.py` loads the external sources
read-only through `load_external_sources()`; they seed the clamp and are never written.

### Militia are a binding, not a name

TAOM militia take the **level-21 baseline regardless of their actual level** so sieges and village defence stay costly (user direction, 2026-06-24). A militia promoting into another militia is therefore flat by design, and those 6 edges are the only exemption from the rule above.

Which troops are militia comes from what a culture **binds**: the 60 ids in `militia_troop` / `melee_militia_troop` / `ranged_militia_troop` / `*_elite_militia_troop` across `taom_spcultures.xml` and `spcultures.xslt`. Both encodings have to be read, a plain attribute and an `<xsl:attribute>` element; Dale, Dunland, Harad, Rhûn and Rohan use only the second. Comments are masked first, so a commented-out `<Culture>` block cannot widen the exemption, and the 60 ids are pinned by identity in the C# gate rather than by count (the old name rule produces the same set plus one, so a count-only assertion would not have caught a revert). The Ranged/melee split for choosing the L21 table comes from `default_group`.

Until 2026-08-30 this was a name substring (`militia` plus one of spearman/archer/veteran). It had exactly one false positive across 871 troops, and that one shipped the worst upgrade edge in the game: `gondor_ano_archer_militia` is a level-11 Anórien **line** troop, bound only in `taom_partyTemplates.xml`, so it wore level-21 stats and out-statted its own level-16 target on seven of its eight skills, -145 total (Riding was already equal on both). Same defect family as the name-based weapon detection replaced in #340/#341.

### What the analyzer still reports

`analyze_troop_balance.py` stays the read-only overview and stays total-based with a ±25 tolerance. It exempts militia-to-militia **edges** now, not militia troops: excluding the troops is how it printed "0 inversions" for two months while the -145 edge sat inside its own exclusion. Run the fixed check against the pre-fix data and it names the bug immediately.

Three within-culture level inversions are known and accepted, all Gondor Ranged: `gondor_ith_longbowman` and `gondor_mt_longbowman` (L36) out-total `gondor_brv_shadowbow` (L41) by 110, and `gondor_ith_moon_guard` (L46) out-totals `gondor_ithilien_ranger` (L51) by 45. These compare disconnected branches, and the whole gap is melee the longbowmen inherited from an infantry parent through the clamp. The higher-tier troop still leads on Bow in every case, and no player sees an upgrade get worse. Closing them means a Gondor balance pass, not a bug fix.

Ten upgrade edges end up exactly flat, which the gate allows because flat is not a decrease: the 6 militia pairs above, the two `*_knight_golden_flower` capstones into their `*_warden_gondolin`, and two more in the goblin and gundabad Ironfang lines. Those two capstones sit at level 51, which is already tier 10, the cap `TaomCharacterStatsModel` sets, so there is no grid step left to promote them into. They are equal-tier variant branches that differ by equipment and role. The `*_bolgs_ironfang` pair had the same shape at level 36 and was fixed properly, by moving them one step to level 41 (the T8 they are labelled as) and restatting.

**Per-skill enforcement lives in two gates**, not in the analyzer: `TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs` (runs in CI, no game install) and the `UPGRADE_SKILL_REGRESSION` error in `tools/taom_schema.py` (so `validate_moduledata.py` fails on it). `tools/tests/test_upgrade_skill_monotonicity.py` covers the gate, the clamp and the entry insertion on synthetic data.

## Key Files

| File | Purpose |
|------|---------|
| `tools/rebalance_troops.py` | Writes skills onto the curve. `GROUP_BASELINES` + `CULTURAL_MODS` + `SKIP_TROOP_IDS` + `detect_culture` + `militia_troop_ids` + `clamp_upgrade_monotonicity` + `insert_missing_skill_entries`. `--dry-run` / `--apply`. |
| `tools/analyze_troop_balance.py` | Read-only overview generator. Imports the curve from `rebalance_troops.py`; emits HTML/MD/JSON. `--outlier-threshold N` / `--stdout`. Never writes troop XML. |
| `tools/taom_schema.py` | `UPGRADE_SKILL_REGRESSION` and `SKILL_TEMPLATE_SHADOWS_SKILLS` (both ERROR), the `validate_moduledata.py` half of the gate. |
| `TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs` | 4 tests: no skill drops, all 8 skills declared, no template shadowing, militia exemption pinned by identity. Fails rather than going inconclusive when it cannot find ModuleData, since a data gate that cannot read its data has checked nothing. |
| `tools/tests/test_upgrade_skill_monotonicity.py` | 25 synthetic-data tests: the gate, the clamp, entry insertion, the fail-closed militia loader, and `SKILL_TEMPLATE_SHADOWS_SKILLS`. |
| `Main/_Module/ModuleData/troops/troops_*.xml` (×16) | The troop definitions (the data under management). |
| `Main/_Module/ModuleData/characters/npcs_*.xml` (×22) | The 15 `villager_*` upgrade sources that feed into the tier-1 troops, read-only to the writer. |
| `.claude/hooks/check-moduledata-validation.sh` | The commit gate. Its `--code` list is an ALLOWLIST, so a new ERROR check does not block until it is named there. |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` | Cross-referenced by the analyzer for weight-coverage gaps (army-composition weighting, not skills). |
| `tools/reports/troop-balance/REPORT.{html,md}` + `troop-balance.json` | Generated overview (gitignored under `tools/reports/`; regenerate any time). |

## How to review balance / run a rebaseline

> **Repairing an upgrade ladder is not a rebaseline.** If the goal is "an upgrade stopped lowering a stat", run `--fix-monotonicity` and skip the rest of this section. `--apply` is for a deliberate curve change.

1. **See where things stand (read-only):** `python tools/analyze_troop_balance.py --stdout`, then open `tools/reports/troop-balance/REPORT.html`. The heatmap parity matrix shows under-tuned (red) vs on-curve (green) vs elite (purple) per culture × tier; the data-quality section flags missing modifiers, dead entries, and excluded creatures.
2. **Adjust the curve:** edit `GROUP_BASELINES` (the power curve — affects every culture) or `CULTURAL_MODS[culture]` (one faction's identity) in `rebalance_troops.py`. To exclude a bespoke/creature troop, add its id to `SKIP_TROOP_IDS`.
3. **Preview:** `python tools/rebalance_troops.py --dry-run` — review the per-level deltas and the >100 warnings. Inspect **downward** changes especially (they may signal a too-weak modifier rather than over-tuned troops — that's how the Dol Guldur bump was found).
4. **Apply:** `python tools/rebalance_troops.py --apply` (regex-based — preserves all XML formatting/comments; only rewrites skill *values* already present in a `<skills>` block).
5. **Verify:** `python tools/validate_moduledata.py` (no broken refs and no `UPGRADE_SKILL_REGRESSION`), regenerate the overview (outliers should collapse), and **read `git diff --stat` per file, not just the total**. Expect a balanced insert/delete count (values-only) apart from any restored `<skill>` elements. A file that moves an order of magnitude more than you expected is the tool doing something you did not ask for: that is how the missing `CULTURAL_MODS['lindon']` entry was caught, mid-apply, at 478 lines against an expected 30.

Save-compat: troop skills are read from XML at agent spawn, so a rebaseline applies to all troops in new *and* existing saves with nothing serialized per-troop-type — no save migration needed.

## Changelog

- 2026-08-31: **Upgrades can no longer lower a stat (#522, #523).** Four independent causes, all
  live at once. Militia detected by name substring (one false positive in 871 troops,
  `gondor_ano_archer_militia`, worth -145 across seven of its eight skills on its own upgrade edge);
  `dg_warg_red_fang` tagged `HorseArcher` while carrying no ranged weapon, which handed it Bow 240
  and made its Cavalry child read as -200; the Ranged curve sitting below the Infantry curve on
  Polearm/TwoHanded; and `skill_template` silently discarding the authored `<skills>` block on 44
  militia troops, which had been shipping vanilla Calradian values while every tool reported the
  authored ones (#523). Fixed by binding-driven militia detection, the retag, a roster-wide
  monotonicity clamp, and removing the 44 dead templates. Widened the graph to include the 15
  villager sources in `characters/npcs_*.xml`, six of whose edges regressed unseen. Added the
  missing `CULTURAL_MODS['lindon']` entry, without which an `--apply` would strip the high-elf
  tuning off all 30 Lindon troops. Taught the writer to insert a missing `<skill>` element,
  resolving the 34 partial-block Mordor/Morannon troops, to bound itself to its own
  `NPCCharacter` element, to round-trip bytes, and to refuse to write XML that no longer parses.
  Added `--fix-monotonicity` and `--restat`, and made `--dry-run` a modifier so the clamp-only path
  can be previewed. Landed with:
  ```
  python tools/rebalance_troops.py --fix-monotonicity --restat gondor_ano_archer_militia,dg_warg_red_fang
  ```
  222 troops changed (251 skill values, 113 previously undeclared `<skill>` elements written, 44
  dead templates removed) across all 16 files, plus 32 NPCs in `characters/`; only the two
  reclassified troops end up lower than before. Gated by 4 C# data tests, two validator ERROR codes,
  and 25 Python contract tests, with the commit hook's code allowlist corrected so any of them can
  actually block. `check_monotonicity` now exempts militia-to-militia edges instead of militia
  troops, which is what hid the bug.

- 2026-06-25 — Orthanc elite: routed the `orthanc_*` guard line (Saruman's best, sword+shield, L26–41) to a new `isengard_orthanc` modifier (net +111) — now the best NON-elf line in the game, a clear step above the regular uruk-hai (+75), still far below elves. Only `troops_isengard.xml` changed (4 troops, +36 each). Same id-routing pattern as the Black Uruks. Surfaced by the 2026-06-25 deep review.
- 2026-06-24 — Black Uruk + goblin-archer follow-up: routed the `mordor_uruk_*` Black Uruk line to a new elite `mordor_uruk` modifier (net +52, between Gundabad & Dol Guldur — they were stuck on the weak Mordor-orc curve); raised goblin `Bow −15 → +15` so goblin archers are dangerous while the melee swarm stays throwaway (Uruks > Orcs > Goblins, with the goblin-archer exception). Only goblin + mordor files changed. Added a **level-monotonicity check** to `analyze_troop_balance.py` (upgrade-path + within-culture+group, militia-excluded): 0 inversions among professional troops.
- 2026-06-24 — Added the read-only `analyze_troop_balance.py` overview generator; full-roster rebaseline (262 troops / 11 files); authored `goblin`/`mistymountainorcs`/`dale` modifiers, bumped `dolguldur` to elite, fixed the `rhun_new`→`rhun` key mismatch, added `SKIP_TROOP_IDS` (cave_troll + elephant rider), fixed a latent `Δ` stdout crash.

## Related

- [troop-progression.md](troop-progression.md) — tier cap (6→10), volunteer cap, wage/recruitment tables.
- [troop-weight-system.md](troop-weight-system.md) — army-composition weighting (orthogonal to skills).
- [battle-balance.md](battle-balance.md) — tier power, casualty rates, blunt/cut ratios (GameModel side).
- [troop-tree-revamp.md](troop-tree-revamp.md) — #212 roster changes that drove most of this pass's corrections.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/armor-balance.md](./armor-balance.md)
- [docs/features/lord-perk-review.md](./lord-perk-review.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
