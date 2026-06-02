# Codex Adversarial Review — New Factions (Misty Mountain Orcs / Goblins / Goblin Town / Blue Craig / Lindon)

Model: gpt-5.5 (xhigh) · 2026-06-02 · prompt: [codex-adversarial-new-factions-2026-06-02.prompt.md](codex-adversarial-new-factions-2026-06-02.prompt.md)
Verdict: 0 CRITICAL / 0 HIGH / 2 MEDIUM / 2 LOW. Verification + fixes: see [rca-new-factions-2026-06-02.md](rca-new-factions-2026-06-02.md) Phase 2/3 and [REVIEW-LOG.md](REVIEW-LOG.md) Review 47.

_(Trimmed to the review output; the full codex exec reasoning trace was discarded — 2.7MB.)_

---

**KNOWN SUSPECTS VERDICTS**

1. **PARTIAL** — The prompt’s capital-G `Gundabad` examples are fixed in the current tree: `taom_spcultures.xml:4516/4538/4712/4875/4897/5071` and `taom_module_strings.xml:2193-2263` now use Goblin/Misty Mountain wording. Remaining complete display leftovers:
   - `Main/_Module/ModuleData/characters/npcs_goblin.xml:320,387,451,501,532,600,668` — notable names still say `pale orc`.
   - `Main/_Module/ModuleData/characters/npcs_mistymountainorcs.xml:320,387,451,501,532,600,668` — same.
   - I found no new-faction `Gundabad` display leftovers in `taom_wanderers.xml`, new clans/lords/heroes, new equipmentsets, or factionmap. Preserved `Item.wm_gundabad_*`, `BodyProperty.fighter_gundabad`, and `SkillSet.spc_wanderer_gundabad` are intentional.

2. **DISPUTED** — Diplomacy is correct. `DiplomacyService.cs:45-46` defaults unlisted pairs to `AllianceTier.Neutral`, and `MakeKey` is order-independent at `DiplomacyService.cs:160-162`. `diplomacy.json:79-133` has valid IDs, no duplicate/conflicting pairs, the orc/east/Free Peoples matrix matches the design, and Lindon has exactly one explicit pair at `diplomacy.json:133`.

3. **DISPUTED** — Cultural feat wiring and dispatch are correct. The 8 new feats are present in fields/properties/register/init/yield paths (`TaomCulturalFeats.cs:78-86`, `224-232`, `372-380`, `644-676`, `960-967`), XML (`taom_spcultures.xml:4729-4732`, `5088-5091`), and dispatch (`CulturalFeatsService.cs:65-72`, `93-94`, `243-246`, `271-272`, `363-366`). `-0.4f` army cost produces `baseCost * 0.6`; food `+0.2/+0.15` increases consumption.

4. **DISPUTED** — Recruitment/troop refs are clean. Culture default troops and volunteer pools resolve; no dangling upgrade targets or new party-template troop refs were found.

5. **PARTIAL** — Feat-card magnitudes match, but faction-map text still advertises cavalry that was stripped. See finding 2. Lindon also has one misleading strength line. See finding 4.

6. **DISPUTED** — The cloned troop trees are structurally clean: no `Horse`/`HorseHarness` slots found in the new troop files, no upgrade targets to deleted cavalry, and party templates reference existing troops. The remaining issue is faction-map copy, not troop XML.

**CROSS-REFERENCE AUDIT**

New kingdoms resolve: `goblin` -> `Culture.goblin` (`taom_spkingdoms.xml:910-918`), `mistymountainorcs` -> `Culture.mistymountainorcs` (`1014-1022`), `bluecraig` -> `Culture.goblin` (`1118-1126`), `lindon` -> `Culture.rivendell` (`1222-1229`). Owners resolve to existing heroes.

New clans resolve to valid cultures/kingdoms: examples at `clans.xml:1235-1407`. Live data has 17 new clans and 130 lord/hero pairs; no duplicate IDs in cultures, kingdoms, clans, lords, or heroes.

Faction-map cards exist for all four playable regions: `high_kingdom_of_lindon`, `goblins_of_blue_craig`, `goblins_of_goblin_town`, `kingdom_of_moria`. `game_faction` is a culture id, so Blue Craig using `"goblin"` is correct.

**C# CORRECTNESS**

No C# logic findings. The feat sign conventions, snow dispatch, food penalty dispatch, army-cost math, and volunteer pools are wired correctly. The tests were updated for 18 culture IDs, 22 kingdom IDs, 105 feats, faction-map cards, CC culture coverage, and new recruitment pools.

**GENERATOR CORRECTNESS**

`insert_new_factions.py` now has the capital-G display substitution and bespoke Gundabad phrase rewrites (`tools/insert_new_factions.py:119-121`). `generate_new_factions.py` handles `Gundabad` (`:71`) but still misses lowercase `pale orc`, which is why the notable names remain stale.

Durable fix: centralize clone display substitutions and add a post-generation assertion over player-facing fields (`name`, `text`, JSON strings) forbidding source-culture phrases like `Gundabad`, `Pale Uruk`, `Pale Orc`, and `pale orc` in new-faction blocks, while explicitly allowing protected technical IDs.

**FINDINGS**

1. `[MEDIUM] Main/_Module/ModuleData/characters/npcs_goblin.xml:320 — Clone Display Text — Seven goblin notable templates still use "pale orc" despite race="goblin"; same issue in npcs_mistymountainorcs.xml:320,387,451,501,532,600,668 — settlement notables surface these names in-game — Replace with goblin/orc-specific wording and teach generate_new_factions.py to rewrite "pale orc"/"Pale Orc".`

2. `[MEDIUM] Main/_Module/ModuleData/factionmap/factions.json:262 — Faction-Map Sync — Goblin Town and Moria cards advertise Warg-riders/cavalry at lines 262,285-286,455-456, but cavalry was stripped and _orc_dropped_cavalry.json:3-14 lists the removed cavalry troops — CC page promises units the troop tree cannot provide — Replace those special units/traits with surviving infantry/archer units or restore cavalry intentionally.`

3. `[LOW] tools/taom_new_factions_layout.json:106 — Source-Of-Truth Drift — layout lists only clan_bluecraig_1 and clan_bluecraig_2, while live data has clan_bluecraig_1..5 plus 40 Blue Craig lord/hero rows — future layout-driven regeneration cannot reason about clans 3-5 — Add clan_bluecraig_3..5 to the layout or mark the clan section non-authoritative and validate against generated data.`

4. `[LOW] Main/_Module/ModuleData/factionmap/factions.json:72 — Faction-Map Sync — Lindon strength says "unifies armies cheaply", but the same card says +25% army influence cost at line 38 and the C# feat is +0.25 cost at TaomCulturalFeats.cs:480-483 — player-facing summary contradicts shipped mechanics — Reword the strength to describe the +35% influence award, not cheap army recruitment.`

CRITICAL: 0 | HIGH: 0 | MEDIUM: 2 | LOW: 2  
VERDICT: ISSUES FOUND
