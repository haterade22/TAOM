# The Long Defeat, Phase 5 plan: Minas Tirith and content (DRAFT)

Written 2026-11-21 against `feat/ld-phase4` at `06a74449e`, with the live `TAOM_Map` and
`LOTRLOME_Armory` read and not changed. Issue #1079, item (4): "Minas Tirith, content and balance".
This is a draft: nothing in it is decided, and every row marked Maintainer waits for Mike. A sample
of its ids was checked against the data a second time (the two garrison troops, the two trolls, the
four lord stubs, the scene's row in `custom_battle_scenes.xml`, the `gondor` and `mordor` culture
ids). The rest is as its author read it, and section 10 lists what nobody could verify.

**Engine source rule.** Every signature comes from the installed v1.5.7 DLLs:
`pwsh tools/taom-src.ps1 path <Type>`. The dump can lag. Quote the line that makes an effect.

**Numbering.** Phase 4 ends at D49 (with D38a, D44a), A35, E58, R33, OQ20, S12. This plan starts at
D50, A36, E59, R34, OQ21, S13.

## Preconditions

| # | Precondition | State on 2026-11-21 |
|---|---|---|
| P1 | Phase 4 Slices 1 to 4 built | Built. Slice 4 proved unattended in three runs; a person's rise is owed. |
| P2 | Phase 4 Slices 5 to 8, 10, 11 | Not built. Slice 9 (the Marshal) dropped. Nothing is for sale (`LocationOffer.Offered` empty). |
| P3 | Helm's Deep places | `keep: null`; no armoury, yard, towers or volley. Sally recorded 2026-11-16. Muster foot and archers recorded. |
| P4 | Helm's Deep recording session (Mike) | Owed. The Minas Tirith session (section 6) uses the same commands and should follow it, so one session proves the tool first. |
| P5 | Phase 5 data that needs a Phase 4 place (armoury, yard, towers) | Waits for the Phase 4 slice that builds that place. Phase 5 Slices 1 to 4 do not. |

## State on 2026-11-22

Mike's word that day: build Minas Tirith now, as Helm's Deep was built. The decisions marked Maintainer below were taken at their recommendations for Slices 1 to 4 (D51 Imrahil, D53 Mordor only, D54 the Witch-king as general, D60 the three existing heroes). Each is one data row to undo.

| Slice | State | Differs from the text below |
|---|---|---|
| 1 | Built | The wrapper's class list was left on Helm's Deep: both fortresses offer the same four class ids, so a per-fortress read changes nothing yet |
| 2 | Built, not played | The fortress name reuses `TAOM_FOW_Region_MinasTirith` ("Minas Tirith", translated already). The offer ids are `militia`, `archer_militia`, `lossarnach_axes`, `morthond_bowmen`, `pinnath_gelin`, `citadel_guard`. The Cavalry text change moved to Slice 5 |
| 3 | Built, not played | The rank names are the lords' own name keys (`aom_lord_1_63_name` and the others), not new `TAOM_LD_Rank_Mordor_*` keys. The general carries both abilities, as the Helm's Deep general does. The summon brings the Barad-dur guard, not the uruk warrior, so it is stronger than the wave it joins. Waves 5 to 10 and the bounties differ from section 4.2: the table there was up to 15 troop levels above Helm's Deep at wave 9 and paid less per level, so the built table follows the Helm's Deep curve |
| 4 | Built, not played | Fifteen upgrade steps; every one is an edge of `troops_gondor.xml` |
| 5 | Built | Not the rewording of section 4.5. A boon row carries `cultures` and a class carries `texts`: words for one defender culture. Helm's Deep keeps its Rohan names, Minas Tirith shows Gondor names, and no English text changed, so D63 no longer changes Helm's Deep and no translated row went stale. Ids, order and weights are the same at both fortresses, so the pinned rolls hold. Open: Minas Tirith sells no rider, so the three cavalry rows are never in its pool; a Dol Amroth pack would open them |
| 6 | Built, not played | Difficulty as D64: `normal` and `hard`, the factor 1.25 and the affixes 2 waves earlier, both in `waves.json` `rules.hard`. The wave count cap scales with the factor: hard's cap is 375 (`min(1000, round(normal * factor))`, one rounding after normal's own cap of 300), where the table in section 5 said the count stays inside `maxCount`. With that cap kept, hard would equal normal from wave 20. Each difficulty keeps its own best run (`<fortress>:hard`), and the co-op payload is version 2 |
| 7 to 10 | Not built | |

Answered since the draft: the tier test accepts the 16 to 26 and 21 to 36 links (it wants a rising level only); the JSON keys of the war cry are `swingSpeedPercent` and `moveSpeedPercent`; the knight's horse and harness are rows beside his equipment rosters, not inside one.

## 1. Scope and non-scope

**In scope**

- A second fortress, `minas_tirith`: Gondor defends the first circle of Minas Tirith against Mordor.
- The menu asks fortress (exists), difficulty (new) and class (exists).
- A Mordor roster: orcs, then uruks, then trolls; commanders Gorbag, Gothmog and the Witch-king.
- A Gondor shop: packs from the out-companies, upgrades from Gondor's tree, heroes, fortifications.
- Gondor class kits. Fortress-neutral boon and class text.
- The wrapper, the judge and `1079.toml` per fortress.
- A recording session at Minas Tirith for its places.
- The troll gate reset, so trolls can take part.

