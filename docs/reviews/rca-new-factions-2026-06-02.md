# RCA — New Factions (Misty Mountains / Goblins / Blue Craig / Lindon) deep-review, 2026-06-02

Deep review (`/deep-review`, 7 parallel agents + adversarial HIGH verification) of the new-factions
work: 2 new orc cultures (goblin, mistymountainorcs) + 4 kingdoms (goblin/Goblin Town,
mistymountainorcs/Misty Mountains, bluecraig/Blue Craig, lindon/Lindon), cultural feats, forever-alliance
diplomacy, ~50 settlements in the external `TAOM_Map` module, recruitment, faction map, ~130 lords/heroes,
generators, tests. **3 HIGH (all adversarially confirmed, refuted=false) + 2 MEDIUM + 3 LOW.** All fixed
in-session. Build ✓, validate_moduledata ✓, 2914 tests pass.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `village_type="VillageType.cattle_range"` on 7 new villages — `cattle_range` is not a registered v1.4.5 VillageType (it's `cattle_farm`). Null VillageType → NRE in `Village.UpdateTotalProduction`. | Invalid engine-entity id from plausible naming | I picked `cattle_range` by plausible name; never verified against `DefaultVillageTypes`. `validate_moduledata.py` does NOT validate `village_type` ids AND does not read the external `TAOM_Map` settlements at all. | **Added `VALID_VILLAGE_TYPES` guard** in `tools/assign_orc_village_types.py` (`fief_types` raises on an unregistered id). Memory: verify VillageType stringIds against the engine, never plausible naming. |
| 2 | HIGH | `TAOM_Map/SubModule.xml` missing `<DependedModule Id="TAOM"/>` — settlements reference `Culture.goblin/mistymountainorcs/rivendell` + `Faction.clan_*` defined in TAOM Main; load order unsafe. | Cross-module data dependency not declared | Authored external-module data referencing TAOM-Main entities but didn't update TAOM_Map's module manifest. **REPEAT** of the rule established in `rca-bandit-management-2026-05-27` / `feedback_cross_module_data_dependency_declaration`. | **Added `<DependedModule Id="TAOM"/>` + `<DependedModuleMetadata id="TAOM" order="LoadBeforeThis"/>`.** Repeat-offender: run the cross-module dependency check during AUTHORING (not just review) whenever editing `TAOM_Map/ModuleData/*` that references Main entities. |
| 3 | HIGH | `town_GBC1` (Blue Craig) had zero bound villages → no village food/hearth/volunteer income; its `Endless Spawn` (+25% volunteer respawn) feat was effectively dead. | Kingdom with no village economy | Blue Craig was created with only its capital town (user said they'd add castles/villages later); a 0-village kingdom is economically inert. | **Added 4 placeholder villages** (`village_GBC1_1..4`, bound `town_GBC1`, goblin village-type rule) so the kingdom has an economy now; user places/relocates scene entities later (same pattern as the Lindon villages). |
| 4 | MED | Goblin kingdom lord region code is `GB` in the generator but `lord_GT*` in `taom_new_factions_layout.json`; layout also documented only 2 goblin clans vs 5 generated. | Documentation/source drift | Changed the lord region code without syncing the layout JSON (which is documentation-only; no script reads its `ruler_hero`/`clans`). | Synced layout: goblin `ruler_hero`/clan `leader_hero` → `lord_GB*`, added `clan_goblin_3..5`. |
| 5 | MED | `make_new_factions_playable.py` wrote `factions.json` unconditionally (no `--apply` dry-run guard) unlike the sibling settlement scripts. | Tooling-safety inconsistency | Written as a one-shot; didn't follow the `--apply`/dry-run pattern. | Added `--apply` guard (dry-run default). Repo-tracked file → git history is the backup (no `.bak`, unlike the external settlements). |
| 6 | LOW | Stale test comment "full 92-feat enumeration" (assertion correctly uses 105). | Stale comment | Comment not updated when feats grew 97→105. | Updated to "105-feat". |
| 7 | LOW | `RohanLoyaltyFeat` registered but not dispatched in `CulturalFeatsService.ApplyLoyaltyFeats`. | Pre-existing (out of scope) | Not part of this changeset — Rohan feats predate the new-factions work. | No change (out of scope; flagged for the user to confirm intent). |
| 8 | LOW | `village_GT1_4` absent (GT1 villages are 1,2,3,5,6,7). | Naming-sequence gap | Reflects the user's actual scene placement (they placed 6 GT villages skipping `_4`). | No change — matches the placed scene entities; adding a `_4` would create a village with no scene entity. |

## Root-cause pattern — "authoring for the engine/other-module without cross-checking the authority"

Findings 1 and 2 share one root: **data authored for the external `TAOM_Map` module was not cross-checked
against its authority** — the engine's code-registered VillageType set (finding 1) and the module
dependency system (finding 2). Both are invisible to `validate_moduledata.py`, which (a) reads only the
*repo* ModuleData, not the external `TAOM_Map`, and (b) has no VillageType-validity or DependedModule
check. So the one automated gate that would normally catch ref bugs was structurally blind to both. The
`/deep-review` Agent 2 (API, decompiled the VillageType registry) and Agent 5 (data-flow, has the
cross-module rule) caught them — which is exactly why the review exists.

Finding 2 is a **repeat offender** (same rule from the Bandit Management RCA, 2026-05-27). The rule was
documented and the review agent had it — but it fired at REVIEW time, not AUTHORING time. The durable fix
is to apply the cross-module-dependency check while editing `TAOM_Map/ModuleData/*`, not only in review.

## Why each deep-review agent's result was what it was

- **Agent 2 (API compat)** — CAUGHT finding 1 by decompiling `DefaultVillageTypes` from the installed
  v1.4.5 DLL. This is the agent working as designed (verify engine ids against installed DLLs).
- **Agent 5 (data flow)** — CAUGHT findings 2 and 3 via the cross-module-dependency rule (rule 10) and the
  bound-village trace. Highest-value agent again.
- **Agent 6 (tooling)** — CAUGHT findings 4 and 5; correctly verified BOM/CRLF preservation, idempotency,
  and the protect-list — all sound.
- **Agents 1, 3, 7 (standards, efficiency, lore/balance)** — PASS; correctly scoped (the C# is clean, hot
  paths are O(1), the user-intent checks all verified incl. Círdan-not-Gil-galad, orc armor, no cavalry,
  party-size/food feats). They did not (and should not) catch findings 1-3, which are data/manifest bugs.
- `validate_moduledata.py` — did NOT catch finding 1 (no VillageType validation; doesn't read external
  settlements). Gap noted; the new `VALID_VILLAGE_TYPES` guard closes it at the tooling layer.

## Feedback memories to codify

1. **VillageType stringIds are code-registered (DefaultVillageTypes), not XML — verify against the engine,
   never a plausible name.** `cattle` is `cattle_farm`, not `cattle_range`. An unregistered id → null
   VillageType → NRE in `Village.UpdateTotalProduction`. (New; sibling of the wm_/item-id verification lessons.)
2. **Cross-module data dependency (REPEAT).** Already codified as
   `feedback_cross_module_data_dependency_declaration`. Reinforce: apply at AUTHORING time when editing any
   `TAOM_Map/ModuleData/*` that references TAOM-Main `Culture.`/`Faction.`/`PartyTemplate.` ids — add the
   `<DependedModule Id="TAOM"/>` in the same edit.

---

## Phase 2/3 — Codex adversarial review + completeness-audit workflow (2026-06-02)

After the deep-review fixes above, ran `/review-codex` (Codex gpt-5.5, xhigh) then a 5-agent adversarial
completeness-audit workflow. Codex verdict: **0 CRITICAL / 0 HIGH / 2 MED / 2 LOW**; it independently
confirmed the proactive clone-leftover fix (below) had landed and **DISPUTED (cleared)** the diplomacy,
cultural-feat-wiring, recruitment, and troop-structure suspects. The completeness workflow then found one
MED both prior reviews missed.

### Findings (all confirmed against source + fixed at the generator source AND live files)

| # | Sev | Source | Bug | Category | Why missed | Preventive action |
|---|-----|--------|-----|----------|------------|-------------------|
| P0 | (proactive) | Claude (pre-dispatch) | Both clone scripts only remapped the bracketed `[Gundabad]` tag + "Pale Uruk" — capital-G free-text "Gundabad" and "pale orc" survived in 2 culture names, 2 descriptions, 2 clan-pool names, ~24 loc strings, 36 notable names → player-facing strings on goblin/mmo factions said "Gundabad". | Clone display-text not remapped | The id-rename is a case-sensitive lowercase `replace("gundabad", culture)`; it never touched capital/display strings, and the deep-review checked structure/refs not free-text wording. | Bespoke per-culture display subs + bare-word catch-all in both generators; **post-generation assertion** in `generate_new_factions.py` that RAISES on any surviving `Gundabad`/`Pale Uruk`/`Pale Orc`/`pale orc`. |
| C1 | MED | Codex | Goblin Town + Moria faction-map cards advertised Warg-riders / wolf-cavalry, but cavalry was stripped (infantry+archer only). | User-facing promise ≠ shipped content | Faction-map card flavor authored before the cavalry-strip decision; deep-review/Codex checked troop XML not card↔roster consistency. | Reworked cards to surviving units in `make_new_factions_playable.py`; regenerated factions.json + harvested strings. (Sibling of `feedback_user_facing_promise_must_match_code`.) |
| C2 | MED | Codex | 14×2 notable names still said "pale orc" (the clone sibling the "Pale Uruk"→raceword rule missed). | Clone display-text not remapped | The proactive fix mapped "Pale Uruk"/"Gundabad" but not lowercase "pale orc". | Added "pale orc"/"Pale Orc" remap; the post-gen assertion now also covers these. |
| **W1** | **MED** | **Completeness workflow** | **`execution/alignment.json` had no entry for the 4 new kingdoms** → `AlignmentService.GetKingdomSide` returns `Neutral` → `AreEnemyAlignments`=always-true + `AreSameAlignment`=always-false → `TaomExecutionRelationModel` execution-relation penalties mis-scored AND the `DiplomacyService.IsWarAllowed` same-alignment war-block backstop silently disabled for goblin/mistymountainorcs/bluecraig/lindon. | Kingdom-enumerating config not updated for new factions | Authored kingdoms/cultures/diplomacy/faction-map/recruitment but missed `alignment.json` — a SEPARATE kingdom-enumerating config that neither the deep-review nor Codex enumerated. | Added orcs=`evil`, lindon=`free`. **Generalize the existing `feedback_faction_map_update_with_cultural_feats` rule: when adding a faction, update EVERY kingdom/culture-enumerating config — alignment.json included.** |
| C3 | LOW | Codex | Lindon strength_1 "unifies armies cheaply" contradicted its own +25% army-influence-cost penalty (Elven Pride); the +35% is an influence *award*. | Player-facing copy contradicts mechanics | Card strength text authored loosely vs the two distinct rivendell army feats (award vs cost). | Reworded to "Wise leadership reaps greater influence from victory" in both factions.json + module_strings. |
| C4 | LOW | Codex | `taom_new_factions_layout.json` listed only `clan_bluecraig_1/_2` vs live `_1..5`. | Doc/source drift | Deep-review synced the goblin clans but the same fix wasn't applied to bluecraig. | Added `clan_bluecraig_3..5`. |
| W2 | LOW | Completeness workflow | 6 goblin-culture notables read "orc" not "goblin" (inconsistent with siblings; lore-defensible but reads as oversight). | Clone display-text (wrong race word) | Plain "orc" (not "pale orc") in the gundabad source notables; not a forbidden phrase, just wrong for the goblin culture. | Culture-aware " orc "→raceword remap (no-op for the orc culture). |

### Root-cause patterns

- **Clone-leftover DISPLAY text is a class, not an instance.** Cloning a faction copies every player-facing
  string; the id-rename only touches lowercase technical ids. P0/C2/W2 are all the same root. The durable
  fix is a generator-level remap of source race/faction words + a fail-the-build assertion, not whack-a-mole
  on individual strings. Codified as a new memory.
- **"Add a faction" means update EVERY kingdom/culture-enumerating config.** W1 (`alignment.json`) is the
  same class as the already-known faction-map rule — the set is larger than the faction map:
  `taom_spkingdoms.xml`, `taom_spcultures.xml`, `clans.xml`, `lords.xml`, `heroes.xml`, `diplomacy.json`,
  `factionmap/factions.json`, `charactercreation/cultures.json`, `execution/alignment.json`,
  recruitment pools, and the C# feat wiring. Neither the deep-review nor Codex enumerated `alignment.json`;
  the completeness workflow's cross-ref-graph agent did. **The highest-value catch of the whole review chain
  came from the independent completeness pass, not from the targeted reviewers.**
- **The reviewers cleared the structural risks.** Codex + the workflow both independently verified the
  diplomacy graph (130 relationships, 0 invalid/dup/contradictory), the feat wiring (8 feats × 5 locations),
  the recruitment pools, and the faction-map↔feat magnitude consistency — high confidence those are correct.
