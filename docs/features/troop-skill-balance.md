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
             THROWN_PRIMARY_TROOP_IDS         -> Throwing takes the Bow curve
             name has pike/spear/sword/axe/…  -> +15 shift, only onto the main class
                                                 (or a carried class when no main is known))
            (then the purpose order, #343, 2026-11-15:
             the melee values are re-dealt to the kit's melee classes, main class first,
             and the two launcher values likewise; Throwing, Athletics, Riding stay;
             alternative weapons of one role then tie at the higher value)
            (then the monotonicity clamp, ROSTER-WIDE since 2026-08-30:
             child[skill] = max(child[skill], parent[skill]) over the whole upgrade DAG,
             followed by a purpose raise that restores the order the clamp broke)
```

- **`GROUP_BASELINES`** — Infantry / Ranged / Cavalry / HorseArcher tables keyed by the 11 tier levels `{1,6,11,16,21,26,31,36,41,46,51}`. Troops at off-grid levels (e.g. L7, L13) are skipped (no reference).
- **`CULTURAL_MODS`**: per-culture skill deltas applied on top of baseline (the faction's identity). Keyed by the **filename culture** (`troops_<culture>.xml`), with four special cases in `detect_culture`: `iron_hills_*` ids (which live in the erebor file) → `iron_hills`, `rhun_new` (the Rhûn file) → `rhun`, `mordor_uruk*` ids → `mordor_uruk`, and `orthanc*` ids → `isengard_orthanc`. **Every culture file must have an entry.** Nine of the 25 files have none today (`anduinvale`, `angaladh`, `arthedain`, `cardolan`, `dorwinion`, `eredluin`, `feral_orcs`, `rhudaur`, `southrhovanion`), so the formula treats each with a zero modifier. Lindon had none until 2026-08-30, so the formula ran against it with a zero modifier: the first `--apply` after that gap opened would have stripped the high-elf tuning off all 30 Lindon troops (27 of the 30 have a Rivendell twin carrying identical skill values). A missing key does not error, it silently rebaselines a whole faction to the bare curve.
- **`SKIP_TROOP_IDS`**: troops excluded from the formula entirely (genuine non-humanoid creatures + hand-tuned bespoke mounts). The Iron Hills noble crossbow line left it on 2026-09-18 (#617, below). They still take part in the monotonicity clamp, which only ever raises, so a skip protects a hand-tune without letting it read backwards in the tree.
- **Militia** take the level-21 baseline whatever their real level; the elite slots (`*_elite_militia_troop`, the veteran spearman and veteran archer) add `MILITIA_ELITE_BONUS` (15) on every skill, since 2026-09-13, because a level-11 militia archer and a level-16 veteran militia archer had been the same eight numbers. Militia are identified by the ids a culture **binds** to a militia slot in `taom_spcultures.xml` / `spcultures.xslt`, never by name. See "Militia are a binding, not a name" below.

### Equipment-driven weapon specialization (#340/#341, 2026-07-13)

Weapon detection was originally **name-keyword-only** (`crossbow`/`arbalest`/`naffatun` triggered the Bow↔Crossbow swap), which mis-statted every crossbowman named "Sharpshooter/Marksman/Scout/Sniper" (12 troops shipped Bow-top) and left two-hander troops named "Knight/Berserker/Champion" on the polearm-biased baselines (59 troops shipped Polearm-top — Cavalry L41 is 1H 310 / 2H 160 / Pol 340 by design). The fix:

- `taom_schema.build_item_class_registry(moduledata, game_modules)` maps item id → skill class. It must read **both** vanilla `<Item Type="…">` and Armory `<CraftedItem crafting_template="…">` — the entire install has zero `Type="TwoHandedWeapon"` items; every two-hander is a crafted item.
- `rebalance_troops.troop_weapon_classes(npc, item_classes)` collects the classes actually carried (weapon slots Item0–3).
- The Bow↔Crossbow swap fires iff crossbow-carried-and-no-bow (unambiguous: no troop carries both). The `naffatun` keyword is gone — it had wrongly swapped two javelin throwers.
- **Replaced 2026-11-15 (#343):** a total-preserving post-pass used to swap Polearm and TwoHanded when a troop carried a two-hander, no polearm, and Polearm > TwoHanded. It covered one case of a general fault and is gone; the purpose order below covers every kit.
- **Thrown-primary troops take the Bow curve on Throwing (#554, 2026-09-06).** A troop whose only
  ranged option is a javelin gets `Throwing = RANGED_BASELINES[level]['Bow']` plus the culture's
  **Bow** modifier, the same borrow the Bow↔Crossbow swap performs. It reaches into the Ranged table
  rather than using the troop's own `Bow`, because these troops sit on the Infantry table whose Bow
  column is 15 to 25: there is no ranged number there to swap. It carries the source skill's modifier
  rather than Throwing's because Throwing's modifiers were calibrated against a ceiling of 100, not
  against numbers reaching 320.
- **The trigger is `THROWN_PRIMARY_TROOP_IDS`, a hardcoded set, not a predicate.** The honest rule is
  "carries a thrown weapon and neither bow nor crossbow", and that is what it should become. It cannot
  be that yet: `LOTRLOME_Armory`'s ModuleData is empty on the reference install, so 247 of the 315
  weapon ids the troop files reference do not classify, and a "carries no bow" test reads an invisible
  Armory bow as no bow and hands a real archer the throwing curve. Restoring the install and swapping
  the set for the predicate is #555. The `'Throwing' in weapon_classes` conjunct on the trigger is a
  sanity check: a run against a registry too broken to see the javelin does nothing rather than writing
  a number derived from nothing.
- **The writer hard-fails without the game install** (`--game-modules`, default resolved through `tools/_gamedir.py`, so `$BANNERLORD_GAME_DIR` wins over the literal) rather than silently degrading to the name heuristic. The read-only analyzer degrades to name-only with a loud warning instead.
- Contract tests: `tools/tests/test_rebalance_equipment.py`.

**Known off-formula residuals:** `python tools/rebalance_troops.py --apply --dry-run` plans a change to 407 troops (327 on the commit before #343; measured 2026-11-15). The purpose order and the formula are two writers: of the 369 troops `--fix-purpose` wrote for #343, `--apply` would move 131, and 45 of those sat exactly on the formula before. Nothing was restatted to close the gap; the decision belongs to the level of the curve. The residuals include the hand-tuned `gondor_loss_noble{,_veteran,_sergeant,_warden,_captain}` line (5 troops, intentional; do not `--apply` over them). `--fix-purpose` reordered their own values on 2026-11-15 (#343): their battle sets open with a two-hander and a polearm equally often and carry no one-handed weapon, so both are main classes, both take the troop's top melee value (110 on `gondor_loss_noble`, which was its OneHanded) and OneHanded dropped to the lowest.

The 34 Mordor/Morannon partial-skill-block troops are **resolved** (2026-08-30, #522). They reported CHANGED on every run and produced no byte change because the writer only rewrites values already present. `insert_missing_skill_entries` now adds the missing element, cloning the shape of the last entry in the same block so both the one-line `<skill id=… value=… />` form and the three-line form survive. This mattered beyond tidiness: `CharacterObject.GetSkillValue` returns 0 for an undeclared skill, so a partial block is a silent zero, and `mordor_uruk_skirmisher -> mordor_uruk_crossbow` read as Bow 130 to 0.

**Hand-tuned, protected via `SKIP_TROOP_IDS`** (these no longer appear as residuals because the tool skips them outright): `iron_hills_noble_scout` / `_sharpshooter` / `_veteran_sharpshooter`, Crossbow 175 / 225 / 275 as of 2026-07-30. The formula derives Crossbow from level and `CULTURAL_MODS['iron_hills']` alone, which gave the noble line exactly the regular `ironpass_*` line's values (130 / 170 / 205) — no edge in the only skill the branch specialises in. If the noble/regular split is ever expressed as a modifier rather than a hand-tune, remove these three ids and let the formula own them again.

**Superseded 2026-09-18 (#617).** The three ids are out of `SKIP_TROOP_IDS`. The ranged ladder put the noble line on the Erebor crossbow cells, Crossbow 120 / 160 / 195 (the same as `ironpass_*`), and Mike kept the ranking over the hand-tune: the line's edge is its crossbows, the hardest-hitting in the game at T4 to T6, and Crossbow skill adds no damage on 1.5.3 (it tightens spread, speeds the reload and sharpens the AI's aim). A full rebaseline now gives them their ladder cell and leaves them as they are on disk. [ranged-ladders.md](ranged-ladders.md).

**Resolved (#343, 2026-11-15):** the one-handed troops with Polearm on top, and every other kit the curve ranked wrongly, are now settled by the purpose rule below.

### Purpose: the kit decides the order (#343, 2026-11-15)

The curve is keyed by formation group, not by kit. The Infantry and Cavalry tables put Polearm at or above OneHanded at most levels, so a swordsman with a shield came out a better spearman than swordsman, and a Ranged elf with a two-handed sword had its best melee number on a one-handed sword it does not carry. The pair swap above caught one of these shapes. The purpose rule replaces it for all of them, and `tools/troop_purpose.py` is its only statement: the writer, the realm generators, the validator and the analyzer call the same functions.

- **P1, the main class:** the weapon class in Item0, over the battle sets (civilian sets never count). `Equipment.GetInitialWeaponIndicesToEquip` wields the first weapon slot that is not held in the off hand, so Item0 is the weapon in hand at spawn. When sets disagree, the class most sets open with wins; a tie goes to the class the troop is more skilled in, and the tie rule below lifts the other to it. A thrown weapon counts: a javelin in Item0 is a thrower's main weapon, as in vanilla's skirmisher factions (`hidden_hand`, `beni_zilal`), and the Marshal's Word thrower trial passed with `gondor_har_javelineer` holding it.
- **P2, Item0 is a real weapon:** never a shield or ammunition while the same set holds a weapon. The engine puts a shield in the off hand and still wields the weapon, so the fault is in the rule's input: it reads no main class from Item0. `--fix-purpose` exchanges Item0 with one other weapon slot of that set; no item is added, removed or replaced. No battle set breaks it today. A troop that carries a launcher in every battle set and is `Ranged` or `HorseArcher` should open with the launcher, but vanilla ships the other layout too, so that is a warning reason no tool repairs: a designer exchanges the slots by hand.
- **P3, skills follow the kit:** the troop's own melee values are re-dealt to the melee classes in purpose order (main first, then the other carried classes by how many sets carry them, then the classes it does not carry), and the two launcher values the same way. The reorder keeps the troop's own total; the tie, the clamp and the purpose raise are what move it. Throwing, Athletics and Riding are never touched, and a ranged ladder cell keeps its number. A thrower's melee values are ordered the same way; its Throwing keeps its value on disk.
- **Ties, the raise before the clamp:** in vanilla, the two troops whose battle sets hold alternative weapons for one role (`imperial_recruit`, `khuzait_nomad`, OneHanded and Polearm) hold both at one value. Two classes of one kind that share a role (Item0, or a side slot) in the same number of battle sets, and never in the same set, are alternatives: one set carries a two-handed sword where the other carries a one-handed sword (the Lorien Marchwarden line), or one set opens with a spear and the other with a two-hander (the Dale lake town line). Neither is "the" weapon, so both take the higher of the two values after the reorder. A tie for Item0 makes both main classes, so a sidearm stays below them. A 3 to 1 split is no tie, and neither is a pair carried together. The tie runs before the clamp, in the formula, the realm generators and `--fix-purpose`. The only other raises are the clamp and the purpose raise after it.
- **After the clamp:** the clamp only raises, and an inherited value can land above the child's own main weapon. The purpose raise then lifts, never lowers: each class to the highest class after it in its kind, and the main class to tie any rival the clamp itself lifted. A gap between kinds that was on disk before the clamp is reported, never filled with a borrowed number.

**The size of the change** (the troop files on commit `078b86761` against the #343 result, measured 2026-11-15): skills changed on 369 troops. On 297 of them the reorder kept the 8-skill total. On the other 72 the total moved by +1518 net: 21 by the tie rule (+712), 33 by the clamp cascade below a reordered parent (+601), and 18 by the regeneration of the Arthedain, Cardolan and Rhudaur realm files (+205; four of those went down). No troop's top weapon skill rose except through a tie. The RCA lists the troops whose Item0 class rose over a class they carry in every set ([rca](../reviews/rca-troop-skill-purpose-2026-11-15.md)).

**What the formula no longer does:** the Polearm and TwoHanded swap is gone. A name shift (`spear`, `sword`, `axe`, `shield` and the rest) fires only when it boosts the main class, or a carried class when no main class is known, so a "Spearman" holding a sword no longer moves 15 points onto a spear.

**Two validator codes** ([codes](../reference/moduledata-validator-codes.md)):

| Code | Severity | Fires when |
|---|---|---|
| `TROOP_ITEM_ZERO_NOT_MAIN` | error | a battle set holds a shield or ammunition in Item0 beside a weapon, so the rule reads no main class from Item0. Commit-gated; no finding in today's data |
| `TROOP_SKILL_KIT_MISMATCH` | warning | a melee or launcher class is above the main class, a class the troop does not carry is above a carried one of the same kind, or a launcher troop's battle set does not open with its launcher. It becomes an error when the count reaches zero (`TROOP_SKILL_KIT_MISMATCH_SEVERITY` in `tools/taom_schema.py`) |

Three warning reasons are left after `--fix-purpose`, and the message names each. "launcher troop that does not open with its launcher": no tool repairs it, because vanilla ships the layout; a designer exchanges the slots by hand. "melee skill above the launcher's ladder cell": an archer whose Bow or Crossbow is a ranged ladder cell and whose melee is higher; moving a value between kinds is a decision on the level of the curve, so the writer leaves them. "thrower below its melee skill": a thrower whose Throwing is under its melee; the message says whether the troop is in `THROWN_PRIMARY_TROOP_IDS`, and `--fix-purpose` prints the formula's number, but no value is invented. No troop has the first reason today; the RCA lists the other two ([rca](../reviews/rca-troop-skill-purpose-2026-11-15.md)).

`tools/analyze_troop_balance.py` **imports these tables verbatim**: it never re-derives the curve, so the "ideal" and the "writer" can never disagree. It compares each troop's actual skills to `calculate_skills(...)` and reports the delta; a ranged ladder troop's Bow or Crossbow reference is its ladder cell (`rebalance_troops.ladder_cells`), the value the writer sets (#617).

### Component diagram

```
GROUP_BASELINES + CULTURAL_MODS  (rebalance_troops.py — the curve)
        |                              |
   rebalance_troops.py            analyze_troop_balance.py
   (WRITES skills via regex,      (READS, imports the curve,
    preserves all formatting)      emits HTML/MD/JSON overview)
        |                              |
  troops_*.xml (25 files)        tools/reports/troop-balance/REPORT.{html,md} + .json
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