**Not in scope** (issue #1079 lists the first four as "Not in the first version")

- Co-op play. The payload version moves only because a new run option is added (D64).
- Any campaign link. Saving in mid-run.
- Falling back through Minas Tirith's circles. Only the first circle is defended.
- Mumakil as mounts (E66). Grond as its own engine (D56).
- The Rohirrim at dawn and Aragorn's fleet as events (D68).
- Variags of Khand (no troop exists, E64). New Morgul orc troops (none exist, E63). New art.

## 2. Decisions

"Maintainer" marks a decision that is Mike's. The others are recommendations under his standing
word (think-before-coding.md, 2026-11-12) and can be undone by editing the data row.

| # | Decision | Reason | Owner |
|---|---|---|---|
| D50 | Fortress id `minas_tirith`, scene `taom_gondor_town_minas_tirith_forceatmo`, `defenderCulture` `gondor`, `attackerCulture` `mordor`. | The scene is in `custom_battle_scenes.xml` with `is_siege_map` true. Affix names key on the attacker culture, and `names.mordor` is complete (a shipped test pins it). | Agent |
| D51 | Player troop `lord_1_9` (Imrahil). | A human stub with a horse in `custom_battle_lords.xml`. Denethor has no stub and is no field commander. Alternative: Faramir `lord_1_34` (a Ranged stub). If Imrahil is the player, he cannot also be a hired hero (D60). | **Maintainer** |
| D52 | Garrison: `fow_gondor_ano_militia` Infantry 100, `fow_gondor_ano_archer_militia` Ranged 50. | Same counts and levels (11 and 11) as Helm's Deep. Both head a Gondor upgrade line. | Agent |
| D53 | Attackers come from `Culture.mordor` only in the first version. Easterlings (`khuzait`) and Haradrim (`aserai`) need a new `attackerAllies` list and relaxed side tests. | The own-troops rule and `ShippedFiles_EveryTroopBelongsToTheSideThatFieldsIt` refuse a foreign wave troop. Recommendation: a later slice (Slice 9), after the base fortress plays. | **Maintainer** |
| D54 | Commanders every 4 waves: captain Gorbag `lord_1_63`, lieutenant Gothmog `lord_1_30`, general the Witch-king `lord_1_15`. | Phase 2 section 11 names Gorbag. Gothmog led the host at the Pelennor. A stub for Gothmog is new (section 4.6). The Witch-king as a killable general departs from the book (he falls on the Pelennor, not at the wall); the alternative is Gothmog as general and a Mordor orc lord as lieutenant. | **Maintainer** (the Witch-king) |
| D55 | Mordor gets its own war cry and summon names. | `CommandersReader` takes ability names per roster; the Isengard names cannot be shown for Mordor. | Agent |
| D56 | Grond is the Hammer of the Underworld affix (engine hit points x1.5 on rams and towers), on the scene's one ram. No Grond engine. | No Grond prefab is in the scene; it has one `batteringram_a_spawner`. The affix name is already in `names.mordor`. | Agent |
| D57 | No mumakil. | Every soldier spawns on foot and the slot is Infantry or Ranged only (E66). A mumak on foot is not a mumak. | Agent |
| D58 | Trolls (`fow_cave_troll`, `fow_hill_troll`) enter only after the troll gate reset (Slice 8). | `CreatureSiegeRules` latches a broken gate as broken for good, so a troll reads a repaired gate as open (E67). A shipped test already says a creature needs Phase 5's gate reset first. | Agent |
| D59 | Shop `gondor`: six packs from the out-companies and the Citadel (section 4.3); upgrades follow Gondor's campaign tree; Rohan's starting gold (600) and price bands. | Lore: Forlong's Lossarnach axes, Duinhir's Morthond bowmen, Hirluin's men of Pinnath Gelin, Imrahil's men of Dol Amroth, the Citadel Guard. #1161 upgrade steps follow the campaign tree. | Agent (prices: **Maintainer** at the balance pass) |
| D60 | Heroes at Minas Tirith: keep `aragorn`, `legolas`, `gimli` (they came with the fleet). New mission heroes Gandalf, Beregond and Pippin go under ids to create; Imrahil only if D51 picks Faramir. | Only `taom_hero_aragorn`, `taom_hero_legolas`, `taom_hero_gimli` exist in `taom_mission_heroes.xml`. Recommendation: Gandalf first (the defence of the gate), Beregond and Pippin later. | **Maintainer** |
| D61 | Fortifications: copy Rohan's gate repair, gate levels and field care. No elven volley at Minas Tirith. Towers wait for the recorded posts. | No elves fought at the Pelennor walls. Towers need posts (section 6). | Agent |
| D62 | Gondor class kits from Gondor fow troops (section 4.4). The cavalry class text becomes neutral. | `EveryKitItemIsCarriedByATroopOfThatCulture_OrIsANamedException` needs a carrier of the culture. The current cavalry text names "a rider of the Mark". | Agent |
| D63 | Boons that name Rohan or its places get neutral names and texts; ids stay. | A fortress filter on boons is new schema; keeping ids keeps the pinned rolls (BoonDeck, Python replay, seeds) stable. The balance hash changes either way, so Helm's Deep opens a new board. Alternative: a `fortresses` filter per boon. | **Maintainer** (Helm's text changes) |
| D64 | Difficulty, smallest design: two levels, `normal` and `hard`. `hard` multiplies each wave's count and the per-wave health and damage growth by one factor from `waves.json` `rules.hard` (proposed 1.25), and starts the affix schedule 2 waves earlier. Board key: fortress, difficulty, balance. | Phase 2 section 11 proposed scaling the endless factors and the affix schedule. One knob block, no new roster. Co-op payload version 1 to 2 because `RunOptions` grows. Smaller alternative: `hard` = start at wave 4 with the normal plan (no new scaling code, but a weak promise). | **Maintainer** |
| D65 | No `split` for Minas Tirith until a proof run shows how the scene's siege objects split the garrison. | The split comes from scene siege objects; Minas Tirith has 4 ladders and 2 towers where Helm's Deep differs (E62). Alternative: copy Helm's 0.7 and 0.7. | **Maintainer** |
| D66 | The wrapper reads classes, judge rules and manifest rows per `--fortress`. Minas Tirith rows in `1079.toml` carry a fortress token. | `CLASSES`, `class_choice` and `class_for_run` read helms even under `--fortress` (Phase 2 section 11). Manifest rows match without a fortress today. | Agent |
| D67 | One recording session at Minas Tirith (section 6), after the Helm's Deep session. | The first circle's places are not in the data. | **Maintainer** (his session) |
| D68 | The Rohirrim relief at dawn (the #1162 analog) and Aragorn's fleet are a later issue. | Both need defender reinforcements of another culture and, for the riders, a mounted spawn path that does not exist (E66). | **Maintainer** |
| D69 | Keep the global `liveCap` 150. Add a per-fortress cap only if a Minas Tirith proof run shows a worse frame time than S8. | S8 at 300 soldiers: wave frame means 14.3 and 13.0 ms, worst 66.7 ms (Helm's Deep at 280: 10.8 to 11.3 ms). Rules are global today; a per-fortress cap is new schema. | Agent |

## 3. Assumptions

| # | Assumption | Check |
|---|---|---|
| A36 | The two `CastleGate` entities carry the outer and inner gate tags from their prefab. | UNVERIFIED. The scene entities have no tags; read the prefab or log the gate reading in the Slice 2 run. |
| A37 | The director's spawn frames work on this scene as at Helm's Deep. | UNVERIFIED. No siege spawn-frame tag was found in `scene.xscene`. Slice 2 proof run. |
| A38 | The Witch-king (`race nazghul`) spawns, fights and dies as a commander on foot. | UNVERIFIED. His equipment set has a horse, which a commander spawn clears. Slice 3 proof run. |
| A39 | `noble_horse_imperial` and `gondor_horse_armor_1_da` spawn under the player at a sally. | UNVERIFIED. Slice 4 proof run. |
| A40 | `wm_gondor_lossarnach_2h_axe_black_ash_a` fits the two-handed slot as the Rohan axe does. | UNVERIFIED. Slice 4 proof run. |
| A41 | A summon count above 50 is refused. | UNVERIFIED: from the `CommandersReader` comment on `CommanderPlan.MaxSummon`, not the constant. |
| A42 | The creature siege role activates in this mission (MCM switch on, an outer gate resolves). | UNVERIFIED. creature-siege-role.md notes the scene's outer `middle_pos` is not between the gates. Slice 8. |

## 3a. Engine and data facts (read at HEAD 06a74449e or in the live installs)

| # | Fact | Source |
|---|---|---|
| E59 | Config is six JSON files under `Main/_Module/ModuleData/long_defeat/`, hashed into `balance`. One leaderboard board per fortress and balance. | long-defeat.md; leaderboard-plan.md lines 29, 134 |
| E60 | `BuildPlan` sets `Affixes = AffixesFor(fortress.AttackerCulture)` and `Boons = _boons.Value` (global). `ClassesFor` returns classes with a kit for the defender culture. | `LongDefeatConfigCatalog.cs` |
| E61 | The setup prompt already asks the fortress over every fortress (with its best run), then the class. | `LongDefeatSetupPrompt.cs` lines 49 to 98 |
| E62 | Scene: 21,164 entities. `CastleGate` x2: `sturgia_castle_gate_outer_l3` (603.009, 571.746, 12.063), `sturgia_castle_gate_inner_l3` (602.832, 563.353, 12.063). One `batteringram_a_spawner` (608.3, 712.758, 6.811). Towers `siege_tower_12m_spawner` left (715.76, 659.391, 4.787), right (469.79, 667.674, 14.012). Four `siege_ladder_11m_spawner`. 8 deployment points (4 on the walls at about z 25.5 and 37, 4 low). Spawners: trebuchet_a 8, mangonel_b 4, mangonel_a 4, ballista_a 4, ballista_b 4. 13 `defender_archer_position` (one inside the gate at 602.94, 548.491, 12.076). 10 `attacker_archer_position`. 6 `WallSegment` (left and right walls with broken and solid children). | Live `TAOM_Map/SceneObj/taom_gondor_town_minas_tirith_forceatmo/scene.xscene`, parsed |
| E63 | `fords_troops.xml` has 54 Gondor troops (each with a campaign twin) and 14 Mordor troops. No Morgul orc troop. | `fords_of_war/fords_troops.xml` |
| E64 | No fow troop has `Culture.battania` (Variags). Harad (`aserai`) fow troops sit at fords_troops.xml lines 8627 to 9933, Easterlings (`khuzait`) at 15085 to 16495. | same |
| E65 | Gothmog is `lord_1_30` (lords.xslt line 3556, race orc, `is_hero` true, `Culture.mordor`) and has no stub. Gorbag `lord_1_63` and the Witch-king `lord_1_15` have stubs. | `lords.xslt`; `custom_battle_lords.xml` lines 25 to 36 |
| E66 | Every soldier spawns on foot: `LongDefeatBattlefieldAdapter.cs:352` passes spawnWithHorse false. A formation slot is Infantry or Ranged (`LongDefeatConfigScope.TrySlot`). | adapter; scope |
| E67 | `CreatureSiegeRules` latches a destroyed gate (`GateTrack`, `SiegeGateReading.cs`). `OversizedCreatureRaces` = cave_troll, hill_troll. | feature doc "Notes for Phase 1"; test `ShippedHelms_FieldsNoOversizedCreature...` |
| E68 | `DreadMissionGate` admits only the Fords, so the Nine project no dread here. Hero aura `witch_king` (lord_1_15; orc, goblin, uruk; morale floor 40, attack eagerness +15) applies in Custom Battle. The affix sampler skips soldiers with a hero aura (#1127). | `hero_auras.json`; feature doc |
| E69 | `ShippedCommanders_EveryRankTroopIsAHeroInLordsXml` reads only `characters/lords.xml` (`is_hero` true). The `Lords()` helper of `CultureProblems` reads lords.xml, then `custom_battle_lords.xml`. | `LongDefeatShippedCommandersTests.cs` lines 41 to 55, 129 to 137 |
| E70 | `LongDefeatSessionPayload` Version = 1 carries every `RunOptions` field. | Co-op payload |
| E71 | The launcher is 144 lines; ADR-002 caps an entry point at 150. | `Hooks/LongDefeatMissionLauncher.cs` |
| E72 | Minas Tirith ships an 11 MB shader sack. S8 loaded 300 soldiers in about 10 s with no shader dialog. S5: the engine was visible with 11 standing points; one soldier used it for about 20 s. One `OnAgentPanicked` warning fired; the first stalemate rule felled 35 attackers with 2 defenders left. | phase1-plan.md:20; long-defeat.md spikes |
| E73 | Reader limits: wave count 101 to 1000; garrison count 1 to 400; commander `every` 2 to 20, bounty 0 to 10000, health 1 to 10; pack size 1 to 50; offer price 1 to 1,000,000; hero and upgrade price 1 to 100,000; hero health factor 1 to 10. | `WavesReader`, `FortressesReader`, `CommandersReader`, `ShopReader`, `HeroesReader`, `UpgradesReader` |

## 4. Data rows

All troop and item ids below were read in the data. Proposed ids are under 4.7. Every edit changes
`balance`.

### 4.1 `fortresses.json`

```json
{
  "id": "minas_tirith",
  "name": "{=TAOM_LD_Fortress_MinasTirith}Minas Tirith",
  "scene": "taom_gondor_town_minas_tirith_forceatmo",
  "defenderCulture": "gondor",
  "attackerCulture": "mordor",
  "playerTroop": "lord_1_9",
  "waves": "minas_mordor",
  "shop": "gondor",
  "garrison": [
    { "troop": "fow_gondor_ano_militia", "class": "Infantry", "count": 100 },
    { "troop": "fow_gondor_ano_archer_militia", "class": "Ranged", "count": 50 }
  ],
  "keep": null
}
```

`muster`, `sally`, `towers`, `volley`, `armoury`, `yard` and `split` stay absent until section 6
records them (D65, D67).

### 4.2 `waves.json`

**Rules.** No change (D69). With D64, add `rules.hard` (proposed: `{ "factor": 1.25, "affixWavesEarlier": 2 }`).

**Bounties** (global table; Isengard troops pay 6 to 20). Proposed by level band. Check them against
the Isengard rows of the same level before shipping.

| Troop | Level | Group | Slot | Bounty |
|---|---|---|---|---|
| `fow_mordor_orc_fighter` | 11 | I | Infantry | 6 |
| `fow_mordor_orc_hunter` | 11 | R | Ranged | 6 |
| `fow_mordor_orc_impaler` | 16 | I | Infantry | 8 |
| `fow_mordor_orc_warrior` | 16 | I | Infantry | 8 |
| `fow_mordor_orc_scout` | 16 | R | Ranged | 8 |
| `fow_morannon_spearman` | 21 | I | Infantry | 10 |
| `fow_mordor_uruk_warrior` | 21 | I | Infantry | 10 |
| `fow_mordor_uruk_crossbow` | 26 | R | Ranged | 12 |
| `fow_mordor_uruk_baraddurguard` | 36 | I | Infantry | 16 |
| `fow_mordor_num_temple_guard` | 46 | I | Infantry | 18 |
| `fow_cave_troll` | 51 | I | Infantry | 20 (Slice 8) |
| `fow_hill_troll` | 51 | I | Infantry | 20 (Slice 8) |

Not used: `fow_mordor_num_knight` (41, C) and `fow_mordor_warg_raider` (11, C). On foot they would
read as a knight without a horse and an orc without a warg.

**Roster `minas_mordor`.** `scaling` copies `helms_isengard`: countPerWave 10, maxCount 300,
healthPerWave 0.05, damagePerWave 0.03, maxFactor 3.0, tierEveryWaves 3.

Tier links (each must pass `EveryTierLink_GoesUpOneLevel_InsideOneClassAndTheAttackerCulture`):

| From | To | Levels | Note |
|---|---|---|---|
| `fow_mordor_orc_fighter` | `fow_mordor_orc_warrior` | 11 to 16 | |
| `fow_mordor_orc_warrior` | `fow_morannon_spearman` | 16 to 21 | |
| `fow_mordor_orc_impaler` | `fow_mordor_uruk_warrior` | 16 to 21 | |
| `fow_mordor_orc_hunter` | `fow_mordor_orc_scout` | 11 to 16 | |
| `fow_mordor_orc_scout` | `fow_mordor_uruk_crossbow` | 16 to 26 | UNVERIFIED whether the test reads 16 to 26 as one level |
| `fow_mordor_uruk_warrior` | `fow_mordor_uruk_baraddurguard` | 21 to 36 | UNVERIFIED, same doubt; drop the link if refused |

Waves. Counts copy Helm's Deep. Weights are proposals.

| Wave | Count | Mix (troop suffix after `fow_mordor_` unless named: weight) |
|---|---|---|
| 1 | 110 | orc_fighter 3, orc_hunter 1 |
| 2 | 120 | orc_fighter 3, orc_impaler 1, orc_hunter 1 |
| 3 | 120 | orc_warrior 2, orc_impaler 1, orc_scout 1 |
| 4 | 125 | orc_warrior 2, `fow_morannon_spearman` 1, orc_scout 1 (captain) |
| 5 | 130 | `fow_morannon_spearman` 2, uruk_warrior 1, orc_scout 1 |
| 6 | 140 | uruk_warrior 2, `fow_morannon_spearman` 1, uruk_crossbow 1 |
| 7 | 150 | uruk_warrior 2, uruk_baraddurguard 1, uruk_crossbow 1 |
| 8 | 170 | uruk_baraddurguard 2, uruk_warrior 1, uruk_crossbow 1 (lieutenant) |
| 9 | 185 | uruk_baraddurguard 2, num_temple_guard 1, uruk_crossbow 1 |
| 10 | 200 | uruk_baraddurguard 2, num_temple_guard 1, uruk_crossbow 1; Slice 8 adds `fow_cave_troll` 1 |

**Commanders** (copy the Helm's Deep shape and numbers):

```json
"commanders": {
  "every": 4,
  "ranks": [
    { "rank": "captain", "name": "{=TAOM_LD_Rank_Mordor_Captain}Gorbag, Captain of Minas Morgul", "troop": "lord_1_63", "bounty": 150, "health": 2.0, "abilities": ["war_cry"] },
    { "rank": "lieutenant", "name": "{=TAOM_LD_Rank_Mordor_Lieutenant}Gothmog, Lieutenant of Morgul", "troop": "lord_1_30", "bounty": 250, "health": 3.0, "abilities": ["war_cry", "summon_uruks"] },
    { "rank": "general", "name": "{=TAOM_LD_Rank_Mordor_General}The Witch-king of Angmar", "troop": "lord_1_15", "bounty": 400, "health": 4.0 }
  ],
  "beyond": "copy helms_isengard (0.25, 0.9, 5, 10)",
  "abilities": {
    "war_cry": { "kind": "WarCry", "name": "{=TAOM_LD_Ability_WarCry_Mordor}The Lidless Eye Is Upon Them", "firstSeconds": 20, "cooldownSeconds": 60, "durationSeconds": 15, "swing": 15, "move": 10 },
    "summon_uruks": { "kind": "Summon", "name": "{=TAOM_LD_Ability_Summon_Mordor}Uruks of Mordor", "firstSeconds": 30, "cooldownSeconds": 120, "count": 20,
      "mix": [ { "troop": "fow_mordor_uruk_warrior", "class": "Infantry", "weight": 3 }, { "troop": "fow_mordor_uruk_crossbow", "class": "Ranged", "weight": 1 } ] }
  }
}
```

The JSON field names inside `beyond` and the war cry (`swing`, `move`, `durationSeconds`) are
written from the Helm's Deep values; copy the exact keys from `helms_isengard` when building. The
rank name key pattern of Helm's Deep was not recorded in this draft: match it. After Slice 8, a
`summon_trolls` ability for the general (Summon, count 2, `fow_cave_troll` and `fow_hill_troll`)
is an option (OQ24).

### 4.3 `shop.json`, shop `gondor`

`startingGold` 600, as Rohan. Offer rows take Rohan's shape (`id`, `name` with a `TAOM_LD_Offer_` key,
`troop`, `class`, `packSize`, `price`). Offer names get a `Gondor` key infix so no key repeats.

| id | Name | Troop | Lvl | Class | Pack | Price | Lore |
|---|---|---|---|---|---|---|---|
| `levy` | Anorien Levy | `fow_gondor_ano_militia` | 11 | Infantry | 12 | 180 | Militia of the city's fief |
| `anorien_archers` | Anorien Archers | `fow_gondor_ano_archer_militia` | 11 | Ranged | 10 | 180 | |
| `lossarnach_axes` | Axemen of Lossarnach | `fow_gondor_loss_axebearer` | 16 | Infantry | 8 | 200 | Forlong |
| `morthond_bowmen` | Morthond Bowmen | `fow_gondor_brv_bowman` | 16 | Ranged | 8 | 200 | Duinhir of the Blackroot Vale |
| `pinnath_gelin` | Men of Pinnath Gelin | `fow_gondor_pg_spearman` | 21 | Infantry | 8 | 240 | Hirluin the Fair |
| `citadel_guard` | Guards of the Citadel | `fow_gondor_mt_trainee` | 26 | Infantry | 6 | 360 | Beregond's company |

Rohan's top pack is level 31 at 6 for 360; `citadel_guard` is level 26. Dol Amroth's men
(`fow_gondor_da_footman`, 21) are the swap if Mike wants Imrahil's own men in the shop. Prices are
the maintainer's at the balance pass (D59).

**Upgrades.** Rows take Rohan's shape: `from`, `to`, `price`, `name` with a `TAOM_LD_Up_` key. `to` is
the fow twin when one exists, else the campaign troop (Rohan does the same:
`fow_rohan_westfold_axeman` to `rohan_westfold_veteran_axeman`). Every step is an edge of
`troops/troops_gondor.xml` (read there).

| from | to | Price |
|---|---|---|
| `fow_gondor_ano_militia` | `fow_gondor_ano_footman` | 15 |
| `fow_gondor_ano_footman` | `fow_gondor_ano_guardsman` | 25 |
| `fow_gondor_ano_guardsman` | `gondor_ano_infantry` | 40 |
| `gondor_ano_infantry` | `gondor_ano_vet_infantry` | 60 |
| `fow_gondor_ano_archer_militia` | `fow_gondor_ano_skirmisher` | 15 |
| `fow_gondor_ano_skirmisher` | `fow_gondor_ano_bowman` | 25 |
| `fow_gondor_ano_bowman` | `gondor_ano_archer` | 40 |
| `fow_gondor_loss_axebearer` | `gondor_loss_vet_axebearer` | 25 |
| `gondor_loss_vet_axebearer` | `gondor_loss_guard` | 40 |
| `fow_gondor_brv_bowman` | `fow_gondor_brv_scout` | 25 |
| `fow_gondor_brv_scout` | `fow_gondor_brv_archer` | 40 |
| `fow_gondor_pg_spearman` | `gondor_pg_vet_spearman` | 40 |
| `gondor_pg_vet_spearman` | `gondor_pg_spearwarden` | 60 |
| `fow_gondor_mt_trainee` | `gondor_mt_veteran` | 60 |
| `gondor_mt_veteran` | `fow_gondor_mt_sergeant` | 90 |

15 steps against Rohan's 9. Cut to the first two of each line if Mike wants Rohan's count.
Whether a campaign-only troop (`gondor_ano_infantry` and the others) spawns in Custom Battle:
`troops_gondor.xml` loads for CustomGame (SubModule.xml:241), and Rohan already upgrades into
campaign troops, so this is the same path.

**Heroes.** `aragorn` 350, `legolas` 300, `gimli` 300, health factor 8 (Rohan's rows). Gandalf,
Beregond and Pippin are ids to create (4.7, D60).

**Fortifications.** Copy Rohan: gate repair 200; gate levels 50/200, 100/300, 150/450; field care
0.75/150/1. No `elvenVolley` (D61). `towers` waits for posts (section 6); proposed tower troop
`fow_gondor_ano_skirmisher` (16, Ranged), the level of Rohan's `fow_rohan_eastfold_bowman`.

### 4.4 `classes.json`, kits for `gondor`

Every item is carried by a Gondor fow troop and was found in the live installs.

| Class | Items | Carrier |
|---|---|---|
| `sword_and_board` | `wm_swan_knight_swordb`, `sm_gd_shield_b1` | `fow_gondor_mt_trainee` |
| `two_handed` | `wm_gondor_lossarnach_2h_axe_black_ash_a` (no exception needed) | `fow_gondor_loss_noble_captain` |
| `archer` | `ladder_blackroot_bow_t3` (Armory `gondor/ranged_ladder.xml:42`), `bodkin_arrows_a`, `piercing_arrows`, `wm_pelargir_sword_a02` | `fow_gondor_brv_bowman`; `piercing_arrows` from `fow_gondor_ith_longbowman` |
| `cavalry` | Item0 `wm_swan_knight_lance_b`, Item1 `sm_gd_shield_a3`, Item2 `wm_gondor_boromir_sword`, Head `sk_gd_dol_cav_helmet_elite_a`, Body `sk_gd_dol_chest_heavy_a`, Cape `sk_gd_dol_pauld_cape_noble_heavy_a`, Gloves `sk_gd_dol_bracer_heavy_a`, Leg `sk_gd_dol_grvs_heavy_a`, Horse `noble_horse_imperial`, HorseHarness `gondor_horse_armor_1_da` | `fow_gondor_da_knight` (one troop's gear, as `TheCavalryKit_IsOneRohanRiderTroopsGear` wants for Rohan) |

Text changes (one text per class across cultures, `ClassesReader`): the cavalry text loses "rider of
the Mark". Proposed: "A rider in heavy armour, with lance, shield and sword, and a horse to carry
you into the fight." Keep the promise the no-sally test pins (read that test's wording before the
edit). The archer text is already neutral.

### 4.5 `boons.json`, `affixes.json`: fortress-neutral wording (D63)

Boons are global (E60), so a Rohan name shows at Minas Tirith. Ids, effects and keys stay; only the
default English text changes, and the 12 language rows for each changed key are reset and
re-translated (loc-key trap). Current names read at HEAD; proposed names are drafts.

| id | Now | Proposed name | Text change |
|---|---|---|---|
| `riddermark_levy` | Levy of the Riddermark | Levy of the Fiefs | "Reeves of the Westfold send coin" to "The fiefs send coin" |
| `weregild_of_the_mark` | Weregild of the Mark | Blood Price | none |
| `haggler_of_edoras` | The Haggler of Edoras | The Quartermaster's Haggling | none |
| `wains_of_the_westfold` | Wains of the Westfold | Wains from the Fields | none |
| `edge_of_the_mark` | Edge of the Mark | A Keen Edge | none |
| `rohirrim_fury` | Fury of the Rohirrim | Fury of the West | none |
| `longbow_of_the_mark` | The Longbow of the Mark | The Longbow of the West | none |
| `gallop_of_the_eorlingas` | Gallop of the Eorlingas | At the Gallop | none |
| `spear_of_eorl` | Spear of Eorl | Spear of the West | none |
| `oath_of_eorl` | Oath of Eorl | An Oath Sworn | none |
| `spears_of_the_westfold` | Spears of the Westfold | Spears of the Line | none |
| `fleet_horses_of_the_mark` | Fleet Horses of the Mark | Fleet Horses | none |
| `hearts_of_the_eored` | Hearts of the Eored | Hearts of the Riders | none |

Unchanged: `riders_at_dawn` (fits both places), `glittering_caves_refuge` (name already "Refuge of
the Inner Keep"; only its id names Helm's Deep). Its effect needs a keep: whether the deck drops a
keep boon at a fortress with `keep: null` is UNVERIFIED (R38).

Affixes: no change. `names.mordor` is complete and pinned (terror "Winged Terror", hammer "Hammer
of the Underworld").

### 4.6 Character data

- `custom_battle_lords.xml` (CRLF): add `<NPCCharacter id="lord_1_30" culture="Culture.mordor" />`
  beside the Mordor stubs (lines 25 to 36). `CustomBattleLordStubsTests` allows a stub when
  `lords.xslt` holds a whole template and `lords.xml` has no row; `lord_1_30` meets both (E65).
- No new fow troop.

### 4.7 Ids to create

| Id | File | Note |
|---|---|---|
| `minas_tirith` (fortress) | `long_defeat/fortresses.json` | |
| `TAOM_LD_Fortress_MinasTirith` | `fortresses.json` and the language files | Name key; match the Helm's key pattern |
| `minas_mordor` (roster) | `long_defeat/waves.json` | |
| `TAOM_LD_Rank_Mordor_Captain`, `_Lieutenant`, `_General` | `waves.json`, language files | Match the Helm's rank key pattern (not recorded here) |
| `TAOM_LD_Ability_WarCry_Mordor`, `TAOM_LD_Ability_Summon_Mordor` | `waves.json`, language files | Follow `TAOM_LD_Ability_WarCry_Isengard` |
| `gondor` (shop) | `long_defeat/shop.json` | |
| `TAOM_LD_Offer_Gondor_*` (6), `TAOM_LD_Up_Gondor_*` (15) | `shop.json`, language files | |
| `lord_1_30` stub | `characters/custom_battle_lords.xml` | |
| `taom_hero_gandalf`, `taom_hero_beregond`, `taom_hero_pippin` (and `taom_hero_imrahil` if D51 picks Faramir) | `characters/taom_mission_heroes.xml`, then `shop.json` heroes | Only if D60 says yes. Gandalf exists only as `taom_fui_gandalf` (UI picker) |
| `rules.hard` | `waves.json` | Only with D64 |
| `attackerAllies` | `fortresses.json` schema | Only with D53 |
| A Variags troop (`Culture.battania`) | `fords_of_war/fords_troops.xml` | Only with D53; none exists |

## 5. Code changes

| Area | Change | Size note |
|---|---|---|
| Launcher `Hooks/LongDefeatMissionLauncher.cs` | No fortress code needed: it already refuses an unknown fortress and missing troops. Difficulty reaches it through `RunOptions`, not new lines. | 144 of 150 lines (E71): any new logic goes to a service |
| `Domain/RunOptions.cs` | Add `Difficulty` (`Normal`, `Hard`), default `Normal`. | |
| `LongDefeatSetupPrompt.cs` | A difficulty question between fortress and class. "Best run" per fortress and difficulty. | |
| Catalog `BuildPlan` | With `Hard`: scale wave counts and per-wave growth by `rules.hard.factor`, shift the affix schedule by `affixWavesEarlier`. Pure function, unit tested. Wave count stays inside 101 to 1000 and `maxCount`. | |
| Report and leaderboard | Report field `difficulty`; board key fortress, difficulty, balance. | leaderboard-plan.md line 134 grows one key |
| Co-op payload | Carry `Difficulty`; `LongDefeatSessionPayload` Version 1 to 2 (E70). | No co-op play in scope |
| Troll gate reset (Slice 8) | Clear the `GateTrack` latch when the gate is repaired, or stand the creature role down in this mission. Recommendation: clear on repair, because Helm's Deep also repairs gates. | `CreatureSiegeRules`, `SiegeGateReading.cs` |
| `attackerAllies` (only with D53) | Fortress schema field, reader, side test and own-troops rule accept the listed cultures for wave troops. | |
| Tests to generalize (Slice 1) | `ShippedShop_EveryUpgradeStep_...` reads each fortress's shop and its culture's troops file (today: helms, `troops_rohan.xml`, 9 steps). `EveryClassHasAKitForTheFortressesDefenderCulture` loops every fortress. `ShippedCommanders_EveryRankTroopIsAHeroInLordsXml` also accepts a `custom_battle_lords.xml` stub whose `lords.xslt` template is `is_hero` (E69). Helm's-only pins (`TheFourVeteranTroops...`, the helms ranks and ability pins, `ShippedHelms_...`) stay helms-only and gain Minas Tirith twins where the rule is general. | |
| Wrapper `tools/run_long_defeat.py` | `CLASSES = class_offer(DEFAULT_FORTRESS)` (line 243), `class_choice` (2088) and `class_for_run` (2146) take the run's fortress. Add `--difficulty` and the arm key `difficulty` beside `("fortress", ...)` in `arm_pairs` (2154). The judge reads sally, split and towers per fortress already; add the difficulty to the report check. | |
| `tools/ingame_checks/1079.toml` | Rows today carry no fortress token (boot pass `result=pass deployment_finished=true siege_ai=true main=true friendly=false`). Add Minas Tirith rows pinned with `fortress=minas_tirith`, or a separate manifest. Recommendation: a token in the log line and per-fortress rows, because an older row that matches a check id alone reads a new producer's line as FAIL. | Log producers need the token |

## 6. Recording session at Minas Tirith (Mike, D67)

Start a Long Defeat run with `--fortress minas_tirith` and stand at each place. One
`taom.ld_record` per kind, then `taom.ld_export`.

| Kind | Where at Minas Tirith | Note |
|---|---|---|
| `keep [radius]` | The last stand inside the first circle, behind the inner gate (602.832, 563.353, 12.063). | The keep frame is UNVERIFIED. The issue forbids falling back through the circles, so the keep stays in the first circle. |
| `muster_foot` | Behind the outer gate, between the two gates. | |
| `muster_archers` | On the wall walk (about z 25.5) near the gate towers. | |
| `sally` | The outer gate (603.009, 571.746, 12.063), facing the Pelennor. | |
| `post <tower>` | Each wall tower that should hold tower archers; Slice 7 names them. | Waits for Phase 4 towers |
| `tower_volley <tower>` | The fall point of each tower's volley. | Waits for Phase 4 towers |
| `volley` | The volley fall point in front of the gate. | |
| `armoury` | A building inside the first circle. | Waits for Phase 4 armoury |
| `yard` | An open place inside the first circle. | Waits for Phase 4 yard |

The scene's 13 `defender_archer_position` entities (one inside the gate) and 8 deployment points
are the engine's; they do not replace these records.

## 7. Slices

Tiers per model-effort-selection.md. Each proof run is `python tools/run_long_defeat.py --fortress
<id> ...` with the flags of the wrapper's `--help`; a `fast-reader` runs it and reads the report.

| # | Tier | Slice | Tests first | Proof run | Maintainer in-game |
|---|---|---|---|---|---|
| 1 | implementer | Generalize the helms-only tests and the wrapper's class lookup per fortress. No data change. | The three tests in section 5 fail on a synthetic second fortress, then pass. Wrapper `tools/tests` for `class_for_run` with `--fortress`. | `--fortress helms`: the Helm's Deep run judges as before. | None |
| 2 | implementer | Fortress row, roster `minas_mordor` without commanders, shop `gondor` packs only, Gondor kits, the cavalry text. | Shipped tests load `minas_tirith` with no warning; every troop exists, sides and classes match; kits carried by Gondor troops. | `--fortress minas_tirith`, waves 1 to 3: spawn frames, gate tags, split (A36, A37, D65). | Look at the opening: where defenders stand, where Mordor comes from. |
| 3 | implementer | Commanders and the `lord_1_30` stub; Mordor ability names. | Rank test accepts stubs; Mordor ranks are Mordor lords; summons are Mordor troops. | Run to wave 12: captain, lieutenant, general spawn and die (A38). | Watch the Witch-king general (D54). |
| 4 | implementer | Upgrades, heroes, fortifications. | Every step is an edge of `troops_gondor.xml`; prices within limits. | Autoplay buys every step and hero; the sally with the cavalry kit (A39, A40). | One class each, short run. |
| 5 | implementer | Boon wording (D63) and language rows. | Shipped boon tests unchanged (ids and rolls); a test that no shipped boon or class text names a place of one fortress (list of names). | `--fortress helms` and `minas_tirith`: same rolls for a seed as before the rename. | None |
| 6 | architect plan, implementer | Difficulty (D64): `RunOptions`, prompt, plan scaling, report, board key, payload v2, wrapper `--difficulty`, manifest. | Scaling is a pure function (counts, growth, affix waves); payload round trip v2; board key. | Both fortresses at `hard`, waves 1 to 5: counts and affix waves match the plan. | Pick hard from the menu once. |
| 7 | Mike, then implementer | Recording session (section 6), then the exported places into `fortresses.json`. | Readers accept the rows; points inside the scene bounds. | Full run with places. | The session itself. |
| 8 | architect plan, implementer | Troll gate reset; trolls in wave 10 and later; optional `summon_trolls`. | `GateTrack` clears on repair; the oversized-creature pin moves from "never" to "after the reset". | Run with a repaired gate and trolls (A42). | Watch a troll at a repaired gate. |
| 9 | architect | Optional: `attackerAllies` with Easterlings and Haradrim (D53). | Side test accepts listed allies only. | `--fortress minas_tirith` to wave 10. | Decide on D53 first. |
| 10 | Mike | Balance pass: prices, bounties, wave weights. | None | Several full runs per difficulty. | The play. |

## 8. Risks

| # | Risk | Mitigation |
|---|---|---|
| R34 | The Witch-king's `nazghul` body or his cleared horse breaks the commander spawn. | Slice 3 proof; fall back to Gothmog as general (D54). |
| R35 | His hero aura pulls him out of the affix sample (#1127), so affix effects look absent near him. | Expected; note it in the report check. |
| R36 | Frame time at full waves is worse than the S8 spike (14.3 ms means at 300). | D69: a per-fortress cap only on evidence. |
| R37 | Four ladders and two towers split the garrison differently from Helm's Deep; the stalemate rule felled 35 attackers with 2 defenders left in S8. | Slice 2 proof; D65 split from evidence. |
| R38 | A keep boon rolls at a fortress with no keep. | Check in Slice 5; filter or keep if the effect is a no-op. |
| R39 | The archer position inside the gate draws archers to the gate floor (z 12) where rams strike. | Watch in Slice 2. |
| R40 | Any data edit changes `balance`: Helm's Deep opens a new board. | Expected (E59); land data edits together. |
| R41 | Renaming boons stales 12 languages. | Reset the rows; one key per id. |
| R42 | The latch fix changes Helm's Deep gate behaviour for creatures. | Helm's fields no creature today (pinned). |

## 9. Open questions

| # | Question | Recommendation |
|---|---|---|
| OQ21 | Player troop: Imrahil or Faramir (D51)? | Imrahil: a mounted prince fits the sally and the cavalry class. |
| OQ22 | The Witch-king as a killable general (D54)? | Yes for play; his fall at the wall is a game licence. Else Gothmog general. |
| OQ23 | Difficulty: two levels with one factor, or start-wave only (D64)? | Two levels, factor 1.25, affixes 2 waves earlier. |
| OQ24 | Trolls: wave mix only, or also a general's summon? | Wave mix first; the summon after a proof run. |
| OQ25 | Easterlings and Haradrim now (D53)? | Later slice. Mumakil stay out (D57). |
| OQ26 | New heroes (D60)? | Gandalf first; Beregond and Pippin later. |
| OQ27 | Boon rewording or a fortress filter (D63)? | Rewording. |
| OQ28 | The Rohirrim at dawn and the fleet (D68)? | A later issue that also builds mounted reinforcements. |
| OQ29 | 15 Gondor upgrade steps or Rohan's 9? | 15: each out-company has its own line. |

## 10. Unverified

- Which side each of the 8 deployment points belongs to, and its radius.
- The gate tags (the gate entities carry none; the prefab's are not read).
- The siege spawn frames (no spawn-frame tag found in the scene file).
- The keep frame at Minas Tirith.
- The Witch-king (`nazghul`) as a commander on foot.
- `noble_horse_imperial` and `gondor_horse_armor_1_da` at spawn.
- The Lossarnach two-handed axe in the two-handed slot.
- Frame time for full waves at Minas Tirith beyond the S8 spike.
- The creature siege role activating in this mission.
- `CommanderPlan.MaxSummon` = 50 (from a comment only).
- Whether the tier test accepts the 16 to 26 and 21 to 36 links.
- Proposed bounties by level band (the Isengard bounty per troop was not matched by level).
- The exact JSON keys of `beyond` and the war cry, and the Helm's rank name key pattern.
- Whether a keep boon drops at a fortress with `keep: null`.
- The `taom.ld_record` kind names as written here; confirm with the command's help.
- How the director spawns each troop's equipment in the first circle (no run yet).
