# Cultural Feats — Expansion Roadmap

**Status:** Wave 1 SHIPPED 2026-06-07 (24 feats, 105 → 129). Waves 1.5 / 2 / 3 are proposals.
**Source of this doc:** promoted from a plan-mode artifact so the full menu survives across sessions. Companion to the feature doc [`docs/features/cultural-feats.md`](../features/cultural-feats.md) (what's actually shipped) and the Wave 1 RCA [`docs/reviews/rca-cultural-feats-wave1-2026-06-07.md`](../reviews/rca-cultural-feats-wave1-2026-06-07.md).

## Why this exists

The #260 faction-map rewrite exposed that several cultures have thin cultural-feat coverage (Dale/Khand had 1 TAOM feat; Harad/Rhûn 2; the new Goblin / Misty Mountain Orcs cultures 4 baseline). This roadmap enumerates lore-fitting additional feats per culture (positive + negative), classified by implementation cost, so future sessions can pick the next batch without re-researching.

## The Wave model (implementation-cost classification)

Every proposed feat is tagged **Q / E / N**:

| Tag | Meaning | Cost |
|---|---|---|
| **Q** | Quick — plugs into an EXISTING `CulturalFeatsService.Apply*` method via one `HasFeat` check | ~10 LOC + XML feat entry + dispatch test + faction-map line |
| **E** | Extension — adds a new override method to an EXISTING `Taom*Model` (e.g. post-victory morale on `TaomPartyMoraleModel`) | ~20-40 LOC + new service `Apply*` method + tests |
| **N** | New model — a brand-new `Taom*Model` overriding a vanilla `Default*Model` not yet wrapped (prisoner take rate, sight range, persuasion, education XP) | ~80-120 LOC + IoC reg + ADR-007 adapter + tests |

**Wave 1 = all Q-class.** Wave 2 = E-class. Wave 3 = N-class. Conditional feats (terrain/time/troop-% gated) are at least E even if they look Q, because they need new condition logic in the service method.

## Wave 1 — SHIPPED (24 feats, 2026-06-07)

See [`docs/features/cultural-feats.md` → Wave 1 Expansion Feats](../features/cultural-feats.md#wave-1-expansion-feats-24--2026-06-07) for the exact table. Summary: Mordor smithing; Erebor tariff; Umbar raid+food; Lothlorien volunteer(neg); Mirkwood army-cost(neg); Goblin smithing+raid; Misty Mtn Orcs smithing+raid+construction(neg); Dale tariff+renown+loyalty(neg); Khand renown+tariff(neg)+food+party; Harad morale+food+raid+army-cost(neg); Rhûn loyalty(neg)+raid.

**Axis-collision audit caught one drop:** a proposed Goblin party-size feat — Goblin already has "Goblin Swarm +40%".

## Wave 1.5 — deferred conditionals (small E, next-cheapest)

These were proposed as Q in the menu but actually need conditional logic in the service method (hence deferred from Wave 1's strict-Q scope):

- **Goblin — Sunlight Aversion** — −10% party speed in daylight (`!isNight` branch in `ApplyTerrainSpeedFeats`) [-]
- **Mirkwood — Spider-Tainted Paths** — −10% party speed outside forest (terrain-conditional) [-]
- **Rhûn — Cavalry-Only Culture** — −5% party speed when infantry >50% (mirror Rohan's existing infantry penalty) [-]
- **Mirkwood — Thranduil's Vaults** — −10% garrison wage (garrison wage lives in `TaomPartyWageModel` in TroopProgression, not `CulturalFeatsService` — different file, but still a HasFeat add) [+]

## Wave 2+ — per-culture proposal menu

Tag legend: `Q`/`E`/`N` cost · `[+]` positive · `[-]` negative. ✅ = shipped in Wave 1.

### Custom cultures

**Gondor** (7 today) — Beacon Network +15% army influence award in defensive war `E[+]` · Rangers of Ithilien +20% prisoner take `N[+]` · Citadel of Stars +10% wage outside home territory `E[-]` · Men of the West +5% morale vs evil factions `Q[+]` · Stoic Defeat −25% gold loss after defeat `E[+]` · Martial Nobility +10% influence/battle `E[+]` · Dol Amroth Levy −8% cavalry upgrade cost `Q[+]` · Steward's Caution −10% renown in peacetime `E[-]`

**Mordor** (12 today) — Eye of Sauron +15% sight range `N[+]` · Slave-Takers +25% prisoner take `N[+]` · Black Speech Coordination +10% raid when ≥2 parties raid same settlement `E[+]` · Morgul Corruption −10% loyalty/day in non-Mordor settlements `Q[-]` · ✅ Dark Smithing −15% smithing `Q[+]` · Iron Will −50% no-wage morale penalty `E[+]` · Plunderers +20% raid loot multiplier `E[+]`

**Isengard** (13 today) — Tireless Uruk-hai −10% food `Q[+]` · Palantír of Orthanc +15% sight `N[+]` · Deforestation Engine +20% raid vs forest settlements `E[+]` · Saruman's Coin −10% caravan cost `Q[+]` · War Machine Momentum +5% construction per war season `E[+]` · Bred Without Mercy −5 morale away from home >30d `E[-]`

**Dol Guldur** (10 today) — Corrupted Forest +5% forest speed `Q[+]` · Shadow Terror −3 morale to adjacent enemies `N[+]` · Slave Pens +15% prisoner take `N[+]` · Necromantic Drain −1 loyalty/day all settlements `Q[-]` · Dark Sorcery +10% decision relation penalty `Q[-]` · Black Gate Network extra −10% army influence cost `Q[+]`

**Gundabad** (10 today) — Grudge-Bearer +10% raid vs Erebor `E[+]` · Mountain Passes Control +15% sight on snow `N[+]` · Warg Packs −10% mounted wage `Q[+]` · Orc Tunnelers +10% construction for fortifications `E[+]` · Bolg's Cruelty −5 morale after failed raid/siege `E[-]`

**Umbar** (5 today) — ✅ Corsair Raid Doctrine +20% raid `Q[+]` · Slave Trade Network +15% prisoner ransom `N[+]` · ✅ Black Numenorean Endurance −10% food `Q[+]` · Harbor Tariffs Expansion +10% town market income `E[+]` · Workshop Guilds +10% workshop income `E[+]` · Maritime Tradition +25% caravan initial gold +10% elite spawn `E[+]` · Black Fleet Terror −3 enemy morale on coastal/plain attack `N[+]`

**Erebor** (7 today) — Iron Hills Alliance +10% army influence award when allied lord reinforces `E[+]` · Tunnel Expertise reduce −15% construction penalty to −5% underground `E[+/-]` · Dwarven Stubbornness in Siege +1 garrison morale `E[+]` · Grudge-Debt +15% raid vs Gundabad/Mordor `E[+]` · Master Smith Quality +10% Fine/Masterwork bias `E[+]` · Dragon-Hoard Mentality −15% renown from peace treaties `E[-]` · ✅ Dwarven Thrift +5% tariff `Q[+]`

**Rivendell** (6 today) — Lore-Keeper Advantage +10% troop XP `N[+]` · Council of the Wise −15% army influence cost in defensive coalitions `E[+]` · Fading People −10% volunteer respawn `Q[-]` · Noldor Craftsmanship −10% smithing `Q[+]` · Gil-galad's Legacy +15% renown `Q[+]` · Elven Pride Burden +10% relation penalty making peace with evil `Q[-]` · Mirror of Imladris +10% sight `N[+]`

**Mirkwood** (5 today) — Woodland Ambush +15% raid in forest `E[+]` · ⏳1.5 Thranduil's Vaults −10% garrison wage `Q[+]` · ⏳1.5 Spider-Tainted Paths −10% speed outside forest `Q-cond[-]` · Silvan Archery −8% ranged recruit cost `E[+]` · Forest Lore +5% sight on forest `N[+]` · ✅ Isolationist Court +15% army influence cost `Q[-]`

**Lothlorien** (6 today) — Mirror of Galadriel +15% sight `N[+]` · Nenya's Preservation +0.5 loyalty/day in forest settlements `E[+]` · ✅ Fading Light −15% volunteer respawn `Q[-]` · Galadhrim Cloaks −10% party detection range `N[+]` · Celeborn's Strategy +10% army influence award defending forest `E[+]` · Timeless Ennui −5% renown from non-combat `E[-]`

### NEW playable cultures (Goblin, Misty Mountain Orcs)

**Goblin** (`goblin`, 4 today — Goblin Town + Blue Craig share it) — Tunnel-Dwellers +sight/terrain `N or Q-cond[+]` · ~~Swarm Tactics +5% party~~ (DROPPED — axis collision with Goblin Swarm +40%) · ⏳1.5 Sunlight Aversion −10% daylight speed `Q-cond[-]` · ✅ Captured-Weapon Hoard −10% smithing `Q[+]` · ✅ Goblin Ambush +10% raid `Q[+]` (added in place of Swarm Tactics) · Goblin Cowardice −30% post-defeat morale `E[-]` · Mountain Tunnels +15% construction for caves `E[+]` · Goblin Slavers +15% prisoner take `N[+]`

**Misty Mountain Orcs** (`mistymountainorcs`, 4 today — Moria) — Dug-In Defenders +garrison morale/loyalty `E[+]` · ✅ Looted Forges −15% smithing `Q[+]` · Balrog's Shadow −5 adjacent-enemy morale `N[+]` · Mithril Veins +25% mountain production `E-cond[+]` · ✅ Echoing Halls −10% construction `Q[-]` (shipped as construction-speed, not the proposed relationship-penalty form) · Drum in the Deep +25% relation penalty with Dwarves `Q[-]` · ✅ Cave Troll Levy +15% raid `Q[+]`

### XSLT-wrapped cultures

**Rohan** (vlandia, 6) — Muster of the Mark −20% army influence cost in defensive war `E[+]` · Last Stand of Helm's Deep +15% garrison morale `E[+]` · Theoden's Charge +5% renown when cavalry >60% `E[+]` · No Infantry Tradition extend speed penalty to garrison `E[-]`

**Dunland** (empire, 6) — Grudge Against Rohan +15% raid vs vlandia `E[+]` · Hill-Tribe Ferocity +5 morale `Q[+]` · Saruman's Pawns +15% relation penalty with Free Peoples `Q[-]` · Scattered Clans −5% army influence award `Q[-]`

**Dale** (sturgia, 1→4) — ✅ Dwarven Trade Alliance +10% tariff `Q[+]` · ✅ Black Arrow Tradition +10% renown `Q[+]` · Black Arrow Precision −8% ranged recruit cost `E[+]` · ✅ Small Territory Exposure −0.5 loyalty/day `Q[-]` (shipped as flat loyalty, not the proposed isolation-conditional)

**Khand** (battania, 1→5) — ✅ Mercenary Premium +8% renown `Q[+]` · ✅ Tribute to Mordor −10% tariff `Q[-]` · ✅ Steppe Endurance −10% food `Q[+]` · ✅ Charioteer Mobility +5% party `Q[+]`

**Harad** (aserai, 2→6) — ✅ Mumakil Drivers +5 morale `Q[+]` · ✅ Desert Endurance −15% food `Q[+]` · ✅ Far Harad Savagery +15% raid `Q[+]` · ✅ Divided Tribes +15% army influence cost `Q[-]`

**Rhûn** (khuzait, 2→4) — Wainrider Formation +10% garrison effectiveness `E[+]` · ✅ Easterling Tribute −0.5 loyalty/day `Q[-]` (shipped as flat loyalty) · ✅ Steppe Raider Doctrine +15% raid `Q[+]` · ⏳1.5 Cavalry-Only Culture −5% speed when infantry >50% `Q-cond[-]`

### Minor cultures (not playable in CC)

**Shaghana** / **Abanissa** (1 each, desert speed only) — Desert Raider +15% raid `Q[+]` · Arid Sustenance −10% food `Q[+]` · No Kingdom / Fractured Clans loyalty/influence penalty `Q[-]`. Low priority (non-selectable; AI-only impact).

## Per-faction differentiation (shared-culture factions)

Two factions share one culture each. Same mechanical feats; differentiate via `factions.json` `name`/`description`/`traits`/`strengths`/`weaknesses` text only (no mechanical divergence without a culture split):
- **Goblin Town vs Blue Craig** (both `goblin`): Great Goblin's hall / chokepoint-home vs Blue Mountain feuding warbands.
- **Imladris vs Lindon** (both `rivendell`): Council of the Wise / hidden valley vs Cirdan's Grey Havens / departure to Valinor.

## Untapped GameModel surface (for Wave 2 E-class)

Existing `Taom*Model`s with untapped methods on the same vanilla base (extend these, no new model needed):

| Existing model | Untapped methods → feat ideas |
|---|---|
| `TaomPartyMoraleModel` | `GetVictoryMoraleChange` / `GetDefeatMoraleChange` (glory/fanaticism), `GetDailyStarvationMoralePenalty`, `GetDailyNoWageMoralePenalty` (iron will) |
| `TaomSettlementMilitiaModel` | `CalculateMilitiaChange` (rate), `CalculateMilitiaSpawnRate` (melee/ranged split — Mirkwood/Lothlorien archery) |
| `TaomSmithingModel` | `GetSkillXpForSmithingInFreeBuildMode` (Noldor lore), `ResearchPointsNeedForNewPart`, `GetCraftedWeaponModifier` (master-smith quality) |
| `TaomBattleRewardModel` | `CalculateInfluenceGain` (martial nobility), `GetLootItemChancesForWinnerParties`, `CalculateGoldLossAfterDefeat` (stoic defeat) |
| `TaomClanFinanceModel` | `CalculateOwnerIncomeFromWorkshop` / `CalculateOwnerIncomeFromCaravan` / `CalculateVillageIncome` (Umbar mercantile) |
| `TaomArmyManagementModel` | `CalculateDailyCohesionChange` (disciplined host / fractious horde) |
| `TaomRaidModel` | `GetRaidLootMultiplier` (plunderers) |
| `TaomCaravanModel` | `GetInitialTradeGold`, `GetEliteCaravanSpawnChance` (maritime tradition) |

**Wave 3 N-class new models:** `DefaultPrisonerSizeModel`/`DefaultRansomValueModel` (slave-takers/ransom), sight-range hook on `DefaultPartySpeedCalculatingModel.GetPartySightRange` (Eye of Sauron / Mirror of Galadriel — decompile to confirm v1.4.5 surface), `DefaultCombatXpModel` (lore-keeper), `DefaultPersuasionModel` (silver tongue), `DefaultEducationDevelopmentModel` (Elrond's fostering).

## Implementation procedure

Per-feat steps are in [`docs/features/cultural-feats.md` → How-To: Add a new feat](../features/cultural-feats.md#add-a-new-feat-to-an-existing-culture). Process discipline (learned the hard way in Wave 1): **run `/deep-review` + `/review-codex` BEFORE the closing commit, and open the GitHub issue before committing** — see `feedback_review_before_commit_not_after`. For Q-class feats, also add the per-(culture,axis) dispatch test AND pin production metadata (`feedback_mirror_table_drifts_from_production`).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/cultural-feats.md](../features/cultural-feats.md)
- [docs/reviews/rca-cultural-feats-wave1-2026-06-07.md](../reviews/rca-cultural-feats-wave1-2026-06-07.md)

<!-- backlinks-end -->