`clamp_upgrade_monotonicity` runs after the formula and before the write, over the whole roster at once because a single topological order is what lets a raise propagate down a chain and lets formula-skipped troops lift their children. It spans two directories: all 844 edges inside `troops/` stay within their own file, but the 21 villager edges in `characters/npcs_*.xml` cross into it. It topologically orders the upgrade graph (1,064 troop definitions and 844 edges, plus 21 villager sources and their 21 edges, once the villagers in `characters/npcs_*.xml` are included; acyclicity is asserted, not assumed) and sets `child[skill] = max(child[skill], parent[skill])`. Troops the formula skipped take part using their current values: they can lift a child and be lifted, and since a clamp only raises, a hand-tune is never reverted.

**Two modes, and picking the wrong one rebaselines the roster inside a bug fix.**

| Mode | Base for the clamp | Use when |
|---|---|---|
| `--apply` | the formula result | you actually want a rebaseline, and have reviewed the per-culture deltas |
| `--fix-monotonicity` | what is on disk | you are repairing upgrade ladders and nothing else |
| `--restat <id,id>` | forces the formula for those ids in clamp-only mode | a specific troop was misclassified and needs to come back onto the curve |
| `--fix-purpose` | what is on disk, reordered to the kit | a troop's skills or its Item0 break the purpose rule (#343). A dry run unless `--apply` is added; it writes any Item0 exchange and the reordered and tied skills, then the clamp and the purpose raise; it prints every tie raise. A second run plans nothing. Not combinable with `--restat` |
| (every mode) | | the clamp and the purpose raise repeat until neither moves a value (`settle_purpose`), so a `--restat` child on a respecialising edge keeps its main class on top. In the clamp-only mode a troop the clamp leaves alone is never raised |
| `--dry-run` | (modifier) | preview EITHER mode without writing. It used to be a third mutually exclusive mode, which meant the clamp-only path could only be inspected by letting it write and then reading `git diff` |

`--fix-monotonicity` exists because `--apply` on this roster sweeps up 23 deliberately off-curve troops: the `gondor_loss_noble` line (#343, documented do-not-apply-over), the hand-authored Black Numenorean `mordor_num_*` line, the dwarf ram riders, and `mistymountainorcs_bolgs_ironfang` (which sits in the goblin file and has no `detect_culture` route, so it takes goblin modifiers). None of that belongs in a monotonicity fix. The tell during the 2026-08-30 pass was a per-file diff size: `troops_lindon.xml` came out at 478 lines where the clamp accounted for 30.

The cost is deliberate. A Ranged troop branching off an Infantry parent keeps the parent's Polearm, so
group specialization blurs on branch upgrades. A clamp can produce a proficiency the troop cannot use:
`mordor_uruk_crossbow` inherits Bow 130 from its bowman parent, which is exactly what vanilla does for
`imperial_trained_archer -> imperial_crossbowman`, and it is inert on a weapon the troop does not carry.

**One inherited value is no longer accepted (#554, 2026-09-06).** This paragraph used to list
`sagarun_naffatun` inheriting Crossbow 160 as the same benign case. It was not. Inert stops being
harmless when the troop also has no skill in the weapon it actually throws: that troop carries two
javelins and an axe, no bow and no crossbow, and shipped with Bow 195 / Crossbow 160 / Throwing 55.
Its Bow and Crossbow are now floored to the Infantry baseline and Throwing takes the ranged curve.
Because its parent `sagarun_crossbowman` genuinely carries a crossbow at 160, flooring the child is a
real regression across a real edge, so the pair is named in `RESPECIALIZATION_EXEMPT_EDGES`, mirrored
in `rebalance_troops.py`, `taom_schema.py` and `TroopUpgradeSkillMonotonicityTests.cs`. All three must
agree: without the entry in the writer's own clamp, the run floors the value and the clamp puts it
straight back, and the pass reads as having done nothing.

### On 1.4.8 `skill_template` beat an inline `<skills>` block outright

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
That was the 1.4.8 loader, which ignored the inline rows whenever a template was named (a dangling id
still returned an empty placeholder). Since 1.5.2
the engine copies the template and lays the inline rows over it, so a listed row wins. The rule that
holds on both (#626, 2026-09-19): a character may declare both, and every inline value must equal its
template's. `SKILL_TEMPLATE_MISMATCH` in `validate_moduledata.py` (the commit hook runs it) and
`TAOM.Tests/Core/LordInlineSkillParityTests.cs` enforce it over every ModuleData file and `lords.xslt`;
`python tools/sync_lord_inline_skills.py --apply` repairs a drift, the SkillSet being the source of
truth. It replaced `SKILL_TEMPLATE_SHADOWS_SKILLS` and `SkillTemplate_NeverShadowsAnInlineSkillsBlock`,
which refused any character declaring both. **A troop with an empty `<skills>` block and a template is
the legitimate shape** and is left alone; both upgrade gates skip templated characters when judging an
edge, because their base skills live in a SkillSet these files cannot see (no troop or villager edge
has a templated side today).

### The graph spans two directories

Upgrade sources are not only in `troops/`. Each of the 21 `villager_<culture>` entries in
`characters/npcs_*.xml` upgrades into its culture's tier-1 troop, and the engine treats any character
with a non-empty `UpgradeTargets` array as upgradeable. Gating only `troops/` left 16 edges
unchecked, six of them regressing, while `validate_moduledata.py` reported PASS. Those villagers now
declare their skills explicitly, at exactly the values the vanilla template had been supplying, so
the data is self-describing and every gate can read the whole graph: **865 edges, 844 of them inside
`troops/` and 21 crossing in from `characters/`.** `rebalance_troops.py` loads the external sources
read-only through `load_external_sources()`; they seed the clamp and are never written.

### Militia are a binding, not a name

TAOM militia take the **level-21 baseline regardless of their actual level** so sieges and village defence stay costly (user direction, 2026-06-24), and the elite slots sit `MILITIA_ELITE_BONUS` (15) above that on every skill (2026-09-13; before that the four slots were identical and a level-11 militia archer and a level-16 veteran were the same troop). A militia promoting into another militia is therefore a fixed step off the curve, and the 22 militia-to-militia edges (across 11 cultures) are exempt from the rule above. The one other exemption is `RespecializationExemptEdges` in the test: `sagarun_crossbowman` to `sagarun_naffatun` for Bow and Crossbow only.

Which troops are militia comes from what a culture **binds**: the 92 ids in `militia_troop` / `melee_militia_troop` / `ranged_militia_troop` / `*_elite_militia_troop` across `taom_spcultures.xml` and `spcultures.xslt`. Both encodings have to be read, a plain attribute and an `<xsl:attribute>` element; Dale, Dunland, Harad, Rhûn and Rohan use only the second. Comments are masked first, so a commented-out `<Culture>` block cannot widen the exemption, and the 92 ids are pinned by identity in the C# gate rather than by count (the old name rule produces the same set plus one, so a count-only assertion would not have caught a revert). The Ranged/melee split for choosing the L21 table comes from `default_group`.

Until 2026-08-30 this was a name substring (`militia` plus one of spearman/archer/veteran). It had exactly one false positive across 871 troops, and that one shipped the worst upgrade edge in the game: `gondor_ano_archer_militia` is a level-11 Anórien **line** troop, bound only in `taom_partyTemplates.xml`, so it wore level-21 stats and out-statted its own level-16 target on seven of its eight skills, -145 total (Riding was already equal on both). Same defect family as the name-based weapon detection replaced in #340/#341.

### What the analyzer still reports

`analyze_troop_balance.py` stays the read-only overview and stays total-based with a ±25 tolerance. It exempts militia-to-militia **edges** now, not militia troops: excluding the troops is how it printed "0 inversions" for two months while the -145 edge sat inside its own exclusion. Run the fixed check against the pre-fix data and it names the bug immediately.

Three within-culture level inversions are known and accepted, all Gondor Ranged: `gondor_ith_longbowman` and `gondor_mt_longbowman` (L36) out-total `gondor_brv_shadowbow` (L41) by 110, and `gondor_ith_moon_guard` (L46) out-totals `gondor_ithilien_ranger` (L51) by 45. These compare disconnected branches, and the whole gap is melee the longbowmen inherited from an infantry parent through the clamp. The higher-tier troop still leads on Bow in every case, and no player sees an upgrade get worse. Closing them means a Gondor balance pass, not a bug fix.

Four upgrade edges end up exactly flat, which the gate allows because flat is not a decrease (the 6 militia pairs above rose by 15 on 2026-09-13 and are exempt anyway): the two `*_knight_golden_flower` capstones into their `*_warden_gondolin`, and two more in the goblin and gundabad Ironfang lines. Those two capstones sit at level 51, which is already tier 10, the cap `TaomCharacterStatsModel` sets, so there is no grid step left to promote them into. They are equal-tier variant branches that differ by equipment and role. The `*_bolgs_ironfang` pair had the same shape at level 36 and was fixed properly, by moving them one step to level 41 (the T8 they are labelled as) and restatting.

**Per-skill enforcement lives in two gates**, not in the analyzer: `TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs` (runs in CI, no game install) and the `UPGRADE_SKILL_REGRESSION` error in `tools/taom_schema.py` (so `validate_moduledata.py` fails on it). `tools/tests/test_upgrade_skill_monotonicity.py` covers the gate, the clamp and the entry insertion on synthetic data.

## Armour is a ladder too

The ladder rule has an equipment half since 2026-09-04 (#541). A troop's armour in a slot is the
average over its BATTLE sets of the item's `head + body + arm + leg` (all four, because a chest
contributes arm armour and a pauldron body armour), an unfilled slot counting as 0 since the engine
draws each slot from an independently chosen set. The total is the sum over Head, Body, Cape, Gloves
and Leg, and **no upgrade target may total less than its source.** 62 edges in 13 cultures did when
first measured: the Rhun `loke_rim_*` and `dragon_wrath_ash_*` capstones in `plate_light` over
`hplate_heavy` parents (-60 to -66), `dg_khamul_shadow_archer` in a hood over a plated disciple
(-74), the uruk skirmisher with no gloves or cape (-65).

| Piece | Where | What it does |
|---|---|---|
| The clamp | `tools/fix_upgrade_armour_regressions.py` | Topological over the whole upgrade DAG (villager sources in `characters/` read-only). Per regressing edge, per slot, in every battle set: an `OVERRIDES` entry, else the cheapest item in the target's own culture folder and slot file sharing the target's item FAMILY (id with the trailing tier tokens stripped) that meets the source's value, else the source's own item. A slot the target never fills is appended to every set. Dry-run default, `--apply` writes byte-faithfully and re-checks from disk. |
| Hero-kit demotion | `DEMOTE` in the same script | The parent is sometimes the anomaly: `imladris_recruit` (L21) wore a lord's circlet in 8 of 13 sets and a lord's torso in 7, and raising the whole elf tree to lord kit would have been the wrong fix. Items matching a pattern are swapped, on troops below the listed level, for the troop's own strongest ordinary item in that slot, before the clamp judges anything. |
| The gate | `UPGRADE_ARMOUR_REGRESSION` (WARNING) in `tools/taom_schema.py` | Same definition, item values from the install (`Registries.item_armour`); skipped, never faked, when the install is absent. Warns rather than errors because the Armory is unversioned and the commit hook cannot see it. |
| The overview | `tools/analyze_troop_balance.py` | "Upgrade armour drops" section beside the skill monotonicity one. |

Exemptions mirror the skill rule: militia-to-militia edges, and the bare-chested-by-design troops
(`_BODYLESS_BY_DESIGN`) are compared without Body and Cape, because their only Body item, where
they have one, is an 89-armour skirt that leaves the chest bare. When a parent out-armours its child because an ITEM is
mis-statted, fix the item: the two Dunland `wulf_helmet_medium_*` carried 45 (the lord row) and made
every L21 Dunlending read as a downgrade, restatted to 22.

## Key Files

| File | Purpose |
|------|---------|
| `tools/rebalance_troops.py` | Writes skills onto the curve. `GROUP_BASELINES` + `CULTURAL_MODS` + `SKIP_TROOP_IDS` + `detect_culture` + `militia_troop_ids` + `clamp_upgrade_monotonicity` + `insert_missing_skill_entries` + `settle_purpose` + `apply_item0_swaps`. `--dry-run` / `--apply` / `--fix-monotonicity` / `--fix-purpose`. |
| `tools/troop_purpose.py` | The purpose rule (#343): main class, Item0 check and exchange, the reorder, the tie rule (`tie_raise`), the post-clamp raise, the findings. Pure functions on plain data; tests in `tools/tests/test_troop_purpose.py` and `tools/tests/test_fix_purpose.py`. |
| `tools/analyze_troop_balance.py` | Read-only overview generator. Imports the curve from `rebalance_troops.py`; emits HTML/MD/JSON. `--outlier-threshold N` / `--stdout`. Never writes troop XML. |
| `tools/taom_schema.py` | `UPGRADE_SKILL_REGRESSION` (ERROR), the `validate_moduledata.py` half of the gate. `TROOP_ITEM_ZERO_NOT_MAIN` (ERROR) and `TROOP_SKILL_KIT_MISMATCH` (WARNING), fed by `Registries.item_classes`. Template parity is `SKILL_TEMPLATE_MISMATCH` in `validate_moduledata.py` (#626). |
| `TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs` | 3 tests: no skill drops, all 8 skills declared, militia exemption pinned by identity (template parity is `TAOM.Tests/Core/LordInlineSkillParityTests.cs` since #626). Fails rather than going inconclusive when it cannot find ModuleData, since a data gate that cannot read its data has checked nothing. |
| `tools/tests/test_upgrade_skill_monotonicity.py` | 25 synthetic-data tests: the gate, the clamp, entry insertion, the fail-closed militia loader, and that the upgrade index leaves templates to `SKILL_TEMPLATE_MISMATCH` (`tools/tests/test_skill_template_mismatch.py`). |
| `tools/fix_upgrade_armour_regressions.py` | The armour clamp (#541): family step-up, parent fallback, slot append, hero-kit demotion. `tools/tests/test_fix_upgrade_armour_regressions.py` covers it on synthetic data. |
| `UPGRADE_ARMOUR_REGRESSION` in `tools/taom_schema.py` | The armour gate (WARNING), fed by `Registries.item_armour`; tests in `tools/tests/test_validate_moduledata.py`. |
| `Main/_Module/ModuleData/troops/troops_*.xml` (×25) | The troop definitions (the data under management). |
| `Main/_Module/ModuleData/characters/npcs_*.xml` (×31) | The 21 `villager_*` upgrade sources that feed into the tier-1 troops, read-only to the writer. |
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

- 2026-11-15: **Skills follow the kit (#343).** The curve is keyed by formation group, so a
  swordsman could top out on Polearm and an archer with a two-handed sword on OneHanded. Added
  `tools/troop_purpose.py` (the purpose rule), `--fix-purpose`, the purpose step in
  `calculate_skills` and the realm generators, and the validator codes `TROOP_ITEM_ZERO_NOT_MAIN`
  and `TROOP_SKILL_KIT_MISMATCH`. Removed the Polearm and TwoHanded swap. Throwing is a main
  class (a thrower's javelin stays in Item0), and alternative weapons of one role tie at the
  higher value. The analyzer shows each
  troop's main class and both findings. Applied with `python tools/rebalance_troops.py
  --fix-purpose --apply` after regenerating Arthedain, Cardolan and Rhudaur; the RCA holds the
  counts ([rca](../reviews/rca-troop-skill-purpose-2026-11-15.md)).

- 2026-09-04: **The Dol Guldur Uruk line sat a tier under the curve, and equipment joined the
  ladder rule (#541).** `f9942d84` had moved the ten `dg_uruk_*` levels up by five without a
  restat (an L36 Black Guard tied an L31 Khamul infantryman); nine Mordor archer, scout and warg
  troops carried the 0-seeded secondaries from the 2026-08-30 insertion. All 20 restatted with
  `--fix-monotonicity --restat`, values only. New: the armour clamp, the `UPGRADE_ARMOUR_REGRESSION`
  gate and the analyzer section above; 62 regressing edges repaired, 0 remain.
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
- [docs/modding/balance-levers.md](../modding/balance-levers.md)
- [docs/modding/skill-sets.md](../modding/skill-sets.md)
- [docs/modding/troops.md](../modding/troops.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
