# CHANGELOG — TAOM (Tales From the Age of Men)

## 2026-06-01

### fix(special-resources): warn only when the next tick goes into deficit

The low-resource warning previously fired on a static "balance below 10% of cap" threshold, nagging the player whenever a balance was low in absolute terms — even when town income comfortably covered upkeep. It now fires **only when the player is heading into a deficit**: the warning projects the next daily tick (steady-state — same towns/party as this tick) and alerts only if the resulting balance would fall to ≤ 0, which is exactly the threshold that triggers troop desertion. The warning thus becomes a precise "your elite troops will desert tomorrow" heads-up; a low-but-stable balance is silent.

- `SpecialResourceService.cs` — extracted the daily earning(+`CustomResourceGain`) − upkeep(+`CustomResourceUpkeepModifier`) math into a private `ComputeDailyNet` (single source of truth); `ApplyDailyTick` now delegates to it. Added public `GetProjectedDailyNet(heroId, kingdomId, cultureId, ownedTownCount, troops)` → resolves the resource (0 if none), returns `ComputeDailyNet`. The shared helper guarantees the projection can never drift from the real tick math.
- `SpecialResourcesBehavior.cs` (`OnDailyTickHero`) — replaced the `balance < Cap * 0.1f` branch with `balance + GetProjectedDailyNet(...) <= 0f`. Message changed from "running low: N/Cap" to "running out: N left, losing M/day".
- `ISpecialResourceService.cs` — declared `GetProjectedDailyNet`.
- 4 new service tests (`GetProjectedDailyNet_*`: positive net, deficit, no-resource, career-modifier parity). The prior static-threshold warning had zero test coverage; the projection math is now fully covered.

Reviewed via two adversarial multi-agent passes (logic, data-flow, tests, standards lenses + independent per-finding verification): 4 findings raised, 0 confirmed.

Verification: `dotnet test --filter ~SpecialResource` → 98/0; `./build.ps1 -RunTests` → build succeeded, 2894/0 (2 pre-existing skips).

Save-compat: safe — additive interface method, no serialized-state change. Not-tested: in-game info-bar render (entry-point behavior, ADR-008-exempt). Follow-up: the warning string remains non-localized (parity with the prior warning; whole SpecialResources message surface is localization-debt, deferred).

### feat(faction-map): Phase 3 — 11 AI-language translation propagation + U+2212 minus-glyph fix (#260)

Phase 3 of the CC faction-map rewrite (issue #260). Ran `tools/translate_with_claude.py` against all 11 AI-translated languages (BR, CNs, CNt, DE, FR, IT, JP, KO, RU, SP, TR; Polish hand-translated per convention) for the 617 new `taom_faction_*` keys from Phase 1+2 + Phase 2 fix. Each language file at `Main/_Module/ModuleData/Languages/<LANG>/std_taom_module_strings_<lang-code>.xml` updated via `rebuild_translation_files.py` then translated via Claude Sonnet 4.5 batched API.

**Coverage:** Out of ~13,000 total entries across 11 languages, ~99.9% successfully translated. ~40 pre-existing `taom_career_rank_*` entries (CareerSystem, not faction-map) consistently fail across languages due to multi-variable string complexity — unrelated to this work. Within Phase 2's 617 new faction-map keys, all 11 languages have 100% coverage after two batch-failure retries (CNt + SP recovered Dol Guldur content; TR recovered Gondor/Gundabad/Harad bonuses).

**In-game bug fix — U+2212 mathematical minus.** In-game verification revealed that the Unicode mathematical minus (U+2212, `−`) used in 50 places like "−20% garrison wage" rendered as an underscore-like low glyph in Bannerlord's font. Replaced ALL 50 occurrences with ASCII hyphen-minus (U+002D, `-`) across:

- `Main/_Module/ModuleData/factionmap/factions.json` (50 → 0)
- `Main/_Module/ModuleData/taom_module_strings.xml` (50 → 0)
- 11 × `Languages/<LANG>/std_taom_module_strings_*.xml` (555 → 0 across all)
- 11 × `tools/translation_cache/*.json` (354 → 0)

Codex review #260 had flagged "37 U+2212 minus signs each" but marked it benign — the in-game render disproved that. Lesson: typographic Unicode characters that "look the same" in a code editor can render very differently in the target font. ASCII is safer for player-facing text.

**Verification:**
- `python tools/validate_moduledata.py` → PASS (5,648 items / 4,178 NPCs / 36 cultures / 262 party templates).
- All 12 `Languages/<LANG>/std_taom_module_strings_*.xml` files parse as well-formed XML.
- `dotnet test TAOM.Tests --filter "FactionMap|LanguageDataXml"` → 109 / 0 / 0.
- Zero U+2212 occurrences anywhere in the localization chain (source + harvest + translations + cache).

**Cost:** ~$16 total API spend across the 11-language initial sweep + 2 retry passes for CNt/SP/TR.

Save-compat: safe (additive only). Not-tested: in-game verification of 1 non-English language (recommend French or Russian for the next CC smoke test).

### fix(character-creation): culture-appropriate family name + auto-filled character name in CC (#264)

The faction-map character-creation flow generated the player **family/clan name from the stale default culture** — a Gondorian player got the Battanian default "fen Leduin" instead of a Gondorian house. Root cause: `FactionMap.CultureSettingService.SetCultureOnCharacterCreation` invoked vanilla `CharacterCreationContent.SetSelectedCulture` — which generates the clan name via `FactionHelper.GenerateClanNameforPlayer()` reading `CharacterObject.PlayerCharacter.Culture` (== `Hero.MainHero.Culture`) — **before** `ExecuteSelectCulture()` assigned the chosen culture to the hero. Pure-vanilla CC does the reverse (assign culture on click in `OnCultureSelection`, generate name on Next in `SetSelectedCulture`); the faction-map rewrite inverted the order, so the name was always generated from the default culture's `<clan_names>`. Gondor's own `<clan_names>` were correctly defined in `taom_spcultures.xml` — just never consulted.

- **Fix:** `CultureSettingService` now assigns `Hero.MainHero.Culture = culture` *before* the `SetSelectedCulture` invoke (mirrors vanilla `CharacterCreationCultureStageVM.InitializePlayersFaceKeyAccordingToCultureSelection`, verified `CharacterObject.Culture` defers to `HeroObject.Culture`). The family name now draws from the selected culture's `<clan_names>` (House of Húrin, House of Dol Amroth, …) and stays editable on the clan-name screen.
- **Enhancement — `Patch44_CCNameAutofill`:** the Review-stage "Enter your name" field (empty in vanilla until the player types or rolls the dice) is now pre-filled with a culture-appropriate first name. Postfix on `CharacterCreationReviewStageVM`'s 6-arg constructor calls the VM's own public `ExecuteRandomizeName()` only when `Name` is blank — never clobbers a typed name, field stays editable. Done at the Review stage so the generated name matches the finalized gender.
- **Files:** `Main/Features/FactionMap/CultureSettingService.cs`; new `Main/Features/CharacterCreation/Hooks/CharacterCreationReviewStageVM_AutoFillName_Patch.cs`; `Main/SubModule.cs` (register `Patch44_CCNameAutofill`).
- **Verified:** compile clean (0 warnings/0 errors); `TAOM.Tests` 2877 passed / 0 failed / 2 skipped. In-game CC verification still pending (the game was holding the module DLLs locked during this session, blocking the game-folder deploy).
- **Rohan (`vlandia` id) special-case:** vanilla `FactionHelper.GenerateClanNameforPlayer` hardcodes `"{=Uk3qRuCS}dey Corvand"` for the `vlandia` culture id, which TAOM repurposes as Rohan — so the ordering fix above alone would still leave Rohan players with "dey Corvand". `CultureSettingService` now overrides that placeholder immediately after `SetSelectedCulture`, regenerating from Rohan's own `<clan_names>` (Harolding, Earfening, …); guarded on a non-empty `ClanNameList` so it falls back to the vanilla value rather than throwing. `vlandia` is the only culture vanilla hardcodes — the other reused vanilla ids (empire/aserai/battania/sturgia/khuzait → Dunland/Harad/Khand/…) already generate correctly via the ordering fix.

Research: `CharacterCreationContent.SetSelectedCulture`, `FactionHelper.GenerateClanNameforPlayer`, `CharacterCreationCultureStageVM.OnCultureSelection`, `CharacterCreationReviewStageVM`, `CharacterObject.Culture`
Save-compat: None — character-creation-only, no persisted state.

### feat(battle-scenes): re-enable Patch0_BattleScenes (loads TAOM's full 0–255 sp_battle_scenes table)

Uncommented `_harmony.PatchCategory("Patch0_BattleScenes");` ([Main/SubModule.cs:159](Main/SubModule.cs)) — the feature was parked since the v1.3→1.4 migration ("custom map not yet ready"). That reason is resolved: `TAOM_Map` ships a `Main_map` scene and `Main/_Module/ModuleData/sp_battle_scenes.xml` covers all 0–255 `map_indices` (81 Scene ids, **0 crash suspects**, every id resolves to an on-disk `SceneObj/` folder). Effect: the `TAOM_Map` worldmap-battle-scene-grid's **extended indices (158–255) now resolve to real battle terrains** (`battle_terrain_r` catch-all) instead of `Debug.FailedAssert`-ing against vanilla's 1–157 table.

- **1.4.5 compatibility verified before flipping** — all 3 Patch0 targets bind against the installed 1.4.5 DLLs: `Campaign.InitializeScenes` (private, by-name), `GameSceneDataManager.{LoadSPBattleScenes,LoadConversationScenes,LoadMeetingScenes}(string)`, `MBMapScene.GetBattleSceneIndexMap(Scene, ref byte[], ref int, ref int)`, `SandBox.MapScene.Load`. Repo `sp_battle_scenes.xml` == deployed copy.
- **Verification:** `dotnet build Main/TAOM.csproj` clean (0 errors); `dotnet test TAOM.Tests` → 2877 passed / 0 failed / 2 skipped; `python tools/audit_battle_scenes.py` → 0 crash suspects, full 0–255 coverage. Feature doc flipped DISABLED→ENABLED ([docs/features/battle-scenes.md](docs/features/battle-scenes.md)).
- **Not-tested:** in-game scene loading (needs the live game + the re-authored grid) — Patch0's diagnostic patch logs the selected map module + the retry guard wraps `GetBattleSceneIndexMap`. The per-index→terrain *geographic* refinement (so each Middle-earth region spawns lore-appropriate terrain) is deferred until the grid is re-authored, since it depends on the final index→region assignment.
- Save-compat: none — scene-table loading only, no persisted state.

### fix(taom_map)+docs(battle-scenes): root-caused a campaign-load crash from a loose battle-scene-grid import

A campaign-load crash (`AccessViolationException` in native `get_battle_scene_index_map` → `MapScene.Load` → `Campaign.LoadMapScene`; game booted to menu fine, crashed only on map load) was **root-caused to a mis-imported `worldmap_battle_scene_grid` texture** in the external `TAOM_Map` module (creating `Assets/Battle Map/…_tex.tpac` + a `RuntimeDataCache/<guid>.rdc`). **Confirmed by reversible delete-test:** removing the import artifacts restored loading on the unchanged 05-28 scene (which had loaded fine before the import). The grid's index data lives in the baked `terrain.bin` (verified: the texture name is absent from `scene.xscene`/`terrain.bin`/`terrain_ed.bin`), which is the working baseline. See the CORRECTION below for the right authoring method (the import itself is correct; it must be lossless).

- **No code change** — the fix is deleting the bad import artifacts (done). `Patch0_BattleScenes` was disabled throughout (no TAOM frames in the stack); its AV retry guard would not have helped (the AV is deterministic, not the transient cold-cache case it was built for).
- **CORRECTION (same day, via the user supplying the official docs):** [BannerlordModding.LT › Battle Scene Grid](https://docs.bannerlordmodding.lt/editor/battle_scene_grid/) (a 1.2.12 source) shows you **do** import the grid into the mod's `Assets` folder — so the initial "never import; must bake into `Main_map`" framing was wrong (over-inferred from the crash). The real defect was a **mis-import**: the index map must be **LOSSLESS** (Texture Inspector → **Do Not Compress** + **Dont Degrade**; DXT mangles the exact R-channel index bytes) and/or a stale `.rdc`. **RESOLVED later same day:** re-importing the grid **lossless at the `Assets/world_map/` resource path** (4.19 MB uncompressed `.rdc` vs the crashed import's ~700 KB compressed one) makes a campaign load with `Main_map` **unchanged** — so 1.4.5 reads the grid as a runtime `Assets/world_map/` texture, **no re-bake needed** (the "baked into Main_map" model was also wrong). The two real fixes were the `world_map/` resource path + lossless settings.
- **Docs/prevention:** RCA [rca-worldmap-grid-loose-import-crash-2026-06-01.md](docs/reviews/rca-worldmap-grid-loose-import-crash-2026-06-01.md), [worldmap-battle-scene-grid.md](docs/reference/worldmap-battle-scene-grid.md), and memory `feedback_battle_scene_grid_baked_not_runtime_swap` — all corrected to the above.
- **Method:** decisive root cause from the live revert experiment; mechanism depth from a 5-agent research workflow whose load-bearing local claims were spot-verified (`Scene.cs:1069` passthrough, the no-texture-name `GetBattleSceneIndexMap` API from the user's pasted 1.4.5 source, the `.zip` artifact, vanilla's no-loose-grid layout).

### feat(career-quests): career-tied quest framework (TOR adoption, 1.4.5-verified) + Gondor proof-of-life

New **Career Quest System** — completing a career's tier quest unlocks that tier of the choice tree (**hybrid** with the level gate) and grants a unique reward. Data-driven adaptation of TheOldRealms' career-quest pattern (GPLv3, with permission), rebuilt on TAOM's adapter/service architecture and **verified against Bannerlord 1.4.5** (TOR is 1.3.15). Phase 1 = framework + one Gondor proof-of-life quest; other careers/tiers fall through to the level gate unchanged. Doc: [career-quest-system.md](docs/features/career-quest-system.md).

- **Hybrid tier gate** — `CareerScreenVM` consults `ICareerQuestService.IsTierUnlocked(level, tier, heroId)` = registry level gate **OR** `ICareerDataService.IsTierUnlocked` (a completed quest's `UnlockTier` reward, persisted in `_taom_careerTiers`). The hybrid lives in the quest **service, not the registry** (injecting it into the registry would be circular) — the registry level gate is untouched; the VM takes the service as an *optional* ctor param (null → registry-only, so existing tests are unchanged).
- **Pure logic (100% unit-tested)** — `CareerQuestService` (quest lookup, gate, per-objective-type progress [count accumulates / threshold banks-highest], completion, reward application) + `CareerQuestConfigProvider` (validating XML loader — skip-and-warn per the config-validation rule). 8 objective types, 5 reward types (trimmed to implemented-only per the no-aspirational-enum rule). Career attribute flags added to `HeroCareerData` (+ a 4th persistence dict).
- **Engine layer (verified 1.4.5)** — `CareerQuest : QuestBase` shell (saveable progress + `List<JournalLog>`; count objectives via `OnPlayerBattleEndEvent`/`OnSettlementOwnerChangedEvent`/`TournamentFinished`/`HeroPrisonerTaken`/`SettlementEntered`; threshold objectives polled in `DailyTick`), `CareerQuestSaveableTypeDefiner` (base id 726900701, class localId 101 → global save-id 726900802), `CareerQuestCampaignBehavior` (offers the next eligible tier quest via inquiry; declined-set persisted), `QuestHeroAdapter` (renown/influence/item sinks + skill/gold/renown reads).
- **API verification first** — a 4-cluster decompile pass against the installed 1.4.5 DLLs caught real drift before it could break the build: `InquiryData`/`InformationManager` moved `TaleWorlds.Core` → `TaleWorlds.Library`; `SettlementEntered` (not `OnSettlementEntered`); `TournamentFinished` winner is a `CharacterObject`; `QuestBase`'s own `DailyTick` for polling (not `CampaignEvents`); `CampaignTime.DaysFromNow` (due time is absolute) not `Years`. The engine layer compiled clean on the first build.

**Reviewed:** `/deep-review` (5 agents) found 1 HIGH (`CareerQuest` missing `SpecialQuestType` → `QuestManager.OnGameLoaded` would silently cancel every career quest on save-load) + MED/LOW (silent unresolvable skill-id; aspirational `giver_kind` removed) — all fixed in-session. `/review-codex` (gpt-5.5 xhigh) ran two passes: it **independently confirmed** the save-load fix + the `List<JournalLog>` save-graph assumption + the 4th-persistence-dict correctness via decompile, found 5 more (all fixed: `VisitSettlementType`/`UnlockTier` config validation tightened; `GrantItem` unresolved-id warning; `_offerPending` try/catch; the lord-defeat objective now requires the target be at war), then a self-review pass over the fixes flagged a `HeroKilledEvent`-timing false-negative — **superseded** by the rename below. RCA: [rca-career-quest-system-2026-06-01.md](docs/reviews/rca-career-quest-system-2026-06-01.md). Lessons codified — memories `feedback_questbase_subclass_special_or_issue` + `feedback_saveable_typedefiner_localid_offset`.

**Post-review corrections (in-game):**
- **`KillEnemyLords` → `DefeatEnemyLords`** — game-semantics fix: in Bannerlord *killing* a lord means *executing* them (honor/relation penalty), so the objective now tracks **defeating + capturing** an at-war lord via `HeroPrisonerTaken` (verified 1.4.5 `IMbEvent<PartyBase, Hero>`), not `HeroKilledEvent`. Also sidesteps the self-review's elimination-timing false-negative (capture fires before any faction-destroy path).
- **Save-id collision crash fixed** — first launch crashed at `Module.Initialize` (`SaveableTypeDefiner` duplicate key): `CareerQuestSaveableTypeDefiner` used class localId `1` → global id `726900701 + 1 = 726900702`, colliding with `FormationPresetSaveableTypeDefiner` (`726900601 + 101`). The engine id is `base + localId`; TAOM definer bases step by 100 so localId must start at 101. Fixed → `726900802`.

Verified: build 0 errors (deploy OK with game closed); `dotnet test TAOM.Tests` → **2877 passed / 0 failed / 2 skipped** (no regressions).
`Not-tested: in-game offer/track/complete + save-load of the QuestBase shell (entry points — live-game only). MCM toggle + remaining-career quests + 11-language translation are Phase 2.`
`Research: TheOldRealms/TOR_Core Quests/Careers + CharacterDevelopment/CareerSystem (1.3.15); TaleWorlds 1.4.5 QuestBase/CampaignEvents/GainRenownAction/ChangeClanInfluenceAction/InquiryData/SaveableTypeDefiner (decompile-verified).`
`Save-compat: new — _taom_careerFlags + _taom_cq_declined dicts default empty; CareerQuest registered via SaveableTypeDefiner 726900701; existing saves unaffected (careers without a quest = level gate).`

### feat(battle-load-diagnostics): phase-stamped battle-load lifecycle log + stall watchdog

New `BattleLoadDiagnostics` feature (`Patch43`) to localize the intermittent infinite-battle-load hang (no crash, no stack trace, on user machines, not reproducible locally). Phase-stamps the full attack→battle-playable lifecycle to `Logs/taom_debug_*.log` across 6 markers (`EncounterStart → MissionOpenNew → BattleSceneSelected → MissionInitialize → AgentEquip{Begin,Ok} → BattlePlayable`). The last line before a freeze names the stuck phase; the per-agent equip dump — captured (and flushed) BEFORE the engine equips, incl. `bo=`/`shieldBo=` collision-mesh names — names the suspect item when an `AgentEquipBegin` has no matching `AgentEquipOk`. Background-thread stall watchdog (runs off the frozen main thread) writes a `STILL LOADING` marker after a 45s default and auto-triggers the CrashReport bundle so users return it in one action. ADR-007 `IEquipmentSnapshotAdapter`, thin exception-swallowing hooks, dedicated MCM page (default ON). 26 new tests; full suite 2872 pass / 0 fail. Doc: `docs/features/battle-load-diagnostics.md`.

Research: PlayerEncounter.Start, MissionState.OpenNew, DefaultSceneModel.GetBattleSceneForMapPatch, Mission.Initialize, Agent.EquipItemsFromSpawnEquipment, ItemObject.{BodyName,CollisionBodyName} (all `taom-src` v1.4.5)
Not-tested: the 6 Harmony hooks + BattleLoadPhaseBehavior (game-only); DryIoc graph resolves at runtime (no container test in suite)

### feat(tools): validate_mesh_refs.py — mesh / collision-body existence validator

Pure-stdlib tool to confirm or eliminate the missing-`bo_`-collision-mesh hypothesis behind the hang. Three tiers: **A** (authoritative `rgl_log` cross-ref — `get_object failed for body`), **B** (offline `.tpac` TOC for visual `mesh=`), **C** (offline coarse byte-scan for `bo_` bodies, which aren't in the TOC). First real run (159 `.tpac`, 0 unparsed, 16,068 Metameshes) found **3 real Armory data bugs**, independently verified against source XML: `MISSING_BODY bo_cap_wm_boromir_shield` (`wm_boromir_shield` *pickup*-collision — the *equipped* `bo_wm_boromir_shield` IS present, so a weak spawn-hang suspect), `MISSING_MESH sk_dg_uruk_pauldron_med_c` + `ar_ardunian_elite_hand` (invisible-armor visual meshes, not hang suspects). Tier A on the captured session: 0 item-referenced runtime-missing bodies → the bo-mesh-at-spawn hypothesis is **not yet confirmed**; the Track-1 runtime log will settle the next live hang. 30 new tests (tools suite 93 pass). Doc: `docs/features/mesh-ref-validation.md`.

### fix(faction-map): Codex review reconcile XSLT inheritance + hover Localize bypass (#260 Phase 2)

Codex adversarial review on Phase 2 (`cbbcc41` + `7f0de78`) returned **0 CRITICAL / 3 HIGH / 3 MED / 1 LOW**. After verification (decompiled `DefaultCulturalFeats` v1.4.5 + traced `spcultures.xslt`), 7 fixes shipped, 2 rejected per `simplicity-criterion.md`.

**HIGH 1 — Dale fabricated inheritance.** The CC page claimed Dale inherits "Sturgian forest speed" and "Sturgian winter resilience" — vanilla Sturgia ships `Grain +10%`, `Army Influence Cost −50%`, `Decision Penalty +20% (neg)`. Fixed: replaced Dale's bonuses + perk text + 1 weakness.

**HIGH 2 — Dunland/Khand Battanian descriptions wrong.** Said "+15% party speed in forest" (vanilla: `50% less forest speed penalty + 15% sight range`) and "−15% construction" (vanilla: `−10%`). Fixed both factions.

**HIGH 3 — Harad/Rhûn omitted concrete inheritance + negatives.** Harad now lists `−30% caravan, −10% trade penalty (Aserai Trader)`, `no desert speed penalty (Aserai Desert)`, `+5% wages (Aserai Wages, neg)`. Rhûn now lists `−10% mounted recruit/upgrade`, `+25% animal village production`, `−20% town tax (neg)`. Removed incorrect "militia" mention from Rhûn.

**HIGH 4 — Hover tooltip Localize bypass.** `FactionDisplayHelper.ShowHoverTooltip` passed `change.FactionName` raw to `TooltipProperty` — would have displayed literal `{=taom_faction_...}Stewardship of Gondor` text on hover. One-line wrap: `Localize(change.FactionName)`. The trace (`PolygonWidget.HoveredFactionName` → `FactionHoverService.UpdateHover` → `HoverStateChange.FactionName` → `ShowHoverTooltip`) bypassed Phase 1's helper.

**MED 5 — 4 village notable feats absent from CC pages.** Mordor `Slave Drivers` +5%, Isengard `Iron Press` +10%, Gundabad `Bone Camps` +10%, Dol Guldur `Hidden Hovels` +10%. Added a positive bonus line to each faction.

**MED 6 — Erebor description math.** "Master Smiths halve forge costs" → "Master Smiths cut smithing energy by 30%" (matches feat `EffectBonus = -0.3f`).

**REJECTED MED 7** (41/48 special-units names not in troop XMLs): names are iconic LOTR archetypes (Citadel Guard, Black Uruk, Mumakil War Tower); renaming to exact troop display names is high authoring cost for low player benefit. Documented as known archetype framing; revisit specific entries during Phase 3 in-game smoke if any read as fabrications.

**REJECTED LOW** (`_su_` abbreviation): runtime key coverage clean (610/610 keys match); renaming for cosmetic consistency is scope creep.

8 of 9 Codex Known Suspects came back DISPUTED-with-evidence; the JSON↔XML alignment, XML escape safety, token double-prefix, and old-content removal all pass.

Verification: `tools/harvest_factionmap_strings.py` → 610 keys (was 599, +11 new bonus/weakness lines). `dotnet test` → **2872 / 0 / 2** (no regression). `tools/validate_moduledata.py` → PASS.

RCA: [`docs/reviews/rca-faction-map-phase2-codex-2026-06-01.md`](docs/reviews/rca-faction-map-phase2-codex-2026-06-01.md). Memory updated: extend `feedback_faction_map_update_with_cultural_feats` with the inherited-feat audit step (XSLT trace + vanilla decompile).

### fix(faction-map): localize FormatDifficultyText (#260 Phase 2 deep-review fix)

Deep-review (6 agents) on Phase 2 (commit `cbbcc41`) returned 1 HIGH (Tooling, FALSE POSITIVE), 2 MED (Tooling, REJECTED per `simplicity-criterion.md`), 1 GAP (Data Flow, CONFIRMED). 4 of 6 agents PASS-clean.

Real fix: `FactionSelectionService.FormatDifficultyText` returned 7 hard-coded English strings ("Difficulty: Hard", etc.) with no `{=KEY}` token — pre-existing Phase 1 holdover, in scope for the full localization sweep. Wrapped each branch in `{=taom_faction_difficulty_N}default` and added 7 hand-authored `<string>` entries to `taom_module_strings.xml` (above the auto-harvested block; these keys come from C# code, not factions.json).

False positive verified empirically: re-ran `python tools/harvest_factionmap_strings.py` and git diff showed zero changes (file remains 2049 CRLF / 0 LF). Python's universal-newline mode handles this correctly on Windows.

Tests: `FactionSelectionServiceTests.FormatDifficultyText_ValidDifficulty_ReturnsKeyedString` updated to assert the new prefixed form. Build green, 2798/0/2 (no regression).

RCA: [`docs/reviews/rca-faction-map-phase2-2026-06-01.md`](docs/reviews/rca-faction-map-phase2-2026-06-01.md).

### feat(faction-map): rewrite all 16 playable factions + key for localization (Phase 2 of #260)

Full content rewrite of the CC faction-map page for all 16 playable factions (`stewardship_of_gondor`, `dominion_of_mordor`, `dominion_of_isengard`, `overlordship_of_dol_guldur`, `overlordship_of_gundabad`, `havens_of_umbar`, `kingdom_of_erebor`, `kingdom_of_imladris`, `kingdom_of_lasgalen`, `kingdom_of_lothlorien`, `kingdom_of_rohan`, `clans_of_dunland`, `kingdom_of_dale`, `khudorom_of_khand`, `taskralan_of_harwan`, `golden_realm_of_rhun`). Each faction's perks/bonuses/special_units/strengths/weaknesses now reflect the **97 cultural feats currently shipping** (post-commit `fd11414`) — terrain speed, party size, garrison wage, smithing, raid damage, food consumption, the per-occupation notable hubs for Isengard / Dol Guldur, plus honest negatives (Rohan Cavalry Dependent, Isengard Saruman's Grip, Mordor Dark Tribute, Dol Guldur Voracious Hordes, etc.).

Concurrently: **every player-facing string** in `factions.json` is now wrapped in `{=KEY}default` format using the `taom_faction_<json_key>_<section>_<index>` convention. **599 new localization keys** added to `taom_module_strings.xml`. Phase 1's `FactionDisplayHelper.Localize` resolution path makes the keyed strings display as their English fallback until Phase 3 propagates translations.

Non-playable factions (29 stubs) get name + description + traits keyed in place — no content rewrite (their CC page is never shown).

New tooling: [`tools/harvest_factionmap_strings.py`](tools/harvest_factionmap_strings.py) (one-off harvester — walks `factions.json`, extracts every `{=key}default` pair, writes matching `<string>` entries into a marked block in `taom_module_strings.xml`. Idempotent re-runs.).

New tests: [`TAOM.Tests/Features/FactionMap/FactionMapDataTests.cs`](TAOM.Tests/Features/FactionMap/FactionMapDataTests.cs) — 20 assertions including (a) JSON parses, (b) ≥16 playable factions, (c) per-playable-faction (DataRow ×16): ≥2 perks, ≥3 bonuses, ≥1 special_units, ≥3 strengths, ≥2 weaknesses, ≥3 traits, (d) every string field starts with `{=KEY}`, (e) every `taom_faction_*` key referenced in JSON has a matching `<string>` entry in `taom_module_strings.xml`.

**Verification:** `dotnet build TAOM.Tests` → 0 errors. `dotnet test TAOM.Tests` → **2798 / 0 / 2** (up from 2778). FactionMap filter → **89 / 0 / 0** (up from 69). `python tools/validate_moduledata.py` → PASS. `taom_module_strings.xml` parses (1863 entries, up from 1264).

Save-compat: safe. Performance: safe.

Not-tested: in-game render of each faction's new page (requires the running game — verified per Phase 3 in-game smoke). Special-units names use lore-appropriate generic forms (Citadel Guard, Swan Knight, Black Uruk, Cave Troll, Mumakil War Tower, etc.); exact troop-tree alignment confirmed during Phase 3 in-game pass.

### fix(wanderers): dwarf wanderers wore vanilla cloth that clips the dwarf skeleton

The 12 Erebor wanderers (`spc_wanderer_erebor_0`…`_11`, `race="dwarf"`, `Culture.erebor`) render on a **custom dwarf skeleton** that vanilla Bannerlord clothing meshes don't fit — the Encyclopedia showed them in a vanilla green tunic (`Item.tunic_with_shoulder_pads`) floating/clipping on the body. All 12 share one roster, `npc_companion_equipment_template_erebor`, which was populated entirely with vanilla items (`vlandia_sword_1_t2`, `leather_cap`, `padded_leather_shirt`, `scarf`, `leather_gloves`, `strapped_shoes`, `fortified_kite_shield`, …).

Swapped that roster (battle + civilian) to **low-end dwarven** equipment authored for the dwarf skeleton — `sk_dwarf_erebor_*` / `sk_dwarf_iron_*` armour + `sm_dwarf_erebor_*` weapons/shields, mirroring the lowest Erebor troop (`erebor_militia_spearman`). 6 battle sets (leather chest + light helmet + light pauldron + gloves + boots, varied 1h-axe/spear + leather shield) and 3 civilian sets (unisex dwarven tunic `sk_dwarf_tunic_*` + light boots + low dwarf sidearm; bare head shows the dwarf's beard). Template **structure** was already correct vs vanilla `npc_companion_equipment_template_khuzait` (right `equipmentType="Civilian"` tagging, slot vocabulary, no horse for an infantry culture) — this is a pure item-ID swap.

Scope: one roster edit fixes all 12 wanderers; the roster id is referenced only by `taom_wanderers.xml`. Dwarf named companions (Gimli et al.) and dwarf troops already use dwarf gear. Dwarf town notables (`characters/npcs_erebor.xml`) may have the same clip — a possible separate follow-up, not included here.

File: `Main/_Module/ModuleData/equipmentsets/taom_wanderer_equipment.xml`.

**Verification:** `python tools/validate_moduledata.py` → PASS (no `BROKEN_ITEM_REF`, civilian sets stay tagged). ModuleData-only change — no build/test impact. In-game Encyclopedia/battle check pending.

### feat(faction-map): resolve TextObject keys in CC faction display (Phase 1 of #260)

Phase 1 of the CC faction-map rewrite (issue [#260](https://github.com/haterade22/TAOM/issues/260)) — adds the localization resolution path. `FactionDisplayHelper.ApplyResult` now wraps every player-facing string flowing from `factions.json` to a VM property in `new TextObject(s).ToString()`. Plain English strings pass through unchanged (`TextObject("Stewardship of Gondor").ToString() == "Stewardship of Gondor"` for tokenless input), so this commit is a no-op for the current shipped content — but unlocks Phase 2's `{=KEY}default` keyed strings for translation propagation.

Wrap points: `SelectedFactionName`, `SelectedFactionDesc`, `DifficultyText`, each `traits[]` / `bonuses[].text` / `perks[].name+description` / `special_units[].name+description` / `strengths[]` / `weaknesses[]` entry. Non-display fields (`Side`, `ImageId`, `*ColorHex`, `BannerImage`, numeric `Difficulty`) stay raw.

**Standing instruction codified (2026-06-01).** Any TAOM work touching cultural identity — CulturalFeats code, `taom_spcultures.xml <cultural_feats>`, `spcultures.xslt`, party-size / volunteer respawn / notable counts / terrain speed / garrison/militia/smithing/raid/morale/loyalty per-culture variants — MUST also update the matching faction's content in `Main/_Module/ModuleData/factionmap/factions.json`. The CC sidebar is the player's primary input for picking a starting culture; the in-game effect shipping without a matching UI update means the page lies. Codified in `feedback_faction_map_update_with_cultural_feats.md` (memory) + step 7 of the "How-To: Add a new feat" section in [`docs/features/cultural-feats.md`](docs/features/cultural-feats.md).

Files:
- `Main/Features/FactionMap/FactionDisplayHelper.cs` — `Localize(string?)` helper + 9 wrap points in `ApplyResult`.
- `TAOM.Tests/Features/FactionMap/FactionDisplayHelperTests.cs` (new) — 6 unit tests: null/empty/plain/keyed-with-default/keyed-without-default/no-token cases.

**Verification:** `dotnet build TAOM.Tests` → 0 errors. `dotnet test TAOM.Tests` → 2778 / 0 / 2 (up from 2772 — +6 new helper tests). FactionMap filter → 69 passed (up from 63).

Save-compat: safe. Performance: safe (helper runs once per faction-click in CC, not per-frame).

## 2026-05-31

### docs(battle-scenes): deep-dive on the worldmap battle-scene grid + LOTR re-author plan

Investigated how Bannerlord 1.4.5 chooses **field-battle terrain** from the `worldmap_battle_scene_grid` texture, prompted by the editor reporting *"Source file is missing"* for the TAOM map's grid. New reference doc [`docs/reference/worldmap-battle-scene-grid.md`](docs/reference/worldmap-battle-scene-grid.md) captures the verified data flow (`MapScene.GetMapPatchAtPosition` → 2-byte index map → `DefaultSceneModel.GetBattleSceneForMapPatch` → `sp_battle_scenes.xml` `map_indices`), the **key fact that the grid is baked into the `Main_map` scene and read natively (`get_battle_scene_index_map`), not loaded by filename**, the two-grids distinction (battle-scene vs `worldmap_colorgrade_grid`), and a phased re-author + bake workflow with a proposed Middle-earth region→terrain mapping.

**Verified state (2026-05-31):** `Patch0_BattleScenes` is disabled (`Main/SubModule.cs:158` commented) → the **active** table is vanilla `SandBox/sp_battle_scenes.xml`; TAOM's extended 0–255 copy is inert until Patch0 is re-enabled (it's loaded only by `Campaign_InitializeScenes_Patch`, not via SubModule.xml). Proof the grid is **baked, not bound**: neither vanilla nor TAOM `Main_map/references.txt` references `battle_scene_grid` (only colorgrade), and vanilla ships no loose grid asset. The user's grid reimport produced a compiled `tex.tpac` (resolving "Source file is missing") but **`Main_map` was not re-baked** (still the 05-28 bake) — so battle selection won't change until the scene is re-baked in the editor.

Cross-linked from `docs/INDEX.md`, `docs/features/battle-scenes.md`, `docs/reference/scene-reference-audit.md`. Memory: `feedback_battle_scene_grid_baked_not_runtime_swap`. No code/data changes — investigation + docs only.

### fix(bandit-management): hideout boss fight spawned all units friendly (forced retreat)

Raiding any LOTR bandit hideout and reaching the boss fight left **every bandit friendly** — the boss gave the generic guard line *"Can't talk right now. Got to keep my eye on things around here."* and the player was forced to retreat instead of fighting. Earlier waves were correctly hostile; only the boss phase broke. (Reported at a Dunlending raider camp.)

**Root cause:** the vanilla hideout boss fight opens by setting the player/bandit teams non-enemy (`HideoutMissionController.OnInitialFadeOutOver` → `SetIsEnemyOf(_enemyTeam, false)`) for the boss walk-up, and only restores enmity as the consequence of the `bandit_hideout_start_defender` boss dialog (`StartBossFightDuelMode`/`StartBossFightBattleMode`). TAOM's 8 bandit cultures pointed `bandit_boss` at **shared regular-roster troops** carrying `occupation="Soldier"`. `GuardsCampaignBehavior.conversation_guard_start_on_condition` fires for any conversation NPC whose `Occupation == Soldier` (enum value **7**) inside a settlement — so the **guard dialog hijacked the boss conversation**, the taunt-and-fight options never appeared, enmity was never restored, and all units stayed friendly. (Vanilla bandit bosses use `occupation="Bandit"`, value 15, which the guard condition ignores.)

**Fix:** added 8 **dedicated** `{culture}_boss` NPCCharacters (`occupation="Bandit"` + the matching bandit culture, mirroring vanilla `sea_raiders_boss`) — faithful copies of the prior boss troops' stats/gear, minus upgrade chains/civilian sets — and repointed each culture's `bandit_boss` (`taom_spcultures.xml`) and `{culture}_boss_party_template` 1× stack (`taom_partyTemplates.xml`) at them. The shared regular troops are untouched (they're mid-upgrade-chain / in regular party templates, so editing them in place would corrupt the normal rosters). The correct bandit culture also restores `HideoutMissionController.SelectBossAgent`'s `Culture.BanditBoss` match (the intended boss, not a highest-level fallback).

Covers all 8 hideout-spawning bandit cultures: `dunland_raiders`, `rhun_raiders`, `harad_raiders`, `gundabad_raiders`, `umbar_corsairs` (the 5 raiders) + `gondor_soldiers`, `erebor_warriors`, `mirkwood_stalkers` (the 3 Wave-2 offshoots — confirmed hideout-homed bandit clans with the same `occupation="Soldier"` collision).

Files: `troops/troops_{dunland,umbar,rhun_new,harad,gundabad,gondor,erebor,mirkwood}.xml` (+8 troops), `taom_spcultures.xml` (8 `bandit_boss` repoints), `taom_partyTemplates.xml` (8 boss-stack repoints). Feature doc updated (`docs/features/bandit-management.md`).

**Verification:** `python tools/validate_moduledata.py` → PASS (4,161 NPCCharacters, no broken refs / dup ids); all 10 edited XML files parse well-formed; `dotnet test TAOM.Tests` → 2760 passed / 0 failed / 2 skipped. In-game confirmation (raid a hideout → boss now taunts + offers duel/battle, units hostile) pending.

Not-tested: live hideout boss fight (requires the running game).
Save-compat: additive — 8 new troop ids; `bandit_boss` + party-template refs resolve at load. No existing troop id changed; safe on existing saves.
Research: `HideoutMissionController.{OnInitialFadeOutOver,SelectBossAgent,StartBossFightDuelModeInternal,StartBossFightBattleModeInternal}`, `HideoutConversationsCampaignBehavior.bandit_hideout_start_defender_on_condition`, `GuardsCampaignBehavior.conversation_guard_start_on_condition`, `Occupation` enum.
Follow-up: 8 new boss-name `{=taom_*_boss_name}` keys carry inline English defaults; propagate via `/localize` later. Boss stats mirror the prior shared troop (no balance change); a boss-difficulty pass is deferred.

### fix(cultures): add missing wanderer conversation + faction strings for Âbanissa & Shaghâna

The two new custom cultures `abanissa` (Âbanissa) and `shaghana` (Shaghâna) — added in `09a9c2d` with full structure (culture/kingdom/clan defs, notables, lords, and 10 wanderer NPCCharacter templates each) — were never given the *content strings* their wanderer templates require. In-game, recruiting/talking to one of their tavern wanderers showed hard errors like `ERROR: Text with id backstory_c doesn't exist! Variation: spc_wanderer_abanissa_6`. Vanilla `LordConversationsCampaignBehavior` calls `GameTexts.FindText("backstory_c", "spc_wanderer_abanissa_6")` and `GameTextManager.FindText` has **no culture fallback** — a missing variation renders the literal error string. (Wanderer *names* rendered fine because those are inline-default `TextObject`s on the `name=` attribute, not GameText-collection lookups.)

**Gap 1 — wanderer conversation strings (`taom_wanderer_strings.xml`):** authored the full 8-string arc (`prebackstory`, `backstory_a/b/c/d`, `response_1`, `response_2`, `generic_backstory`) for all 20 wanderers — 10 per culture × 8 = **160 entries**. Each backstory is unique, lore-consistent prose written to the NPC's existing traits/voice/age and the culture's flavor (Âbanissa: deep-south jungle stone-dynasty Houses, war-mûmakil, ruler styled *Châjaphân*; Shaghâna: eastern-steppe tribal raiders/horsemen, nine clans, ruler styled *Taskral*).

**Gap 2 — culture/faction encyclopedia strings (`taom_module_strings.xml`):** comparing against a complete culture (Erebor) surfaced that abanissa/shaghana lacked the 17-key `str_culture_*` / `str_faction_*` set every one of the 10 sibling custom cultures defines (faction official/ruler/term-in-speech/name-with-title/adjective/short-term/formal+informal/neutral-term + culture description/rich-name/adjective/player-parent-names). Added the full **17-key set × 2 = 34 entries**, mirroring Erebor's exact `{?RULER.GENDER}…{?}…{\?}` / `{RULER.NAME}` token format and the cultures' established lore (kingdom owners Phaxar/Zarkan, ruler titles Châjaphân/Taskral). (XSLT-repurposed cultures harad/rhun/dunland don't need these — they key off the vanilla engine id aserai/khuzait/empire — but abanissa/shaghana are brand-new `is_main_culture` cultures, so they do.)

**Verification:** both XML files parse well-formed; exact counts confirmed (160 + 34 = 194 new strings, every wanderer has all 8); 0 duplicate `<string>` ids in either file; `python tools/validate_moduledata.py` → PASS. In-game confirmation (tavern conversation shows prose with zero error lines; encyclopedia culture page) pending — XML/content-only change, no C# touched.

Not-tested: live tavern wanderer conversation + encyclopedia rendering (require the running game).
Save-compat: additive GameText only — no SyncData, no save impact.
Follow-up: the 194 new `{=…}` keys carry inline English defaults (errors gone now); propagate to the 11 AI-translated languages later via `/localize` → `tools/translate_with_claude.py`.

### feat(castle-recruitment): recruit troops from castles (player + AI)

Castles become a recruitment source for the first time — previously the player and AI could only recruit volunteers at towns and villages. New feature module `Main/Features/CastleRecruitment/` (Patch42).

**Player:** a "Recruit troops" option on the castle menu opens the vanilla recruit screen against castle notables (reuses loc key `{=E31IJyqs}`). **AI:** lord parties now score, travel to, and drain castle volunteers like towns — two Harmony transpilers swap the `!settlement.IsCastle` AI-scoring gates (`AiVisitSettlementBehavior.AiHourlyTick` + `FillSettlementsToVisitWithDistancesAsDays`) for a runtime toggle, plus a postfix on `HourlyTickParty` invokes the private `CheckRecruiting` for castles.

**Notables:** spawned via `HeroCreator.CreateNotable` on new game + save-load (retrofits existing saves) and maintained daily; vanilla's `OnHeroCreated` places them. Castle-safe occupations only (GangLeader/Headman/Merchant/Artisan — never RuralNotable, which NREs for castles). Volunteers generated daily via a castle-safe probability (vanilla's production model NREs on a castle's null `.Village`); castle notables draw culture-correct LOTR troops from the existing `VolunteerRecruitmentService` `castle_*` pools. Issues/quests suppressed for castle notables via `CanHaveCampaignIssuesEvent` (relations untouched).

**Config:** MCM group "Castle Recruitment" + `castle_recruitment_config.json` — master toggle, AI toggle, notables-per-castle (1-5, default 3). Disabling leaves existing castle notables inert in the save.

**Review:** `/deep-review` (5 agents) — 30/30 APIs verified, 0 incompatibilities; 5 findings fixed in-session (hot-path open-delegate instead of `MethodInfo.Invoke`; transpiler hardened to first-`get_IsCastle` + anchor fail-safe; wider anchor window; `default: throw` on the occupation map; behavior split to `CastleNotableMaintainer` for ADR-002). 2762 tests pass (24 new). RCA: `docs/reviews/rca-castle-recruitment-2026-05-31.md`. Feature doc: `docs/features/castle-recruitment.md`.

Constraint: castle's `Settlement.Village` is null → vanilla `GetDailyVolunteerProductionProbability` / `GetBasicVolunteer` (rural path) NRE for castles; volunteer fill is a castle-safe reimplementation rather than a gate-widen.
Not-tested: Harmony transpilers + postfix + menu (require live game).
Save-compat: additive — castle notables are engine-persisted Heroes; no new SyncData.

### feat(cultural-feats): per-occupation town notable counts + Isengard/Dol Guldur Gang-Leader hubs

Refactors the per-settlement notable-count feat dispatch from per-(culture, settlement-type) uniform multipliers to per-(culture, occupation) flat-add semantics for towns, so asymmetric distributions like "few Merchants, many Gang Leaders" become expressible. Feat total **92 → 97** (+5 net: −4 uniform town feats, +9 per-occupation town feats; village feats unchanged).

**Why:** the shipped uniform multipliers couldn't differentiate per slot at small bases. `ceil(2 × 1.05) = ceil(2 × 1.50) = 3` for both Merchants and Gang Leaders, so Mordor's intended +5% and Dol Guldur's intended +50% both produced 3 / 3 / 2 = 8 in town — the differentiation vanished into ceiling rounding. Also, Isengard's single town capped at 8 notables even at +50%, which isn't enough to seed AI recruitment competitive with Rohan's distributed town/village/castle map. The per-occupation Add design gives direct slot-level control (`+12 Gang Leaders` reads as "+12 Gang Leaders," not "+650% Gang Leaders").

**New town target distributions** (Merchant / Artisan / Gang Leader / Total):

| Culture | Vanilla | New | Change |
|---------|---------|-----|--------|
| Mordor | 2/1/2 = 5 | 2/1/4 = **7** | +2 Gang Leaders only |
| Gundabad | 2/1/2 = 5 | 2/2/5 = **9** | +1 Artisan, +3 Gang Leaders |
| **Isengard** | 2/1/2 = 5 | 4/2/14 = **20** | +2 Merchant, +1 Artisan, +12 Gang Leaders (Isengard has 1 town; this is the recruitment hub) |
| **Dol Guldur** | 2/1/2 = 5 | 3/2/15 = **20** | +1 Merchant, +1 Artisan, +13 Gang Leaders (shadow command center) |

Villages unchanged: 3 RuralNotable + 2 Headman = 5 across all 4 cultures.

**New `NotableOccupationKind` enum** (TAOM-owned) — `TaomNotableSpawnModel` maps the sealed TaleWorlds `Occupation` to this kind at the boundary so the service stays free of engine types (ADR-007). Service signature: `int ApplyNotableCountFeat(culture, NotableOccupationKind occupation, int baseCount)`. Town occupations sum flat `Add` feats; village occupations keep the legacy uniform per-(culture, village) `AddFactor` with ceiling.

**Removed** 4 old per-(culture, town) feats: `taom_{isengard,dolguldur,mordor,gundabad}_notable_count_town`. **Added** 9 per-(culture, occupation) town feats with `AddType.Add` semantics: 3 for Isengard, 3 for Dol Guldur, 2 for Gundabad, 1 for Mordor. Skip-zero — feats with no boost for a given (culture, occupation) pair aren't registered.

**17 new Gang Leader NPCs** to give the engine enough distinct templates for the new targets without clone-spawning. Isengard gets `spc_notable_isengard_gl5` through `_gl12` (8 new), Dol Guldur gets `spc_notable_dolguldur_gl5` through `_gl13` (9 new). Each is BOTH defined in `characters/npcs_{culture}.xml` AND registered in the culture's `<notable_templates>` block — the two-layer registration rule we codified in the previous commit's RCA. Equipment cycles between light/medium variants; trait blocks cycle Calculating/Valor + Mercy/Generosity penalties to keep generated heroes from feeling identical.

**Tests:** the 8 old uniform-multiplier notable-count tests are replaced by 13 per-occupation tests covering: each new Add feat applied on the right occupation, isolation (town feats don't fire on village occupations and vice versa), `NotableOccupationKind.Other` is a no-op, Mordor's gang-leader-only registration doesn't affect Merchant/Artisan slots. Reflection table updated (−4 +9). EachCulture dict: Isengard 11→13, Gundabad 9→10, DolGuldur 8→10, Mordor 11 unchanged (net feat count same, different slots).

Build + test: **2772 passed / 0 failed / 2 skipped** (up from 2760 — +12 new dispatch tests, including the `_DolGuldurArtisan_AddsOne` cell added per Codex review #45). ModuleData validator clean. Save-compat safe (FeatObjects are init-time; new notables spawn on the weekly tick as new Hero objects).

**Codex review #45 (2026-05-31)** — 0 CRITICAL / 1 HIGH / 1 MED. HIGH: missing `(Dol Guldur × Artisan)` dispatch test, even though the feat was declared, registered, XML-bound, and in the reflection-init table — Codex enumerated the (culture × occupation) cross-product cells and caught the gap that all 5 deep-review agents missed. Fixed: added `ApplyNotableCountFeat_DolGuldurArtisan_AddsOne`. MED: vanilla `DefaultHeroCreationModel.GetRandomTemplateByOccupation` samples templates with replacement, so pool size = target (Isengard 14/14 GL, Dol Guldur 15/15 GL) yields expected `~0.632 × N` distinct archetypes (~9 of 14, ~9.5 of 15) with the rest as duplicates. **Known characteristic, documented in `docs/features/cultural-feats.md`** — the user can request pool headroom or a no-replacement Harmony patch after in-game observation; the chosen target was AI-recruitment density, not encyclopedia name diversity. New systemic memory: `feedback_per_branch_dispatch_test_enumeration.md` (per-cell coverage, not per-axis test count). RCA: [`docs/reviews/rca-cultural-feats-per-occupation-2026-05-31.md`](docs/reviews/rca-cultural-feats-per-occupation-2026-05-31.md). REVIEW-LOG entry 45.

### feat(cultural-feats): party-size retune + Dunland/Rhun/Harad party-size feats + village volunteer respawn-rate feats + per-settlement notable-count feats

Three new cultural-feat dimensions plus a retune of the existing party-size set. Feat total **77 → 92** (+15 new, 4 retuned). All apply through the existing `CulturalFeats` `FeatObject` pipeline.

**Party size** — 4 retunes + 3 new XSLT-culture feats:
| Culture | Old | New |
|---------|-----|-----|
| Mordor | +30% | **+10%** |
| Gundabad | +30% | **+20%** |
| Dol Guldur | +25% | **+20%** |
| Gondor | +10% | **+2.5%** |
| Isengard | +20% | +20% (unchanged) |
| Dunland (empire) | — | **+5%** (new) |
| Rhûn (khuzait) | — | **+5%** (new) |
| Harad (aserai) | — | **+5%** (new) |

**Village volunteer respawn rate** (new) — `TaomVolunteerModel.GetDailyVolunteerProductionProbability` override wraps the vanilla `float` in an `ExplainedNumber`, applies feats via `ICulturalFeatsService.ApplyVolunteerRespawnFeats`, and clamps to `[0,1]`. Keyed on `settlement.OwnerClan.Culture` (matches `TaomSettlementMilitiaModel` — a Mordor village produces +20% volunteers while Mordor owns it; conquest flips the bonus on the next daily tick). No vanilla culture has a volunteer-rate feat to mirror, so this is a brand-new hook site — the `ExplainedNumber + AddFactor` pattern matches how vanilla `DefaultVolunteerModel` already applies the Cantons policy and CavalryTactics perk.
- Dunland (empire) **+10%**, Gundabad / Dol Guldur / Mordor **+20%**

**Notable count per settlement** (new) — new `TaomNotableSpawnModel : DefaultNotableSpawnModel` overrides `GetTargetNotableCountForSettlement(Settlement, Occupation)`. Keyed on `settlement.Culture` (settlement identity, NOT ownership — an Isengard town stays Isengard-flavored even when conquered). Returns `(int)Math.Ceiling(base × (1 + bonus))` so any positive bonus on any non-zero base advances by at least 1 — the user's stated +5% Mordor targets are deliberately meaningful, not no-ops on the small ints vanilla returns. Vanilla totals are town = 5 notables (2 Merchant + 2 GangLeader + 1 Artisan), village = 3 (2 RuralNotable + 1 Headman). With feats:
- Isengard / Dol Guldur: town **+50%**, village **+10%** (Isengard has only 1 town — this is the biggest motivator)
- Mordor: town **+5%**, village **+5%**
- Gundabad: town **+10%**, village **+10%**

**Supporting XML work** — 4 new `RuralNotable` templates (`spc_notable_{isengard,mordor,dolguldur,gundabad}_23`) so the new village target of 3 doesn't make the engine reuse one of the existing 2 templates and spawn clone notables. Each new template duplicates `_22`'s structure with a distinct LOTR-flavored name (no equipment changes). **Critical two-layer registration:** each new NPC is BOTH defined in `characters/npcs_{culture}.xml` AND registered as `<template name="NPCCharacter.spc_notable_{culture}_23" />` in the culture's `<notable_templates>` block in `taom_spcultures.xml` — the engine pools from the latter; the NPC file alone is unreachable. Deep-review Agent 5 (Data Flow) caught a HIGH gap where only the NPC-file half had been done before the fix — exactly the clone-notable bug the new templates exist to prevent. RCA: [`docs/reviews/rca-cultural-feats-3pack-2026-05-31.md`](docs/reviews/rca-cultural-feats-3pack-2026-05-31.md). New rule paragraph in `.claude/rules/xml-data.md` + feedback memory `feedback_notable_template_two_layer_registration` codify the two-layer requirement so future authors aren't caught by it.

**Consistency fix (carries over from Codex review 43):** `TaomPartySizeModel` was using `party.Owner?.Culture ?? party.Culture` — even narrower than the speed model's old `Owner.Culture` pattern. Extracted `ResolvePartyCulture` to `CultureFeatAdapter.FromOrNull(PartyBase?)` so all models share one vanilla-`PartyBaseHelper.HasFeat`-precedence resolver (`LeaderHero.Culture → party.Culture → Owner.Culture → Settlement.Culture`). `TaomPartySpeedModel` switched over; its private `ResolvePartyCulture` helper was removed.

**Codex follow-up (review 44) — 4-model culture-resolver sweep.** `/review-codex` flagged `TaomPartyTroopUpgradeModel` still using the old narrower `Owner?.Culture ?? party.Culture` resolver. The author's broader audit triggered by that flag found **3 additional sibling models** Codex didn't enumerate (`TaomFoodConsumptionModel`, `TaomPartyMoraleModel`, `TaomPartyHealingModel`). All 4 fixed by adopting the shared helper; `CultureFeatAdapter` gained a `ResolvePartyCulture(PartyBase?)` raw-`CultureObject?` overload so the healing model (per-culture config-StringId lookup, not feat check) can use the same precedence. Codex also caught a LOW doc-table drift — `docs/features/cultural-feats.md` party-size table still showed pre-retune values; updated. All 10 Codex Known Suspects came back no-bug or disputed-with-evidence (notably S2 vanilla `hero.CurrentSettlement` NRE risk — cleanly DISPUTED rather than papered over with a defensive guard). Build + test: **2760 passed / 0 failed / 2 skipped** after the 4-model fix. Systemic lesson: the existing `feedback_replicate_vanilla_safety_gates_in_prefix` memory gained a third extension — *when fixing a per-model boundary convention in one GameModel, grep all sibling `Taom*Model.cs` for the same pattern* — the same pattern repeats more than the author thinks.

**Architecture:**
- `CultureFeatAdapter.FromOrNull(PartyBase?)` — shared boundary helper (new overload).
- `ICulturalFeatsService.ApplyVolunteerRespawnFeats` — new `void` method matching the existing `Apply…Feats` shape.
- `ICulturalFeatsService.ApplyNotableCountFeat(culture, isTown, baseCount) → int` — new int-returning method (notable target is `int`, not `ExplainedNumber`).
- `TaomVolunteerModel` constructor gains `ICulturalFeatsService`; `SubModule.cs` hoists `culturalFeats = IoC.Resolve<…>()` above the volunteer registration so both blocks reuse the same reference.
- `TaomNotableSpawnModel` registered alongside other cultural-feat models in `SubModule.cs`.

Tests: 20 new dispatch tests (3 new party-size cultures + retune defensive + 6 volunteer respawn + 11 notable-count incl. ceiling edge cases, town/village isolation, zero-base no-inflate). `EachCulture_HasExpectedFeatCount` updated for Isengard/Gundabad/Dol Guldur/Mordor (+3 each) + Dunland (+2), Rhûn/Harad (+1). Full suite **2735 passed / 0 failed / 2 skipped**. ModuleData validator clean.

Save-compat: safe — `FeatObject`s are init-time campaign objects; new notables spawn on the weekly tick as `CampaignObjectManager` Hero objects (vanilla does the same when a notable dies and is replaced); volunteer probability is consumed inline each daily tick with no SyncData.

Files: `Main/Features/CulturalFeats/{CultureFeatAdapter.cs, TaomCulturalFeats.cs, ICulturalFeatsService.cs, CulturalFeatsService.cs, Models/TaomPartySpeedModel.cs, Models/TaomPartySizeModel.cs, Models/TaomNotableSpawnModel.cs (new)}`, `Main/Features/TroopProgression/Models/TaomVolunteerModel.cs`, `Main/SubModule.cs`, `Main/_Module/ModuleData/{taom_spcultures.xml, spcultures.xslt, characters/npcs_{isengard,mordor,dolguldur,gundabad}.xml}`, both CulturalFeats test files.

### fix(gui): troop thumbnails stuck on loading spinner — stale 1.4.5 prefab clones

The Party screen rendered every troop row's character thumbnail as a perpetual
loading spinner (the rotating circle of dots) — no troop image ever appeared.
Reported on a Dale party, but the cause was **global** (all cultures).

**Root cause.** Bannerlord **1.4.5 renamed** the image-source binding on both
`ImageIdentifierWidget` and `MaskedTextureWidget` from `ImageTypeCode` →
`TextureProviderName` (the backing `ImageIdentifierVM` now exposes
`TextureProviderName`). TAOM ships full `<Prefab>` **clones** of several vanilla
GUI prefabs, and these clones were stale from a pre-1.4.5 version — the troop
thumbnail still bound `ImageTypeCode="@ImageTypeCode"`, which in 1.4.5 resolves to
nothing, so the widget never learned which texture provider renders the character
`{Code}` and the `LoadingIconWidget` spun forever. Exactly the stale-clone-across-
versions failure `.claude/rules/vanilla-data-comparison.md` warns about. (Ruled out
during investigation: a missing `fighter_sturgia` body property — it's defined in
vanilla `sandboxcore_bodyproperties.xml` and a body-template mismatch renders a
wrong-looking troop, never a hang; and the `CharacterTableau.SetRace` patch — that
drives the big 3D center preview, not the row thumbnails.)

**Blast radius — 9 prefabs with the stale binding**, fixed in one pass:
- **Re-synced to vanilla 1.4.5** (clones of still-shipped vanilla prefabs; replaced
  verbatim, no TAOM-custom content existed in them): `Party/PartyTroopTuple.xml`,
  `Party/PartyTroopTupleLeft.xml`, `Party/PartyTroopManagerPopUp/PartyTroopRecruitItem.xml`,
  `Party/PartyTroopManagerPopUp/PartyTroopUpgradeItem.xml`,
  `CustomBattle/CompositionSlider.xml`, `CustomBattle/TroopTypeSelectionPopUp.xml`.
  This also picked up other 1.4.5 redesigns the stale clones had missed (restructured
  upgrade popup, percentage slider input, gamepad-nav scopes).
- **`Party/PartyScreen.xml`** re-synced too (it had other stale bindings — missing
  `ScrollToCharacter`/`IsScrollTargetPrisoner`/`ScrollCharacterId`, `TextWidget`→
  `RichTextWidget`, old easing), **re-applying** TAOM's 2 intentional tweaks
  (`PartyNameLabel.Height=55`; wage widget 80 / icon 40 / margin 40 for T10 wages).
  Verified the file now diffs against vanilla 1.4.5 by exactly those 4 lines.
- **Surgical binding rename** (TAOM-original prefabs, no vanilla counterpart; the
  datasource is the vanilla `ImageIdentifierVM`, so XML-only, no VM change):
  `MomentumView/MomentumView.xml` (×2), `MomentumView/KingdomIcon.xml`,
  `MomentumView/Relationship.xml`.

Static-verified: 0 `ImageTypeCode` remaining in `Main/_Module/GUI/`; all 10 touched
files well-formed XML; the 6 verbatim re-syncs are byte-identical to vanilla 1.4.5.

`Not-tested: in-game render (GUI prefab XML isn't unit-testable; requires the live game) — confirm troop thumbnails load on the Party screen, the Custom Battle troop-selection screen, and the MomentumView. No C# changed.`

A blast-radius audit (adversarially verified) inventoried all TAOM GUI prefabs: **48 total, 32 are vanilla clones, 16 TAOM-original, only 4 byte-identical to vanilla.** The same 1.4.5-rename failure class beyond `ImageTypeCode` was then **also fixed** (follow-up, this CHANGELOG covers both passes): `CustomBattle/SimpleDropdown.xml` `DropdownWidget` binding `RichTextWidget=`→`TextWidget=` (verified: 4/4 vanilla DropdownWidgets use `TextWidget=`; selected-faction text now binds), and the obsolete `LayoutImp.LayoutMethod`→`StackLayout.LayoutMethod` rename (verified obsolete: 0 vanilla uses vs 926 `StackLayout.LayoutMethod`; value-preserving) across 6 prefabs — `CustomBattle/{ArmyComposition,CustomBattleScreen,SimpleDropdown}.xml`, `FacGen/PreBuildCharacterSelection.xml`, `MomentumView/{MomentumView,Relationship}.xml`. The valid `LayoutImp.Horizontal/VerticalLayoutMethod` attributes (still used by vanilla 1.4.5) were left intact. **`EaseIn` was left untouched (correct action), but the justification was itself wrong** — re-verifying with an attribute-name regex (not a substring grep) + a decompile of `VisualDefinitionTemplate` showed bare `EaseIn=` has **0** vanilla uses (the earlier "18×" was a substring miscount of `EaseType="EaseInOut"`/`IsEaseInOutEnabled`), and the template has no `EaseIn` property — so `EaseIn="true"` is silently ignored: inert dead markup, harmless to leave, not "still used by vanilla." CustomBattle clones were fixed **surgically** rather than re-synced — their other divergences are vanilla *additions* TAOM lacks, not breaks, and re-syncing risks the custom-battle feature. Deliberate TAOM redesigns (Encyclopedia banner-widget swaps, `SettlementNameplateItem` diamond layout, `CharacterCreation*Stage` theming) are NOT drift and were left alone.

Documented: RCA [docs/reviews/rca-party-troop-thumbnail-stale-prefab-clone-2026-05-31.md](docs/reviews/rca-party-troop-thumbnail-stale-prefab-clone-2026-05-31.md); `.claude/rules/vanilla-data-comparison.md` extended with a "GUI prefab clones" section + `GUI/PreFabs` auto-load globs + a v1.4.5 attribute-change table; `.claude/rules/gui-ui.md` pointer; memory `feedback_gui_prefab_clones_stale_across_versions.md`. The rename table lists only **vanilla-verified** changes plus a "verify each against vanilla with a discriminating method — don't trust a list" caution. A later adapted adversarial review (4 dimensions, each finding refutation-tested) hardened this entry: it caught that the `098ede9` PartyScreen re-sync had silently **dropped** the 5 deliberate tweaks the message claimed to preserve (restored — Height=55, both name-label MarginTop=2, wage 80 / icon 40 / margin 40), and that the `EaseIn` evidence was fabricated **twice** via substring grep (audit's "rename" then a "correction"'s "18×") — bare `EaseIn=` is 0 in vanilla and inert; corrected above. The `AutoScroll*Offset`↔`ScrollYOffset` direction was also stated inverted at one point (`ScrollYOffset` is the stale one). The rule now also warns to scope vanilla counts to `SandBox`/`SandBoxCore`/`Native` (the install's `Modules/*` includes the deployed TAOM module, which pollutes a naive count). VM-binding/data-flow review dimension: clean (the re-syncs bind correctly against the vanilla VMs TAOM patches).

`Root-cause: the v1.3.15→v1.4.5 migration scoped to C# API drift + equipment XML; GUI prefab clones (added 2026-03 in c31570f) were outside the audit boundary and shipped stale. Static review can't catch this — only the live game confirms a prefab renders.`

### fix(career): in-game review pass on the career-screen revamp

In-game testing of the 2026-05-30 career-screen revamp surfaced several issues across a few iteration passes; **all are fixed below and confirmed working in-game** (user screenshots, 2026-05-31): singular titles, flush-left tier labels, consistent tier spacing, the One Ring pip rendering, centered "Requires Level N" labels, and hover descriptions aligned row-by-row with the pips.

- **Singular titles** — it's a single-player career, so the player's path/rank titles should be singular ("Warden of the East Bank", not "Wardens"). Singularized the **head noun** of **109** group names via new [tools/singularize_career_names.py](tools/singularize_career_names.py) — a preposition-aware rule that only touches the title's head noun, so objects of prepositions stay plural (e.g. "Wrath under the Trees", "Lord of the Dome of Stars" keep Trees/Stars). Re-injected into `taom_career_choices.xml` and updated all 294 source `<string>` defaults in `taom_module_strings.xml`. Rank titles were already singular (0 changes).
- **Tier rank labels flush-left** — the tier headers (per-career rank names) used a fixed 200px width with centered text, so a longer title (e.g. "Ohtar of the Crossing") wrapped and looked indented vs. shorter ones. Changed to `WidthSizePolicy="CoverChildren"` + `HorizontalAlignment="Left"` — all three flush-left, single-line.
- **Consistent tier node spacing** — locked tiers (T2/T3) hid the whole `+`/`−` column, collapsing node width so the two nodes sat closer than Tier 1 and their headers collided ("Vanguard of the Ramn**Swords** of the Last Bridge"). The button column now always reserves a fixed 70px and only the buttons are gated on `@IsActive`, so all three tiers space identically.
- **Pip sprite (blank) — fixed; TWO causes (corrects the earlier RCA).** The blank pip needed **both** a sprite-sheet bake **and** a prefab render fix:
  1. *Bake.* The pip is a new loose PNG, so the offline `SpriteSheetGenerator.exe` had to pack it. You ran the generator, which baked `career_point_pip` onto sheet 1 of `ui_taom_career_system` (`AssetSources/GauntletUI/ui_taom_career_system_1.png` + `Assets/GauntletUI/ui_taom_career_system_1_tex.tpac`). **There is no `pack0.tpac`** — UI atlases are per-category `<cat>_<n>` pairs; the earlier "baked into `AssetPackages/pack0.tpac`" claim was wrong. Verified the bake by cropping the pip's `SheetX=2428 SheetY=1670` rect out of the regenerated sheet and seeing the gold ring.
  2. *Render (the blocker that survived the regen).* Even correctly baked, the pip stayed blank because the **prefab** drew it at `22×28px` with `Color="#FFFFFF45"` (27% alpha) — a thin gold ring invisible on a near-black node. Fixed prefab-only: pip → `38×38`, state opacities `#FFFFFFFF` (taken) / `#FFFFFFE0` (available) / `#FFFFFF78` (empty), container `46×46`.
  - Pipeline now **decompile-verified** (`SpriteSheetGenerator.Library.dll` → `SpriteEditorDomain.Tick()` / `SpriteDataEditor.Save*`): inputs `GUI/SpriteParts/<cat>/**.png` + `Config.xml`; outputs `GUI/<Module>SpriteData.xml` + `AssetSources/GauntletUI/<cat>_<n>.png` + `Assets/GauntletUI/<cat>_<n>_tex.tpac`. Corrected [gui-sprite-system.md](docs/features/gui-sprite-system.md) "The sprite-bake pipeline", memory `feedback_sprite_atlas_baked_regen_required` (two failure modes), and [RCA finding A](docs/reviews/rca-career-ui-revamp-2026-05-30.md#post-review-in-game-findings-2026-05-31) (which had wrongly concluded regen alone would fix it). Lesson: **baked ≠ visible** — verify both, and only the live game confirms either.
- **"Requires Level N" centered between columns** — first switched the label to `CoverChildren` + `HorizontalAlignment="Center"`, but that centers on the *row* center, which is where it already sat (no visible change). The real cause is the asymmetric **70px `+`/`−` button reserve** on each node's right, which shifts the two node boxes ~35–40px left of the row center; added `PositionXOffset="-40"` to the locked-tier (T2/T3) labels so they land in the actual gap between the boxes.
- **Hover descriptions inline with the pips** — the pip strip and the perk-description list were two parallel `{Choices}` lists with mismatched row heights (pips 46px vs descriptions 40px) and brush-centered text, so descriptions drifted out of row with their pips and floated centered in the node. Matched the description rows to **46px** and switched the text to `CoverChildren` + left-align, so each description sits on its pip's row immediately to the right.

Verified: the **bake** (pip pixels present in the regenerated sheet at the manifest coords); repo `TAOMSpriteData.xml` is byte-identical to the regenerated game-install manifest (a rebuild won't clobber the pip); the fixed prefab is deployed to the game install. The singularization/code work remains at `dotnet test TAOM.Tests` → **2698 passed / 0 failed / 2 skipped** (these in-game fix passes are prefab + docs only — no C#). **In-game confirmed (user screenshots, 2026-05-31):** pips render as gold rings, T2/T3 level labels sit centered between the columns, hover descriptions align row-by-row with their pips, and tier titles are flush-left.

### docs(sprites): decompile-verified GUI sprite-bake pipeline + end-to-end workflow for future sessions

Documented the full, **decompile-verified** Bannerlord UI sprite pipeline (`TaleWorlds.TwoDimension.SpriteSheetGenerator.exe` + `.Library.dll` → `SpriteEditorDomain.Tick()` / `SpriteDataEditor.Save*`) so future sessions follow a correct, repeatable process and stop repeating the career-pip mistakes.

- **[gui-sprite-system.md](docs/features/gui-sprite-system.md)** — rewrote "The sprite-bake pipeline" (inputs `GUI/SpriteParts/<cat>/**.png` + `Config.xml`; generator writes `GUI/<Module>SpriteData.xml` + `AssetSources/GauntletUI/<cat>_<n>.png`; the `Assets/GauntletUI/<cat>_<n>_tex.tpac` is a **separate downstream texture-compile**, NOT a generator output — timestamps confirm; generator invocation/params; **no `pack0.tpac` for UI sprites**), and added two new how-to sections: **"Verifying a sprite (bake + render)"** (the bake check = crop the sprite's `SheetX/Y` rect from the regenerated sheet; the render check = in-game only — **baked ≠ visible**) and **"Deploying a prefab/sprite change"** (+ a consolidated sprite-gotchas table linking the 4 sprite memories).
- **Self-verified via a 3-agent Workflow** (accuracy vs the decompiled generator + live files; repo-wide stale-claim sweep; link/anchor integrity). It confirmed all pipeline claims except one overstatement — the generator does NOT write the `_tex.tpac` (it writes the manifest + `AssetSources` PNG; the texture pass compiles the tpac afterward) — which is corrected above + in gui-ui.md + the memory.
- **Corrected stale claims** repo-wide: the old "automatic on launch" / `pack0.tpac` / "sheet 2" / "runtime-build-from-loose" assertions in gui-sprite-system.md (two spots), [career-system.md](docs/features/career-system.md), and the [RCA](docs/reviews/rca-career-ui-revamp-2026-05-30.md) (finding A + a correction note on the Codex narrative). (Map-module `AssetPackages/pack*.tpac` refs are correct + untouched — those are scene/terrain packs, not UI; verbatim Codex transcripts left as historical record.)
- **Discoverability:** added an "Adding a NEW sprite (not just referencing one)" subsection to the auto-loading rule [.claude/rules/gui-ui.md](.claude/rules/gui-ui.md) (fires when editing prefabs/widgets/VMs/GUI), refreshed the [INDEX.md](docs/INDEX.md) line, and corrected memory `feedback_sprite_atlas_baked_regen_required` (two failure modes: not-baked AND baked-but-invisible).

`Save-compat: documentation only; no game/save/code impact.`

## 2026-05-30

### feat(tooling): schema-driven ModuleData cross-reference validator (TOR_Tools adoption)

Reviewed [TheOldRealms/TOR_Tools](https://github.com/TheOldRealms/TOR_Tools) (MIT, the content-tooling suite for the Old Realms Bannerlord conversion) exhaustively and adopted its highest-leverage idea — *schemas as the source of truth* + a cross-reference/validation engine — into TAOM's Python tooling. Full review + tiered adoption map: [docs/reviews/tor-tools-adoption-review-2026-05-30.md](docs/reviews/tor-tools-adoption-review-2026-05-30.md). Idea-only port; no TOR code/binaries vendored.

- **New unified validator** consolidating the scattered one-shot validators (`validate_all_troop_refs.py`, `audit_item_refs.py`, the `equipmentType` PowerShell snippet, duplicate-id-across-Armory-folder checks) into one declarative, schema-driven, read-only engine: [tools/taom_schema.py](tools/taom_schema.py) + [tools/validate_moduledata.py](tools/validate_moduledata.py) + 3 schemas under [tools/schemas/](tools/schemas/). Feature doc: [docs/features/moduledata-validation.md](docs/features/moduledata-validation.md).
- **Catches the recurring bug classes up front:** `BROKEN_ITEM_REF` (underwear bug), `BROKEN_TROOP_REF` (dead upgrade_target / party-stack / culture troop ref), `UNKNOWN_CULTURE` ("rohan" vs "vlandia"), `DUPLICATE_{NPC,CULTURE,ROSTER}_ID`, `DUPLICATE_ITEM_DEF` (multi-folder Armory shadowing), `MISSING_CIVILIAN_TYPE` (Faramir/Boromir wrong-outfit), `INVALID_ENUM`, `BROKEN_PARTY_TEMPLATE_REF`. Cross-ref patterns are attribute-agnostic (match the prefix on any attribute), registries injected (unit-testable without the game install), checks fail-fast on bad schemas.
- **Verified live:** PASS / 0 issues against the installed game registry, corroborated by `validate_all_troop_refs.py` + a positive-control resolving 27,449 item / 2,869 troop / 3,957 culture / 136 party-template references with zero unresolved.
- **Tests:** [tools/tests/test_validate_moduledata.py](tools/tests/test_validate_moduledata.py) — 19 unittest cases (one per issue code + edge cases). `python -m unittest discover -s tools/tests -p "test_validate*.py"` → 19 passed.
- **Reviewed:** `/deep-review` (4 agents adapted for Python tooling) found 2 HIGH + 5 MED/LOW; `/review-codex` (gpt-5.5 xhigh) independently found a further 2 HIGH + 2 MED (culture-registry pollution from config-file `<Culture id=>`, NPCCharacter file-coverage gap, item comment-pollution, civilian-rule under-match) and correctly disputed all 8 weak suspects (0 false positives). **All 9 confirmed findings verified against source + fixed in-session** (24 tests pass; live still PASS). RCA: [docs/reviews/rca-moduledata-validation-2026-05-30.md](docs/reviews/rca-moduledata-validation-2026-05-30.md). Codex review: [docs/reviews/codex-adversarial-moduledata-validation-2026-05-30.md](docs/reviews/codex-adversarial-moduledata-validation-2026-05-30.md). The HIGH attribute-agnostic-ref fix raised troop-ref coverage 628→2,869.
- **Discoverability wired in** so future sessions/agents actually use it: registered in CLAUDE.md (tool index + Doc-Lookup), a new auto-loaded scoped rule [.claude/rules/moduledata-validation.md](.claude/rules/moduledata-validation.md) (fires when editing troop/character/equipment/culture/party-template XML or the schemas), and a `PreToolUse` hook [.claude/hooks/check-moduledata-validation.sh](.claude/hooks/check-moduledata-validation.sh) registered in `.claude/settings.json` that hard-blocks Claude-driven commits staging ModuleData XML with ERROR-severity findings (fail-open: missing python/game-install/crash never blocks; warnings don't block). Hook tested (allow-paths + block path).
- **MCP server** (the TOR_Tools Tier-2 idea, built): [tools/taom_mcp_server.py](tools/taom_mcp_server.py) — a stdio MCP server (FastMCP / `mcp` SDK) exposing the validation engine as 9 tools (`validate_moduledata`, `item_exists`, `troop_exists`, `culture_exists`, `party_template_exists`, `find_references`, `list_cultures`, `registry_sizes`, `list_schemas`) so agents query mod-data integrity interactively instead of grepping. Backed by a new pure-stdlib query API [tools/taom_query.py](tools/taom_query.py) (+ tests [tools/tests/test_taom_query.py](tools/tests/test_taom_query.py), [tools/tests/test_taom_mcp_server.py](tools/tests/test_taom_mcp_server.py)). Registered as `taom-moduledata` in `.mcp.json` + enabled in `settings.local.json` (needs a Claude restart to activate). Protocol-tested in-process (`list_tools`/`call_tool` — 9 tools, correct results). Built in Python wrapping the engine, so the earlier .NET-version deferral reason didn't apply. All Python tests pass (**63 total** — 38 for this feature + 25 pre-existing).
- **Second `/deep-review` pass** (2026-05-31, after the MCP server was added): 4 adapted agents (Python-correctness, MCP/config integration, completeness, data-flow). Confirmed + fixed: `party_template_exists` had no MCP tool (added — 8→9 tools) and no test (added); `find_references(kind="npccharacter")` silently fell back to an all-prefix search (added a `npccharacter`→`troop` alias); MCP `find_references` didn't expose `limit` (added); no automated MCP-server test (added `test_taom_mcp_server.py`); `_build_query()` startup failures now print a clear diagnostic before re-raising. One agent HIGH ("the hook's `Main/_Module/ModuleData/*.xml` glob misses nested files") was **disputed as a false positive** — bash `case` `*` matches `/` (proven empirically). RCA addendum: [docs/reviews/rca-moduledata-validation-2026-05-30.md](docs/reviews/rca-moduledata-validation-2026-05-30.md).

`Activation: restart Claude Code to load the taom-moduledata MCP server (the mcp SDK is present; config is registered + enabled).`

`Research: TheOldRealms/TOR_Tools schemas/, Core/Validation, Core/Services/CrossReferenceService.cs, Mcp.Host/Tools (12 MCP tools cataloged); TAOM ModuleData XML conventions (.claude/rules/xml-data.md, troops.md).`
`Not-tested: an MCP-server wrapper (deferred, Tier 2 — separate net-version project, needs Claude restart to load).`
`Save-compat: read-only analysis tooling; no game/save impact.`

### feat(career): career-screen UI revamp + 441 web-researched lore names + TOR rank-title adoption

Reworked the career screen ([CareerScreen.xml](Main/_Module/GUI/Prefabs/CareerSystem/CareerScreen.xml)) to read cleanly instead of the prior broken layout (raw group-id headers, nodes clipped to an 80px sliver, a full-width prison-bar "gate" overlay smeared across locked tiers).

**Phase A — visual (C# + prefab):**
- **Tier order flipped** — Tier 3 on top → Tier 1 on the bottom.
- **Gate art removed** — the `locked_gate_*` sprites + `#000000CC` dark-tint overlays are gone; locked tiers now show a clean centered **"Requires Level N"** label. The unlock level is sourced from a new single-source-of-truth `ICareerRegistry.GetTierUnlockLevel(tier)` (1/10/20), and `IsTierAvailable` was refactored to use it.
- **Node readability** — each node now renders an always-visible vertical **point-pip strip** (one pip per choice) with perk descriptions revealed on hover; no more clipping. New `CareerChoiceObjectVM.IsUnavailable` adds a dim "empty slot" pip state so every slot shows a pip (bright = taken, mid = available, ghosted = empty) instead of a blank gap.
- Pip states use **brightness** tints (`#FFFFFFFF` / `#FFFFFFA0` / `#FFFFFF45`) rather than hue tints, so the gold "One Ring" pip sprite keeps its colour. The pip art (a clean engraved gold-on-transparent One Ring) was delivered, resized to 256×256 with alpha, and **registered** in [TAOMSpriteData.xml](Main/_Module/GUI/TAOMSpriteData.xml) on a new dedicated 256×256 sheet (sheet 2 of `ui_taom_career_system`, zero overlap with the existing 4096² sheet) + `GenericSprite`; category already `<AlwaysLoad/>` in `Config.xml`.
- Dead gate VM properties (`Tier2GateBottomHalf`/`Tier3GateTopHalf`/`Tier3GateFull`/`Tier1Locked`) removed.
- New tests: `GetTierUnlockLevel` (3) + `IsTierAvailable` consistency, `IsUnavailable` (4), group `display_name` parse (2). New loc key `taom_career_tier_requirement`.

**Phase B — lore names (data):**
- Authored **294 Middle-earth-lore names** for every career choice-group sub-path (49 careers × 6 groups, 16 culture factions), via **web research** (Tolkien Gateway / LOTR wiki / Encyclopedia of Arda) with honest `attested` flags — canonical terms where they exist (Henneth Annûn, Oath of Eorl, Knights of the Swan-Prow, Bowmen of the Elvenking, Lances of Laurelindórenan, Heir of Castamir…) and flagged originals where canon is thin (Khand/Dunland). Reviewed mapping at [tools/career_group_names.json](tools/career_group_names.json).
- New domain field `CareerChoiceGroupDefinition.DisplayName` + `display_name` XML attribute (parsed by `CareerConfigProvider`); `CareerChoiceGroupObjectVM.GroupName` binds it with a humanized-id fallback. Applied via new idempotent tool [tools/apply_career_group_names.py](tools/apply_career_group_names.py) (`--dry-run`/`--apply`, BOM/CRLF-preserving bytes I/O) → **288 active ChoiceGroups** got `display_name`; the 6 `cave_troll_master_*` ids land inside that career's pre-existing `<!-- DISABLED … WIP -->` comment (inert, left as-is). 294 `{=taom_career_grp_*}` keys harvested into `taom_module_strings.xml`.

**Phase C — per-tier rank titles (TOR_Core adoption):** adopted TOR's `tor_career_rank{1,2,3}_name` convention — the tier headers now show a per-career **rank ladder** (the reference's "Knight Errant → Questing Knight → Grail Knight" pattern) instead of generic "Tier 1/2/3". New `CareerDefinition.Rank1/2/3Name` + `rank{1,2,3}_name` XML attrs (parsed by `CareerConfigProvider`); `CareerScreenVM` binds them to the tier labels with a "Tier N" fallback. **147 web-researched Tolkien-grounded titles** across all 49 careers (e.g. Rivendell's attested `Ohtar → Roquen → Knight of the Golden Flower`; Rohan's `Rider of the Mark → Captain of the Éored → Marshal of the Riddermark`; Dale's `Bowman of Esgaroth → Dragon-Shooter → Bearer of the Black Arrow`), with `attested` flags. Reviewed mapping [tools/career_rank_names.json](tools/career_rank_names.json), applied via [tools/apply_career_rank_names.py](tools/apply_career_rank_names.py); 147 `{=taom_career_rank{1,2,3}_*}` keys harvested. TAOM keeps its improvements over TOR (3-state pips vs TOR's blank-at-0-points, tested service/adapter split, lore-name depth) — see [docs/reviews/tor-career-ui-comparison-2026-05-30.md](docs/reviews/tor-career-ui-comparison-2026-05-30.md). TOR_Core (GPLv3) used with the TOR team's permission.

Reviewed: `/deep-review` (6 agents — standards, API-compat, efficiency, completeness, data-flow, tooling) found no HIGH; the one actionable finding (injector BOM-convention) was fixed; RCA at [docs/reviews/rca-career-ui-revamp-2026-05-30.md](docs/reviews/rca-career-ui-revamp-2026-05-30.md). Verified: `dotnet test TAOM.Tests` → **2698 passed / 0 failed / 2 skipped**; all 4 edited data XMLs parse clean.

`Not-tested: in-game career screen (tier order, "Requires Level N" labels, pip strip + hover, lore group headers + rank-title tier headers) — requires live game.`
`Known-limitation: 29 of 49 careers have no portrait PNG yet (pre-existing; blank portrait, no crash) — tracked for future art.`
`Save-compat: display_name is presentation-only; no persisted-state or save-format impact. Reuse.Singleton config caches for the process — needs a Bannerlord restart, not a reload.`
`Research: per-faction Tolkien lore via tolkiengateway.net / lotr.fandom.com / encyclopedia-of-arda.com (sources noted per faction in the mapping file).`

### balance(cc): cut character-creation bonus budget back to vanilla (7→5 focus)

Audited the skill/attribute/focus bonuses character creation hands out per culture — some builds were stacking into very overpowered starts. The per-pick magnitude was never the problem: before this change, every one of the **435** menu options (96 parents + 6 childhood + 92 youth + 96 education + 96 adulthood + 49 career) granted the exact vanilla bundle `(focus 1, skill-level 10, attr 1)` — identical to vanilla's own `CharacterCreationCampaignBehavior` defaults. The overpowering was **structural**: TAOM has a 6th bonus stage vanilla lacks (the **Career** stage) plus a **culture-base** bonus from `cultures.json`, so a build could collect **7 focus / 6 attribute points** and — because the same per-culture themes repeat across ungated stages — stack **+60 levels onto one skill and +6 onto one attribute** (6 stages × the per-pick bundle).

Cut to vanilla's budget by zeroing the two TAOM-added sources:
- **Career stage** (`career_menu.json`, 49 entries): `focus/skill_level/attribute_to_add → 0`, and the `skills`/`attribute` arrays cleared so a selected career renders the same clean zero-bonus line as the "No specialization" option (`0 unspent Focus/Attribute Point`) instead of a confusing "0 … to Bow and Scouting" on the career menu + final review screen. Career still drives specialization + starting equipment; it just grants no stat bonus.
- **Culture base** (`cultures.json`, 18 entries): `focus/skill_level_to_add → 0`.

Result: **5 bonus stages, 5 total focus, 5 total attribute points** across all 16 selectable cultures — matching vanilla (verified by the audit report). Worst-case single-skill/attribute **concentration was deliberately left as-is** (a min-max build can still stack the 5 narrative stages: +50 on one skill, +5 on one attribute); diversifying the repeated themes is the documented next lever.

Also closed a latent landmine: the C# deserialization/model **defaults still encoded the old 1/10/1**, so a future edit that *deleted* (rather than zeroed) the keys would silently restore the pre-cut budget undetected. Flipped the **Career + Culture** provider + model defaults to `0` (`CareerMenuDataProvider`/`CareerMenuOptionDefinition`, `CultureCreationDataProvider`/`CultureCreationData`) so a missing key now means "no bonus", matching the data. **`NarrativeDataProvider` was deliberately left at 1/10/1** — that *is* the intended per-stage bonus, so its default correctly matches its data (flipping it would create the inverse inconsistency). Updated the two default-value tests to assert `0`.

New reusable tool [tools/audit_cc_bonuses.py](tools/audit_cc_bonuses.py): `--report` (default) emits per-stage uniformity, a **deterministic** value-aware per-culture worst-case concentration table (ties broken alphabetically — the prior `Counter.most_common()` tie-break was hash-seed-dependent and made the report non-reproducible), a vanilla-budget comparison, and a full per-culture menu dump; `--apply`/`--dry-run` perform the budget cut via formatting-preserving line edits (CRLF + inline arrays byte-preserved, writes `.bak`). Also: empty-result CSV guard + a warning when `taom_careers.xml <EligibleCultures>` references non-culture ids (`empire_w`/`empire_s` are kingdom ids — inert at runtime). `*.bak` added to `.gitignore`. Audit report at [docs/reviews/cc-bonus-audit-2026-05-30.md](docs/reviews/cc-bonus-audit-2026-05-30.md).

Reviewed: adversarial multi-agent review (5 dimensions, every finding independently verified) — 0 HIGH, 7 MED; all confirmed findings fixed in this entry (439→435 count, tool non-determinism, career zero-bonus line, C# default landmine, stale docstring, `.bak` gitignore, CSV guard). Verified: `./build.ps1 -RunTests` → **2698 passed / 0 failed / 2 skipped**.

`Research: NarrativeMenuOptionArgs.SetLevelToSkills/SetLevelToAttribute + GetPositiveEffectText; CharacterCreationCampaignBehavior._focusToAdd/_skillLevelToAdd/_attributeLevelToAdd (vanilla defaults 1/10/1).`
`Save-compat: CC-only bonuses applied at character creation; no persisted-state or save-format impact. Reuse.Singleton providers cache for the process — changes need a full Bannerlord restart.`
`Not-tested: in-game CC walkthrough (confirm a few cultures land at vanilla starting stats; career now shows the empty zero-bonus line).`

### feat(troops): wire culture/race body-property templates into all troop rosters

Most TAOM troops still pointed their `<face_key_template>` at vanilla fallback body properties that don't match their culture — `fighter_empire` bled across Gondor, Erebor (dwarves), Rivendell/Mirkwood (elves), Isengard, Dol Guldur, Mordor and Gundabad; Rhun was on `fighter_khuzait`, Rohan on `fighter_vlandia`, Harad/Umbar on `fighter_aserai`, Dunland on `fighter_battania`. Only ~4 elite troops per culture used the correct custom template from `TAOM_bodyproperties.xml`, so dwarves/elves/Haradrim rendered as generic Empire men in-game.

Rewired **665 `face_key_template` values across 13 troop files** (`troops_*.xml`) to the culture- and race-appropriate templates. Human/elf/dwarf cultures get a single template each (Gondor→`fighter_gondor`, Rohan→`fighter_rohan`, Harad+Umbar→`fighter_haradrim`, Rhun→`fighter_rhun`, Erebor→`fighter_erebor`, Rivendell→`fighter_rivendell`, Mirkwood→`fighter_mirkwood`); Dunland round-robins across the five `fighter_dunland_a…e` build variants. The four multi-race "evil" cultures use **per-unit-type classification** keyed on each troop's id: Mordor cave troll→`fighter_cave_troll`, uruks→`fighter_uruk_mordor`, orcs/warg-riders→`fighter_orc_mordor`; Isengard uruk-hai→`fighter_uruk_hai`, berserkers→`fighter_uruk_berserker`, snaga/scout orcs→`fighter_orc_mordor`; Gundabad→`fighter_gundabad` (berserkers→`fighter_uruk_berserker`); Dol Guldur goblins+spider-riders→`fighter_goblin`, orcs/uruks/wargs→`fighter_dolguldur`, and **Khamûl's Easterling "shadow" troops→`fighter_rhun`** (matches the `sk_dg_khml_*`→`rhun/` Armory convention). Lore-aware exception: the 16 `*_militia*` levies in the orc cultures keep their current template (no orc faces forced onto human levies). Dale is left unchanged (no `fighter_dale` template exists); Lothlórien has no troop file.

Applied via a new reusable tool, [tools/apply_troop_bodyproperties.py](tools/apply_troop_bodyproperties.py) (dry-run default, `--apply` to write) — regex-over-raw-text, block-keyed on each NPCCharacter's own id, builds its valid-template set dynamically from `TAOM_bodyproperties.xml`, and preserves UTF-8 BOM + CRLF byte-for-byte. Verified: all 740 references reconcile (701 custom + 35 Dale `fighter_sturgia` + 4 Isengard militia `fighter_empire`, all intentional), all 14 files XML-well-formed, diff surgical.

`Not-tested: in-game face/skin render (requires live game) — confirm Erebor/Mirkwood/Harad, the Mordor cave troll, and Dol Guldur goblins show correct bodies.`
`Save-compat: face_key_template is re-applied on troop spawn; no id changes, no persisted-state impact.`

### feat(troops): regroup Gondor Lossarnach axe-thrower line to Infantry

The Lossarnach Skirmisher → Axe-Thrower → Veteran Axe-Thrower upgrade line (`gondor_loss_skirmisher`, `gondor_loss_axe_thrower`, `gondor_loss_vet_axe_thrower`) was assigned `default_group="Ranged"`, so the engine formed them up in the ranged line. They're melee axemen who also throw axes, so switched all three to `default_group="Infantry"`. Equipment/skills/throwing axes untouched; they keep their axes and still throw, just from the infantry formation. Other Gondor `*_skirmisher` troops (Lebennin, Pelargir, Harondor, Osgiliath, Anórien) are genuine ranged skirmishers and were left alone.

`Save-compat: default_group is a formation-class field re-applied on load; no id change.`

### docs(rules): institutionalize anti-fabrication discipline (evidence-over-claims §C)

Added a third facet to the always-load `.claude/rules/evidence-over-claims.md` — **C. Never fabricate — "I don't know" is the correct answer** — plus a high-visibility **Never Fabricate** row in CLAUDE.md's Critical Rules table. Written from a live failure this session: the hotfix-review doc + CHANGELOG (`c5b0438`) were authored before the proving `diff` output was read, producing an invented changed-type list, a "fix" that never happened, and (caught while writing *this* entry) two fabricated item-audit counts in `41dc2e8` ("12,476 / 2,665" — the real figures are 5,658 / 2,619). Facet C names the principle (uncertainty → research, never invention) and the three mechanical traps: writing the findings artifact before its evidence is in hand; filling a failed/empty read with a guess (the Windows `Read` tool can't see git-bash `/tmp` — write scratch to a repo-relative path); and trusting your own prior tool results from memory. Also corrects the fabricated item-audit counts in the entry below. Memory: `feedback_no_write_before_reading_tool_output.md`.

### chore(engine): v1.4.5 hotfix review + item-reference audit — zero TAOM impact

Bannerlord shipped a **build-number bump** (`v1.4.5.114824` → `v1.4.5.114927`; `Version.xml` still reads `v1.4.5`, every TaleWorlds DLL rebuilt 2026-05-30 06:52). Re-decompiled the installed client and diffed against the pre-hotfix baseline (preserved at `E:\Decompiled_Bannerlord_pre_hotfix_20260529`).

- **Engine delta: exactly 10 managed files.** 6 are the build-changeset stamp (`114824`→`114927`) in `BuildInfo` / `ApplicationVersion` / `VirtualFolders` / launcher `Program` / `DependedModule` / `ModuleInfo`. 3 add a new window-state API to the **standalone windowing layer** — `User32.IsIconic` (P/Invoke), `WindowsForm.IsMinimized`, `GraphicsForm.IsMinimized` (additive; nothing removed/changed). 1 is the only behavioral change: `StandaloneUIDomain` now `Thread.Sleep`s instead of rendering a frame while the window is minimized (CPU/GPU saver). **No CampaignSystem / MountAndBlade gameplay type changed.**
- **Zero TAOM impact — verified, not assumed.** Build clean against the new DLLs (0 errors); binding gate `dotnet test --filter "TestCategory=BindingVerification"` green (35/35, green from the first run — no test change made); full suite 2686/0/2. None of the 10 changed types is referenced by TAOM, and none of the fragile hotspots (3 transpiler targets, `NavigationCacheAdapter`, `MapConversationTableau`) is among them. Full write-up: [docs/migration/api-diff-v1.4.5-hotfix-2026-05-30.md](docs/migration/api-diff-v1.4.5-hotfix-2026-05-30.md).
- **Scene + item audits clean.** `audit_scene_names.py` / `audit_battle_scenes.py`: every TAOM battle scene id on disk, all 256 map_indices covered. `audit_item_refs.py` (weapon/equipment cross-check requested this session): **5,658 defined item IDs, 2,619 TAOM references, 0 broken** (re-run + read this turn) — the hotfix renamed/removed no item TAOM uses.
- **Caveat:** `TaleWorlds.Native.dll` (native) also rebuilt; NativeSkinFixes byte-pattern-scans it at install time — managed-clean ≠ native-clean. Flagged for the next in-game smoke test (watch for the NativeSkinFixes degraded-state banner).

> **Correction (supersedes `c5b0438`).** The original CHANGELOG entry + api-diff doc were authored before the diff output was actually read (a scratch-file read-path failure meant the real `diff` was never seen), so they named the wrong changed types (`ExplainedNumber`, `Clan`, `Hero`, …) and described a `GameModelOverrideBindingTests` "fix" that does not exist — the binding gate was green from the first run and no test file was modified. The `c5b0438` snapshot refresh also captured an in-flight `Patch41_McmLayoutFix` row from uncommitted MCM work; that was reverted in `4d288b5`. The zero-impact *conclusion* was always correct; the supporting *evidence* was not. This entry is re-derived from the real diff. Lesson: `evidence-over-claims.md` B — no claim before the proving output is in hand.

## 2026-05-29

### feat(bandit): themed LOTR hideout descriptions — replace vanilla "(Undefined hideout type)"

TAOM bandit hideouts ("Dunlending Raider's Camp" etc.) showed the literal placeholder **"(Undefined hideout type)"** in the encounter-menu prose. Root cause (decompile-verified, v1.4.5): vanilla `HideoutCampaignBehavior.game_menu_hideout_place_on_init` sets the `HIDEOUT_DESCRIPTION` GameText variable to a `{=DOmb81Mu}(Undefined hideout type)` default, then overrides it **only** for the five hardcoded vanilla bandit culture StringIds (`forest_bandits`, `sea_raiders`, `mountain_bandits`, `desert_bandits`, `steppe_bandits`). TAOM renamed those cultures (`dunland_raiders`/`gundabad_raiders`/`harad_raiders`/`rhun_raiders`/`umbar_corsairs`), so none match and the placeholder leaks through. The hideout *name* was fine because it comes from the settlement `name=` attribute (a different code path).

**Fix:** new `Patch40_HideoutDescription` — a Postfix on the private `game_menu_hideout_place_on_init` that re-sets `HIDEOUT_DESCRIPTION` for TAOM's 5 bandit cultures via the new `IHideoutDescriptionService` (string→string, no TaleWorlds types — ADR-007). The postfix runs inside `on_init` before the menu renders `{=!}{HIDEOUT_TEXT}` (which embeds `{HIDEOUT_DESCRIPTION}`); `MBTextManager` resolves nested GameText vars lazily at render, so the override propagates. Only `hideout_place` shows the variable — `hideout_after_wait` uses its own culture-agnostic text — so a single patch is complete. 5 biome-themed `taom_hideout_desc_*` strings added to `taom_module_strings.xml` (Dunland hills / Gundabad crags / Harad dunes / Rhûn grasslands / Umbar cove), translated into all 11 AI languages (`tools/translate_with_claude.py`) and inserted into each `std_taom_module_strings_<locale>.xml`. 9 service unit tests.

`Not-tested: Harmony patch + GameTexts render (requires live game)` — service logic fully unit-tested; the patch wiring needs in-game confirmation.
`Research: HideoutCampaignBehavior.game_menu_hideout_place_on_init, MBTextManager.SetTextVariable, DefaultBanditDensityModel`
`Save-compat: no persisted state; pure menu-text override`

### feat(bandit): early-game density boost + cap tuning — initial hideouts 7→14, min-infest 1, caps 100/3

The bandit world felt sparse at campaign start because `TaomBanditDensityModel` floored every value at vanilla and only scaled *up* with `PlayerProgress` — at a new game (multiplier 1.0) density was exactly vanilla. Added the real early-game lever: a new `NumberOfInitialHideoutsAtEachBanditFaction` override (vanilla 7 → default **14**), threaded through the existing config → JSON → settings → service → model chain with `[1,30]` validation and a new MCM slider ("Initial Hideouts Per Faction", `World/Bandit Scaling`, Order 6). Each faction now opens with 14 active hideouts and settles toward the steady-state max as the player clears them ("early burst then settle"), while every infested hideout drives the hourly roaming-bandit spawn. Plus three requested knob changes: **max parties/hideout cap 5→3** (= vanilla, pinned), **max hideouts/faction cap 15→100** (physical hideout count binds first), **min parties to infest 2→1** (hideouts go active/visible with a single party). All MCM-tunable; defaults updated in C# + JSON.

**Adversarial review (MED, fixed in-session):** the `Cap()` helper returned *below* vanilla when a user dragged an MCM cap slider under the vanilla base (e.g. parties-cap 3→2 yielded 2, vanilla is 3) — violating the class's own documented "vanilla is always the floor" invariant. Fixed: the effective ceiling is now `max(vanillaBase, hardCap)`, so a sub-vanilla cap can never reduce density below vanilla. `Cap`/`Scale` promoted to `internal static` and covered by 7 new `TaomBanditDensityModelTests` (incl. the floor regression). Two further review findings refuted: "initial 14 > max 9 at progress 0" is the intended design (engine handles it safely — `AddNewHideouts` simply doesn't grow while infested ≥ max); the settings-provider test gap targets a live-MCM path untestable in the harness (`TaomSettings.Instance` is null).

**Upgrade caveat (LOW, documented):** MCM persists settings per-property, so a player upgrading from a build before this change keeps their old `BanditMaxHideoutsPerFaction=15` / `BanditMaxPartiesPerHideout=5`; only the brand-new `BanditInitialHideoutsPerFaction` picks up its default (14). Reset the **World / Bandit Scaling** group in MCM (or edit `TAOM.json`) to get the new tuning. Inherent MCM behaviour, not a bug — see `docs/features/bandit-management.md`.

`Research: DefaultBanditDensityModel, BanditSpawnCampaignBehavior.SpawnHideoutsAndBanditsPartiallyOnNewGame/AddNewHideouts/SpawnBanditsAroundHideout`
`Save-compat: new MCM property + JSON field, read with ?? default; no save-state change`

### fix(ui): MCM mod-options screen rendered bottom-to-top — Patch41 Harmony flip on MCM's loader

The in-game MCM (Mod Configuration Menu) options screen rendered every mod's settings inverted: group headers appeared *below* their members, members in reverse `Order`, and groups in reverse `GroupOrder` (Fief Management `GroupOrder=26` above Messengers `GroupOrder=25`). Decoding the panel bottom-to-top reproduced the correct declared order — a wholesale vertical flip.

**Root cause:** the same v1.4.0 engine regression fixed for TAOM's own prefabs in `ad836d1` (`VerticalBottomToTop` now renders bottom-to-top), but inside the **MCM library's embedded prefabs** — `SettingsView.xml`, `SettingsPropertyGroupView.xml`, `ModOptionsView.xml`, `ModOptionsPageView.xml`, embedded as resources in `Bannerlord.MBOptionScreen.v1.4.x.dll` (MCM v5.11.4). The `ad836d1` mass-flip never saw them (it grepped only TAOM's loose `Main/_Module/GUI/Prefabs/` XML). The C# `Order`/`GroupOrder` values in `TaomSettings.cs` were correct and were **not** changed.

**Why an earlier UIExtenderEx `PrefabExtensionSetAttributePatch` attempt failed (verified by decompiling MCM + UIExtenderEx + the engine prefab system):** MCM does not load these prefabs through the engine's disk-file `WidgetPrefab.LoadFrom` — it reads the embedded XML and registers it via `WidgetFactoryManager.CreateAndRegister(name, XmlDocument)`, which builds the prefab through UIExtenderEx's `LoadFromDocument` **reverse-patch**. That path omits the `ProcessMovie` step where `[PrefabExtension]` patches are applied, so a PrefabExtension on these movies is a structural no-op (only disk-file prefabs like vanilla `MapBar` get patched). No movie-name or registration-order change can route an embedded prefab through the patched loader.

**Fix:** `Patch41_McmLayoutFix` — a thin Harmony **Postfix** on `Bannerlord.UIExtenderEx.ResourceManager.WidgetFactoryManager.CreateAndRegister(string, XmlDocument)` that mutates the `XmlDocument` in place before its lazy parse, flipping every `LayoutImp.LayoutMethod`/`StackLayout.LayoutMethod` `VerticalBottomToTop`→`VerticalTopToBottom`. Pure XML logic in `Main/Features/Mcm/McmLayoutRewriter.cs` (17 unit tests); thin entry in `Main/Features/Mcm/Hooks/Patch41_McmLayoutFix.cs`; registered in `SubModule.OnSubModuleLoad` (before MCM's `Inject()` at `OnBeforeInitialModuleScreenSetAsRoot`). Scoped to MCM's option-prefab names; idempotent and fail-safe (try/catch-swallowed so a cosmetic fix can never block MCM screen registration). The dead UIExtenderEx attempt (`Mcm/UI/McmSettingsLayoutPrefab.cs`) was deleted. Self-scoping: vanilla + TAOM loose prefabs use a different loader entirely and are untouched.

`Not-tested: Harmony Postfix on MCM's loader (requires live game restart — MCM's Inject() runs once per process at startup)`. Build clean; full suite 2686 passed.
`Research: WidgetFactoryManager.CreateAndRegister, WidgetPrefabPatch.LoadFromDocument, MCM ResourceInjector.Load/DefaultResourceInjector.Inject, TaleWorlds.GauntletUI.PrefabSystem.WidgetPrefab.LoadFrom`

### docs(rot): fix 1 dead link + 3 genuinely-stale version refs (lint_docs.py)

Doc-health pass after the settlement work. Fixed the 1 dead link (`rca-cultural-feats-terrain-2026-05-28.md` had a memory file linked via a mangled `../../C:/Users/...` absolute path → converted to a plain `` `feedback_...` `` reference, matching how memories are cited elsewhere). Fixed 3 refs that described TAOM's *current* target as 1.3.15: `spider-skeleton-tpac-tools.md` origin line (said "Bannerlord 1.3.15" one line above its own `bannerlord-1.4.5` branch link) → 1.4.5; `bannerlord-together-compat.md` requirement row → 1.4.5 (kept "BT's stated minimum is 1.3.15" as an accurate caveat, since BT's own 1.4.5 support is unverified post-migration); `patterns.md` dropped the stale "(shipped with Bannerlord 1.3)" Harmony parenthetical (the CodeMatcher point is version-agnostic). Result: dead links 1→0.

**Deliberately left ~34 `1.3.15` linter flags** — they are accurate history, not rot: migration-doc filenames, pinned sig-file names (`scriptcomponentbehavior-v1.3.15.txt`), informational RVA references, port-origin/API-break records, review-log/audit/issue-body entries, and the meta-examples in ADR-010 + `agent-operating-manual.md` that *explain the staleness rule itself*. The linter is a blunt substring matcher; changing these would falsify records or break the examples. (Future option: teach `lint_docs.py` to exempt historical-record doc types / lines that cite both 1.3.15 and 1.4.5.)

### fix(troop-progression): restore buyer-hero recruitment-cost perk discounts (GameModel audit MED)

Resolves the one genuine finding from the GameModel base-delegation audit (below). `TaomPartyWageModel.GetTroopRecruitmentCost` fully replaces vanilla's cost computation (extended T0–T10 table + LOTR cultural feats) but silently dropped vanilla's `if (buyerHero != null)` personal skill-perk discount block — so a player's HeadHunter / ChinkInTheArmor / ShowOfStrength / HardyFrontline / RenownedArcher / Piercer / Frugal / SwordForBarter / SlickNegotiator perks did nothing toward recruitment cost.

- **New `RecruitmentPerkInputs` primitive struct** (in `IWageModifierService.cs`) — pre-resolved perk bonuses + troop/hero facts, mirroring the existing `WageFeatInputs` / `MountedCostFeatInputs` pattern; keeps `WageModifierService` free of TaleWorlds sealed types (ADR-007).
- **Boundary** `TaomPartyWageModel.ResolveBuyerRecruitmentPerks` resolves each `buyerHero.GetPerkValue(perk) ? perk.{Secondary|Primary}Bonus : 0f`; **service** `WageModifierService.SumBuyerPerkFactors` owns the troop-type/tier/leader/mercenary gating (unit-testable) + applies the summed factor with vanilla's `LimitMin(1f)`.
- **Vanilla parity verified via ilspy on the installed v1.4.5 DLL** (`/deep-review` Agent 2): all 9 perks, Primary-vs-Secondary bonus selection, infantry-vs-ranged else-if, and tier≥2 HeadHunter gate match `DefaultPartyWageModel.GetTroopRecruitmentCost` exactly. `KhuzaitRecruitUpgradeFeat` intentionally **not** restored — TAOM replaces the mounted-recruit cultural feat with the Isengard/Rohan mounted-cost feats. Summed-then-single-`AddFactor` is numerically identical to vanilla's sequential calls (confirmed against the `ExplainedNumber` decompile: `AddFactor` accumulates `SumOfFactors += value`).
- **Tests:** 14 new gate tests (skip-guard exhaustion). Full suite 2650 passed / 0 failed. `/deep-review` (5 agents) clean — zero parity drift, zero dead struct fields.
- **Audit false-positive recorded:** `TaomPregnancyModel.GetDailyChanceOfPregnancyForHero` already applies the Virile (Charm) perk identically to vanilla — verified, no change. RCA: `docs/reviews/rca-gamemodel-base-delegation-audit-2026-05-29.md`.

Research: `DefaultPartyWageModel.GetTroopRecruitmentCost`, `ExplainedNumber.AddFactor` (installed v1.4.5). Save-compat: behavior-only (cost calc), no serialized state. Not-tested: GameModel override invocation (requires live game) — service logic fully covered.

### docs(reference): external-resources index + GameModel base-delegation audit

Two follow-ups from the open-web research session:
- **`docs/reference/external-resources.md`** — curated, verified external resources to improve TAOM (official-vs-community Bannerlord docs hierarchy — corrected a research mislabel: `moddocs.bannerlord.com` is the official TaleWorlds site, `docs.bannerlordmodding.com` is community; BUTR deps + Harmony + dnSpy; LOTR/Tolkien canon + RealElvish naming generators + Atlas of Middle-earth; comparable total-conversion mods; save-compat semver). Linked from `docs/INDEX.md`. Lore-resource pointers added to `new-culture-authoring.md` + `lord-skills-authoring.md`.
- **GameModel base-delegation audit** (37 `Taom*Model : Default*Model`, 68 overrides via a 4-agent sweep): **85 classified OK, 6 flagged RISK — but on verification against the actual vanilla decompile, 5 were false positives** (the agents speculated vanilla safety-gates that don't exist — e.g. `DefaultKingdomDecisionPermissionModel.IsStartAllianceDecisionAllowedBetweenKingdoms` is literally `return true`; the wage model is a plain tier-table TAOM intentionally extends to T0–T10; MilitaryPower delegates to base when its toggle is off; Siege replicates vanilla perk-gating + adds Trebuchet by design). **One genuine MED candidate**: `TaomPartyWageModel.GetTroopRecruitmentCost` doesn't pass `buyerHero` to its cost service, so vanilla recruitment-cost perk discounts (HeadHunter etc.) are likely not applied — confirm whether intentional before any fix. Reinforces `evidence-over-claims.md` A.4: subagent code-audit claims about vanilla MUST be verified against the decompile before relaying. No code changed.

### docs(claude-md): register block-dangerous-git in the Hooks + Hook-Response-Contracts tables

Final CLAUDE.md row for the `block-dangerous-git` guard shipped in the prior commit — documents the hook in the "## Hooks" table and adds its "## Hook Response Contracts" entry (when it prompts, approve only if you intend to discard work). Closes the shared-config doc batch; the earlier series rows (evidence-over-claims, mark-verification-run, check-verification-evidence, /adopt-external + /security-scan skills & routing) were already committed.

### feat(hooks): block-dangerous-git — confirm before work-destroying git ops

Adopted the one genuine idea from mattpocock/skills (the rest skipped — duplicative/TS-specific/contrary): a PreToolUse(Bash) guard for git ops that permanently discard uncommitted/unpushed work, which TAOM didn't cover (validate-push.sh only guarded push/force-push). New `.claude/hooks/block-dangerous-git.sh` emits `permissionDecision:"ask"` (confirm, NOT hard-block — your choice) for `git reset --hard`, `clean -f[d]`, `branch -D`, `checkout`/`restore` worktree-discard, `stash drop|clear`. **Calibrated to TAOM, not blind-ported:** mattpocock's version hard-blocked ALL pushes with no override (would break our push workflow) — ours excludes push entirely (validate-push owns it), confirms rather than blocks, and fails open.

Detection is **segment-anchored** (split on `| ; && ||`; per segment strip env-prefixes + `git` + global flags; match the subcommand) so it does NOT false-fire on commands that merely mention a destructive phrase as data (`echo "git reset --hard"`, `git log | grep "…"`, `git commit -m "…"` all ALLOW). Robust JSON extraction via jq→python3 (handles escaped quotes), mirroring `check-claude-files-tracked.sh`.

Built through full ship discipline: **adversarially reviewed** (2-dim find→verify, 6 confirmed findings — 1 HIGH + 3 MED false-positives from substring-anywhere matching, all fixed by the segment-anchor rewrite; 1 MED parser-truncation fixed via python3; 1 LOW convention divergence resolved; 1 finding correctly rejected). **Re-tested 38/38** (asks on destructive incl. env/global-flag forms; allows push/checkout-b/branch-switch/reset--soft/restore--staged/data-mentions; fail-open on malformed). Registered in `settings.json` PreToolUse Bash. CLAUDE.md Hooks + Hook-Response-Contracts rows staged for the shared-config batch.

### docs(process): institutionalize 2 self-review lessons into the /adopt-external cycle

A self-review of the open-design verdict (reading the primary sources directly instead of trusting the subagent summaries) caught two process faults, now codified so the next iteration applies them automatically:
1. **Verify load-bearing security claims yourself before relaying a subagent's read.** A security subagent had reported open-design's telemetry "content off by default"; reading `apps/daemon/src/app-config.ts` directly showed `telemetry: { metrics: true, content: true }` — both default ON pre-disclosure. The wrong claim rode into the user-facing summary until an independent check caught it. Added as `evidence-over-claims.md` A.4 (relaying a subagent report = a claim to spot-verify, like an agent's "done"), reinforced in `external-repo-adoption.md` Phase 2 + the `/adopt-external` gotchas.
2. **Right-size the fan-out.** open-design's domain-orthogonality was decisive from the README alone, yet a 2–3 agent workflow was run to "confirm the obvious" (and still got the telemetry detail wrong). New principle in `external-repo-adoption.md` + `/adopt-external`: scout inline first; for clearly out-of-domain repos do a light inline pass and verify load-bearing claims yourself; reserve multi-agent fan-out for genuinely adjacent/large repos. Memory: `project_external_repo_adoption.md`.

### feat(map): distribute settlement ownership across clans for 8 more regions (Dale, Isengard, Dunland, Harad, Umbar, Khand, Dol Guldur, Gundabad)

Extends the Rhûn redistribution to 8 further kingdoms in the **live** TAOM_Map module — 74 town/castle fiefs re-owned (60 changed). Region→kingdom mapping resolved from the user-verified memory (the two in-repo prefix tables conflict), and **membership selected by owner-clan's kingdom**, not id-prefix/culture (a culture can span regions — e.g. Khand `K` settlements carry `Culture.khuzait` but are owned by `Kingdom.battania` clans; Harad/Khand correctly exclude adjacent shaghana/abanissa fiefs).

Data gathered by a discover→verify agent workflow (16 agents): per region, clans + tiers (vanilla `spclans.xml` + `spclans.xslt` overrides for XSLT cultures; `clans.xml` for custom cultures), town/castle ownership, prosperity, village counts — every value independently re-derived in a second pass.

Rules (same as Rhûn, plus a user-chosen conflict policy): towns → rank-≥4 clans (ruling clan keeps its lore capital); castles → rank-≤3 clans, spread. **Finding:** only Dale has enough towns for its rank-≥4 clans; the other 7 regions were authored with more high clans than towns, making the 3 rules mutually unsatisfiable. Per user decision (**"land every high clan"**, **all 8 regions**): high clans with no town left each take one castle as their seat, guaranteeing every rank-≥4 clan ends with ≥1 holding; remaining castles spread among low clans. Verified: XML well-formed, every town owned by a tier-≥4 clan, every tier-≥4 clan landed, ruling clan keeps its capital, no rule violations; BOM+CRLF preserved.

- New generalized tool **`tools/Assign-SettlementOwners.py`** (dry-run default, `--apply`; SPEC-driven rules engine + the same anchored-regex, exactly-once-assert, `.bak`, byte-round-trip apply path as `Assign-RhunSettlementOwners.py`).
- Notable per-region outcomes: clan_1 monopolies broken (Isengard/Dol Guldur/Gundabad were 100% clan_1); Dunland & Dol Guldur now seat *all* clans one-fief each; tiny-region trade-off accepted — **Isengard**'s 4 fiefs all go to its 4 high clans, leaving its 7 low clans landless (vanilla-style roaming nobles).
- Note (not changed): `castle_G1` (Gundabad) still has the placeholder display name "Castle G1" — flagged for a future rename pass like `castle_RU9`→Carndûr.

### feat(map): distribute Rhûn (RU) settlement ownership across the Khuzait/Rhûn clans

All 20 Rhûn-region holdings (8 towns + 12 castles, `RU` prefix) in the **live** TAOM_Map module were owned by the single ruling clan `Faction.clan_khuzait_1`, making the region a one-clan monopoly at campaign start. Redistributed by rank (clan `tier`; Rhûn == `Kingdom.khuzait`):

- **Towns → tier ≥ 4 clans** (only clans 1/2/3/6 qualify), 2 each. **Lest** (`town_RU2`) + **Mistrand** (`town_RU1`) stay with Clan 1 (Hûz) per request; the other 6 towns split between Salurian (clan_2), Nikathian (clan_3), Khundolar (clan_6). Every tier-≥4 clan ends with ≥1 holding.
- **Castles → tier ≤ 3 clans**, spread one-each across 12 distinct low clans (clans 4,5,7,8,9,10,11,12,13,14,15,17). Clans 16/18/19 left landless in RU (all tier 3 — acceptable; the rule only guarantees holdings for tier ≥ 4).
- **`castle_RU9` renamed** from placeholder "Castle RU9" → **"Carndûr"** (Carnen/Redwater corridor, south of the Iron Hills, north of the Sea of Rhûn) and given to **Mithruntai** (clan_17) as its sole holding.

New tool **`tools/Assign-RhunSettlementOwners.py`** (dry-run by default, `--apply` to write): byte-level UTF-8 round-trip (BOM + CRLF preserved), regex anchored on the unique Settlement `id`, asserts each id matches exactly once, writes a `.bak`. The rename reused **`tools/Apply-MapVillageNames.py`** (added one `castle_RU9` entry) to update the master + all 12 `loc_settlements.xml`. Villages need no edits — they `bound` to their parent and inherit the new owner. Verified: file still parses as XML, owner counts match the plan, clan_1 retains exactly the 2 named towns. **Only the external live `TAOM_Map/ModuleData/settlements.xml` was touched** (the repo shadow is stale/unregistered). No C# changed; applies to new campaigns.

### chore(items): remove clo_* cloth-sim overlay items from the game

Cloth-simulation mesh overlays (ids prefixed `clo_*`) had leaked into the item pool and were appearing as purchasable shop items (e.g. "[Dale] Cloth Archer Armour A01..A04"). They are duplicates of real armor's cloth-sim mesh and were never meant to be standalone, equippable items. `CultureMarketplace` auto-derives town pools from every culture-tagged `ItemObject` with no type filter, so any `clo_*` item with `culture="Culture.sturgia"` got injected into Dale markets daily — removing the item definitions removes them from `MBObjectManager` and stops the injection.

- **Deleted 11 standalone `clo_*` items** (external `LOTRLOME_Armory`, not git-tracked): 9 from `dale/body_armors.xml` (`clo_sk_dale_chest_archer_a01`..`a04`, `_chivalry_a01`/`a02`, `_infrantry_a01`/`a02`, `_lake_town_chest_mariner_a01`), `clo_urukscout_body` from `isengard/body_armors.xml`, `clo_urukscout_helmet` from `isengard/head_armors.xml`. Left `m_northern_cape_a` untouched (it uses `mesh="clo_…"` — a mesh attribute, not a `clo_*` item id).
- **Fixed 6 player-career Body refs** in `taom_career_starting_equipment.xml`: `clo_sk_dale_chest_archer_a01` → real twin `sk_dale_chest_archer_a01` (`player_career_sturgia_{ranged,cavalry,infantry}_{m,f}`) — prevents the underwear bug on a Dale career start.
- **De-clo'd 3 generators** so a future `--apply` can't resurrect the items: `generate_dale_armor.py` now skips `clo_`/overlay meshes from the manifest; `generate_isengard_armor.py` dropped its `clo_urukscout_*` body/head lists; `apply_isengard_troop_revamp.py` swaps its `clo_urukscout_*` equip refs to the real `urukscout_*` twins.

No troops or heroes equipped any `clo_*` item, so deletion is safe. No C# changed.

### feat(tooling): adopt affaan-m/ECC concepts — config security scanner + /adopt-external workflow skill

Second external-repo review (after obra/superpowers), following the same security-vet → tier → port → adversarial-review → RCA cycle. **Security verdict on ECC:** safe to learn from; installers/hooks are local-only with no install-time code-exec (postinstall is an echo); the sole external vector is an opt-in, closed-source `insa_its` SDK (disabled by default) — moot since we port text, never install. ~88% of ECC (12 language ecosystems, web/enterprise/media) is irrelevant or duplicative for a C#/.NET mod; only the "audit your own agent config" idea was genuinely additive.

Adopted (Tier 1 + the marginal Tier 2), all TAOM-owned:
- **`tools/audit_claude_config.py`** — deterministic, offline, stdlib-only config security scanner (a TAOM-calibrated subset of ECC's AgentShield). 5 categories: secrets, permissions, hook-exfil, mcp-risk, prompt-injection. Calibrated to TAOM — notably does NOT flag mandated fail-open hooks (`|| true`/`2>/dev/null`/`exit 0`), which upstream does. Placeholder-suppressed, secret-masked, `audit-allow:` inline suppression, exit 2 on CRITICAL (CI gate). **First real catch:** `.mcp.json`'s `filesystem` server uses unpinned `npx -y` (MED — consider pinning `@version`).
- **`/security-scan`** skill + a pointer from `/skill-stocktake`.
- **`/adopt-external`** skill + **`docs/ai-includes/external-repo-adoption.md`** — captures the whole review-and-adopt cycle so future sessions run it consistently.
- **Discoverability** (so future sessions/agents auto-run it): CLAUDE.md "Skill Routing → Strong proactive-invoke" gained a row (share a repo / "next repo" / a GitHub URL → invoke `/adopt-external`) + Skills-table rows; `agent-operating-manual.md` gained an "Adoption/security" recommend-line for subagents; memory `project_external_repo_adoption.md`. (CLAUDE.md edits are staged for the shared-config batch — they take effect locally immediately.)
- Tier 2: a `[HEURISTIC]`-tagged "Token-optimization knobs" note in `/context-budget`.

**Tested:** scanner runs clean on the real tree (1 MED, exit 0), fires 4 CRITICAL + 8 HIGH on a planted fixture (exit 2), `audit-allow` suppression works, and `_FETCH_EXEC` passes a 6/6 pipe-vs-semicolon test. **Adversarially reviewed** (3-dim find→verify, 32 findings: 20 confirmed / 12 rejected — the 12 rejected being the discipline working: reviewers' own "no fix needed" affirmations killed at verify). 4 real findings fixed pre-commit. **RCA** `docs/reviews/rca-ecc-adoption-2026-05-29.md`: the `/adopt-external`-duplicates-its-doc finding was a REPEAT of superpowers RC2 (amplification) — prevention existed (`external-skill-ports.md` "don't amplify"), the gap was not applying it to my own fresh skill; and a "mandate" was cited that harness-facts.md didn't state (now formalized: a "TAOM hooks MUST fail open" row). CLAUDE.md skill/routing rows left for the shared-config batch.

### fix(career): party-size passive applied flat, not as a ×N factor

User-reported: Gondor infantry career (Captain of Osgiliath) "+2 party size" choice granted ~+150. Root cause: `TaomPartySizeModel` applied the career `PartySize` passive via `ICareerPassiveService.ApplyFactor` (`ExplainedNumber.AddFactor` — multiplicative), but PartySize magnitudes are authored as whole counts (2–6, per "+N party size" descriptions). `AddFactor(2)` = ×(1+2) = triple the base (~75 → ~225). Switched to `ApplyFlat` (`result.Add`) so +2 means flat +2. The change is type-isolated — the other 9 campaign-layer passive types keep `ApplyFactor` (their magnitudes are fractional). Fixes every direct-schema flat PartySize entry across all careers, not just Gondor. Regression test contrasts flat (+2 → 102) vs factor (×3 → 300). See [RCA](docs/reviews/rca-career-partysize-2026-05-29.md).

Not-tested: GameModel override invocation (entry point — verified in-game per ADR-008).

### fix(career): activate 310 dead wrapped-schema career passives

`CareerConfigProvider.ParseChoice` read only a direct `<PassiveEffect>` child and only the `magnitude=` attribute, so 310 choices authored in the `<PassiveEffects>` (plural) wrapper with a `value=` attribute were **completely unparsed** — whole careers across all 16 cultures had dead passives. The parser now falls back to the wrapper (first child; all wrappers are single-child — verified) and accepts `value=` as an alias for `magnitude=` (`magnitude=` wins when both present). Reconciled the 20 wrapped PartySize entries (`value="0.10/0.12/0.14"`, descriptions mixing flat + percentage) to flat counts 4/5/6 in `taom_career_choices.xml` + `taom_career_strings.xml`, and propagated the number change to all 12 language files + 11 translation caches via `tools/fix_wrapped_partysize_translations.py` (numeral-only, BOM-faithful; handles Turkish `+%N`). 5 new parser/service tests. Deep-review (6 agents) confirmed no ×N regression from activation.

The 13 reworded descriptions were re-translated into the 11 AI languages via the Claude API translation tool (scoped to exactly those keys — a broad run would have swept ~1000 unrelated untranslated entries per language); PL (community-hand-translated) keeps the English-source wording. Known limitation: 5 `PassiveEffectType` values (`Ammo`, `HorseChargeDamage`, `HorseHealth`, `TroopResistance`, `StealthBonus`) have no consumer — pre-existing; choices advertising them are no-ops pending per-type consumer implementation. Deferred: make the passive cache `IsPercentage`-aware so flat-vs-factor is data-driven.

### feat(hooks): auto-enforce verification-before-completion + bake review disciplines into the workflow

Made the adopted superpowers disciplines fire **automatically** rather than relying on the agent to remember the rules. Two new hooks + four skill edits:

- **`mark-verification-run.sh`** (PostToolUse Bash) — touches `.claude/logs/.verification-ran` (gitignored) whenever a `dotnet build`/`dotnet test`/`build.ps1` command runs. jq-optional (precise via `jq` when present, raw-payload fallback otherwise; a false match only suppresses a soft reminder, never adds one).
- **`check-verification-evidence.sh`** (Stop) — reminds (soft, stderr, non-blocking) when a dirty `.cs` file is **newer than** the marker, i.e. C# source was edited after the last verification. `[[ file -nt marker ]]` handles the never-built case for free. Mirrors `check-deep-review.sh` conventions (git state, no stdin, always `exit 0`). Doc/rule/XML-only sessions never match the `*.cs` case → stay silent. Enforces the "verification before done" half of `.claude/rules/evidence-over-claims.md`.
- Registered both in `settings.json`; added rows to CLAUDE.md "## Hooks" + a "## Hook Response Contracts" row (run the build it names; if the build genuinely can't run, say so per `environment-failures.md` — don't dismiss it).
- **Baked the review disciplines into the skills that auto-implement findings:** `review-codex` §3b and `deep-review` §3e now state a finding is a hypothesis requiring source-verification before implementation (per `evidence-over-claims.md`); `deep-review` Step 2 + `ship` Phase 1 now carry the spec-compliance-before-code-quality triage ordering (per `agent-teams.md`); `skill-stocktake` gained an "Authoring discipline" checklist section (per `external-skill-ports.md`).

**Adversarially reviewed** (3-dimension find→verify workflow, 17 findings: 10 confirmed / 7 rejected — the verify stage rejecting 7 not-real findings IS the evidence-over-claims discipline dogfooding itself). Confirmed defects fixed: (MED) the Stop hook re-nagged on every Stop while `.cs` stayed dirty — added session muting (`.verification-reminded` marker, set on first reminder, cleared the moment there's nothing left to verify, so a build OR a revert re-arms it); (MED) trimmed redundant `ship` triage prose that merely duplicated `deep-review`; (LOW) aligned `mark-verification-run.sh` I/O preamble to its siblings + documented the jq-optional fallback + staged-deletion `-f` guard. The other 8 confirmed findings verified as already-correct (deleted-file handling, missing-marker `-nt`, non-build filtering, concurrency).

**Tested** in an isolated temp git repo — 12/12 scenarios pass: first-remind → mute-on-repeat → build → silent + re-arm → remind-again, plus doc-only silent, non-build no-marker, staged-deletion no-trigger, and fail-open (exit 0) on malformed input. Marker gitignored (`.gitignore:70 .claude/logs/`); hooks NOT gitignored; `core.filemode=false` so `100644` matches sibling hooks.

**RCA** (`docs/reviews/rca-superpowers-enforcement-2026-05-29.md`): the muting + I/O-preamble misses share one root cause — a new hook mirrored its sibling's *detection* convention but not its *full* convention set (muting / idempotency / I/O preamble / exit). Prevention institutionalized in always-load rules so it can't recur: `harness-facts.md` gained an "Authoring a new hook — mirror the sibling's FULL convention set" checklist; `external-skill-ports.md` gained a "Don't amplify a rule — point to it" note (the skill-prose enforcement-theater finding); memory `feedback_hook_authoring_mirror_siblings.md`.

### docs(process): adopt 4 disciplines from obra/superpowers (reviewed + security-vetted)

Critical review of the external `obra/superpowers` skills framework (Jesse Vincent, MIT, ~93k stars) to find practices TAOM can absorb. **Security pass first** (per request): pulled every runnable file verbatim — `hooks/session-start`, `run-hook.cmd`, `hooks.json`, `package.json`, the OpenCode plugin, maintainer scripts. All clean: no network calls, no `eval`/base64, no credential access, and **no npm install/postinstall script** (the #1 supply-chain vector is absent). One caveat documented: the framework is prompt-injection-by-design (every `SKILL.md` body becomes high-authority injected context) — so we **deliberately did NOT install the plugin**; we ported reviewed *text* into TAOM-owned files instead (no auto-update, no context tax, goes through our own checklist). The page summarizer also hallucinated "212k stars" (real ~93k) — a live instance of our "verify before reference" rule.

Headline finding: TAOM had already independently re-invented most of superpowers (often more maturely — `/investigate` vs their systematic-debugging, `/verify` + `/deep-review` + `/review-codex`, the worktree + gitignore rules already in `harness-facts.md`). Only ~4 genuine gaps were worth importing; the rest is documented as explicitly-not-adopted in the plan. Placed to **minimize always-load context cost** (net: one new always-load rule):

1. **New always-load rule [`.claude/rules/evidence-over-claims.md`](.claude/rules/evidence-over-claims.md)** — combines their `receiving-code-review` + `verification-before-completion` into one "evidence over performance" discipline: verify a review finding against the codebase before implementing it (grounded in `feedback_audit_findings_not_always_correct.md` ~95%-not-100% rate); no performative agreement ("You're absolutely right!"); no "done" claim without fresh verification output (subagent self-reports don't count). Directly hardens the auto-implementing `/review-codex` loop.
2. **Extended [`.claude/rules/external-skill-ports.md`](.claude/rules/external-skill-ports.md)** (path-scoped, zero always-load cost) — added a from-scratch skill-authoring section (description = *when-to-use* not *what-it-does*; active-voice naming; flowcharts only for non-obvious decisions; anti-patterns) + the **rationalization-bulletproofing technique** (RED-GREEN-REFACTOR a discipline rule: run the scenario without it, capture the agent's excuses, write explicit counters). Flags the CRITICAL divergence: superpowers allows 1024-char descriptions; TAOM keeps its ≤30-word cap (eager-load economy) and adopts only the framing.
3. **Extended [`.claude/rules/think-before-coding.md`](.claude/rules/think-before-coding.md)** (existing always-load) — a *lightweight* design pass for open-ended work (ask one question at a time via `AskUserQuestion`, propose 2-3 approaches with a recommendation). Deliberately dropped superpowers' heavyweight per-feature design-doc-commit gate; preserved the existing "don't over-question" guard.
4. **Extended [`docs/ai-includes/agent-teams.md`](docs/ai-includes/agent-teams.md)** (on-demand doc) — two-stage subagent review ordering (spec compliance BEFORE code quality), give subagents complete context, fix→re-review→repeat. Composes with the `Workflow` tool.

CLAUDE.md Scoped Rules table updated (new rule registered, two rows refreshed) + a one-line pointer to the review-ordering section from "Briefing subagents." No C# / build impact (docs + rules only). Plan + full security writeup archived at `~/.claude/plans/review-this-repo-do-wondrous-eich.md`.

## 2026-05-28

### chore(tools): harden scene/bandit Python tools — byte-faithful BOM I/O

`/deep-review` over the uncommitted changeset (3 targeted agents — data-flow, tooling-correctness, docs-consistency — since there's no new C#). Data-flow + docs passed clean (8 flows traced, 0 gaps; all links/frontmatter valid). Tooling agent found a BOM-handling inconsistency across the new script family: `migrate_hideouts_to_lotr.py` read/wrote plain `utf-8`, and 3 siblings wrote the BOM via a U+FEFF source literal. **No live corruption** (verified: repo XML correctly BOM-less, external `settlements.xml` BOM retained, all parse) — but fragile/inconsistent. Standardized all 6 scripts to byte-level BOM-preserving I/O (`read_bytes` detect → `utf-8-sig` decode → `write_bytes` with explicit `b"\xef\xbb\xbf"` prefix); fixed a LOW case-sensitivity mislabel in `audit_scene_names.py`. Convention documented in `tools/README.md`; RCA at [`docs/reviews/rca-scene-tooling-2026-05-28.md`](docs/reviews/rca-scene-tooling-2026-05-28.md) (root cause: multi-script family grown across turns without a shared I/O convention → drift; the C#-core deep-review agents don't review Python tooling, so a dedicated tooling agent was needed). Verified post-fix: all 6 scripts syntax-OK + idempotent no-op re-runs.

**Prevention institutionalized** so this class can't recur silently: (1) [`.claude/skills/deep-review/SKILL.md`](.claude/skills/deep-review/SKILL.md) Step 2c now launches a **Tooling Correctness agent** whenever the changeset includes data-mutating `tools/**/*.py` or `*.ps1` (checks BOM/encoding preservation, idempotency, dry-run gating, backup-before-write, regex over-match, case-insensitivity); (2) the byte-faithful I/O pattern is the documented standard in `tools/README.md`; (3) memory `feedback_xml_tool_bom_io_convention.md` for cross-session recall.

### fix(scenes): field-battle crash — sp_battle_scenes.xml referenced a non-existent battle terrain

TAOM's `sp_battle_scenes.xml` (battle-terrain → map-cell mapping) defined a catch-all `<Scene id="battle_terrain_extended">` for map indices 158–255, but **no `battle_terrain_extended` scene exists on disk** — so any field battle on a map cell in that index range crashed. This is the "fighting battles near specific places crashes" report. Repointed to `battle_terrain_r` (a real Plain battle scene present on disk, not already used by TAOM, terrain matches the `Plain` of the broken entry). Vanilla never reuses a Scene id, so a fresh valid id was used rather than duplicating `battle_terrain_a`. Audit tool: `tools/audit_battle_scenes.py` (cross-references Scene ids vs on-disk SceneObj + checks 0–255 index coverage). After the fix: 0 crash suspects, all 256 indices covered. Fixed in repo source + deployed `Modules/TAOM/ModuleData/` (`.bak_scenes` backup).

Institutionalized the "compare against vanilla before relying on mirrored data" discipline so future sessions catch this class: new scoped rule [`.claude/rules/vanilla-data-comparison.md`](.claude/rules/vanilla-data-comparison.md) (auto-loads when settlements/sp_battle_scenes/spcultures/spclans/spkingdoms/xslt are edited), reference how-to [`docs/reference/scene-reference-audit.md`](docs/reference/scene-reference-audit.md), a post-version-bump scene-audit step in [`docs/migration/v1.4.x-changes.md`](docs/migration/v1.4.x-changes.md), CLAUDE.md Doc Lookup + Scoped Rules rows, and two memories (scene-specific + the general umbrella principle). The 3 audit/remap tools are reusable for every future bump.

Hideout-scene check (same audit): all 99 vanilla-derived hideout scenes present (`bandit_forest_sv`, `desert_hideout_002/004_sv`, `hideout_steppe_001/002_sv`, `mountain_hideout_002/004_sv`, `sea_bandit_a-d_sv`). The 30 new `hideout_gondor/erebor/mirkwood_*` referenced scenes not yet exported to `SceneObj/` (WIP in editor) — **interim-repointed to vanilla hideout scenes** to prevent raid crashes: gondor/mirkwood → `forest_hideout_004_sv`, erebor → `mountain_hideout_002_sv` (`.bak_hideoutscenes` backup). Revert each `scene_name` back to its settlement id once the custom scenes are exported to `SceneObj/`. After this, the full scene audit reports 0 missing settlement scene refs.

### fix(scenes): repoint 61 stale settlement scene_name refs (v1.4.5 rename + missing custom)

Battles/visits near specific settlements were crashing. Audit (`tools/audit_scene_names.py`, cross-references all settlement `scene_name` refs vs on-disk `SceneObj/` folders, case-insensitive) found 9 distinct dangling scene refs in the live `TAOM_Map/settlements.xml` — 61 occurrences. Two root causes:

1. **v1.4.5 renamed the house-interior scenes.** Towns' `house_1/2/3` locations still pointed at retired names (the *tavern* locations had already been updated). Repointed to the v1.4.5 equivalents: `battania_house_a_interior_house`→`battania_town_house_b_interior_b_house`, `khuzait_house_a_interior_house`→`khuzait_house_c_interior_a_house`, `sturgia_house_a_interior_house`→`sturgia_town_house_d1_interior_b_house`, `vlandia_house_d_interior_house`→`vlandia_city_house_a_interior_house`. (57 occurrences across town_L1/M1/RU1/RU2/E1/E2/V4.)
2. **Custom scenes missing/misspelled.** East Osgiliath (town_EW3) referenced `lotraom_e_osgiliath_i_forceatmo`; the real custom scene on disk is `lotrtaom_e_osgiliath_i_forceatmo` (typo) — repointed. Three Isengard settlements (`castle_orthanc_gate`, `castle_village_isengard_a`, `village_isengard_a`) referenced custom scenes that don't exist on disk — repointed to rugged vanilla scenes of matching type (`battania_castle_a` / `battania_village_c` / `battania_village_e`) to stop the crashes; rebuild proper Isengard scenes later for the LOTR look.

Tools added: `tools/audit_scene_names.py` (scene-ref vs on-disk audit, classifies missing-everywhere vs WIP-in-SceneEditData) + `tools/remap_stale_scene_names.py` (verified remap, every replacement confirmed present on disk before write). Full audit: [`docs/reviews/scene-name-audit-2026-05-28.txt`](docs/reviews/scene-name-audit-2026-05-28.txt). `.bak_scenes` backup of the external file saved.

Note: `HART_isengard` was a false alarm — folder is `HART_ISENGARD` (Windows case-insensitive resolves it). The stale repo shadow `Main/_Module/ModuleData/settlements.xml` still has these refs but isn't loaded (not registered in SubModule.xml); left as-is.

### feat(bandit-management): wave-2 bandit cultures + hideout expansion

Three new LOTR bandit cultures (heroic-faction offshoots), each now LIVE with culture + party templates + clan + 10 hideouts. Plus expansion of two existing hideout ranges.

New bandit cultures in `taom_spcultures.xml` (`is_bandit` + `can_have_settlement`), clans in `characters/clans.xml` (banner = parent-kingdom heraldry):
- `gondor_soldiers` — **Gondor Soldiers** (Anórien troops; banner `empire_w`)
- `erebor_warriors` — **Erebor Warriors** (regular dwarven line; banner `erebor`)
- `mirkwood_stalkers` — **Mirkwood Stalkers** (Silvan militia line; banner `mirkwood`; Mirkwood's tree jumps L16→L36 so the Stalkers top out at veteran militia)

Each ships a raider + boss party template (6 total), ~16 loc keys (name + male/female), a bandit clan (`is_bandit`/`is_outlaw`, `initial_home_settlement` → `hideout_<culture>_1`), and 10 hideouts (`hideout_gondor_1`–`10`, `hideout_erebor_1`–`10`, `hideout_mirkwood_1`–`10`) in the external TAOM_Map module. Hideout `scene_name` = the matching scene id authored in the map editor; placeholder positions repositioned in-editor.

**Authoring order (crash-safety):** the cultures were authored INERT first (no clan → not iterated in `Clan.BanditFactions` → never hits the `_hideouts[culture]` hard indexer). The clans were authored only AFTER the 10 hideouts of each culture existed, so `_hideouts[culture]` is populated when `GetInfestedHideoutCount` runs — no new-game KNFE.

Hideout expansion (existing cultures with existing clans, functional immediately) via new idempotent `tools/add_bandit_hideouts.py`:
- `hideout_desert_30`–`43` (14) → `harad_raiders` (Haradrim Raider's Camp)
- `hideout_seaside_30`–`45` (16) → `umbar_corsairs` (Corsair's Cove) — `seaside_46` removed (miscount)

Verification: `[xml]` parse on all touched files; per-culture clan + culture + raider-template resolve 1:1; 10 hideouts each resolve to their defined culture; `seaside_46` gone; no lingering pre-rename ids. `.bak2`/`.bak3` backups saved on the external TAOM_Map files.

### fix(bandit-management): strip homeless vanilla bandit clans + kingdom banners

Two follow-ups to the shipped feature (commit `ae39aae`):

1. **New-game crash fix (HIGH).** After the hideout migration repointed all 99 hideouts to the 5 new LOTR bandit cultures, the 5 vanilla bandit clans (`sea_raiders`, `mountain_bandits`, `forest_bandits`, `desert_bandits`, `steppe_bandits`) had no hideouts of their cultures left in the world. Vanilla `BanditSpawnCampaignBehavior.SpawnBanditsAroundHideoutAtNewGame` iterates `Clan.BanditFactions` and indexes `_hideouts[clan.Culture]` with a hard dictionary indexer → `KeyNotFoundException` → new-game crash. Fixed by stripping the 5 vanilla bandit clans via empty-template rules in `spclans.xslt` (`<xsl:template match="Faction[@id='X']"/>`), dropping them from `Clan.BanditFactions`. The vanilla `looters` clan is KEPT — its StringId is hardcoded in `DefaultBanditDensityModel.GetMaxSupportedNumberOfLootersForClan` and it spawns around villages on a separate code path independent of the hideout dictionary. (The new LOTR bandit clans live in the additive `characters/clans.xml`; vanilla clans can only be removed via XSLT transform of the vanilla `spclans.xml`, hence the split.)

2. **Kingdom banners.** The 5 LOTR bandit clans in `characters/clans.xml` shipped with generic vanilla bandit greyscale banner keys. Swapped each clan's `banner_key` to its parent kingdom's heraldry so raiders read as offshoots of their lore faction: `dunland_raiders` → Dunland (`empire`), `rhun_raiders` → Rhûn (`khuzait`), `harad_raiders` → Harad (`aserai`), `gundabad_raiders` → Gundabad, `umbar_corsairs` → Umbar. Keys sourced from `spkingdoms.xslt` (empire/khuzait/aserai) + `taom_spkingdoms.xml` (gundabad/umbar). The clan `banner_key` drives the owner banner once a bandit party of the culture is resident; **additionally** `faction_banner_key` was added to each bandit `<Culture>` (same kingdom key, via `tools/add_bandit_faction_banner_keys.py`) so the hideout map UI renders the parent-kingdom heraldry at culture level too — vanilla bandit cultures omit it, but the settled cultures all carry it and the nameplate falls back to it. (Earlier note that `faction_banner_key` "isn't needed" was wrong — in-game testing showed the hideout banner blank without it.)

Both data-only. XML re-validated via `[xml]` parse on `spclans.xslt` + `clans.xml`.

### chore(troops): delete urukhai_hunter + urukhai_chosenmarksman

- Removed both troops from `troops_isengard.xml` (51 → 49 NPCs). They were orphaned out of the Isengard upgrade graph by the bow-line restructure (c07ed72); the bow line now runs Scout → Tracker → Archer with no Hunter/Chosen-Marksman.
- Re-pointed the 2 dangling `urukhai_hunter` party-template stacks (`kingdom_hero_party_isengard_template`, `kingdom_hero_party_outlaw_isengard_template`) to `urukhai_scout` — keeps a bow uruk in those AI parties.
- Save-compat: deleting troop IDs is not save-safe; existing saves that already recruited these two troops will drop them on load. Accepted per explicit request.

### feat(recruitment): Dunland culture + clan pools, Erebor Iron Hills mix

Two `VolunteerRecruitmentService` pool changes.

**Dunland (`Culture.empire`)** — previously had no pool (fell through to null). Added:
- Culture pool ("them all"): all 4 Dunland `is_basic_troop="true"` troops at weight 1 — Peasant + the three totem noble sons (Wolf/Blaidd `dunland_noble_son`, Boar/Turch `dunland_boar_noble_son`, Raven/Cigfran `dunland_raven_noble_son`).
- Clan pools (specific troops by name): the 3 totem clans with a matching noble son recruit Peasant + their own noble son (`clan_empire_north_1` Wolf, `_2` Boar, `_5` Raven, weight 1 each). The 6 totem clans without a named noble son (Uch/Ox, Arth/Bear, Hebog/Hawk, Draig/Dragon, Caru/Stag, Avanc — `_3/_4/_6/_7/_8/_9`) recruit the full roster (Peasant + all 3 noble sons).
- `SettlementMap` deliberately left empty for Dunland (per spec).

**Erebor** — clans + settlements now field a **mix of Erebor + Iron Hills** troops (was Erebor-only). All 13 Erebor settlement pools (`town_E1-4`, `castle_E1-9`) and 7 clan pools (`clan_erebor_1-7`) now use a shared `EreborMix`: `erebor_reg_miner` 5, `erebor_noble` 3, `iron_hills_reg_recruit` 2, `iron_hills_noble` 2 (same blend as the existing culture fallback). Settlements were updated too because `SettlementMap` is checked before `ClanMap` — a clan-only change would never surface in the mapped Erebor towns/castles.

Tests: 5 new Dunland (culture low/high-roll, Wolf + Boar clan signature, non-totem full roster) + 2 new Erebor-mix (town roll→IH recruit, clan roll→IH noble); 1 existing Erebor settlement test updated for the new total weight (8→12). Full suite 2624 passed / 0 failed / 2 skipped.

Save-compat: additive recruitment-pool changes only; no troop/clan/settlement data touched.

### feat(isengard): fold Scout sub-tree into main tree (bow + 2H lines) + recruitment pool

Dismantled the standalone Uruk-Hai Scout sub-tree and folded its troops into the main Recruit→Fighter→Warrior progression, per the in-game tree review.

**Bow line under Fighter** — `urukhai_fighter` now upgrades to Scout (alongside Warrior + Skirmisher). Scout (L6→L16) → Tracker (L16→L21) → Archer (L21→L26), cloning the Skirmisher/Arbalest/Crossbowman gear + skills but with the Isengard bow (`wm_isengard_bow_a01`/`a02`) + arrows (`wm_isengard_arrow_a01`) instead of crossbow + bolts. Bow primary at each tier (85/130/170).

**2H line under Warrior** — `urukhai_warrior` now upgrades to Feller (alongside Spearman + Swordman). Feller (L11→L21) → Slayer (L16→L26) → Reaver (L21→L31), cloning the Swordman/Infantry/White-Hand gear + skills but with Isengard 2H axes (`isengard_2h_axe_a`/`b`/`c`, no shield); OneHanded↔TwoHanded swapped so TwoHanded is primary.

**Orphaned** (save-compat — kept in file, dropped from upgrade graph): `urukhai_hunter` (L11) + `urukhai_chosenmarksman` (L26), the old bow line's middle + top. Still valid troops for existing saves + party templates; just unreachable via upgrade.

**Recruit trim** — `urukhai_recruit` (L6) loses its helmet (`sk_uruk_hai_helmet_sword_light_a1`) + shoulder/pauldron (`sk_uruk_hai_pauldron_light_a1`); a level-6 levy doesn't need them. Keeps sword + shield + body + gloves + boots.

**Recruitment pool (new)** — Isengard had none (hostile-faction only; `GetVolunteerTroopId` returned null). New `InitializeIsengardCulture()` adds a culture-level pool (total weight 10): Recruit 4, Skirmisher 2, Orc Warg-Rider Scout 2, Warrior 1, Scout 1. Applies to all Isengard settlements. 3 unit tests (low/mid/high roll).

Verification: `validate_all_troop_refs.py` PASS (all `sk_uruk_hai_*` / `urukscout_*` armor + bow/axe weapon IDs resolve); tree-shape check confirms fighter→{warrior,skirmisher,scout}, warrior→{spearman,swordman,feller}, bow line scout→tracker→archer terminal, 2H line feller→slayer→reaver terminal, hunter+chosenmarksman orphaned; 14/14 Volunteer tests pass (3 new Isengard). C# compiles (deploy step blocked by locked `0Harmony.dll` — environment, Bannerlord open).

Save-compat: all troop IDs preserved (re-tier + re-equip only). Party templates reference the moved troops by ID — still resolve; tier shifts nudge AI composition but don't break. Recruitment pool is additive.

### feat(cultural-feats): terrain-based cultural movement-speed bonuses

Each LOTR culture now moves faster on its "home" terrain, deepening cultural identity on the campaign map. 18 new `FeatObject` terrain-speed feats (total 59 → 77) applied via the existing `TaomPartySpeedModel`/`CulturalFeats` pipeline.

**Bonus matrix:** Forest → Mirkwood/Lothlorien/Rivendell (+10%); Snow → Erebor/Gundabad (+10%); Steppe → Khand/Rhûn (+10%); Desert → Umbar/Harad/Shaghâna/Âbanissa (+10%); Plain → Gondor/Rohan/Dale/Dunland/Isengard (+10%); Swamp → Isengard (+10%); Mordor → plain/swamp **+5%** (deliberately smaller) + **night +10%** (terrain-independent, `Campaign.Current.IsNight`). All bonuses are flat `ExplainedNumber.AddFactor` that stack on vanilla's terrain modifiers (forest -30%, desert -10%, night -25%).

**Elf rework (balance change):** Mirkwood/Lothlorien forest feats changed from scaled penalty-reduction (~+18% / +15% net) to a uniform flat **+10%**, and Rivendell gains a matching forest feat — so all three elven cultures share one consistent forest bonus.

**Architecture:** new TAOM-owned `TerrainKind` enum keeps the service free of TaleWorlds types (ADR-007); `TaomPartySpeedModel.MapTerrain` converts `TerrainType` → `TerrainKind` at the boundary (`Dune` folds into `Desert`). The old `ICulturalFeatsService.ApplyForestSpeedFeats` (scaled) is replaced by `ApplyTerrainSpeedFeats(culture, terrain, isNight, ref result)` — a `switch` over terrain + `ApplyIfHas` helper, hot-path-safe (no allocation). Feat membership: `taom_spcultures.xml` `<cultural_feats>` for the custom cultures; `spcultures.xslt` for the six vanilla-wrapped cultures — Dunland/Rohan appended to their inline override blocks, Harad/Rhûn/Dale/Khand via new `Culture[@id='X']/cultural_feats` templates that copy vanilla feats and append the TAOM feat (vanilla bonuses preserved, verified by transform test).

**Localization:** feat names/descriptions use inline `{=key}default` defaults — matching the existing 59-feat convention (zero `taom_feat_` keys are registered in `taom_module_strings.xml`). Not run through the translation pipeline, to avoid a half-translated feat set; revisit only if all 77 feats are registered+translated uniformly.

**Snow is terrain-based by design:** the snow bonus keys off `TerrainType.Snow` *terrain* (consistent with forest/desert/etc.), not snowy *weather*. The `TAOM_Map` navmesh faces around the snowy regions are author-painted with terrain id `3` (= `TerrainType.Snow`), so `GetFaceTerrainType` returns `Snow` there and the Erebor/Gundabad bonus fires — the terrain-only check is intentional and must not be switched to weather detection (vanilla derives snow from weather, but the TAOM map paints it as terrain).

Tests: `CulturalFeatsServiceTests` (terrain dispatch per terrain, Mordor 5% vs 10%, night-only, null/wrong-terrain/None no-ops, plain+night stack) + `TaomCulturalFeatsDefinitionTests` (counts 59 → 77, per-culture distribution, new DataRows). Full suite 2624 passed / 0 failed / 2 skipped.

**Review (`/deep-review` + `/review-codex`):** Codex found 1 HIGH + 3 MED + 1 LOW. Fixed 3: (a) Mordor night feat was applied at sea where vanilla has no night penalty to offset → now gated `isNight && !IsCurrentlyAtSea`; (b) feat-culture was resolved via `Owner.Culture` only → now mirrors vanilla `PartyBaseHelper.HasFeat` precedence (leader → party → owner → settlement) via a `ResolvePartyCulture` boundary helper, so garrison/militia/caravan and leader-driven parties get the right terrain feats; (c) an orphaned XML-doc summary. Declined 2 with recorded reasons: the HIGH snow-weather finding (terrain-only is intentional — see above) and the per-recalc adapter allocation (pre-existing; speed recalc is cached, not per-frame). RCA: [`docs/reviews/rca-cultural-feats-terrain-2026-05-28.md`](docs/reviews/rca-cultural-feats-terrain-2026-05-28.md).

Save-compat: safe — FeatObjects are init-time campaign objects, no new serialized save fields.

Files: `Main/Features/CulturalFeats/{TerrainKind.cs (new), TaomCulturalFeats.cs, ICulturalFeatsService.cs, CulturalFeatsService.cs, Models/TaomPartySpeedModel.cs}`, `Main/_Module/ModuleData/{taom_spcultures.xml, spcultures.xslt}`, both test files, `docs/features/cultural-feats.md`.

### feat(skills): add /finish-branch — trunk-integration workflow capture

Captures the branch-integration workflow we ran by hand twice this session (the knowledge-base merges + the may27a lint-cleanup merge) as a skill, per the "Workflow → Skill" convention. Passes the three-part filter: repeatable (every merge), multi-step (FF-check → merge → backlinks → CHANGELOG → delete branch local+remote → push), and carries TAOM-specific gotchas (FF-vs-real-merge detection, don't sweep parallel-work backlink drift into the merge commit, `git branch -d` as a merge gate, push-to-becoming-master confirmation).

Distinct from `/ship` (the pre-merge review gate this follows) and the plugin `git-workflow:finish` (Git Flow + version tags; TAOM is trunk-based on `bannerlord-1.4.5` with ephemeral `taom-*` branches, no tagging).

`/review-trends` (analyze_reviews → progress.png) and the rebalancing CLIs were evaluated against the same filter and **deliberately not** skill-ified — single-command / already-documented operations would just tax eager context. `/skill-stocktake` will flag them if they start recurring.

Files: `.claude/skills/finish-branch/SKILL.md` (new), CLAUDE.md skill table row.

### feat(agents): subagent execution-model awareness — operating manual, de-staled agents, spawn-prompt convention

Made every execution context (sessions, custom agents, ad-hoc subagents) understand how/when to use skills and tools. Motivated by an audit that found subagents (a) running on stale knowledge and (b) instructed to do impossible things.

**The constraint** (confirmed via the Claude Code docs): a subagent runs in its own context with a **strict tool allowlist** and is NOT guaranteed to inherit CLAUDE.md / `.claude/rules` / skill descriptions. None of the 5 custom agents have the `Skill`/`Task` tool, so **agents cannot invoke skills** — they must recommend them to the orchestrator.

**New shared reference:** `docs/ai-includes/agent-operating-manual.md` — execution model (allowlist, can't-invoke-skills → recommend, report-don't-self-heal, retry budget), a tool catalog (taom-src primary, build/test, binding gate, validators, snapshot) with exact commands, a skill catalog grouped by purpose (to recommend, not invoke), and where the convention docs live.

**Agent fixes (short inline "Execution model" block + de-stale):**
- `feature-builder` — removed dead "invoke `/freeze` / `/build-fix` via the Skill tool" instructions (it has no Skill tool) → now "recommend to the orchestrator"; corrected the stale "`E:\Decompiled_Bannerlord\` is a different version" note (it's v1.4.5 now) and made `taom-src` the primary signature tool.
- `taleworlds-researcher` — de-staled v1.3.15 → v1.4.5, promoted `taom-src` to the primary decompile path (was `ilspycmd`/`ilspy`-MCP only), fixed the `.vscode/mcp.json` → `.mcp.json` reference.
- `debugger`, `error-detective`, `refactoring-specialist` — added the execution-model block (recommend-don't-invoke).

**Convention + propagation:**
- CLAUDE.md "Briefing subagents (spawn-prompt convention)" — the orchestrator must brief every ad-hoc `Explore`/`Plan`/`general-purpose` agent (they have no body) with: read the operating manual, can't-invoke-skills, the tool reminder, explicit scope, and which convention docs to read. Manual registered in Doc Lookup.
- `agent-teams.md` spawn templates updated to carry the briefing preamble + `taom-src` over `ilspycmd`.

Zero eager-context cost: agent bodies + docs load lazily (only when that agent runs / the doc is read).

### feat(skills): convert the workflow backlog to skills + codify "workflow → skill" convention

Batch-converted the documented-but-un-skilled workflow backlog into 6 new skills, and made "every recurring workflow becomes a skill" a standing practice.

**New skills** (`.claude/skills/`):
- `verify-bindings` — run the TaleWorlds API-binding gate + refresh the committed signature snapshot (the workflow built earlier today).
- `ship` — orchestrate the MANDATORY completion sequence (`/verify` → `/deep-review` → fix → `/review-codex` → close issue → docs → CHANGELOG). Previously prose-only in CLAUDE.md; the RCA history shows steps got skipped.
- `new-culture`, `lord-skills`, `author-armor` — thin entry points over the `docs/ai-includes/` authoring guides (culture armor+troops+recruitment; lord SkillSets; armor item + roster revamp), encoding the top gotchas (mesh-typo fidelity, canonical Armory folder, cover attributes, `skill_template`-not-`<skills>`).
- `localize` — the 12-language propagation pipeline (wrap `{=KEY}` → register → `translate_with_claude.py` → validate).

**Convention + enforcement:**
- CLAUDE.md "Workflow → Skill convention" — the three-part filter (recurring + multi-step + gotcha-bearing) for when to skill-ify, with an explicit eager-context caveat so one-offs/reference docs stay docs. All 6 skills registered in the Skills table.
- `/skill-stocktake` gained a "Workflow coverage" checklist item that flags qualifying documented workflows missing a skill (read-only, surfaces for human decision — never auto-creates).

All new descriptions ≤30 words (validated via `context-budget/scan.sh`); total added eager cost ~250 tokens. Skills are thin orchestrators that point to the authoritative docs rather than duplicating them.

### feat(migration): offline TaleWorlds API-binding gate + committed v1.4.5 signature snapshot

Closes the offline-checkable portion of the never-run S6 migration validation gate (`docs/migration/TRACKING.md`). Three new test classes under `TAOM.Tests/Migration/` verify — on every `dotnet test`, no game launch — that TAOM's engine touchpoints still bind against the *installed* v1.4.5 DLLs:

- **`HarmonyPatchBindingTests`** — enumerates all **110** `[HarmonyPatch]` / `TargetMethod` classes in TAOM.dll and resolves each target exactly as Harmony does at `PatchAll`.
- **`GameModelOverrideBindingTests`** — all **39** GameModels are registered in `SubModule.cs` (`AddModel`), override ≥1 base virtual, and don't shadow a base virtual without `override`. (The compiler already enforces override-signature drift via CS0115 — this targets the gaps it can't see.)
- **`ReflectionSiteBindingTests`** — **32** auxiliary private/internal reflection members (the EditorCacheRebuild `NavigationCache<T>` web, EquipPresets/CompanionTactics/CC/CustomBattles/SpecialResources private fields) resolve against the installed engine.

`GameAssemblies.cs` pre-loads SandBox/CustomBattle/StoryMode module DLLs so engine types resolve in the test AppDomain. TAOM.Tests now references `Lib.Harmony` (compile + runtime) for the resolution APIs.

**Caught a real defect on first run:** `HeroViewModel_FillFrom_Patch` used a name-only `[HarmonyPatch(..., "FillFrom")]`; `HeroViewModel` inherits two more `FillFrom` overloads from `CharacterViewModel`, so Harmony's `AccessTools.Method` threw `AmbiguousMatchException` at patch time and the postfix silently never applied in v1.4.5 (hero-portrait clan colors broken). Fixed by pinning the argument types — the documented HarmonyLib way to disambiguate overloads.

**Committed signature snapshot** at `docs/reference/taleworlds-api-snapshot/` (`reflection-sites.md`, auto-generated `gamemodel-bases.md` + `patch-targets.md` via `tools/snapshot_api_surface.ps1 [-Check]`) so signature lookups no longer require the external `E:\Decompiled_Bannerlord\` dump. Residual in-game checks itemized in `docs/migration/s6-runtime-punchlist.md`.

Full suite green: **2586 passed / 2 skipped / 0 failed**. Negative-proof confirmed (corrupting a catalogued member turns the gate red).

Not-tested: in-game patch *application* + dynamic-reflection CC flow (require a running game — see s6-runtime-punchlist.md).
Research: HarmonyLib annotations (overloaded-method argument-type rule), BUTR.Harmony.Analyzer (compile-time complement).

### docs(lint): may27a doc-rot cleanup — 186 → 35 findings (first `/lint-cleanup-loop` run)

Merged branch `taom-lint/may27a`: the first production run of the autonomous `/lint-cleanup-loop` skill (the program.md-style campaign skill built from the karpathy/autoresearch review — see [docs/research/karpathy-autoresearch.md](docs/research/karpathy-autoresearch.md)).

**Autonomous loop (7 commits, 7 keeps, 0 discards, 0 crashes):** the loop iterated on `stale_versions` findings, fixing where a `v1.3.15` reference described **timeless API surface** (drop the specifier) or **vanilla behavior expected to still hold** (generalize), and leaving every reference that recorded a **historical fact**. Branch-isolated, single-metric (`total_findings` from `tools/lint_docs.py --summary`), results logged to the gitignored `.claude/state/lint-cleanup-loop/results.tsv`. Content fixes dropped stale_versions 184 → 155 across ~10 feature docs + 2 audit-guidance docs.

**Linter calibration (post-loop):** the run confirmed the residual ~150 were over-flags, not rot. `tools/lint_docs.py` now exempts:
- `codex-prompt-*` / `codex-result-*` filenames — Codex review transcripts; their `v1.3.15` refs are verification instructions/records, not stale docs.
- the whole `docs/audits/` tree — the v1.3.15 migration-audit record, where every `v1.3.15-unverified` flag is a historical fact about that campaign.
This dropped stale_versions 155 → 35 in one config change. The 35 remaining are the irreducible floor: filename links (`api-diff-1.3.15-to-1.4.5.md`, sig `.txt` files), essential version *comparisons* (audit-v1.4.5-refactor), upstream port-context (native-skin-fixes shipped a 1.3.15-only DLL), and historical REVIEW-LOG / issue-body snapshots — all left as-is.

**Dead links:** 2 broken memory-path links in REVIEW-LOG.md (`../../../../.claude/projects/.../feedback_*.md`, non-portable user-home paths) converted to inline-code prose. dead_links 2 → 0.

**Backlinks:** regenerated `## Referenced by` footers for 6 docs (lord-skills, bandit-management, dependencies-foundation, issues-drafts) that had drifted from parallel work.

Final doc-lint state on `bannerlord-1.4.5`: **0 dead links, 35 stale-version refs (all intentional), 0 orphans, 0 missing feature docs.**

Tooling: `tools/lint_docs.py` (+`--summary` grep block, atomic `--report` write), `tools/build_backlinks.py`, `tools/analyze_reviews.py` (→ `docs/reviews/progress.png`), `.claude/skills/lint-cleanup-loop/SKILL.md`. All from the karpathy/autoresearch Tier-1 adoption.

## 2026-05-27

### fix(bandit-management): XSLT-remove 5 vanilla hideout-bandit clans (new-game KNFE)

Starting a new campaign crashed with `KeyNotFoundException` in vanilla `BanditSpawnCampaignBehavior.GetInfestedHideoutCount(Clan)` after the same-day `feat(bandit-management)` work. The migration moved all 99 `TAOM_Map` hideouts to the 5 new TAOM bandit cultures (`dunland_raiders`, `rhun_raiders`, `harad_raiders`, `gundabad_raiders`, `umbar_corsairs`) but left the 5 vanilla hideout-bandit clans (`sea_raiders`, `mountain_bandits`, `forest_bandits`, `desert_bandits`, `steppe_bandits`) in `Clan.BanditFactions`. Vanilla's `OnNewGameCreatedPartialFollowUp` iterates that list and reads `_hideouts[clan.Culture]` with a hard indexer — the 5 vanilla clans no longer have any hideouts of their cultures, so the dict lookup throws.

Fix is XSLT-only: 5 empty-template matches in [`spclans.xslt`](Main/_Module/ModuleData/spclans.xslt) drop those clan rows from the merged XML, so they never reach `Clan.BanditFactions`. Vanilla `looters` is kept intact (hardcoded by StringId in `DefaultBanditDensityModel`, and spawns around villages via a separate code path that doesn't touch `_hideouts`). No C# changes — vanilla `IsLooterFaction` identifies looters structurally (`!Culture.CanHaveSettlement && !HasNavalNavigationCapability && StringId != "deserters"`) so the looter spawn path remains unaffected.

Verified zero vanilla C# code hardcodes the 5 removed clan StringIds (grepped decompiled v1.4.5 sources). Captured by 4 escalating TAOM crash bundles today (`taom_crash_20260528_000340_*` and 3 earlier).

### feat(bandit-management): LOTR bandit cultures + PlayerProgress-scaled hideout density + party sizes

Replaces the 5 vanilla bandit cultures with lore-appropriate LOTR factions and adds MCM-tunable difficulty scaling. Greenfield feature — no prior TAOM code touched bandits or hideouts.

LOTR replacement (no save break — hideout IDs preserved):

- `forest_bandits` → **Dunlending Raiders** (troops pulled from `troops_dunland.xml`: peasant, raider, hunter, clan_warrior, wolf_raider)
- `mountain_bandits` → **Gundabad Orc Raiders** (snaga, hunter, grunt, lurker, scout, despoiler_of_the_vale)
- `desert_bandits` → **Haradrim Raiders** (levy, skirmisher, archer, camelscout, footman, camelrider)
- `steppe_bandits` → **Rhûn Raiders** (balcoth_volunteer, balcoth_footman, kharaghul_rider, balcoth_archer, kharaghul_raider)
- `sea_raiders` → **Corsairs of Umbar** (aux_basic, umbar_elite, umbar_elite_root1, umbar_elite_root0, umbar_elite_root00)

Scaling (curves are `1 + curve × PlayerProgress`, default `curve = 1.5` ⇒ vanilla at new campaign, 2.5× at endgame):

- `TaomBanditDensityModel : DefaultBanditDensityModel` overrides 4 properties (hideouts/faction, parties/hideout, first-fight troops, boss-fight troops). MCM caps default to 15 hideouts/faction and 5 parties/hideout (vanilla = 9 and 3).
- `Patch39_BanditPartySize` postfixes `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty`, scaling bandit party troop counts up toward each stack's `MaxValue`. Non-bandit parties pass through. Vanilla floor enforced — bandits never get weaker than vanilla.
- 6 MCM knobs under TAOM → World / Bandit Scaling (`GroupOrder = 35`): master toggle + 3 curves + 2 caps. All NaN/Infinity-guarded per [`feedback_clamp_nan_infinity_propagates.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_clamp_nan_infinity_propagates.md). JSON fallback at [`Main/_Module/ModuleData/bandit_management/bandit_scaling_config.json`](Main/_Module/ModuleData/bandit_management/bandit_scaling_config.json).

XML data:

- 5 new `is_bandit="true"` cultures appended to [`taom_spcultures.xml`](Main/_Module/ModuleData/taom_spcultures.xml).
- 10 new party templates appended to [`taom_partyTemplates.xml`](Main/_Module/ModuleData/taom_partyTemplates.xml) (5 raider + 5 boss).
- ~80 localization keys appended to [`taom_module_strings.xml`](Main/_Module/ModuleData/taom_module_strings.xml) (culture display names + male/female names).
- 99 hideouts in **TAOM_Map external module** had `culture=` swapped + `name=` rewritten ("Dunlending Raider's Camp" etc.) via [`tools/migrate_hideouts_to_lotr.py`](tools/migrate_hideouts_to_lotr.py). 12 loc files also updated. Hideout IDs intentionally preserved for save-compat. Backups saved as `.bak`.

Architecture per CLAUDE.md conventions:

- ADR-002 thin entry — `TaomBanditDensityModel` properties delegate to service; no inline branching per [`gamemodels.md`](.claude/rules/gamemodels.md).
- ADR-007 adapter pattern — service has no TaleWorlds dependencies; all logic is pure math.
- Constructor injection only — `IBanditScalingService` ← `IBanditScalingSettingsProvider` ← (`IBanditScalingConfigProvider` + static `TaomSettings.Instance`).
- `FiniteFloatValidator` guards every numeric MCM/JSON field per [`csharp-architecture.md`](.claude/rules/csharp-architecture.md) "Config Providers MUST Validate".

Tests: 31/31 passing in [`TAOM.Tests/Features/BanditManagement/`](TAOM.Tests/Features/BanditManagement/) — `BanditScalingServiceTests` (16) covering curve linearity, NaN/Infinity guards, monotonicity, vanilla floor + `MinPartiesToInfest` delegation; `BanditScalingConfigProviderTests` (15) covering missing/malformed/partial JSON + every validation rule.

Feature doc: [`docs/features/bandit-management.md`](docs/features/bandit-management.md). Includes how-to for adding hideouts, adding bandit cultures, and disabling scaling.

GitHub issue: [#247](https://github.com/haterade22/TAOM/issues/247). RCA: [`docs/reviews/rca-bandit-management-2026-05-27.md`](docs/reviews/rca-bandit-management-2026-05-27.md).

Phase 1 — `/deep-review` (5 parallel Claude agents): Standards/Compat/Efficiency PASS; Agent 5 (Data Flow) caught one MEDIUM gap (`MinPartiesToInfest` was loaded from JSON + validated but never consumed). Fixed in-session.

Phase 2 — `/review-codex` (adversarial): caught **4 additional bugs** Claude missed (1 CRITICAL, 3 HIGH). All fixed in-session before closing:

1. **CRITICAL — XML comment double-hyphen.** `taom_partyTemplates.xml` had `--` inside an XML comment body. XML spec forbids `--` inside comments — engine parser would have rejected the file at load. Fix: replaced `--` with `=` in two lines. Added `pwsh [xml]$x = Get-Content` validation step.
2. **HIGH — Patch39 was dead code.** `Patch39_BanditPartySize` had `[HarmonyPatch]` but no `[HarmonyPatchCategory]` + no matching `_harmony.PatchCategory(...)` call in `SubModule.cs`. TAOM uses category-based patching exclusively. Result: the bandit party size postfix never engaged. Fix: added `[HarmonyPatchCategory("Patch39_BanditPartySize")]` + `_harmony.PatchCategory("Patch39_BanditPartySize")` to SubModule.
3. **HIGH — Missing bandit clan rows.** `Culture.is_bandit="true"` alone does NOT create a bandit clan in vanilla. `Hideout.MapFaction` resolves via `clan.IsBanditFaction` (loaded from `<Faction is_bandit="true">` rows). Without matching clan rows, the 99 migrated hideouts would have no resolvable MapFaction and `BanditSpawnCampaignBehavior` would NRE on spawn. Fix: authored 5 `<Faction>` rows in `Main/_Module/ModuleData/characters/clans.xml`, one per new culture, each with `initial_home_settlement` pointing at a migrated hideout + `default_party_template` referencing the new raider party template.
4. **HIGH — Cross-module dependency missing.** `TAOM_Map/settlements.xml` references cultures defined in TAOM Main but TAOM_Map didn't depend on TAOM in its SubModule.xml. Load-order was accidental. Fix: added `<DependedModule Id="TAOM"/>` + `<DependedModuleMetadata id="TAOM" order="LoadBeforeThis"/>` to TAOM_Map's SubModule.xml. Backup at `SubModule.xml.bak`.

Phase-2 root-cause pattern: all 4 bugs share "data declared in one place, referenced in another, no connection check." Agent 5's prompt enumerates trace categories but didn't include "every `[HarmonyPatch]` needs a `PatchCategory` registration", "every `is_bandit` culture needs a matching `is_bandit` clan", "every cross-module XML reference needs a `<DependedModule>` declaration", or "every modified XML needs a parse smoke-test." Codified as three new feedback memories — [`feedback_harmony_patch_category_registration_verification.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_harmony_patch_category_registration_verification.md), [`feedback_cross_module_data_dependency_declaration.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_cross_module_data_dependency_declaration.md), [`feedback_xml_parser_smoke_test_before_commit.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_xml_parser_smoke_test_before_commit.md) — and as four new trace categories (9, 10, 11, 12) in the [`/deep-review`](.claude/skills/deep-review/SKILL.md) Agent 5 prompt so the same bug class is caught at the next review. CLAUDE.md "Harmony Patch Categories" updated with `Patch39_BanditPartySize`.

Post-fix verification: `dotnet build` 0 errors. `dotnet test` 2551/2553 (2 preexisting skips, zero regressions, +31 BanditManagement tests).

Save-compat: all hideout IDs preserved; new cultures + templates are pure additions; MCM settings have safe defaults. Existing saves load cleanly.

Verification: `dotnet build Main/TAOM.csproj` 0 errors. `dotnet test --filter BanditManagement` 30/30 passing. Dry-run + apply of [`migrate_hideouts_to_lotr.py`](tools/migrate_hideouts_to_lotr.py) reports 99 master rewrites + 99×12 = 1188 loc rewrites.

### fix(deps): Codex review #42 — 6 correctness defects in Dependencies/Foundation

Adversarial Codex review of the DR3 Phase 4 BetaDeps-parity port (`Dependencies/Foundation/`). 8 architectural Known Suspects + 3 incidental findings → 6 confirmed, 5 disputed with code citations, 0 false positives. All 6 fixed in same session. RCA at [`docs/reviews/rca-dependencies-foundation-2026-05-27.md`](docs/reviews/rca-dependencies-foundation-2026-05-27.md); REVIEW-LOG.md entry [Review 42](docs/reviews/REVIEW-LOG.md#review-42--dependenciesfoundation-dr3-phase-4-betadeps-parity-2026-05-27).

Confirmed fixes:

- **S1 HIGH — `PatchShield.TryUnpatchOffendingPatches` owner allowlist too narrow.** Was `owner.StartsWith("TAOM")` — the first MissingMethodException in any ButterLib-patched method would have auto-unpatched ButterLib's entire patch set (owners are `Bannerlord.ButterLib.*`, `butterlib.*`, etc.). New `ProtectedOwnerPrefixes` array enumerated via ilspycmd from `new Harmony("X")` call sites in every vendored DLL: TAOM + Bannerlord.ButterLib + butterlib. + Bannerlord.UIExtenderEx + Bannerlord.MBOptionScreen + Bannerlord.ModuleLoader + Bannerlord.MCM + bannerlord.mcm. + MCM + MCMv5 + MCM.UI.Adapter + BUTR. + HarmonyLib. + 0Harmony. `IsProtectedOwner(owner)` helper.
- **S5 HIGH — `SubModuleConstructionGuard` mis-attributed AddSubModule failures.** Was `ex.TargetSite` (the vanilla `Module.AddSubModule` method itself), not the offending SubModule class. Now reads `__args[0].SubModuleClassTypeName` + `__args[1].GetType(className)` via `TryResolveAddSubModuleTarget` for authoritative attribution.
- **S2 MED — `SaveShield.IsEngineAssembly` substring trap.** `StartsWith("TAOM")` matched `TAOMBar`, `TAOM.Foo`, anything beginning with `TAOM`. Now exact-match for `TAOM` / `TAOM.Dependencies` + dot-prefix check for `TAOM.` sub-namespaces. Also added missing vendored prefixes (`Bannerlord.MBOptionScreen`, `Bannerlord.ModuleLoader`, `MCM.UI.Adapter`, `BUTR.CrashReport`) and removed redundant raw `TAOM` from `_enginePrefixes` array.
- **A1 MED — `IncompatibleModDetector.ReadCurrentModlist` returned installed mods, not active ones.** Diff comments said "enabled mods" but folder scan returned every installed module — disabled mods appeared in both old and new modlists, masking the real culprit. Strategy-1 now reflects on `TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules()`; strategy-2 folder scan only as fallback for very early init.
- **A2 LOW — `PatchShield` counter name lied.** `_swallowedOther` incremented on both the swallow path AND the re-throw path. Re-thrown means not swallowed; removed increment from re-throw branch.
- **A3 LOW — `PatchShield._shieldedMethods` dedupe collision on overloads.** Key was `DeclaringType.FullName + "::" + method.Name` — method overloads collided, second overload silently skipped. Now `(method.Module.ModuleVersionId, method.MetadataToken)` with `method.ToString()` fallback. Real risk on `Mission.SpawnTroop` (4 overloads in v1.4.5).

Disputed with code citations (would have been bugs if confirmed wrong):

- **S4 (would-be-CRITICAL) — by-ref `__result` Finalizer signature.** Codex DISPUTED that `ref Type[] __result` on `CollectAssemblyTypesShim.SwallowFinalizer` is invalid for an `Assembly.GetTypes()` Finalizer patch. Citing `0Harmony 2.4.2 MethodCreatorTools.EmitCallParameter` showing by-ref `__result` parameters use `Ldloca` (load local address). Verified against the vendored 0Harmony source in this commit.
- **S3 — `VersionProbe.DetectViaApplicationVersion` null arg.** Codex confirmed safe: reflective lookup uses `Type.EmptyTypes` parameter array, vanilla method is parameterless.
- **S6 — `PatchShield` double-install guard.** Codex confirmed `Interlocked.CompareExchange(ref _passOneInstalled, 1, 0)` is correct.
- **S7 — IncompatibleModDetector regex.** Codex confirmed `[\s\S]*?` multi-line strip is correct.
- **S8 — SaveShield dedupe key.** Codex confirmed `MethodInfo` reference equality is stable for distinct overloads.

Root-cause pattern across all 6 confirmed bugs: "filter/attribute/enumerate based on assumption rather than enumeration of upstream/vendored data." Captured as new feedback memory [`feedback_harmony_owner_allowlist_from_vendored_dll_enumeration.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_harmony_owner_allowlist_from_vendored_dll_enumeration.md) — sibling rule to `feedback_substring_keyword_matches_external_data.md`. AGENTS.md updated with 7 new lessons; review counter incremented to 42 reviews / 126 bugs found.

Verification: `dotnet build TAOM.sln` 0 errors. `dotnet test TAOM.Tests` 2,520/2,522 passing. Codex findings were exception-path corner cases; in-game main-menu reach unaffected.

### fix(deps): DR3 Phase 4 — move IncompatibleModDetector.RunEarlyPhase to OnSubModuleLoad (alias stub ctors don't fire)

Second-launch diag.log (with comprehensive logging from previous commit) confirmed the alias stub ctors are NEVER called by the launcher. Root cause: each alias stub module folder (`Modules/Bannerlord.{Harmony,UIExtenderEx,ButterLib,MBOptionScreen}/`) has no `bin/Win64_Shipping_Client/` subfolder, so when the launcher looks for `TAOM.Dependencies.dll` inside the stub's own bin path (per `<DLLName value="TAOM.Dependencies.dll"/>`), it doesn't find it. Construction silently skips.

Why this works practically — the AssemblyResolve handler installs from `TAOM.Dependencies.SubModule`'s static cctor (642ms BEFORE OnSubModuleLoad, per timestamps in diag.log session 4) which is the right early timing for third-party-mod assembly redirects. The only thing that needed the stub-ctor timing was `IncompatibleModDetector.RunEarlyPhase` (writes crash-loop marker). Pre-OnSubModuleLoad crashes can't be diagnosed by TAOM anyway since our code isn't loaded yet — so deferring 642ms is acceptable.

Fix: moved `IncompatibleModDetector.RunEarlyPhase()` call from `AliasStubSubModule.ctor` (dead code, never invoked) → `Dependencies/SubModule.OnSubModuleLoad` (deterministic timing). Marker writes work, crash-loop detection works.

Alias stub `<SubModule>` entries remain in the alias SubModule.xml files — they cost nothing if never invoked, and BLSE may use their PRESENCE as the "this module loads code" signal even if the construction silently fails.

Verification: `dotnet build` 0 errors. `dotnet test` 2,520/2,522 passing.

Optional follow-up (deferred): deploy 4 copies of `TAOM.Dependencies.dll` into each alias stub's `bin/Win64_Shipping_Client/` so the launcher CAN construct `AliasStubSubModule` and all early-phase shims fire from the canonical BetaDeps timing. Cost: ~240KB extra disk for 4 DLL copies + MSBuild target update. Defer until evidence shows the timing matters.

### diag(deps): DR3 Phase 4 — comprehensive lifecycle logging + defer late-phase shield installs

First-launch verification of commit `c47d12e` (v1.4.5 signature fixes) revealed:

1. **`Bannerlord.Harmony` mis-recorded in `last-good-modlist.txt` as `TAOM.Dependencies`** — the IncompatibleModDetector regex matched the FIRST `<Id value="..."/>` pattern in the stub's SubModule.xml, including ones inside XML comments. The Harmony stub's long comment block mentions `<Id value="TAOM.Dependencies"/>` on line 8 before the real `<Module><Id>` on line 42.
2. **No `[VersionProbe]`, `[CollectAssemblyTypesShim]`, `[SubModuleConstructionGuard]`, or `[AliasStub]` lines in `diag.log`** — early-phase shims wired into AliasStubSubModule.ctor were silent in BOTH diag.log AND drained EarlyLog buffer. Either ctors weren't running, or methods were throwing pre-DiagLog. No way to tell without more instrumentation.

Fixes:

- **`IncompatibleModDetector.ReadCurrentModlist`** — strip XML comments via `Regex.Replace(text, @"<!--[\s\S]*?-->", "")` before matching `<Id>`. Caught Bannerlord.Harmony / TAOM.Dependencies duplicate.
- **`Dependencies/AliasStubSubModule.cs`** — added unconditional `DiagLog.Log("AliasStub", "ctor entered")` at top of ctor (proof-of-life); subsequent-instance guard now also logs. `TrySwallow` now dual-logs to both DiagLog (immediate visibility) AND EarlyLog (drained later) so any caught exceptions appear in diag.log.
- **Moved `CollectAssemblyTypesShim.Install` + `SubModuleConstructionGuard.Install` from `AliasStubSubModule.ctor` → `Dependencies/SubModule.OnSubModuleLoad`** — alongside PatchShield + SaveShield (proven-good timing). Stub ctors retain `InstallAssemblyResolveHandler` + `IncompatibleModDetector.RunEarlyPhase` (no Harmony dependency).
- **Wired `VersionProbe.IsDetected` access from `OnSubModuleLoad`** — was lazy-detect via getter access; nothing was touching the getters so detection never ran.
- **Comprehensive lifecycle DiagLog instrumentation** across:
  - `Dependencies/SubModule.cs` — OnSubModuleLoad entry/exit/step-by-step (Harmony guards, UIExtenderEx init, each Install call, ProcessExit hook)
  - `Dependencies/SubModule.cs` — OnGameInitializationFinished entry/exit/each step
  - `Dependencies/SubModule.cs` — InstallAssemblyResolveHandler entry/already-installed/registered
  - `Dependencies/Foundation/CollectAssemblyTypesShim.cs` — Install entry/Harmony ctor/finalizer-resolve/per-patch step + COMPLETE marker
  - `Dependencies/Foundation/SubModuleConstructionGuard.cs` — same pattern (Harmony ctor, finalizer-resolve, MBSubModuleBase ctor enumeration, AddSubModule reflective find + patch, COMPLETE marker)
  - `Dependencies/Foundation/VersionProbe.cs` — EnsureDetected entry, DetectViaApplicationVersion entry, type resolution, method resolution + arg count, invoke result, extracted Major/Minor/Revision
  - `Dependencies/Foundation/IncompatibleModDetector.cs` — RunEarlyPhase + MarkSessionLaunchSuccessful entry/exit/each branch (moduleDir resolve, marker check, modlist count, snapshot write)

Trade-off: diag.log is now significantly more verbose (~30-50 lines per session vs ~6 previously). Acceptable for diagnostic instrumentation phase; revisit log volume after we've used the data to identify what's actually broken.

Verification: `dotnet build TAOM.sln` 0 errors. `dotnet test TAOM.Tests` 2,520/2,522 passing. In-game re-launch will show: (a) every Install method's success or specific failure point, (b) whether AliasStubSubModule.ctor is reached at all, (c) VersionProbe detection result + which strategy succeeded, (d) `Bannerlord.Harmony` now correctly in last-good-modlist (vs duplicate `TAOM.Dependencies`).

### docs(lord-skills): authoring guide + feature doc + Doc Lookup row + memory entry

Captures the end-to-end lord-skills+traits workflow built over three commits
(`feabca2` → `8665ca6` → `c5dc168`) so future sessions can repeat the process
without re-discovering the architecture.

- New: [`docs/ai-includes/lord-skills-authoring.md`](docs/ai-includes/lord-skills-authoring.md) —
  the playbook. TL;DR + 3-layer architecture + end-to-end workflow + script
  quick-ref + 35-archetype catalog + 3 recipes (add canonical / add archetype /
  add culture) + verification checklist + commit-split pattern + per-culture
  GitHub issue creation + gotchas with memory references.
- New: [`docs/features/lord-skills.md`](docs/features/lord-skills.md) — feature
  doc per CLAUDE.md mandatory rule.
- Updated: [`CLAUDE.md`](CLAUDE.md) Doc Lookup table — new row pointing at the
  authoring guide.
- Updated: [`docs/INDEX.md`](docs/INDEX.md) — Quickstart paths row + Career &
  Progression listing.
- New memory: `feedback_skill_template_overrides_explicit_skills.md` — codifies
  the systemic learning that hero NPCCharacters use `skill_template` not
  `<skills>`; future agents must check this BEFORE recommending explicit-skill
  edits to lord rosters.

### feat(career-system): starter equipment rosters for every culture (12 new cultures × 6 archetypes)

Previously only Gondor had full career starter rosters; the four warg cultures (Mordor/Isengard/Gundabad/Dol Guldur) had horse-only cavalry rosters with no body/leg/weapons; the remaining 11 cultures fell through entirely to the youth/title-default outfit.

User direction: "implement the equipments for every career like we did Gondor. Identify the lowest armored stuff in the Armoury. and give it to the player."

New tool [`tools/generate_career_starter_rosters.py`](tools/generate_career_starter_rosters.py) — for each TAOM culture with a dedicated LOTRLOME_Armory folder, parses the folder's `body_armors.xml` + `leg_armors.xml`, picks the item with the lowest `body_armor` / `leg_armor` stat, and emits 6 rosters (Infantry/Ranged/Cavalry × m/f) referencing those armor items + per-culture weapon picks from a hardcoded config table. Idempotent (`--dry-run` / `--apply`); re-runnable when LOTRLOME_Armory ships new low-tier items.

[`taom_career_starting_equipment.xml`](Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml) regenerated — **78 rosters** total:

| Culture group | Coverage |
|---|---|
| Gondor | **Preserved verbatim** — already authored with custom-tuned `starter_*_gondor_*` items |
| Warg cultures (Mordor, Isengard, Gundabad, Dol Guldur) | Body+leg+weapons added; horse stays warg_brown + warg_saddle |
| Erebor / Rivendell / Mirkwood / Rohan (vlandia) / Dunland (empire) / Harad (aserai) / Rhun (khuzait) / Dale (sturgia) | Full 6 rosters; non-warg cavalry uses vanilla `saddle_horse` + `leather_horse_harness` |
| Lothlorien / Umbar / Khand (battania) | **SKIPPED** — no dedicated LOTRLOME_Armory folder. Graceful fallback in `ICareerStartingEquipmentService` keeps the menu functional; player keeps culture-default outfit until rosters are authored later. |

Lowest-armor picks per culture (selected from `body_armor` / `leg_armor` stat, sorted ascending):
- mordor: `sk_uruk_mordor_chainmail_light_a` / `sk_md_orc_boots_a`
- isengard: `sk_uruk_hai_tunic_a1` / `sk_uruk_hai_shoes_a1`
- gundabad: `sk_gb_uruk_chest_light_a` / `sk_gb_uruk_boots_light_a`
- dolguldur: `sk_dg_uruk_chest_light_a` / `sk_dg_uruk_boots_light_a`
- erebor: `sk_dwarf_dress_normal_a` / `sk_dwarf_erebor_boots_light_a`
- rivendell: `rivendell_torso_light_light_tier1` / `rivendell_boots_leather1`
- mirkwood: `mkwd_inf3_chest` / `mirkwood_boots`
- vlandia (Rohan): `rohan_militia_tunic_a` / `cts_rohan_boots3`
- empire (Dunland): `dunland_caerdh_chainmail_light_a` / `dunland_caerdh_boots_light_a`
- aserai (Harad): `harad08_torso` / `harad08_boots`
- khuzait (Rhun): `sk_rh_loke_tunic_a` / `easterling02_v1_boots`
- sturgia (Dale): `clo_sk_dale_chest_archer_a01` / `sk_dale_boots_archer_a01`

Each roster overrides only Body + Leg + Item0..2 (+ Horse + HorseHarness for cavalry) — Head/Cape/Gloves inherit from the culture-default applied just before via `Equipment.FillFrom`'s slot-merge.

Files: new generator tool + regenerated rosters XML. **No new items authored** in LOTRLOME_Armory. No code change.
Tests: 2520/2522 passing. `CareerCultureCoverageTests` JSON↔XML cross-reference still balanced (rosters are layered on top of careers, not in 1:1 lockstep).
Save-compat: equipment binds at character-creation finalize; no migration.
Not-tested: in-game spot-check per culture. Likely visual review needed since some lowest-armor picks (e.g., erebor's `sk_dwarf_dress_normal_a` which is a "dress" mesh, not soldier garb) may want manual override.

### fix(lords-skills): TAOM-owned SkillSets — make the lore-driven values actually take effect

Follow-up to the 16-culture lords-skills sweep (commit 8665ca6). In-game testing
revealed Boromir showing OneHanded=145 instead of the 295 written into his XSLT
template. Root cause: the engine ignores explicit `<skills>` blocks on hero
NPCCharacters and uses the value referenced by `skill_template="SkillSet.X"`
instead. Vanilla's `spc_dandy_skills` (OneHanded=140) was overriding everything.

**Fix:** create TAOM-owned SkillSets that the engine WILL respect, and point
each adult lord at the right one.

- New file `Main/_Module/ModuleData/taom_lord_skill_sets.xml` with **120 SkillSets**:
  - 35 archetype sets (`taom_lord_skills`, `taom_knight_skills`, `taom_dwarf_king_skills`,
    `taom_elf_king_skills`, `taom_nazgul_skills`, `taom_orc_chieftain_skills`, etc.)
  - 85 per-canonical sets (`taom_canonical_lord_1_75_skills` for Boromir,
    `taom_canonical_lord_1_9_skills` for Imrahil, `taom_canonical_lord_L1_1_skills`
    for Galadriel, etc.)
- Each adult NPCCharacter's `skill_template` attribute swapped from vanilla
  `SkillSet.spc_*` to the matching TAOM set: e.g. Boromir now points to
  `SkillSet.taom_canonical_lord_1_75_skills` (OneHanded=295, Leadership=285).
- Registered in `Main/_Module/SubModule.xml` alongside the existing
  `taom_wanderer_skill_sets` entry.
- Tool changes in `tools/apply_culture_skills_traits.py`:
  - New `build_skill_sets_xml()` — emits the full SkillSets file from
    `BASE_ARCHETYPES` + per-culture `canonical` dicts (deterministic, sorted by id).
  - New `update_skill_template_in_block()` — swaps `skill_template="SkillSet.X"`
    on both lords.xml inline attrs and lords.xslt `<xsl:attribute>` blocks.
  - New `--all-cultures` flag for batch processing; existing `--culture <key>`
    still works.
  - Canonical NPCs now bypass the age<14 skip (fixes two Nazgûl that had
    placeholder ages 9 + 11 in lords.xslt and were treated as children).
  - Added Gondor to `CULTURES` dict (was previously handled by a separate
    Gondor-only script).

Coverage: 737 NPCs across 17 cultures (Gondor through Abanissa) now use TAOM
SkillSets. 29 children + 1 vanilla NPC (lord_6_6 Suran) still reference vanilla
`spc_*_skills_rookie` — by design (skip-children rule).

Save-compat: hero skills are baked at hero creation. Existing campaigns keep
their old stats; new campaigns + un-spawned heroes use the new SkillSets.

Not-tested: in-game spot check of Boromir / Imrahil / Galadriel skill panels
(blocked by 0Harmony.dll lock — Bannerlord was running during the session).

Refs: #228-#245

### fix(deps): DR3 Phase 4 follow-up — v1.4.5 signature verification across all defensive shields

First in-game launch (commit `09c94de` ran clean) flagged 4 SaveShield install warnings in `diag.log`. User asked for a full audit of Phase C TaleWorlds touchpoints against the actual v1.4.5 decompile. Three issues found and fixed:

**1. `SaveShield._targets` — 4 stale entries (BetaDeps reference list was for a different Bannerlord version)**

Decompiled v1.4.5 actual signatures via `ilspycmd` against the installed DLLs:

| Stale (BetaDeps original) | v1.4.5 actual | Why |
|---|---|---|
| `SandBoxSaveHelper.LoadSaveGame` | `SandBoxSaveHelper.TryLoadSave` | `LoadSaveGame` doesn't exist; `TryLoadSave` is the public entry point |
| `TaleWorlds.SaveSystem.LoadResult` (top-level) | dropped — class is at `TaleWorlds.SaveSystem.Load.LoadResult` | No top-level `LoadResult` type exists |
| `TaleWorlds.SaveSystem.Load.LoadResult.Load` | `LoadResult.InitializeObjects` + `LoadResult.AfterInitializeObjects` | Class is the RESULT of load, not the action; hot methods are these two |
| `TaleWorlds.MountAndBlade.Mission.OnInitialize` | `Mission.Initialize` (public) + `MissionState.OnInitialize` (protected override) | `Mission` has `Initialize`; lifecycle hook lives on `MissionState` |

`_targets` went from 10 entries (4 broken) to 11 entries (all verified). Coverage should now report `+11 new, 0 skipped` instead of `+7 new, 4 skipped`.

**2. `VersionProbe` — both detection strategies referenced types that don't exist in v1.4.5**

- Strategy 1 referenced `TaleWorlds.ModuleManager.ApplicationVersionHelper.GameVersion()` — **type does not exist in v1.4.5**. `ilspycmd` confirms `InvalidOperationException: Could not find type definition`.
- Strategy 2 referenced `Module.CurrentModule.Version` — `Module` class has no `Version` property in v1.4.5 (verified via decompile).

Replaced with the v1.4.5-correct path: `TaleWorlds.Library.ApplicationVersion.FromParametersFile()` (reads `parameters/Version.xml`, returns `ApplicationVersion` struct with `Major`/`Minor`/`Revision` properties). Dropped the `GameBranch` enum (BetaDeps's auto-disable rule classifier — we don't have those rules yet); the public API is now just `Major`/`Minor`/`Revision`/`IsDetected`. No callers needed updating (no internal use of `Branch`/`IsBeta`/`IsPublic`).

**3. `SubModuleConstructionGuard` — patching MBSubModuleBase ctors alone misses derived ctor body exceptions**

BetaDeps's original implementation patched only `MBSubModuleBase` ctors. But the implicit `base()` call returns successfully BEFORE the derived class's ctor body runs — so exceptions thrown by the derived ctor body (e.g., `new ThirdPartyMod.SubModule()` calling `MissingMethodException`-throwing code in its body) propagate UP past our Finalizer.

Verified the v1.4.5 actual construction site: `Module.AddSubModule(SubModuleInfo, Assembly)` (private) calls `constructor.Invoke(new object[0])` on the derived ctor — the exception path runs through TaleWorlds's `AddSubModule` method, NOT through the base class ctor.

Added a SECOND Finalizer patch site at `Module.AddSubModule` (reflectively located, gracefully degrades if type not loaded yet). Catches exceptions wrapped in `TargetInvocationException`, unwraps to the inner ctor exception, attributes to culprit assembly, swallows non-TAOM exceptions. Shared `SwallowFinalizer` between both patch sites for consistency.

Verification: `dotnet build TAOM.sln` 0 errors. `dotnet test TAOM.Tests` 2,520/2,522 passing. In-game `diag.log` re-launch expected to show:
- `[SaveShield] install complete: shielded +11 new, 0 already-shielded, 0 skipped` (was +7, 4 skipped)
- `[VersionProbe] detected Bannerlord v1.4.5 via ApplicationVersion.FromParametersFile` (new strategy, was 0)
- `[SubModuleConstructionGuard] installed Finalizer on N construction site(s)` where N = (1 to 2) MBSubModuleBase ctors + 1 Module.AddSubModule

Research: `ilspycmd` against installed v1.4.5 DLLs (`TaleWorlds.Core.dll`, `TaleWorlds.Library.dll`, `TaleWorlds.SaveSystem.dll`, `TaleWorlds.MountAndBlade.dll`, `Modules/SandBox/bin/SandBox.dll`). All TaleWorlds reflection targets in Phase C now verified against the actual installed v1.4.5 API.

### chore(deps): DR3 Phase 4 D — polish: THIRD-PARTY-LICENSES + maintenance docs + CLAUDE.md (#246)

Closing the BetaDeps parity work with attribution + documentation updates.

- New `Dependencies/_Module/THIRD-PARTY-LICENSES.txt` — explicit MIT (and Apache 2.0 for Serilog) attribution for the 20+ vendored runtime DLLs (0Harmony, Bannerlord.UIExtenderEx, ButterLib, MCMv5, MBOptionScreen, BUTR.CrashReport family, Microsoft.Extensions.*, Serilog.*, System.* polyfills). Plus a note that the `Dependencies/Foundation/` classes are clean-room MIT rewrites of the corresponding BetaDeps.Foundation classes. Deploys automatically via Bannerlord.BuildResources's `_Module/` mirroring; visible at `<game>/Modules/TAOM.Dependencies/THIRD-PARTY-LICENSES.txt`.
- `docs/migration/dr3-maintenance.md` gets new "Defensive infrastructure" section listing each shield's purpose, the four disk artifacts (`diag.log`, `failed-mods-catalog.txt`, `session-launching.marker`, `last-good-modlist.txt`), the two opt-out flags (`patchshield-disabled.flag`, `saveshield-swallow-disabled.flag`), and a "verifying the shields are healthy" rubric showing what a clean `diag.log` looks like vs what swallow counts indicate a broken mod. Also adds the v99 stub-version maintenance rule explanation.
- `CLAUDE.md` Key Paths gains a "TAOM.Dependencies defensive infrastructure" row pointing at `Dependencies/Foundation/` with the full 11-class inventory; updates the existing stub-modules row to reflect the new real-`<SubModule>` entry pattern + v99 strategy.

Phase 4 complete: stub-XML hardening (A) + AssemblyResolve/load-order expansion (B) + defensive infrastructure (C) + polish (D). Closes #246.

### feat(deps): DR3 Phase 4 C — defensive infrastructure ports PatchShield + SaveShield + IncompatibleModDetector (#246)

The high-leverage half of the BetaDeps parity plan. Eight new classes under `Dependencies/Foundation/` port BetaDeps's runtime error-tolerance framework — the components that deliver BetaDeps's "Catches out-of-date mod errors at runtime so the game keeps running" Nexus promise.

**Component overview:**

- `RuntimeLog.cs` — resolves canonical paths: `<game>/Modules/TAOM.Dependencies/diag.log` for runtime events + `ModuleDir` for shield disable-flag files. Path resolution walks back from `typeof(RuntimeLog).Assembly.Location` to the module root.
- `DiagLog.cs` — append-only structured logger writing directly to `diag.log`. Threadsafe via single lock. Nested try/catch so logging failures never escape — Finalizers depend on DiagLog being safe to call from inside an exception handler. Session marker written on first log of session.
- `ReflectionUtils.cs` — small helpers: `FindTypeAcrossLoadedAssemblies`, `TryInvokeStatic`, `GetAssemblySimpleName`. All exception-safe.
- `VersionProbe.cs` — detects Bannerlord version + branch (Public vs Beta) via reflection. Strategy 1: `ApplicationVersionHelper.GameVersion()`. Strategy 2: `Module.CurrentModule.Version`. Bannerlord 1.4+ classified as Beta.
- `IncompatibleModDetector.cs` — crash-loop detection. Writes `session-launching.marker` at stub-ctor time; deletes it in `MarkSessionLaunchSuccessful()` from `OnGameInitializationFinished`. If marker survives to next launch, previous session crashed pre-menu — diffs current modlist against `last-good-modlist.txt` to identify newly-added mods as likely culprits. **TAOM Phase 4 ships detection only, NOT auto-disable**; XML mutation of LauncherData.xml is deferred (BetaDeps gates it with `auto-disable-enabled.flag`).
- `PatchShield.cs` — **biggest user-value component**. Iterates `Harmony.GetAllPatchedMethods()`, attaches a Finalizer to every non-TAOM patch. Finalizer catches the trinity (`MissingMethodException`, `MissingFieldException`, `TypeLoadException`) and auto-unpatches the offending owner's prefixes/postfixes/transpilers from the failing target. The patched method continues running uncaught from the user's perspective — game keeps going. Per-category counters; opt-out via `patchshield-disabled.flag`. Session summary written to `diag.log` on `AppDomain.ProcessExit`. Installed in `OnSubModuleLoad` + re-installed in `OnGameInitializationFinished` to catch late-registering mods.
- `SaveShield.cs` + `FailureRecord.cs` + `FailedModsCatalog.cs` — targeted Finalizer shield on 10 TaleWorlds save/load/mission methods (`MBSaveLoad.LoadSaveGameData`, `SandBoxSaveHelper.LoadGameAction/LoadSaveGame`, `SaveManager.Load`, `LoadResult.Load`, `MissionState.FinishMissionLoading`, `Mission.SetMissionMode/OnInitialize/SpawnTroop`). Catches DuplicateKey + other save failures. Stack-walks the exception to attribute culprit assembly (skipping engine prefixes like `TaleWorlds.*`, `SandBox`, `0Harmony`, `TAOM`, etc.). Persists records to `failed-mods-catalog.txt` for user diagnosis. Per-session dedupe by (culprit, exception-type, owner). Opt-out via `saveshield-swallow-disabled.flag`.
- `SubModuleConstructionGuard.cs` — Harmony Finalizer on `MBSubModuleBase` ctors. When a third-party mod's SubModule ctor throws (referencing a removed vanilla API), swallows the exception, logs culprit type + assembly. Refuses to shield TAOM-owned SubModules (we want our own errors to propagate during dev).
- `CollectAssemblyTypesShim.cs` — Finalizer on `Assembly.GetTypes()` + `Assembly.GetExportedTypes()`. Catches `ReflectionTypeLoadException` and returns the partial-types list (`ex.Types.Where(t => t != null)`) instead of propagating. Prevents cascade failures when one broken third-party mod's `GetTypes()` call brings down launcher utilities that iterate all assemblies.

**Wiring (lifecycle integration):**

- `Dependencies/AliasStubSubModule.cs` (the four stub SubModules' loadable class, from Phase A) ctor now invokes the early-phase shims via `TrySwallow`: `IncompatibleModDetector.RunEarlyPhase`, `CollectAssemblyTypesShim.Install`, `SubModuleConstructionGuard.Install`. These run BEFORE any third-party mod's SubModule ctor.
- `Dependencies/SubModule.cs` `OnSubModuleLoad` installs late-phase shields: `PatchShield.Install()` + `SaveShield.Install()`. Hooks `AppDomain.ProcessExit` to flush `PatchShield.WriteSessionSummary()`.
- New override `Dependencies/SubModule.OnGameInitializationFinished(Game)` — calls `IncompatibleModDetector.MarkSessionLaunchSuccessful()` (deletes the crash-loop marker + snapshots `last-good-modlist.txt`) AND re-runs `PatchShield.Install()` to catch late-registered patches.

**Opt-out flags (place in `<game>/Modules/TAOM.Dependencies/`):**

- `patchshield-disabled.flag` — skip PatchShield install entirely
- `saveshield-swallow-disabled.flag` — install SaveShield but re-throw exceptions instead of swallowing

**Files added (8 under `Dependencies/Foundation/`):**

`RuntimeLog.cs`, `DiagLog.cs`, `ReflectionUtils.cs`, `VersionProbe.cs`, `IncompatibleModDetector.cs`, `PatchShield.cs`, `SaveShield.cs`, `FailureRecord.cs`, `FailedModsCatalog.cs`, `SubModuleConstructionGuard.cs`, `CollectAssemblyTypesShim.cs` (10 files — `Dependencies/Foundation/` is the new namespace mirroring BetaDeps.Foundation).

**Files modified**: `Dependencies/SubModule.cs` (added `using TaleWorlds.Core` + `using TAOM.Dependencies.Foundation`; wired `PatchShield.Install` / `SaveShield.Install` / `OnGameInitializationFinished` override + `ProcessExit` summary hook). `Dependencies/AliasStubSubModule.cs` (ctor now invokes early-phase shims).

**Verification**: `dotnet build TAOM.sln` 0 errors. `dotnet test TAOM.Tests` 2,520/2,522 passing (baseline maintained — no production C# code changed in the Main module, only Dependencies module gained 8 new files and 2 wired hooks).

In-game verification pending: confirm `diag.log` writes appear at `<game>/Modules/TAOM.Dependencies/diag.log` on next launch; that PatchShield shield-counter entries appear in the session summary; that an intentionally-broken third-party Harmony patch is auto-unpatched + logged instead of crashing.

Research: full source-level review of BetaDeps v0.7.5.1 (decompiled BetaDeps.Foundation.dll via ilspycmd). Ports adapt BetaDeps's design but rewrite all code under MIT — see `Dependencies/_Module/THIRD-PARTY-LICENSES.txt` (added in Phase D).

### feat(deps): DR3 Phase 4 A+B — BetaDeps parity stub hardening + AssemblyResolve expansion (#246)

After source-level review of BetaDeps v0.7.5.1 (Nexus 11274), adopted the BUTR-community-canonical patterns for stub modules + AssemblyResolve coverage. Phase A+B of the 4-phase parity plan; Phase C (defensive infrastructure — PatchShield, SaveShield, IncompatibleModDetector) and Phase D (licenses + docs) follow in subsequent commits.

**Phase A: Stub XML hardening**
- Bumped stub `<Version>` from exact-match to `vX.Y.99.0` strategy: `Bannerlord.Harmony` 2.4.99.0, `.UIExtenderEx` 2.13.99.0, `.ButterLib` 2.10.99.0, `.MBOptionScreen` 5.11.99.0. Third-party mods often declare `<DependedModuleMetadata version="vX.Y.x"/>` as a minimum-version check; v99 satisfies any reasonable lower-bound without claiming a major-version jump.
- Added BLSE/multiplayer-mod compat tags: `<SingleplayerModule value="true"/>`, `<MultiplayerModule value="true"/>`, `<Official value="false"/>`, `<Url value=""/>`. The MP flag closes the Agent 5 Trace 9 gap from the 2026-05-25 deep-review — multiplayer mods that depend on standard BUTR IDs are now satisfied.
- Added `xsi:noNamespaceSchemaLocation` BUTR XSD reference to all four stubs + `Dependencies/_Module/SubModule.xml`. Free editor validation in IDEs; zero runtime impact.
- **Critical: replaced empty `<SubModules />` with a real `<SubModule>` entry** referencing new `TAOM.Dependencies.AliasStubSubModule`. BetaDeps v0.7.2 → v0.7.5 evolution proved empty `<SubModules />` makes BLSE treat the stub as "metadata-only / not actually loaded" — drag-to-reorder breaks with "Missing Bannerlord.Harmony a0.0.0.0 to a0.0.0.0". Our stubs would have hit the same trap silently if any user installed BLSE.
- New `Dependencies/AliasStubSubModule.cs` (~70 lines, MBSubModuleBase derivative). Ctor + OnSubModuleLoad both call `SubModule.InstallAssemblyResolveHandler()`, all work try/catch-wrapped so any LauncherEx-time exception is logged but never escapes.
- Refactored `Dependencies/SubModule.cs`: promoted AssemblyResolve install into a public static `InstallAssemblyResolveHandler()` with `Interlocked.CompareExchange` gate. Idempotent and threadsafe; safe to call from both the SubModule static cctor and the four AliasStubSubModule ctors.

**Phase B: AssemblyResolve coverage + load-order pinning**
- Expanded `RedirectedSimpleNames` from 4 names to 22 (BetaDeps's reference list): adds Microsoft.Extensions.* family (5), Serilog (2), Mono.Cecil + MonoMod.Core/Utils, System.* polyfills (6), Newtonsoft.Json. Catches version-mismatch scenarios when a consumer mod ships its own bundled copy of any of these.
- Expanded `<ModulesToLoadAfterThis>` in `Dependencies/_Module/SubModule.xml` from 5 vanilla entries to 26: adds the 4 stub IDs + 17 known BUTR-consumer mod IDs (DismembermentPlus, XorberaxLegacy, DynaCulture, ArtemsLivelyAnimations, FluidCombatLite, BetterSmithing, FasterTime, BanditBlackHole, PerfectFireArrows, IDontCare, ImprovedGarrisons, DistinguishedService, CulturedStartV2, DiplomacyFixes, Diplomacy, CalradiaExpanded, CalradiaExpandedKingdoms, CargoHolds, CrashDoctor, AchievementUnblocker, CREST, Crest). Pinning order so TAOM.Dependencies loads BEFORE any consumer mod's bundled MCMv5/0Harmony copy could win the AppDomain slot. Unknown Ids are silently ignored by the launcher.

**Verification**: `dotnet build TAOM.sln` 0 errors; `dotnet test TAOM.Tests` 2,520/2,522 passing (no regressions). All 4 stubs deployed to game install with new fields verified via PowerShell read-back.

**Architectural decisions (locked, not implemented here)**: continue vendoring official BUTR binaries (no clean-room reimpl), keep MSBuild build-time stub deploy (no runtime `BootstrapAliasFolders`), skip custom MCMOptionRow.xml prefab, defer FOMOD installer. See plan file for full rationale.

Research: full source-level review of BetaDeps v0.7.5.1 distribution (4 alias SubModule.xml, FOMOD installer config, decompiled BetaDeps.Foundation.dll + BetaDeps.Harmony.dll). 22-name AssemblyResolve list mirrors `BetaDeps.Foundation.AssemblyVersionShim._redirectedNames`. 26-entry ModulesToLoadAfterThis mirrors BetaDeps SubModule.xml.

### feat(docs-kb): Tier-1 buildout from karpathy/autoresearch review — analyze_reviews, structured `---` summaries, atomic writes, `/lint-cleanup-loop` skill

Honest audit after the prior commit's research-only contribution: 0 of 15 documented gaps had been closed. This commit closes the Tier-1 set.

**`tools/analyze_reviews.py`** — mirrors autoresearch's [`analysis.ipynb`](https://github.com/karpathy/autoresearch/blob/master/analysis.ipynb). Parses the markdown tables in [`docs/reviews/REVIEW-LOG.md`](docs/reviews/REVIEW-LOG.md) (32 reviews across two summary tables, 77 real bugs, 8 false positives, 9 missed), computes per-prompt-version accuracy + cumulative real bugs, produces [`docs/reviews/progress.png`](docs/reviews/progress.png) via matplotlib. The PNG is committed even though no TSV equivalent exists in repo — same convention as autoresearch (story-output committed, raw log stays in markdown). `--summary` emits a `---` block; `--no-plot` skips the chart for headless contexts. The chart shows the v1/v2-prompt high-miss-rate era visibly, then 91% accuracy through v4+.

**`--summary` flag on `tools/lint_docs.py` and `tools/build_backlinks.py`** — emits a structured `---`-delimited block of `key:           value` lines designed for `grep "^dead_links:" output.log`. Modeled on autoresearch's `train.py` final-output block. Autonomous loops can now consume tool output without parsing markdown.

**Atomic-write for `tools/lint_docs.py --report`** — write to `.tmp` then `os.rename` (atomic on POSIX + Windows). Mirrors autoresearch's `prepare.py` download pattern. Prevents partial-write corruption if the script is interrupted mid-write — actually relevant since the lint report is ~37KB and we'll start running it from autonomous loops.

**`.claude/skills/lint-cleanup-loop/SKILL.md`** — first program.md-style autonomous campaign skill. Maps every autoresearch primitive into TAOM:
- `train.py` ↔ TAOM docs being edited
- `prepare.py` ↔ `tools/lint_docs.py` (the read-only ground-truth metric)
- `program.md` ↔ the SKILL.md itself
- `val_bpb` ↔ `total_findings` from `--summary` output
- `results.tsv` ↔ `.claude/state/lint-cleanup-loop/results.tsv` (gitignored)
- `autoresearch/<tag>` branch ↔ `taom-lint/<tag>` branch
- 5-min budget ↔ 10-min per-iteration soft cap
- "NEVER STOP" ↔ same (cites the rule from CLAUDE.md + program.md)
- Crash judgment, simplicity criterion, output-capture discipline ↔ same

Stop conditions explicitly enumerated: metric=0, 5 consecutive non-improvements, user interrupt, or `--max-iters` hit. Branch-isolated (`taom-lint/<tag>`) so it can't damage `bannerlord-1.4.5`. Doesn't auto-merge — the user reviews the kept commits before merging.

**`.gitignore`** adds `.claude/state/` for skill working-state (results.tsv, last-lint.md, per-iteration captures).

**What's still un-done** (Tier 2/3 deferred deliberately):
- Tier 2: fast-fail guards in long-running scripts (covered by the loop's "5 consecutive discards = stop" rule but not per-script-internal yet)
- Tier 2: exponential-backoff retry in Python tools (only relevant when we have network I/O, which we don't yet)
- Tier 2: time-budget enforcement with warmup exclusion (relevant when iterations have variable startup time; today they don't)
- Tier 3: gitignore CLAUDE.md / AGENTS.md (explicitly rejected — TAOM treats them as canonical)
- Tier 3: move data to `~/.cache/taom/` (explicitly rejected — Modules/ deployment model doesn't map)

Constraint: matplotlib was not previously a TAOM tooling dependency. Installed via `pip install --user matplotlib` on the dev machine; not added to a requirements.txt because we don't have one for `tools/`. If `tools/analyze_reviews.py` runs in CI later, the workflow will need to `pip install matplotlib` explicitly. The `--no-plot` flag makes the script useful headlessly.
Research: full read of karpathy/autoresearch (10 files, 4 substantive) — see [`docs/research/karpathy-autoresearch.md`](docs/research/karpathy-autoresearch.md) for the 52-pattern catalog this commit was derived from.

### fix(rohan): clothe naked tavern/town NPCs + upgrade guards to chainmail

Two related XML fixes in [`npcs_rohan.xml`](Main/_Module/ModuleData/characters/npcs_rohan.xml).

**Root cause for naked Aldburg tavern NPCs:** 31 civilian-occupation NPCs (Townsfolk, Villager, GoodsTrader, Blacksmith, Weaponsmith, Armorer, HorseTrader, Tavernkeeper, TavernGameHost, TavernWench, Musician, ArenaMaster, ShopWorker) used bare `<EquipmentRoster>` instead of `<EquipmentRoster civilian="true">`. The engine's settlement-walk / tavern / dialog code only selects rosters tagged `civilian="true"`; bare rosters fall through to underwear. Items themselves all resolved correctly in Armory. Vanilla SandBoxCore + TAOM's [`npcs_gondor.xml`](Main/_Module/ModuleData/characters/npcs_gondor.xml) use the attribute correctly. Fix: 56 rosters across 31 NPCs flipped to `civilian="true"` via scoped Python (`occupation` whitelist), leaving the 20 combat-context rosters (PrisonGuard, Soldier, CaravanGuard, gang-leader bodyguards) bare.

**Guard upgrade:** `prison_guard_rohan` (2 rosters) + `guard_rohan` (3 rosters) swapped from `rohan_militia_armour_*` leather (~20 body armor) to proper Rohan chainmail/scalemail. Prison guards now wear `cts_rohan_armor1` (Chainmail I) and `cts_rohan_armor3` (Chainmail II) with `roh_sol_bts` boots. Town guard gets one captain-tier loadout (`cts_rohan_armor2` Scalemail + `cts_rohan_helmet_captain1` + `cts_rohan_captain_bracer` + `cts_rohan_captain_boots`) and two chainmail variants (`armor3`, `armor1` + `roh_sol_bts`) for visual diversity. Helmets/armguards on the chainmail loadouts kept at existing `cts_rohan_helmet1/2/2a` and `armguard1/2/4` for mid-tier soldier aesthetic. `theodred_helmet` / `theoden_helmet` deliberately not used (named-character gear).

Not-tested: in-game visual on Aldburg tavern + dungeon + town walls to confirm fix.

### feat(all-cultures-lords): lore-driven skills + traits for every culture (16 cultures, ~880 adult NPCs across lords.xml + lords.xslt)

Sweep continuation of the Gondor pass (118 NPCs) to cover ALL 16 TAOM cultures with lore-driven skills + traits.

**Cultures shipped this pass** (skills+traits applied per archetype + canonical override per named Tolkien hero):

| Culture | culture_id | adult NPCs touched | Canonical Tolkien overrides |
|---|---|---|---|
| Rohan | vlandia | 92 | Théoden, Éomer, Théodred, Éowyn, Erkenbrand, Grimbold, Théodwyn, Elfhild, + family/marshals |
| Erebor (Dwarves) | erebor | 30 | Dáin II Ironfoot, Thorin III Stonehelm |
| Dale (Bardings) | sturgia | 82 | bio-driven archetypes (TAOM Slavic-named roster, mostly wives + nobles) |
| Mirkwood (Silvan Elves) | mirkwood | 29 | Thranduil (Elvenking), Legolas |
| Rivendell (Imladris) | rivendell | 7 | Elrond, Celebrían, Elladan, Elrohir, Arwen, Glorfindel |
| Lothlórien | lothlorien | 3 | Galadriel (Charm/Leadership 300), Celeborn |
| Mordor | mordor | 97 | 3 Nazgûl, Grishnâkh, Verina, Svala Redfang, Black Númenórean family |
| Dol Guldur | dolguldur | 59 | 6 compound chieftains (Thrangul, Narzugh, Lorgath, Urzara, Throrgash, Thrurg) |
| Mount Gundabad | gundabad | 50 | 5 compound chieftains incl. Bolgath (Bolg-evoking) |
| Isengard | isengard | 34 | Uglûk, Mauhûr, Lugdush, Lurtz, Sharku (Warg-Rider Captain) |
| Dunland | empire | 68 | bio-driven (Norse-themed shieldmaidens + raiders) |
| Harad | aserai | 73 | bio-driven (Haradrim cavalry + Mumakil + desert ladies) |
| Khand (Variags) | battania | 56 | bio-driven (Variag cavalry + ladies) |
| Easterlings/Rhûn | khuzait | 71 | bio-driven (Wainrider/horsearcher archetypes) |
| Umbar Corsairs | umbar | 10 | Ar-Gimilkhâd line (Black Númenórean corsair lords) |
| Shaghana | shaghana | 9 | TAOM-invented southern lords (default archetypes) |
| Abanissa | abanissa | 8 | TAOM-invented coastal lords (default archetypes) |

**New archetypes added** (on top of the 10 Gondor base archetypes):
- `dale_lord` / `dale_bowman` — Northmen bowmen of Esgaroth
- `dwarf_king` / `dwarf_lord` / `dwarf_warrior` / `dwarf_lady` / `dwarf_young` — Erebor + Iron Hills; high TwoHanded + Crafting + Engineering
- `elf_king` / `elf_queen` / `elf_lord` / `elf_warrior` / `elf_archer` / `elf_lady` / `elf_young` — Centuries-trained masters, Bow + Charm + Tactics all 270+
- `orc_chieftain` / `orc_warrior` / `orc_berserker` / `orc_scout` / `orc_warg` / `orc_female` — Mordor / Dol Guldur / Gundabad / Isengard; Honor/Mercy negative, brutal combat
- `nazgul` — Ringwraith tier; everything 270+, Charm 280 (terror), Mercy -2
- `black_numenorean` / `bn_sorceress` — Fallen Númenor: Charm + sorcery + statecraft + combat
- `dunland_warrior` / `dunland_raider` / `dunland_brenin` — Norse-themed hillfolk
- `haradrim_lord` / `haradrim_cav` / `mumak_rider` / `desert_lady` — Scarlet-and-gold southrons
- `variag_lord` / `variag_lady` — Slavic-Mongol cavalry
- `easterling_lord` / `easterling_archer` / `easterling_lady` — Rhûn wainriders + horse-archers
- `corsair_lord` / `corsair_captain` — Umbar pirate fleet
- `rider` / `shieldmaiden` / `horse_breeder` — Rohan-specific (already in Gondor pass)

**Race-aware default archetypes**: `default_archetype` now branches on `culture_data.race` (`man`/`dwarf`/`elf`/`orc`/`uruk_hai`/`nazgul`). Elves never treat low age as "young" (placeholder ages, immortal). Dwarves use combat-heavy defaults regardless of age (250-yr lifespan).

**Power thresholds respected**:
- Most adult lords cap 200-270 in their specialty
- Canonical peak Tolkien heroes push 280-295 (Boromir 295 OneHanded, Imrahil 290, Legolas 295 Bow, Glorfindel 295 OneHanded)
- Three characters hit 300: Galadriel (Charm 300, Leadership 300), Denethor (Steward 300) — all justified by lore (Galadriel is ~8000 years old; Denethor was greatest Steward)
- Leadership > 275 (party-size bonus territory) reserved for: Imrahil, Boromir, Denethor, Théoden, Éomer, Erkenbrand, Dáin II, Elrond, Galadriel, Celeborn, Nazgûl

**Coverage check**: `lords.xml` 566 NPCs with populated skills + `lords.xslt` 384 templates with populated skills = **950 total NPCs with lore-driven skills**. Pre-existing children skipped.

Applied via the same generalized [`tools/apply_culture_skills_traits.py`](tools/apply_culture_skills_traits.py) (one script, `--culture <name>` flag, per-culture canonical-override dicts).

**Files modified:**
- [Main/_Module/ModuleData/characters/lords.xml](Main/_Module/ModuleData/characters/lords.xml)
- [Main/_Module/ModuleData/lords.xslt](Main/_Module/ModuleData/lords.xslt)
- [tools/apply_culture_skills_traits.py](tools/apply_culture_skills_traits.py) — extended with 14 new cultures + 25 new archetypes

**Save-compat:** see prior Gondor entry — applies the same way; new campaigns see new values, existing baked-in hero stats unchanged.

**Not-tested:** in-game smoke test of named heroes (Théoden, Éomer, Boromir, Dáin, Thranduil, Elrond, Galadriel) and one representative orc chieftain (Uglûk/Grishnâkh). Mandatory before ship.

**Research:** Tolkien Gateway for every culture's canonical roster — `Sources` per culture documented in the session transcript.

### feat(gondor-lords): lore-driven skills + traits for 118 Gondor NPCs

Filled in skills and traits for every Gondor adult lord across both XML sources. Continuation of the Gondor Lord Review begun 2026-05-26 (Amrothos clan + 11 culture flips + 24 body keys).

**Scope:** 83 adults in `Main/_Module/ModuleData/characters/lords.xml` + 35 canonical lord templates in `Main/_Module/ModuleData/lords.xslt`. 7 child NPCs skipped (age <14). Total: 118 NPCs received fresh `<skills>` and `<Traits>` blocks.

**Approach:**
- **10 archetypes** as the base layer: `lord` / `knight` / `ranger` / `lady` / `matriarch` / `elder_lord` / `young_lord` / `young_lady` / `steward` / `errand_rider`. Each archetype is a tuned 18-skill + 8-trait template targeting the typical Bannerlord-female bias (Steward/Charm/Medicine high, combat low) for ladies, and the canonical military-leader profile for lords.
- **~25 canonical Tolkien overrides** layered on top — explicit per-NPC skill/trait sets for Imrahil (greatest knight, OneHanded 290 / Leadership 290), Boromir (Captain-General, OneHanded 295 / Leadership 285 / Athletics 285), Faramir (Ranger Captain, Bow 275 / Scouting 290 / Mercy +2), Denethor (Steward 300 / Charm 270, Mercy -1 / Authoritarian +2), Forlong "the Fat" (Polearm 255 / Athletics reduced for girth), Hirluin "the Fair" (balanced + Charm 250), Angbor "the Fearless" (Valor +2 / OneHanded 265), Golasgil (coastal lord with Trade 230 / Bow 225), Duinhir (Morthond archer-king, Bow 290), plus Imrahil's family, Húrioneth, Nemos, Hirgon, Borhador, and their wives.
- **Auto-archetype inference** for the remaining ~80 supplementary lords using a keyword scan against the TAOM bio text in `heroes.xslt` ("ranger" → ranger, "errand-rider" → errand_rider, "knight" / "rides with" → knight, etc.) combined with gender and age (60+ female → matriarch; 14-25 male → young_lord).
- **Power thresholds respected:** most adults cap 200-270 in their specialty; canonical peak heroes push 280-295. Leadership above 275 deliberately reserved for Imrahil / Boromir / Denethor (each grants meaningful party-size / inspirational bonuses).
- **Children unchanged** — toddler stat blocks preserved.

Applied via single-pass Python script [`tools/apply_gondor_skills_traits.py`](tools/apply_gondor_skills_traits.py) — kept in repo for future tuning passes and as a pattern for other cultures (Rohan, Dale, etc.).

**Files modified:**
- [Main/_Module/ModuleData/characters/lords.xml](Main/_Module/ModuleData/characters/lords.xml) — 83 NPC `<skills>` + `<Traits>` blocks rewritten
- [Main/_Module/ModuleData/lords.xslt](Main/_Module/ModuleData/lords.xslt) — 35 canonical Gondor templates (Denethor / Boromir / Faramir / Imrahil et al.) populated
- [tools/apply_gondor_skills_traits.py](tools/apply_gondor_skills_traits.py) — new

**Save-compat:** skill values are read on hero creation. Existing campaigns: hero skills are baked into the save file, so this change affects NEW campaigns and any hero NOT yet realised at session start. Wandering companions and reserved NPCs that haven't spawned yet will use the new values.

**Not-tested:** in-game encyclopedia walk + skirmish-test the canonical lords' army-size + medic-tent perks (recommended before ship).

**Constraint:** Bannerlord running during apply locked `0Harmony.dll` → couldn't run `./build.ps1`. XML well-formedness verified independently; no C# touched.

**Research:** Tolkien primary canon (RotK Appendix, UT) for Boromir/Faramir/Denethor/Imrahil; Tolkien Gateway for the Outlands lords (Forlong, Hirluin, Angbor, Golasgil, Duinhir).

### feat(knowledge-base): Karpathy-style layered wiki architecture (ADR-010, 4 phases)

Adopt a self-maintaining knowledge-base architecture on top of the existing `docs/` tree, modeled on Karpathy's "LLM Knowledge Bases" pattern (raw → compiled wiki → Q&A → linting). Four phases, all shipped together:

**Phase 1 — `docs/INDEX.md`** — curated topical map across all 70 feature docs, 10 ADRs, 79 reviews, 13 ai-includes, and 16 migration docs. Grouped by domain (Character/Race, Combat/AI, Career/Progression, Equipment/Armor, Sieges, Economy/Settlements, Faction/Diplomacy, Sandbox/Lifecycle/UI, Infrastructure) plus quickstart paths, cross-cutting concerns where memory and docs intersect, and a glossary section. Wired into CLAUDE.md's Doc Lookup as the entry point. Architecture decision recorded in [`docs/adrs/010-knowledge-base-architecture.md`](docs/adrs/010-knowledge-base-architecture.md) with 4 alternatives considered (Obsidian, docs-site generator, INDEX-only, treat-.claude/-as-KB).

**Phase 2 — `tools/lint_docs.py` + `/lint-docs` skill** — automated doc-health linter. Checks: dead markdown links (with code-fence + inline-code + file:// + whitespace-target filtering, exempting codex transcripts, TEMPLATE.md, and `docs/archive/`); stale version refs (`1.3.15`, `1.3.x`, `Bannerlord 1.3` outside `docs/migration/`/`docs/archive/` and outside rca-/codex-adversarial- filenames); orphan feature docs (no inbound references); missing feature docs (Main/Features/<X>/ with no docs/features/<x>.md). Initial baseline report filed at [`docs/reviews/doc-lint-2026-05-27.md`](docs/reviews/doc-lint-2026-05-27.md): 38 dead links + 184 stale-version refs + 0 orphans + 1 missing doc (MissionDiagnostic).

**Phase 3 — `tools/build_backlinks.py`** — symmetric link footers. Walks `docs/` and writes a `## Referenced by` HTML-comment-delimited block to every doc with inbound references; removes the block when refs drop to zero; idempotent re-runs. First apply touched **150 files**, adding inbound-reference visibility from feature docs → INDEX/ADRs/reviews and the inverse. Same exemption rules as the lint (codex transcripts, TEMPLATE.md, `docs/archive/` are neither footer-targets nor reference-sources). Imports its helpers from `tools/lint_docs.py` to keep the link-parsing logic single-sourced.

**Phase 4 — `docs/raw/` + `docs/research/` + `/knowledge-compile` skill** — ingest layer for unstructured source materials (Tolkien lore clippings, decompiled engine notes, external mod design refs) plus a compile layer that LLM-summarizes raw → wiki nodes with summary/sources/cross-references/key-claims/open-questions. Skill runs `tools/compile_research.py` as a deterministic inventory probe (file listing + keyword-driven cross-ref candidates from existing docs), then Claude drafts the research node, then lint + backlinks regenerate. Large binary policy in `.gitignore` (PDFs, mp4, mov, zip, tar.gz under `docs/raw/` are user-local only).

Links remain markdown `[text](path)` throughout; Obsidian `[[wikilinks]]` migration was considered and rejected on cost (290+ files) and renderer compatibility (GitHub treats `[[X]]` as plain text). Memory files retain their existing `[[name]]` syntax since the auto-memory system supports it.

Out of scope (deferred indefinitely): vector store / RAG layer, CHANGELOG → wiki conversion, automated rewriting of existing feature docs, Karpathy-style "naive search engine" CLI (`Grep` over `docs/` is fast enough at this scale).

Files added (new):
- `docs/INDEX.md`, `docs/adrs/010-knowledge-base-architecture.md`, `docs/reviews/doc-lint-2026-05-27.md`
- `docs/raw/README.md`, `docs/research/README.md`
- `tools/lint_docs.py`, `tools/build_backlinks.py`, `tools/compile_research.py`
- `.claude/skills/lint-docs/SKILL.md`, `.claude/skills/knowledge-compile/SKILL.md`

Files modified: `CLAUDE.md` (Doc Lookup section); `docs/adrs/README.md` (ADR table row); `.gitignore` (docs/raw binaries); 150 docs got `## Referenced by` footers.

Constraint: bot-authored footers will increase PR diff size when re-generated; the HTML-comment delimiters make them visually skippable in review.
Research: Karpathy's "LLM Knowledge Bases" post (raw → compiled wiki → Q&A → linting → search → finetune); existing TAOM structure (71 feature docs, 96 memory files, REVIEW-GUIDE.md adversarial loop) already matched ~80% of the pattern.

## 2026-05-26

### fix(gondor-lords): Amrothos clan + 11 Mordor-mis-tagged Gondor lords + 24 female body keys

Consolidated Gondor noble-roster review. Four bug classes addressed in one pass:

1. **Amrothos clan re-assigned to Imrahil's House (Dol Amroth)** — `heroes.xslt` template for `lord_1_24` was inheriting vanilla `Faction.clan_empire_west_1` (Stewards), now overridden to `clan_empire_west_2` to match his canonical place beside his brothers Elphir + Erchirion. Same `faction` override pattern as the existing Faramir + Lothwen templates; the original Amrothos template forgot it.

2. **Three Gondor noblewomen renamed + culture-corrected** — `lord_1_40_1` Catella → Lindariel, `lord_1_45_1` Vanyalos → Berethiel, `lord_1_46_1` Seorgys → Thorwen. All three were tagged `culture="Culture.mordor"` in `lords.xml` (likely block-copied from a Mordor template) — flipped to `Culture.gondor`. The localized loc-key values in `taom_xslt_strings.xml:758-760` were already correct; only the inline `{=KEY}DEFAULT` fallback values needed updating. Tolkien-name research summarized in [`~/.claude/plans/gondor-lord-review-amrothos-zippy-frost.md`](file).

3. **Tier 2 culture sweep — 8 additional Gondor lords flipped from Culture.mordor → Culture.gondor**: Sophalia, Jephalia, Popilia, Arytha, Brandir, Borlong, Arador, Arvedui (all in `lord_1_*_*` IDs whose vanilla parent faction is `clan_empire_west_*`).

4. **Female body diversity — 24 NPCCharacters got fresh face keys** — user-supplied curated 15 Gondor-female face keys distributed round-robin alphabetical across 23 adult Gondor females. `Lord_EW_1_1` Ciriel additionally redirected to share Dorwen's post-Fix-4 key (#7) so the two visually match per user request. Body key only — each NPC's existing `age`/`weight`/`build` preserved so older matriarchs (Lothwen 72, Nauriel 60, Laswen 53) keep their age weathering. Child NPCs (Lothiriel 1, Ancalimú 6, Jephalia 2) skipped to avoid baby-as-adult visual bug.

Applied via single-pass Python script [`tools/apply_gondor_lord_review.py`](tools/apply_gondor_lord_review.py) — kept in repo for future re-runs (e.g. extending the body-key pool, adding more cultures).

**Files modified:**
- [Main/_Module/ModuleData/heroes.xslt](Main/_Module/ModuleData/heroes.xslt) — `lord_1_24` template gains `faction` override
- [Main/_Module/ModuleData/characters/lords.xml](Main/_Module/ModuleData/characters/lords.xml) — 29 NPCCharacters touched (24 body keys, 11 culture flips, 3 name renames; some overlap)
- [tools/apply_gondor_lord_review.py](tools/apply_gondor_lord_review.py) — new

**Save-compat:** name/culture changes are cosmetic — no save-data impact. Body key changes re-render face on next FaceGen pass; existing cached face meshes may need an in-game encyclopedia visit to refresh.

**Not-tested:** in-game encyclopedia smoke test (mandatory before ship — see plan file Verification section).

**Research:** Tolkien Gateway (Berúthiel, Forlong, Lossarnach), web research on canonical wife names for Forlong (not named in canon).

**Constraint:** `re.sub` with `r'\1' + key_starting_with_0` corrupts output because Python treats `\10` as group-10 backref (FIRST attempt corrupted 24 BodyProperties lines; reverted via `git checkout`). Fix: use lambda replacement `lambda m: m.group(1) + new_k + m.group(2)`.

### feat(taom-map): rename 345 placeholder villages with lore-appropriate names across all 15 regions

Replaced every `Castle Village <PREFIX>X_Y` / `Village <PREFIX>X_Y` placeholder display name on the campaign map with a lore-appropriate name in the linguistic idiom of its parent culture. Done in two passes: Dol Guldur (17) + Mirkwood (17) interactively, then 311 across the remaining 13 regions in one bulk Python pass.

**Scope:** display-name only. Settlement IDs untouched (save-compatible), positions untouched, bound-village relationships untouched. Pure cosmetic / UX change.

**Files modified (EXTERNAL — not in this repo):**
- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml` — 345 `name="{=key}DEFAULT"` defaults updated
- `E:\Steam\...\Modules\TAOM_Map\ModuleData\Languages\<LANG>\loc_settlements.xml` × 12 — 345 `<string ... text="..."/>` entries per file (BR/CNs/CNt/DE/FR/IT/JP/KO/PL/RU/SP/TR all carry the same Tolkien-language proper noun — invented names don't translate)
- **Total: 4,485 string replacements across 13 files**

**Linguistic idiom per region** (full table in `docs/reference/taom-map-settlement-naming.md`):

| Region | Idiom | Example |
|---|---|---|
| Dol Guldur, Mordor, Gundabad | Black Speech / Uruk | Ghâshnak, Gûl-mauz, Mazūg-mîr |
| Mirkwood, Lothlórien, Rivendell, Gondor, Isengard | Sindarin / Silvan | Annon Sirith, Mallorlas, Anduinmir |
| Rohan | Anglo-Saxon | Seolforhamm, Sealtburg, Hwætland |
| Erebor | Khuzdul + Old Norse mix | Kibilbund, Frôstkorn, Azanrandol |
| Dunland | Welsh / Brythonic | Caer Dunwyr, Lhan Penrhos |
| Khand, Far Harad, Umbar, Rhûn | Variag / Mongol-Arabic / Adunaic | Khond-Vol, Bara-Mukh, Bej-Phazân, Mistrand-Krish |

**Tooling produced (reusable for future map rename passes):**
- [`tools/Apply-MapVillageNames.py`](tools/Apply-MapVillageNames.py) — bulk renamer with name-mapping dict + uniqueness gate. Idempotent, preserves UTF-8/CRLF/BOM. Run from repo root.

**Documentation:**
- [`docs/reference/taom-map-settlement-naming.md`](docs/reference/taom-map-settlement-naming.md) — full methodology, region prefix → culture → idiom table, suffix vocabulary per language, save-compatibility guarantees, glyph compatibility notes, known pre-existing duplicate names worth fixing later.

**Critical discovery for future sessions** (codified in memory + docs): TAOM repo's `Main/_Module/ModuleData/settlements.xml` is a STALE SHADOW last touched 2026-04-06 — NOT registered in `Main/_Module/SubModule.xml`, NOT loaded by the engine. The live source is the external TAOM_Map module. Existing PowerShell tools (`Apply-SettlementNames.ps1`, `Generate-Settlements.ps1`) target the stale shadow.

Tests: N/A (display-name change, no code path affected). XML well-formedness verified on all 13 files via `ElementTree.parse`. Zero placeholder substrings remain. No duplicate display names introduced.
Save-compat: safe — IDs and bound-village relationships unchanged.
Not-tested: in-game spot-check across all 15 regions (deferred — user verification needed).

### feat(career-system): re-enable cave_troll_master as Gundabad Berserker (Infantry)

Reverses the 2026-05-14 WIP disable + matching 2026-05-19 JSON cleanup (commit `9c8d545`). Three coordinated edits restore the career across the three locations the disable touched:

1. [`taom_careers.xml`](Main/_Module/ModuleData/career_system/taom_careers.xml) — unwrap the `<Career id="cave_troll_master">` element from its `<!-- DISABLED ... -->` block. Display name already reads "Gundabad Berserker" from the prior rename. Description updated to clarify Gundabad origin.
2. [`CareerSystemIoC.cs`](Main/Features/CareerSystem/CareerSystemIoC.cs) line 143 — uncomment `["cave_troll_master"] = CareerArchetype.Infantry,` so the executor registry + archetype-service lookup recognise it.
3. [`career_menu.json`](Main/_Module/ModuleData/charactercreation/career_menu.json) — restore the entry between `goblin_sniper` and `warg_pack_leader`, with the original `TwoHanded + Leadership / Vigor` skill assignment (recovered from commit `9c8d545`'s removed diff).

Choice trees ([`taom_career_choices.xml`](Main/_Module/ModuleData/career_system/taom_career_choices.xml) lines 6771+) and ability template (`cave_troll_master_ability`) were never removed — they remained intact through the disable, so no further authoring needed.

Gundabad now has 3 selectable careers in CC: **Gundabad Berserker** (Infantry, newly enabled), Gundabad Orc Hunter (Ranged), Gundabad Fell Warg Pack Leader (Cavalry).

Tests: 2520/2522 passing — `CareerCultureCoverageTests.EveryJsonEntry_HasMatchingCareerInXml` + `EveryXmlEntry_HasMatchingJsonEntry` re-balanced. Sister troll career (`far_harad_halftroll`) remains disabled per the 2026-05-14 note.
Save-compat: existing saves unaffected. New Gundabad campaigns see the third career option.
Not-tested: in-game spot-check (Gundabad CC → 3-option menu → confirm Gundabad Berserker spawns with TwoHanded + Leadership focus).

### docs(culture-authoring): repeatable end-to-end process guide from the Dale session

Captures the patterns that emerged from the 11-commit Dale culture session as a reusable workflow for future cultures.

- **New**: [`docs/ai-includes/new-culture-authoring.md`](docs/ai-includes/new-culture-authoring.md) — phase-by-phase guide. Prerequisites → armor manifest from `.tpac` → armor generator + Armory registration → lore research → troop tree design → troop generator + SubModule registration → XSLT culture binding (with the full CultureObject attribute list per Codex #227) → 9 party templates → Volunteer recruitment pool with the 4-level lookup priority → validation → `/verify` + `/deep-review` + `/review-codex` + RCA → 8 known iteration loop kinds. Includes the bronze/silver color convention, "Royal goes last" naming rule, per-tier explicit-suffix armor helpers, light/heavy split via Y-fork pattern, save-compat rules covering tier shifts and display-name desync, and a file reference table.
- **Modified**: [`.claude/rules/troops.md`](.claude/rules/troops.md) — added 4 new sections: Volunteer Recruitment Lookup Priority (Conditional > Settlement > Clan > Culture), Per-Tier Explicit Armor Pattern (with the helper table), "Royal Goes Last" Naming Convention, Cross-Reference Vanilla Weapon Stats Before Tier-Ordered Picks (per the bow-tier-inversion Codex finding). Save-compat rules extended to cover tier shifts + display-name desync.
- **Modified**: [`CLAUDE.md`](CLAUDE.md) Doc Lookup — added link to the new guide.
- **Modified**: [`docs/features/dale.md`](docs/features/dale.md) — added a banner pointing at the new authoring guide so future readers find the repeatable process.

No code or game-data changes.

### fix(career-system): rename Gundabad careers to Tolkien-flavored display names

Same two-file pattern as the prior renames.

| Career ID | Was | Now |
|-----------|-----|-----|
| `cave_troll_master` | Cave Troll Master | Gundabad Berserker |
| `goblin_sniper` | Goblin Sniper | Gundabad Orc Hunter |
| `warg_pack_leader` | Warg Pack Leader | Gundabad Fell Warg Pack Leader |

Two-file rename in [`taom_careers.xml`](Main/_Module/ModuleData/career_system/taom_careers.xml) lines 891/912/932 + [`taom_career_strings.xml`](Main/_Module/ModuleData/taom_career_strings.xml) lines 87/89/91. Note: `cave_troll_master` ID retained even though the display name no longer references trolls — it's still disabled in [`CareerSystemIoC.cs`](Main/Features/CareerSystem/CareerSystemIoC.cs) (the original `DISABLED 2026-05-14: Troll careers WIP` line), so the Infantry slot will only appear in-menu once that registration is uncommented. ID rename can follow if/when the user wants it.

Files: 2 XML files, 3 lines each. No code change.

### fix(career-system): rename Rivendell careers to Ñoldor-themed display names

Same two-file pattern as the prior renames.

| Career ID | Was | Now |
|-----------|-----|-----|
| `blade_dancer` | Blade Dancer | Ñoldor Blademaster |
| `elven_archer` | Elven Archer | Ñoldor Sentinel |
| `rivendell_knight` | Rivendell Knight | Ñoldor Knight |

Two-file rename in [`taom_careers.xml`](Main/_Module/ModuleData/career_system/taom_careers.xml) lines 634/654/674 + [`taom_career_strings.xml`](Main/_Module/ModuleData/taom_career_strings.xml) lines 63/65/67. Localization keys unchanged. The `Ñ` character is U+00D1 — both files are UTF-8 (CRLF) per the project convention so the encoding round-trips cleanly.

Files: 2 XML files, 3 lines each. No code change. Save-compat: career IDs unchanged.

### fix(career-system): rename Dunland careers to Tolkien-flavored display names

Same two-file pattern as the Dale + Isengard renames. Player-facing display names only — career IDs unchanged so all wiring keeps resolving.

| Career ID | Was | Now |
|-----------|-----|-----|
| `avanc_luth_raider` | Avanc-lúth Raider | Dunlending Champion |
| `wolfskin_hunter` | Wolfskin Hunter | Dunlending Archer |
| `clanguard_rider` | Clanguard Rider | Dunlending Outrider |

Two-file rename in [`taom_careers.xml`](Main/_Module/ModuleData/career_system/taom_careers.xml) lines 228/248/268 + [`taom_career_strings.xml`](Main/_Module/ModuleData/taom_career_strings.xml) lines 25/27/29. Localization keys unchanged; same pending translation re-run.

Files: 2 XML files, 3 lines each. No code change. Save-compat: career IDs unchanged.

### fix(career-system): rename Isengard careers to Tolkien-flavored display names

Same pattern as the Dale rename — player-facing display names only, career IDs (`uruk_berserker` / `uruk_crossbow` / `warg_scout`) unchanged so all wiring keeps resolving identically.

| Career ID | Was | Now |
|-----------|-----|-----|
| `uruk_berserker` | Uruk Berserker | Uruk-Hai Berserker |
| `uruk_crossbow` | Uruk Crossbow | Uruk-Hai Crossbowman |
| `warg_scout` | Warg Scout | Warg Rider |

Two-file rename in [`taom_careers.xml`](Main/_Module/ModuleData/career_system/taom_careers.xml) lines 826/846/866 + [`taom_career_strings.xml`](Main/_Module/ModuleData/taom_career_strings.xml) lines 81/83/85. Localization keys unchanged so existing translations in the 12 language files still resolve safely (now stale relative to English — same pending re-run as ff75c93 + Dale rename).

Files: 2 XML files, 3 lines each. No code change. Save-compat: career IDs unchanged.

### fix(career-system): rename Dale careers to Tolkien-flavored display names

Player-facing display names only — career IDs (`dale_guardsman` / `dale_marksman` / `dale_outrider`) stay the same so all archetype mappings, choice trees, ability templates, and equipment rosters keep resolving identically.

| Career ID | Was | Now |
|-----------|-----|-----|
| `dale_guardsman` | Dale Guardsman | Dalian Master Swordsman |
| `dale_marksman` | Dale Marksman | Barding Marksman |
| `dale_outrider` | Dale Outrider | Dalian Cavalier |

Two-file rename (single source of truth pattern): [`taom_careers.xml`](Main/_Module/ModuleData/career_system/taom_careers.xml) lines 506/526/546 (`display_name="{=key}fallback"`) + [`taom_career_strings.xml`](Main/_Module/ModuleData/taom_career_strings.xml) lines 51/53/55 (the matching `<string>` registry entry). Localization keys unchanged so existing translations in the 12 language files still resolve safely — they're now stale relative to English but the game won't crash; the translation pipeline (`tools/translate_with_claude.py`) needs to re-run to propagate, tracked with the same ongoing follow-up as the 50 ability tooltip rewrite (commit `ff75c93`).

Files: 2 XML files, 3 lines each. No code change. No new items. No tests needed (data only).
Save-compat: career IDs unchanged → existing saves resolve careers identically.
Not-tested: in-game spot-check that career menu + encyclopedia show the new names.

### feat(native-skin-fixes): adopt + port NativeSkinFixes into TAOM (v1.4.5, in-repo, pattern-scanning)

Pulls the entire **NativeSkinFixes** mod (covers_head morph fix + hair cloth + beard cloth) into TAOM as a first-party feature. Replaces the inert vendored `TAOM.NativeSkinFixes.dll` (v1.4.0 RVAs, no managed loader committed) with a full integration:

- **C++ source vendored in-repo** at `Dependencies/NativeSkinFixes.NativeHooks/` — six `.cpp` + six `.h` + MinHook 1.3.4 binaries + `.vcxproj` + `Build.ps1`. The `.vcxproj` writes `TAOM.NativeSkinFixes.dll` directly into `Main/_Module/bin/Win64_Shipping_Client/`. No external source location, no "lives outside this repo" footnote.
- **Hardcoded RVAs replaced with byte-pattern scanning.** New `SignatureScanner.{h,cpp}` parses IDA-style hex patterns (`"48 89 5C 24 ? 48 89 74 24 ?"`) and walks the loaded `TaleWorlds.Native.dll` image at hook-install time. The 7 target signatures (3 hook targets + 4 helpers — `add_skin_meshes`, `cloth_factory`, `render_list_build`, `AddToList`, `GpuInit`, `HasClothData`, `NotifyPhysics`) live in a single `Signatures.h` registry. The original `notifyPhysics = clothFactory - 0xF6A0` inter-function offset (fragile across builds) is gone — `NotifyPhysics` now has its own scanned signature.
- **C# wrapper inlined into TAOM.dll** under `Main/Features/NativeSkinFixes/` (4 files: `NativeSkinFixesInstaller.cs` + 3 interop classes). Loads from `TaomSubModule.OnBeforeInitialModuleScreenSetAsRoot`, uninstalls from `OnSubModuleUnloaded`. Editor-mode skip preserved (`wEditor` substring check). Localized boot banner via new `taom_nativeskinfixes_loaded` key in `taom_module_strings.xml`.
- **Unified logging** — replaces the original mod's two scattered log paths (`Modules/NativeSkinFixes/HairClothHook.log` + `C:\ProgramData\...\NativeSkinFixes_renderlist.log`) with a single `%USERPROFILE%\Documents\Mount and Blade II Bannerlord\Logs\TAOM\NativeSkinFixes.log` consistent with other TAOM logging.
- **Graceful degradation everywhere.** Missing DLL, missing export, unscanned pattern, or pattern miss — each fails individually with a logged warning and the game continues vanilla. No NRE, no crash, no boot block.
- **TDD:** 8 unit tests in `TAOM.Tests/Features/NativeSkinFixes/NativeSkinFixesInstallerTests.cs` cover editor-mode predicate (null / empty / normal / editor / mixed case / false-positive guard) and the localization-key wiring. All 8 pass. The native interop layer itself can't be unit-tested.
- **Feature doc:** [`docs/features/native-skin-fixes.md`](docs/features/native-skin-fixes.md) covers each hook's bug-fixed, the scanner architecture, how to rebuild, and the IDA workflow for authoring patterns when a Bannerlord patch breaks scanning.
- **Open follow-up:** the 7 byte patterns ship as `<PATTERN_TBD>` placeholders. The scanner architecture is verified end-to-end (compiles, tests green, scanner returns 0 + logs cleanly on stub patterns). Authoring the v1.4.5 patterns is a one-time ~30 min IDA session — see the feature doc's "Pattern authoring" section. Until authored, hooks log "pattern not authored for this build (stub)" and stay inert.

Build: 0 errors. Tests: 8/8 NativeSkinFixes tests pass.

Files touched: 13 new C# / C++ files, `Main/SubModule.cs` (3 lines added), `taom_module_strings.xml` (2 lines), feature doc + dr3-maintenance update + CLAUDE.md update.

Not-tested: byte-pattern scanner against live `TaleWorlds.Native.dll`, MinHook trampoline install, the 3 hook bodies (all require a hosted Bannerlord process).

### chore(deep-review,docs): add C++ checks to deep-review skill + CLAUDE.md native port discipline

Post-mortem on the NativeSkinFixes port (`docs/reviews/rca-native-skin-fixes-port-2026-05-26.md`) found that the deep-review skill's Agent 1 (Standards) and Agent 3 (Efficiency) prompts were C#-only — three of four review findings on a hybrid C#/C++ changeset only got caught because I ad-hoc-customized Agent 3's prompt for this session. Memory-only prevention has a poor track record (the same category of bug ships again when the memory isn't in the active context window), so the user directed the skill itself be hardened.

- **`.claude/skills/deep-review/SKILL.md`** — Agent 1 gains check #9b "C++ Native Hook Standards" + Agent 3 gains check #15 "C++ HOT-PATH CHECKS", both conditional on `.cpp`/`.h` files being in the changeset (skipped on pure-C# reviews). Covers hot-path logging gates, SEH filter narrowness, atomic counter usage, SRWLock reader/writer balance, unbounded memory iteration, heap allocations, TLS abuse, `extern "C"` blocks, calling convention symmetry.
- **`CLAUDE.md`** — new "Native C++ port discipline" section under "Working Discipline" with a 6-point pre-commit checklist for any future C++ port. References the RCA + the three feedback memories.
- **Feedback memories** — three new entries indexed in MEMORY.md: `feedback_native_port_hot_path_audit.md`, `feedback_seh_filter_specificity.md`, `feedback_degraded_state_distinct_banner.md`.

Prevention layering: skill prompt (mechanical check) + feedback memory (narrative why) + CLAUDE.md discipline (project-level documentation). Each layer covers a failure mode of the other two — memory evicts from context windows, skill prompts get out of date, project docs become wallpaper. Three layers redundant by design.

### feat(dale,armory): Lake-Town recruitment override + Armoury helmet hair_cover_type bulk fix

Two independent user-directed changes shipped together.

**Lake-Town settlement-specific recruitment pool** — `town_S1` (Lake-Town per user clarification; vanilla "Dale") gets a unique 2-troop pool, all other Sturgia settlements still use the culture default:
- Lake-Town Peasant (`dale_recruit`) — 9
- Dalian Levy (`dale_squire`) — 1

New `InitializeDaleSettlements()` static initialiser added to `VolunteerRecruitmentService`. `SettlementMap["town_S1"]` is checked BEFORE the culture pool, so Lake-Town recruits ~90% Peasants and ~10% Levies regardless of clan ownership.

3 new unit tests: roll 0 → Peasant; roll 9 → Levy (rare terminal slot); `town_S2` fallthrough confirms the override is town_S1-only.

**Armoury helmet hair_cover_type bulk → "all"** — `LOTRLOME_Armory/ModuleData/LOTRLOME_items/**/head_armors.xml` across all 18 culture folders. Was a mix of `all` (560), `type2` (279), `type1` (115), `none` (11) = 965 helmets total. Now uniformly `all`. Also updated `tools/generate_dale_armor.py` so future regeneration defaults to `all`.

Verification: build green; 9/9 tests pass (4 Dale culture + 3 Rohan clan + 2 new Lake-Town settlement); helmet bulk-replace verified zero non-`all` values remain (`grep -roh hair_cover_type=` shows 965/965 = "all").

### feat(dale): single-variant armor, Riverman→infrantry, cavalry color swap, Veteran Northman Scout

User-directed cleanup pass. 34 → 35 troops (added Dalian Veteran Northman Scout); recruitment pool replaced.

**Archer + Crossbow lines** — removed the per-troop armor variation (overlap a01+a02, etc.). Now strictly 1 armor variant per tier; both rosters use the same armor mesh and only the weapons vary:
- Archers: `a01` (T4 Yeoman) → `a02` (T5 Bowman) → `a03` (T6 Marksman) → `a04` (T7 Barding).
- Crossbowmen: `b01` (T4) → `b02` (T5 Veteran) → `b03` (T6 Master) → `b04` (T7 Royal).

**Riverman line** — armor mesh swapped from `mariner` (Lake-Town look) to `infrantry` silver `b01-b03` (Dale royal look), 1 variant per tier:
- T4 Dalian Riverman → `b01`; T5 Dalian Shipman → `b02`; T6 Dalian Mariner → `b03`.

**Cavalry restructure** (light + heavy colors swapped — heavy is now bronze, light is silver; the user spec inverts the previous convention):
- T4 `dale_outrider` "Dalian Merchant Guard": chivlary **`a01` + `b01`** (mixed split-point — one bronze + one silver roster).
- LIGHT branch (silver chivlary, 2 tiers):
  - T5 `dale_knight` "Dalian Northman Scout" → `b02`.
  - **NEW** T6 `dale_veteran_northman_scout` "Dalian Veteran Northman Scout" → `b03` (roster A) + `b04` (roster B). Skill curve `s_veteran_northman_scout_t6` — between Northman Scout (T5) and Heavy Cavalry (T6), with higher Bow reflecting scout flavor. Terminal.
- HEAVY branch (bronze chivlary, 3 tiers, 1 variant per troop):
  - T5 `dale_royal_cavalier` "Dalian Cavalry" → `a02`.
  - T6 `dale_kinsman_of_eorl` "Dalian Heavy Cavalry" → `a03`.
  - T7 `dale_kings_guard` "Dalian King's Guard" → `a04`.

**Volunteer recruitment pool** for Dale (`VolunteerRecruitmentService.CultureMap["sturgia"]`) — replaced. New pool (total weight 10):
- Dalian Levy (`dale_squire`) — 4 (the most common recruit; royal-line entry)
- Dalian Riverman (`dale_riverman`) — 1
- Dalian Militia (`dale_man_at_arms`, NOT the Lake-Town Militia `dale_militia`) — 1
- Dalian Yeoman (`dale_bowman`) — 1
- Dalian Crossbowman (`dale_crossbowman`) — 1
- Dalian Merchant Guard (`dale_outrider`) — 1
- Lake-Town Peasant (`dale_recruit`) — 1

Pool surfaces one representative entry troop for every branch + the Lake-Town entry. Replaces the prior pool that included `dale_militia` (Lake-Town Militia) and `dale_footman` and weighted Lake-Town Peasant at 5.

4 Dale unit tests updated to match the new pool order (roll 0 → squire; roll 4 → riverman; roll 9 → Peasant terminal; settlement-fallthrough → squire). 7/7 tests pass (4 Dale + 3 Rohan).

**Party template**: `kingdom_hero_party_dale_template` gains a `dale_veteran_northman_scout` stack (1-3) between Northman Scout and Dalian Cavalry.

Verification: build green, 7/7 tests pass, validator green (Dale: 35 troops, 154 armor refs, 0 missing); spot-check confirmed correct armor mesh per troop after the swap.

Save-compat: All existing IDs preserved; the new `dale_veteran_northman_scout` ID + the recruitment-pool change are additive.

### fix(dale): swap Royal/Master so "Royal" reads as the highest rank

User-directed rename — "Royal" should denote the most-elite tier across all Dale lines.

Crossbow line (pair swap on the line that had them inverted):
- T6 `dale_royal_crossbowman` "Dalian Royal Crossbowman" → "Dalian Master Crossbowman"
- T7 `dale_master_crossbowman` "Dalian Master Crossbowman" → "Dalian Royal Crossbowman"

Great Infantry line (apply the same principle to its T7 terminal):
- T7 `dale_running_river_warden` "Dalian Master Swordsman" → "Dalian Royal Swordsman"

IDs intentionally desynced from display names in 2 places (`dale_royal_crossbowman` now displays "Master Crossbowman"; `dale_master_crossbowman` now displays "Royal Crossbowman"). Save-compat preserved — only display strings changed. Documented in `docs/features/dale.md`.

Verification: 4 Dale tests + 3 Rohan clan tests still pass (7/7); ID-to-display mapping spot-checked.

### feat(dale): explicit per-tier armor (bronze/silver) + Crossbowman line

User-directed armor mapping pass for all royal Dale lines + new ranged sub-line. 30 → 34 troops (4 new crossbowmen).

**Color convention** (per user clarification): `a` suffix = **bronze** mesh; `b` suffix = **silver** mesh. Same shape, different paint.

**Three new explicit-suffix armor helpers** in `tools/generate_dale_troops.py` (parallel to `lake_town_armor_explicit`):
- `chivalry_armor_explicit(suffix)` — chivlary cavalry mesh (chivalry on chest, chivlary elsewhere — Solus's typo split).
- `infantry_armor_explicit(suffix)` — infrantry royal-infantry mesh.
- `archer_armor_explicit(suffix)` — archer mesh; falls back on the missing shoulder variants (a02/a04/b02/b04 → next-lower available, since Solus authored archer shoulders only at a01/a03/a04/b01/b03/b04 — note a04/b04 DO exist for archer shoulders, unlike mariner).

**Armor rewires** (existing troops, IDs unchanged):

| Line | New armor mapping |
|---|---|
| Light cavalry (T4-T5) | chivlary a01/a02 (Merchant Guard) → a03/a04 (Northman Scout) |
| Heavy cavalry (T5-T7) | chivlary b01/b02 (Dalian Cavalry) → b02/b03 (Heavy Cavalry) → b03/b04 (King's Guard); adjacent tiers overlap one variant for continuity |
| Royal Infantry (T4-T7) | "variation per level" — `aNN`+`bNN` per tier (T4: a01+b01, T5: a02+b02, T6: a03+b03, T7: a04+b04) |
| Bow line (T4-T7) | bronze a01-a04 with overlap (T4: a01+a02, T5: a02+a03, T6: a03+a04, T7: a04+a04) |

**NEW Crossbowman line** (4 troops T4-T7, off Dalian Levy; silver archer armor b01-b04):
- T4 `dale_crossbowman` "Dalian Crossbowman" (b01+b02; crossbow_c + bolt_a)
- T5 `dale_veteran_crossbowman` "Dalian Veteran Crossbowman" (b02+b03; crossbow_d + bolt_b)
- T6 `dale_royal_crossbowman` "Dalian Royal Crossbowman" (b03+b04; crossbow_e + bolt_c/d)
- T7 `dale_master_crossbowman` "Dalian Master Crossbowman" (b04+b04 — top silver; crossbow_f + bolt_d/e — T7 terminal)
- Skill curves: Crossbow-primary scaling 90→125→160→195 across T4-T7, with OneHanded sidearm + low Bow. Sturgia noble sword sidearms at higher tiers.

**Dalian Levy** (T3 `dale_squire`) upgrade list extended from 4 → 5 targets: now also upgrades to `dale_crossbowman` (alongside riverman/man_at_arms/bowman/outrider).

**Party templates**:
- `kingdom_hero_party_dale_template`: added 3 crossbowman stacks (crossbowman 3-8, veteran 2-5, royal 1-3).
- `patrol_party_dale_template_level_2`: added veteran crossbowman 2-4.

**Verification**: build green; 7/7 tests (4 Dale culture + 3 Rohan clan) still pass; validator PASS across all 8 cultures (Dale: 35 troops [34+wrapper], 154 armor refs, 0 missing); all 9 new vanilla weapon IDs (`crossbow_c..f`, `bolt_a..e`) verified in SandBoxCore; spot-check confirmed correct chivlary/infrantry/archer + a/b suffix mapping per troop.

Save-compat: All existing IDs preserved (chest mesh changes for existing troops are part of the routine armor rebalance — engine re-picks equipment on next load, no troops disappear from existing parties). The 4 new crossbowman IDs + Dalian Levy's 5th upgrade target are additive.

### feat(rohan): every Rohan clan recruits all 7 basic troops at weight 1

User request: surface every Rohan `is_basic_troop="true"` troop to the player from every Rohan clan, so any clan-bound Rohan settlement can recruit the full T2 lineup regardless of region.

New `InitializeRohanClans()` static initialiser in `VolunteerRecruitmentService` populates `ClanMap` for all 11 Rohan clans (`clan_vlandia_1` through `clan_vlandia_11`) with the 7 Rohan basic troops at weight 1 each:
- `rohan_wold_recruit`, `rohan_westemnet_recruit`, `rohan_eastemnet_recruit`, `rohan_eastfold_recruit`, `rohan_westfold_recruit`, `rohan_westmarches_recruit`, `rohan_edoras_recruit`

The `ClanMap` lookup runs AFTER per-settlement pools (none authored for Rohan yet) and BEFORE the culture-level fallback, so any future per-settlement Rohan flavor still wins. Without per-settlement entries, every Rohan clan-bound settlement now resolves to this uniform 7-way pool.

3 unit tests added covering low-roll (0 → wold_recruit), high-roll (6 → edoras_recruit, terminal slot), and highest-numbered-clan (`clan_vlandia_11` — catches off-by-one in the loop range).

Verification: 3/3 new tests pass; 4 Dale tests still green (no regression in shared static-ctor state); validator green across all 8 cultures.

### feat(dale): Dalian rebrand + cavalry split (Light/Heavy) + King's Guard T7

User-directed naming pass + cavalry restructure. 29 → 30 troops (added Dalian King's Guard at T7).

**11 display renames** (all IDs unchanged; save-compat preserved):
- `dale_squire`: "Dale Levy" → "Dalian Levy"
- `dale_man_at_arms`: "Dale Militia" → "Dalian Militia"
- `dale_riverman`: "Riverman" → "Dalian Riverman"
- `dale_shipman`: "Shipmen" → "Dalian Shipman" (singular, matching other prefixed names; "Shipment" in spec was a typo)
- `dale_bowman`: "Yeoman" → "Dalian Yeoman"
- `dale_longbowman`: "Bowman" → "Dalian Bowman"
- `dale_royal_archer`: "Marksman of Dale" → "Dalian Marksman"
- `dale_black_arrow_marksman`: "Barding Marksman" → "Dalian Barding"
- `dale_outrider`: "Merchant Guard" → "Dalian Merchant Guard"
- `dale_knight`: "Northman Scout" → "Dalian Northman Scout"
- (`dale_dalian_mariner` already "Dalian Mariner" from prior pass — no change)

**Cavalry split** — the linear T4→T5→T6→T7 chain becomes a Y-split at T4:
- T4 `dale_outrider` "Dalian Merchant Guard" — now has **two** upgrade targets:
  - **LIGHT**: T5 `dale_knight` "Dalian Northman Scout" — terminal (single-tier branch; was upgrading to royal_cavalier).
  - **HEAVY**: T5 `dale_royal_cavalier` "Dalian Cavalry" → T6 `dale_kinsman_of_eorl` "Dalian Heavy Cavalry" → T7 `dale_kings_guard` "Dalian King's Guard" (NEW terminal).

**Tier downshift** for the heavy line (user-approved): `dale_royal_cavalier` T6→T5, `dale_kinsman_of_eorl` T7→T6, and the NEW `dale_kings_guard` takes T7. Skill curves rebalanced to match new tiers — added `s_dalian_cavalry_t5` (heavy-entry, +Riding/Polearm vs Northman Scout); renamed `s_royal_cavalier_t6` → `s_heavy_cavalry_t6` and `s_kinsman_eorl_t7` → `s_kings_guard_t7` for clarity. Armor mesh also shifts: Dalian Cavalry now uses chivlary a03/a04 (was b01/b02), Heavy Cavalry uses b01/b02 (was b01/b02 — unchanged at T6), King's Guard takes the top chivlary mesh **b03/b04** via `cavalry_armor(8, ...)` (the b03/b04 slot was previously unused — Dale capped at T7 with b01/b02 cavalry armor).

**Party templates**:
- `vassal_reward_troops_dale`: swapped `dale_kinsman_of_eorl` → `dale_kings_guard` (the elite-reward stack now reflects the new T7 terminal).
- `kingdom_hero_party_dale_template`: added 1-2 stack `dale_kings_guard`.

**Verification**: 4 Dale tests pass against existing TAOM.dll (no C# in this change); validator PASS across all 8 cultures (Dale: 31 troops [30+wrapper], 120 armor refs [+10 for King's Guard's 5 armor slots × 2 rosters], 0 missing); cavalry tree spot-checked — Merchant Guard splits to {knight (terminal), royal_cavalier→kinsman→kings_guard}.

Save-compat: All existing troop IDs preserved. Tier downshifts on royal_cavalier + kinsman_of_eorl rebalance their levels (32→25, 39→32) and re-pick equipment on next load — no troop disappears from any party.

### fix(dale): swap Watch/Pikeman weapons + drop Peasant bracers

User-directed correction. Names were tactically backwards relative to the equipment:
- **Watch line** now wields **2H halberds/polearms** (`sturgia_2haxe_1_t4`, `billhook_polearm_t2`, `sturgia_polearm_1_t5`, `sturgia_2haxe_2_t5`) — shock-infantry role. Skill curve adds mild TwoHanded (sturgia_2haxe_* items use the TwoHanded skill).
- **Pikeman line** now wields **vanilla pikes** (`fine_pike_t4`, `military_fork_pike_t3`, `vlandia_pike_1_t5`, `thamaskene_pike_t4`) — anti-cavalry role. Skill curve drops TwoHanded (pikes use Polearm only).
- The new T7 `dale_lake_town_hearthguard` follows its line and is now a vanilla-pike royal-tier anti-cavalry unit (TwoHanded dropped 150 → 0; Polearm bumped 220 → 235).

Lake-Town Peasant (T2 `dale_recruit`) armor reduced to chest + boots only — added `no_bracers` parameter to `lake_town_armor_explicit` helper (was `no_helmet=True, no_shoulder=True`; now also `no_bracers=True`).

Verification: validator PASS across all 8 cultures; weapon-swap spot-check confirms Watch Item0 is now `sturgia_2haxe_*` / `sturgia_polearm_1_t5` / `billhook_polearm_t2` and Pikeman Item0 is now `fine_pike_t4` / `military_fork_pike_t3` / `vlandia_pike_1_t5` / `thamaskene_pike_t4`; Peasant slots reduced to {Body, Leg} only.

### feat(dale): T8 removed, Lake-Town Hearthguard added, per-tier explicit armor

Third pass on the Dale tree per user direction. 30 → 29 troops (deleted 2 T8 elites, added 1 T7 terminal). Dale now caps at T7 — no T8 troops.

**Tree changes:**
- Dale Levy (`dale_squire`) upgrade order reordered top→bottom: **dale_riverman, dale_man_at_arms, dale_bowman, dale_outrider** (was bowman/man_at_arms/outrider/riverman).
- Deleted T8 troops: `dale_kings_bowman` "King's Bowman" and `dale_kings_champion` "King's Champion" removed entirely. Skill functions `s_kings_bowman_t8` / `s_kings_champion_t8` deleted.
- `dale_black_arrow_marksman` upgrades=[] (was → dale_kings_bowman). Display: **"Barding"** → **"Barding Marksman"**.
- `dale_running_river_warden` upgrades=[] (was → dale_kings_champion). Display: **"Warden of the Running River"** → **"Dalian Master Swordsman"**.
- New T7 troop **`dale_lake_town_hearthguard`** "Lake-Town Hearthguard" — Lake-Town royal-tier 2H polearm shock infantry. `dale_veteran_spearman` now upgrades to it (was terminal). Skill curve: Polearm-primary (220), mild TwoHanded (150), OneHanded (150).

**Explicit per-tier mariner armor** for all 8 existing Lake-Town troops (Watch line + Pikeman line + levy root) + the new Hearthguard, per user spec. Each tier maps to a single mariner suffix across all 5 slots:

| Troop | Suffix | Helmet | Chest | Bracers | Boots | Shoulder |
|---|---|---|---|---|---|---|
| Lake-Town Peasant (T2) | a01 | — | a01 | a01 | a01 | — |
| Lake-Town Militia (T3) | a01 | a01 | a01 | a01 | a01 | a01 |
| Lake-Town Watchman (T4) | a02 | a02 | a02 | a02 | a02 | a01* |
| Lake-Town Veteran Watchman (T5) | a03 | a03 | a03 | a03 | a03 | a03 |
| Lake-Town Officer of the Watch (T6) | a04 | a04 | a04 | a04 | a04 | a03* |
| Lake-Town Patrolman (T4) | b01 | b01 | b01 | b01 | b01 | b01 |
| Lake-Town Pikeman (T5) | b02 | b02 | b02 | b02 | b02 | b01* |
| Lake-Town Veteran Pikeman (T6) | b03 | b03 | b03 | b03 | b03 | b03 |
| Lake-Town Hearthguard (T7) | b04 | b04 | b04 | b04 | b04 | b03* |

*Solus's mariner shoulder mesh exists only at a01/a03/b01/b03. For tiers a02/a04/b02/b04 the shoulder falls back to the next-lower available variant (a02→a01, a04→a03, b02→b01, b04→b03). Documented in `tools/generate_dale_troops.py:lake_town_armor_explicit`.

**Party template updates** (`Main/_Module/ModuleData/taom_partyTemplates.xml`):
- `vassal_reward_troops_dale`: removed `dale_kings_champion` + `dale_kings_bowman` stacks; added `dale_running_river_warden` (2), `dale_black_arrow_marksman` (3), `dale_lake_town_hearthguard` (1) — keeps `dale_kinsman_of_eorl` (2). 8 elite-T7 troops total per vassal reward.
- `kingdom_hero_party_dale_template`: added 1-2 stack `dale_lake_town_hearthguard`.

**Verification**: `python tools/generate_dale_troops.py --dry-run` shows 29 troops, all 25 upgrade refs resolve, T8 troops confirmed gone, Hearthguard confirmed present. `validate_all_troop_refs.py` PASS (Dale: 30 troops [29 + wrapper-count offset], 110 armor refs, 0 missing). 4 Dale tests pass against the existing TAOM.dll (no C# changes in this commit — pure data + Python tool + docs).

Save-compat: All existing troop IDs preserved (display-name only renames). Deletion of `dale_kings_bowman` / `dale_kings_champion` would break only saves where one of those specific IDs is referenced inline (vassal-reward parties not yet generated, since vassal_reward_troops_dale shipped only in ce978f5 today and no campaigns have run long enough for it to fire). The new `dale_lake_town_hearthguard` ID is additive.

Not-tested: live in-game upgrade chain to Hearthguard, per-tier mariner armor visual verification. Per ADR-008.

Build-state note: `dotnet build Main` fails the post-build deploy step on a locked `0Harmony.dll` (game/launcher holds handle); the deploy step is orthogonal to this change-set, which is pure data. Tests pass against the prior TAOM.dll.

### feat(dale): troop tree restructure — Lake-Town renames + Riverman line + vanilla pikes

User-directed second pass after the initial Dale ship (commit `ce978f5`). Reorganises the 27-troop tree into 30 troops with display-name renames, equipment swaps for the two Lake-Town infantry lines, and a new royal-tier spear-and-shield line off Dale Levy. All troop IDs unchanged (save-compat preserved per `troops.md` rule); display names + equipment changed in-place.

**Lake-Town levy line — display renames** (IDs in parens unchanged):
- `dale_recruit` "Recruit" → "Lake-Town Peasant"
- `dale_militia` "Militia" → "Lake-Town Militia"

**Lake-Town Watch line — renamed + equipment swapped from javelin to pike (2H) + 1H sword sidearm, no shield**:
- `dale_lake_town_skirmisher` "Lake-town Skirmisher" → "Lake-Town Watchman" (Item0: `fine_pike_t4` / `military_fork_pike_t3`)
- `dale_lake_town_mariner` "Lake-town Mariner" → "Lake-Town Veteran Watchman" (Item0: `vlandia_pike_1_t5` / `thamaskene_pike_t4`)
- `dale_lake_town_veteran` "Lake-town Veteran" → "Lake-Town Officer of the Watch" (Item0: `vlandia_pike_1_t5`)
- Skill curve adjusted: Throwing-primary → Polearm-primary (matching the new weapons).

**Lake-Town Pikeman line — renamed + equipment swapped from spear+shield to 2H halberds/polearms, no shield, armor swapped from Dale-infrantry to Lake-Town mariner mesh class**:
- `dale_footman` "Footman" → "Lake-Town Patrolman" (Item0: `sturgia_2haxe_1_t4` / `billhook_polearm_t2`)
- `dale_spearman` "Spearman" → "Lake-Town Pikeman" (Item0: `sturgia_polearm_1_t5` / `sturgia_2haxe_2_t5`)
- `dale_veteran_spearman` "Veteran Spearman" → "Lake-Town Veteran Pikeman" (Item0: `sturgia_polearm_1_t5` / `sturgia_2haxe_2_t5`)
- Skill curve adjusted: added mild TwoHanded for overhead 2H polearm swings.

**Royal Archer branch — display renames**:
- `dale_bowman` "Bowman" → "Yeoman"
- `dale_longbowman` "Longbowman" → "Bowman"
- `dale_royal_archer` "Royal Archer" → "Marksman of Dale"
- `dale_black_arrow_marksman` "Black Arrow Marksman" → "Barding"
- `dale_kings_bowman` "King's Bowman" — unchanged

**Royal Infantry branch — display renames**:
- `dale_man_at_arms` "Man-at-Arms" → "Dale Militia"
- `dale_guardsman` "Guardsman" → "Dalian Guardsman"
- `dale_royal_guard` "Royal Guard" → "Dalian Swordsman"
- `dale_running_river_warden` and `dale_kings_champion` — unchanged

**Royal Cavalry branch — display renames**:
- `dale_outrider` "Outrider" → "Merchant Guard"
- `dale_knight` "Knight" → "Northman Scout"
- `dale_royal_cavalier` "Royal Cavalier" → "Dalian Cavalry"
- `dale_kinsman_of_eorl` "Kinsman of Eorl" → "Dalian Heavy Cavalry"

**Dale Levy** (T3 royal root, was "Squire"):
- `dale_squire` "Squire" → "Dale Levy"
- Added 4th upgrade target: `dale_riverman` (alongside existing `dale_bowman`, `dale_man_at_arms`, `dale_outrider`).

**New troops — Riverman line** (T4-T6, off Dale Levy; spear + shield + 1H sword + Lake-Town armor; royal-tier water-folk):
- T4 `dale_riverman` "Riverman" → T5 `dale_shipman` "Shipmen" → T6 `dale_dalian_mariner` "Dalian Mariner" (terminal).
- Added to `kingdom_hero_party_dale_template` (3 stacks) and `patrol_party_dale_template_level_2` (1 stack) in `taom_partyTemplates.xml`.

**Verification**: `python tools/generate_dale_troops.py --dry-run` shows 30 troops with all upgrade chains resolved; all 8 new vanilla weapon IDs (`fine_pike_t4`, `military_fork_pike_t3`, `vlandia_pike_1_t5`, `thamaskene_pike_t4`, `sturgia_2haxe_1_t4`, `sturgia_2haxe_2_t5`, `billhook_polearm_t2`, `sturgia_polearm_1_t5`) verified in vanilla SandBoxCore items; `validate_all_troop_refs.py` PASS (Dale: 31 troops, 121 armor refs, 0 missing).

Not-tested: live in-game pike vs halberd visual differentiation, Riverman line spawning in lord parties. Per ADR-008.

### feat(dale): Dale culture — armor authoring + 27-troop tree

Solus completed the Dale armor mesh set (163 items across 5 slots: head/body/leg/arm/shoulder). This change wires them into LOTRLOME_Armory and adds a full troop tree on the vanilla `Culture.sturgia` id (renamed "Barding" via existing XSLT).

**Task A — Armor** (`tools/generate_dale_armor.py` + manifest `tools/dale_armor_meshes.txt`):
- Mesh IDs harvested from the 5 `.tpac` files via the existing spider-skeleton `tools/tpac_skeleton_scan.py --all-types` tool. 169 mesh IDs deduped to 163 authored items (6 `_slim` female-fit meshes auto-paired with their base via `has_gender_variations="true"`, not authored as separate items).
- All items: `culture="Culture.sturgia"` + `<Flags UseTeamColor="true" />` per spec.
- Special covers_hands="false" applied to the 10 user-specified bracer/archer-gauntlet IDs; the other 22 hand items get `covers_hands="true"`.
- Material/modifier-group lookup by (class, tier) — archer light → Cloth, archer elite → Chainmail; chivalry light → Chainmail, chivalry medium+ → Plate; infantry light → Leather, infantry heavy+ → Plate; mariner stays Cloth/Leather with Chainmail only at elite veteran.
- 5 XML files written to `LOTRLOME_Armory/ModuleData/LOTRLOME_items/dale/`; folder registered in `LOTRLOME_Armory/SubModule.xml`.
- Solus's mesh naming quirks preserved verbatim — `chivlary` typo on 4 slots, `chivalry` correct spelling on chest slot, `infrantry` typo, `lake_town_mariner` for Lake-town bracers.

**Task B — Troop tree** (`tools/generate_dale_troops.py`):
- 27 troops in `Main/_Module/ModuleData/troops/troops_dale.xml`, lore-grounded per a web-research pass (Tolkien sources cited in `docs/features/dale.md`).
- Three royal branches off a shared T3 Squire root:
  - **Excellent Archers** (T4-T8): Bowman → Longbowman → Royal Archer → Black Arrow Marksman → King's Bowman. +10-15 Bow skill above standard tier curve.
  - **Great Infantry** (T4-T8): Man-at-Arms → Guardsman → Royal Guard → Warden of the Running River → King's Champion.
  - **Decent Cavalry** (T4-T7, capped — Dale isn't horse-country per Tolkien): Outrider → Knight → Royal Cavalier → Kinsman of Eorl. Skill curve ~10% under Rohan parity.
- Lake-town smallfolk line (T2 Recruit → T3 Militia → T4-T6 Lake-town Skirmisher/Mariner/Veteran or Footman/Spearman/Veteran) for low-tier Esgaroth militia.
- 4 militia troops (`dale_militia_spearman/archer/veteran_spearman/veteran_archer`) referenced by spcultures.xslt for garrison spawns.
- Equipment uses new Dale armor items + vanilla Sturgia weapons (`sturgia_sword_*`, `northern_spear_*`, `sturgia_lance_*`) + shared LOTRAOM horses/shields + bow line keyed on Bard's longbow tradition (`hunting_bow` → `lowland_yew_bow` → `lowland_longbow` → `noble_bow`).

**Registration changes:**
- `Main/_Module/SubModule.xml` — registered `troops/troops_dale` XmlNode next to Erebor.
- `Main/_Module/ModuleData/spcultures.xslt` — added 9 military attribute overrides on the existing `Culture[@id='sturgia']` block (`basic_troop`, `elite_basic_troop`, 4 militia slots, `default_party_template`, 2 equipment-roster slots).
- `Main/_Module/ModuleData/taom_partyTemplates.xml` — 9 new Dale templates (kingdom_hero, mercenary, outlaw, militia, 3 patrol levels, rebels, vassal_reward).
- `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` — added `InitializeDaleCulture()` binding `CultureMap["sturgia"]` to a 5-entry pool (recruit/militia/bowman/footman/squire). Single culture-level pool, no per-settlement granularity (deferred follow-up).
- `tools/validate_all_troop_refs.py` — added `"dale"` to the culture list. All 121 Dale armor refs + 50 weapon/horse/shield refs resolve.

**Tests** (`TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`):
- 4 new tests covering the Dale culture pool weighted-random distribution (low/mid/high rolls) + settlement-with-no-pool fallthrough.

**Lore sourcing** (cited inline in `docs/features/dale.md`):
- *Hobbit* ch. 14 + 17 ("Fire and Water" + "The Clouds Burst") — Black Arrow as heirloom of Girion, Bardings "armed with long swords and tall spears, bearing great bows."
- *LOTR* Appendix A III — Erebor-made mail-shirts traded to Esgaroth.
- *LOTR* Appendix B — Battle of Dale TA 3019, Brand falls before the Gate.
- *Two Towers* "Riders of Rohan" — Rohirrim kin to "the Bardings of Dale, and the Beornings of the Wood."

**Verification:**
- `/verify quick` — build green.
- `/deep-review dale` (5 agents) — Standards PASS, API Compatibility PASS, Efficiency PASS, Completeness identified missing tests/doc/issue/CHANGELOG (all addressed this session), Data Flow 7/7 flows connected, 1 MEDIUM follow-up (lord rosters in `taom_equipment_sets_dale.xml` still reference vanilla Sturgia items — Dale TROOPS look correct but Dale LORDS still look vanilla Sturgian; documented in feature doc as known limitation).
- `/review-codex dale` (Codex adversarial) — 3 confirmed findings, all fixed in-session:
  - **P1**: 6 XSLT culture-template bindings missing (`militia_party_template`, `rebels_party_template`, `vassal_reward_party_template`, `settlement_patrol_template_level_1/2/3`) — Dale's new militia/patrol/rebels/reward party templates were dead code because the vanilla XML's passthrough preserved `militia_sturgia_template` etc. Fixed in `spcultures.xslt`.
  - **P2**: Bow tier inversion — `lowland_yew_bow` (difficulty 50, damage 69) is stronger than `lowland_longbow` (difficulty 30, damage 57). T5 `dale_longbowman` could roll the yew bow; T6 `dale_royal_archer` only had the weaker longbow. Swapped — T5 now uses longbow, T6 graduates to yew_bow.
  - **P3**: Cavalry skill curve was 40-45% under Rohan, not the "~10% under" the generator comment claimed. Bumped Riding/Polearm by ~35-45% so Dale cavalry lands at roughly 70% of Rohan tier-matched parity (still clearly weaker per Tolkien's Éothéod-vs-Bardings split). Updated comments to match.
- 4 Dale tests pass after fixes; full test suite unaffected.
- RCA at `docs/reviews/rca-dale-2026-05-26.md`. Key process lesson: the 5-agent `/deep-review` checks "what we DID is correct" but doesn't check "what we DIDN'T DO, was that intentional." Codex's vanilla-deserializer decompile is the right way to catch missing bindings. Feedback memory codified at `feedback_xslt_passthrough_unintended_inheritance.md`.

Closes #226.

Research: `pwsh tools/taom-src.ps1 path TaleWorlds.Core.CultureObject` — confirmed `BasicTroop`/`EliteBasicTroop`/militia/party-template/equipment-roster attribute deserialization paths.
Save-compat: New troops are additive; no existing troop IDs renamed or deleted. Sturgia kingdom save loads will lazily populate Dale recruits on next volunteer tick.
Not-tested: visual/in-game rendering of armor items (gender-variation auto-swap, UseTeamColor banner tint, mesh binding) — verified live in custom battle per ADR-008.

## 2026-05-25

### chore(harness): broaden GauntletLayer input-wiring rule to cover MissionScreen overlays

Followup to commit `28c8d1e` (#225). The rule codified in commit `0b951c7` (#204) for issue #202 was scoped to ScreenBase overlays only and explicitly excluded MissionScreen overlays — based on the wrong inference that `BattleActionBar` "worked" without `SetInputRestrictions()`. It didn't; only its hotkey path worked, which masked the broken mouse path. The OOB and BattleActionBar bugs in #225 are direct consequences of that scope error.

Broadens [.claude/rules/gui-ui.md](.claude/rules/gui-ui.md) "Custom GauntletLayer Input Wiring", [.claude/skills/deep-review/SKILL.md](.claude/skills/deep-review/SKILL.md) Standards-agent check #10, and the [`feedback_gauntlet_overlay_input_wiring.md`](~/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_gauntlet_overlay_input_wiring.md) memory entry to cover BOTH `ScreenBase` overlays (Harmony postfix on `OnInitialize`) AND `MissionScreen` overlays (`MissionView.OnMissionScreenInitializeFirstTime`, `MissionLogic` attach, etc.). The v1.4.5 input dispatcher does not distinguish the two host types for this purpose. Display-only HUDs (no `ButtonWidget` / `Command.Click` matches) remain the documented exception.

RCA at `docs/reviews/rca-companiontactics-overlay-input-2026-05-25.md` captures the recurring "rule scope inferred from a working sibling without verifying which input path made the sibling work" pattern + a process change: when codifying a rule from one instance, sweep the codebase for siblings before treating the rule as load-bearing. The original sweep would have caught the OOB + BattleActionBar instances immediately.

### fix(companiontactics): wire input restrictions on OOB + battle-bar overlays

User reported that on the pre-battle Order-of-Battle (deployment) screen, the two TAOM-added buttons "Assign Heroes" and "Presets" did nothing when clicked while the adjacent vanilla "Reset Deployment" / "Ready" buttons worked normally. Same bug class as the EquipPresets fix from earlier (commit `d141304`, #202) — the custom `GauntletLayer` in [OOBOverlayService.cs:114](Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs#L114) was added without `_layer.InputRestrictions.SetInputRestrictions()`, so it painted but never registered with the MissionScreen's input dispatcher.

Latent twin caught while investigating: [BattleActionBarMissionView.cs:54](Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs#L54) had the same broken pattern. The `BattleActionBar.xml` prefab has `Command.Click="ExecuteAction"` button bindings but mouse clicks were silently dropped; the bar happened to remain functional via numeric hotkeys (`HandleHotkeyInput` polls `Mission.InputManager` directly, bypassing the Gauntlet input dispatcher entirely), which masked the mouse-path bug until now.

Fix in both files: add `_layer.InputRestrictions.SetInputRestrictions()` after layer construction (paired with `_layer.InputRestrictions.ResetInputRestrictions()` before `RemoveLayer` in the teardown path). Closes #225.

The fact that this slipped past the rule we codified in commit `0b951c7` (#204) is itself a process bug — the rule was scoped to ScreenBase overlays only and explicitly excluded MissionScreen overlays based on a wrong inference. Followup commit broadens the rule + writes the RCA at `docs/reviews/rca-companiontactics-overlay-input-2026-05-25.md`.

Research: ilspycmd verified `GauntletLayer.InputRestrictions.SetInputRestrictions()` / `ResetInputRestrictions()` exist on v1.3.15 base class `ScreenLayer.InputRestrictions` regardless of whether the layer is hosted on `ScreenBase` or `MissionScreen`.
Not-tested: MissionView entry points are tested live in-game per ADR-008.

### feat(troops): Gondor troop polish — equipment audit + Pinnath Gelin cavalry (#224)

Follow-up to #212 KEYforce troop tree revamp. Visual review of the Gondor trees in custom battle surfaced concrete equipment gaps. Single delta-style apply script (`tools/apply_gondor_polish_224.py`) — distinct from the full-roster-swap pattern used by #99/#212.

**58 troops touched, 94 equipment ops applied, 2 new NPCCharacter blocks, 1 upgrade-target patch.**

**Changes by category:**

| Category | Troops | Change |
|----------|--------|--------|
| **T1 boots fix** | 4 (Anfalas Levy, Belfalas Recruit, Lebennin Militia, PG Volunteer) | Added `sk_gd_ano_boots_a` Leg slot |
| **1h sword sidearms** | 11 (Anorien archer line × 4, Methir archer × 3, Ithil Guard × 3, Cair Andros, Anfalas Footman) | Tier-matched `wm_gondor_sword_a01–a09` per troop level |
| **Lebennin → Lebennin swords** | 8 (whole militia/archer/infantry chain) | `wm_pelargir_sword_a01/a02` replaces generic `wm_gondor_sword_*` |
| **Lamedon → 2h swords + drop shield** | 5 (whole Clansman → Hill-Warden chain) | `wm_gondor_lamedon_2h_sword_a/b/c/d` + cleared shield |
| **Anorien cavalry chain** | 3 (`mt_cavalry/heavy_cavalry/knight`) | Banner Spear IV/I/II + canonical Gondor (Light) Horse Armour |
| **Arndir cavalry → Numenorean 2h** | 4 (whole chain) | `numenorean_sword_2h_d/f/i/l` + cleared shield |
| **Calembel — drop shield on 2h users** | 3 (Heavy Swordsman, Sergeant, Vale Knight) | Cleared `gond_shield_one_red` (was illegal with 2h sword) |
| **Dol Amroth — horse armour + spear + sword** | 5 (Squire → Swan Knight) | Belfalas / Belfalas / Belfalas / Swan I / (unchanged) harnesses; speara/speara/speara/spearb pairs; tier-matched 1h sword sidearm; Swan Knight gets lance II + sword I |
| **Pelargir — javelins across chain** | 5 (Skirmisher → Anchor Guard) | 1–2 stacks `imperial_throwing_spear_1_t4` |
| **Crossbow troops — use crossbows + bolts** | 7 (Lond-Galen × 2, Tolfalas × 5) | Vanilla `crossbow_b–f` + `bolt_b–d` (no LOTRLOME variant exists) + tier-matched sidearm sword |
| **Lossarnach Axe Thrower line** | 3 (Thrower, Skirmisher, Vet Thrower) | Cleared extra 1h axe slot — keep only 2h axe + thrown axe per user spec |

**New troops (Pinnath Gelin cavalry branch):**

| Troop | Level | Tier | Loadout |
|-------|-------|------|---------|
| `gondor_pg_cavalry` | 26 | T5 | PG spear A + sword T5 + green shield + 1 javelin + empire_horse + Pinnath Gelin Light Horse Armour |
| `gondor_pg_vet_cavalry` | 31 | T6 | PG spear B + sword T6 + green shield + 1 javelin + t2_empire_horse + Pinnath Gelin Horse Armour |

Branches off `gondor_pg_spearman` `upgrade_target` (`gondor_pg_spearman` now upgrades to either `gondor_pg_vet_spearman` OR `gondor_pg_cavalry`).

**Downstream updates:**
- `Main/_Module/ModuleData/taom_partyTemplates.xml` — added 2 stacks (`gondor_pg_cavalry` + `gondor_pg_vet_cavalry`) to `kingdom_hero_party_gondor_template`.
- `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` — added Gondor section + `gondor_pg_vet_cavalry` weight 2.0 (T6 elite tier).
- No recruitment-pool change — PG cavalry is upgrade-only (`is_basic_troop="false"`); recruitment via Spearman upgrade.
- No `recruitment_pools/gondor.json` edit — parallel session owns it.

**Decisions:**
- Empire-themed vanilla items used where no LOTRLOME Gondor variant exists: crossbows + bolts + javelins + empire/imperial horses. Consistent with TAOM's XSLT Empire→Gondor culture remap.
- Tier-matched 1h sword convention: L+5 step = +1 tier (`wm_gondor_sword_a01` at L6, `_a02` at L11, ..., `_a09` at L46). Same convention as #99 / #211 / #212.
- Apply script is **delta-style** (`set`/`clear`/`replace` operations per slot), not full-roster swap. Header comment notes this so future clones don't mis-apply the wrong pattern.

**Out of scope:**
- Lossarnach Axebearer line — user mentioned "axebearer line should have 1h axe + shield" but no Axebearer troops exist in TAOM. Deferred pending clarification on add-vs-rename approach.
- Patrol-level / vassal-reward templates for new PG cavalry (only `kingdom_hero_party_*` expanded this pass).
- Localization XML for `gondor_pg_cavalry_name` / `gondor_pg_vet_cavalry_name` — falls back to inline `name="..."` attribute.

Build: 0 errors, 1042 warnings (pre-existing). Validator: PASS all 7 cultures, 0 missing. Gondor: 181 troops (was 179, +2 PG cavalry).

Issue: #224.

**Post-commit deep-review fix (same day):** `/deep-review` Agent 5 (data-flow) caught a HIGH gap — the Lossarnach Axe Thrower line's 1h-axe-clear targeted `Item2`/`Item3` instead of `Item1`, so the 1h axe stayed everywhere and one 2h axe was wrongly cleared. Plus 2 latent MEDIUM script bugs (`apply_set` insertion path for non-roster slots; duplicate `gondor_leb_militia` dict key). All fixed in `apply_gondor_polish_224.py`; Lossarnach state corrected (1h axe now 0/6 rosters per troop, 2h axe restored to all 6/6). RCA: `docs/reviews/rca-gondor-polish-224-deep-review-2026-05-25.md`.

---

### feat(map): distance-based settlement nameplate fade (#223)

Settlement nameplates on the campaign map now fade smoothly with camera distance. Vanilla shows all nameplates at full visibility regardless of distance; on TAOM's 863-settlement map this is visually noisy. New feature module:

- Harmony Postfix on `SettlementNameplateWidget.DetermineTargetAlphaValue()` multiplies the vanilla target alpha by a fade factor in [0, 1] derived from the widget's `DistanceToCamera`. Vanilla's lerp toward the new target smooths transitions automatically.
- Three new MCM settings under `Map UI/Settlement Nameplates`: `EnableNameplateFade` (toggle, default on), `NameplateFadeNearDistance` (5-500, default 80), `NameplateFadeFarDistance` (10-1000, default 200).
- Applies uniformly to towns, castles, villages, and hideouts (`SettlementNameplateWidget` is shared across all settlement types).
- Disabled / NaN / `Far <= Near` paths short-circuit to multiplier 1.0 (vanilla behavior preserved).

`/deep-review` caught 1 HIGH + 1 MED + 1 LOW before commit, all fixed in-session — full RCA at [docs/reviews/rca-settlement-nameplate-fade-2026-05-25.md](docs/reviews/rca-settlement-nameplate-fade-2026-05-25.md). Highlights: cached `TaomSettings.Instance` reference in the provider constructor (9000 redundant singleton dereferences/sec on a 60 FPS × 50-settlement load → one); switched from `Lazy<INameplateFadeService>` to the project-standard `Initialize(svc)` + static-field service capture; added the missing `InfinityNearDistance` regression test to symmetrize the NaN/Infinity coverage matrix.

`/review-codex` not run for this 150-line feature — user discretion. Per CLAUDE.md, Codex pass requires explicit user intent.

**Build:** 0 errors, 0 new warnings. **Tests:** 2501/2503 passing (2 unrelated skipped), 19 new tests for this feature.

Feature doc: [docs/features/settlement-nameplate-fade.md](docs/features/settlement-nameplate-fade.md).

Research: ilspycmd on installed `TaleWorlds.MountAndBlade.GauntletUI.Widgets.dll` v1.4.5 — verified `SettlementNameplateWidget.DetermineTargetAlphaValue` (private float, no params), `DistanceToCamera` (public float property).
Not-tested: Harmony patch invocation in live game (verified manually by moving camera).

### fix(crash-report): Codex adversarial review (Review 41) — 8 confirmed findings, all fixed

`/review-codex` on the CrashReport feature surfaced **2 HIGH + 4 MEDIUM + 2 LOW** findings beyond the 6 caught by Phase 1 `/deep-review`. All 8 independently verified against TAOM source + decompiled vanilla DLLs, no false positives, all fixed in the same session. Full RCA: [docs/reviews/rca-crash-report-codex-2026-05-25.md](docs/reviews/rca-crash-report-codex-2026-05-25.md). Codex output: [docs/reviews/codex-adversarial-crash-report-2026-05-25.md](docs/reviews/codex-adversarial-crash-report-2026-05-25.md).

**Combined `/deep-review` + `/review-codex` workflow caught 14 total bugs in a 60-file feature that the author tried to declare "done" after `/verify` alone.** See REVIEW-LOG Review 41 entry.

**HIGH-1:** [Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs](Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs) — static `_service` cache survived `OnSubModuleUnloaded` → post-reload Finalizers called disposed `FileLogger`. Added `ResetForUnload()` + call site in `SubModule.OnSubModuleUnloaded`.

**HIGH-2:** [Main/Features/CrashReport/CrashReportSettings.cs](Main/Features/CrashReport/CrashReportSettings.cs) — `EnableCrashCapture` MCM hint promised runtime no-op + AppDomain unsubscribe but property was read only at startup. Same shape as Phase 1's `SuspendButterLibHandler` decorative-toggle bug. Runtime gates added in `CrashReportPatchHelper.HandleAndSwallow`, `AppDomainExceptionHook.OnUnhandled`, both dev triggers.

**MED-1:** [SubModule.cs](Main/SubModule.cs) — Patch37 attached at line 108, but `IoC.Configure()` (88), UIExtender setup (90-92), `ITimeAccelerationService` resolve (94), `Harmony` ctor (96), `CrashReportSettings.Instance` read (104) all ran BEFORE — throws in those lines uncatchable. Moved `_harmony = new Harmony(...)` + Patch37 attach to immediately after `IoC.Configure()`. Only remaining blind spot is `IoC.Configure()` itself (documented).

**MED-2 (effectively CRITICAL — Codex understated):** [HarmonyCorrelationCollector.cs](Main/Features/CrashReport/Collectors/HarmonyCorrelationCollector.cs) — `Collect(stack, frames=null)` ran the per-stack-frame `Harmony.GetPatchInfo(mb)` block only when the optional `frames` parameter was non-null. The sole production caller passed only the snapshot list. Result: the "Harmony patches affecting every frame" feature advertised in CHANGELOG was **DEAD CODE** — every per-frame entry constructed with an empty `Patches` list. Added `CollectFromException(exception, stack)` overload that builds raw `StackFrame[]` internally; service calls the new overload.

**MED-3:** [AppDomainExceptionHook.cs](Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs) — `OnUnhandled` can fire on TaleWorlds worker threads (`TWParallel.For` agent ticks); Mission/Campaign collectors read main-thread-only engine state; `InformationManager.ShowInquiry` invokes UI subscribers off-thread. Main thread id captured at `Subscribe()`; off-thread captures tag exception's `Data` dict with `OffMainThreadDataKey`; service switches to reduced-capture mode (skips Mission/Campaign + UI inquiry).

**MED-4:** [CrashReportService.cs](Main/Features/CrashReport/CrashReportService.cs) — `_butterLibSuspended` one-shot flag prevented re-disable after user re-enabled ButterLib via its own MCM at runtime. Codex decompiled ButterLib's `Disable()` and confirmed it's idempotent. Removed the flag; `TrySuspend()` now called per crash when MCM toggle is on.

**LOW-1:** [CrashReportApplicationTickTrigger.cs](Main/Features/CrashReport/DevTriggers/CrashReportApplicationTickTrigger.cs) + [CrashReportDevTrigger.cs](Main/Features/CrashReport/DevTriggers/CrashReportDevTrigger.cs) — `CrashReportSettings.Instance` per-tick is a provider scan (Codex decompiled MCMv5 `BaseSettingsProvider.GetSettings(id)`), not a static-field read. Cached via `??=` in both dev triggers.

**LOW-2:** [CrashBundleWriter.cs](Main/Features/CrashReport/Rendering/CrashBundleWriter.cs) — `Write` returned the zip path even after mid-write `catch`, pointing player at a broken bundle. On mid-write failure: rename to `*.zip.partial` + return `null`.

**Plus pre-existing build warnings fixed earlier in the session:**
- **BHA0001** (BUTR.Harmony.Analyzer) on all 10 Patch37 classes — swapped attribute order to `[HarmonyPatch(...)]` FIRST, `[HarmonyPatchCategory(...)]` SECOND (matches existing TAOM convention).
- **MSB3277** (System.Management version conflict) — `<Reference Include="System.Management" />` resolved to .NET 4.7.2 BCL v4.0.0.0 but TaleWorlds.* DLLs depend on v4.0.1.0 from `<game>\bin\Win64_Shipping_Client\System.Management.dll`. Switched to HintPath. [Main/TAOM.csproj](Main/TAOM.csproj).

**Build:** 0 warnings, 0 errors. **Tests:** 2440/2440 passing, 2 skipped, 0 failed.

**Process improvements triggered:**
- [AGENTS.md](AGENTS.md) — added 7 new "Bugs Codex caught Claude missed" lessons (one per Review 41 finding) + bumped lesson counter to 38 reviews / 114 bugs.
- [docs/reviews/REVIEW-LOG.md](docs/reviews/REVIEW-LOG.md) — Review 41 entry with full findings table + RCA + Codex quality notes.
- [.claude/skills/deep-review/SKILL.md](.claude/skills/deep-review/SKILL.md) — Agent 5 prompts tightened: (2b) MCM toggle-cross-reference now applies to EVERY toggle enumerated from the settings class (previously hand-listed; let HIGH-2 slip past); (2c) DTO Completeness extended from "is this populated?" to "are non-empty values actually produced under normal operation?" (would have caught MED-2 dead Harmony correlation at deep-review time).

Constraint: ButterLib re-enable at runtime breaks `_butterLibSuspended` semantics — removing the flag is the simpler fix than tracking ButterLib state.
Research: ilspycmd against installed v1.4.5 DLLs for ScreenManager / InformationManager / MCMv5 BaseSettingsProvider / ButterLib ExceptionHandlerSubSystem.
Rejected: keeping `_butterLibSuspended` and polling ButterLib state — premature optimisation; `Disable()` is idempotent and cheap per decompile.
Save-compat: no save-data changes.
Not-tested: thread-safety tests for off-main-thread captures, lifecycle tests for `ResetForUnload()`, master-toggle-disabled tests — listed as test-debt follow-up in the RCA.

### chore(deps): deep-review cleanup of stub-module changeset — glob tighten + doc resync + RCA (#221)

Post-ship deep review of commits `031283c` (stub modules) + `8a9d18f` (auto-enable) found 1 MED + 3 LOW findings — all doc-drift / discoverability, zero functional defects. Fixes:

- **Tightened MSBuild deploy glob** in `Dependencies/TAOM.Dependencies.csproj`: `..\Stubs\**\*.*` → `..\Stubs\**\SubModule.xml`. Prevents accidental deployment of stray `.bak` / `.tmp` / editor swap files to the game install. Build still reports `deployed 4 stub-module files`.
- **Resynced `docs/migration/dr3-maintenance.md` Category 1 table**: `Lib.Harmony 2.2.2` → `2.4.2`, `Bannerlord.MCM 5.11.3` → `5.11.4`. Both pins were bumped in earlier same-day commits (csproj + stubs updated) but the doc summary table lagged.
- **Added inline csproj comment** above the three BUTR `<PackageReference>` lines: "WHEN BUMPING any of these three PackageReference versions: also bump the matching stub `<Version>` in `Stubs/<ID>/_Module/SubModule.xml`." Point-of-edit reminder so a maintainer doesn't have to read external docs first.
- **Added Step 7 to `.serena/memories/task_completion_checklist.md`**: explicit stub-version-sync step. Covers the case where a PR bumps a `<PackageReference>` without reading the maintenance doc.
- **Added build prerequisite note** to dr3-maintenance.md: "Bannerlord must be CLOSED during `./build.ps1`." File-locked `0Harmony.dll` causes `UnauthorizedAccessException`.
- **Wrote RCA** at `docs/reviews/rca-stub-modules-2026-05-25.md` per harness-facts rule (every confirmed finding requires RCA regardless of severity). Documents the systemic doc-drift pattern + proposed feedback memory `feedback_version_pin_doc_drift.md` (grep all docs for old version strings when changing a pin).
- **Created retroactive GitHub issue #221** documenting the third-party-mod-compatibility scenario for future-maintainer discoverability.

Tests: `dotnet build TAOM.Dependencies.csproj` 0 errors; deploy target unchanged behavior. No production C# touched.

### chore(workflow): Codex dispatch is now Claude-direct via `codex exec` (no user terminal step)

> **Note:** the prose entry below was authored in the same session as the code change but was accidentally squashed into the [`chore(deps)`](https://github.com/haterade22/TAOM/commit/30794ea) commit. The actual `.claude/skills/{codex-verify,review-codex,deep-review}/SKILL.md` + `CLAUDE.md` code changes ship in the present commit (immediately following).

Updated `/codex-verify`, `/review-codex`, and `/deep-review --codex` to dispatch Codex DIRECTLY from inside the skill via Bash (`codex exec - < prompt.md > output.md 2>&1` with `run_in_background: true`). Previous workflow asked the user to open a separate terminal and run `/codex:adversarial-review --background` manually; the new flow eliminates that hand-off — invoking the skill IS the dispatch. The harness notification on background-job completion triggers Claude's auto-resume; no `/review-codex` re-invocation needed.

**Why:** the manual terminal step was friction that the session author dropped during the CrashReport feature build, shipping a 60-file feature with 1 HIGH + 2 MED + 3 LOW deep-review findings that should have been caught before close-out. RCA: [docs/reviews/rca-crash-report-2026-05-25.md](docs/reviews/rca-crash-report-2026-05-25.md) meta-finding. Direct dispatch removes the "I forgot to open the terminal" excuse.

**Files modified:**

- [.claude/skills/review-codex/SKILL.md](.claude/skills/review-codex/SKILL.md) — added "Codex CLI invocation contract" section; rewrote Phase 2e from "guide user to dispatch" to "Bash dispatch in background"; documented harness-notification auto-resume; added explicit fallback path for `codex` CLI missing/auth failure.
- [.claude/skills/codex-verify/SKILL.md](.claude/skills/codex-verify/SKILL.md) — same contract; rewrote Steps 1-4 to dispatch directly via Bash with a lighter focused prompt.
- [.claude/skills/deep-review/SKILL.md](.claude/skills/deep-review/SKILL.md) — Step 0 `--codex` mode updated to direct dispatch; explicitly notes parallel Codex run with Claude agents.
- [CLAUDE.md](CLAUDE.md) — Codex Integration section rewritten with the dispatch contract + skill table; Completion Workflow phase blocks updated to remove the "Dispatch to Codex — /codex:adversarial-review --background (terminal)" hand-off lines.
- `~/.claude/commands/codex-verify.md` — user-level slash command updated for consistency (works across all of mikew's projects, not just TAOM).

**"Never auto-invoke" tier unchanged.** `/codex-verify` and `/review-codex` still require explicit user intent — Codex costs real money. The change is that when the user DOES invoke, Claude does the dispatch instead of telling the user where to point a terminal.

**Pre-flight contract:** every dispatch starts with `codex login status`. If not `Logged in using ChatGPT`, the skill stops and tells the user to run `codex login` (interactive browser flow). Claude does NOT attempt authentication.

Not-tested: skills can't be unit-tested; verification is the next `/review-codex` invocation working end-to-end. The CrashReport feature's `/review-codex` run on 2026-05-25 was the first dispatch via the new contract and produced a multi-MB Codex review file at `docs/reviews/codex-adversarial-crash-report-2026-05-25.md`.

### feat(crash-report): comprehensive crash diagnostic capture (10 Harmony Finalizers + AutoGenerated reflection + ZIP bundle)

New feature module at [Main/Features/CrashReport/](Main/Features/CrashReport) — TAOM-native crash diagnostic capture inspired by (but not a port of) BetterExceptionWindow v8.0.0. BEW is GNU AGPL v3, so we authored equivalents from scratch using BEW only as a design reference for *what to patch* and *what to display*; the TaleWorlds API surface BEW patches is uncopyrightable. Full feature doc at [docs/features/crash-report.md](docs/features/crash-report.md).

**Catch points (Patch37_CrashReport — registered FIRST in [SubModule.cs](Main/SubModule.cs:97) `OnSubModuleLoad`).** 10 Harmony Finalizers at priority 800 on `Managed.ApplicationTick` / `ScriptComponentBehavior.OnTick` / `Module.OnApplicationTick` / `MissionView.OnMissionScreenTick` / `ScreenManager.Tick` / `ScreenManager.Update` (private no-arg) / `Mission.Tick` / `MissionBehavior.OnMissionTick` / `MBSubModuleBase.OnSubModuleLoad` — plus reflection-attached Finalizers on every static method in `TaleWorlds.{MountAndBlade,Engine,DotNet}.AutoGenerated.dll` types whose names end with `CallbacksGenerated` (BEW's "native2managed" mode, gated by MCM toggle, default ON). Plus `AppDomain.CurrentDomain.UnhandledException` as a final safety net. All 10 patch targets verified against v1.4.5 install via `ilspycmd`.

**Comprehensive data capture.** [ExceptionContext](Main/Features/CrashReport/Domain/ExceptionContext.cs) DTO aggregates 18 record types covering: identity (Bannerlord + TAOM versions, exe FileVersion, TAOM.dll SHA1, language), exception + inner chain (depth-capped at 10) + `Exception.Data` dictionary, every stack frame with file/line (PDB-aware) + IL offset, Harmony patches affecting **every frame in the stack** (not just the throwing one) + full process-wide inventory grouped by owner, every active mod with main DLL SHA1 + declared deps + dep-order inversion detection + declared XML files, every loaded assembly, campaign state (hero, party with tier histogram, location, recent CampaignEvents ring buffer), mission state (teams + formations + player agent + wielded item), TAOM-specific state (career/resources/feats/revolt/messengers stubs ready for per-feature wiring), full reflected MCM settings snapshot from every `AttributeGlobalSettings`/`AttributePerCampaignSettings` provider (TAOM + third-party), process state (working set, GC totals + gen counts, throwing thread metadata), GPU info via WMI (vendor/driver/VRAM per adapter), display info, OS + locale + CLR, AppDomain, filtered env vars (`BANNERLORD_*`/`TAOM_*`/`DOTNET_*`/`STEAM_*`), frame timing ring buffer (v1 storage only; hook to populate is v2), and log tails (TAOM debug log + RGL log auto-located at `%USERPROFILE%\Documents\Mount and Blade II Bannerlord\logs\`).

**Crash bundle ZIP** — uses in-BCL `System.IO.Compression.ZipArchive` (added `<Reference Include="System.IO.Compression" />` to [Main/TAOM.csproj](Main/TAOM.csproj); no new package). Writes `Logs/taom_crash_{utc}_{sig8}.zip` containing `report.txt` (sectioned plain text) + `report.json` (Newtonsoft.Json) + `taom_debug.log` (live session) + `rgl_log.txt` (live session) + `manifest.txt`. Players upload one file; we reproduce locally. Crash signature is SHA1 of `(ExceptionType + originatingPatchTarget + top-5 frame method names)` → first 8 chars in the filename for visual dedup matching across reports.

**BUTR coexistence.** TAOM ships `Bannerlord.ButterLib` 2.10.4 with its own `ExceptionHandlerSubSystem`. Our [ButterLibExceptionHandlerAdapter](Main/Features/CrashReport/Adapters/ButterLibExceptionHandlerAdapter.cs) reflects in `ExceptionHandlerSubSystem.Instance.Disable()` on first capture so only one crash UI fires. MCM toggle (`Master → Suspend BUTR Exception Handler`, default ON) controls this. No hard compile-time dependency on `Bannerlord.ButterLib.ExceptionHandler.dll`.

**Re-entry protection.** Two layers of thread-static `_handling` guards: one in `CrashReportPatchHelper` (the Finalizer sink) and one in `CrashReportService.HandleException`. A throw inside any collector / renderer breaks the loop and lets vanilla / BUTR take over.

**Player-facing surface.** v1 uses `InformationManager.ShowInquiry` (2-button native dialog: *Continue (risky)* / *Open bundle folder*). Richer Gauntlet overlay deliberately deferred to v2 — the comprehensive data is in the log + bundle either way, and a Gauntlet movie is fragile relative to the diagnostic value. Open-bundle-folder action shells out to `explorer.exe /select,<zip>` so the player can drag-and-drop into Discord / issue tracker.

**MCM page.** Dedicated `TAOM — Crash Report` page (separate from `TaomSettings`) at [CrashReportSettings.cs](Main/Features/CrashReport/CrashReportSettings.cs). Groups: *Master* (enable, suspend BUTR, native-to-managed), *Bundle* (write ZIP), *QA — Dev Triggers* (throw on next mission tick / app tick — auto-reset after firing).

**Dev triggers for QA.** [CrashReportDevTriggerMissionBehavior](Main/Features/CrashReport/DevTriggers/CrashReportDevTrigger.cs) (wired into every mission via `OnMissionBehaviorInitialize`) reads the `ThrowOnNextMissionTick` MCM toggle and throws a tagged `TaomDevTriggerException`. [CrashReportApplicationTickTrigger](Main/Features/CrashReport/DevTriggers/CrashReportApplicationTickTrigger.cs) does the same for `Module.OnApplicationTick` so QA can exercise the pipeline from the main menu without entering a mission.

**`IModLogger` extension.** Added `string? LogFilePath { get; }` to [IModLogger.cs](Main/Core/Logging/IModLogger.cs) so CrashReport can attach the live TAOM debug log to the bundle without downcasting to `FileLogger`. NSubstitute mocks in `TAOM.Tests` auto-implement the new property — no test breakage.

**21 new unit tests** at [TAOM.Tests/Features/CrashReport/](TAOM.Tests/Features/CrashReport) covering the pure utilities: `ExceptionFrameBuilder` (depth cap, null, inner chain, `Data`), `StackFrameSnapshotBuilder`, `CrashSignatureCalculator` (determinism, frame-depth-5 cutoff), `RingBuffer` (overflow ordering, clear, capacity-0 edge), `PlainTextCrashReportRenderer` (all 18 sections render, header signature, inner-chain depth labels, collector failure visibility). Full suite green: 2440/2440 passed, 2 skipped, 0 failed.

Not-tested: 10 Harmony Finalizers + service composition + TaleWorlds-facing collectors (Modules/Mission/Campaign/Gpu/Logs) + ButterLib reflection adapter + UI notifier — all require integration / manual QA. Tagged `Not-tested:` per the commit trailer convention.

**Files added/modified:**

- 18 new DTO records under [Main/Features/CrashReport/Domain/](Main/Features/CrashReport/Domain)
- 14 collectors + 5 utility classes under [Main/Features/CrashReport/Collectors/](Main/Features/CrashReport/Collectors)
- 2 renderers + 1 bundle writer under [Main/Features/CrashReport/Rendering/](Main/Features/CrashReport/Rendering)
- 1 reflection adapter under [Main/Features/CrashReport/Adapters/](Main/Features/CrashReport/Adapters)
- 4 patch / hook classes under [Main/Features/CrashReport/Hooks/](Main/Features/CrashReport/Hooks)
- 3 dev-trigger classes under [Main/Features/CrashReport/DevTriggers/](Main/Features/CrashReport/DevTriggers)
- 1 notifier under [Main/Features/CrashReport/UI/](Main/Features/CrashReport/UI)
- [CrashReportService.cs](Main/Features/CrashReport/CrashReportService.cs), [ICrashReportService.cs](Main/Features/CrashReport/ICrashReportService.cs), [CrashReportIoC.cs](Main/Features/CrashReport/CrashReportIoC.cs), [CrashReportSettings.cs](Main/Features/CrashReport/CrashReportSettings.cs)
- 5 test classes under [TAOM.Tests/Features/CrashReport/](TAOM.Tests/Features/CrashReport)
- [IoC.cs](Main/IoC.cs): register `CrashReportIoC`
- [SubModule.cs](Main/SubModule.cs): apply Patch37 FIRST + subscribe AppDomain hook + attach Native2ManagedPatcher; add `CrashReportDevTriggerMissionBehavior` to every mission
- [Main/TAOM.csproj](Main/TAOM.csproj): added `<Reference Include="System.Management" />` + `<Reference Include="System.IO.Compression" />` + `System.IO.Compression.FileSystem` (.NET 4.7.2 BCL — no new packages)
- [IModLogger.cs](Main/Core/Logging/IModLogger.cs) + [FileLogger.cs](Main/Core/Logging/FileLogger.cs): added `string? LogFilePath { get; }`

Constraint: BEW is GPL/AGPL v3, can't ship its DLL — reimplemented from architectural reference only.
Research: ilspycmd against installed v1.4.5 DLLs for every patch target signature + ButterLib reflection target.
Rejected: WinForms HTML window (BEW's choice — drags `System.Windows.Forms` into TAOM.dll, fullscreen DX focus-steal risk, ugly mismatch); Gauntlet overlay (deferred to v2 — fragile relative to data value).
Save-compat: No save-data changes. New MCM settings file created on first launch.
Not-tested: 10 Harmony Finalizer patches + service composition + TaleWorlds-facing collectors + UI notifier (manual QA via MCM dev triggers).


### docs(target): declare TAOM is on Bannerlord 1.4.5

Bumped declarative target-version statements from v1.3.15 → v1.4.5 across [README.md](README.md), [CLAUDE.md](CLAUDE.md), [AGENTS.md](AGENTS.md), and four scoped rule files (`adapters`, `gui-ui`, `harmony-patches`, `environment-failures`). Reframed the CLAUDE.md "🚧 Active migration" banner and [docs/migration/TRACKING.md](docs/migration/TRACKING.md) header to honestly state: S0–S5b ✅ landed 2026-05-22 (adapters, GameModels, equipment XML, roster authoring); S6–S12 (smoke test, per-tier feature validation, Codex review, closeout) were rolled into ongoing feature work on the `bannerlord-1.4.5` branch rather than executed as discrete gates. TRACKING.md S6 note now directs runtime crashes to one-off `/investigate`, not a stage gate.

Also updated [docs/reviews/REVIEW-PLAN.md](docs/reviews/REVIEW-PLAN.md) Wave 1 patch-target verification version, and [docs/tools/spider-skeleton-tpac-tools.md](docs/tools/spider-skeleton-tpac-tools.md) GitHub raw-URL branch from `bannerlord-1.3.15` → `bannerlord-1.4.5`.

Historical narrative preserved as Tier-4 (feature descriptions citing "ported in 1.3.15 era", EMPIRICAL `memory/feedback_*.md` entries with version-stamped observations, archived review prompts, migration-source-version refs in `1.3.15 → 1.4.5` framings). Verification grep on `1\.3\.15` confirmed zero residual forward-looking claims.

### fix(deps): auto-enable stub modules so third-party mods become toggleable

Follow-up to commit `031283c` (four BUTR stub modules). In-game verification surfaced that the stubs were deployed correctly but the launcher's first-launch enablement logic left them **un-ticked** by default. Decompiling `LauncherModsVM.cs:~350`:

```csharp
bool flag = !HasUserData
    ? (item.IsRequiredOfficial || item.IsDefault)
    : (item.IsRequiredOfficial || userModData.IsSelected || userModData.IsUpdatedToBeDefault(item));
item.IsSelected = item.IsNative || (flag && AreAllDependenciesOfModulePresent(item));
```

Stubs were shipped with `DefaultModule="false"` and `ModuleType="Community"` — neither `IsRequiredOfficial` nor `IsDefault` true, so they landed unchecked. Third-party mods like "Bannerlord Cheats Reload" declaring `<DependedModule Id="Bannerlord.Harmony"/>` reported the dep as missing-from-user-config and stayed greyed out in the launcher mod list, even though the stub files were present on disk.

Fix: flipped `<DefaultModule value="false"/>` → `<DefaultModule value="true"/>` in all four `Stubs/Bannerlord.*/_Module/SubModule.xml` files. Matches the BetaDeps-community convention for infrastructure stubs. Users can still untick stubs manually if they want to install standalone BUTR modules from Workshop instead.

Also documented in `docs/migration/dr3-maintenance.md` the distinction between two unrelated launcher concepts that look similar in the UI:
- **`IsDisabled`** (`LauncherModuleVM.cs:297`) — toggleability gate; fires when deps are missing.
- **`IsDangerous`** (`LauncherModuleVM.cs:280-282`) — red `(!)` icon for "Couldn't verify some or all of the code". Permanent warning for any unsigned/third-party DLL, **independent of toggleability**. Every non-Bannerlord mod gets this icon — it does not block enabling the mod.

Verification: `dotnet build Dependencies/TAOM.Dependencies.csproj` redeployed 4 stub files; in-place `grep DefaultModule` against each deployed `SubModule.xml` confirms `value="true"`. `dotnet test TAOM.Tests` 2,440 passing (no production C# touched). User in-game verification pending: stubs should auto-tick on next launcher startup; third-party mods previously greyed out should now be toggleable.

### feat(crash-report): comprehensive crash diagnostic capture (10 Harmony Finalizers + AutoGenerated reflection + ZIP bundle)

New feature module at [Main/Features/CrashReport/](Main/Features/CrashReport) — TAOM-native crash diagnostic capture inspired by (but not a port of) BetterExceptionWindow v8.0.0. BEW is GNU AGPL v3, so we authored equivalents from scratch using BEW only as a design reference for *what to patch* and *what to display*; the TaleWorlds API surface BEW patches is uncopyrightable. Full feature doc at [docs/features/crash-report.md](docs/features/crash-report.md).

**Catch points (Patch37_CrashReport — registered FIRST in [SubModule.cs](Main/SubModule.cs:97) `OnSubModuleLoad`).** 10 Harmony Finalizers at priority 800 on `Managed.ApplicationTick` / `ScriptComponentBehavior.OnTick` / `Module.OnApplicationTick` / `MissionView.OnMissionScreenTick` / `ScreenManager.Tick` / `ScreenManager.Update` (private no-arg) / `Mission.Tick` / `MissionBehavior.OnMissionTick` / `MBSubModuleBase.OnSubModuleLoad` — plus reflection-attached Finalizers on every static method in `TaleWorlds.{MountAndBlade,Engine,DotNet}.AutoGenerated.dll` types whose names end with `CallbacksGenerated` (BEW's "native2managed" mode, gated by MCM toggle, default ON). Plus `AppDomain.CurrentDomain.UnhandledException` as a final safety net. All 10 patch targets verified against v1.4.5 install via `ilspycmd`.

**Comprehensive data capture.** [ExceptionContext](Main/Features/CrashReport/Domain/ExceptionContext.cs) DTO aggregates 18 record types covering: identity (Bannerlord + TAOM versions, exe FileVersion, TAOM.dll SHA1, language), exception + inner chain (depth-capped at 10) + `Exception.Data` dictionary, every stack frame with file/line (PDB-aware) + IL offset, Harmony patches affecting **every frame in the stack** (not just the throwing one) + full process-wide inventory grouped by owner, every active mod with main DLL SHA1 + declared deps + dep-order inversion detection + declared XML files, every loaded assembly, campaign state (hero, party with tier histogram, location, recent CampaignEvents ring buffer), mission state (teams + formations + player agent + wielded item), TAOM-specific state (career/resources/feats/revolt/messengers stubs ready for per-feature wiring), full reflected MCM settings snapshot from every `AttributeGlobalSettings`/`AttributePerCampaignSettings` provider (TAOM + third-party), process state (working set, GC totals + gen counts, throwing thread metadata), GPU info via WMI (vendor/driver/VRAM per adapter), display info, OS + locale + CLR, AppDomain, filtered env vars (`BANNERLORD_*`/`TAOM_*`/`DOTNET_*`/`STEAM_*`), frame timing ring buffer (v1 storage only; hook to populate is v2), and log tails (TAOM debug log + RGL log auto-located at `%USERPROFILE%\Documents\Mount and Blade II Bannerlord\logs\`).

**Crash bundle ZIP** — uses in-BCL `System.IO.Compression.ZipArchive` (added `<Reference Include="System.IO.Compression" />` to [Main/TAOM.csproj](Main/TAOM.csproj); no new package). Writes `Logs/taom_crash_{utc}_{sig8}.zip` containing `report.txt` (sectioned plain text) + `report.json` (Newtonsoft.Json) + `taom_debug.log` (live session) + `rgl_log.txt` (live session) + `manifest.txt`. Players upload one file; we reproduce locally. Crash signature is SHA1 of `(ExceptionType + originatingPatchTarget + top-5 frame method names)` → first 8 chars in the filename for visual dedup matching across reports.

**BUTR coexistence.** TAOM ships `Bannerlord.ButterLib` 2.10.4 with its own `ExceptionHandlerSubSystem`. Our [ButterLibExceptionHandlerAdapter](Main/Features/CrashReport/Adapters/ButterLibExceptionHandlerAdapter.cs) reflects in `ExceptionHandlerSubSystem.Instance.Disable()` on first capture so only one crash UI fires. MCM toggle (`Master → Suspend BUTR Exception Handler`, default ON) controls this. No hard compile-time dependency on `Bannerlord.ButterLib.ExceptionHandler.dll`.

**Re-entry protection.** Two layers of thread-static `_handling` guards: one in `CrashReportPatchHelper` (the Finalizer sink) and one in `CrashReportService.HandleException`. A throw inside any collector / renderer breaks the loop and lets vanilla / BUTR take over.

**Player-facing surface.** v1 uses `InformationManager.ShowInquiry` (2-button native dialog: *Continue (risky)* / *Open bundle folder*). Richer Gauntlet overlay deliberately deferred to v2 — the comprehensive data is in the log + bundle either way, and a Gauntlet movie is fragile relative to the diagnostic value. Open-bundle-folder action shells out to `explorer.exe /select,<zip>` so the player can drag-and-drop into Discord / issue tracker.

**MCM page.** Dedicated `TAOM — Crash Report` page (separate from `TaomSettings`) at [CrashReportSettings.cs](Main/Features/CrashReport/CrashReportSettings.cs). Groups: *Master* (enable, suspend BUTR, native-to-managed), *Bundle* (write ZIP), *QA — Dev Triggers* (throw on next mission tick / app tick — auto-reset after firing).

**Dev triggers for QA.** [CrashReportDevTriggerMissionBehavior](Main/Features/CrashReport/DevTriggers/CrashReportDevTrigger.cs) (wired into every mission via `OnMissionBehaviorInitialize`) reads the `ThrowOnNextMissionTick` MCM toggle and throws a tagged `TaomDevTriggerException`. [CrashReportApplicationTickTrigger](Main/Features/CrashReport/DevTriggers/CrashReportApplicationTickTrigger.cs) does the same for `Module.OnApplicationTick` so QA can exercise the pipeline from the main menu without entering a mission.

**`IModLogger` extension.** Added `string? LogFilePath { get; }` to [IModLogger.cs](Main/Core/Logging/IModLogger.cs) so CrashReport can attach the live TAOM debug log to the bundle without downcasting to `FileLogger`. NSubstitute mocks in `TAOM.Tests` auto-implement the new property — no test breakage.

**21 new unit tests** at [TAOM.Tests/Features/CrashReport/](TAOM.Tests/Features/CrashReport) covering the pure utilities: `ExceptionFrameBuilder` (depth cap, null, inner chain, `Data`), `StackFrameSnapshotBuilder`, `CrashSignatureCalculator` (determinism, frame-depth-5 cutoff), `RingBuffer` (overflow ordering, clear, capacity-0 edge), `PlainTextCrashReportRenderer` (all 18 sections render, header signature, inner-chain depth labels, collector failure visibility). Full suite green: 2440/2440 passed, 2 skipped, 0 failed.

Not-tested: 10 Harmony Finalizers + service composition + TaleWorlds-facing collectors (Modules/Mission/Campaign/Gpu/Logs) + ButterLib reflection adapter + UI notifier — all require integration / manual QA. Tagged `Not-tested:` per the commit trailer convention.

**Files added/modified:**

- 18 new DTO records under [Main/Features/CrashReport/Domain/](Main/Features/CrashReport/Domain)
- 14 collectors + 5 utility classes under [Main/Features/CrashReport/Collectors/](Main/Features/CrashReport/Collectors)
- 2 renderers + 1 bundle writer under [Main/Features/CrashReport/Rendering/](Main/Features/CrashReport/Rendering)
- 1 reflection adapter under [Main/Features/CrashReport/Adapters/](Main/Features/CrashReport/Adapters)
- 4 patch / hook classes under [Main/Features/CrashReport/Hooks/](Main/Features/CrashReport/Hooks)
- 3 dev-trigger classes under [Main/Features/CrashReport/DevTriggers/](Main/Features/CrashReport/DevTriggers)
- 1 notifier under [Main/Features/CrashReport/UI/](Main/Features/CrashReport/UI)
- [CrashReportService.cs](Main/Features/CrashReport/CrashReportService.cs), [ICrashReportService.cs](Main/Features/CrashReport/ICrashReportService.cs), [CrashReportIoC.cs](Main/Features/CrashReport/CrashReportIoC.cs), [CrashReportSettings.cs](Main/Features/CrashReport/CrashReportSettings.cs)
- 5 test classes under [TAOM.Tests/Features/CrashReport/](TAOM.Tests/Features/CrashReport)
- [IoC.cs](Main/IoC.cs): register `CrashReportIoC`
- [SubModule.cs](Main/SubModule.cs): apply Patch37 FIRST + subscribe AppDomain hook + attach Native2ManagedPatcher; add `CrashReportDevTriggerMissionBehavior` to every mission
- [Main/TAOM.csproj](Main/TAOM.csproj): added `<Reference Include="System.Management" />` + `<Reference Include="System.IO.Compression" />` + `System.IO.Compression.FileSystem` (.NET 4.7.2 BCL — no new packages)
- [IModLogger.cs](Main/Core/Logging/IModLogger.cs) + [FileLogger.cs](Main/Core/Logging/FileLogger.cs): added `string? LogFilePath { get; }`

Constraint: BEW is GPL/AGPL v3, can't ship its DLL — reimplemented from architectural reference only.
Research: ilspycmd against installed v1.4.5 DLLs for every patch target signature + ButterLib reflection target.
Rejected: WinForms HTML window (BEW's choice — drags `System.Windows.Forms` into TAOM.dll, fullscreen DX focus-steal risk, ugly mismatch); Gauntlet overlay (deferred to v2 — fragile relative to data value).
Save-compat: No save-data changes. New MCM settings file created on first launch.
Not-tested: 10 Harmony Finalizer patches + service composition + TaleWorlds-facing collectors + UI notifier (manual QA via MCM dev triggers).

## 2026-05-24

### feat(faction-map): kingdom card overhaul — multi-unit support, painted portraits, tuned difficulty, content refresh

Large session-wide refresh of the FactionMap kingdom-selection screen on the CharacterCreation flow.

**Schema refactor — multiple special units per kingdom.** Changed [factions.json](Main/_Module/ModuleData/factionmap/factions.json) schema from single `"special_unit": {...}` object to `"special_units": [...]` array so factions can list >1 iconic unit. Mordor now displays Black Uruks + Trolls as two stacked entries (was a single combined-string workaround). All 16 playable-faction entries migrated. New parser ([FactionDataParser.cs](Main/Features/FactionMap/FactionDataParser.cs)) accepts BOTH new array form and legacy single-object form — backward-compat for any third-party mods or hand-edited configs. New VM class [FactionSpecialUnitItemVM.cs](Main/Features/FactionMap/ViewModels/FactionSpecialUnitItemVM.cs) mirrors `FactionPerkItemVM`; [FactionSelectionVM.cs](Main/Features/FactionMap/ViewModels/FactionSelectionVM.cs) exposes `MBBindingList<FactionSpecialUnitItemVM>`; [CharacterCreationCultureStage.xml](Main/_Module/GUI/PreFabs/CharacterCreation/CharacterCreationCultureStage.xml) prefab replaces two single TextWidgets with a `ListPanel`+`ItemTemplate` iterating the collection. Section header renamed to plural "Special Units".

**Difficulty label expansion to 7 tiers + per-kingdom retuning.** Expanded [`FactionSelectionService.FormatDifficultyText`](Main/Features/FactionMap/FactionSelectionService.cs) from `Easy/Normal/Hard/Very Hard/Extreme` (5 tiers) to `Very Easy/Easy/Medium/Medium-Hard/Hard/Very Hard/Extreme` (7 tiers). Renumbered all 16 difficulty values in factions.json to fit the new scheme — e.g. Rivendell `Very Hard` → `Very Easy`, Isengard `Very Hard` → `Very Hard` (unchanged label but new int 6), Dunland `Very Hard` → `Hard`, Mordor `Hard` → `Easy`. Difficulty text margin reduced from 8 to 2 so the label no longer touches the divider line above it.

**Kingdom card frame: landscape portrait + compact card.** Resized `FactionImageWidget` in [CharacterCreationCultureStage.xml](Main/_Module/GUI/PreFabs/CharacterCreation/CharacterCreationCultureStage.xml) from `429×792` (portrait, aspect 0.54) to `429×240` (landscape, aspect 1.79) to match the natural 16:9 of the new painted portraits. Parent card shrunk `440×1023` → `440×480` so the whole card compacts at the bottom-left of the screen.

**14 painted kingdom portraits at full source resolution.** Generated and installed Alan Lee / John Howe style painted portraits for all 14 playable kingdoms (Mirkwood, Lothlórien, Rivendell, Erebor, Rohan, Gondor, Mordor, Dunland, Isengard, Dol Guldur, Gundabad, Rhûn, Harad, Umbar) at full 5504×3072 source resolution (~30 MB each — ~380 MB total). Each kingdom-tier portrait was also mirrored into all sub-faction sibling files (`stewardship_of_gondor`, `tribes_of_harad`, `havens_of_umbar`, `dominion_of_mordor`, `dark_lands_of_mordor`, `shadow_of_dol_guldur`, `dominion_of_isengard`, `clans_of_dunland`, `hill_men_of_dunland`, `overlordship_of_dol_guldur`, `orcs_of_gundabad`, `overlordship_of_gundabad`, `easterlings_of_rhun`, `golden_realm_of_rhun`, `taskralan_of_harwan`, `elves_of_mirkwood`, `elves_of_lothlorien`, `elves_of_rivendell`, `dwarves_of_erebor`, `kingdom_of_imladris`) so every name-aliased card matches its parent kingdom.

**Content updates: Gondor, Mordor, Rohan perks/strengths/weaknesses.** Per-kingdom content rewrites in factions.json: Stewardship of Gondor (perks: Dunedain Blood + Gondor's Courage; "Varies — Elite Units in Specific Regions"; 4-item balanced strengths; region-vulnerability weaknesses), Dominion of Mordor (perks: Dark Lord's Will + Sauron's Hordes + Grond's Hammer; 2 special units; 9-item over-the-top strengths list; weak troops + slow replenishment weaknesses), Kingdom of Rohan (added "Strong in specific terrain - Plains"; replaced 3 weaknesses with cavalry-only / archery-mediocre / border-pressure / weak-alliance set).

**Companion UI fixes batched in.** Career button repositioned from center-screen to upper-right of CharacterDeveloper header (220×93 banner next to the name plate); Presets button in the inventory/trade screen moved from bottom-center (where it overlapped the gold-cost display) to southwest of the character preview.

Tests: 3 new MSTest cases on `FactionConfigProvider` — multi-entry array parsing (Mordor case), legacy single-object backward-compat coercion, plus the existing single-entry assertion updated to new array form. All 61 FactionMap tests passing post-refactor.

Not-tested: live in-game appearance of the new landscape portrait frame on the user's 49" ultrawide monitor — initial coordinates are estimates and may need 1-2 line follow-up tweaks based on visual review.

### fix(career-system): all 50 ability tooltips now state actual archetype effects + duration

Every career ability tooltip in [taom_ability_templates.xml](Main/_Module/ModuleData/career_system/taom_ability_templates.xml) and the mirrored [taom_career_strings.xml](Main/_Module/ModuleData/taom_career_strings.xml) was descriptive-only — "boosting your ranged damage, draw speed, and movement speed for a short duration" — without specific numbers. Two Infantry tooltips (`captain_of_osgiliath`, `watchman_of_stangard`) wrongly stated "boosting melee damage by 20" while the engine actually applies 15% per the tuning XML.

Per user direction (lore + specifics, text matches actual tuning), rewrote all 50 tooltips using the per-archetype numbers from [taom_ability_tuning.xml](Main/_Module/ModuleData/career_system/taom_ability_tuning.xml):

| Archetype | Effect text now displayed |
|-----------|---------------------------|
| Infantry  | "boosts +15% melee damage and +10% damage reduction to allies within 50m for 8s" |
| Ranged    | "boosts +20% ranged damage, +20% draw speed, and +15% movement speed for 8s" |
| Cavalry   | "boosts +25% charge damage, +20% mount speed, and +10% melee damage for 8s" |

Each tooltip keeps its lore lead-in (e.g., "Spring a deadly ambush", "Bring your war-hammer crashing down with titanic force") and appends the specific stat clause via em-dash. The 49 abilities at 8s duration plus `olog_hai_warchief` at 10s (its actual template duration) are all reflected.

Mechanical rewrite via new tool [tools/rewrite_ability_tooltips.py](tools/rewrite_ability_tooltips.py) — single source-of-truth list of `(career_id, archetype, lore_lead_in)` tuples + per-archetype effect strings + duration override map. Idempotent; can be re-run if tuning numbers change in the future.

Translations in `Main/_Module/ModuleData/Languages/<LANG>/std_taom_career_strings_*.xml` (PL hand-curated + 11 AI-translated) are now stale relative to English. Translation pipeline (`tools/translate_with_claude.py`) needs to re-run to propagate; tracked separately.

Files: ability templates XML, strings XML, new rewrite tool. 100 tooltip rewrites (50 abilities × 2 files). No code change. No new items.
Save-compat: tooltip text only — no game-state impact.
Not-tested: in-game spot-check that any one ability now shows the new format (e.g., Hold the Line → "boosts +15% melee damage and +10% damage reduction to allies within 50m for 8s").

### feat(deps): four stub modules for third-party mod compatibility (Bannerlord.Harmony / .UIExtenderEx / .ButterLib / .MBOptionScreen)

DR3 follow-up. After bundling all BUTR runtime DLLs inside `TAOM.Dependencies` (single `<Id value="TAOM.Dependencies"/>`), third-party Bannerlord mods that declare `<DependedModule Id="Bannerlord.Harmony"/>` (or the other standard BUTR IDs) became un-toggleable in the vanilla launcher's mod menu — the launcher's `AreAllDependenciesOfModulePresent` check does an exact string match on `m.Id` and couldn't find those module IDs anywhere.

Adopted the BUTR-community-standard **stub module pattern** (used by the BetaDeps mod on Nexus). Ships four passive `SubModule.xml` files in new `Stubs/` directory:

| Folder | `<Id>` | `<Version>` |
|---|---|---|
| `Stubs/Bannerlord.Harmony/_Module/` | `Bannerlord.Harmony` | `v2.4.2` |
| `Stubs/Bannerlord.UIExtenderEx/_Module/` | `Bannerlord.UIExtenderEx` | `v2.13.1` |
| `Stubs/Bannerlord.ButterLib/_Module/` | `Bannerlord.ButterLib` | `v2.10.4` |
| `Stubs/Bannerlord.MBOptionScreen/_Module/` | `Bannerlord.MBOptionScreen` | `v5.11.4` |

Each stub declares the standard BUTR ID + `<DependedModule Id="TAOM.Dependencies"/>` + `<SubModules />` empty (no DLLs load from the stub — real DLLs come from TAOM.Dependencies). New `DeployTAOMDependenciesStubs` MSBuild target in `Dependencies/TAOM.Dependencies.csproj` deploys all four to `<GameFolder>/Modules/Bannerlord.*/` after `PostBuildCopyToModules`.

Updated `docs/migration/dr3-maintenance.md` with a "Stub modules" section explaining the maintenance rule (when a BUTR `PackageReference` version bumps, bump the matching stub's `<Version>` too). Replaced old "disable external Bannerlord.Harmony module" mitigation with the more accurate "uninstall any standalone BUTR modules; TAOM.Dependencies + stubs provide everything" guidance.

Verification: `dotnet build` deploys 4 stub-module files (visible in MSBuild output as `DeployTAOMDependenciesStubs: deployed 4 stub-module files`); `ls $game/Modules` shows the four new `Bannerlord.*` folders. `dotnet test TAOM.Tests` remains at 2,325 passing (no production C# code touched). User in-game verification pending: third-party mods declaring the standard BUTR IDs should now be toggleable in the launcher.

Research: vanilla Bannerlord launcher decompiled at `ModuleHelper.AreAllDependenciesOfModulePresent` + `LauncherModuleVM.UpdateIsDisabled` (no multi-ID alias support — exact string match only; `optional="true"` would also work but is a per-third-party-mod declaration we don't control).

### fix(gondor): clothe naked prison guard + drop `_slim` body-item variants everywhere

Two XML data fixes in [`npcs_gondor.xml`](Main/_Module/ModuleData/characters/npcs_gondor.xml), [`troops_gondor.xml`](Main/_Module/ModuleData/troops/troops_gondor.xml), [`troops_umbar.xml`](Main/_Module/ModuleData/troops/troops_umbar.xml), and [`taom_wanderer_equipment.xml`](Main/_Module/ModuleData/equipmentsets/taom_wanderer_equipment.xml):

1. `prison_guard_gondor` had only `Item0` (sword) + `Item1` (shield) in its inline `<EquipmentRoster civilian="true">` — engine rendered the underwear mesh. Added Head/Body/Gloves/Leg slots mirroring `guard_gondor`'s armor (Anórien helmet/bracer/greaves + Cair Andros chainmail half-b).
2. Per user direction, stripped `_slim` suffix from every active body-item reference (27 refs total): 25 across the 4 ModuleData XML files above (`gondor_noble_{coat,jerkin}_{a,b}_slim` and `ithilien_jerkin_{long,long_var,short,short_var}_slim` → non-slim) plus 1 in [`tools/generate_batch2_wanderers.py`](tools/generate_batch2_wanderers.py) (would re-introduce `_slim` on next regen) and 5 in [`docs/features/gondor-ithilien-ranger.md`](docs/features/gondor-ithilien-ranger.md) (roster-content table + dependencies list resync to XML reality). Women now wear the same item rows as men. All 8 non-slim equivalents verified to exist in `LOTRLOME_Armory/.../gondor/body_armors.xml`. Remaining `_slim` occurrences in the repo (translation_cache JSONs, `armor_rebalance.csv`, v1.4 migration docs' unrelated `no_slim` XML *attribute*) are records about items that still exist in the Armory module — intentionally left alone.

Not-tested: in-game visual pass on female meshes to check for clipping on the male-cut Body items (acknowledged risk of the slim-strip).

### feat(career-system): warg-mount cavalry starters for Isengard, Gundabad, Mordor, Dol Guldur

Authored 4 new culture × 2 genders = 8 cavalry rosters in [taom_career_starting_equipment.xml](Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml). All four cultures' existing troop XMLs ([troops_isengard.xml](Main/_Module/ModuleData/troops/troops_isengard.xml), [troops_gundabad.xml](Main/_Module/ModuleData/troops/troops_gundabad.xml), [troops_mordor.xml](Main/_Module/ModuleData/troops/troops_mordor.xml), [troops_dolguldur.xml](Main/_Module/ModuleData/troops/troops_dolguldur.xml)) use wargs as the mount of choice — confirmed by grepping `slot="Horse"` references which all resolved to `Item.warg_brown` / `Item.warg_dark`. So cavalry-archetype career players (`warg_scout` / `warg_pack_leader` / `snaga_rider` / `fell_rider`) now spawn on `warg_brown` + `warg_saddle` from the Alliance.Wargs module instead of falling through to the youth/title-default mount.

Each roster only overrides Horse + HorseHarness — `Equipment.FillFrom` is a slot-merge, so body/leg/weapons cleanly inherit from the culture-default applied just before. Full starter armor sets for these cultures are a separate follow-up (matching the Gondor proof-of-life pattern from 2026-05-19).

Files: 8 new EquipmentRoster entries, single-file edit. No new items authored — `warg_brown` + `warg_saddle` already ship with Alliance.Wargs.
Save-compat: equipment binds at character-creation finalize; no migration.
Not-tested: in-game spawn (Isengard/Gundabad/Mordor/Dol Guldur → cavalry career → confirm warg mount + warg saddle in inventory).

### fix(career-system): Gondor cavalry starter — low-tier horse + low-armor harness

`player_career_gondor_cavalry_m/f` were spawning brand-new players on `Item.charger` (vanilla war-horse, difficulty 30, charge_damage 22) with `Item.chain_horse_harness` (28-armor chainmail). Both too good for a starter.

Per user direction:
- Horse: `charger` → `saddle_horse` (vanilla lowest mountable; difficulty 0, charge_damage 12, item_category `sumpter_horse`).
- Harness: `chain_horse_harness` → new `starter_cavalry_gondor_horse_armor_a` ("[Gondor] Riding Caparison"). Authored in [`LOTRAOM_horses.xml`](file:///E:/Steam/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml) reusing the `lrd_horse_armour_2` mesh from `gondor_horse_armor_1` (same visual as the in-game "[Gondor] Horse Armour") but `body_armor="10"` (down from 28), `weight="10"` (down from 25), `material_type="Leather"`.

Existing `gondor_horse_armor_1` is unchanged — lord/troop rosters that reference it still get the 28-armor version.

Files: [`taom_career_starting_equipment.xml`](Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml) (4 slot swaps across the two cavalry rosters), `LOTRAOM_horses.xml` in LOTRLOME_Armory (one new item).
Save-compat: equipment binds from XML at character-creation finalize; no migration.
Not-tested: in-game spawn (Gondor → Knight of Belfalas → confirm saddle horse + 10-armor caparison).

### Feat(diagnostic): MissionDiagnostic feature — comprehensive crash-investigation logging in `taom_debug_*.log`

Follow-up to the BehaviorTrees inlining (#217). Two users still crashing on first battle on `bannerlord-1.4.5` with NRE in `Mission.CheckMissionEnded`, root cause still unidentified after Codex caught the original RCA was wrong. This adds in-process logging so the NEXT user crash report tells us the offender directly instead of requiring debugger inspection.

**What it captures:**

1. **Session snapshot** (one-time, on `OnGameStart`):
   - OS / CLR / machine / core count
   - Bannerlord (Native) version
   - All active modules + versions (via `ModuleHelper.GetActiveModules()`)
   - All loaded BUTR/MCM/Bannerlord.*/Harmony assemblies + versions (catches version drift on community DLLs)
   - Campaign state: in-game time, MainHero name + culture + kingdom

2. **Mission start snapshot** (every battle, on first `OnMissionTick`):
   - Scene name + `MissionBehaviors.Count` + `MissionLogics.Count` + null-slot count
   - Every `MissionBehavior` dumped: index, full type name, `BehaviorType`, `IsMissionLogic`, assembly
   - **Auto-flags any class returning `BehaviorType=Logic` while not inheriting `MissionLogic`** at ERROR level with the explicit `← OFFENDER` marker + assembly name. This is the null-cast pattern that NREs `CheckMissionEnded` every tick. If a user uploads a crash log, the offender's class + DLL is now in the log file.

3. **Action-set scan** (first 5 seconds of every mission):
   - For each unique `(actionSetName, raceName)` combo observed on a spawned Agent, log once at INFO level. Catches cases like an elf-race agent ending up with `as_human_warrior` — useful for the action-set theory.
   - Self-disables after 5s; no per-frame cost beyond that.

**Implementation:**

- `Main/Features/MissionDiagnostic/IMissionDiagnosticService.cs` — interface
- `Main/Features/MissionDiagnostic/MissionDiagnosticService.cs` — gathers + writes logs via `IModLogger`
- `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs` — `: MissionLogic` boundary (per `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance`)
- `Main/Features/MissionDiagnostic/MissionDiagnosticIoC.cs` — DI registration
- `Main/IoC.cs` — added registration call
- `Main/SubModule.cs` — added session-snapshot call in `OnGameStart`; added `MissionDiagnosticBehavior` to mission init (LAST so it sees all behaviors added by TAOM AND other mods)

**Best-effort design:** every log path is wrapped in try/catch — a diagnostic failure NEVER blocks gameplay. Failures log a single WARNING with the exception type and message.

**No regression test:** the feature exists to capture data the test harness can't simulate (real Bannerlord runtime + real mod stack). Verified via build + smoke test on author's machine.

**Verified:** `dotnet build` clean, `dotnet test` 2416 passing (same as before — diagnostic has no testable surface), 1 pre-existing unrelated failure (`GetVolunteerTroopId_EreborCulture_HighRoll`), 2 skipped.

---

### Feat(behaviortrees): inline vendored BehaviorTreeWrapper + BehaviorTrees into TAOM.dll for full source ownership; fix v1.4.5 double-tick regression

Two findings from `/deep-review` on the localization-pipeline session:

1. **HIGH (perf):** `tools/translate_with_claude.py:write_back()` compiled N regex patterns and ran N `subn()` calls per file. For the 1,431-entry XSLT file × 12 languages, this wasted ~5 minutes of CPU per full-suite run. Replaced with a single regex using id alternation + dictionary lookup in the replacement callback — one compile, one sub. ~10-50× speedup. Verified correctness with a synthetic test covering XML-attr escape handling, partial updates, and tag swap.

2. **LOW (config drift):** Spanish `lang_name` was `"Latin American Spanish"` in `translate_with_claude.py` but `"Spanish (LA)"` in `rebuild_translation_files.py`. Latent — only activated if the translate-script's output is consumed directly without going through `rebuild`. Aligned to `"Spanish (LA)"` (matches what's already on disk and what the canonical rebuild path emits).

Both findings stem from duplicate logic between the two Python tools (LANGUAGES dict, source-XML list, XML escape logic, `{=KEY}default` parsing). Documented in [`docs/reviews/rca-localization-pipeline-2026-05-24.md`](docs/reviews/rca-localization-pipeline-2026-05-24.md) — deferred refactor (extract `tools/_loc_common.py`) flagged for the next localization-tool change. Both tools are stable; surgical fix preferred over mid-session refactor.

No re-translation needed — both bugs were either output-preserving (perf) or latent (mismatch not yet activated in canonical path).

### Feat(behaviortrees): inline vendored BehaviorTreeWrapper + BehaviorTrees into TAOM.dll for full source ownership; fix v1.4.5 double-tick regression

**Codex correction (must read before treating this as the looter-crash fix):** the original RCA in this entry identified the deleted `BehaviorTreeWrapper.dll` as the source of the null `MissionLogics` entry that NRE'd `CheckMissionEnded`. Codex adversarial review caught that conclusion is wrong: the deleted DLL returned `BehaviorType => (MissionBehaviorType)1`, and v1.4.5 enum is `Logic=0, Other=1`, so it was actually reporting **Other** — vanilla would have put it in `_otherMissionBehaviors`, never in `MissionLogics`. **The user's actual crash root cause is still unidentified.** See [docs/reviews/rca-looter-battle-nre-2026-05-24.md](docs/reviews/rca-looter-battle-nre-2026-05-24.md) for the post-Codex revision and the follow-up investigation plan (likely a community DLL — MCM, ButterLib, UIExtenderEx, or a TAOM class the source-grep audit missed).

**What this commit DOES deliver:**

Two users on `bannerlord-1.4.5` reported a `System.NullReferenceException` in `TaleWorlds.MountAndBlade.Mission.CheckMissionEnded()`. Original RCA pinned the cause on the vendored `BehaviorTreeWrapper.dll`'s `BehaviorTreeMissionLogic`. Codex confirmed this was a misdiagnosis (see above). Despite that, this commit ships three real wins:

1. **Single-DLL ship surface + full source ownership** — both vendored libraries (no upstream source repo for either) decompiled and inlined into TAOM.dll. Future bugs in this code are now fixable by `Edit`.
2. **Codex F1 fix (real v1.4.5 regression):** removed manual `comp.OnTick(dt)` from `WargMissionBehavior.cs:127` and `SpiderMissionBehavior.cs:152`. The `OnTickAsAI → OnTick` rename combined with v1.4.5 `Agent.Tick:4768` auto-calling `component.OnTick(dt)` every frame would have caused 2× ticks per frame on every warg/spider. Vanilla auto-tick now handles BT components correctly for both player- and AI-controlled agents.
3. **7 inherited perf issues fixed** (E1–E7 from `/deep-review`). All were in the original vendored DLL; visible now that we own the source.

**Detail (no upstream source repo for either DLL, so rebuilt both in-tree for permanent ownership):**

1. Decompiled `Main/_Module/bin/Win64_Shipping_Client/BehaviorTreeWrapper.dll` (~1300 lines) and `BehaviorTrees.dll` (~980 lines) via `ilspycmd`.
2. Inlined cleaned-up source into `Main/BehaviorTreeWrapper/` and `Main/BehaviorTrees/`. Both compile into `TAOM.dll` — single ship surface, no separate DLLs.
3. Defensive inheritance change: `BehaviorTreeMissionLogic : MissionLogic` (was `: MissionBehavior`). Originally framed as the bug fix; per Codex this is actually a no-op for the reported crash but kept as a defensive change so the wrapper participates correctly in `MissionLogics` iteration if any future TaleWorlds version reaches it there.
4. Reconciled v1.3 → v1.4.5 API drift surfaced by the rebuild: `AgentComponent.OnTickAsAI(float)` → `OnTick(float)` in `BehaviorTreeAgentComponent`. Codex Finding 1 caught that this rename, combined with v1.4.5 `Agent.Tick` auto-calling `component.OnTick(dt)`, would cause 2× ticks per frame at the manual call sites in `WargMissionBehavior.cs:127` + `SpiderMissionBehavior.cs:152`. **Both manual calls removed** — vanilla auto-tick handles BT components every frame in v1.4.5. The IsActive-pruning loop in each behavior is preserved (vanilla doesn't prune our shadow list). Also fixed `MBInformationManager.AddQuickInformation` signature change (now requires `Equipment` arg).
5. Deleted `Main/_Module/bin/Win64_Shipping_Client/BehaviorTreeWrapper.dll` and `BehaviorTrees.dll`; dropped both `<Reference>` entries from `Main/TAOM.csproj`. Dropped C# 12 primary-constructor syntax from the decompile down to C# 10 plain constructors. Dropped unused demo namespaces (`BehaviorTreeWrapper.Tests`, `FPSCounter`).
6. Added regression test `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs` asserting `typeof(MissionLogic).IsAssignableFrom(typeof(BehaviorTreeMissionLogic))`. Codex review notes this catches a *different* class of bug than originally claimed, but remains a useful invariant for the defensive inheritance change.

**Verified:** `dotnet build` clean, `dotnet test` 2416 passing (one more than before — the new regression test), 1 pre-existing failure unrelated (`GetVolunteerTroopId_EreborCulture_HighRoll` — Rhun recruitment in flight on this branch), 2 skipped.

**Save-compat:** none — vendored DLLs swapped for inlined source with identical type names/namespaces; runtime behavior unchanged except for the bug fix.

**Inherited perf cleanup (deep-review E1–E7, fixed in same session):** the vendored DLL had 7 allocation/cleanup issues that the rebuild surfaced. All seven fixed in the same commit since we now own the source:
- **E1 (HIGH):** `BehaviorTreeMissionLogic.OnMissionTick` allocated `new object[] { dt }` every frame (60 Hz). Now reuses an instance-cached `_dtArgs` array.
- **E2:** 15+ `new object[]` allocations across 14 OnAgentXxx event handlers. Now uses a shared `EmptyArgs = Array.Empty<object>()` for empty notifications.
- **E3:** `FindCalledListeners` allocated `new List<>` per call. Now reuses an instance-cached `_tempMatched` list (documented synchronous-dispatch contract).
- **E4:** 18+ `list.ForEach(l => ...)` closures across event handlers. Rewritten as plain `for`/`foreach` — no delegate allocation.
- **E5:** `OnEndMissionInternal` didn't clear `actions`/`tickListeners`/`trees` dicts. Cross-mission leak fixed.
- **E6:** `Extensions.GetBehaviorTree` did `ContainsKey` + indexer (double dict lookup). Now uses `TryGetValue`.
- **E7:** `BehaviorTreeAgentComponent` allocated a `new Random()` per agent. Now uses a `static SharedRandom`.

**Memory + CLAUDE.md updates:** extended `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance.md` to require decompiling every vendored MissionBehavior subclass before declaring a `BehaviorType` audit complete; updated CLAUDE.md "Vendored Main-module DLLs" section to remove both library entries and note the inlined source paths.

**Action-set red herring:** the `as_human_warrior does not contain act_map_rider_horse_attack_1h` flood in `rgl_log` that triggered an initial action-set-rebuild hypothesis was a cosmetic vanilla engine warning that fires on stock v1.4.5 too. Not on the crash path. Plan workflow's Phase 1 verification gate caught the misdirection before any of the proposed 175k-line per-race action_set generation was written. RCA: [docs/reviews/rca-looter-battle-nre-2026-05-24.md](docs/reviews/rca-looter-battle-nre-2026-05-24.md).

---

### Feature: XSLT-injected text now translatable — kingdom/culture/clan/lord/hero descriptions

In-game testing of BR translation surfaced a class of strings that weren't reaching the translation pipeline: the kingdom descriptions ("Gondor stands as a proud bastion..."), hero biographies ("Húrioneth serves the House of..."), and similar narrative text. These live inside TAOM's XSLT files (`heroes.xslt`, `lords.xslt`, `spclans.xslt`, `spkingdoms.xslt`, `spcultures.xslt`) which inject content into vanilla XML at load time.

The text already had `{=KEY}default` loc markup in the XSLTs, but no source loc XML file collected those keys, so the engine had no fallback registry and translators had no discoverable list of what to translate.

**Fix:**
1. Extracted all 1,431 unique loc keys from 5 XSLT files into a new source XML `Main/_Module/ModuleData/taom_xslt_strings.xml`
2. Registered as a new GameText path in `SubModule.xml`
3. Added 7th `<LanguageFile>` entry across all 12 language directories
4. Created empty stubs for all 12 languages
5. Updated `tools/translate_with_claude.py` and `tools/rebuild_translation_files.py` to include the new source
6. Updated `LanguageDataXmlTests.HaveExactlySevenLanguageFiles` (was Six)
7. Ran translator across 11 languages (~$22) and rebuilt all XML files from cache

**Coverage achieved on the new XSLT strings:**
- CNs, FR, JP, KO: 100% (1431/1431)
- CNt, RU: 99.9% (~1429/1431)
- DE, SP, TR: 97% (~1390/1431)
- BR, IT: 94% (1351/1431)
- PL: 0% (preserved — community translator hasn't covered these yet)

Verified BR: `TAOM_gondor_desc` now reads "Gondor ergue-se como um orgulhoso bastião de força e resiliência na Terra-média..." and `TAOM_hero_1_8` reads "Húrioneth serve a Casa de Húrinionath, um guardião firme da tradição dos regentes de Gondor."

This makes the description text the player sees in the Encyclopedia for kingdoms, cultures, lords, and heroes properly translate to the active language.

**Files touched (TAOM repo, git-tracked):**
- `Main/_Module/ModuleData/taom_xslt_strings.xml` (NEW — 1431 entries)
- `Main/_Module/SubModule.xml` (added GameText registration)
- `Main/_Module/ModuleData/Languages/<LANG>/language_data.xml` × 12 (added 7th LanguageFile entry)
- `Main/_Module/ModuleData/Languages/<LANG>/std_taom_xslt_strings_*.xml` × 12 (new translation files)
- `tools/translate_with_claude.py`, `tools/rebuild_translation_files.py` (added new source)
- `tools/translation_cache/*.json` × 11 (~13K new cached translations)
- `TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs` (count test 6 → 7)

**Known limitation:** XSLT entries with conditional markup hit the same gender-agreement validator rejections as before (~40-80 fallback per language). These specific keys keep English. Translators can fill via overrides.

### data(races): assign race="elf" to all elven NPCCharacters (#216)

Backfilled the `race="elf"` attribute onto every Rivendell / Mirkwood / Lothlórien NPCCharacter in `Main/_Module/ModuleData/` — 266 entries across 7 XML files. Previously only `lord_R3_1` (Chägermeister, added in [`4f61f53`](4f61f53)) carried the attribute, so the other 42 elven lords, all elven town notables (167), all elven recruitable troops (45), and 12 child education templates were rendering with human heads/bodies in elven settlements and battles.

Per-file additions: [`lords.xml`](Main/_Module/ModuleData/characters/lords.xml) +42, [`npcs_rivendell.xml`](Main/_Module/ModuleData/characters/npcs_rivendell.xml) +72, [`npcs_mirkwood.xml`](Main/_Module/ModuleData/characters/npcs_mirkwood.xml) +68, [`npcs_lothlorien.xml`](Main/_Module/ModuleData/characters/npcs_lothlorien.xml) +27, [`troops_rivendell.xml`](Main/_Module/ModuleData/troops/troops_rivendell.xml) +28, [`troops_mirkwood.xml`](Main/_Module/ModuleData/troops/troops_mirkwood.xml) +17, [`taom_education_character_templates.xml`](Main/_Module/ModuleData/taom_education_character_templates.xml) +12. Also normalized the Chägermeister entry's spurious-space `race ="elf"` to the canonical `race="elf"` (matching the established `race="dg_uruk"` style used by 40+ Dol Guldur lords). `taom_wanderers.xml`'s 30 elven entries already had `race="elf"` (no change). `characters/clans.xml` was excluded — its elven entries are `<Faction>` (clan defs), not `<NPCCharacter>`, and Factions don't take a `race` attribute. No `troops_lothlorien.xml` exists; Lothlórien fields no in-game troops.

Implemented as a new idempotent tool — [`tools/add_race_attribute.py`](tools/add_race_attribute.py) (`--dry-run` / `--apply`) — that walks each in-scope XML, handles both single-line and multi-line `<NPCCharacter>` opening-tag layouts, inserts `race="elf"` immediately after the `id="..."` attribute, and re-parses every modified file with `xml.etree.ElementTree` as a well-formedness self-check. Re-running `--apply` is a no-op.

Not-tested: in-game render. The user will spot-check Imladris recruits and a Rivendell lord post-apply (pointed ears + elven head mesh).

### ui(career): reposition button to upper-right header

In-game preview of yesterday's CAREER banner revealed it landed in the center skill-panel band — the 295×125 image at `MarginTop=150 HorizontalAlignment=Center` overlapped the "handed weapon speed/damage" stats text and the top edge of the perks panel. Repositioned to the upper-right of the CharacterDeveloper header so it reads as a secondary nav element next to the name plate instead of dominating the skill area.

Updated [`CareerButtonPrefab.cs`](Main/Features/CareerSystem/UI/CareerButtonPrefab.cs): `SuggestedWidth=220 SuggestedHeight=93 HorizontalAlignment=Right MarginTop=30 MarginRight=100`. Aspect 220/93 ≈ 2.37 preserves the source art's 2.36:1 ratio (no horizontal squash). Still anchored in `TopPanelParent` (no XPath / insert-type change — same safe injection point per `.claude/rules/gui-ui.md`).

Not-tested: live in-game coords. The `MarginTop=30` / `MarginRight=100` values are estimates based on the vanilla layout map; minor follow-up tweaks expected after the user verifies in-game.

## 2026-05-23

### ui(career): replace career-button placeholder with themed LOTR banner art

Overwrote [`Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/career_button_placeholder.png`](Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/career_button_placeholder.png) with a 1024×434 (2.36:1) photoreal banner — gilt "CAREER" lettering over weathered parchment with Tengwar-style border script, true alpha. Downsampled from a 6336×2688 source via high-quality bicubic; preserved alpha channel.

Updated [`CareerButtonPrefab.cs`](Main/Features/CareerSystem/UI/CareerButtonPrefab.cs) to render the button at 295×125 (was 233×75) so the new art's natural aspect isn't squashed, and removed the overlay `<TextWidget Text="Career">` block now that "CAREER" is baked into the art. Sprite ID (`CareerSystem\career_button_placeholder`) and all C#/XML references unchanged; engine regenerates the atlas entry automatically on next game launch.

Not-tested: in-game appearance — verified PNG/format/build only. Confirm with a campaign launch on any career-owning culture and `C` (CharacterDeveloper) → banner sits above the existing tabs when `@HasCareer` is true.

### Feature: AI first-draft translation pipeline + 11-language coverage

Built `tools/translate_with_claude.py` and `tools/rebuild_translation_files.py` — Python tooling that produces first-draft translations via the Claude API (Sonnet 4.5) for TAOM's loc XML files. 4-tier fallback chain: hand-curated overrides → cache → LLM → English fallback. Hardened with incremental cache persistence (resumable on interruption), UTF-8 stdout for non-ASCII error messages, JSON-decode error tolerance, and a placeholder-preservation validator that drops translations breaking `{VARIABLE}` or `{?GENDER}{?}{\?}` markup.

Seeded `tools/translation_overrides/ru.json` with 49 canonical Russian Tolkien names (Kistyakovsky/Muravyov convention) — Беорнинги, Рохиррим, "Войти в Эпоху Людей", etc.

Ran all 11 languages in parallel (RU, SP, DE, FR, IT, BR, JP, KO, TR, CNs, CNt) in two phases. First pass spent ~$140 and exhausted API credit mid-run; after a top-up, a second gap-fill pass spent another ~$41 to fill missing TAOM_Map and Armory entries. Cache made re-running already-translated entries free.

**Final coverage after both passes (translated / total entries per module):**

| Lang | TAOM | TAOM_Map | LOTRLOME_Armory |
|------|------|----------|-----------------|
| RU   | 4756/4789 (99%) | 1101/1102 (99%) | 2582/2782 (92%) |
| SP   | 4677/4789 (97%) |  722/1102 (65%) | 2421/2782 (87%) |
| DE   | 4678/4789 (97%) |  724/1102 (65%) | 2292/2782 (82%) |
| FR   | 4673/4789 (97%) |  594/1102 (53%) | 2577/2782 (92%) |
| IT   | 4678/4789 (97%) |  722/1102 (65%) | 2464/2782 (88%) |
| BR   | 4634/4789 (96%) |  723/1102 (65%) | 2418/2782 (86%) |
| JP   | 4782/4789 (99%) | 1074/1102 (97%) | 2582/2782 (92%) |
| KO   | 4745/4789 (99%) | 1054/1102 (95%) | 2542/2782 (91%) |
| TR   | 4546/4789 (94%) |  712/1102 (64%) | 2342/2782 (84%) |
| CNs  | 4787/4789 (99%) | 1087/1102 (98%) | 2502/2782 (89%) |
| CNt  | 4777/4789 (99%) | 1102/1102 (100%)| 2542/2782 (91%) |

Untranslated entries (placeholder-validation failures or credit-exhausted batches) fall back to English text rather than corrupting the file. Translators receive AI first drafts and refine, instead of starting from blank stubs.

**Files in TAOM repo (git-tracked):**
- `tools/translate_with_claude.py` — main translator (NEW)
- `tools/rebuild_translation_files.py` — rebuild language XMLs from cache (NEW)
- `tools/translation_overrides/ru.json` — canonical Russian Tolkien names (NEW)
- `tools/translation_cache/*.json` — 11 language caches with all successful translations (NEW)
- `Main/_Module/ModuleData/Languages/{BR,CNs,CNt,DE,FR,IT,JP,KO,RU,SP,TR}/std_taom_*.xml` — populated translations (existing PL untouched — human-translated)
- `docs/localization/TRANSLATOR_GUIDE.md` — added AI workflow section
- `tools/README.md` — added Localization Pipeline section

**Files in game install (not git-tracked, mirror locations):**
- `TAOM_Map/ModuleData/Languages/<LANG>/loc_settlements.xml`
- `LOTRLOME_Armory/ModuleData/Languages/<LANG>/loc_*.xml`

**Cost:** ~$181 total Anthropic API spend (Sonnet 4.5) across both passes — $140 first pass + $41 gap-fill. Cache makes future incremental updates near-free.

**Known limitations:**
- Latin-script European languages (SP, DE, FR, IT, BR, TR) show 53-65% coverage on TAOM_Map settlement names where the placeholder-preservation validator rejected translations that added gender-agreement conditionals. The Tolkien proper-noun rich content needs case/gender variation that the strict validator drops. Translators can fill these gaps via overrides.
- 5-200 entries per module per language remain English (~1-15%) where translations couldn't preserve placeholder structure cleanly.
- Re-translation of failed entries requires either a relaxed validator (risks asymmetric translations) or per-language human curation.

Pilot validation (RU, 50 entries hand-audited): 100% variable preservation, 100% gender conditional preservation, canonical Tolkien names from overrides correctly applied, natural-sounding Russian narrative tone.

### docs(equipment): codify equipmentType="Civilian" schema rule + Ithilien Ranger feature doc

Captures the 2026-05-23 schema discovery so the bug doesn't recur:

- **`.claude/rules/xml-data.md`** — new "EquipmentRosters Schema (MANDATORY for `equipmentsets/*.xml`)" section. Documents the battle (implicit) vs civilian (`equipmentType="Civilian"` required) split, with a one-liner validator grep authors can run pre-commit. The rule loads on every `ModuleData/**/*.xml` open, so future edits to any equipment_sets file will see it. Explicitly disambiguates the standalone-roster pattern from the inline `<NPCCharacter><Equipments>...</Equipments>` pattern (which uses a different attribute, `civilian="true"` on `<EquipmentRoster>`).
- **`docs/features/gondor-ithilien-ranger.md`** — feature doc for the T9 troop + Faramir equipment work shipped earlier in the session. Includes the full 8-roster breakdown (one per Ithilien jerkin variant + matching hood/cloak/boots/bow/arrow rotation), settlement allocation (Minas Tirith + Amonost + Erethir at weight 3 vs basic peasant at weight 7), `TaomVolunteerModel.MaxVolunteerTier=6` non-interaction proof (only gates upgrade progression, not initial slot assignment — verified via ilspycmd on `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement`), and the "How to add a similar region-specialty troop" runbook.
- **Memory:** new `feedback_equipmenttype_civilian_required.md` + entry in `MEMORY.md` index; `equipment-armory-system.md` got a brief schema section cross-referencing the new feedback memory. (Memory lives user-local at `~/.claude/projects/.../memory/`, not in git.)

### fix(equipment): mark all 96 civilian rosters with `equipmentType="Civilian"` (16 culture files)

Every `<EquipmentSet>` inside a `*_civ_*` / `*_civ_equipment` `<EquipmentRoster>` across `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_*.xml` was missing the `equipmentType="Civilian"` attribute that vanilla Bannerlord's standalone-roster schema requires (verified against `SandBoxCore/ModuleData/sandboxcore_equipment_sets.xml`). Without it, the engine treats those rosters as battle equipment regardless of roster-ID naming convention — silently breaking civilian-context rendering (encyclopedia preview, settlement walks, dialog scenes).

Likely the actual root cause of Faramir's "peasant in encyclopedia" symptom we just patched cosmetically — the civilian roster wasn't being recognized as civilian at all, so the engine defaulted to battle/random selection and the recently-improved civilian outfit may not have applied in some contexts. Same latent bug exists for every other named lord (Boromir, Imrahil, Forlong, Hirluin, Angbor, Golasgil, Tirnelion, Dain, Theoden, Eomer, Eowyn, Sauron, Witchking, Nazgul, Khamul, Thranduil, Legolas, Glorfindel, Galadriel) and every `*_civ_template_*` culture default.

Per-culture roster counts:

| File | Civ rosters tagged |
|---|---:|
| gondor | 13 |
| mordor | 10 |
| rohan | 8 |
| mirkwood | 7 |
| erebor | 6 |
| lothlorien | 6 |
| rivendell | 6 |
| dale, dolguldur, dunland, gundabad, harad, isengard, rhun, umbar | 5 each |
| **TOTAL** | **96** |

Battle rosters intentionally untouched (vanilla has no `equipmentType="Battle"` — battle is implicit default; verified zero matches in SandBoxCore). Regression scan confirms 0 false positives — no battle roster was tagged.

Applied via regex sweep anchored on `id="..._civ..."` pattern. XML well-formedness verified on all 16 files via `[xml]` parser. Pure attribute add — fully save-compatible, no schema change, no roster IDs touched.

### feat(lords): add Patreon supporter Chägermeister + Elen-Nolmarë clan (Rivendell tier-2)

First Patreon-supporter clan in TAOM. Added `clan_rivendell_3` (Elen-Nolmarë, tier 2, owned by `lord_R3_1` Chägermeister) under Imladris, plus the lord himself (elf, Cavalry archetype, custom skills/traits per supporter spec) and a `<Hero text=…/>` biography rendered in the encyclopedia. Establishes the lightweight "Patreon supporter" convention as XML comments on both `clans.xml` and `lords.xml` entries — no new file, no new feature module. Banner reuses Imladris's flag as a placeholder; equipment uses the existing `rivendell_bat_template_medium_c` / `rivendell_civ_template_default_c` template pair.

Files: `Main/_Module/ModuleData/characters/clans.xml`, `characters/heroes.xml`, `characters/lords.xml`.

### fix(lords): re-equip Faramir as Ithilien Ranger Captain (was rendering as a peasant)

Faramir's battle and civilian rosters in [taom_equipment_sets_gondor.xml](Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_gondor.xml) previously used `ithilien_jerkin_long` (30 body armor, brown leather) + `sk_gd_ano_pauld_noble_med_a` + `sk_gd_ano_grvs_noble_med_a` + `sk_gd_ith_noble_helmet_heavy_a` — visually indistinguishable from a peasant in portrait view next to Boromir's heavy Osgiliath plate. Swapped to Faramir's dedicated character-specific kit (all items already in LOTRLOME_Armory):

| Slot | Was | Now |
|---|---|---|
| Head | `sk_gd_ith_noble_helmet_heavy_a` | `ithilien_hood` |
| Body | `ithilien_jerkin_long` | `faramir_armor` (Faramir's Armour) |
| Cape | `sk_gd_ano_pauld_noble_med_a` | `ithilien_cloak_var` (Ithilien Cloak Two) |
| Leg | `sk_gd_ano_grvs_noble_med_a` | `ithilien_boots_heavy` (Ithilien Heavy Leather Boots) |
| Gloves | `faramir_bracers` | (unchanged) |

Weapons + horse unchanged on battle roster: Faramir's Sword + Ithilien Bow III + Noldar Elven Arrow X + charger. Civilian mirrors battle minus weapons + horse (matches Boromir's roster pattern).

Save-compat: per-slot replacement, roster IDs `faramir_bat_equipment` and `faramir_civ_equipment` preserved. `lords.xslt` (lines 1602–1638) references roster IDs only, no edit needed. Existing saves keep currently-spawned equipment; new loadout applies on next equipment refresh.

Item-ID verification: all 5 new IDs grepped against `LOTRLOME_Armory\ModuleData\LOTRLOME_items\gondor\*.xml` before writing. XML well-formedness confirmed via `[xml]Get-Content` parse (26 rosters preserved, no schema change).

Build verification: full `./build.ps1` blocked by Bannerlord running (BehaviorTrees.dll locked, environment issue per `.claude/rules/environment-failures.md`); XML edit is pure ID substitution against verified-existing IDs.

### feat(lords): add Patreon supporter Chägermeister + Elen-Nolmarë clan (Rivendell tier-2)

First Patreon-supporter clan in TAOM. Added `clan_rivendell_3` (Elen-Nolmarë, tier 2, owned by `lord_R3_1` Chägermeister) under Imladris, plus the lord himself (elf, Cavalry archetype, custom skills/traits per supporter spec) and a `<Hero text=…/>` biography rendered in the encyclopedia. Establishes the lightweight "Patreon supporter" convention as XML comments on both `clans.xml` and `lords.xml` entries — no new file, no new feature module. Banner reuses Imladris's flag as a placeholder; equipment uses the existing `rivendell_bat_template_medium_c` / `rivendell_civ_template_default_c` template pair.

Files: `Main/_Module/ModuleData/characters/clans.xml`, `characters/heroes.xml`, `characters/lords.xml`.

### feat(troops): Rhun recruitment + Easterling → Loke-Rim + conditional-pool API (#215, commit bce0824)

Per-settlement Rhûn volunteer pools (the last major TAOM culture without recruitment overrides — every Rhun notable previously fell through to vanilla `DefaultVolunteerModel.GetBasicVolunteer()` and produced an `easterling_recruit`). Easterling line retired and replaced by Loke-Rim throughout. Wainrider elite cavalry get proper tier-4 Rhun barding. Gondor recruitment moved to JSON config with a new conditional-pool API for ownership-gated pools. Ithil Guard line re-equipped.

**`VolunteerRecruitmentService` additions:**
- `InitializeRhunSettlements` + `InitializeRhunCulture`: 6 themed pools (Dragon-Wrath / Balcoth / Far-Rhun / Wain / mixed / Kharaghul) across 20 Rhûn settlements + `khuzait` culture fallback. Castle entries cover bound villages via `VolunteerContextAdapter` `BoundSettlementId` resolution.
- `AddSettlementConditional` + `ConditionalSettlementMap` + `ResolveConditionalPool`: new API for state-sensitive pools. Predicate evaluated at lookup time. Conditional resolves BEFORE regular settlement; falls through cleanly on predicate=false.
- `EnsureGondorJsonLoaded`: idempotent (`Interlocked.CompareExchange`) instance-side JSON loader. Hand-written Gondor pools kept as safety net; JSON overwrites matching keys at runtime. Tests where the JSON file is absent fall back to hand-written behaviour automatically.

**New: `GondorRecruitmentJsonLoader`** — parses `Main/_Module/ModuleData/recruitment_pools/gondor.json` (23 chance groups). Percentages → integer weights via *10000 (preserves 33.3334% → 333334 precision). Rejects NaN/Infinity/negative/blank entries with warning. Fail-closed on unrecognised condition strings; only the Ithil Guard rule ("town_ES2 + Gondor-owned" substring match) currently recognised.

**`VolunteerContext` + `VolunteerContextAdapter`:** + `OwnerCultureId` field populated from `Settlement.OwnerClan?.Culture?.StringId` (read live so kingdom flips take effect for the next pick). Existing 4-arg ctor still works (defaults `OwnerCultureId=null`, predicate evaluates false, conditional pool can't fire — safe).

**Easterling line orphan** (13 troops in `troops_rhun_new.xml`): `easterling_recruit` flipped to `is_basic_troop="false"`; NPCCharacter blocks preserved per `.claude/rules/troops.md` "Never delete troops". All references stripped:
- `spcultures.xslt` khuzait `basic_troop` → `loke_rim_initiate`, `elite_basic_troop` → `loke_rim_cavalry`
- `npcs_rhun.xml` `villager_rhun` upgrade_target → `loke_rim_initiate`
- `taom_partyTemplates.xml` — 8 Rhun party templates rewired via role-preserving Easterling→Loke-Rim map (recruit→initiate, militia→footman, bowman→bowman, footman_new→infantry, skirmisher_new→archer, swordsman_new→shieldguard, halberdier_new→maceman, cavalry_new→cavalry, archer_new→marksman, veteran_*→gilded_*)

**Wainrider equipment:** Khan's Chosen / Swift-Chariot / Warlord Chariot HorseHarness → `lrd_horse_armour_4` (tier-4 Kataphrakt for level 41-46 cavalry). `khuzait_charger` retained.

**Ithil Guard line equipment** (`troops_gondor.xml`):
- 4 melee troops (watcher/veteran/sergeant/captain): each gains 2 new EquipmentRoster blocks (`wm_gondor_lamedon_2h_sword_e` 2H sword + `wm_gondor_swanknight_speara` Belfalas Banner Spear 2H polearm) alongside existing 1H+shield roster — engine randomises per spawn for visual + tactical variety.
- 3 archer troops (longbowman/sharpshooter/moon_guard) standardised on `gondor_steel_bow` + `piercing_arrows` ×2 (vanilla damage=4 vs prior `bodkin_arrows_a` damage=3).

**38 new tests** (Rhun pools + boundary rolls + JSON loader hardening + conditional pool both states + parser edge cases incl. NaN/negative/malformed/missing-file + integration test against real JSON). Full suite **2418/2420** (2 unrelated skips).

**Save-compat:** Easterling NPCCharacter blocks preserved; existing parties keep their Easterling troops, new spawns are Loke-Rim only.
**Constraint:** `IModLogger` has no `IsDebugEnabled` — hot-path LogDebug interpolation in `GetVolunteerTroopId` is pre-existing (not introduced this PR), deferred per RCA.
**Research:** `Settlement.OwnerClan?.Culture?.StringId` verified against installed v1.4.5 `TaleWorlds.CampaignSystem.Settlement`; `BasePath.Name` returns `"../../"` on desktop.

**Deep-review caught 1 HIGH (data flow agent):** `wain_cavalry` referenced in `InitializeRhunSettlements` Wain pool didn't exist (typo from spec "Wain Cavalry"); actual ID is `wainrider_cavalry`. Fixed + test updated. Sibling-naming-symmetry is a false-positive signal — codified in `feedback_verify_troop_ids_against_canonical_xml.md`.

Feature doc: [`docs/features/volunteer-recruitment.md`](docs/features/volunteer-recruitment.md). RCA: [`docs/reviews/rca-rhun-gondor-recruitment-2026-05-23.md`](docs/reviews/rca-rhun-gondor-recruitment-2026-05-23.md).

**Pre-existing horse-armor audit gap (flagged, NOT fixed this PR):** 8 Rhun cavalry troops at level 21-26 still use `lrd_horse_armour_4` despite the new "tier-4 → level 31+ only" rule (`balcoth_horse_archer`, `far_rhun_cavalry`, `far_rhun_horse_master`, `kharaghul_raider`, `kharaghul_horse_scout`, `kharaghul_horse_archer`, `kharaghul_horse_master`, `darkhun_horseman`, `darkhun_cavalry`). Will be addressed in a follow-up.

### fix(armory): correct `covers_hands` on 4 LOTRLOME bracers

User-reported render mismatches across four bracers in `LOTRLOME_Armory/ModuleData/LOTRLOME_items/`. Each was the opposite of the intended visual. Per `feedback_lotrlome_armor_cover_attributes.md`, the engine equips an item but skips the mesh over the hand when `covers_hands="false"` (and vice versa) — so the wrong value silently produces either bare-skin-where-armor-should-be or armor-cuff-with-no-glove.

| Item id | Display | File | Was | Now |
|---|---|---|---|---|
| `sk_ar_art_bracer_noble_med_a` | [Arnor] Noble Bracers | `arnor/arm_armors.xml` | `false` | **`true`** |
| `sk_dg_uruk_bracer_elite_j` | [Dol Guldur] Uruk Archer Elite Bracer J | `dol_guldur/arm_armors.xml` | `true` | **`false`** |
| `sk_uruk_hai_bracer_elite_a1` | [Isengard] Elite Plate Bracer I | `isengard/arm_armors.xml` | `true` | **`false`** |
| `sk_md_orc_bracer_med_a` | [Mordor] Mordor Orc Bracer I | `mordor/arm_armors.xml` | `true` | **`false`** |

Item ids, `arm_armor` values, `modifier_group`, and `material_type` unchanged on all four. Single-attribute flip per item.

Not-tested: in-game render — automated tests don't cover armor cover attributes; user verifies visually.
Save-compat: none — attribute flip only.

### chore(build): vendor warg + native DLLs in Main/_Module/bin, drop redundant MCMv5 ref

Adds `BehaviorTrees.dll`, `BehaviorTreeWrapper.dll`, `MinHook.x64.dll`, and `TAOM.NativeSkinFixes.dll` to the repo via `.gitignore` allowlist (same pattern as `Dependencies/_Module/bin/`). Fresh clones and CI can now build — previously these vendored DLLs were caught by the top-level `bin/` ignore and had to be sideloaded by hand on every machine. The `Bannerlord.BuildResources` `PostBuildCopyToModules` target already mirrors the folder into the Steam install on every build, so commits to these DLLs (e.g., when `TAOM.NativeSkinFixes.dll` is recompiled externally) now propagate to teammates automatically.

Removes the vestigial `<Reference Include="MCMv5">` HintPath block from `Main/TAOM.csproj`. No C# code uses the `MCMv5.` namespace — `using MCM.*` calls in `Main/Features/TaomSettings.cs` are served at compile time by the `Bannerlord.MCM` NuGet (`IncludeAssets="compile"`) and at runtime by `TAOM.Dependencies` (`Bannerlord.MBOptionScreen*.dll` + `MCM.UI.Adapter.MCMv5.dll`). `MCMv5.dll` correspondingly removed from `Main/_Module/bin/Win64_Shipping_Client/` (repo + install + editor mirror). Build verified clean (0 errors).

CLAUDE.md "Key Paths" row updated: "BT DLLs" → "Vendored Main-module DLLs" covering all 4 vendored DLLs + the rebuild workflow + the MCMv5-is-elsewhere guidance.

Not-tested: install-side smoke (MCM settings open + warg battle) — recommend before merge.
Save-compat: none — no field/save changes.

### feat(troops): KEYforce troop tree revamp — Mordor/Isengard/Dol Guldur/Gundabad/Erebor (#212)

Follow-up to #211 (Armory item authoring). KEYforce's per-culture spec files at `E:\repos\lotraom-assets\tools\<culture>_armors_and_troops.txt` define unit progression trees with per-tier armor + weapon loadouts. #211 authored items but deferred troop tree work; this issue closes that gap for 5 cultures (Rhun handled in a separate session).

**Per-culture summary:**

| Culture | New troops | Deleted | Refits | Validator |
|---------|-----------|---------|--------|-----------|
| **Gundabad** | 1 (`gundabad_bolgs_ironfang` T8) | 4 (`champion`, `pike_warrior`, `veteran_pike_warrior`, `warg_warrior`) | 16 (renames + equipment per spec) | PASS — 27 troops, 93 armor refs, 0 missing |
| **Mordor** | 21 (10 orc + 6 Nurn Warg + 5 Black Uruk variants) | 14 (10 old uruk extras + 3 orc stubs + `mordor_black_numenorean` per user) | 9 (kept Black Uruk troops refitted to spec) | PASS — 35 troops, 123 armor refs, 0 missing |
| **Isengard** | 13 orc-race troops (Section 1 entirely missing — `isengard_orc_*` line) | 0 (per user: keep `orthanc_*` lore-canonical line) | 30 (Uruk-Hai Legion + Scouts) | PASS — 51 troops, 126 armor refs, 0 missing |
| **Dol Guldur** | 0 (Khamul human line already complete in file) | 12 (6 old Khamul stubs + 6 berserker line) | 17 (Uruk line + Fell Warg Riders) | PASS — 50 troops, 157 armor refs, 0 missing |
| **Erebor** | 13 (Iron Hills Noble line: archer/infantry/shock branches) | 0 | 41 (Erebor Regular/Noble/Oathsworn + Iron Hills + Ironpass) | PASS — 58 troops, 218 armor refs, 0 missing |

**Total: 48 new troops, 30 deletions, 113 equipment refits.** Build clean (0 errors). All 7 culture troop trees pass cross-reference validation.

**Race attributes per CLAUDE.md `troops.md` table + user direction (2026-05-23):**
- Mordor orc line + Nurn Warg Riders: `race="orc"`
- Mordor Black Uruks: `race="uruk"`
- Isengard new orc line: `race="orc"`
- Isengard Uruk-Hai + Scouts: existing `race="uruk_hai"` / `"berserker"` (preserved for save compat)
- Dol Guldur Uruk + Warg: `race="dg_uruk"`
- Dol Guldur Khamul humans: no race attribute (vanilla human)
- Gundabad Pale Uruk: `race="pale_uruk"`
- Erebor + Iron Hills: `race="dwarf"`

**Per-culture apply scripts (all idempotent, `--dry-run` / `--apply` flags):**
- `tools/apply_gundabad_troop_revamp.py`
- `tools/apply_mordor_troop_revamp.py`
- `tools/apply_isengard_troop_revamp.py`
- `tools/apply_dolguldur_troop_revamp.py`
- `tools/apply_erebor_troop_revamp.py`

**Downstream cleanup (per CLAUDE.md `troops.md` checklist):**
- `tools/cleanup_deleted_troops_212.py` — removed 26 refs to deleted troops across `taom_partyTemplates.xml` (13), `troop_weights.xml` (5), `troop_resource_costs.xml` (8)
- `tools/expand_party_templates_212.py` — added 47 new `PartyTemplateStack` entries to `kingdom_hero_party_<culture>_template` blocks (Mordor 21, Isengard 13, Erebor 13)
- `VolunteerRecruitmentService.cs` — added `InitializeGundabadCulture()` fallback pool (Gundabad had no recruitment entries before); appended `iron_hills_noble` (T2 entry of new Erebor noble line) to `InitializeEreborCulture` so players can recruit it in villages
- `troop_weights.xml` — added 13 elite-tier weights: 9 `iron_hills_noble_*` (incl. `royal_warden` at 3.0), 4 new Mordor uruk/warg elites (T5–T6 at 2.0)
- `troop_resource_costs.xml` — added 4 new Mordor uruk/warg ranged + cavalry entries (`uruk_crossbow`, `uruk_heavy_crossbow`, `uruk_heavy_archer`, `warg_beastmaster`) gated by `war_spoils`

**Decisions:**
- "Delete if not in `.txt` file" applied strictly to TAOM-fabricated extras (Mordor uruk_feller/ravager/executioner/etc — 10 troops; old DG initiate/disciple stubs — 6 troops; berserker line not in spec — 6 troops; Gundabad duplicates of repurposed roles — 4 troops). Total 30 deletions.
- **Exceptions (per user direction)**: Isengard `orthanc_*` line kept (4 troops — Tolkien-canonical Orthanc tower elite uruks). `mordor_black_numenorean` deleted (user explicitly removed).
- DG `dg_uruk_veteran_warrior` repurposed in-place (level 21→16) rather than renamed, to preserve save compat per `troops.md` rule.

**Out of scope:**
- Rhun (user is handling in a separate session).
- Lossarnach pauldrons (Gondor #99 known limitation).
- Full localization XML entries for the 47 new troop display names (game falls back to display name in NPCCharacter XML).
- Patrol-level / vassal-reward templates for new troops (only `kingdom_hero_party_*` templates expanded this pass).
- Dol Guldur Khamul human troops still need party-template wiring — they're already in `kingdom_hero_party_dolguldur_template` per the audit.

**Known limitations (pre-existing — not regressions from #212; tracked for follow-up):**
- Mordor and Isengard have no `VolunteerRecruitmentService` pools at all (no `InitializeMordor*` / `InitializeIsengard*` methods). New troops are fielded by AI lords via party templates but are not recruitable from villages. This was the same state before #212; closing the gap would be a separate follow-up issue.
- Pre-existing Mordor uruk troops (`archer`, `shieldbearer`, `infantry` at L26) lack `war_spoils` resource cost. Same baseline as before #212.

Issue: #212.

## 2026-05-22

### feat(armory): KEYforce multi-culture armor revamp — Mordor/Isengard/Dol Guldur/Erebor/Rhun (#211)

Mesh-first authoring pass following the Gondor (#99) pipeline. KEYforce shipped updated specs for 7 cultures; this pass closed all Armory item gaps where meshes are available. Total **277 new Armory items** across 5 cultures (Gundabad was already complete at 101/101).

| Culture | New items | Notes |
|---------|-----------|-------|
| Mordor | 103 | Generic orc pool — sk_gn_orc_* helmets (9 shapes) + sk_md_orc_* paint helmets/chests/pauldrons/bracers/boots. Black Uruk pool (90 sk_uruk_mordor_*) already authored. |
| Isengard | 15 | sk_is_orc_* paint helmets (per spec — Pik/Rdr/Sct excluded, no IS variants) + clo_urukscout_* cloth overlays. Uruk-Hai Legion (137) + scout base (4) already authored. |
| Dol Guldur | 14 | sk_dg_orc_* paint helmets. Brt + Vgd excluded per spec (no DG variants). 113 sk_dg_uruk_* + 194 sk_dg_khml_* already authored. |
| Gundabad | 0 | Already complete (101/101 sk_gb_uruk_* items match spec exactly). |
| Erebor | 123 | sk_dwarf_iron_* Iron Hills — was completely unauthored. 174 sk_dwarf_erebor_* core already shipped. Auto-classified per ID pattern (helmet/chest/pauldron/bracer/boots × light/medium/heavy/elite/lord). |
| Rhun | 22 | Final Loke-Rim elite helmets (21) + 1 heavy hood. 564/586 spec items already in Armory; this closes the gap. |

**New tooling (one per culture):**
- `tools/generate_mordor_armor.py`
- `tools/generate_isengard_armor.py`
- `tools/generate_dolguldur_armor.py`
- `tools/generate_erebor_armor.py` (parses spec file directly)
- `tools/generate_rhun_armor.py`

All idempotent, default to Steam install path, reuse the `STAT_TIERS` table from `tools/generate_gondor_armor.py` for stat consistency across cultures.

**Decision per user direction**: "use the spec as a guide, but create variations within that guide to use all meshes. It is important all armor is showed off." → mesh-first authoring: every shipped mesh has a corresponding item; spec items without meshes are deferred to artist's next pass.

**Verification:**
- Build: 0 errors.
- Troop tree validation (`tools/validate_all_troop_refs.py` — new generic multi-culture validator): all 7 cultures PASS. 504 troops, 1,112 armor refs, 0 missing.

| Culture | Troops | sk_*/clo_*/urukscout_* refs | Missing |
|---------|--------|----------------------------|---------|
| gondor | 179 | 155 | 0 |
| mordor | 29 | 87 | 0 |
| isengard | 39 | 137 | 0 |
| dolguldur | 63 | 190 | 0 |
| gundabad | 31 | 99 | 0 |
| erebor | 46 | 169 | 0 |
| rhun_new | 117 | 275 | 0 |

**Patches applied during validation:**
- Dol Guldur: added 2 missing items referenced by troops_dolguldur.xml — `sk_dg_uruk_bracer_elite_j` (spec-canonical Archer line per spec line 124) and `sk_dg_uruk_pauldron_med_c` (variant of med_a/b for troop-XML reference). Pre-existing broken refs, not introduced by this revamp.

**Post-review correction (`/deep-review` data-flow agent caught):**
- Erebor `sk_dwarf_iron_*` items were initially authored to `LOTRLOME_items/erebor/` but the canonical home is `LOTRLOME_items/iron_hills/` (where 125 pre-existing Iron Hills items already lived). 118 of the 123 authored items duplicated existing IDs across the two folders, which would have caused engine shadowing/warnings at runtime. Rolled back via `tools/rollback_erebor_iron_misfile.py --apply` and re-ran `generate_erebor_armor.py` against `iron_hills/`. Net: 5 new items added to `iron_hills/shoulder_armors.xml` (the 5 spec items not previously authored), 118 duplicates avoided. Generator's `DEFAULT_ARMORY_BASE` now correctly targets `iron_hills/`. RCA: `docs/reviews/rca-multi-culture-armor-revamp-2026-05-22.md`.

**Out of scope (deferred to future passes):**
- Troop equipment refits per culture (items authored, troop loadouts not re-wired in this commit).
- Dol Guldur Khamul human T4–T9 troop line (armor items exist; troop tree authoring pending).
- Lossarnach pauldrons (#99 known limitation).
- Goblin / Guldur elite / Half-orc — spec explicitly says "DO NOT IMPLEMENT — future content".

Issue: #211.

### fix(cc): author missing `as_elf_facegen` + `as_elf_female_facegen` — elves on parent menu render upright

Mirkwood / Rivendell parents on the Character Creation parent-menu screen rendered as a horizontally-stretched / contorted mesh (no proper standing pose). Same failure-mode class as the 2026-05-04 "broken custom-race CC parents" bug, but with a different root cause: not a 1.2-vs-1.3 action-type-name mismatch, but a complete absence of the action_set the engine looks for.

**Root cause.** LOTRLOME_Armory's `monsters.xml` defines `elf` with `monster_usage="human"` and `action_set="as_human_warrior"` (i.e. elves use the human skeleton, just retextured), but LOTRLOME never declared `as_elf_facegen` or `as_elf_female_facegen`. The 2026-05-04 fix added Bannerlord 1.3 action-type aliases to LOTRLOME's 12 pre-existing facegen sets (dwarf, uruk, orc, nazghul, hill_troll, cave_troll, pale_uruk, dg_uruk, goblin, saruman, berserker, uruk_hai) — but did not author the missing pair for elves, even though both the commit message and the CHANGELOG entry mentioned "elf". Elves silently fell through to a default that didn't bind correctly to the human skeleton.

`Patch20_NarrativeHorseGuard`'s race-sync prefix on `CharacterCreationNarrativeStageView.RefreshAgentVisuals` (already in place from 2026-05-04) correctly tells the engine to look up `as_elf_facegen` — but until today there was no such action_set, so the lookup failed.

**Fix.** Appended two slim action_set entries to both the live `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\action_sets.xml` and the tracked snapshot at [`docs/reference/lotrlome-armory-snapshot/action_sets.xml`](docs/reference/lotrlome-armory-snapshot/action_sets.xml):

- `as_elf_facegen` with `base_set="as_human_warrior"` — declares all 14 male CC parent action types (7 × Bannerlord 1.2 names `act_character_creation_male_default_0..6` + 7 × Bannerlord 1.3 names `_default_standing` / `_side_to_side_1` / `_mother_front` / `_father_sitting` / `_side_to_side_2` / `_side_to_side_3` / `_hugging`), each pointing at `anim_father_0..6`. Inherits childhood / toddler / conversation / sit / stand actions from `as_human_warrior` because elves use the human skeleton.
- `as_elf_female_facegen` with `base_set="as_elf_facegen"` — same 14 action types for the female mirror, pointing at `anim_mother_0..6`.

**Audit.** Cross-checked every distinct `race="..."` value used by TAOM cultures / troops / character templates (`berserker`, `cave_troll`, `dg_uruk`, `dwarf`, `elf`, `goblin`, `human`, `orc`, `pale_uruk`, `uruk`, `uruk_hai`) against LOTRLOME's `_facegen` action_set list — `elf` was the only hole. The 10 other races already have both male + female facegens (patched on 2026-05-04 with the 1.3 aliases). `human` doesn't need a custom facegen — it resolves to the engine default.

**Regression guard.** Per user choice: snapshot + doc only — no TAOM-side startup check or build-time injector. Updated [`docs/reference/lotrlome-armory-snapshot/README.md`](docs/reference/lotrlome-armory-snapshot/README.md) with an explicit per-race "Required facegen entries" checklist + a one-line `grep` sanity check the user can run after any LOTRLOME update. The implicit "etc." in the previous README is what hid the elf hole for 18 days; the new checklist names every required entry by ID.

**Memory** `feedback_lotrlome_action_set_aliases.md` was extended to record the rule: the fix recipe for custom-race CC parents must both (a) **patch** existing facegen action_sets with 1.3 aliases AND (b) **create** missing facegen action_sets for any race a TAOM culture consumes that LOTRLOME's authors never anticipated as playable. Patching alone is insufficient.

**Files changed:**
- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\action_sets.xml` — appended 51 lines (2 × action_set with 14 actions each + header comment).
- [`docs/reference/lotrlome-armory-snapshot/action_sets.xml`](docs/reference/lotrlome-armory-snapshot/action_sets.xml) — same append, kept in lockstep with live.
- [`docs/reference/lotrlome-armory-snapshot/README.md`](docs/reference/lotrlome-armory-snapshot/README.md) — added per-race facegen checklist + grep sanity check + bumped snapshot date to 2026-05-22.

**Verification.** XML-only — no C# touched, no rebuild needed (`./build.ps1` is a no-op for this change). In-game smoke test: launch Bannerlord → New Sandbox → pick Mirkwood or Rivendell → advance through CC until the parent-menu youth options render both parents → confirm parents stand upright with proper anim cycling, no T-pose / stretched mesh. Repeat for every culture that uses `race="elf"` in its character templates.

Not-tested: every other CC parent action type beyond the 14 declared here. If a vanilla CC stage references a parent-anim type we missed, the engine will fall through to `as_human_warrior`'s definition (or further down the inheritance chain) — same path the dwarf facegen uses for non-CC-parent actions. If a future Bannerlord patch renames the CC parent action types again, repeat this fix recipe for whatever names the 1.4+ engine looks up.

**Follow-up same session (v2 fix).** The first in-game test of the slim entries above confirmed the parent menu now renders elves upright — but the **Early Childhood** stage (and all subsequent CC stages) still showed the elf child lying down / T-posed. Root cause: the Bannerlord 1.3 facegen action-lookup does **not** fall through `base_set` for post-parent CC action types. Inheriting `act_childhood_*` / `act_character_creation_toddler_*` / `act_inventory_idle*` / `act_stand_*` / `act_sit_*` / `act_rider_story_background_*` / `act_horse_story_background_*` via `base_set="as_human_warrior"` returns nothing because `as_human_warrior` is a combat set, not a facegen set. LOTRLOME's `as_dwarf_facegen` works because it declares all ~100 action types **directly** inside the facegen block, not by inheritance.

Replaced the slim 51-line elf entries with **verbatim copies of LOTRLOME's `as_dwarf_facegen` (lines 16812-17134) and `as_dwarf_female_facegen` (lines 17135-17232) blocks**, with only two edits to each: `id` and `base_set` attributes renamed. Male: `id="as_dwarf_facegen"` → `id="as_elf_facegen"`, `base_set="as_dwarf_warrior"` → `base_set="as_human_warrior"`. Female: `id="as_dwarf_female_facegen"` → `id="as_elf_female_facegen"`, `base_set="as_dwarf_facegen"` → `base_set="as_elf_facegen"`. Every animation file referenced (`anim_male_custom`, `anim_childhood_*`, `anim_father_*`, `anim_mother_*`, `anim_toddler_*`, `anim_rider_story_background_*`) is skeleton-flexible — dwarves use them on the dwarf skeleton, elves will use them on the human skeleton they share via `monster_usage="human"`.

Diff size per file grew from ~51 lines (v1) to ~420 lines (v2). Both `E:\Steam\...\LOTRLOME_Armory\ModuleData\action_sets.xml` and [`docs/reference/lotrlome-armory-snapshot/action_sets.xml`](docs/reference/lotrlome-armory-snapshot/action_sets.xml) re-edited in lockstep; both still parse as valid XML (verified via `python -c "import xml.etree.ElementTree as ET; ET.parse(...)"`).

[`docs/reference/lotrlome-armory-snapshot/README.md`](docs/reference/lotrlome-armory-snapshot/README.md) extended with explicit enumeration of all 6 action-type categories that must be declared directly in any `as_<race>_facegen` (the original "14 CC parent action types" was an incomplete list). Memory `feedback_lotrlome_action_set_aliases.md` 2026-05-22 addendum extended with the "declare everything, don't trust inheritance" sub-rule — when authoring a new race facegen, copy LOTRLOME's `as_dwarf_facegen` verbatim and rename only `id` + `base_set`.

**Verification scope updated:** the in-game smoke test now requires advancing through **every** CC stage (parent menu → Early Childhood → Youth → Adolescence → Adulthood), confirming the agent stands / sits / plays anim correctly at each one, not just the parent menu.

### fix(cc): override broken vanilla age-30 animation in Starting Age menu

Third bug surfaced in the same session: at the **Starting Age** narrative menu, clicking age 30 ("You are at your prime...") rendered the player as a horizontally-stretched / lying-down mesh. Ages 20, 40, and 50 worked correctly. Confirmed in-game on all races (dwarf / uruk / elf / human / orc) — not race-specific.

**Root cause.** Vanilla `CharacterCreationCampaignBehavior.AgeSelectionAdultOptionOnSelect` (decompiled v1.3.15) hard-codes `character.SetAnimationId("act_childhood_athlete")` at age 30; the other three age handlers use `act_childhood_focus` (20), `act_childhood_sharp` (40), `act_childhood_tough` (50). LOTRLOME's `as_<race>_facegen` action_sets declare all four action types identically (verified via bit-for-bit compare across orc/dwarf/uruk/elf — all map `act_childhood_athlete → anim_childhood_athlete`), and `act_childhood_athlete` is properly registered in `Native/ModuleData/action_types.xml`. The bug is in the `anim_childhood_athlete ↔ human_skeleton` binding at runtime — a vanilla v1.3.15 regression, not an LOTRLOME data issue.

The user's earlier hypothesis that "action set ids youth, adult, etc." controlled the per-age pose was off-target — vanilla uses the same `as_<race>_facegen` action_set across all four ages and just changes the animation_id per option. No `_youth` / `_adult` / `_elder` action_set IDs exist anywhere in LOTRLOME or Native.

**Fix.** Single Harmony Postfix appended to [`Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`](Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs) — new class `CharacterCreationCampaignBehavior_AgeSelectionAdultOptionOnSelect_Patch` in the existing `Patch20_NarrativeHorseGuard` category (which is already registered in `SubModule.cs` line 459, so no SubModule changes needed). The Postfix runs after vanilla, finds the `player_age_selection_character`, and re-sets the animation to `act_childhood_focus` (the proven-working age-20 anim). Vanilla's age value, equipment, birthday, StartingAge field, and attribute/focus bonuses are all untouched.

Scope is deliberately tight — the Postfix only intercepts the age-30 code path. The other call sites of `act_childhood_athlete` in vanilla (`CharacterCreationCampaignBehavior.cs:1599` + `:2016`, both in youth backstory option handlers) are NOT touched; the user has not reported breakage there and changing them risks regressions on stages that currently work.

**Files changed:**
- [`Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`](Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs) — added 38 lines (one `[HarmonyPatch]` + `[HarmonyPostfix]` class, with xmldoc explaining the vanilla bug it works around).
- [`docs/features/character-creation.md`](docs/features/character-creation.md) — extended the existing "LOTRLOME `as_<race>_facegen` action_set requirement" section with a sub-note about the age-30 vanilla bug + this override.
- [`docs/reviews/rca-elf-cc-facegen-2026-05-22.md`](docs/reviews/rca-elf-cc-facegen-2026-05-22.md) — appended a third addendum (vanilla-bug-not-LOTRLOME-bug discovery, same session).
- Memory `feedback_lotrlome_action_set_aliases.md` — appended v3 sub-rule: when all races break at the same CC stage despite identical action_set data, the bug is at the engine/anim layer, not the action_set XML — fix via TAOM-side Harmony override, not data edits.

**Verification.** `dotnet build Main/TAOM.csproj -t:Restore,Compile` succeeded with 0 errors / 0 warnings. Post-build deploy step blocked at write time by Bannerlord holding `0Harmony.dll` / `DryIoc.dll` locked — pure environment issue, not a code issue. In-game smoke test (on the user) once the game is closed and the new DLL deploys: pick any culture → advance CC to the Starting Age menu → click each age option → confirm the agent stands upright at every age including 30.

Not-tested: the two other call sites of `act_childhood_athlete` in vanilla (`CharacterCreationCampaignBehavior.cs:1599` and `:2016` — youth backstory options). If the user encounters the lying-down pose at those stages, those need their own Postfixes following the same recipe.

### chore(shaders): hide Pre-compile Shaders main-menu option

Feature isn't 100% reliable right now, so the `TaomPrecompileShaders` `InitialStateOption` is commented out in [`Main/SubModule.cs`](Main/SubModule.cs) via a `/* DISABLED 2026-05-22 ... */` block around the `AddInitialStateOption(...)` call. Everything else stays wired up: the `ShaderPrecompilation` service + IoC registration, `Patch21_ShaderPrecompilation` Harmony patch on `LoadingWindowViewModel.Update`, the `OnApplicationTick` in-game shader-progress reporter (`_shaderTickAccumulator` / `_lastShaderCount`), tests, docs, and localization strings — all of those are independently useful and remain active. Re-enable the menu button by removing the surrounding block-comment once the underlying reliability issue is fixed.

### tune(diplomacy): War of the Ring phase defaults to Day 2 / Day 14

Phase 1 (Isengard + Dunland attack Rohan) now defaults to **Day 2** (was 30 in MCM / 1 in JSON). Phase 2 (full hostile-tier sweep, peace blocked between hostile tiers) now defaults to **Day 14** (was 45 in MCM / 1 in JSON). Both remain user-tunable via MCM → War of the Ring → Phase 1/2 Start Day (range 1–365).

Also tightened `testMode` defaults in `war_of_the_ring.json` from 2/5 → 1/3 so test mode remains meaningfully faster than the new normal cadence.

Existing saves are unaffected: phase state is persisted via `SyncData` (`WarOfTheRing_CurrentPhase`), so a save that already advanced past Phase 2 stays in Phase 2 regardless of config changes. The new defaults apply to fresh campaigns and to any save still in Peace phase.

### fix(diplomacy): split peace + alliance invariants in EnforcePermanentAlliances; close Dale↔Isengard gap; align MCM 5.11.4

In-game encyclopedia showed Mordor simultaneously in the "Wars" list AND "Alliances" list with Harad — an impossible vanilla state. Root cause: `DiplomacyService.EnforcePermanentAlliances` short-circuited on `AreAllied=true` and never checked war state. Sequence at game start:

1. `EstablishInitialAlliances` (called from `OnNewGameCreatedPartialFollowUp`) creates the Mordor↔Harad alliance.
2. Vanilla initial-state setup declares wars for the southern factions, sweeping Harad into Mordor's war list.
3. `EnforcePermanentAlliances` (called from `OnSessionLaunched`) sees `AreAllied=true`, hits `continue`, never calls `MakePeace`.

Fix: split the two invariants. For every Permanent pair, check **independently** (a) NOT at war (call `MakePeace` if so) and (b) Allied (call `StartAlliance` if not). The fix applies on next `OnSessionLaunched`, so a save-reload clears the corrupted state without requiring a new campaign. Added 2 regression tests:
- `EnforcePermanentAlliances_AlliedButAlsoAtWar_MakesPeaceWithoutRestartingAlliance` — direct repro of the in-game bug
- `EnforcePermanentAlliances_NotAlliedAndAtWar_MakesPeaceThenStartsAlliance` — fresh-state path; verifies peace-before-alliance ordering

Also added missing diplomatic entry: `sturgia ↔ isengard Hostile` (Dale should be at war with Isengard, matching Erebor's hostile list). Without it the Phase-2 `DeclareHostileTierWars` would silently exclude that pair from the Day-1 war declaration sweep.

Also bumped `Bannerlord.MCM` NuGet pin from 5.11.3 → 5.11.4 in both csprojs so the API library version matches the vendored MBOptionScreen UI version (was visible in-game as "Mod Configuration Menu 5.11.3" + "MCM UI 5.11.4" — now both 5.11.4).

Verification: `dotnet build` 0 errors. `dotnet test TAOM.Tests` 2,325 passed (was 2,323 — +2 new regression tests). Deployed MCMv5.dll v5.11.4.0 confirmed at game install.

### fix(ui): stop "Press V" hint overlapping ability name in CareerSystem battle HUD

`AbilityHUD.xml` had `Press V` bottom-anchored at `MarginBottom=14` inside a 110px parent while the ability name sat at `MarginTop=74`, so the two text rows collided (`Cap…Press V…rath` instead of `Captain's Wrath` + `Press V` on separate lines). Grew the parent to `SuggestedHeight=132` (panel grows upward since it's bottom-anchored, on-screen position unchanged) and re-anchored `Press V` to `VerticalAlignment=Top MarginTop=96`. New stack top→bottom: portrait → name → `Press V` → charge bar.

### data(mordor): unify banner_key on 7 Mordor clans

Set shared Mordor banner `11.2001.2001.1528.1528.764.764.1.0.0.19015.2002.171.700.700.764.764.0.0.0` on Khôrahîm, Wâwrim, Ârki, Îkhon, Zarûnik, Ûgrakhûr, Brughash. Khôrahîm + Wâwrim go through `spclans.xslt` (vanilla IDs `clan_empire_south_8/9` renamed by TAOM); the other five are direct edits to TAOM-custom `clan_empire_south_10/11/13/14/15` in `characters/clans.xml`. Akheth (`_12`) intentionally retained its distinct banner.

### migration(deps): DR3 — bundle entire BUTR stack inside TAOM.Dependencies module

End-user launcher now needs ONLY `TAOM` + `TAOM.Dependencies` enabled (plus Native/SandBox/SandBoxCore/CustomBattle). No external `Bannerlord.Harmony` / `.UIExtenderEx` / `.ButterLib` / `.MBOptionScreen` modules required — all bundled inside TAOM.Dependencies's `bin/Win64_Shipping_Client/`.

**Architecture (3 dependency categories):**

1. **NuGet PackageReferences** (auto-deployed on build):
   - `Lib.Harmony 2.4.2` → `0Harmony.dll` (Harmony + MonoMod + Cecil ILRepack'd by pardeike)
   - `Bannerlord.UIExtenderEx 2.13.1` → `Bannerlord.UIExtenderEx.dll`
   - `Bannerlord.MCM 5.11.3` → `MCMv5.dll` (MCM API only — settings attributes + base classes)
2. **Vendored BUTR runtime DLLs** (from Steam Workshop, manually copied; tracked in git via `.gitignore` exception):
   - `Bannerlord.ButterLib.dll` + `Implementation.1.4.{0,1}.dll`
   - `Bannerlord.MBOptionScreen.v1.4.{0,1}.dll` + `Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll`
   - `MCM.UI.Adapter.MCMv5.dll`
3. **Microsoft.Extensions + Serilog** (ButterLib runtime deps, vendored alongside ButterLib):
   - 8 Microsoft.Extensions.* + Microsoft.Bcl.HashCode DLLs
   - 3 Serilog.* DLLs
   - 5 System.* polyfills (Buffers, Memory, Collections.Immutable, Numerics.Vectors, Reflection.Metadata)

**Total deployment**: 28 DLLs in `TAOM.Dependencies/bin/Win64_Shipping_Client/`. `SubModule.xml` registers 7 SubModule classes that bootstrap UIExtenderEx + ButterLib (core + Implementation Loader) + MCMv5 (API + Basic Impl) + MBOptionScreen Module Loader. Harmony auto-loads on first HarmonyLib type touch — no SubModule needed.

**Effort**: ~10 hours empirical iteration across decompile-based + upstream-source source-merge attempts (both blocked by polyfill cascading conflicts + Roslyn ICEs documented in `docs/migration/dr3-execution-handoff.md`). Final architecture pivots away from source-merge to the simpler bundle-DLLs approach. NuGet-deployed types share standard assembly identities (HarmonyLib types live in `0Harmony` assembly, etc.) — no decompile artifacts.

**Documentation**:
- `docs/migration/dr3-maintenance.md` — comprehensive update procedure for each dependency category, common scenarios (Bannerlord minor/major patch, BUTR major version, security patch), smoke test verification, risk scenarios (external BUTR module conflict, BUTR delays, fresh clone, Linux compat).
- `docs/migration/dr3-execution-handoff.md` — empirical findings from earlier source-merge attempts (preserved for reference; superseded by this bundle approach).
- `docs/migration/dr3-mcm-internalization-plan.md` — original architectural plan + investigation log.

**Verification**:
- `dotnet build`: 0 errors, 1 pre-existing warning
- `dotnet test TAOM.Tests`: 2,323/2,325 pass (same as baseline; 2 skipped pre-existing)
- Game install `Modules/TAOM.Dependencies/bin/`: 27 DLLs deployed correctly

**Migration impact**: TAOM users who upgrade need to **DISABLE** the external `Bannerlord.Harmony` / `.UIExtenderEx` / `.ButterLib` / `.MBOptionScreen` modules in their launcher to avoid Harmony assembly conflicts. (TAOM provides all of these via TAOM.Dependencies now.)

**Not-tested**: In-game MCM tab rendering — user verifies on next launch.

**Commits**: a89e07a (csproj architecture change), f6c1b76 (complete bundle + maintenance doc).

### fix(deps): bundle BUTR.CrashReport (v14.0.0.99) — resolves ButterLib ReflectionTypeLoadException

DR3 follow-up. First in-game launch surfaced `TAOM Dependencies.ButterLib submodule could not be loaded correctly due to a dependency conflict` immediately after `Bannerlord.ButterLib.dll` loaded. Debug trace showed `ReflectionTypeLoadException` from `mscorlib.dll` during CLR type enumeration of ButterLib.

**Root cause**: `Bannerlord.ButterLib.dll` v2.10.4 metadata references the `BUTR.CrashReport` family (6 DLLs at v14.0.0.99): `BUTR.CrashReport`, `.Models`, `.Renderer.Html/.ImGui/.WinForms/.Zip`. All 6 were missing from our bundle. Diagnosed by dumping ButterLib's `AssemblyReferences` table via `System.Reflection.Metadata` — referenced types resolved to absent assemblies, so CLR threw on `Assembly.GetTypes()` enumeration.

**Red herring ruled out**: ButterLib (and all BUTR DLLs — UIExtenderEx, MCMv5) metadata pins `0Harmony Version=2.2.2.0` while we ship 2.4.2.0. Earlier hypothesis was version mismatch. But `0Harmony.dll` has `PublicKeyToken=null` (un-strong-named) — the CLR default binder matches un-strong-named assemblies by simple name only, ignoring version. The version drift is benign in practice.

**Fix**:
- Copied 6 `BUTR.CrashReport*.dll` v14.0.0.99 from `E:/LOTRAOMAssets/ButterLib-2018-v2-10-4-1777059538/` into `Dependencies/_Module/bin/Win64_Shipping_Client/` (vendored alongside other BUTR DLLs; tracked via `.gitignore` exception).
- Added belt-and-braces `AssemblyResolve` handler in `Dependencies/SubModule.cs` static cctor that redirects requests for `0Harmony`, `Bannerlord.UIExtenderEx`, `Bannerlord.ButterLib`, `MCMv5` to the loaded assembly regardless of requested version. Logs each redirect for diagnostics.
- Ignored `Dependencies/_Module/bin/Gaming.Desktop.x64_Shipping_Client/` (build artifacts only — Bannerlord on Windows reads Win64).
- Updated `Dependencies/_Module/SubModule.xml` comment block + `Dependencies/TAOM.Dependencies.csproj` Harmony pin comment to document the actual root cause + the un-strong-named binding fact.

**Total bundle now**: 33 DLLs deployed (was 27; +6 BUTR.CrashReport).

**Verification**: `dotnet build` 0 errors, `dotnet test TAOM.Tests` 2,323/2,325 pass (baseline parity). In-game verification pending user launch.

Research: ButterLib `AssemblyReferences` metadata table dump via `System.Reflection.PortableExecutable.PEReader` + `PEReaderExtensions.GetMetadataReader`.

---

## 2026-05-22

### feat(diplomacy): Harad permanently allied with Mordor + MakePeace step in alliance enforcement

User reported (post-migration smoke test) Harad showing up in Mordor's Wars panel rather than its Alliances panel. By design Harad should be a Mordor ally from game start and throughout — same bloc as Dol Guldur / Gundabad / Isengard / Rhun.

**Data change:** `Main/_Module/ModuleData/diplomacy/diplomacy.json` — `empire_s ↔ aserai` promoted from tier `Natural` → `Permanent`. This puts Harad in the same enforced bucket as the other Evil-bloc kingdoms (auto-formed at `EstablishInitialAlliances`, re-enforced on every session launch, can't be broken by vanilla AI declaring war).

**Code change:** `Main/Features/Diplomacy/DiplomacyService.EnforcePermanentAlliances` now ends any pre-existing war between the two kingdoms before calling `StartAlliance`. Vanilla 1.4.5's `AllianceCampaignBehavior.StartAlliance` (verified via decompile at `AllianceCampaignBehavior.cs:327-364`) does NOT end an active war — it just creates the `Alliance` object and dispatches `OnAllianceStarted`. Without an explicit `MakePeace` step, loading an existing save where two kingdoms were at war and then newly promoted to Permanent would leave them in allied-AND-at-war contradictory state (alliance object created, `StanceLink.IsAtWar` still true).

New adapter surface: `IAllianceAdapter.MakePeace(kingdomAId, kingdomBId)` wrapping `MakePeaceAction.Apply`. Per ADR-007: TAOM-owned, no sealed types in the interface.

**Reload scope:** `DiplomacyConfigProvider` and `DiplomacyService` are both `Reuse.Singleton` — the config is cached for the entire Bannerlord process. Picking up the new Harad tier requires a Bannerlord restart, not just a save reload. After restart, loading an existing campaign will trigger `EnforcePermanentAlliances` on `OnSessionLaunchedEvent`, which will detect the new permanent pair, end the Mordor↔Harad war via `MakePeace`, then create the alliance.

**Diagnostic instrumentation** (kept from `97f564d`): both `EstablishInitialAlliances` and `EnforcePermanentAlliances` now emit a one-line summary at the end (`X created, Y already-allied, Z silent-noop / X already-ok, Y restored, Z STILL MISSING`) plus a per-pair `AreAllied` probe right after the `StartAlliance` call. The user-reported smoke-test confirmed the system works (Mordor properly allied with all 4 originally-Permanent Evil-bloc kingdoms); the summary stays as a future-debug aid.

---

### migration(ui): mass-flip remaining 22 TAOM prefab files (VerticalBottomToTop → VerticalTopToBottom)

Live in-game observation confirmed the audit-deferred mass swap is now safe. After the CC stages fix landed, the user reported the same inversion happening "across the board" — Party screen, Encyclopedia subpages, Custom Battle screens, Nameplates, MomentumView, Career screen, GameMenu, FacGen PreBuild. The earlier conservatism (per-site review before mass swap) was justified pre-test; with in-game confirmation, the blanket flip is the right call.

73 occurrences across 22 files flipped:
- `CareerSystem/CareerScreen.xml` — 6
- `CustomBattle/{ArmyComposition,CustomBattleScreen}.xml` — 3 each
- `CustomBattle/{SimpleDropdown,TroopTypeSelectionPopUp}.xml` — 1 each
- `Encyclopedia/EncyclopediaSubPages/Encyclopedia{Clan,Faction,Hero,Settlement}Page.xml` — 4 / 3 / 6 / 5
- `FacGen/PreBuildCharacterSelection.xml` — 6
- `GameMenu/GameMenu.xml` — 5
- `MomentumView/{MomentumView,Relationship}.xml` — 8 / 1
- `Nameplate/{Party,PartyPlayer,SettlementLarge,SettlementMedium,SettlementSmall}NameplateItem*.xml` — 1 each
- `Party/{PartyScreen,PartySortController}.xml` — 9 / 1
- `Party/PartyTroopManagerPopUp/{PartyTroopManagerPopUp,PartyTroopUpgradeItem}.xml` — 3 / 3

Zero `VerticalBottomToTop` remain in TAOM source. Deployed directly to game install (build deploy step currently blocked by Bannerlord's DLL lock — game is mid-session).

**Risk acknowledged:** if a specific TAOM site was deliberately authored to use `VerticalBottomToTop` (e.g., a chat-log scroller wanting newest-at-top), it'll now render bottom-up. The user has agreed to revert individual sites post-test if anything looks worse — but the global UI improvement is the expected outcome based on in-game inspection across multiple screens.

---

### migration(ui): fix CC narrative + culture stage ListPanel direction (v1.4.0 layout-fix regression)

**Symptom:** Character Creation Family/Youth/Adolescence stages rendered the "You were born into a family of..." prompt at the BOTTOM of the option list instead of the top. Same regression on the CultureStage faction-info panel (perks/bonuses/strengths/weaknesses lists appeared inverted).

**Root cause:** v1.4.0 fixed a long-standing engine bug where `StackLayout.LayoutMethod="VerticalBottomToTop"` was actually rendering top-to-bottom (the inverse of what the name says). With the engine bug fixed, vanilla 1.4.5 also updated its own prefabs to use `VerticalTopToBottom` at the affected sites. TAOM's customized copies of `CharacterCreationNarrativeStage.xml` and `CharacterCreationCultureStage.xml` were authored against the broken engine and still used `VerticalBottomToTop`.

**Changes:**

- `Main/_Module/GUI/Prefabs/CharacterCreation/CharacterCreationNarrativeStage.xml`: 3 sites flipped `VerticalBottomToTop` → `VerticalTopToBottom`. Verified against vanilla 1.4.5's `Native/GUI/Prefabs/CharacterCreation/CharacterCreationNarrativeStage.xml` — TAOM and vanilla now match exactly on layout direction (TAOM's button-template customizations preserved).
- `Main/_Module/GUI/Prefabs/CharacterCreation/CharacterCreationCultureStage.xml`: 6 sites flipped. These are TAOM-custom faction-info ListPanels (outer `ContentList`, `FactionPerks` list + per-item, `FactionBonuses`, `FactionStrengths`, `FactionWeaknesses`) — all want their children to render top-down in declaration order (section title, then items).

**NOT touched** in this commit: the other 22 TAOM prefabs that still contain `VerticalBottomToTop` (Party screen, Encyclopedia subpages, Custom Battle screens, Nameplates, MomentumView, GameMenu, FacGen PreBuild, Career screen). Per-site review needed — vanilla 1.4.5 kept `VerticalBottomToTop` at 31 specific sites across Native/SandBox/SandBoxCore, so a blind mass-flip would break the legitimate cases. Tracked as a separate audit task; per-prefab diff against vanilla counterpart will drive each remaining fix.

---

### migration(deps): DR1 — point TAOM.csproj at internalized TAOM.Dependencies (fixes launcher dep-conflict error)

**Symptom:** Launcher showed *"TAOM.TAOM submodule could not be loaded correctly due to a dependency conflict"* on game launch — silent runtime no-op behind a soft warning.

**Root cause:** `Main/TAOM.csproj` was still referencing external `Lib.Harmony 2.4.2` NuGet (compile-only, `IncludeAssets="compile"`) and the external `Bannerlord.UIExtenderEx` module via game-folder HintPath. At runtime, TAOM.dll's CLR type refs pointed at assembly names `0Harmony` and `Bannerlord.UIExtenderEx` — but neither DLL ships in `TAOM/bin/`. The merged `TAOM.Dependencies.dll` provides those types under its own assembly identity instead. ButterLib's module pre-check detects the unresolved type refs and reports the parent module as having a "dependency conflict."

The TAOM.Dependencies project was rebuilt for v1.4.5 in commit `43206df` but the consumer csproj refactor that was supposed to follow (and was described as having happened on a prior 1.4 branch) was never committed to this repo's history. DR1 does that consumer-side refactor.

**Changes:**

- `Main/TAOM.csproj`:
  - **Added** `<ProjectReference Include="..\Dependencies\TAOM.Dependencies.csproj"><Private>False</Private></ProjectReference>`. HarmonyLib + Bannerlord.UIExtenderEx type refs in compiled TAOM.dll now point at `TAOM.Dependencies` assembly identity.
  - **Removed** external `<Reference Include="$(GameFolder)\Modules\Bannerlord.UIExtenderEx\...">` HintPath.
  - **Replaced** `<PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />` with `<PackageReference Include="Lib.Harmony" Version="2.4.2" ExcludeAssets="all" PrivateAssets="all" />` — suppresses the transitive `0Harmony 2.2.2.0` pulled in by `Harmony.Extensions` / `BUTR.Harmony.Analyzer`, which would otherwise cause CS0433 ambiguity ("type 'HarmonyPatch' exists in both 0Harmony and TAOM.Dependencies").
  - **Removed** `<PackageReference Include="Harmony.Extensions" Version="3.2.0.77" PrivateAssets="all" />` — TAOM source has zero `using BUTR.Harmony.Extensions` / `using HarmonyLib.BUTR` imports (verified via grep across 110 Harmony-touching files), so the package was unused weight that was only pulling transitive 0Harmony.
  - **Kept** `Bannerlord.MCM 5.11.3` NuGet (compile-only) + bundled `MCMv5.dll` runtime reference. MCM is NOT yet merged into TAOM.Dependencies — that's DR3, deferred to a separate session per the dependency-internalization plan.

- `Main/_Module/SubModule.xml`:
  - `<DependedModuleMetadata id="Native" version="e1.4.5.*" />` → `version="v1.4.5.*"`. Installed Native module declares `<Version value="v1.4.5"/>` (with `v` prefix), so the `e1.4.5.*` constraint never matched.

- `Main/Properties/launchSettings.json`: debug launch args trimmed to drop external BUTR modules (`Bannerlord.Harmony`, `Bannerlord.ButterLib`, `Bannerlord.UIExtenderEx`, `Bannerlord.MBOptionScreen`) and add `TAOM.Dependencies`. The runtime needs zero external BUTR modules now that Harmony + UIExtenderEx + BUTR.Shared are merged into TAOM.Dependencies.dll.

**Verification:** `dotnet build` 0 errors, 1 pre-existing warning. `dotnet test TAOM.Tests` 2,323/2,325 pass (2 skipped pre-existing, same as baseline). Deployed `TAOM/bin/` no longer contains `0Harmony.dll` or `Bannerlord.UIExtenderEx.dll`; `TAOM.Dependencies/bin/` contains the merged `TAOM.Dependencies.dll` (2.8 MB, 1648 classes including `HarmonyLib.*` + `Bannerlord.UIExtenderEx.*` + `Bannerlord.BUTR.Shared.*`).

**Out of scope for DR1 (followups):** MCM internalization (DR3) and a deep-dive on why prior sessions left MCM out of TAOM.Dependencies. ButterLib audit (DR4) — preliminary grep shows zero direct usage in TAOM, but UIExtenderEx may consume BUTR.Shared transitively (already in the merged DLL).

---

### migration(refactor): 10-agent behavioral audit + 5 confirmed runtime fixes

Compile-clean ≠ runtime-correct. Per empirical evidence from the April 2026 v1.4.0 migration attempt (which compiled but failed at runtime), launched a 10-parallel-agent behavioral audit against decompiled vanilla 1.4.5 source. Each agent owned a subsystem (Army, Diplomacy, Battle, Equipment, CulturalFeats, Banner/WotR, UI/Mixin, Mission AI, Reflection sites, CharacterCreation/RaceAge).

**Outcome: 14 findings total — 5 confirmed real (fixed), 4 false positives (rejected), 5 deferred to S6 smoke test verification.**

Full audit report: `docs/reviews/audit-v1.4.5-refactor-2026-05-22.md`.

#### Confirmed fixes

1. **CRITICAL — `Patch22_ArmyTargeting` silent dead feature.** Vanilla 1.4.5 `AiMilitaryBehavior.CalculateDistanceScoreForBesieging` added 3 `out` params (`bestNavigationType`, `isFromPort`, `isTargetingPort`). TAOM's Postfix only matched `(Settlement, MobileParty, ref float)` → Harmony silently failed to bind → entire border-proximity-floor feature has been a runtime no-op since v1.4.0. Added the 3 missing `ref` params; Harmony now binds. (Agent 1)

2. **CRITICAL — 40 commoner-child rosters invisible to engine.** `taom_child_equipment_templates.xml` had 40 rosters with `IsLordTemplate="false"`. The v1.4.3 engine flag-match is exact-subset on TRUE attributes — `false` means "not in the set" → engine never queries for these rosters → naked commoner children at age-up events. Flipped all 40 to `IsLordTemplate="true"` via regex (preserves the 20 existing `true` rosters and the noble-vs-commoner semantic distinction is encoded by the parent `<EquipmentSet equipmentType="Civilian">` already). (Agent 4)

3. **CRITICAL — no `IsKingdomRulerTemplate` rosters for any TAOM culture.** v1.4.3 added `NPCEquipmentsCampaignBehavior.OnRulingClanChanged` which calls `GetEquipmentsForChangingRuler` — returns null when no ruler roster exists → engine wipes new ruler's equipment. Every War of the Ring ruler change would have left lords naked. Extended `tools/generate_lord_template_equipment.py` to emit 4 ruler rosters per culture × 18 cultures = 72 new ruler rosters. Total rosters now 186 (was 76). (Agents 6 + 10)

4. **CRITICAL — 6 XSLT cultures had no lord rosters.** `vlandia`/`empire`/`aserai`/`khuzait`/`sturgia`/`battania` (renamed in `spcultures.xslt` to Rohan/Dunland/Harad/Easterlings/Dale/Khand) had ZERO lord rosters → `Debug.FailedAssert` spam + fallback to vanilla generic Calradic gear at every age-up event for heroes of these cultures. Extended generator's CULTURES list to include the 6, mapping each to closest-styled TAOM equipment file (`rohan`/`dunland`/`harad`/`rhun`/`dale`; battania→harad fallback). (Agent 4)

5. **HIGH — Alliance EndAlliance reentry duplicate-queue bug.** `AllianceCampaignBehavior.OnAllianceTimerExpired` daily-tick calls `EndAlliance(k1,k2)` then `AddAllianceDecision(k1,k2)` unconditionally on the next line. TAOM's Prefix blocks `EndAlliance` to preserve Permanent lore pairs but the duplicate `StartAllianceDecision` still queues, accumulating in `kingdom.UnresolvedDecisions` until vanilla side effects fire on an already-allied pair (undefined behavior). The existing TAOM comment claimed vanilla short-circuits on `IsAlliedWith` — verified false against v1.4.5 source. Created `AllianceCampaignBehavior_AddAllianceDecision_Patch.cs` — Prefix returns false when `kingdomToAddDecision.IsAllyWith(kingdomToOffer)`. Wired in `SubModule.cs`. (Agent 2)

#### Rejected false positives (verified against vanilla 1.4.5)

- Agent 5: `TaomPartyWageModel.cs` missing — file present at `Main/Features/TroopProgression/Models/`, CLAUDE.md doc stale (listed it under CulturalFeats).
- Agent 9: `Mission.RegisterBlow` removed — still 7-param method at `Mission.cs:5400` in v1.4.5; TAOM's reflection lookup correctly resolves it.
- Agent 1: empire-feat double-apply in `TaomArmyManagementModel` — TAOM's `ApplyArmyInfluenceAward` only applies TAOM-custom feats (Rivendell, Gondor), not vanilla `EmpireArmyInfluenceFeat`. Feat sets disjoint.
- Agent 3: `PlayerBluntDamageChance` default stuck at 0.1 — already `0.30f` in `TaomSettings.cs` (likely fixed pre-migration).

#### Deferred to S6 smoke test

- Siege auto-resolve duration doubling (balance concern, not crash) — Agent 3
- `TaomTargetScoreModel` safety-gate refactor (balance) — Agent 1
- `TaomPartyHealingModel` cultural survival multipliers re-tune (data, not code) — Agent 3
- `VerticalBottomToTop` mass swap (60+ sites need per-site visual review) — Agent 7
- `TaomAgentStatCalculateModel` double-invoke verification (runtime check at S6) — Agent 8
- NamedCompanions culture-routing audit (verify each of 18 companions resolves to a TAOM-authored roster) — Agent 10

**Build + tests:**
- `dotnet build Main/TAOM.csproj` — 0 errors, 1 warning
- `dotnet test TAOM.Tests` — 2,323 / 2,325 pass

**Process lesson:** 36% of audit-agent findings were false positives. Future audit-agent prompts must require paste-as-evidence (vanilla source + TAOM source) rather than paraphrased claims, to catch agent confusion before it propagates into fixes.

Refs: #210

## 2026-05-22

### migration(s5b): author v1.4.3 mandatory equipment rosters across 12 cultures

The v1.4.3 equipment-system overhaul requires each culture to provide rosters tagged with specific `<Flags>` combinations so the engine's `EquipmentSelectionModel.GetEquipmentForXxx` queries can find appropriate equipment. Without these rosters, custom-culture NPCs would spawn naked / wrong-culture during come-of-age, marriage, succession, and child-generation events.

**Generated 76 mandatory rosters** in a new centralized file `Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml`:

- 10 cultures × 6 rosters: `IsLordTemplate` × {male, female} × {Battle, Civilian, Teen-Civilian}
- 2 cultures (shaghana, abanissa) × 8 rosters: above + `IsChildEquipmentTemplate` × {male, female} (these 2 cultures aren't covered by `taom_child_equipment_templates.xml` so they need their own child rosters)

**Items sourced from existing TAOM per-culture equipment files** — the first `<EquipmentRoster id="<culture>_bat_template_*">` battle roster and `<culture>_civ_template_*">` civilian roster provide the items. Shaghana and abanissa (no per-culture files; sub-cultures of Harad per kingdom-culture-mapping memory) fall back to harad items. Items are not LOTR-themed per se, just reused from existing TAOM content — they may be refined later as a polish pass.

**New tool:** `tools/generate_lord_template_equipment.py` — generates the centralized roster file by extracting items from existing per-culture equipment files. Idempotent: re-running regenerates from the latest source items. Additive: does NOT modify any existing equipment files.

**Registered in `Main/_Module/SubModule.xml`** under `<Xmls>` as a new `<XmlNode>` so the engine loads it.

**Coverage gate:**
- `python tools/audit_equipment_roster_coverage.py` — all 12 cultures pass 8/8 mandatory combos (was 0-2/8 before; gain: 76 new rosters across the matrix).
- Optional `IsKingdomRulerTemplate` × {male, female} × {Battle, Civilian} = 4/12 cultures still missing → deferred enhancement (TAOM can author dedicated ruler equipment if engine fallback to lord-tier rosters isn't acceptable).

**Build + tests:**
- `dotnet build Main/TAOM.csproj` — 0 errors, 1 warning.
- `dotnet test TAOM.Tests` — 2,323 / 2,325 pass.

Not-tested: live game launch (S6 smoke test deferred). Specifically: that the engine actually resolves these rosters at come-of-age / child-generation events without falling back to vanilla.

Refs: #210
Research: `docs/migration/v1.4.x-equipment-overhaul.md` + `docs/migration/templates/equipment-rosters.md` mandatory matrix.
Constraint: items reused from existing TAOM battle/civilian rosters rather than authored fresh per culture; some semantic mismatches possible (e.g. a "teen" roster uses adult civilian items because TAOM has no separate teen items). S7 feature validation should flag any visible issues.

### migration(s5a): mass XML migration to v1.4.3 equipmentType convention

Mechanical migration of the deprecated equipment-system attributes per the v1.4.3 dev migration spec. Surgical diff (~2,150 line changes total) — formatting, attribute order, comments, self-closing style all preserved.

**Migrated:**
- **1,628 `<EquipmentSet civilian="true">` → `equipmentType="Civilian"`** across 16 XML files (`troops_*.xml`, `characters/*.xml`, `equipmentsets/*.xml`, `taom_wanderers.xml`).
- **389 `<EquipmentSet ... civilian="true" />` in `lords.xslt`** — same migration on the XSLT template.
- **160 deprecated `EquipmentFlags` references in `taom_child_equipment_templates.xml`**:
  - 60× `IsNobleTemplate=*` → `IsLordTemplate=*` (1:1 rename, preserving true/false value).
  - 40× `IsCivilianTemplate="true"` dropped (the new `equipmentType="Civilian"` on parent `<EquipmentSet>` is the new way to encode "civilian template").
  - 60× `IsNoncombatantTemplate="true"` dropped (same reason).

**Left intentionally untouched (per dev spec):**
- `<EquipmentRoster civilian="true">` inline rosters inside `<NPCCharacter>/<Equipments>` blocks. Vanilla 1.4.5 still uses this 1,097× in `spnpccharacters.xml` — it remains valid in 1.4.3+. The migration tool filters by element name (`<EquipmentSet>` only).
- `equipmentType="Battle"` on bare sets. Vanilla 1.4.5 OMITS the attribute when the set is Battle (implicit default). Adding it explicitly would diverge from vanilla style.

**Migration tool improvement:**
- `tools/migrate_equipment_type_1_4_3.py` rewritten to use regex-on-text instead of lxml `tree.write()`. Initial version produced a 130K-line diff because lxml serializes from scratch (reformats whitespace, collapses multi-line tags, changes self-closing style, re-emits the XML declaration with single-quote attribute values). New version reads the file as text, finds opening `<EquipmentSet>` tags via regex, replaces `civilian="true"` in-place — surgical 1,628-line diff.
- Sanity-check: regex replacement count must match lxml-detected count. Mismatch → don't write (would imply tag-detection drift, e.g. multi-line opening tag the regex missed).

**8 Python generators updated to emit new format:**
- `tools/assign_xslt_lord_equipment.py`, `tools/generate_rhun_troops.py`, `tools/generate_gondor_troops.py`, `tools/generate_char_creation_equipment.py`, `tools/generate_batch2_wanderers.py` (×2 sites), `tools/extract_wanderers.py` (×2 sites) — write-side emit `equipmentType="Civilian"`.
- `scripts/replace_equipment_templates.py`, `tools/assign_lord_equipment.py` — read-side detector now accepts BOTH new and legacy forms for backward compat (will never see the legacy form post-S5a, but accepts it defensively).

**Validation gates:**
- `python tools/validate_equipment_flags_1_4_3.py` — 0 deprecated flag occurrences remaining (was 160).
- `python tools/migrate_equipment_type_1_4_3.py --dry-run` — 0 files needing migration (was 16).
- `dotnet build Main/TAOM.csproj` — 0 errors, 1 warning.
- `dotnet test TAOM.Tests` — 2,323 / 2,325 pass.

Refs: #210
Research: `docs/migration/v1.4.x-equipment-overhaul.md` + `docs/migration/templates/equipment-rosters.md`
Not-tested: live game launch (S6 smoke test deferred to a follow-up session).
Pending: S5b (~96 missing equipment rosters across 12 cultures for the v1.4.3 mandatory roster contract).

## 2026-05-22

### migration(v1.4.5): S1–S5 complete — `bannerlord-1.4.5` builds + tests green against 1.4.5

After S0 Foundation, the actual code migration completed in **4 file fixes**:

1. **`Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs`** — `CalculateRenownGain` signature gained 2 params in v1.4.0:
   - Old (3-param): `(PartyBase party, float renownValueOfBattle, float contributionShare)`
   - New (5-param): `(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float renownMultiplierForWinnerSide, bool includeDescriptions)`
2. **`Main/Features/Diplomacy/Models/TaomAllianceModel.cs`** — `GetScoreOfStartingAlliance` dropped `IFaction evaluatingFaction` param (had been added in v1.4.0, removed in v1.4.5 — TAOM had swung past the target).
3. **`Main/Adapters/ChildCreatorAdapter.cs`** — `GetEquipmentRostersForInitialChildrenGeneration` (returned `MBList<MBEquipmentRoster>`) renamed in v1.4.3 to `GetEquipmentForInitialChildrenGeneration` (returns single `Equipment`); gender + culture filtering moved inside the model. Rewrote `AssignEquipment` to mirror vanilla 1.4.5 `InitialChildGenerationCampaignBehavior`.
4. **`Main/Features/SpecialResources/SpecialResourcesBehavior.cs`** — `OnHideoutCompleted` event delegate gained 3rd param `HideoutEventComponent.HideoutBattleEndState` in v1.4.3.

**Build + test results:**
- `dotnet build Main/TAOM.csproj`: ✅ 0 errors, 1 warning
- `dotnet test TAOM.Tests`: ✅ **2,323 / 2,325 pass** (0 failed, 2 skipped — pre-existing)

S1 (TAOM Dependencies) was a separate effort: restored from git SHA `0b16cca` (1,444 files), rebuilt clean against 1.4.5 (0 errors, 878 benign warnings). Internalized Harmony 2.4.2 fork confirmed fully API-compatible with 1.4.5.

**Pending (S5a, S5b, S6–S12):**
- S5a: 3,372 `<EquipmentSet civilian="true">` → `equipmentType="Civilian"` migrations across 51 files + 160 deprecated `EquipmentFlags` hits in `taom_child_equipment_templates.xml`
- S5b: ~96 missing equipment rosters across 12 cultures (1.4.3 mandatory contract — IsLordTemplate/IsKingdomRulerTemplate combinations)
- S6: smoke test (game launch + campaign loop)
- S7–S10: per-feature validation
- S11: Codex adversarial review
- S12: closeout

Not-tested: in-game runtime — pending S6.

Constraint: vanilla 1.4.0 AI Army + Diplomacy rewrites + 1.4.3 Equipment system overhaul didn't break the compile, but may surface behavioral changes during S7–S10 feature validation. Cavalry auto-resolve bonus removal alone may rebalance several cav-heavy faction features.

Research: `DefaultBattleRewardModel.CalculateRenownGain`, `DefaultAllianceModel.GetScoreOfStartingAlliance`, `EquipmentSelectionModel.GetEquipmentForInitialChildrenGeneration`, `CampaignEvents.OnHideoutBattleCompletedEvent` — all decompiled from live 1.4.5 install at `E:\Steam\...\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll`.

### migration(v1.4.5): S0 Foundation complete — Bannerlord 1.3.15 → 1.4.5 migration started

Started multi-week migration to Bannerlord 1.4.5 on branch `bannerlord-1.4.5` (from `bannerlord-1.3.15` HEAD `2f6756d`). S0 (Foundation) is complete; S1–S12 pending.

**Decompile + tooling**
- Fresh 1.4.5 decompile at `E:\Decompiled_Bannerlord\` (6,146 core + 354 module .cs files — SandBox, StoryMode). Stale 1.4.x dump archived to `E:\Decompiled_Bannerlord_v1.4_OLD\`.
- `tools/taom-src.ps1` now auto-detects version from `Version.xml` (caches `~/.taom-src/<version>/` — supports v1.3.15 backup + v1.4.5 live install simultaneously).
- New tools: `tools/decompile_to_folder.ps1`, `tools/migrate_equipment_type_1_4_3.py`, `tools/audit_equipment_roster_coverage.py`, `tools/validate_equipment_flags_1_4_3.py`.
- `Directory.Build.props` extended with `BANNERLORD_OVERRIDE_DIR` for dual-DLL workflow.
- `Main/_Module/SubModule.xml` Native dep bumped `e1.3.0.*` → `e1.4.5.*`.
- 1.3.15 DLL backup at `E:\BannerlordBackup\1.3.15\bin\Win64_Shipping_Client\` (1.475 GB, 8,568 files).

**Documentation** (all in `docs/migration/`)
- `v1.4.x-overview.md`, `v1.4.x-changes.md` (full changelog analysis), `v1.4.x-equipment-overhaul.md` (v1.4.3 deep dive), `v1.4.x-taom-impact.md` (per-surface matrix).
- `dual-dll-setup.md` — Steam update + DLL backup procedure.
- `api-diff-1.3.15-to-1.4.5.md` — 15-class signature diff.
- `templates/{README,characters,equipment-rosters,troops-and-parties}.md` — vanilla-1.4.5-derived "what right looks like" templates.

**Issues surfaced for downstream sessions**
- `TaomBattleRewardModel.CalculateRenownGain` has 3-param signature; 1.4.5 needs 5 (S3).
- `TaomAllianceModel.GetScoreOfStartingAlliance` has extra `IFaction evaluatingFaction` param dropped in 1.4.5 (S3).
- `ChildCreatorAdapter.cs:40` API renamed + return type changed — structural rewrite needed (S2).
- 3,372 `<EquipmentSet civilian="true">` occurrences across 51 files need migration (S5a).
- `taom_child_equipment_templates.xml` has 160 deprecated `EquipmentFlags` hits (S5a manual fix).
- All 12 cultures (not 10) fail the 1.4.3 mandatory roster matrix — need IsLordTemplate / IsKingdomRulerTemplate combinations (S5b).
- 6 sites use `VerticalBottomToTop` ListPanel layout — may need swap to `VerticalTopToBottom` after v1.4.0 fix (S5).
- TAOM.Dependencies source de-tracked from git since `0b16cca` (April 2026); needs restore for S1.

Not-tested: live game launch (will happen in S6 after compile is green).

Save-compat: TBD — will validate in S6.

Constraint: cannot stay on v1.3.15 indefinitely; player base will be forced to 1.4.5 once Steam locks the version.

## 2026-05-21

### data(clans): unify banner_key heraldry across Rohan + Isengard noble houses

Updated `banner_key` on 21 noble clans in [clans.xml](Main/_Module/ModuleData/characters/clans.xml) for visual consistency within each kingdom.

**Rohan (vlandia, 10 clans, sigil `21005`)** — Ælfwiging (12), Celmunding (13), Widmunding (14), Ordgaring (15), Hunthelming (16), Bregdaning (17), Deáfringas (18), Marhad (19), Morcargas (20), Rungaring (22). Unified template `11.X.X.1528.1528.764.764.1.0.0.21005.2004.171.700.700.764.764.0.0.0`; primary color X varies per house. Celmunding/Ordgaring/Rungaring intentionally share `X=273`. Halethring (21) untouched.

**Isengard (11 clans, sigil `23000`)** — White Hand (1), Red Echelon (2), Black Maw (4), Blood Guard (6), Shadow Reavers (7), Skull Crushers (8), War Hounds (9), Ash Guard (10), Stone Breakers (11) on `23000`. Two named-sigil exceptions: Black Echelon (3) → `23001`, Iron Fist (5) → `23002`. Common template `11.2001.2001.1528.1528.764.764.1.0.0.<sigil>.224.171.605.605.764.764.0.0.0`.

Save-compat: existing saves keep their cached banners; new campaigns pick up the new keys.

### fix(lore): rewrite Gondor named-hero equipment for Boromir + Faramir

**Boromir** was rendering naked in the encyclopedia because `boromir_bat_equipment` + `boromir_civ_equipment` (in [taom_equipment_sets_gondor.xml](Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_gondor.xml)) referenced `Item.sk_gd_osg_inf_chest_elite_a` — a body armor variant that does not exist in `LOTRLOME_Armory` (Osgiliath only ships `_med_a`, `_med_b`, `_heavy_a`, `_heavy_b`). `MBObjectManager.GetObject<ItemObject>` returned null → body slot resolved to empty → bare torso. Same failure class as career-system RCA 2026-05-19.

Rewrote both rosters with the full Osgiliath noble/elite kit per user direction — all IDs verified by exact display-name match against `LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/*.xml`:

| Slot | Item ID | Display name |
|------|---------|--------------|
| Head | `sk_gd_osg_noble_helmet_heavy_a` | Osgiliath Noble Helmet A |
| Body | `sk_gd_osg_inf_chest_heavy_b` | Osgiliath Heavy Armour B |
| Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | Osgiliath Elite Pauldron I - Cape |
| Gloves | `sk_gd_osg_bracer_noble_elite_a` | Osgiliath Noble Elite Bracer |
| Leg | `sk_gd_ano_grvs_noble_heavy_a` | Anorien Noble Heavy Greaves (Osgiliath has no Noble Heavy Greaves variant; Anorien is the only family that ships this slot at that tier) |

Boromir's signature weapons (`wm_gondor_boromir_sword`, `wm_boromir_shield`) + mount + harness were already correct — kept as-is.

**Faramir** wasn't rendering his Ithilien Ranger identity in the encyclopedia — his battle roster referenced `faramir_armor` (a custom brown noble jerkin mesh) instead of an Ithilien-themed piece. Per user direction, swapped two slots in `faramir_bat_equipment`:
- Body: `faramir_armor` → `ithilien_jerkin_long` (green/grey ranger cloth-leather, lore-accurate for Captain of the Rangers of Ithilien)
- Arrows: `wm_elven_arrow_v4_a` ("Noldar Elven Arrow IV") → `wm_elven_arrow_v2_d` ("Noldar Elven Arrow X" — top of the I-X Noldar range, per the same "best in Armory" rationale that drove the 2026-05-19 Ithilien Ranger troop quiver upgrade)

Civilian roster (`faramir_civ_equipment`) intentionally stays on `faramir_armor_slim` — Faramir is a Noble of Gondor off-duty, the brown jerkin fits that peace-time identity. Encyclopedia + battle preview now show the ranger kit.

Files: [taom_equipment_sets_gondor.xml](Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_gondor.xml) lines 144-191. No code changes. No new items.
Save-compat: equipment is re-bound from XML on game load; no migration needed.
Not-tested: in-game verification (user reported the naked + wrong-outfit symptoms originally; expect their next encyclopedia check to confirm).

### refactor(marketplace): extract maintenance service + deep-review fix-ups (#207 follow-up)

`/deep-review` on the prior commit's changes returned 2 HIGH, 1 MEDIUM, 1 LOW — all fixed:

- **HIGH:** `OnNewGameCreatedPartialFollowUpEvent` fires for i ∈ [0, 99]; the prior guard `if (i < 2) return` ran the uncapped initial filter sweep 98× per new game (functionally correct but 97 redundant log lines). Added `_initialSweepDone` one-shot flag set after success OR on exception.
- **HIGH:** [`CultureMarketplaceBehavior`](Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs) had grown to 194 lines with inline business logic in `EnsureGuaranteedStock` + `FilterForeignCultureItems` — violates ADR-002. Extracted to new [`ICultureMarketplaceMaintenanceService`](Main/Features/CultureMarketplace/ICultureMarketplaceMaintenanceService.cs) + [impl](Main/Features/CultureMarketplace/CultureMarketplaceMaintenanceService.cs); behavior delegates and shrinks to ~140 lines. Tests renamed from `CultureMarketplaceBehavior{GuaranteedStock,Filter}Tests` (reflection-private) to `CultureMarketplaceMaintenanceService{GuaranteedStock,Filter}Tests` (public API) with +2 null-cultureId defenses.
- **MEDIUM:** [`TownRosterAdapter.GetItemCount`](Main/Adapters/TownRosterAdapter.cs) previously returned only the first matching `ItemRoster` stack's count via `FindIndexOfItem`. Vanilla splits stacks by `ItemModifier`, so a town with "Sharp warg_brown ×3" + "Damaged warg_brown ×2" reported 3 (or 2), not 5. Latent at MinStock=1, real misfire at higher floors. Now iterates all stacks where `GetItemAtIndex(i) == itemObject` and sums `GetElementNumber(i)`.
- **LOW (deep-review false positive — initial "fix" reverted):** Deep-review flagged Routing dict's `StringComparer.Ordinal` as asymmetric vs `_byCulture`'s `OrdinalIgnoreCase`. Initially changed but self-audit on the Codex Phase 4b prompt's S4 case-sensitivity suspect surfaced that the agent conflated culture-id-keyed dicts (case-insensitive, defensive against author-typo) with item-id-keyed dicts (case-sensitive because `MBObjectManager.GetObject<ItemObject>` is case-sensitive ordinal — verified Phase 3). Item-id-keyed dicts (`_routing`, `Blacklist`, `WeightBoosts`) are consistently `Ordinal` across the feature. Reverted; corrected the comment to distinguish key scopes.

Plus: registered `ICultureMarketplaceMaintenanceService` in [`CultureMarketplaceIoC`](Main/Features/CultureMarketplace/CultureMarketplaceIoC.cs); SubModule.cs ctor now passes 6 deps (was 5).

Full suite **2323/2325 green** (was 2321). CultureMarketplace+adapter scope: **89/89** (was 87). RCA Phase 4b addendum at [`docs/reviews/rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md`](docs/reviews/rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md).

Save-compat: no change. Service registration is additive in IoC.
Research: confirmed via decompiled v1.3.15 `ItemRoster.FindIndexOfItem` returns first matching index (file path: `~/.taom-src/v1.3.15/TaleWorlds.CampaignSystem.Roster.ItemRoster.cs`).

### feat(marketplace): guaranteed warg stock + cross-culture item filter (#207 follow-up)

In-game screenshot at Orthanc (Isengard) revealed two market problems after the original `#207` ship: wargs were missing (random K=6 draw from a ~200-item Isengard pool gives each warg ~3% chance per day), and `[Gondor] Light Horse Armour — Pinnath Gelin` + `[Rohan] Horse Armour I` were leaking in from vanilla's `VillageGoodProductionCampaignBehavior.DistributeInitialItemsToTowns` (25 production passes seeded each town's roster with no culture filter, items vanilla classifies via attribute culture). Two new behavior passes solve both:

- **Guaranteed-stock pass** — new `min_stock` attribute on `<Routing><Item>` entries. Listed items are kept above a floor by daily top-up; the cap (`PerTownTotalRosterCap` = 200) is bypassed because lore-essential items must always be available. Seeded `min_stock="1"` on the 4 warg items so every Isengard / Mordor / Gundabad / Dol Guldur town has ≥1 of each variant + the saddle at all times.
- **Cross-culture filter pass** — for each distinct item in a town's roster, compute its effective culture via the shared classifier (attribute → prefix → alias). If the effective culture is set AND ≠ the town owner's culture AND the item is NOT in the routing list for this culture, remove it. Vanilla universals (no culture attribute) and routed items (wargs in mordor towns, etc.) are kept. Capped at `MaxFilterRemovalsPerTick` = 6 to bound surprise; the cap is dropped for a one-time sweep on `OnNewGameCreatedPartialFollowUpEvent(i≥2)` so the entire vanilla initial seed gets cleaned in one pass.

Both passes share an extracted classifier `ICultureItemPoolService.ClassifyEffectiveCulture(attributeCultureId, prefixCultureId)` so the filter and the pool builder never disagree about what culture an item belongs to. The behavior also gains `GetRoutedItemsForCulture(cultureId)` for both routing-aware passes.

Files: [`MarketplaceTuning.cs`](Main/Features/CultureMarketplace/Domain/MarketplaceTuning.cs) (+ `MaxFilterRemovalsPerTick=6`), [`RoutedItem.cs`](Main/Features/CultureMarketplace/Domain/RoutedItem.cs) (NEW — `ItemId + Cultures + MinStock`), [`RosterItemSnapshot.cs`](Main/Adapters/RosterItemSnapshot.cs) (NEW — TAOM-owned DTO per ADR-007), [`ITownRosterAdapter.cs`](Main/Adapters/ITownRosterAdapter.cs) + [`TownRosterAdapter.cs`](Main/Adapters/TownRosterAdapter.cs) (`+GetItemCount` + `+RemoveItem` via `AddToCounts(-N)` + `+EnumerateRoster`), [`ICultureMarketplaceConfigProvider.cs`](Main/Features/CultureMarketplace/ICultureMarketplaceConfigProvider.cs) + [`CultureMarketplaceConfigProvider.cs`](Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs) (routing dict now `Dictionary<string, RoutedItem>`, `min_stock` parsed with `FiniteFloatValidator`-style range gate 0–100), [`ICultureItemPoolService.cs`](Main/Features/CultureMarketplace/ICultureItemPoolService.cs) + [`CultureItemPoolService.cs`](Main/Features/CultureMarketplace/CultureItemPoolService.cs) (extracted `ClassifyEffectiveCulture` + new `GetRoutedItemsForCulture`), [`CultureMarketplaceBehavior.cs`](Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs) (now `OnDailyTickSettlement` runs guaranteed → filter → weighted-random; hooks `OnNewGameCreatedPartialFollowUpEvent` for the uncapped initial sweep), [`culture_marketplace_config.xml`](Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml) (`min_stock="1"` on 4 wargs), [`Main/SubModule.cs`](Main/SubModule.cs) (resolves `MarketplaceTuning` for the behavior ctor).

34 new tests across 4 new classes ([`CultureItemPoolServiceClassifierTests`](TAOM.Tests/Features/CultureMarketplace/CultureItemPoolServiceClassifierTests.cs) — 6, [`GetRoutedItemsForCultureTests`](TAOM.Tests/Features/CultureMarketplace/GetRoutedItemsForCultureTests.cs) — 6, [`CultureMarketplaceBehaviorGuaranteedStockTests`](TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceBehaviorGuaranteedStockTests.cs) — 8 via reflection-invoked private methods with mocked adapter, [`CultureMarketplaceBehaviorFilterTests`](TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceBehaviorFilterTests.cs) — 9 same shape) plus 5 new tests extending the existing ConfigProvider tests for `min_stock` validation. Full suite 2321/2323 green (was 2287). CultureMarketplace+adapter scope: 87/87 (was 53).

Save-compat: no schema change; the new XML attribute is optional. The filter is destructive to existing rosters on the first run after install — towns lose foreign-culture LOTRLOME items they previously held, but vanilla universals (food, trade goods, base armour) are unaffected.
Research: confirmed via decompiled v1.3.15 that `ItemRoster.AddToCounts(EquipmentElement, int)` accepts negative counts for removal (line 194), `OnInventoryUpdated` → `TownMarketData.OnTownInventoryUpdated` updates market prices on the negative path (Town.cs line 754), `FindIndexOfItem`/`GetItemAtIndex`/`GetElementNumber` are the right index-shift-safe enumeration primitives (lines 117 / 232 / 242).
Not-tested: end-to-end in-game (player must restart fresh sandbox, walk to Orthanc, confirm wargs present + no Gondor/Rohan items). Adapter `RemoveItem` going through `AddToCounts(-N)` is unit-tested via mocked adapter; live `OnInventoryUpdated` notification flow not exercised.

### fix(career-system): Captain of Osgiliath Keystone descriptions now say "Career Ability" not "Sailing"

In-game report: the Captain of Osgiliath career screen Tier 1 Keystone read *"Sailing ability radius increased, commanding wider river approaches."* — confusing, since the actual ability is named **Hold the Line** in [`taom_ability_templates.xml`](Main/_Module/ModuleData/career_system/taom_ability_templates.xml):16 and the user expected a "Career Ability" label. Every other career in [`taom_career_choices.xml`](Main/_Module/ModuleData/career_system/taom_career_choices.xml) uses its actual ability name in the Keystone text (Ambush, Stampede, Twin Strike, Storm of Arrows, …); Captain of Osgiliath was the only career whose descriptions referenced a thematic word ("Sailing") that doesn't match its ability. Fix: 7 string substitutions across the root passive + 6 Keystones, replacing standalone `Sailing` with `Career Ability` and `Sailing ability` with `Career Ability` (avoiding the redundant *"Career Ability ability"*). Applied across 5 files: the runtime career choices XML, the English strings XML, and the 3 language stubs (RU/PL/SP — all English placeholders, untranslated). Ability template untouched — the ability stays "Hold the Line"; user chose the generic "Career Ability" phrasing over renaming the ability itself. Pure data fix, no C# or schema changes.

## 2026-05-20

### fix(lore): rewrite Gondor + Mordor hero encyclopedia bios — 47 lords get inline `text=`

In-game report: a Gondor lord's encyclopedia bio read *"Vorondir is a clansman of Dunland of Empire and head of House of Garvirionath..."* — clearly wrong, both the title ("clansman of Dunland") and the kingdom name ("Empire" not "Gondor"). **Root cause:** [`spkingdoms.xslt`](Main/_Module/ModuleData/spkingdoms.xslt):55 sets `Kingdom.empire_w` (Gondor) and `:87` sets `Kingdom.empire_s` (Mordor) both to `culture="Culture.empire"` — necessary because the engine's `str_faction_official.<culture>` lookup is culture-keyed and can't route one culture to three sibling LOTR kingdom labels. So the engine resolves `{TITLE}` via the kingdom's culture, hitting [`taom_module_strings.xml`](Main/_Module/ModuleData/taom_module_strings.xml):9 (`str_faction_official.empire` = *"a clansman of Dunland"*) and [`module_strings.xslt`](Main/_Module/ModuleData/module_strings.xslt):42 strips `str_short_term_for_faction.empire_w` without a replacement so `{FACTION_NAME}` falls back to vanilla *"Empire"*. **Fix:** override per-hero rather than touch the kingdom culture mapping (root-cause fix would risk troop spawn / fief / dialog systems). Added `text="{=aom_lord_<id>_bio}..."` to 38 Gondor `<Hero>` elements + 9 Mordor uruk-captain `<Hero>` elements in [`heroes.xml`](Main/_Module/ModuleData/characters/heroes.xml) — the engine treats per-hero `text=` as a complete override of the template, bypassing the broken culture chain (pattern already in use at `heroes.xml:1122` for `lord_1_60` Eldorion). Bios are 3-4 sentence Tolkien-anchored prose with civil-war stance (Denethor vs. the King's faction) for each Gondor house; orc-captain bios use Black Speech ("Lugburz", "ghash", "tarks") and inter-clan rivalries (Mauhoshat vs. Maugrukh, Ruklash vs. Mauhoshat) in the Shagrat/Gorbag mould. Lore reference docs drafted from Tolkien Gateway sources by two web-research sub-agents; 4 parallel bio-drafting sub-agents produced the prose. Apply script at [`tools/apply_hero_bios.py`](tools/apply_hero_bios.py) embeds all 47 bios, regex-inserts the attribute, and XML-validates the result; one-shot, idempotent. **No code changes, no XSLT changes, no kingdom culture changes.** Out-of-scope follow-up: the broken `Culture.empire` mapping on `Kingdom.empire_w`/`Kingdom.empire_s` remains — un-textured future Gondor/Mordor heroes will still get the broken auto-template (file a separate issue for the root-cause fix; behavior risk).

### fix(equip-presets): hero lookup via CampaignObjectManager (Save New was a no-op)

In-game report: opening the inventory → **Presets** modal showed `Hero: main_hero  Presets: 0/10` correctly, but `Save New` produced `No active hero on the inventory screen.` and the preset was never persisted. Root cause: [`EquipmentSlotAdapter.HeroExists`](Main/Adapters/EquipmentSlotAdapter.cs) + `Capture` looked the player Hero up via `MBObjectManager.Instance.GetObject<Hero>("main_hero")` — but Heroes register **only** in `Campaign.Current.CampaignObjectManager` ([`Hero.ctor`](C:\Users\mikew\.taom-src\v1.3.15\TaleWorlds.CampaignSystem.Hero.cs):1450-1452 calls `CampaignObjectManager.AddHero(this)` and nothing else). `MBObjectManager.GetObject<Hero>(…)` therefore returns null for every hero, gating `SaveCurrent` at the `HeroExists` check. The dialog header worked because `InventoryScreenAdapter.ActiveHeroStringId` reads `_currentCharacter` from the live `SPInventoryVM` and doesn't go through any object manager. Fix is a one-line swap to `Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroStringId)` on both call sites (the canonical TaleWorlds API, used by vanilla itself in `HeroCreator.CreateBasicHero` and `Hero.FindHero`). `grep` confirmed these are the only two `MBObjectManager.GetObject<Hero>` uses in `Main/`. Existing tests stay green — they mock `IEquipmentSlotAdapter` so they never exercised the broken lookup path. Memory entry [`feedback_hero_lookup_via_campaignobjectmanager.md`](file://C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_hero_lookup_via_campaignobjectmanager.md) added so this trap doesn't recur. Not-tested: hero lookup requires live `Campaign.Current`.

### fix(marketplace): dedup routed-cultures after alias normalization (#207)

Codex self-review of the post-fix code ([`docs/reviews/codex-adversarial-culturemarketplace-fixes-2026-05-20.md`](docs/reviews/codex-adversarial-culturemarketplace-fixes-2026-05-20.md)) found 1 LOW: routed cultures weren't deduplicated after alias normalization. Two collision cases were broken:

- **Author typo:** `<Item id="x" cultures="mordor,mordor" />` — same culture listed twice → item added to mordor pool twice → 2× draw weight silently.
- **Alias collision:** `<Item id="x" cultures="rohan,vlandia" />` — `rohan` aliases to `vlandia` per Codex review C2 fix, so both entries collapse to `vlandia` → 2× weight silently.

**Fix:** [`CultureItemPoolService.BuildPools`](Main/Features/CultureMarketplace/CultureItemPoolService.cs) routing branch now builds a `HashSet<string>(StringComparer.OrdinalIgnoreCase)` of post-alias culture targets. Only first occurrence is added; subsequent duplicates increment a counter; one warning is logged per affected item if duplicates were seen. Two regression tests added: `BuildPools_RoutedItem_DuplicateCultureExact_DedupsToOneEntry` + `BuildPools_RoutedItem_AliasCollision_DedupsToOneEntry`. Full suite 2287/2289 green.

Other 6 of 7 Codex Known Suspects DISPUTED (no bug — S1 routing+alias interaction, S3 C4 latch counter behavior, S4 diagnostic logging cost, S5 routing invariant, S6 C1 rename leftovers, S7 case-sensitivity asymmetry). Regression check confirmed all C1–C4 fixes carry over clean. RCA addendum "Phase 3" section.

### feat(marketplace): cross-culture item routing + per-culture pool diagnostics (#207)

User in-game testing surfaced two findings the Codex review didn't cover:

- **C5 — Wargs only show in Isengard markets.** [`Alliance.Wargs/ModuleData/Items/LOTR/lotr_warg.xml`](file:///E:/Steam/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/Alliance.Wargs/ModuleData/Items/LOTR/lotr_warg.xml) tags all four warg items (`warg_brown`, `warg_dark`, `warg_albino`, `warg_saddle`) with `culture="Culture.isengard"` — but lore-correctly wargs belong to all four "evil" cultures (Isengard + Mordor + Gundabad + Dol Guldur). Added a generic `<Routing>` XML mechanism in [`culture_marketplace_config.xml`](Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml). Items in the routing block IGNORE their attribute/prefix and appear ONLY in the listed cultures' pools. New methods: [`ICultureMarketplaceConfigProvider.GetItemRouting()`](Main/Features/CultureMarketplace/ICultureMarketplaceConfigProvider.cs); `CultureItemPoolService.BuildPools` checks routing before the attribute/prefix path. The 4 warg items are now seeded in the config; future cross-culture items need one XML line.

- **C6 — Rivendell market shows Harad/Rhun equipment.** Investigation: LOTRLOME `Rivendell/` folder has zero items tagged with Aserai/Khuzait cultures, the PrefixMap routes Rivendell items correctly, and my injection only pulls from `GetPool("rivendell")` for Rivendell-owned towns. Most likely cause: vanilla `DistributeInitialItemsToTowns` runs 25 village-production passes at `OnNewGameCreatedPartialFollowUpEvent(i=1)` and writes the output directly into town rosters with no culture filter — independent of CultureMarketplace. To make this verifiable next session, added per-culture pool-size + sample-item logging in [`CultureItemPoolService.BuildPools`](Main/Features/CultureMarketplace/CultureItemPoolService.cs). Boot log will now show one line per culture with the first 4 item IDs, so the user can confirm whether the Rivendell pool actually contains foreign items (real bug) or only Rivendell items (vanilla-seeded, out of scope).

12 new regression tests added (6 routing pool-service + 6 routing config-parser); full suite 2276/2278 green. RCA addendum "Phase 2.5" section. Save-compat: no change — XML override is additive, no schema migration.

### fix(marketplace): post-Codex review fixes for culture injection (#207)

Codex adversarial review of CultureMarketplace ([`docs/reviews/codex-adversarial-culturemarketplace-2026-05-20.md`](docs/reviews/codex-adversarial-culturemarketplace-2026-05-20.md)) found 0 CRITICAL, 0 HIGH, 2 MEDIUM, 2 LOW — all four confirmed and fixed in the same session:

- **C1 (MED) cap semantics** — `PerTownInjectedCap` was enforced against the whole `Settlement.ItemRoster.Count`, but vanilla `VillageGoodProductionCampaignBehavior.DistributeInitialItemsToTowns` runs 25 village-production passes per town at OnNewGameCreated, often leaving towns at 30-80 distinct items before our feature ever runs. A town at 50+ distinct vanilla items got at most 10 of our items before the cap permanently blocked further injection. **Fix:** renamed [`MarketplaceTuning.PerTownInjectedCap`](Main/Features/CultureMarketplace/Domain/MarketplaceTuning.cs) → `PerTownTotalRosterCap` and raised the default 60 → 200. The semantic now matches the field name; cap retains its bound-unbounded-growth purpose with 120+ headroom even in fully-seeded towns.

- **C2 (MED) Rohan culture alias** — `LOTRAOM_horses.xml` lines 231-330 declare Rohan harnesses with invalid `culture="Culture.rohan"` (Rohan towns use `vlandia`, not `rohan`, per TAOM culture cheatsheet). Attribute-wins logic routed those items into a `rohan` pool no town ever queries — Rohan markets never saw their own horse harnesses. **Fix:** added `CultureAliases` dictionary in [`CultureItemPoolService`](Main/Features/CultureMarketplace/CultureItemPoolService.cs) mapping `rohan` → `vlandia` (case-insensitive). Normalization runs after attribute lookup but before grouping. Future invalid aliases get one new dict entry.

- **C3 (LOW) prefix fallback gaps** — Five LOTRLOME crafted weapons (`mirkwood_sword_a01`, `mirkwood_spear_a01/a02`, `mirkwood_glaive_a01`, `wm_harad_glaive_a01`) ship with `is_merchandise="true"` but no `culture=` attribute and no PrefixMap row, so they were silently dropped from injection. **Fix:** added `("mirkwood_", "mirkwood")` and `("wm_harad_", "aserai")` rows to [`ItemPoolAdapter.PrefixMap`](Main/Adapters/ItemPoolAdapter.cs). 7 new prefix-resolution tests guard against future regressions.

- **C4 (LOW) failure latch** — `EnsurePoolBuilt` caught all exceptions and left `_poolBuilt=false`, causing `OnDailyTickSettlement` to retry on every town tick (200+ towns × daily) forever and spam the log. **Fix:** added a 3-attempt latch in [`CultureMarketplaceBehavior`](Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs); after 3 failed `BuildPools` attempts the feature flips inert for the rest of the session with one final error log.

Disputed (no bug — design verified correct): S4 (`ItemObject.Culture` vs `Clan.Culture` type asymmetry — `StringId` comparison is the right path; vanilla `ReadObjectReferenceFromXml` strips the `Culture.` prefix), S5 (1211× unit-weight float distribution — accumulation is exact for integer sums ≤ 16,777,216; last-item fallback unreachable), S6 (save-load resilience — vanilla `ItemRoster.CalculateCachedStatsOnLoad` replaces invalid items with `DefaultItems.Trash` automatically), S7 (DailyTickSettlement parallelism — `_dailyTickSettlementTicker.Initialize(doParallel: false)` confirms sequential dispatch), S8 (no quest item reservations).

9 new regression tests added across `CultureItemPoolServiceTests` + new `ItemPoolAdapterPrefixTests`; full suite 2263/2263 green. RCA addendum at [`docs/reviews/rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md`](docs/reviews/rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md) "Phase 2" section.
Save-compat: no schema change. The cap rename is in-memory only (no SyncData). Safe to load existing saves.

### feat(marketplace): culture-aware item injection into town markets (#207)

LOTRLOME_Armory ships ~6,155 culture-tagged items across 17 LOTR factions but town markets never surface them — vanilla's only routine producer is `VillageGoodProductionCampaignBehavior.TickGoodProduction`, which iterates `VillageType.Productions` (food + raw materials, not equipment), and `ItemObject.Culture` is silently unread at market time. New [`CultureMarketplaceBehavior`](Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs) listens on `DailyTickSettlementEvent` and injects K=6 weighted-random culture-appropriate items per town per day (per-town distinct-item cap of 60 prevents unbounded growth, vanilla price flow handles depletion). Culture binding is dynamic — `town.OwnerClan?.Culture?.StringId` is read every tick, so conquest immediately shifts market identity (Mordor takes Minas Tirith → next day's stock pulls from the Mordor pool).

Pool is auto-derived at first tick: scan every `ItemObject` in `MBObjectManager`, group by `Culture.StringId`, fall through to an ID-prefix table for the ~50% of LOTRLOME shields that lack the culture attribute. Optional [`culture_marketplace_config.xml`](Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml) exposes per-culture blacklist + weight-boost overrides; every weight is `FiniteFloatValidator`-guarded and reverts NaN / Infinity / negative / >1000 values to 1.0 with a warning, per the project config-validation rule.

Architecture mirrors [`SpecialResources`](Main/Features/SpecialResources/) (Singleton ConfigProvider + Service + CampaignBehavior + IoC module). No Harmony patch — `Settlement.ItemRoster` is a public mutable property and `AddToCounts(EquipmentElement, int)` is the modifier-preserving overload per `.claude/rules/adapters.md`. No `SyncData` — injected items live in vanilla `Settlement.ItemRoster` which the engine already persists.

Files: [`CultureMarketplaceBehavior.cs`](Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs), [`CultureMarketplaceConfigProvider.cs`](Main/Features/CultureMarketplace/CultureMarketplaceConfigProvider.cs), [`CultureItemPoolService.cs`](Main/Features/CultureMarketplace/CultureItemPoolService.cs), [`CultureMarketplaceInjectionService.cs`](Main/Features/CultureMarketplace/CultureMarketplaceInjectionService.cs), [`CultureMarketplaceIoC.cs`](Main/Features/CultureMarketplace/CultureMarketplaceIoC.cs), 6 domain types under [`Main/Features/CultureMarketplace/Domain/`](Main/Features/CultureMarketplace/Domain/), adapters [`IItemPoolAdapter`](Main/Adapters/IItemPoolAdapter.cs) + [`ItemPoolAdapter`](Main/Adapters/ItemPoolAdapter.cs) and [`ITownRosterAdapter`](Main/Adapters/ITownRosterAdapter.cs) + [`TownRosterAdapter`](Main/Adapters/TownRosterAdapter.cs), [`culture_marketplace_config.xml`](Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml) stub, IoC + SubModule wiring, [`docs/features/culture-marketplace.md`](docs/features/culture-marketplace.md), 31 new tests (12 ConfigProvider + 9 ItemPoolService + 10 InjectionService) across [`TAOM.Tests/Features/CultureMarketplace/`](TAOM.Tests/Features/CultureMarketplace/). Full test suite 2254/2254 green.
Save-compat: no SyncData. Injected items use vanilla roster serialization. Safe to load existing saves.
Research: `taom-src` ItemObject (Culture / ItemType / IsTradeGood at lines 150/220/244), Settlement (ItemRoster at 323, OwnerClan at 497), PartyBase (ItemRoster at 135), TownMarketData.UpdateStores, VillageGoodProductionCampaignBehavior.TickGoodProduction confirmed as the only vanilla restock path.
Not-tested: end-to-end in-game verification (live boot needs visual confirmation that Minas Tirith shows Gondor items and that conquest shifts the pool). Unit-tested logic covers pool grouping, prefix fallback, weight bias, cap enforcement, NaN/Infinity weight rejection.

### fix(career-system): review-stage 3D preview now matches career selection (#206)

The career-menu live preview (shipped in `0048c33`) updated the menu's `NarrativeMenuCharacter` but not `Hero.MainHero.Equipment`. The downstream [`CharacterCreationReviewStageView`](file:///E:/Decompiled_Bannerlord/Modules/SandBox.GauntletUI/SandBox.GauntletUI.CharacterCreation/CharacterCreationReviewStageView.cs) builds its 3D agent from `Hero.MainHero.CharacterObject.Equipment` directly — so the review stage was still showing the youth/culture-default outfit even after the player picked a career, even though the career menu itself looked correct.

[`CareerMenuService.UpdateCareerEquipmentPreview`](Main/Features/CharacterCreation/CareerMenuService.cs) now runs the same two-step apply chain that `OnCharacterCreationFinalize` does — `IPlayerEquipmentService.ApplyPlayerStartingEquipment` (resets to culture+title default) → `ICareerStartingEquipmentService.ApplyCareerStartingEquipment` (overlays career roster). Two consequences:

1. Review stage 3D agent now matches the career-menu preview and the actual spawn equipment.
2. Switching careers mid-CC (cavalry → ranged → infantry) starts from a clean culture-default slate each click rather than inheriting the previous career's overrides — important because `Equipment.FillFrom` is a slot-merge, not a wholesale replace.

Files: [CareerMenuService.cs](Main/Features/CharacterCreation/CareerMenuService.cs) (+2 ctor deps, expanded UpdateCareerEquipmentPreview), [CareerMenuServiceTests.cs](TAOM.Tests/Features/CharacterCreation/CareerMenuServiceTests.cs) (ctor update + null-manager assertions extended to the new services), [docs/features/career-system.md](docs/features/career-system.md) (Live Preview section rewritten to explain WHY two updates are needed — menu char buffer vs `Hero.MainHero.Equipment` for review stage).
Save-compat: no schema changes. The runtime grant at finalize is unchanged; this fix only affects what the player sees during CC.
Research: ilspycmd against `TaleWorlds.CampaignSystem.dll` v1.3.15 confirmed `CharacterCreationContent.TryGetEquipmentToUse` is the vanilla equipment-id resolution chain; decompiled `CharacterCreationReviewStageView.AddCharacterEntity` confirmed it reads from `Hero.MainHero.CharacterObject.Equipment` (not from a menu character).

### feat(troop-progression): Lond Cirion + Caras Tolfalas pools + 2 new clan registrations

Second-round Gondor (EW) volunteer tuning in [`VolunteerRecruitmentService.cs`](Main/Features/TroopProgression/VolunteerRecruitmentService.cs):

- **`town_EW6` (Lond Cirion / Anfalas coast)** — pool retuned to `anf_levy 7, anf_guardsman 3` (was `bel_recruit 7, da_noble 3`). Lond Cirion is the oldest Númenórean settlement, geographically Anfalas, owned by clan_empire_west_14 (House of Baranionath).
- **`castle_EW9` (Caras Tolfalas)** — flipped to `tol_arbalest 7, bel_recruit 3` (was `bel_recruit 7, tol_arbalest 3`). Tolfalas crossbowmen are now the regular at their namesake island settlement; Belfalas mainland recruits remain as filler.
- **`clan_empire_west_8` (House of Olindurionath, owns Serelond / "Seregond")** — replaced single-troop `har_conscript 8/2` pool (Harondor mismatch) with a 4-troop Anfalas + Seregond mix: `anf_levy 5, ser_pikeman 2, ser_noble 2, anf_guardsman 1` (total 10). Captures both the clan's Anfalas geography and its rule over Serelond.
- **`clan_empire_west_13` (House of Hirilionath) registered for the first time** with `tol_arbalest 7, bel_recruit 3`. Clan 13 owns `castle_EW9` (Caras Tolfalas), so its lord-party pool now matches the settlement-level pool.
- **`clan_empire_west_14` (House of Baranionath) registered for the first time** with `anf_levy 7, anf_guardsman 3`. Clan 14 owns `town_EW6` (Lond Cirion), pool matches settlement.

Test coverage: extended `SpecificSettlements` (added `town_EW6`, updated `castle_EW9` expected troop) + `SpecificClans` (added clans 8 / 13 / 14) DataRows. New parameterized boundary-roll DataRow for clan 8's 4-troop pool (verifies the 5/2/2/1 split across rolls 0–9).

### refactor(troop-progression): volunteer pools accept arbitrary-length tuples

[`VolunteerRecruitmentService.AddSettlement`](Main/Features/TroopProgression/VolunteerRecruitmentService.cs) and `AddClan` migrated from the fixed 2-troop shape `(id, weight, id, weight)` to `params (string troopId, int weight)[]`. Every existing call site rewritten to the tuple shape — pure mechanical change, no behavior delta for any pool that did not actively need a 3+ entry table. New `internal static BuildPool` validates: rejects empty pools, non-positive weights, and blank troop ids — surfacing data errors at static-init time instead of as silent zero-probability rolls. Unblocks the next data round.

### feat(troop-progression): Gondor (EW) volunteer pool retune + 3 new clan registrations

End-to-end pass over Gondor's volunteer recruitment in [`VolunteerRecruitmentService.cs`](Main/Features/TroopProgression/VolunteerRecruitmentService.cs):

- **`town_EW1` (Minas Tirith)** + **`clan_empire_west_1`** — expanded from 2-troop to 4-troop pools: peasant 6 / ithilien_ranger 1 / fountain_guard 1 / trainee 2 (total 10). Notables at Minas Tirith now occasionally offer Fountain Guards + Citadel Trainees.
- **`town_EW2` / `town_EW3` (West / East Osgiliath)** — flipped: `osg_veteran 6, ano_peasant 4` (was `peasant 7, osg_veteran 3`). Osgiliath veterans are now the regular recruit at their namesake settlements.
- **`clan_empire_west_9`** — regular changed to `gondor_brv_bowman 7, gondor_ano_peasant 3` (was `anf_levy 7, brv_bowman 3`).
- **`clan_empire_west_10` / `_11` / `_12` registered for the first time** — these clans exist in `clans.xml` (Houses of Hýarthulionath / Caladionath / Garvirionath) but were not in the volunteer service. Pools: clan 10 `har_conscript 7, met_noble 3`; clan 11 `ca_noble 9, ithilien_ranger 1`; clan 12 `lin_noble 7, ano_peasant 3`. Lord parties from these clans will now offer the right culture/region recruits.
- **Geography audit — 8 high-confidence + 5 medium-confidence settlement swaps.** Settlements whose name was the troop-prefix namesake but whose pool used an unrelated-region troop as regular were corrected: Pelargir → `pel`, Dol Amroth → `da`, Calembel → `cal`, Serelond → `ser`, Methir → `met`, Cair Andros → `ca`, Linhir → `lin` (Lossarnach was 200+km away), Edhellond → Belfalas. Medium-confidence settlements aligned with owner clan: castle_EW8 Hyarpëndë → Pinnath Gelin/Arndir; castle_EW10/EW15/EW16 → clan 10 (Methir) `har_conscript`/`met_noble`; castle_EW11 → clan 2 (Dol Amroth) `bel_recruit`/`da_noble`.

Test coverage: parameterized boundary-roll DataRows for town_EW1, town_EW2/EW3, castle_EW15/EW16, and clan_empire_west_1/_11. Extended `SpecificClans` + `SpecificSettlements` DataRows. 4 new `BuildPool` validation tests cover empty pools, non-positive weights, and blank troop ids.

## 2026-05-19

### feat(career-system): archetype-driven starting equipment at character creation

After the existing culture-default roster is applied at `OnCharacterCreationFinalize`, the player's career archetype (`Infantry` / `Ranged` / `Cavalry`) drives an override loadout — bow + arrows + sword for ranged, spear + shield + sword + horse + harness for cavalry, 1H + shield + (2H or spear, culture-decides) for infantry, with archetype-appropriate light/medium/heavy armor weight.

New [`CareerArchetype`](Main/Features/CareerSystem/Domain/CareerArchetype.cs) enum + [`ICareerArchetypeService`](Main/Features/CareerSystem/CareerArchetypeService.cs) source from a single static dictionary in [`CareerSystemIoC.GetCareerArchetypeMap()`](Main/Features/CareerSystem/CareerSystemIoC.cs) — the same table the ability executor registry consumes (Infantry/Ranged/Cavalry executors), eliminating duplication. New [`ICareerStartingEquipmentService`](Main/Features/CharacterCreation/CareerStartingEquipmentService.cs) reuses the proven `IPlayerEquipmentAdapter.ApplyRosterToPlayer` path: lookup archetype → build roster ID `player_career_{culture}_{archetype}_{f|m}` → `FillFrom` onto `Hero.BattleEquipment`. Missing rosters fall through gracefully — the culture-default applied just before stays in place.

Authored Gondor end-to-end as proof of life: 15 new `starter_*_gondor_*` armor items in [LOTRLOME_Armory](file:///E:/Steam/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/starter_armors.xml) (light/medium/heavy variants reusing existing meshes; no troop regressions — only new `starter_` IDs); 6 new player rosters in [taom_career_starting_equipment.xml](Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml) (Gondor × 3 archetypes × 2 genders); reuses existing low-tier Gondor weapons (`wm_gondor_bow`, `wm_gondor_sword_a01`, `wm_gondor_spear_a`, `wm_gondor_shield_a02`, `bodkin_arrows_a`) + vanilla `charger` + `chain_horse_harness` for cavalry. Other 15 cultures fall through to culture-default until their rosters are authored.

In-session deep-review caught five issues before close-out; all addressed:
- **CRITICAL:** `starter_armors.xml` was initially written to a phantom `Mount &amp; Blade II Bannerlord` directory (Write tool entity-encoded `&` → `&amp;`); moved to the real game module path, phantom tree deleted. Player spawned naked in initial in-game test. Memory: `feedback_write_tool_ampersand_path_encoding.md`.
- **HIGH (latent):** `Equipment.FillFrom` is a slot-by-slot merge, not a wholesale replace; ranged/infantry career rosters were going to inherit a Horse from the culture-default. Added explicit `<Equipment slot="Horse" id=""/>` + `<Equipment slot="HorseHarness" id=""/>` overrides to the 4 non-cavalry rosters.
- **HIGH (visual):** leg + glove items missing `covers_legs="true"` / `covers_hands="true"` — without these the meshes don't render even when the items are equipped. Added to all 6 leg/glove items. Memory: `feedback_lotrlome_armor_cover_attributes.md`.
- **MEDIUM:** `GetCareerArchetypeMap()` allocated a fresh dictionary on each call (twice during IoC startup); cached in a `static readonly` field.
- **LOW:** redundant `?.` on constructor-injected `_careerMenuService` + misleading "menu service is authoritative" comment; both corrected.

Files: [CareerArchetype.cs](Main/Features/CareerSystem/Domain/CareerArchetype.cs), [ICareerArchetypeService.cs](Main/Features/CareerSystem/ICareerArchetypeService.cs), [CareerArchetypeService.cs](Main/Features/CareerSystem/CareerArchetypeService.cs), [CareerSystemIoC.cs](Main/Features/CareerSystem/CareerSystemIoC.cs) (refactor + static archetype map cache), [ICareerStartingEquipmentService.cs](Main/Features/CharacterCreation/ICareerStartingEquipmentService.cs), [CareerStartingEquipmentService.cs](Main/Features/CharacterCreation/CareerStartingEquipmentService.cs), [CareerEquipmentRosterIds.cs](Main/Features/CharacterCreation/CareerEquipmentRosterIds.cs), [CharacterCreationContentService.cs](Main/Features/CharacterCreation/CharacterCreationContentService.cs) (wire into `GrantPlayerStartupResources`), [CharacterCreationIoC.cs](Main/Features/CharacterCreation/CharacterCreationIoC.cs) (register), [SubModule.xml](Main/_Module/SubModule.xml) (register new EquipmentRosters XML), [taom_career_starting_equipment.xml](Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml) (Gondor rosters with explicit Horse clears on non-cavalry), [docs/features/career-system.md](docs/features/career-system.md) (new "Starting Equipment Override" section), [docs/reviews/rca-career-starting-equipment-2026-05-19.md](docs/reviews/rca-career-starting-equipment-2026-05-19.md), [CareerArchetypeServiceTests.cs](TAOM.Tests/Features/CareerSystem/CareerArchetypeServiceTests.cs) (+8 cases), [CareerStartingEquipmentServiceTests.cs](TAOM.Tests/Features/CharacterCreation/CareerStartingEquipmentServiceTests.cs) (+12 cases), [CharacterCreationContentServiceTests.cs](TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs) (ctor update).
Save-compat: no schema changes. Equipment is granted only at character creation; existing saves keep their current equipment.
Not-tested: in-game spawn verification of the second armor render pass after `covers_legs` / `covers_hands` fix (initial test surfaced the Critical phantom-path bug; covers-* fix not yet revisited in-game).

### fix(diplomacy): block war between same-alignment kingdoms (#203)

In-game notifications showed *Bard II calling Erebor to war against Mirkwood* and *Thranduil declaring war on Dale* — all three are tagged `"free"` in [`alignment.json`](Main/_Module/ModuleData/execution/alignment.json) and should not be able to war each other. The two existing war-block gates ([`TaomKingdomDecisionPermissionModel.IsWarDecisionAllowedBetweenKingdoms`](Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs) + the [`DeclareWarAction.ApplyInternal` Harmony Prefix](Main/Features/Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs)) only blocked when the per-pair tier in `diplomacy.json` was `Permanent`. Permanent was reserved for canonical lore alliances (Gondor-Rohan, Erebor-Dale, the Elven trio); other free-vs-free pairs were at `Natural` or missing entirely (`mirkwood↔sturgia` defaulted to `Neutral`).

Added [`IDiplomacyService.IsWarAllowed(a, b)`](Main/Features/Diplomacy/IDiplomacyService.cs) composing the existing `Permanent`-tier check with `TAOM.Features.Execution.IAlignmentService.AreSameAlignment` (reused as-is from the Execution feature — no file moves). Routed both gates through it. `AreSameAlignment` returns `false` whenever either side is `Neutral`, so Umbar/Khand-equivalents stay free to war each other. Neither `diplomacy.json` nor `alignment.json` was modified — the gate is policy, the JSON files keep their separate semantics (tier still drives the alliance-score modifier).

The Harmony Prefix on `DeclareWarAction.ApplyInternal` covers all 8 public `ApplyBy*` entry points including `ApplyByCallToWarAgreement`, so the call-to-war path ("Bard II calls Erebor") is gated at execution time too — confirmed via decompile of `DeclareWarAction` against installed v1.3.15 DLL.

No save-load healer per user direction — existing wars in current saves persist (AI will sue for peace naturally); new war decisions are blocked going forward.

Files: [DiplomacyService.cs](Main/Features/Diplomacy/DiplomacyService.cs), [IDiplomacyService.cs](Main/Features/Diplomacy/IDiplomacyService.cs), [TaomKingdomDecisionPermissionModel.cs](Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs), [AllianceActionHook.cs](Main/Features/Diplomacy/Hooks/AllianceActionHook.cs), [DiplomacyServiceTests.cs](TAOM.Tests/Features/Diplomacy/DiplomacyServiceTests.cs) (+7 cases), [AllianceActionHookTests.cs](TAOM.Tests/Features/Diplomacy/AllianceActionHookTests.cs) (3 updated).
Research: ilspycmd verified `DefaultKingdomDecisionPermissionModel.IsWarDecisionAllowedBetweenKingdoms` signature + `DeclareWarAction.ApplyInternal` private-static entry-point uniqueness against installed v1.3.15 DLL.
Save-compat: no schema changes. Existing wars persist; new decisions blocked.
Not-tested: Harmony entry-point invocation (`/deep-review` 5/5 PASS — Standards, Compatibility, Efficiency, Completeness, Data-Flow agents all green; in-game verification pending). RCA at [docs/reviews/rca-diplomacy-same-alignment-war-block-2026-05-19.md](docs/reviews/rca-diplomacy-same-alignment-war-block-2026-05-19.md).

### fix(career-system): remove orphan `career_menu.json` entries for disabled WIP careers

`taom_careers.xml` disabled the `far_harad_halftroll` and `cave_troll_master` careers on 2026-05-14 by wrapping them in XML comments (`<!-- DISABLED ... WIP; not ready for live game yet. Re-enable by uncommenting. -->`), but the matching entries in [career_menu.json](Main/_Module/ModuleData/charactercreation/career_menu.json) were left in place — JSON has no comment syntax, so they remained active config. The `CareerCultureCoverageTests.EveryJsonEntry_HasMatchingCareerInXml` cross-reference test correctly caught the mismatch and had been failing for 5 days. Removed both JSON entries; full test suite now 2147/2147 (was 2146 + 1 pre-existing fail). When the careers are re-enabled in XML, the JSON entries need to be re-added alongside (the test will catch the inverse case too).

### chore(harness): codify Gauntlet-overlay input-wiring rule in scoped GUI rule + `/deep-review`

Adds "Custom GauntletLayer Input Wiring (MANDATORY)" section to [.claude/rules/gui-ui.md](.claude/rules/gui-ui.md) and a tenth check to the `/deep-review` Standards agent prompt in [.claude/skills/deep-review/SKILL.md](.claude/skills/deep-review/SKILL.md). The rule fires whenever a new `GauntletLayer` is instantiated in a Harmony postfix on `<ScreenBase>.OnInitialize`: the layer MUST call `_layer.InputRestrictions.SetInputRestrictions()` after construction (paired with `ResetInputRestrictions()` in teardown) or the layer paints but the input dispatcher never sees clicks. The follow-up to the EquipPresets Presets-button RCA (`docs/reviews/rca-equippresets-presets-button-silent-2026-05-19.md`); the bug shipped past 2 prior reviews because TAOM had no prior `ScreenBase` overlay to compare against. Distinct from `IsFocusLayer = true`, which is the right choice ONLY for full-screen replacements (`GauntletCareerScreen`, `GauntletFiefManagementScreen`) and the wrong choice for parasitic overlays that need vanilla to keep hotkey focus.

### fix(equip-presets): wire input restrictions on overlay layer — Presets button was a silent no-op

The "Presets" button rendered in the inventory overlay (`Patch33_GauntletInventoryScreen`) but clicking it did nothing — no dialog, no message, no error. Root cause: the custom `GauntletLayer` at z-order 1000 was added via `__instance.AddLayer(_layer)` without calling `_layer.InputRestrictions.SetInputRestrictions()`. Without that call the layer paints but never registers with the screen's input dispatcher, so mouse events pass through the overlay to the vanilla inventory beneath. Every other TAOM-authored Gauntlet layer that needs mouse input on a `ScreenBase` (`GauntletFiefManagementScreen`, `GauntletCareerScreen`) calls `SetInputRestrictions()`; EquipPresets was the only one that didn't, which is why neither `/deep-review` nor Codex review #28 caught it — both focused on the service layer + prefab structure, not layer input wiring (no inventory-overlay precedent in the project to compare against).

Deliberately did NOT set `IsFocusLayer = true` — that would steal Esc / Tab / hotkey focus from the live inventory underneath. Parent widget in `PresetsOverlay.xml` is `DoNotAcceptEvents="true"` so non-button areas still pass clicks through to vanilla. Paired the `SetInputRestrictions()` call in `OnInitialize_Postfix` with `ResetInputRestrictions()` in `OnFinalize_Prefix`, matching the `GauntletCareerScreen` teardown pattern (caught by `/deep-review` data-flow agent on the followup pass).

RCA at `docs/reviews/rca-equippresets-presets-button-silent-2026-05-19.md` documents why this slipped past three review layers and adds a `feedback_gauntlet_overlay_input_wiring.md` memory entry plus a `/deep-review` GUI-checklist follow-up.

Files: [Main/Features/EquipPresets/Hooks/Patch33_GauntletInventoryScreen.cs](Main/Features/EquipPresets/Hooks/Patch33_GauntletInventoryScreen.cs).
Research: ilspycmd verified `GauntletLayer.InputRestrictions` + `SetInputRestrictions(bool, InputUsageMask)` (both defaults supplied) against installed v1.3.15 DLL.
Not-tested: Harmony entry-point — verified live in-game (button now opens the Save/Load/Update/Delete multi-selection inquiry).

### feat(troops): upgrade Ithilien Ranger quivers to Noldar Elven Arrows (best in Armory)

Replaced vanilla `bodkin_arrows_a` (3 thrust_damage, 32 stack) in all 8 `gondor_ithilien_ranger` rosters with [Noldar Elven Arrow](https://example) variants from LOTRLOME_Armory (`wm_elven_arrow_v*_*` — 5 thrust_damage, 50 stack). Audited every `Type="Arrows"` item in the Armory: Noldar Elven Arrows have the highest damage tier (tied with Mirkwood at 5) AND the largest quiver (50 vs Mirkwood 40, vs Isengard 40, vs Erebor 30) — clear "best" winner. Isengard/Erebor arrows at 3 damage are worse than vanilla. No Gondor-prefixed arrow exists in the Armory; the Noldar series is the closest lore-appropriate upgrade (Lothlórien gifted Ithilien Rangers their cloaks per book lore — elven ammunition fits the Galadhrim-adjacent theme).

Each of the 8 rosters now uses a unique Noldar visual variant (`v1_a`, `v2_a`, `v3_a`, `v4_a`, `v1_b`, `v1_c`, `v1_d`, `v2_b`) — all stat-identical, mesh-distinct. Sidearm remains `wm_gondor_sword_a10` (no Ithilien sword in the Armory; Gondor swords are the natural fit per user direction).

Save-compat: per-element replacement, no troop IDs touched. Existing recruits keep their already-spawned equipment; new recruits draw the upgraded quivers.

### refactor(troops): drop Ithil Guard plate rosters from `gondor_ithilien_ranger` (12 → 8)

Removed `EquipmentRoster` blocks 9–12 from [troops_gondor.xml](Main/_Module/ModuleData/troops/troops_gondor.xml). Those rosters used `sk_gd_ith_chest_noble_*` (Ithil Guard plate), `sk_gd_ith_noble_helmet_heavy_*` (Ithil Guard helmets), and `wm_gondor_shield_*_minas_ithil` (Minas Ithil shields) — items whose display names read "Ithil Guard" / "Minas Ithil," not "Ithilien." Visually they made rangers look like heavily-armored garrison guards rather than light skirmisher rangers. Rangers should only wear items literally branded "Ithilien."

Remaining 8 rosters all use the ranger-leather wardrobe: `ithilien_jerkin_{long,long_slim,long_var,long_var_slim,short,short_slim,short_var,short_var_slim}` bodies, `ithilien_hood{,_var,_masked,_masked_var}` heads, `ithilien_boots{,_heavy}` legs, `ithilien_cloak{,_var}` capes, `ithilien_bracers` gloves, `wm_ithilien_bow{,_b,_c}` bows, `bodkin_arrows_a` ×2 quivers, `wm_gondor_sword_a10` sidearm (no Ithilien sword exists in the Armory).

Save-compat: pure subtraction — no troop IDs changed, only rosters dropped. Existing recruits keep their already-spawned equipment; new recruits draw from the remaining 8 rosters.

Build verified via `dotnet build -t:CoreCompile` (0 errors). Post-build copy step fails because game is running (BehaviorTrees.dll locked) — environment issue per `.claude/rules/environment-failures.md`, not a code regression.

## 2026-05-18

### feat(tooling): add `tools/Audit-MeshRefs.ps1` — diff every Armory XML `mesh=` ref against every `.tpac` mesh, find orphans in one pass

New PowerShell CLI at [tools/Audit-MeshRefs.ps1](tools/Audit-MeshRefs.ps1). Loads `TpacTool.Lib.dll` (`E:\Release_v0.5.1\bin\`) via `Add-Type`, recursively parses every `.tpac` under a module's `Assets/`, recursively scans every `.xml` under `ModuleData/` for `mesh="X"` attributes, and emits a `REPORT.md` plus three sorted text files under `tools/reports/mesh-audit/<module>/`. Orphan items (refs with no mesh) are the cleanup list; orphan assets (meshes nothing references) are informational. First LOTRLOME_Armory run: 4235 `.tpac` parsed, 0 failures, 3829 unique meshes, 3287 XML refs, **20 orphan items**. Replaces the unsafe "the screenshot is exhaustive" workflow that almost flagged `wm_pelargir_shield_a/b` as orphans in an earlier conversation. Report output is `.gitignore`'d (`tools/reports/`).

### fix(armory): clean 20 orphan mesh refs across LOTRLOME_Armory + TAOM cross-refs

Each row is a `mesh="X"` reference whose `X` does not exist in any `.tpac`. Fix path varies by category:

| Category | Fix | Files touched |
|---|---|---|
| `gond_shld2`, `gond_shld2_lrg` (Gondor shields) | Deleted Item rows from Armory. Retargeted 9 TAOM refs to `Item.wm_gondor_shield_a02` (8 in `taom_char_creation_equipment.xml`, 1 in `lords.xml` for Boromir-like lord at line 8620). | `LOTRAOM_shields.xml` (Armory), `taom_char_creation_equipment.xml`, `lords.xml` |
| `m_northern_armor_{a1,a2,a3,b1,b2,b3,b4}` + `m_northern_cape_a` (Northern mercenary) | Item IDs preserved (no consumers but defensive). `mesh=` retargeted by tier: `a1/2/3` → `sk_northern_armor_light_{a,a_slim,b}`, `b1/2/3/4` → `sk_northern_armor_medium_{a,a_slim,b,b_slim}`, cape → `clo_sk_northern_pauldron_cape_c`. | `mercenary/body_armors.xml`, `mercenary/shoulder_armors.xml` |
| `sk_dwarf_erebor_pauldron_scale_cape_{a,b}_{blue,green,red}` (6 Erebor) | Deleted via PowerShell regex (Edit-tool string-match failed on CRLF line endings). Zero consumers in TAOM or Armory. | `erebor/shoulder_armors.xml` |
| `dunland_caerdh_pauldron__elite_a` (double-underscore typo) | Renamed id + mesh + name-key to single underscore in Armory item XML; updated 13 `loc_dunland.xml` language files; updated 5 TAOM consumer refs (4 in `taom_equipment_sets_dunland.xml`, 1 in `troops_dunland.xml`). New name resolves to `dunland_caerdh_pauldron_elite_a` mesh which exists in `.tpac`. Save-compat note: existing characters wearing the typo'd item lose it on load (acceptable per user instruction to "fix the typo"). | Armory `dunland/shoulder_armors.xml` + 13 language files + 2 TAOM files |
| `wm_swan_knight_spear_{,gondor_,pg_}banner` (3 crafting pieces) | Updated `mesh="..."` to `_flag` suffix variants which exist in `.tpac`. CraftingPiece ids unchanged (would break 9 `<AvailablePiece id="...">` refs in `weapon_descriptions.xslt`). | `LOTRLOME_crafting_pieces.xml` |

Final audit confirms **0 orphan items** in LOTRLOME_Armory.

### feat(troops): add standalone T9 `gondor_ithilien_ranger` with 12 Ithilien-themed equipment rosters

New troop `gondor_ithilien_ranger` (level 41, `is_basic_troop="true"`, default_group=`Ranged`, Bow=280) appended after `gondor_brv_ranger` in [Main/_Module/ModuleData/troops/troops_gondor.xml](Main/_Module/ModuleData/troops/troops_gondor.xml). Standalone — not reachable via BRV upgrade chain (no existing troop upgrades into it; `<upgrade_targets />` empty). Spawned units randomize across 12 `<EquipmentRoster>` blocks built from every Ithilien-prefixed item in `LOTRLOME_Armory`:

| Roster bucket | Bodies (12) | Helmets (6) | Cloaks (2) | Boots (2) | Bows (3) | Shields (2) |
|---|---|---|---|---|---|---|
| Rosters 1–8 (ranger leather) | `ithilien_jerkin_long{,_slim,_var,_var_slim}` + `ithilien_jerkin_short{,_slim,_var,_var_slim}` | `ithilien_hood{,_var,_masked,_masked_var}` | both | both | `wm_ithilien_bow{,_b,_c}` | none (twin quiver) |
| Rosters 9–12 (Ithil Guard plate) | `sk_gd_ith_chest_noble_{med,heavy}_{a,b}` | `sk_gd_ith_noble_helmet_heavy_{a,b}` | both | heavy only | `wm_ithilien_bow_{b,c}` | `wm_gondor_shield_{a,d_new}_minas_ithil` |

Gloves slot is `ithilien_bracers` across all rosters; sidearm is `wm_gondor_sword_a10` (matches `gondor_brv_shadowbow`'s T9 sword tier). The starter bow `wm_ithilien_bow_starter` is excluded — too weak for T9. Every Ithilien item in the Armory appears in at least one roster; cross-verified via `Grep "id=\"(ithilien_|sk_gd_ith_|wm_ithilien_|...minas_ithil)"` against all relevant Armory XMLs before writing.

Recruitable directly from notables in Minas Tirith (`town_EW1`) and the two Ithilien-area castles Amonost (`castle_EW15`) and Erethir (`castle_EW16`) via [VolunteerRecruitmentService.cs](Main/Features/TroopProgression/VolunteerRecruitmentService.cs). Replaces the existing notable-slot offering for those three settlements (previously `gondor_mt_trainee` for Minas Tirith, `gondor_ith_watcher` for the two castles) — significant power jump but matches the user's intent to make Ithilien Rangers a regional-specialty elite recruitable straight from the notable pool. Regular-troop slot (`gondor_ano_peasant`, weight 7) is unchanged.

Tests: 3 new `_HighRoll_ReturnsIthilienRanger` cases in [VolunteerRecruitmentServiceTests.cs](TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs) cover all three settlements via weighted-random boundary roll (rolls 7 with total weight 10). Existing `_WeightedRandom_HighRoll_ReturnsNobleTroop` test repurposed to assert the new Minas Tirith notable. Full suite: 2143 passed, 1 unrelated pre-existing failure (`EveryJsonEntry_HasMatchingCareerInXml` — orphaned `far_harad_halftroll`/`cave_troll_master` in `career_menu.json`, confirmed failing on the pre-change tree).

Save-compat: fully additive. New troop ID added; no existing IDs renamed or removed. The 3 settlement entries change their notable-slot reference, but the old troop IDs (`gondor_mt_trainee`, `gondor_ith_watcher`) remain defined in `troops_gondor.xml` and remain reachable via clan-map fallback (`clan_empire_west_1` still references `gondor_mt_trainee` as its notable). Existing saves load without troop drops.

Research: cross-referenced 25 Armory item IDs against five `LOTRLOME_Armory\ModuleData\LOTRLOME_items\gondor\*.xml` files plus `LOTRAOM_weapons.xml` and `LOTRAOM_shields.xml`. Verified `gondor_brv_shadowbow` (existing T9 endpoint) was untouched so the BRV ranger line still has its terminal upgrade.

Deep-review: 5 agents (standards/compat/efficiency/completeness/data-flow). Data-flow agent flagged `MaxVolunteerTier=6` as a CRITICAL tier filter; verified false via `ilspycmd` on `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement` — the tier check only gates upgrade progression on slots with non-empty `UpgradeTargets`, and the new troop has empty `<upgrade_targets />`. Disagreement memorialized in `feedback_codex_caught_api_misread.md`-style decompilation cross-check. No HIGH findings remain.

### feat(tooling): add `taom-src` CLI + SKILL — one-command TaleWorlds v1.3.15 decompile cache (inspired by vercel-labs/opensrc)

Reviewed [vercel-labs/opensrc](https://github.com/vercel-labs/opensrc) for transferable patterns. Most of its surface (npm/PyPI/crates registry resolution, shallow git clone, npm distribution, Turborepo monorepo) doesn't apply — TAOM's "dependency" is a closed-source local game install. But the central idea — a single composable command that returns an absolute path to cached source — maps directly to TAOM's recurring TaleWorlds-signature-lookup ritual. Full review at `~/.claude/plans/review-https-github-com-vercel-labs-open-deep-brooks.md`.

- **`tools/taom-src.ps1`** wraps `ilspycmd` against `$env:BANNERLORD_GAME_DIR\bin\Win64_Shipping_Client\*.dll` with a three-tier DLL resolver (index cache → namespace heuristic → brute-force iteration). Caches at `$env:USERPROFILE\.taom-src\v1.3.15\<FullyQualifiedType>.cs`. Maintains `sources.json` (cached-type registry) + `dll-index.json` (type→dll fast-path). Subcommands: `path`, `list`, `remove`, `clean`. Progress to stderr, path to stdout, so `$(pwsh tools/taom-src.ps1 path X)` composes cleanly with `rg`/`cat`/`grep`. Detects ilspycmd hits via positive C# token match on the first non-empty line (`using `/`namespace `/`[`/modifier/keyword/`//`/`#`) — necessary because ilspycmd always exits 0 and emits raw exception text on miss, including the `MetadataFileNotSupportedException` thrown for the native AMD-GPU DLL in the bin folder that would otherwise false-positive substring matchers. Errors clean per `.claude/rules/environment-failures.md` (missing `BANNERLORD_GAME_DIR`, missing `ilspycmd` on PATH, type not found in any DLL).
- **`.claude/skills/taom-src/SKILL.md`** teaches the verbs in 80 lines. Description 27 words (within the 30-word eager-load cap from `.claude/rules/harness-facts.md`). References `feedback_codex_caught_api_misread.md` as the canonical "why this matters" anecdote (the 2026-05-06 Codex review that caught Claude's API agent confidently reading the v1.4 dump and shipping a P1).
- **Smoke-tested:** cache miss decompiles in ~3s (probes `TaleWorlds.CampaignSystem.dll` first via namespace heuristic, hits on first try for `DefaultPartyWageModel`). Cache hit returns in ~0.5s (pwsh cold-start dominates). Missing-type case correctly iterates all `TaleWorlds.*.dll`s in alphabetical order, then throws and exits 1. Generic types (`TaleWorlds.Library.MBList\`1`) cache and resolve correctly. Explicit `-Dll` override skips the heuristic. Missing-env-var path produces an actionable error pointing at the env var.
- **What's NOT included from opensrc:** multi-registry support (no PyPI/crates/GitHub equivalents for TAOM), lockfile version detection (TAOM is branch-pinned), shallow git clone (wrong primitive — we need decompile), Rust as implementation language (PowerShell wrapper is ~270 lines, no native-binary overhead justified), the npm distribution model, Turborepo workspace structure, the Next.js docs site, and the CI release-PR-bump-version flow. The CHANGELOG `<!-- release:start -->` markers and AGENTS.md injection-block pattern were noted in the review file as interesting but with no current trigger.

## 2026-05-14

### fix(battle): restore `BehaviorType.Other` on three ported MissionBehaviors (root-cause NRE in `Mission.CheckMissionEnded`)

Field battles NRE'd at t≈10s in vanilla `TaleWorlds.MountAndBlade.Mission.CheckMissionEnded()` (v1.3.15, Mission.cs:4701). RCA via live VS debugger: three TAOM `MissionBehavior` subclasses had `BehaviorType => MissionBehaviorType.Logic` despite **not** inheriting from `MissionLogic`. Vanilla `AddMissionBehavior` does `MissionLogics.Add(missionBehavior as MissionLogic)` — the `as` cast returns null when the class isn't a MissionLogic, leaving 3 null slots in `Mission.MissionLogics`. Vanilla `CheckMissionEnded` then unconditionally calls `missionLogic.MissionEnded(ref ...)` and NREs every 10s.

Decompiling the original DLLs (`~/Downloads/Features_fixed/`) showed every one declared `BehaviorType => (MissionBehaviorType)1` (= `Other`). The bug was introduced during the 2026-05-07 ports (`eed8e7b`, `7ddd6bb`, `bc15949`) — `Logic` was cargo-culted into the port without verifying inheritance. Restored to `Other` to match the working originals.

| File | Was | Now |
|---|---|---|
| `Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs:24` | `Logic` | `Other` |
| `Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs:36` | `Logic` | `Other` |
| `Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs:19` | `Logic` | `Other` |

None of the three override any `MissionLogic`-only virtual (`MissionEnded`, `OnMissionResultReady`, `OnEndMissionRequest`, `OnBattleEnded`, `OnRetreatMission`, `OnSurrenderMission`, `GetExtraEquipmentElementsForCharacter`, `ShowBattleResults`), so routing through `_otherMissionBehaviors` instead of `MissionLogics` changes no observable behavior beyond eliminating the null.

**Prevention:** Memory entry `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance.md` added to the project memory directory. Documents the v1.3.15 vanilla NRE path, the `Logic=0/Other=1` enum mapping (which the original ports preserved correctly via `(MissionBehaviorType)1`), and the rule that `BehaviorType => Logic` requires actually inheriting from `MissionLogic`. Future ports should decompile the original DLL with `ilspycmd` and copy the `BehaviorType` getter literally rather than inferring from class semantics.

A diagnostic Patch37 with `[MissionTrace]` lifecycle logging + null-scrub was built during RCA and removed once root cause was identified — defense-in-depth was no longer justified per the simplicity criterion (`/.claude/rules/simplicity-criterion.md`).

Save-compat: BehaviorType change reroutes the three behaviors from `MissionLogics` into `_otherMissionBehaviors`. No save-file fields touched.
Not-tested: live Harmony patch wiring (requires game runtime); manual battle-load smoke required before close-out.

### Phase 9b — CareerSystem agent-stat service extraction (closes deferred #142)

Audit issue #142 had 5 P2 dispositions; two service-locator issues were resolved by #173. The three remaining were inline-business-logic rule-4 violations in `TaomAgentStatCalculateModel.UpdateAgentStats` (55-line override body with `CareerAbilityBuffTracker` integration + stat-mutation logic all inline), `TaomAgentApplyDamageModel`'s three overrides (mixed early-exit + nested-guard pattern with inline `if` chains), and unreachable defensive null guards on `_passiveService` across 5 models (the service is resolved unconditionally at the SubModule registration site — DryIoc throws if missing).

- **`ICareerAgentStatService` extracted.** Four methods cover the union of the two models: `ApplyAgentStatModifiers(string? heroId, int agentIndex, bool isHuman, bool isHero, AgentDrivenProperties props)` for `UpdateAgentStats`; `CalculateDamageAmplification(string? attackerHeroId, float baseResult)` for the attacker armor-pen passive; `CalculateDamageReduction(string? victimHeroId, int? victimAgentIndex, float baseResult)` for victim resistance + hero self-buff + AoE ally-buff stacking; `ShouldShrugOffBlow(string? victimHeroId)` for the victim ShruggedOff passive. Per ADR-007 every method accepts primitive `string?` heroId / `int` agentIndex — boundary models extract `(agent.Character as CharacterObject)?.HeroObject?.StringId` and `agent.Index` at the call site. `AgentDrivenProperties` is a TaleWorlds reference type (verified against `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentDrivenProperties.cs:1`) — passed by-value, mutations flow back to the caller; no `ref` needed.
- **Pragmatic acceptance of `CareerAbilityBuffTracker` static state.** The service reads the static tracker directly rather than wrapping it behind an adapter. The tracker is a TAOM-owned `Dictionary<string, ActiveBuffs>` (not a sealed TaleWorlds type), and the brief's pragmatic acceptance carve-out applies: the goal of this extraction is to get LOGIC out of the model bodies, not to eliminate the static tracker — that's a separate refactor and out of scope. Tests reset the static state in `TestInitialize` + `TestCleanup` so they exercise the service in isolation.
- **Both model bodies now thin per rule 4.** `TaomAgentStatCalculateModel.UpdateAgentStats` reduces from 55 lines to 4 lines (extract heroId/agentIndex/isHuman/isHero, delegate). `GetEffectiveMaxHealth` also tightened to one-line extract-and-delegate. `TaomAgentApplyDamageModel`'s three overrides each become 2-3 lines (call base, delegate). Primitive-extractor helpers (`GetAttackerHeroId`, `GetVictimHeroId`, `GetVictimAgent`) kept private + static since they're pure null-safe routing with no business branching. No inline `if`/`foreach`/`switch` remains in any override body.
- **Unreachable defensive null-guards removed.** `if (_passiveService == null) return baseValue` removed from `TaomAgentStatCalculateModel.GetEffectiveMaxHealth`, `TaomClanTierModel.GetCompanionLimit`, and both `TaomAgentApplyDamageModel` damage paths. DryIoc resolves `ICareerPassiveService` unconditionally at the SubModule registration site (Main/SubModule.cs:334), so a null reference here would be a wiring bug, not a runtime state — the guard never fires in production and masks the actual failure mode. `TaomClanTierModel` cleanup also collapsed `var leader = clan?.Leader; if (leader == null) return baseLimit;` into a single `var leaderId = clan?.Leader?.StringId; if (leaderId == null) return baseLimit;` so the StringId extract happens at the boundary, not inside the service call.
- **`CareerSystemIoC.RegisterCareerSystemFeature`** registers `ICareerAgentStatService` as `Reuse.Singleton` (no per-call allocations on the per-frame `UpdateAgentStats` path). `Main/SubModule.cs:347-352` resolves once into a local `var careerAgentStat` and threads it into both model ctors — `TaomAgentStatCalculateModel(careerPassiveService, careerAgentStat)` and `TaomAgentApplyDamageModel(careerAgentStat)`.
- **Tests:** `CareerAgentStatServiceTests` adds 22 tests covering: `ApplyAgentStatModifiers` (non-human skip, human-non-hero skip without ally buff, hero passive lookup × 3 effects, all 7 hero-self-buff fields, zero-mount-speed preservation, ally-buff all fields, hero+ally stacking, isHero-with-null-heroId-skips-hero-branch), `CalculateDamageAmplification` (null attacker, zero magnitude, non-zero), `CalculateDamageReduction` (no hero + no victim, hero resistance, hero+self-buff stacking, ally-buff alone, hero+ally on same index stacking, zero-resistance-and-zero-buffs), `ShouldShrugOffBlow` (null hero, zero, positive, negative). All NSubstitute-mocked `ICareerPassiveService`; the static `CareerAbilityBuffTracker` is set/cleared per test via the existing `SetBuff`/`SetAllyBuff`/`ClearAll` surface.
- **Test count delta:** `+22` (full suite 2122 → 2144 total; 2120 → 2141 passing). The single pre-existing failure `EveryJsonEntry_HasMatchingCareerInXml` is unrelated to this work — caused by a parallel session's `taom_careers.xml` edit that removed the troll careers but left their entries in `career_menu.json` (verified by stashing my changes — the test still fails). Not my domain to fix.
- **Closes deferred dispositions in #142** (3 of 5 remaining P2: `UpdateAgentStats` inline logic + `ApplyDamage` branching + unreachable null guards). The 2 service-locator dispositions were closed by #173. Issue stays open per orchestrator direction.

### Phase 9b — SpecialResources R1 reset + desertion grace + per-resource seed (closes deferred #133)

The three remaining P2 deferred dispositions from audit issue #133 — singleton-state leakage across campaigns, immediate desertion after load, and per-kingdom-change re-seeding. All three were "no immediate crash, just wrong behavior" bugs that compound for players who switch saves, change kingdoms, or save at a depleted balance.

- **`ISpecialResourceService.ResetSessionState()`** added + implemented. Clears the three singleton-scope fields that previously survived `OnNewGameCreated`: `_loggedResolveKeys` (would suppress first-resolve diagnostics in the new campaign), `_pendingSpend` (would silently debit the new hero's balance at the next CommitSession), `_inSession` (would let an orphan CommitSession from the prior campaign run). `SpecialResourcesBehavior.OnNewGameCreated` calls this BEFORE `InitializeHero` so the dedupe set is empty for the new campaign's first-resolve log line. Logging surfaces both prior values (`pending was X, inSession was Y`) so a player who reports "I think a refund got eaten" can grep the log for the exact transition.
- **`_isFirstTickAfterLoad` desertion grace** added to `SpecialResourcesBehavior`. Pre-fix: a player who saved at balance=0 and loaded back in lost 10% of each upkeep troop type on the very first daily tick, with no opportunity to earn (battle / raid / siege) between load and tick. Fix: a single-bool flag set true in `OnSessionLaunched` + `OnNewGameCreated`, cleared at the end of `OnDailyTickHero`. While the flag is true and balance<=0 with upkeep troops, the desertion branch is suppressed and a `[SpecRes] DailyTick: desertion grace active...` log line surfaces the skip so a player who's confused why no troops left can trace it. The grace is exactly one tick — the second daily tick after load applies desertion normally if the balance is still <= 0.
- **Per-resource legacy-seed gate** added in `OnSessionLaunched`. Pre-fix the seed branch was `if (current <= 0f && resource.StartingAmount > 0f)`, which fired EVERY time `OnSessionLaunched` saw a resolved-resource balance of 0. Two failure modes: (1) mid-session kingdom change Gondor→Mordor surfaces War Spoils as a "new" resource the hero has never owned (balance=0) — seeded as if it were a legacy save, bypassing earn-it progression. (2) player spends down to 0, saves, reloads — re-seeded back up to StartingAmount, effectively a refund. Fix: gate on `_storage.Contains(heroId, resource.Id)`, which is true iff the (hero, resource) pair has ever been written. Pre-existing saves (predating SpecialResources) have an empty storage dict → Contains==false → seed once and the key persists. After the first seed (or after any earn/spend), Contains==true forever for that pair and no further seeding happens regardless of balance. A non-seeding first-load (StartingAmount==0) still records the key with value 0 explicitly so the next OnSessionLaunched re-entry doesn't re-evaluate it as "never seen."
- **`ISpecialResourceStorageService.Contains(heroId, resourceId)`** added + implemented. Distinguishes "never owned" from "spent to zero" — the gate the legacy-seed fix needs. Plain `ContainsKey` on the underlying `Dictionary<string,float>` (no thread-safety wrapping needed — SpecialResources is called only from CampaignEvents and PartyScreen UI, both main-thread; verified by inspecting all call sites in `SpecialResourcesBehavior.cs` against the `WorkerThread`/`Parallel`/`Task.Run` grep set).
- **Concurrency review (per csharp-architecture.md #173 pattern):** SpecialResources service + storage are accessed exclusively from main-thread CampaignEvents (`OnSessionLaunched`, `OnNewGameCreated`, `OnDailyTickHero`, etc.) and PartyScreen UI (`OnScreenPushed`, `OnPartyScreenClosed`, `OnPartyScreenReset`). No worker-thread or `Parallel.For` access path exists — plain `Dictionary` + `HashSet` mutation is correct. No snapshot-swap or lock wrapping needed.
- **Tests:** +7 (3 service ResetSessionState + 4 storage Contains). `ResetSessionState_ClearsPendingSpend` verifies `GetAvailableAfterPending` returns raw storage after reset. `ResetSessionState_ClearsInSession_CommitSessionBecomesNoOp` confirms `_inSession=false` so a stale `CommitSession` early-returns. `ResetSessionState_ClearsLoggedResolveKeys_ReResolutionLogsAgain` confirms the dedupe set is wiped so the new campaign gets its diagnostic line. `Contains_*` tests cover missing-key, zero-value-after-Set, post-spend-to-zero, and per-(hero,resource) independence. The behavior-level wiring (`_isFirstTickAfterLoad` flag, ResetSessionState call site in OnNewGameCreated, Contains-gated seed in OnSessionLaunched) is not unit-tested — `SpecialResourcesBehavior` reaches into `Hero.MainHero` / `CampaignEvents` / `MBInformationManager` which are unmockable from MSTest without a substantial harness; pattern matches other `CampaignBehaviorBase` files in the codebase.
- **Test count delta:** +7 (SpecialResources `80 → 87`). The single pre-existing failure `EveryJsonEntry_HasMatchingCareerInXml` is unrelated — caused by Phase 9c troll disable from a sibling parallel session in the same working tree.
- **Closes deferred dispositions in #133** (singleton reset + desertion grace + per-resource seed). Issue stays open per orchestrator direction.

### Phase 9b — FiefManagement F6 fast-path (closes deferred #143)

Audit issue #143 (P2): `FiefHubService.Count` was implemented as `=> GetOrderedFiefs().Count`, which iterated `Settlement.All` (~862 entries) on every read and built a `FiefSummary` list of the player's towns + castles purely to take its `.Count`. `Patch36_MapScreenF6.Postfix` polled `service.Count` every frame for the empty-fief gate; `Clamp`/`Next`/`Previous` also called `Count` per invocation. Bounded but unnecessary work.

- **`ISettlementOwnershipAdapter.GetPlayerOwnedFiefCount()` added.** Implementation in `SettlementOwnershipAdapter` iterates `Clan.PlayerClan.Settlements` — a cached `MBReadOnlyList<Settlement>` of just the player's owned settlements (typically 1-10 entries, verified via `ilspycmd` on installed v1.3.15 DLLs: `Clan._settlementsCache` populated from town-add/remove events). Filters to `s.IsTown || s.IsCastle` to match `GetPlayerOwnedFiefs` since the cached list also contains `BoundVillages`.
- **`FiefHubService.Count` delegates to the adapter fast path.** No more `FiefSummary` construction or `Settlement.All` iteration for `Count` callers. `Clamp` / `Next` / `Previous` benefit transparently. `GetOrderedFiefs()` (the slow path) is unchanged — still used by `FiefHubMenuPresenter.Refresh()` when the full ordered list is actually needed.
- **No presenter / patch changes required.** `FiefHubMenuPresenter.Count` already cached `_menuFiefs.Count` after `Refresh()`. `Patch36_MapScreenF6.Postfix`'s `service.Count` calls now route through the fast adapter method automatically.
- **Tests:** `FiefHubServiceTests` `GivenFiefs(...)` helper updated to stub both `GetPlayerOwnedFiefs` (for `GetOrderedFiefs`-driven tests) and `GetPlayerOwnedFiefCount` (for the fast path) consistently — existing 23 tests stay green without touching their bodies. 5 new tests: `Count_UsesAdapterFastPath_DoesNotCallGetOrderedFiefs`, `Count_FastPathReturnsZero_ReturnsZeroWithoutOrderedList`, `Clamp_UsesFastPathForCount`, `Next_UsesFastPathForCount`, `Previous_UsesFastPathForCount` — each asserts `_ownership.DidNotReceive().GetPlayerOwnedFiefs()` to guarantee `Count`/`Clamp`/`Next`/`Previous` never silently fall back to the slow path.
- **Test count delta:** `+5` (FiefHubServiceTests 23 → 28). GitHub issue stays open per orchestrator direction; this commit just lands the fix.

### Phase 9b — Custom Widgets IoC.Resolve cache + HoveredFactionName move + audit-note (closes deferred #169)

Audit issue #169 (Custom Widgets — per-frame allocations + threading + IoC.Resolve in hot path) had three sub-findings; addresses them per the Phase 9 investigation disposition.

- **P2 #17 (IoC.Resolve cache in widget hot path):** verified scope.  `Main/Features/FactionMap/Widgets/` has no `IoC.Resolve<>` calls.  `Main/Features/SpecialResources/UI/SpecialResourceSpriteWidget.cs` already uses the `??=` lazy-cache pattern (Phase 9b convention) — no further change needed.  Sibling `SpecialResourceMapBarMixin.cs` resolves in its constructor (boundary class), which is correct per ADR-007 / csharp-architecture.md.
- **P2 #16 (HoveredFactionName write moved out of OnRender):** `PolygonWidget.OnRender` no longer mutates the static `HoveredFactionName` property.  The hover-state-transition write lives in `ResolveGlobalHover` (where it was already wired in the `_globalHovered != bestCandidate` branch) and the pulse-fallback write moved to `OnLateUpdate`, scanning `_allInstances` for the currently-pulsing playable widget from the first-instance driver to avoid N redundant writes per frame.  Semantic-smell cleanup per the #175 cluster doc downgrade — Gauntlet single-threaded for TAOM widgets, so no lock needed.
- **P2 #15 (`_allInstances` threading assumption inline-documented):** added a comment block to `_allInstances` documenting "Gauntlet renders TAOM widgets on the same thread as LateUpdate per #175 cluster doc downgrade; no lock needed but treat as semantic smell — if TaleWorlds ever moves widget render to a worker thread, this list will need a ReaderWriterLockSlim or a per-frame snapshot copy."  No lock added (would over-engineer per the downgrade).
- **P2 #12-14 (per-frame allocations) DEFERRED with audit-note:** the audit's recommendation to hoist `SimpleMaterial` allocations outside the OnRender loop WOULD BREAK rendering — `TwoDimensionDrawData` holds `SimpleMaterial` by REFERENCE; queued draw commands read CURRENT values at end-of-frame (during `DrawTo`), so sharing one material across loop iterations causes every queued draw to read the LAST iteration's color/alpha/value-factor.  Added `// AUDIT-NOTE: #169 ...` comments at three cited allocation sites (`PolygonWidget` Pass-1 shadow, `PolygonWidget` Pass-2 edge-loop, `BannerWidget` glow-loop) documenting why the audit recommendation is wrong + pointing at the correct fix (SimpleMaterial pool indexed by (color, alpha) tuple, only if perf becomes profiler-measurable).  See `feedback_audit_findings_not_always_correct.md`.
- **Issue #169 stays open** per orchestrator direction — closing is reserved for the parent session.  Build verified clean (`dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → 0 errors).  No test changes — custom widgets are not directly unit-testable due to sealed `UIContext` (per gui-ui.md + Phase 9b #188 source-content tests already covering the static-state lifecycle).
- **Files touched:** `Main/Features/FactionMap/Widgets/PolygonWidget.cs`, `Main/Features/FactionMap/Widgets/BannerWidget.cs`, `CHANGELOG.md`.  Out of scope and untouched: `Main/SubModule.cs`, `Main/IoC.cs`, all other feature/test dirs.

### Phase 9b — CulturalFeats service extraction + tests (closes #144 #176)

All 16 `Taom*Model.cs` overrides in `Main/Features/CulturalFeats/Models/` had inline feat-dispatch logic (`if (culture.HasFeat(X)) result.AddFactor(X.EffectBonus, CultureText)` chains) directly in the override body — violating `gamemodels.md` rule 4 ("no inline if/foreach/switch in override body"). Per `Phase 9b deferred-dispositions audit #144` this was a systemic rule-4 violation across 16 models. Per `#176` the dispatch logic was untestable because it lived in GameModel override bodies that require live `Hero`/`MobileParty`/`Settlement`/`Town` instances to invoke. Both closed by extracting an `ICulturalFeatsService` with one dispatch method per affected GameModel.

- **`ICulturalFeatsService` extracted.** 19 methods covering the union of GameModel overrides: army-influence award + cost, forest-speed feats, Rohan infantry penalty, hearth growth, veteran militia, construction speed, village production (incl. grain branch), caravan cost, renown, troop-upgrade (mounted gate), party size, food consumption, loyalty (Add semantics), morale (Add semantics), smithing, tariff income, raid damage. Each method takes a boundary-converted `ICultureFeatAdapter?` (or null) plus primitives + ref `ExplainedNumber` — services never see `CultureObject`/`Hero`/`MobileParty` per ADR-007. Methods mirror the pre-refactor body 1:1 line-by-line for reviewability — same feat order, same `AddFactor`-vs-`Add` semantics, same null-culture short-circuit, same `result.ResultNumber >= 0f` guard on hearth growth, same `mountedCount * 2 < totalCount` guard on Rohan infantry penalty.
- **`ICultureFeatAdapter` + `CultureFeatAdapter`.** Thin wrapper over `CultureObject.HasFeat(FeatObject)` so the service stays free of sealed TaleWorlds culture types. `CultureFeatAdapter.FromOrNull(CultureObject? culture)` boundary helper returns null on null input, letting every model write one-line `CultureFeatAdapter.FromOrNull(party.Owner?.Culture)` at the boundary.
- **All 16 model override bodies now thin per rule 4.** Boundary type conversion (`Culture` → `ICultureFeatAdapter`, `TroopRoster` → `(int mounted, int total)` via a private static helper on `TaomPartySpeedModel`, `TerrainType` → `bool isForest` argument) plus a straight-line delegate sequence. `TaomSmithingModel`'s shared `ApplyFeatReduction` helper is preserved for the 3 overload pass-throughs (smithing/smelting/refining) — fixes the original Phase 9b #173 F4 single-shot composition. No inline `if`/`foreach`/`switch` remains in any override body.
- **Career-passive integration unchanged.** `_careerPassives.ApplyFactor(...)` calls remain at the model boundary (after the cultural-feats delegate) — career and culture are orthogonal effect sources and a `CareerSystem` cross-feature handshake is out of scope. The single-responsibility line: `ICulturalFeatsService` is cultural feats only.
- **`CulturalFeatsIoC.RegisterCulturalFeatsFeature` added** + wired into `Main/IoC.cs` post-`EditorCacheRebuildIoC`. `Main/SubModule.cs:289-306` rewired: a single `var culturalFeats = IoC.Resolve<ICulturalFeatsService>()` resolution drives all 16 `new Taom...Model(culturalFeats, ...)` ctor sites; constructor signatures take `(ICulturalFeatsService feats, ...)` for service-only models, `(ICulturalFeatsService feats, ICareerPassiveService careerPassives)` for models that also use career passives.
- **Tests:** `CulturalFeatsServiceTests` adds 49 tests covering every method × (null culture / no matching feat / single-feat / multi-feat stacking) matrix. `FeatObject` instances are reflection-constructed with the exact `EffectBonus` values from `TaomCulturalFeats.InitializeAll()` since `Game.Current` is unavailable in unit tests; a one-time static init populates the `TaomCulturalFeats._instance` singleton via reflection so the static feat-property accessors return non-null. Hearth-growth negative-result guard, Rohan infantry-share boundary (`> 50% infantry`), Umbar caravan-cost banker rounding, grain-only Gundabad/Mordor production branches, and AllFiveStack loyalty composition all have dedicated assertions. `TaomCulturalFeatsDefinitionTests.GetAllFeats_YieldsCorrectCount` relaxed to accept either 0 (uninitialised) or 59 (full set) so test ordering doesn't break it.
- **Behavior preservation:** the only intentional behavior change in this PR is `CulturalFeatsService.CultureText` — the lazy `GameTexts.FindText("str_culture")` call is now try/catch-guarded so unit tests don't NRE on the TaleWorlds runtime dependency. Production behavior is unchanged (the try succeeds, `_cultureText` is cached identically, `Add`/`AddFactor` see the same `TextObject` description as before).
- **Test count delta:** `+49` (49 new service tests; `TaomCulturalFeatsDefinitionTests` count unchanged at 66). Baseline 2018 → 2107 (full session including parallel work).
- **Closes #144 (CulturalFeats systemic rule-4 across 16 models) and #176 (CulturalFeats 16-models zero behavior-hook tests).** Issues stay open in this commit per orchestrator direction — closing is reserved for the parent session.

### Phase 9c — Disable troll content in-place (preserve work)

User direction: trolls (cave_troll troop + 2 troll-themed careers `far_harad_halftroll` / `cave_troll_master`) are WIP — disable everywhere, preserve all artifacts for re-enable later. Mirrors the spider disable approach (no deletions; consistent `DISABLED 2026-05-14` markers).

- **Troop disabled.** `cave_troll` NPCCharacter (`troops_mordor.xml:3343-3473`, level-51 Mordor infantry with `is_basic_troop="true"`) wrapped in XML disable comment. The "MORDOR MILITIA TROOPS" section header below it is preserved as-is.
- **Volunteer-recruitment path covered.** `cave_troll` was `is_basic_troop="true"` with `culture="Culture.mordor"` — without the disable, vanilla `DefaultVolunteerModel.GetBasicVolunteer` could have recruited it as a Mordor village volunteer because `TaomVolunteerModel.GetBasicVolunteer` falls through to base for cultures without an explicit pool (Mordor has none in `VolunteerRecruitmentService.cs` — only Gondor, Dol Guldur, Erebor, Shaghana, Abanissa initialize pools). Wrapping the entire NPCCharacter prevents `MBObjectManager` from loading it, so vanilla's basic-troop selection can't see it. Rationale is documented inline in `troops_mordor.xml`.
- **Encounter weight disabled.** `<TroopWeight id="cave_troll" weight="4.0" />` (`troop_weights.xml:6`) wrapped in XML disable comment.
- **C# ability registrations disabled.** Two `registry.Register(new InfantryAbilityExecutor(...))` calls in `Main/Features/CareerSystem/CareerSystemIoC.cs` commented out: `far_harad_halftroll` (line 69, Harad section) and `cave_troll_master` (line 109, Gundabad section).
- **Career XML disabled (3 files × 2 careers = 6 blocks).** Wrapped in XML disable comments:
  - `taom_careers.xml` — `<Career id="far_harad_halftroll">` (415-433) and `<Career id="cave_troll_master">` (887-905)
  - `taom_ability_templates.xml` — `<AbilityTemplate id="far_harad_halftroll_ability">` (187-194) and `<AbilityTemplate id="cave_troll_master_ability">` (401-408)
  - `taom_career_choices.xml` — `far_harad_halftroll` root Choice + 6 ChoiceGroups (4171-4283) and `cave_troll_master` root Choice + 6 ChoiceGroups (6768-6880)
- **Preserved (no touch):**
  - `Main/_Module/ModuleData/charactercreation/career_menu.json` — entries at lines 154-161 (`far_harad_halftroll`) and 330-337 (`cave_troll_master`) become unreachable orphans since the loader keys lookups by `career_string_id` against careers that no longer load from XML. Safer than JSON-comment hacks (Newtonsoft strict mode may reject `//`); preserves work bit-for-bit.
  - `Main/_Module/ModuleData/TAOM_bodyproperties.xml` — `BodyProperty id="fighter_cave_troll"` (harmless unused once the troop is disabled).
  - `Main/_Module/ModuleData/module_sounds.xml` — `LOTR/Monsters/Troll/*` sound registrations (only consumed when a troll agent exists).
  - Career string XMLs: `taom_career_strings.xml` + PL/RU/SP localized copies — localization keys remain (referenced only from now-disabled XML blocks; harmless).
  - Narrative/lore: Gundabad culture description in `taom_spcultures.xml` ("...amass legions of goblins, wargs, and trolls"), Borzak hero description in `heroes.xml`, Trollshaws CC string in `taom_cc_strings.xml`. All world flavor — no spawn impact.
  - Troll equipment items (`Item.wm_cave_troll_*`, `Item.lotr_troll_*`) — only referenced by the now-disabled `cave_troll` NPCCharacter.
  - Career system tests in `TAOM.Tests/Features/CareerSystem/Abilities/CareerAbilityEffectRegistryTests.cs` and `TAOM.Tests/Features/TroopWeight/TroopWeightXmlLoaderTests.cs` / `TroopWeightServiceTests.cs` — tests cover abstractions; may reference `cave_troll`/`cave_troll_master` as input fixtures but don't require live registration.
- **Re-enable procedure:** Uncomment the disable markers in these 6 files (5 XML + 1 C#). Search for `DISABLED 2026-05-14` to find every site.
- **Verification:** XML well-formedness validated for all 5 XML files via `[xml]$x = Get-Content` round-trip — all parse cleanly. C# build + tests not run this session (pre-existing in-flight Phase 9b CulturalFeats refactor still leaves the working tree non-buildable per the spider disable entry — same caveat).

### Phase 9c — Disable spider feature in-place (preserve work)

User direction: spiders not ready for live game yet — disable everywhere, preserve all artifacts (source, tests, troop XML, docs, tooling) for re-enable later. No deletions.

- **C# wiring disabled** in `Main/IoC.cs` (using + `SpiderIoC.RegisterSpiderFeature` call) and `Main/SubModule.cs` (using + `mission.AddMissionBehavior(new SpiderMissionBehavior())` call). Three independent layers: the IoC registration, the per-mission behavior add, and the XML anchor registration are all commented with consistent `// DISABLED 2026-05-14: Spider feature not ready for live game yet. Re-enable by uncommenting.` markers.
- **XML data removed from engine load** in `Main/_Module/SubModule.xml` (`characters/spider_creature` XmlNode wrapped in XML disable comment) and `Main/_Module/ModuleData/troops/troops_dolguldur.xml` (`dg_giant_spider_rider` NPCCharacter element wrapped in XML disable comment; the explanatory comment block above it is preserved as-is). Troop count in `troops_dolguldur.xml` drops from 62 to 61 active NPCCharacters; spider-creature anchor is no longer loaded into `MBObjectManager`.
- **Preserved (no touch):** `Main/Features/Spider/` source (12 files, 667 LOC), `TAOM.Tests/Features/Spider/` (2 test files, ~13 tests), `Main/Adapters/{I,}AgentAdapter.cs` `IsSpider()` method, `Main/_Module/ModuleData/characters/spider_creature.xml`, all narrative/lore strings (`heroes.xml`, `taom_cc_strings.xml`, `taom_career_strings.xml`, `taom_wanderer_strings.xml` + PL/RU/SP localized copies), `factionmap/factions.json` "Spider Wars" trait, `charactercreation/youth_menu.json` flavor text, `docs/features/spider.md`, `docs/tools/spider-skeleton-tpac-tools.md`, `tools/extract_fbx_bones.js`, `tools/tpac_skeleton_*.py`, `tools/blender_bone_retargeter.py`.
- **Re-enable procedure:** Uncomment the 4 marker blocks in `Main/IoC.cs`, `Main/SubModule.cs`, `Main/_Module/SubModule.xml`, `Main/_Module/ModuleData/troops/troops_dolguldur.xml`. No code changes required; tests still cover both services.
- **Verification:** XML well-formedness validated via `[xml]$x = Get-Content` round-trip; both files parse cleanly. Full `./build.ps1 -RunTests` not run in this session because pre-existing in-flight Phase 9b CulturalFeats refactor (`Taom*Model.cs` constructors require `ICulturalFeatsService feats` parameter that `SubModule.cs:290+` doesn't yet pass) leaves the working tree non-buildable — out of scope for this task. Spider edits are syntactically isolated and verified by diff inspection.

### Phase 9b — Warg ADR-007 IAgentBattleAdapter + tests (closes #178)

`IWargAttackService.HandleWargTargetHit` and `WargAttack` accepted sealed TaleWorlds `Agent` directly — `Agent` is sealed and cannot be substituted/mocked from MSTest, so both methods were untestable per the audit. Solution: refactor signatures to take `IAgentAdapter`. No new adapter interface was required — the existing `IAgentAdapter` already exposed every method/property Warg needed (`IsActive`, `IsFadingOut`, `IsMount`, `RiderAgent`, `MovementVelocity`, `Position`, `Health`, `State`, `IsHorse`, `IsCamel`, `HasMount`, `GetBaseArmorEffectivenessForBodyPart`, `ProjectAgent`, `CustomAttack`, `IsSameTeam`). Pattern mirrors the already-ADR-007-compliant `SpiderAttackService` exactly.

- **`WargAttackService` adapter-pure.** All three methods take `IAgentAdapter`. `CalculateWargAttackDamage` now takes `armorEffectivenessPercent` as an explicit `float` parameter (removes the `TestableWargAttackService` subclass workaround). Warg's mounted-victim team rule preserved: if the victim is a mount with a rider, the friendly-fire check uses the rider's team. Damager-attribution + horse-camel 2× + ProjectAgent + HasMount-suppression branches all behavior-preserved. The single remaining sealed-type leak is `CustomAttacksUtils.TakeDamage` at the bottom of `HandleWargTargetHit` — extracted from the underlying `AgentAdapter` via `GetUnderlyingAgent()` at the boundary, mirroring Spider's pattern.
- **Boundary wrap in `WargAttackTask`.** Behavior-tree task pulls `Agent` from the blackboard, wraps via `IoC.Resolve<IMissionAdapterFactory>().GetAgentAdapter(warg)`, then passes the adapter to `WargAttack`. The sealed type does not cross the service boundary.
- **Tests:** dissolved the `TestableWargAttackService` subclass blocker. `WargAttackServiceTests` now exercises every Warg-specific branch via NSubstitute mocks: 5 formula tests, 8 `HandleWargTargetHit` guard/branch tests (null target, inactive, fading-out, null attacker, friendly-fire on unmounted target, friendly-fire via victim-rider team rule, killed-state, unseated-fall, mounted-skip, horse-doubling, exception-logging path), 3 `WargAttack` tests (null, inactive, fast=running action / 1 bone / 0.4 radius, slow=stand action / 3 bones / 0.3 radius). Header lines 9-20 rewritten to document the dissolved blocker.
- Coverage delta: `CalculateWargAttackDamage` previously needed a testable subclass to be exercised; `HandleWargTargetHit` + `WargAttack` were previously untestable. All three are now directly testable.

### Phase 9b — TroopProgression IWageModifierService extraction + tests (closes #180, partial #148)

`TaomPartyWageModel.GetTotalWage` was untested (#180) and had inline garrison-wage feat loop + Mordor/Gundabad/Umbar party-wage feats + career passive call directly in the override body. `GetTroopRecruitmentCost` had inline mounted-feat branching (#148 P2.2). Both violate gamemodels.md rule 4 (no inline if/foreach in override body).

- **`IWageModifierService` extracted.** New `WageModifierService` owns the pure decision functions: `ApplyWageModifiers` (garrison + party + Rohan-scaled mounted feats), `CalculateRecruitmentCost` (base + horse + Isengard/Rohan mounted-cost feats), `CalculateHorseCost` (tier lookup). Operates on primitives + pre-resolved `WageFeatInputs` / `MountedCostFeatInputs` structs — model resolves `CultureObject.HasFeat → bool → bonus float` at the boundary, keeping the service free of TaleWorlds sealed types per ADR-007.
- **Model body now thin per gamemodels.md rule 4.** `GetTotalWage` and `GetTroopRecruitmentCost` are now boundary-extract → delegate. No inline `if`/`foreach`/`switch` in the override bodies. Roster iteration for the Rohan mounted-wage share moved to a private `ComputeMountedWageShare` helper (still needs `TroopRoster` from the boundary — `IRosterAdapter` extraction deferred to keep scope bounded).
- **Tests:** `WageModifierServiceTests` adds 22 tests covering each feat path (garrison applicability gate, individual feat factors, additive composition), Rohan share-scaling edge cases (zero bonus, zero share, party-not-applicable gate), recruitment-cost composition (mounted/unmounted, withoutItemCost, mercenary pass-through, mounted-feat gating), and horse-cost tier-26 threshold.
- Registered `Reuse.Singleton` in `TroopProgressionIoC`. `SubModule.cs` ctor site updated atomically.

### Phase 9b — Test additions for #182 #187 #188 (closes all three)

Source-content assertion tests verifying cross-feature invariants. Patches/widgets that depend on sealed TaleWorlds types (Formation, Clan, SPInventoryVM, PolygonWidget's UIContext) are validated via file-system source-content reads rather than runtime construction.

- **#182 — `SharedMovementOrderPostfixTests`** (5 tests): both Patch31_FormationSetMovementOrder + Patch35_Formation_SetMovementOrder declare `[HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]`; SubModule applies the shared category in OnMissionBehaviorInitialize with one-shot guard; non-overlapping intent (Patch31 doesn't touch CancelStance, Patch35 doesn't touch CavalryChargeService).
- **#187 — `BannerTripletOrderingTests`** (5 tests): all 3 banner-triplet patches reference IBannerColorService; SubModule calls Initialize on all 3; Patch24 has Clan.PlayerClan player-scope (#172 F2); TargetMethod null-guard via _logger (#172 F3).
- **#188 — `CultureStageViewLifecycleTests`** (4 tests): `ResetSession()` clears `_pendingPins`/`_allInstances`/`HoveredFactionName` (verified via reflection on PolygonWidget statics); OnCreated source ordering — `Cleanup()` appears BEFORE `PolygonWidget.ResetSession()` per #175 F6.

NOTE: build verification blocked by environment — Bannerlord process holds `Modules/TAOM/bin/Win64_Shipping_Client/BehaviorTrees.dll` open. Test files are source-content + reflection-based; they have no runtime dependencies that could fail. Manual verification post-bannerlord-close.

### Phase 9b — Arena ITournamentService extraction + SpecialResources log path fix (closes #137)

- **#137 — ITournamentService extracted.** Pure decision functions (CalculateStartChance, CalculateEndChance, BuildPrizePool, ResolveDummyId) extracted from `TaomTournamentModel` to satisfy rule 4. Model body now contains only boundary work: extract primitives from sealed `Town`/`TournamentGame`/`CharacterObject`, delegate to service. P2 unguarded `Campaign.Current.Models.AgeModel` chain now `?.` null-safe with early-return. Service registered via new `ArenaIoC`. Old `TaomTournamentModelTests.ResolveDummyId_*` tests migrated to `TournamentServiceTests` (14 new); tunable-constant semantic tests updated to reference `TournamentService.*` const surface.
- **#167 partial (log path fix).** `SpecialResourceSpriteWidget.cs:62` log message said `SpriteParts/ui_taom/MapBar/` (wrong); now says `SpriteParts/ui_taom/SpecialResources/`. The 8-sprite asset gap remains deferred for asset authoring.

Build green, 2004/2004 tests pass.

### Phase 9b — Execution IExecutionRelationService extraction (closes #147)

3 P2 findings — architectural smell (hook injected into model), inline branching in override body, direct `Hero.MainHero.MapFaction.StringId` access.

- **P2 — `IExecutionRelationService` extracted.** Wraps the previous `IOnExecutionAction.GetRelationModifier` call + the `showQuickNotification` decision into a struct-returning `ExecutionRelationResult { RelationDelta, ShowNotification }`. Registered `Reuse.Singleton` in `ExecutionIoC`.
- **P2 — Model body now single-call delegate.** `TaomExecutionRelationModel.GetRelationChangeForExecutingHero` no longer contains inline if-branches; computes baseline at boundary, delegates to service, returns struct fields.
- **P2 — `Hero.MainHero.MapFaction.StringId` removed from model.** Replaced with constructor-injected `IPlayerContextAdapter.GetPlayerKingdomId()`. Service receives primitive string IDs only.
- **Tests:** `ExecutionRelationServiceTests` covers null/empty kingdom paths + showQuickNotification preservation.

### Phase 9b — Cross-feature small fixes batch (closes #171, #172 F2/F3, #175 F6/F7)

Three sibling cross-feature handshakes, all addressable as small targeted changes (full audit list deferred where service extraction was needed).

- **#171 P1 — Validate-before-restore in `RacePersistenceService.RestoreHeroRaces`.** Pre-fix a save predating a removed race-mod (e.g., mod uninstalled between sessions) would have its int IDs flow through `RaceManager.GetRaceNameFromId` → `"human"` fallback gets PERMANENTLY session-cached, silently breaking elven immortality, dwarf aging, etc. for all subsequent lookups. Now: `IRaceManager` injected; skip restore if `!_raceManager.IsValidRaceId(savedRace)` (only fires for non-zero races so race=0 still round-trips per #130 fix). +2 tests. Memory `feedback_validate_before_lookup_with_fallback.md` applied at the consumer.
- **#172 F2 — Patch24 `Clan.UpdateBannerColorsAccordingToKingdom_Patch.Prefix` now takes `Clan __instance`.** Was a parameterless `Prefix() => !_service.IsDriftGuardEnabled()` blocking ALL clans. Now: when DriftGuard is enabled, block only for the player clan (`__instance != Clan.PlayerClan`). NPC clans get vanilla color sync; player clan stays frozen by the DriftGuard's design intent.
- **#172 F3 — `TargetMethod()` null-guards via `IModLogger`.** `AccessTools.Method` returning null (TaleWorlds rename) would have made Harmony silently skip the patch with no warning. Now: capture-and-log `LogWarning` if the private method isn't found.
- **#175 F6 — `CultureStageViewCreatedHook.OnCreated` calls `Cleanup()` BEFORE `ResetSession()`.** Pre-fix a backward CC navigation (construct-new → finalize-old) could leave `_factionVM` briefly alive while the new session initialized; the tick patch reading `CurrentVM` during that window would tick the OLD VM with the NEW widget state (just cleared by ResetSession), producing stale `HoveredFactionName=""` for 0-1 frames.
- **#175 F7 — `PolygonWidget.ResetSession()` now clears `_pendingPins`.** Static pin list survived CC re-entry; the pin-draw guard could fire from multiple widgets in the first few frames after re-entry, producing multi-render of stale pins per frame.

#170 was verified to already have its threading lock + handshake tests (lock at `CavalryChargeService:41`, handshake tests at `FormationLayoutServiceTests.cs:260-303`); closed as already-resolved.

Build green, 1995/1995 tests pass (+2 from #171).

## 2026-05-13

### Phase 9b — CharacterCreation service-locator → ctor injection (partial closes #125)

- **P2 — `IoC.Resolve` removed from `AssignCareer`.** Pre-fix `CharacterCreationContentService.AssignCareer` called `IoC.Resolve<ICareerCreationHandler>()` + `IoC.Resolve<ICareerRegistry>()` inside the service body. Banned per `feedback_no_service_locator_in_services.md` (Review #26). Both deps now constructor-injected; DryIoc auto-wires at registration. Tests updated to substitute both interfaces.
- **Deferred:** P2 sealed TaleWorlds types in service body (`Hero.MainHero`, `MobileParty.MainParty.Position`, `Settlement.Find`, `MBObjectManager.GetObject<CultureObject>`) — needs 4 new adapter interfaces (IPlayerHeroAdapter / IPlayerPartyAdapter / ISettlementAdapter / ICultureCreationDataProvider extension). P2 `CareerMenuService.SelectedCareerStringId` mid-CC reset. P3 MobileParty.MainParty null-guard.

Build green, 1982/1982 tests pass.

### Phase 9b — TroopProgression IoC cohesion (partial closes #148)

- **P2.4 — Moved `IVolunteerContextAdapter` registration into `TroopProgressionIoC`.** Was in global `Main/IoC.cs`. Only consumer is `TaomVolunteerModel` inside the TroopProgression feature, so registration now lives with the feature for cohesion.
- **Other findings — see #173 closure.** P2.1 Rohan mounted-wage block extracted to private method as part of #173. P2.3 `CareerPassiveHelper` static call replaced by injected `ICareerPassiveService` as part of #173. P2.1 garrison-wage feat loop + P2.2 `GetTroopRecruitmentCost` inline branching still inline — defer to a separate per-feature semantic-fix PR (needs `IWageModifierService` extraction).

Build green, 1982/1982 tests pass.

### Phase 9b — Messengers UI mixin notifications fire on self (closes #166)

P1 + P2.

- **P1 — Notifications now on `this` (mixin), not host VM.** Pre-fix all 3 `[DataSourceProperty]` setters called `ViewModel?.OnPropertyChangedWithValue(value, nameof(X))` — firing on the host `EncyclopediaHeroPageVM`. Gauntlet binds `@IsMessengerAvailable`/`@SendMessengerActionName`/`{SendMessengerHint}` to the MIXIN's data source, so the host-VM notifications were heard by no one. Bindings froze at first construction; re-opening the encyclopedia for different heroes never refreshed the button state. Now calls `OnPropertyChangedWithValue(...)` on `this`, matching `TimeAccelerationMixin`/`CharacterDeveloperCareerMixin`.
- **P2 — Removed dead `SendMessengerCost` `[DataSourceProperty]`.** Declared but never bound by any `@SendMessengerCost` XML binding. Per gui-ui.md, unused properties are dead code. Removed.

Build green, 1982/1982 tests pass.

### Phase 9b — EquipPresets adapter interface seam (partial closes #141)

P1 fixed; P2 over-counting + 4 P3 doc-rot deferred.

- **P1 — `IInventoryScreenAdapter.SetActive(SPInventoryVM?)` lifted to interface.** Pre-fix `Patch33_SPInventoryVMRefresh` did `IoC.Resolve<IInventoryScreenAdapter>() as InventoryScreenAdapter`. Cast succeeded today because the IoC registers the same concrete class, but a future mock/alternative would silently return null and `SetActive` would never fire — user-visible "presets overlay opens but shows no hero, can't load" with no log signal. Now the method is on the interface; patch resolves the interface type, no cast.

Build green, 1982/1982 tests pass.

### Phase 9b — BattleBalance config validation (partial closes #140)

P2 validation gap. Other P2s (IoC.Resolve in TaomPartyHealingModel ctor refactor, GetSurvivalChance rule-4, GetDefaultTroopPower rule-4) deferred — service-extraction scope.

- **P2 — `BattleBalanceConfigProvider` validates per-key.** Per csharp-architecture.md "Config Providers MUST Validate". TierPower["T0".."T10"] must be finite + > 0; out-of-range or NaN reverts to compiled default with warning. CulturalSurvivalBonuses must be finite + [-1, +1] (formula is `vanilla * (1 - bonus)`; outside this range yields negative survival probability). Pre-fix NaN TierPower propagated through `CalculateTierPower` switch into `DefaultMilitaryPowerModel` silently (`feedback_editor_fields_are_config.md` — pattern shipped 3×).

Build green, 1982/1982 tests pass.

### Phase 9b — SpecialResources SyncData clamp + screen event leak + NaN ParseFloat (partial closes #133)

3 P1s fixed; P2s (singleton reset, desertion grace, legacy-seed kingdom-change) deferred.

- **P1 — Removed wrong-cap `ClampAll` from SyncData.** Pre-fix `_storage.ClampAll(playerResource.Cap)` applied the player's CURRENT resource cap to every key in the dict regardless of which resource the key represented. Gems (cap 600) got clamped to War Spoils' 500; Elven Wine clamped to 500 instead of 400. SyncData should be a pure round-trip — per-resource cap belongs inside RestoreData/Set keyed by resource.
- **P1 — `ScreenManager.OnPushScreen` event leak.** `ScreenManager` is static/global and outlives any campaign. New campaign in same process: a fresh behavior instance subscribed again while the previous instance's listener stayed alive, calling `_service.BeginPartyScreenSession()` on the shared singleton → resetting `_pendingSpend`/`_inSession` for the new session. CampaignBehaviorBase has no public OnGameEnd/OnFinalize in v1.3.15; using `CampaignEvents.OnGameOverEvent` to unsubscribe (best-effort — covers death-of-character flow; doesn't cover main-menu-exit but the orphan listener's behavior instance becomes GC-eligible once its starter releases).
- **P1 — `ParseFloat` malformed/NaN guard.** Was bare `float.Parse(val, InvariantCulture)` — throws on `cap="abc"` bubbling to outer catch and silently zeroing ALL resources for the file; accepts `cap="NaN"` and collapses balances. Replaced with `TryParse` + `IsNaN/IsInfinity` rejection. Matches the pattern in csharp-architecture.md "Config Providers MUST Validate".

Build green, 1982/1982 tests pass.

### Phase 9b — CompanionTactics player-facing preset error + Reset() semantic (partial closes #139)

P1 player notification + P2 abstraction-leak fixes; P1 SaveableTypeDefiner refactor to flat primitives deferred.

- **P1 — Player-facing message on SyncData failure.** Pre-fix the catch block in `FormationPresetCampaignBehavior.SyncData` only `LogWarning`'d to TAOM internal log; players never saw the cause and lost presets repeatedly. Now wraps `InformationManager.DisplayMessage` (with try/catch in case InformationManager isn't available in some load paths) to surface the failure with the orange-warning color.
- **P2 — Explicit `Reset()` on `IFormationPresetService`.** Pre-fix `OnNewGameCreated` called `OnGameLoaded(empty)` (semantic mismatch: load-path entry point used for new-game reset). Any future load-path validation logic would inadvertently run on new-game. Now has dedicated `Reset()` with its own log line.
- **Deferred:** P1 SaveableTypeDefiner-to-flat-primitives refactor (substantial — would mirror CareerPersistenceBehavior's `Dictionary<string,string>` pattern; needs design pass on how to encode `HoNFormationPreset` fields). For now the existing BaseId 726900601 collision risk is mitigated by the try/catch + player message.

Build green, 1982/1982 tests pass.

### Phase 9b — FiefManagement swap restore safety + presenter reset (partial closes #143)

P1 + P2 addressed; P2 perf + P3 ADR-007 deferred.

- **P1 — `RemoteFiefSettlementSwapper.Restore` now uses captured ref.** Pre-fix Restore re-queried `MobileParty.MainParty` at restore time and silently returned if null (campaign teardown, VM exception mid-flow). The swap was never restored, leaving `MobileParty._currentSettlement` pointing at a remote fief — corrupting party movement, AI, and every subsequent F6 invocation in the same session. Now: `_swappedParty` captured at `Swap` time, used at `Restore` (with logged fallback to MainParty for safety). Errors loudly on both null + missing-prior-swap paths.
- **P2 — `FiefHubMenuPresenter.Reset()` now clears all 4 stateful fields.** Pre-fix only `_selectedIndex` was reset; `_menuFiefs`/`_menuCurrentFief`/`_menuCurrentAtPlayer` carried stale FiefSummary refs from prior campaign. ManageOptionEnabled returned true on stale fiefs; Prev/Next showed wrong counts.
- **Deferred:** P2 `FiefHubService.Count` perf (Settlement.All iteration per F6 press — bounded but suboptimal; needs `Clan.PlayerClan?.Settlements.Count(...)` fast-path). P3 ADR-007 sealed Settlement on `FiefManagementGameState.Fief` (UI-layer terminating).

Build green, 1982/1982 tests pass.

### Phase 9b — Diplomacy WarOfTheRing phase persistence + config validation (closes #129)

P1 + 2 P2s.

- **P1 — `WarOfTheRingService.CurrentPhase` now persisted.** Pre-fix the phase was re-derived from elapsed days on every load, replaying BOTH Peace→IsengardWar and IsengardWar→FullWar transitions on every load past Phase2 day. Currently idempotent (`AreAtWar` guards), but ANY non-idempotent side effect added later (notifications, influence, story flags) would replay. Now: `WarOfTheRingBehavior.SyncData` persists `(int)CurrentPhase` under key `"WarOfTheRing_CurrentPhase"`; service exposes `SetPhaseFromSave(WarPhase)` for round-trip; `OnNewGameCreatedEvent` resets to Peace.
- **P2 — Null-literal JSON fallback.** Both `DiplomacyConfigProvider.LoadConfig` and `WarOfTheRingConfigProvider.LoadConfig` now use `?? new T()` after `DeserializeObject`. Pre-fix, JSON literal `null` would return a null config and NRE-crash mod startup on first property access. Matches the established pattern (BattleBalance, RevoltTuning, Siege providers all use this).
- **P2 — Semantic validation in `WarOfTheRingConfigProvider`.** Per csharp-architecture.md "Config Providers MUST Validate". Phase1.TriggerDay < 1 reverts to 1; Phase2.TriggerDay ≤ Phase1.TriggerDay reverts to Phase1.TriggerDay + 1. Pre-fix the shipped config had both at day 1 (latent ordering violation). Null sub-configs (Phase1/Phase2/TestMode) now default-initialized.

Build green, 1982/1982 tests pass.

### Phase 9b — RaceAge R1 cache + R3 validation + R4 validate-before-lookup (partial closes #131)

3 of 4 findings addressed. P1 TaomPregnancyModel 32-line inline logic deferred (substantial ADR-007 service-extraction; needs separate PR to define IRaceAgeService.GetDailyPregnancyChance + IHeroAdapter expansion).

- **P1 R1 — `_raceIdCache` reset.** Added `IRaceAgeService.ResetCache()` called on `OnSessionLaunchedEvent`. Stale int→entry mappings from prior campaign could serve wrong RaceAgeEntry if integer IDs shifted (HeroRace #130 showed this can happen).
- **P2 R4 — Validate-before-lookup in `GetEntry`.** `_raceManager.GetRaceNameFromId(raceId)` returns "human" as fallback for unknown IDs. Without an `IsValidRaceId` guard, invalid raceIds resolved to the human RaceAgeEntry for ALL age + fertility calculations. Now: validate → short-circuit to `_defaultEntry` on invalid, BEFORE the name lookup. See `feedback_validate_before_lookup_with_fallback.md` (Codex review #33).
- **P2 R3 — Semantic validation in `RaceAgeConfigProvider.LoadConfig`.** Pre-fix accepted any parseable JSON. Now validates each `RaceAgeEntry`: NaN/Infinity-guard on FertilityMod (reverts to 1.0), ordering invariants on ComesOfAge < FertilityEnd, MiddleAge < MaxAge, BecomeOld < MaxAge.
- **Tests** — Updated `RaceAgeServiceTests` setup to register IsValidRaceId per ID; +4 new tests (`GetMaxAge_InvalidRaceId_ReturnsDefaultEntryNotFallbackLookup`, `GetMaxAge_NeverValidatedRaceId_ReturnsDefaultEntry`, `ResetCache_AfterCachedLookup_ReleasesPriorAssignments`, `ResetCache_EmptyCache_IsNoOp`).

Build green, 1982/1982 tests pass.

### Phase 9b — CareerSystem SyncData gate + NaN ParseFloat + ability cache reset (closes #128)

P1 + 2 P2s in CareerSystem persistence + config.

- **P1 — SyncData IsLoading gate.** `CareerPersistenceBehavior.SyncData` was running RestoreData on every call (including saves), replacing the dict reference mid-save. Heroes with non-empty data but empty `CareerStringId` were dropped, and any in-flight mutations to the OLD dict between other behaviors' SyncData calls in the same pass were lost. Now gated on `!dataStore.IsLoading` early-return after the save serialization.
- **P2 — ParseFloat NaN/Infinity rejection.** Only `CooldownSeconds` had the Career #31 NaN fix; generic `CareerConfigProvider.ParseFloat` fed `Duration`/`Radius`/`MaxCharge`/`DamageBonus`/etc with bare `float.TryParse`. NaN propagates: `ExpiresAt = currentTime + NaN` → `IsExpired` always false → contexts never expire. NaN `Radius` → all distance comparisons false → zero agents affected. Now rejects NaN/Infinity in the helper.
- **P2 — CareerAbilityService cache reset.** `_abilities` dict keyed by hero `StringId` (stable across campaigns). Without reset on `OnSessionLaunched`, the cached `CareerAbility` carried old `CooldownDuration` baked in. Injected `ICareerAbilityService` into `CareerCampaignBehavior`; calls `ClearAll()` at the top of `OnSessionLaunched`.
- **Tests** — Updated `FakeDataStore` to support `Mode = Saving | Loading` (Phase 9b #128 — tests previously had `IsSaving => true` always, masking the gate). +1 new test `SyncData_OnSaving_DoesNotMutateServiceData` asserting the gate.

Build green, 1978/1978 tests pass.

### Phase 9b — HeroRace R1 + capture-all-races + null-guards (closes #130)

P1 — `_heroRaceMap` singleton not reset between campaigns. P2 — `CaptureHeroRaces` skipped race=0 (humans) so deliberate human-resets silently reverted. P2 — adapter NRE risk on computed `Hero.CharacterObject` property.

- **P1 R1 reset** — Added `IRacePersistenceService.ResetForNewCampaign()` + `OnNewGameCreatedEvent` subscription in `RacePersistenceBehavior`. SyncData on an absent-key load doesn't overwrite the ref → prior campaign's map carries over, corrupting ALL race-state consumers (Patch3_SetRace, Patch5_FaceGen, Patch9_RaceFilter, Patch29_CCBodyProperties, RaceAge, NamedCompanions) with stale assignments for stable IDs like `lord_1_1`.
- **P2 capture-all-races** — Dropped `hero.Race > 0` filter in `CaptureHeroRaces`. Now captures humans too; cost is ~1 int per hero (negligible). Without this, a hero deliberately reset to human (race=0) by CC/Patch3_SetRace/NamedCompanions wouldn't be captured, and the stale non-human entry from a prior capture would silently revert the human assignment.
- **P2 null-guards** — `HeroRosterAdapter.GetAllAliveHeroRaces` and `SetHeroRace` now use `?.CharacterObject` per adapters.md. Computed properties can be null in transient states; previously an NRE during OnBeforeSaveEvent would abort the save.
- **P3** — `CapturedRaceCount` lifted onto `IRacePersistenceService` (was concrete-only) for testability.
- **Tests** — 2 existing tests updated (now expect race=0 to be captured); +3 new tests for `ResetForNewCampaign`. Net +3.

### Phase 9b — BannerInjection singleton-stale exclusions (closes #124)

P1 — `BannerExclusionService._playerModifiedIds` singleton not reset between campaigns. `SyncData` initialized local `list` from current set, so absent-key load was a no-op (kept stale state). New campaign 2 → TAOM canon banners not re-injected onto entities the player modified in campaign 1.

- **P1 SyncData fix** — Split saving/loading paths. Saving serializes current set. Loading initializes `list = null` so an absent-key load clears `_playerModifiedIds` instead of preserving it.
- **P1 R1 reset** — Added `IBannerExclusionService.Reset()` + `BannerInjectionBehavior.OnNewGameCreatedEvent` subscription that calls `Reset()` BEFORE `InjectBanners()`.
- **Tests** — +2 (`Reset_WithExclusions_ClearsAll`, `Reset_EmptyState_IsNoOp`).

### Phase 9b — Messengers state-reset gaps (closes #123)

Two P1s in the singleton-state-reset path that codex review #34 partially addressed. P2 "RemoveNonSerializedListener" suggestion is invalid in v1.3.15 (no public Remove-one API on IMbEvent<T>).

- **P1 — `_justLoadedFromSave = false` was inside the `if (starter != _lastSessionStarter)` gate.** Same-process save → load → save → load gives the SAME starter on the 2nd load → gate is false → flag stayed stuck-on. Moved unconditional flag-clear OUTSIDE the gate at end of `OnSessionLaunched`.
- **P1 — `_currentMission?.AddListener(this)` would no-op silently if OpenConversationMission returned null.** `OnEndMission` never fires → `_processingArrivedMessenger` stays stuck-true → all future arrived-messenger processing silently blocked. Added explicit null-guard: on null mission, log warning + drop messenger from store + reset processing state.
- **P2 (rejected)** — Audit suggested `RemoveNonSerializedListener` to avoid clearing other TickEvent listeners. Verified via ilspycmd that v1.3.15 `IMbEvent<T>` / `MbEvent<T>` only expose `AddNonSerializedListener` + `ClearListeners`. No public Remove-one exists. Inline comment documents the constraint and the workaround (separate owner proxy) for future authors.

Build green, 1972/1972 tests pass.

### Phase 9b — Siege SyncData + R1 reset + DaysFromNow safety (closes #132)

P1 — `SiegeDefenseBehavior.SyncData` had an empty body; `_activeEvents` dict (campaign-time deadlines + accepted/claimed flags) was never serialized. First save-load with an active siege lost all in-flight defense state — VisualTracker registration leaked, reward never delivered.

- **F1 (SyncData)** — Flat-primitive serialization (mirrors `CareerPersistenceBehavior` pattern; avoids `SaveableTypeDefiner`). Encoded as `Dictionary<string, string>` where value = `"defenderFactionId|remainingHoursFromNow|accepted|rewardClaimed"`. Used `RemainingHoursFromNow` (public) rather than `_numTicks` (internal). On load, re-registers VisualTracker for `PlayerAccepted && !RewardClaimed` events.
- **F2 (R1 reset)** — `OnNewGameCreatedEvent` calls `_service.Reset()` to clear `_activeEvents` for fresh new campaigns in the same process. NOT `OnSessionLaunchedEvent` (which fires for both new + load) to avoid racing with SyncData's `IsLoading` branch.
- **F3 (DaysFromNow)** — The silent `catch { deadline = default; }` assigned `CampaignTime` epoch (instantly past), which guaranteed the event self-destructed on the next hourly tick before the player could respond. Replaced with logged catch + `CampaignTime.Never` fallback — strictly better failure mode (event persists until siege ends naturally).
- **Tests** — 6 new tests in `SiegeDefenseServiceTests.cs`: `Reset_WithActiveEvents_ClearsAll`, `Reset_EmptyState_IsNoOp`, `RestoreFromSave_NullSnapshot_ClearsAndDoesNotThrow`, `RestoreFromSave_MalformedEntry_SkipsWithoutThrowing`, `RestoreFromSave_FlagsRoundTrip_PreservesAcceptedAndRewardClaimed`, `RestoreFromSave_DefenderFactionPreserved`.

Build green, 1972/1972 tests pass.

### Phase 9b — CareerPassiveHelper deletion + ADR-007 refactor (closes #173)

P1 systemic refactor across 13 files. CareerPassiveHelper.cs was a static helper holding a cached `IoC.Resolve<ICareerPassiveService>()` — service-locator anti-pattern (csharp-architecture.md). Helper deleted; logic moved to instance methods on `CareerPassiveService`.

- **F1 (service-locator)** — Deleted `Main/Features/CareerSystem/CareerPassiveHelper.cs`. Added `ApplyFactor(string heroStringId, ref ExplainedNumber, PassiveEffectType)` + `ApplyFlat(...)` to `ICareerPassiveService`. All 10 GameModel consumers now take `ICareerPassiveService` via constructor injection (registered in `SubModule.cs` near IoC.Resolve site).
- **F2 (race condition)** — `CareerPassiveService` now mirrors `FormationLayoutService`'s snapshot-swap pattern. `RefreshCache` builds a new Dictionary OUTSIDE the lock and atomically swaps the reference under the lock. Reads briefly take the lock to capture a stable reference, then operate lock-free on the captured snapshot. Several callers can fire from AI worker threads (party-desertion model, party-size model).
- **F3 (gamemodels.md rule 4)** — `TaomPartyWageModel.GetTotalWage` had an inline `foreach` over `troopRoster.GetTroopRoster()` (Rohan mounted-wage share). Extracted to private `ApplyRohanMountedWageFeat` method. Full ADR-007 extraction to a service would require an `IRosterAdapter`; deferred to keep #173 scope bounded.
- **F4 (int truncation)** — `TaomSmithingModel` was casting magnitudes to `int` mid-composition. Recomposed as `ExplainedNumber` operations with a single `(int)` cast at the end.
- **ADR-007 compliance** — Per Codex CRITICAL feedback, `ApplyFactor`/`ApplyFlat` accept primitive `string heroStringId`, not sealed `Hero`. All 10 call sites extract `hero?.StringId` at the boundary.
- **Tests** — 8 new tests in `CareerPassiveServiceTests.cs` covering ApplyFactor/ApplyFlat (non-zero/null/empty/zero-magnitude) + RefreshCache snapshot-swap (second refresh replaces prior cache).

Build green, 1966/1966 tests pass. Test count: 1958 → 1966.

### Phase 9b — StartupResources Gold/Influence validation (Category 2 R3, closes #136)

P1 config validation gap. Pre-fix `Gold` (int) and `Influence` (float) were parsed via bare `int.Parse`/`float.Parse` — asymmetric with `PlayerGold` which already used `TryParse` + range validation. Concrete bugs: `gold="-500000"` flowed to `GiveGoldToHero(-500000)`; `influence="NaN"` returned NaN and the downstream `> 0f` guard rejected silently with no warning (csharp-architecture.md "Config Providers MUST Validate" — NaN BEFORE range check).

- **`Main/Features/StartupResources/StartupResourcesConfigProvider.cs`** — added `ParseGold(raw, cultureId)` and `ParseInfluence(raw, cultureId)` helpers using the same TryParse-and-validate pattern as `ParsePlayerGold`. Influence uses `FiniteFloatValidator.IsFiniteAtLeast(value, 0f)` so NaN/Infinity/negative all revert with a warning log.

Build green, 1958/1958 tests pass.

### Phase 9b — CustomBattles + QuickActions (closes #146, #162)

- **#162 (P2 v1.3.15-unverified) — CustomBattleSideVM.OnCultureSelection verification.** Confirmed via ilspycmd on installed `Modules/CustomBattle/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.CustomBattle.dll` that the v1.3.15 signature is `private void OnCultureSelection(BasicCultureObject selectedCulture)` — exact match for the patch. Added inline comment documenting the verification + assembly path so future readers don't have to re-verify, with explicit warning that the type is in `TaleWorlds.MountAndBlade.CustomBattle` (not `TaleWorlds.MountAndBlade` or `SandBox.GauntletUI`) — if a future TaleWorlds refactor moves the type, the entire Patch19 category would fail to apply.

- **#146 (P2) — QuickActions IsSearchAvailable per-save contract.** Pre-fix, `OnGameLoaded` and `OnTick` both unconditionally overwrote `_isSearchAvailable` with current MCM value, contradicting CLAUDE.md's "per-save toggle" promise. Re-architected with an explicit `_persistedVersion` SyncData tag (v0 = legacy, v1 = post-#146): legacy saves still reconcile against MCM (can't tell stored-true from missing-key); new saves are authoritative on load. Mid-game MCM toggle is now detected via transition observation (`_lastSeenMcmValue != currentMcm`) instead of unconditional per-tick overwrite — preserves both "per-save preference survives reload" AND "MCM toggle mid-game takes effect."

Build green, 1958/1958 tests pass.

### Phase 9b — Diplomacy prefix documentation + diagnostic logs (closes #152, #153)

Two P2 patches where the prefix returns false to skip vanilla. Documented the suppression semantics inline so future maintainers don't re-introduce duplicate-side-effect bugs.

- **#152 — AllianceCampaignBehavior.EndAlliance prefix.** Vanilla callers (`OnAllianceTimerExpired`, `OnWarDeclared`) sequence `EndAlliance(A,B)` → `AddAllianceDecision(A,B)`. When the prefix blocks `EndAlliance`, the subsequent `AddAllianceDecision` could in theory queue a "propose new alliance" for kingdoms that are still allied. Vanilla `AddAllianceDecision` (decompiled) checks `IsAlliedWith` before queuing, so the duplicate is filtered at that layer. Inline comment documents the mitigation + escalation path (Patch15 on `AddAllianceDecision` if reports surface). LogDebug surfaces blocked attempts for visibility.
- **#153 — DeclareWarAction.ApplyInternal prefix.** Prefix returns false → vanilla skips the `CampaignEventDispatcher.Instance.OnWarDeclared` dispatch. This is intentional (war never happened from vanilla's perspective) but documented inline: future "force-declare war through TAOM's own path" code must either use `DeclareWarAction.ApplyByX(...)` (emits the event) or manually dispatch `OnWarDeclared` via `CampaignEventDispatcher.Instance`. LogDebug surfaces blocked attempts.

Build green, 1958/1958 tests pass.

### Phase 9b — InitialChildGeneration config validation (Category 2 R3, closes #126)

Two P1s + one P2.

- **P1 — NaN/Infinity/range-violation in `FemaleRatio` + `ChildCountMultiplier`.** Pre-fix the config provider parsed `double?` values via Newtonsoft `Value<double?>()` with no semantic validation. NaN propagates through `_random.NextDouble() < NaN` as `false` → all-male children. Negative multiplier or NaN flows through `Math.Ceiling(baseCount * X) -> (int)` to nonsense. Added `ValidateRatio` (finite + [0, 1]) and `ValidateMultiplier` (finite + ≥ 0) helpers using `FiniteFloatValidator`. Applied to defaults + culture_overrides + clan_overrides.
- **P1 — `SelectTemplate` `ArgumentOutOfRangeException` on zero-adult clan.** Pre-fix the else branch indexed `[0]` on `AdultMaleHeroIds` when the outer `if` already proved both lists empty. Changed to return null; caller now `continue`s the loop to skip child creation for that clan.
- **P2 — `MinAge > MaxAge` ordering invariant.** Pre-fix this triggered `Random.Next(min, max)` to throw, aborting generation. Added `ValidateAgeOrdering` swap + log.
- Extended `FiniteFloatValidator` with `double` overloads for `IsFiniteInRange`/`IsFiniteAtMost`/`IsFiniteAtLeast` (matches the float overloads' semantics).

Build green, 1958/1958 tests pass.

### Phase 9b — P1 NRE null-guards (Category 2, closes #134 #135)

Two P1 null-guard fixes on hot paths.

- **#134 (P1) — TaomSiegeEventModel `party.MobileParty` NRE** — `party.MobileParty` is null for garrison defenders (`PartyBase.IsMobile=false`). Pre-fix the unguarded `party.MobileParty.HasPerk(...)` chain threw NRE on every garrison siege-defense calculation. Added `?.HasPerk(...) == true` short-circuit; fall-through treats null `MobileParty` as "no fire-perk engines" which matches vanilla's `false`-return-on-null-perk-check semantic.
- **#135 (P1) — TaomPartySpeedModel `Campaign.Current.MapSceneWrapper` NRE on per-tick path** — both `Campaign.Current` and `MapSceneWrapper` can be null during scene transitions. `CalculateFinalSpeed` fires per-party-per-tick on the world-map hot path. Added `?. ?? TerrainType.Plain` short-circuit so non-Forest fall-through skips the forest-feat block correctly.

Build green, 1958/1958 tests pass.

### Phase 9b — small model+UI fixes (Category 2, closes #138 #145 #168)

Three model/UI fixes batched.

- **#138 (P2 × 2) — ArmyTargeting TaomTargetScoreModel** — extracted the inline ternary + early-return branch out of `GetTargetScoreForFaction` per gamemodels.md rule 4. Added `IArmyTargetingService.GetEffectiveStrength(factionId, isBesieger, ourStrength)` and `ApplyTargetScoreModifiers(baseScore, isBesieger, factionId, targetSettlementId, committedTargetId)`. Model body now does only boundary extraction (factionId from MapFaction.StringId, isBesieger from missionType, committedTargetId from Army.AiBehaviorObject) and delegates.
- **#145 (P2) — Encyclopedia TaomInformationRestrictionModel** — replaced concrete-singleton coupling (`TaomSettings.Instance?.ShowAllEncyclopediaCharacters`) with injected `IEncyclopediaSettingsProvider`. New files: `IEncyclopediaSettingsProvider.cs` + `EncyclopediaSettingsProvider.cs` + `EncyclopediaIoC.cs`. Registered in `Main/IoC.cs` (new `EncyclopediaIoC.RegisterEncyclopediaFeature(container)` call after `ExecutionIoC`). `Main/SubModule.cs:302` now constructs the model with `IoC.Resolve<IEncyclopediaSettingsProvider>()`. Test file updated to use NSubstitute on the new interface.
- **#168 (P2 + P3) — TimeAcceleration UI** — `IsExtraFastForwardActive` now watches `Campaign.Current.TimeControlMode == CampaignTimeControlMode.StoppableFastForward` (Option A from the audit) instead of `SpeedUpMultiplier > 4f` (only mutated by cheat console). The button's selected-state visual now activates correctly. Known limitation documented inline: button is functionally redundant with vanilla's FastForwardButton; Option B (actual extra speed via service-raised SpeedUpMultiplier) is a future enhancement. P3 tooltip localized via `{=taom_extra_fast_forward_hint}Extra Fast Forward (E)` TextObject.

Build green, 1958/1958 tests pass.

### Phase 9b — Harmony cleanups batch (Category 2, closes #156 #159 #161 #163 #164)

Five mechanical patch-hygiene fixes across 9 files. All match audit-specified solutions verbatim. No behavior change in the normal path; better diagnostic visibility + threading correctness + perf on the degraded path.

- **#156 (P2 dormant) — BattleScenes** — `Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs`: marked `_isRetrying` as `volatile` (cross-thread visibility for the re-entry guard) and the class itself `static` per Harmony 2 convention. Dormant today (Patch0_BattleScenes category is commented out) but correct for re-enablement.
- **#159 (P2 v1.3.15-unverified) — BannerColor MobilePartyVisual** — `MobilePartyVisual_AddCharacterToPartyIcon_Patch.cs`: dropped the explicit param-type array (which included `typeof(ActionIndexCache).MakeByRefType()` for the two `in ActionIndexCache` params — `in` is modreq-qualified in IL and Harmony 2's AccessTools is inconsistent about matching modreq). Verified via ilspycmd that the method has exactly one overload in v1.3.15, so name-only resolution is unambiguous.
- **#161 (P2 perf) — ArmyTargeting Patch22** — `AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs`: cached the 3 IoC.Resolve calls (`IArmyTargetingService`, `IArmyTargetingSettingsProvider`, `IModLogger`) in static fields via lazy `??=` init. Patch fires ~500-2000 calls/AI-cycle per feature doc; each pre-fix invocation walked the DryIoc registration table 3 times. Also marked class `static` (#151 pattern).
- **#163 (P2) — CharacterCreation SpawnNonHuman finalizer** — `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`: kept the specific `ArgumentNullException(ParamName="key")` swallow (known TaleWorlds horse-data bug), but generic `NullReferenceException` now logs before suppressing so real bugs in the target method surface in diagnostics instead of being masked forever.
- **#164 (P3 consolidated) — Misc patch cleanups** — 6 files:
  - `Patch35_OOBHeroItem_GetCaptainTooltip.cs` + `Patch35_Formation_SetMovementOrder.cs` — bare `catch {}` replaced with one-shot logging via `_exceptionLogged` flag. `Patch35_Formation_SetMovementOrder` also gained `?` nullable annotations on its lazy-init static fields.
  - `CulturalFeats/Hooks/Campaign_InitializeDefaultCampaignObjects_Patch.cs`, `SpecialResources/Hooks/PartyCharacterVM_InitializeUpgrades_Patch.cs`, `SpecialResources/Hooks/PartyScreenLogic_UpgradeTroop_Patch.cs` — added missing `[HarmonyPostfix]` attribute (works today via Harmony's naming convention; explicit attribute is defensive against a future Harmony version that tightens binding rules).
  - `SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs` — added missing `[HarmonyPrefix]` attribute (same rationale).
  - `SmartCavalryAI/Hooks/Patch31_FormationSetMovementOrder.cs` — added explicit `new[] { typeof(MovementOrder) }` param-type array on `[HarmonyPatch]` for defensive consistency with sibling Patch35.
  - `SubModule.cs` (lines 470-475) — added missing `else IoC.Resolve<IModLogger>().LogWarning(...)` fallbacks on the two `MapConversationTableau.SpawnOpponent*` manual `_harmony.Patch(...)` sites. Matches the diagnostic pattern from #122/#158.

Build green, 1958/1958 tests pass.

### Phase 9b — close #151 + #155 (Category 2 patch hygiene + threading hardening)

Two small audit fixes batched together. Both are pure-mechanical, single-file changes matching their audit's specified solutions verbatim.

- **#151 (P2)** — `Main/Features/HeroRace/Hooks/ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs` — `public class` → `public static class`. Harmony 2 attribute-based patches require static; non-static causes unpredictable application behavior. All other TAOM patches were static; this one was the outlier. The audit-flagged "possible no-op duplicate of vanilla" sub-finding is deferred (separate scope — needs vanilla `ActionSetCode.GenerateActionSetNameWithSuffix` decompile + behavioral comparison).
- **#155 (P2)** — `Main/Features/SmartCavalryAI/CavalryChargeService.cs` — added `private readonly object _lock = new();` and wrapped `_states` accesses (GetState, OnMissionEnd, HandleChargeOrder, Tick) plus the downstream `state.State = ...` mutations in `lock (_lock) { ... }`. Mirrors the FormationLayoutService pattern Codex review #35 established for the sister service. Today Patch31's team filter structurally prevents enemy-team threads from reaching the service, but the absence of locking was fragile — a future refactor of Patch31 could re-introduce the race silently. Belt-and-braces lock now.

Build green, 1958/1958 tests pass.

### Fix — TaomPregnancyModel heroAge truncation regression (Codex HIGH catch)

Codex independent review of the Phase 9b autonomous-run production changes (commits `ec054a4..303adbf`) caught a HIGH regression I introduced in commit `57e9d9b` (#179 ComputeBaseChance extraction). The extraction commit passed `(int)hero.Age` to the new helper, truncating fractional age toward zero — a 44.9-year-old hero computed identically to a 44-year-old, materially shifting late-window pregnancy chance vs vanilla `DefaultPregnancyModel` which uses `Hero.Age` (float) directly.

- **`Main/Features/RaceAge/Models/TaomPregnancyModel.cs`** — replaced `heroAge: (int)hero.Age` with `heroAge: hero.Age`; changed `ComputeBaseChance` parameter from `int heroAge` to `float heroAge`. Int literals in the existing tests implicit-convert to float — no test breakage. Inline comment documents the regression class.
- **`TAOM.Tests/Features/RaceAge/TaomPregnancyModelTests.cs`** — added `ComputeBaseChance_FractionalAge_PreservesPrecision` regression test. Asserts that ages 44 / 44.5 / 45 produce three distinct monotonically-decreasing values. If `heroAge` were int-truncated, 44 and 44.5 would match — test would go red.
- **`docs/reviews/rca-phase9b-autonomous-codex-review-2026-05-13.md`** — NEW. Documents the extraction-without-type-preservation root-cause pattern, walks through why all 5 deep-review agents missed it (Codex's independent re-read with adversarial framing was the load-bearing safety net), and proposes two new feedback memories for future extraction work.
- One MEDIUM Codex finding deferred: `Patch35_Formation_SetMovementOrder` team filter is necessary-but-not-sufficient if a player-team formation is AI-controlled (player not general OR delegates command) — the postfix can still execute on the async AI thread for player-team formations. Audit issue #149 specified the team filter as "the simpler fix"; full hardening (lock / main-thread marshal / PlayerOwner gate) is Phase 10 candidate, not Phase 9. Tracked in the RCA.

1957 → 1958 tests, all passing.

### Build — auto-mirror Win64_Shipping_Client → Win64_Shipping_wEditor on every deploy

Map-maker hand-off testing of CS_Road exposed a long-standing footgun: `Bannerlord.BuildResources`'s `CopyBinariesWindows` target is hardcoded to `Win64_Shipping_Client`, so the standalone modding kit (`Win64_Shipping_wEditor`) silently launched stale TAOM.dll + companions until someone ran `cp -v Win64_Shipping_Client/* Win64_Shipping_wEditor/` by hand. Easy to forget; resulting "code change has no effect in editor" reports waste hours.

- **`Main/TAOM.csproj`** — added `MirrorWin64ShippingClientToEditor` target (`AfterTargets="PostBuildCopyToModules"`). Globs `<game>/Modules/TAOM/bin/Win64_Shipping_Client/*.*` and copies into `Win64_Shipping_wEditor/` with `SkipUnchangedFiles="true"`. Inherits the same `DisableModuleCopy != 'true'` + `Exists($(GameFolder))` + `ModuleId != ''` gate as the deploy itself, so unit-test builds (`-p:DisableModuleCopy=true`) skip cleanly. Emits `TAOM: mirrored <N> files Win64_Shipping_Client -> Win64_Shipping_wEditor` at high importance so the build log shows it ran. Verified end-to-end: deleted TAOM.dll from wEditor → ran `./build.ps1` → wEditor restored to 9-file parity with Client (identical sizes + timestamps).
- **`docs/features/scene-scripts.md`** — "Editor compatibility" section updated. Removed the obsolete `cp -v` procedure and explained the new auto-mirror target.

### Fix — CS_Road comprehensive diagnostic logging

Map-maker hand-off testing reported "step 5 (click Generate) does nothing." Audit found three log-coverage gaps that masked the real cause: (1) `LogTag = 1L<<44` is filtered out by the engine's debug-tag mask in both editor and in-game log windows — even our existing yellow warnings were being silently swallowed; (2) four silent return paths in `GenerateMesh` (`!entity.IsValid`, `Scene == null`, `samples.Count < 2`, `triangles.Count == 0`) had zero logs; (3) no positive-success log on the happy path, so the map maker couldn't distinguish "click reached code, succeeded" from "click never reached code at all."

- **`Main/SceneScripts/CS_Road.cs`** —
  - `LogTag` switched from `17592186044416uL` (= `1L << 44`) to `0uL` so all `Debug.Print` calls are unconditionally surfaced. Comment added explaining why.
  - New `LogInfo(string)` helper alongside `LogWarn` for white-text non-warning lines.
  - `OnEditorVariableChanged` `Generate` case now logs `Generate button clicked.` before invoking `GenerateMesh`, so the map maker can distinguish event-routing failure from generation failure.
  - `GenerateMesh` now logs `GenerateMesh start.` at entry, fills in the 4 previously-silent return paths with explanatory `LogWarn` lines, and logs `generated mesh from path '<X>' (totalDistance=<X>m, <N> samples, <N> triangles, material='<Y>').` on success.

Build green. No behavior change beyond log surfacing. CS_Road remains engine-bound and is verified manually in the editor (helpers retain their 67-test coverage).

### Docs — CS_Road map-maker quickstart

A non-developer-facing one-page guide for map authors. The existing `docs/features/scene-scripts.md` hand-off checklist is buried under architecture / license / clean-room sections; the new doc distills only the operational content (prerequisites → 5-step workflow → 16-knob table → StepCurve cheatsheet → 3-step diagnostic ladder → cleanup gotcha → `Live`-mode warning) so a non-coder can follow it top-to-bottom without scrolling past irrelevant content.

- **`docs/scene-scripts/map-maker-quickstart.md`** — new file. Pulls field defaults from `Main/SceneScripts/CS_Road.cs:32-47` and StepCurve semantics from `docs/scene-scripts/specs/cs-road.md:47-60`. Covers both editor targets (`Win64_Shipping_wEditor` and the in-game scene editor during an active campaign). Troubleshooting reorganized into a 3-step diagnostic ladder reflecting the new log surface (click reception → bail reason → invisible-mesh debugging).
- **`docs/features/scene-scripts.md`** — added a one-line pointer at the top of the existing "How to verify CS_Road in the modding kit" section linking to the map-maker version. The architecture-doc version stays in place for engineers.

### Phase 9b — close #160 CharacterSelection transpiler soft-fail (Category 2 R5)

P2 degradation fix. `RefreshCharacterEntityAuxPatch.Transpiler` previously threw `ArgumentException` at three points (missing ctor / missing ActionSet / missing IL pattern). Because the patch is applied via `PatchCategory("Late_Transpiler")` in `OnGameInitializationFinished`, any throw crashed the mod during game initialization rather than just disabling the one transpiler — bricking startup even though no other TAOM feature is affected.

- **`Main/Features/CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs`** — replaced all 3 `throw new ArgumentException(...)` calls with `LogTranspilerDegradation(detail) + return instructions` (unchanged). One-shot error log via cached `IModLogger.LogError` per failure cause, then graceful fallback so the game can boot. Vanilla `BodyGeneratorView.RefreshCharacterEntityAux` continues to run unmodified; the only consequence is the face-generator action-set injection doesn't apply this session.

### Phase 9b — close #157 SettlementGuards bare-catch diagnostic (Category 2 R5)

P2 diagnostic-visibility fix. `GuardsCampaignBehavior_TakeGuardAgentData_Patch.Prefix` reflected `PrepareGuardAgentDataFromGarrison` and called `Invoke(null, ...)` assuming static. The audit flagged this as v1.3.15-unverified — but per `ilspycmd` on installed `SandBox.dll`, the v1.3.15 signature IS `private static AgentData PrepareGuardAgentDataFromGarrison(CharacterObject, bool, bool)` — the static call shape is correct. The real remaining issue was the bare `catch {}` swallowing any unexpected exception with zero log output, masking future TaleWorlds drift.

- **`Main/Features/SettlementGuards/Hooks/GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs`** — replaced bare `catch {}` with `catch (Exception ex)` + one-shot logging via `IModLogger.LogError`. `_exceptionLogged` guard prevents per-spawn log spam (the patched method fires on every settlement enter). Vanilla fallback (`return true`) preserved. v1.3.15 staticness explicitly documented in the catch comment so future readers don't have to re-verify.

### Phase 9b — close #150 MapConversationTableau color writes silently failed (Category 2 R5)

P1 silent-failure fix. Pre-fix, the leader + bodyguard `MapConversationTableau` Postfixes mutated `AgentVisualsData.ClothColor1Data/ClothColor2Data` AFTER `AgentVisuals` was constructed. Because `MBAgentVisuals.CreateAgentVisuals(...)` already pushed the initial deterministic colors to native renderer in the ctor, the post-construction C# field writes were silent no-ops — conversation tableau leader / bodyguard always rendered with vanilla `CharacterHelper.GetDeterministicColorsForCharacter` output.

- **`Main/Features/BannerColorPersistence/Hooks/MapConversationTableau_SpawnOpponentLeader_Patch.cs`** + **`MapConversationTableau_SpawnOpponentBodyguard_Patch.cs`** — added cached `_refreshMethod` resolution for `AgentVisuals.Refresh(bool needBatchedVersionForWeaponMeshes, AgentVisualsData data, bool forceUseFaceCache = false)` (verified via ilspycmd against installed v1.3.15 `TaleWorlds.MountAndBlade.View.dll` — signature identical to decompile). After the existing `ClothColor1/2` fluent setters, the Postfix now invokes `Refresh(false, visData, false)` to re-run `AddTeamColorToMesh` / `AddSkinArmorWeaponMultiMeshesToEntity` against the mutated data — the cloth colors finally reach the GPU.
- This is the alternative ("Option B") from the audit's fix sketch: the audit suggested either moving to a Prefix on `AgentVisuals.Create` (Site 5 pattern, hard because the Prefix has no character context) or finding a native SetClothColor API. ilspycmd showed no native push API exists — `AgentVisualsData.ClothColor1/2(uint)` are just fluent setters on private-set properties. `AgentVisuals.SetClothingColors(uint, uint)` (line 886) is the same — just calls the fluent setters. The Refresh-after-mutation pattern works because Refresh's mesh-build path reads `_data.ClothColor1Data/ClothColor2Data` at call time, not from the value captured at ctor time.

### Phase 9b — close #149 CompanionTactics Patch35 team filter (Category 2 R5)

P1 concurrency fix. Pre-fix, `Patch35_Formation_SetMovementOrder.Postfix` mutated `TroopStanceManager._stances` for every team's formations — including enemy formations whose movement orders are issued from the async AI tick (`Mission.doAsyncAITick → TickAgentsAndTeamsAsync → BehaviorXxx.TickOccasionally → Formation.SetMovementOrder`). .NET Framework 4.7.2 `Dictionary<TKey,TValue>` is not concurrent-safe, so concurrent worker-thread `Remove` (Postfix) racing main-thread `TryGetValue`/`SetStance` (BattleActionBarMissionView) could produce `KeyNotFoundException` or silent bucket-chain corruption.

- **`Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs`** — added `if (__instance.Team != Mission.Current?.PlayerTeam) return;` before the `_stances.ClearStance(...)` call. One-line filter matching the audit's specified fix verbatim. Stances are player-team-only semantically, so the filter is simpler than adding lock-based synchronization.

### Phase 9b — close #181 CharacterCreation × HeroRace race-ID round-trip (Category 4c)

The cross-feature contract from Phase 6 #171: a player race assigned at OnCharacterCreationFinalize must survive save/load via RacePersistence. Existing tests verified Capture and Restore independently but never wired them into a single round-trip through the SyncData serialization handoff.

- **`TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs`** — added `CaptureRestore_RoundTrip_PreservesPlayerRaceSetByCharacterCreation`. Simulates the full save/load cycle: CC sets player race=2 (elf) → CaptureHeroRaces → SyncData(saving) via a hand-rolled `RoundTripDataStore` IDataStore stub that captures the dict → NEW service instance (simulating Bannerlord process restart) → SyncData(loading) re-injects the snapshot → fresh adapter shows heroes at race=0 → RestoreHeroRaces re-applies the persisted race-2. NSubstitute couldn't easily mock `SyncData<T>(string, ref T)` with the Do-callback pattern (ref args are tricky), so the test uses a 30-line hand-rolled stub.
- 1956 → 1957 tests, all passing.

### Phase 9b — close #183 HeroRace OnSessionLaunched + persistence wiring tests (Category 4c)

Pre-this, `RacePersistenceBehaviorTests` covered only `SyncData` delegation. `OnSessionLaunched` (which re-applies captured race IDs to live heroes on load) and `OnBeforeSave` (which captures them) had ZERO tests.

- **`TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs`** — 2 new source-content assertions:
  - `RegisterEvents_SubscribesOnBeforeSaveAndOnSessionLaunched` — pins both `CampaignEvents.OnBeforeSaveEvent` (capture) and `CampaignEvents.OnSessionLaunchedEvent` (restore) subscriptions in the production source. Drop either subscription and the cross-feature contract with CharacterCreation (Phase 6 #171, race IDs from CC must round-trip through save/load) silently breaks.
  - `MainSubModule_AndIoC_RegisterRacePersistenceBehavior` — pins `AddBehavior` + `HeroRaceIoC.RegisterHeroRaceFeature` wiring.
- 1954 → 1956 tests, all passing.

### Phase 9b — close #189 + #190 SmartCavalryAI × MixedFormations handshake tests (Category 4c)

The two-feature contract: SmartCavalryAI owns cavalry formation behavior; MixedFormations defers via two `RepresentativeIsCavalry` guards in `FormationLayoutService` (lines 74 and 191). Phase 7 audit found both guards had ZERO tests — a refactor of either feature could silently re-introduce the P1 charge-line overwrite Codex 2026-05-06 already caught.

- **`TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs`** — 3 new tests in a "SmartCavalryAI × MixedFormations handshake" section:
  - `ComputeUnitPlanePosition_CavalryFormation_ReturnsNull_HonoringSmartCavalryHandshake` pins the line-74 guard.
  - `IsMixedFormation_CavalryFormation_ReturnsFalse_HonoringSmartCavalryHandshake` pins the line-191 guard.
  - `CavalryHandshake_NonCavalry_DoesNotShortCircuit_BaselineAssertion` baseline that a polarity-flip (returning null/false for ALL formations) would catch.
- 1951 → 1954 tests, all passing.

### Phase 9b — close #177 FiefManagement behavior-callback coverage (Category 4b)

ADR-008 80% behavior-hook coverage target was entirely unmet for `FiefHubCampaignBehavior` (5 callbacks, zero tests) even though `FiefHubService` had 22 tests on 8 methods.

- **`TAOM.Tests/Features/FiefManagement/FiefHubCampaignBehaviorTests.cs` — NEW (7 tests).** Three direct-delegation tests (`OnNewGameCreated_CallsPresenterReset`, `OnGameLoaded_CallsPresenterReset`, `SyncData_DoesNotTouchDataStore`) reflection-invoke the private handler methods and verify the mocked presenter / data store. One type-sanity test (`Behavior_IsCampaignBehaviorBase`). Three source-content wiring tests for the engine-coupled callbacks: `RegisterEvents_SubscribesAllExpectedCampaignEvents` asserts all 3 subscriptions are in `FiefHubCampaignBehavior.cs`; `OnSessionLaunched_RegistersFiefHubMenuAndOptions` asserts the `fief_hub` menu + 4 menu options are registered; `MainSubModule_AddsFiefHubCampaignBehavior` asserts the `AddBehavior` call survives in `Main/SubModule.cs`.
- 1944 → 1951 tests, all passing.

### Phase 9b — fix NamedCompanions Entity State Matrix completion (#127 + #184)

P1 cross-feature fix for the Review #23 regression class plus its missing state-matrix tests. Pre-fix, Prisoner companions (mobile captor, `PartyBelongedTo=null`, no settlement) and Fugitive companions (`HeroState=Fugitive`, all party fields null) slipped through ALL guards in `EnsureCompanionsPlaced` and got force-placed via `EnterSettlementAction` every load — corrupting captor prison rosters and resetting fugitive state to Active. Plus a P1 singleton-state bug: `_spawned` survived across campaigns in the same Bannerlord process so campaign 2 silently skipped all companion placement.

- **`Main/Adapters/INamedCompanionAdapter.cs`** + **`NamedCompanionAdapter.cs`** — added `IsHeroPrisoner` (Hero.IsPrisoner ∪ PartyBelongedToAsPrisoner != null) + `IsHeroFugitive` (HeroState == Fugitive); broadened `IsRecruitedOrInParty` to include `PartyBelongedToAsPrisoner` per #127 P2.
- **`Main/Features/NamedCompanions/INamedCompanionService.cs`** + **`NamedCompanionService.cs`** — added `ResetSession()` clearing `_spawned`. `EnsureCompanionsPlaced` now checks IsHeroPrisoner + IsHeroFugitive before placement.
- **`Main/Features/NamedCompanions/NamedCompanionBehavior.cs`** — subscribed `ResetSession()` to `OnNewGameCreatedEvent` (NOT `OnSessionLaunchedEvent` — see RCA). Codex review caught that `OnSessionLaunched` fires AFTER `OnNewGameCreatedPartialFollowUpEvent`, which would have cleared the latch within the same session. Per decompiled `CampaignEvents.cs:2078-2084`, `OnNewGameCreatedEvent` fires FIRST (line 2080), then the partial-follow-up loop (line 2083) — so the reset correctly lands before `SpawnCompanions` runs and leaves the latch set.
- **`TAOM.Tests/Features/NamedCompanions/NamedCompanionServiceTests.cs`** — 3 new tests: `EnsureCompanionsPlaced_PrisonerCompanion_SkipsPlacement`, `EnsureCompanionsPlaced_FugitiveCompanion_SkipsPlacement`, `ResetSession_AllowsSpawnCompanionsAgainInSameProcess`.
- **`docs/reviews/rca-named-companions-state-matrix-2026-05-13.md` — NEW.** Documents the lifecycle-ordering near-miss for the audit-spec-vs-codebase pattern. Codex independently re-read the decompiled source and caught what the audit's fix sketch got wrong.
- `/codex-verify` MEDIUM finding addressed pre-commit. 1941 → 1944 tests, all passing.

### Phase 9b — close #186 Spider SpawnSpiders Monster lookup tests (Category 4e)

The Phase 7 audit body slightly overstated the gap on `SpiderSpawnerService` — `ComputeSpawnPosition` math IS tested at `SpiderSpawnerServiceTests.cs:84-114` (radius bounds + z/w preservation). The actual gap was the **monster lookup** path and the **lookup ID contract**.

- **`TAOM.Tests/Features/Spider/SpiderSpawnerServiceTests.cs`** — added 2 tests: `SpawnSpiders_MonsterNotFound_ReturnsEmptyAndLogs` (symmetric partner to the existing `SpawnSpiders_AnchorCharacterNotFound_ReturnsEmptyAndLogs` — covers the LOTRLOME_Armory-not-loaded / "spider" id renamed branch); `SpawnSpiders_LookupsByExpectedIds` (pins the `"spider"` Monster id and `"taom_spider_creature"` character id constants — a rename in production without an audit-trail fix would break the silent path).
- Team-assignment behavior tests (verifying `AgentBuildData.Team(team)` is invoked) deferred — `AgentBuildData` is a fluent-builder over sealed engine types and can't be observed without engine state.
- 1937 → 1941 tests, all passing.

### Phase 9b — close #185 AdvancedCombat SpatialGridDebugService minimum-coverage (Category 4e)

ADR-008 minimum-coverage tests for `SpatialGridDebugService`. The audit's "consumption path unknown" framing was wrong — `AdvancedCombatBehavior.OnMissionTick` calls `RenderDebugVisualization()` every 2 seconds — but the `RenderDebugVisualization` body is 100% engine-coupled (sealed `Agent.Main` + `Input.IsKeyDown` + `SpatialGrid.Instance` + `MBDebug.RenderDebugSphere` statics) so a full behavior test would need an ADR-007 refactor introducing `IAgentSourceAdapter`/`IInputAdapter`/`ISpatialGridAdapter`/`IDebugRendererAdapter`. That's out of scope per #185.

- **`TAOM.Tests/Features/AdvancedCombat/SpatialGridDebugServiceTests.cs` — NEW (2 tests).** Constructs without throwing (protects DryIoc Singleton lazy-init), implements `ISpatialGridDebugService` (protects `AdvancedCombatBehavior.OnMissionTick` consumer). Mirrors the `#195 TroopWeightHooksTests` pattern.

### Phase 9b — close #179 RaceAge TaomPregnancyModel ComputeBaseChance tests (Category 4d)

Extracted the pure-math portion of `GetDailyChanceOfPregnancyForHero` to a static helper (`TaomPregnancyModel.ComputeBaseChance`), mirroring the `TaomAgeModel.ApplyRaceAgeLimits` pattern. The 5 branches the Phase 7 audit (#179) flagged as untested are now exercisable without the sealed-`Hero` coupling. Full ADR-007 refactor (introduce `IHeroAgeInfo` adapter, move logic into `IRaceAgeService`) is tracked separately as #131.

- **`Main/Features/RaceAge/Models/TaomPregnancyModel.cs`** — extracted `ComputeBaseChance(int heroAge, int comesOfAge, int fertilityEnd, int childCount, int clanTier, int aliveLords, bool playerOrSpouseInvolved, float raceFertilityModifier)` as a `public static` helper. The override body now does the engine-coupled extraction (`hero.CharacterObject.Race`, `hero.Spouse`, `hero != Hero.MainHero`, perk lookups) then delegates to the pure-math helper.
- **`TAOM.Tests/Features/RaceAge/TaomPregnancyModelTests.cs` — NEW (10 tests).** Age-factor branches (peak at `comesOfAge`, decayed at `fertilityEnd`, zero-window fallback), child-count quadratic decay (1 child, 3 children), population-factor branch (player-involved short-circuit, NPC overpopulation, NPC moderate), race fertility multiplier (dwarven half, sterile zero).
- 1927 → 1937 tests, all passing.

### Phase 9b — fix + regression test SpecialResources × CareerSystem discount-debit parity (#174, #194)

Cross-feature bug + its missing regression test, closed together. Pre-fix, `ClampUpgradeCount` / `CanAffordUpgrade` / `SpendForUpgrade` all applied the `CustomResourceUpgradeCostModifier` career passive (effective cost), but `QueueUpgradeSpend` debited the bare base cost — so a player with a -30% career discount queued upgrades at the discounted gate then got debited the full base price at `CommitSession`. Silent overpay by the discount percentage.

- **`Main/Features/SpecialResources/SpecialResourceService.cs::QueueUpgradeSpend`** — one-line fix replacing `cost.UpgradeCost * count` with `GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, count)`. `heroId` was already a parameter — the gap was the service not threading it through to the effective-cost helper.
- **`TAOM.Tests/Features/SpecialResources/SpecialResourceServiceTests.cs`** — two regression tests: `QueueUpgradeSpend_WithPassiveDiscount_DebitsEffectiveCost` (base 10, -30% → 7 debit) and `QueueUpgradeSpend_NoCareerDiscount_DebitsBaseCost` (no discount → bare cost). The latter is the negative-case partner that pins down "the fix didn't accidentally change behavior when no discount is active."
- `/codex-verify` confirmed CLEAN — fix correctly aligns the 4 effective-cost call sites (`CanAffordUpgrade`, `SpendForUpgrade`, `ClampUpgradeCount`, `QueueUpgradeSpend`).
- 1925 → 1927 tests, all passing.

### Phase 9b — close #195 TroopWeight 4 IOn* hook implementation tests (Category 4a)

ADR-008 minimum-coverage tests for the four `IOn*` hook implementations the Phase 7 audit (#195) flagged as having zero tests. Full behavior tests would require an ADR-007 adapter refactor (the hooks accept sealed `PartyBase`, `MBBindingList<PartyCharacterVM>`, `RecruitmentVM` and call static `MBTextManager.SetTextVariable`) which the audit explicitly placed out of scope. What we CAN test without engine state is now covered.

- **`TAOM.Tests/Features/TroopWeight/TroopWeightHooksTests.cs` — NEW (10 tests).** Per-hook: construction with substituted deps + interface implementation check. For the two `PartyBase*` hooks: explicit null-receiver early-exit assertion (production catches all exceptions inside try/catch so a future refactor that drops the explicit `null` guard would silently mask the bug; this test asserts the guard works without exception AND that `__result` is preserved unchanged).
- The 4 hooks covered: `PartyBaseNumberOfAllMembersHook`, `PartyBaseNumberOfRegularMembersHook`, `PartyVMPopulatePartyListLabelHook`, `RecruitmentVMRefreshPartyPropertiesHook`.
- Deliberately out of scope: full behavior tests requiring engine init (deferred to an ADR-007 refactor session).
- 1915 → 1925 tests, all passing.

### Phase 9b — close #193 SiegeDismount MissionBehavior wiring test (Category 4a, mechanism-corrected)

The Phase 4 audit originally claimed SiegeDismount uses manual `_harmony.Patch(...)` like SettlementGuards (#192). Phase 9a verification (Codex confirmed) corrected the mechanism: SiegeDismount wires via `mission.AddMissionBehavior(new SiegeDismountMissionBehavior())` inside `Main/SubModule.cs::OnMissionBehaviorInitialize`. The wiring is uniquely vulnerable in TWO ways: drop the `AddMissionBehavior` line and the behavior never registers (silent broken siege dismount), or drop the `SiegeDismountIoC.RegisterSiegeDismountFeature` line and the behavior ctor's `IoC.Resolve<ISiegeDismountService>()` throws at mission start.

- **`TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs` — NEW (3 tests).** `MainIoCConfigure_IncludesSiegeDismountFeatureRegistration` (source-content), `MainSubModule_AddsSiegeDismountMissionBehaviorOnMissionInit` (two-part assertion: the literal call AND the `OnMissionBehaviorInitialize` method that contains it — protects against the call surviving inside a comment or unreachable branch), `SiegeDismountMissionBehavior_IsMissionBehavior_LogicType` (type sanity: must inherit `MissionBehavior` so `AddMissionBehavior` accepts it).
- 1912 → 1915 tests, all passing.

### Phase 9b — close #192 SettlementGuards manual-Harmony wiring test (Category 4a)

Mirror of the #191 pattern, scoped to SettlementGuards' two manual `_harmony.Patch(...)` sites. Unlike most TAOM features, SettlementGuards has no `[HarmonyPatchCategory]` because both target methods are private instance methods that AccessTools can only resolve at runtime — the patches are applied directly from `Main/SubModule.cs` via `_harmony.Patch(...)`. That makes the wiring uniquely vulnerable to a Messengers-class regression.

- **`TAOM.Tests/Features/SettlementGuards/SettlementGuardsWiringTests.cs` — NEW (4 tests).** Source-content assertions cover the 3 wiring-catalog requirements: `MainIoCConfigure_IncludesSettlementGuardsFeatureRegistration`, `MainSubModule_AppliesManualHarmonyPatches` (both `TargetMethod()` call sites — `TakeGuardAgentData` + `GetSuitableSpear`), `MainSubModule_InitializesBothPatchClassesWithService` (the `Initialize(_service)` calls so the Prefix's static `_service` isn't null). One DryIoc smoke test (`RegisterSettlementGuardsFeature_RegistersService`) verifies the service + config provider resolve after registration.
- `SettlementGuardService` pulls a cross-feature `IRandomProvider` dep from TroopProgression; the smoke test registers it before calling `RegisterSettlementGuardsFeature` to mirror what `Main/IoC.cs` guarantees by ordering.
- 1908 → 1912 tests, all passing.

### Phase 9b — close #191 Messengers wiring regression test (Category 4a)

The audit-motivating regression-class root. The Messengers crash (#121) shipped because `Main/IoC.cs::Configure` never called `MessengerIoC.RegisterMessengerFeature(container)` and `Main/SubModule.cs::OnGameStart` never added `MessengerCampaignBehavior` to the campaign starter. Build was clean, 1903 unit tests passed, encyclopedia hero-click NRE was the first signal in-game. None of the existing Messenger tests asserted the feature was actually plugged into the global IoC catalog.

- **`TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs` — NEW (5 tests).** Two source-content regression tests directly catch the #121 class: `MainIoCConfigure_IncludesMessengerFeatureRegistration` reads `Main/IoC.cs` and asserts it contains the `MessengerIoC.RegisterMessengerFeature(container);` call; `MainSubModule_AddsMessengerCampaignBehavior` reads `Main/SubModule.cs` and asserts it contains the `AddBehavior(IoC.Resolve<MessengerCampaignBehavior>())` call. Plus two DryIoc smoke tests (`RegisterMessengerFeature_RegistersBehavior_WithAllDependencies`, `RegisterMessengerFeature_RegistersService`) verifying that after the feature module's registration call, the behavior + all 3 sub-services resolve from the container. Plus a `Behavior_IsCampaignBehaviorBase` type sanity check.
- The two source-content assertions are unconventional but EXACTLY the regression-grade tests #121 demanded: revert either `IoC.cs` line and the test goes red. Path resolution mirrors `ConfigIdValidationTests.FindModuleDataPath` (walk up from current dir until file found, `Assert.Inconclusive` if not in repo context).
- 1903 → 1908 tests, all passing.

### Phase 9b — close 2 audit-impl mechanical-wiring issues (Category 1: Mechanical wiring)

Two one-line wiring fixes in `Main/SubModule.cs`. Both target the same patch-init block (banner-color manual Harmony patches); no behavior change beyond completing the patch wiring.

- **#122 BannerColorPersistence MobilePartyVisual Initialize never called (P2 audit-wiring)** — `Main/SubModule.cs:180` added `MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize(bannerColorService, bannerHeroAdapter);` next to the sibling manual-patch Initialize calls (AgentVisuals, MapConversationTableau). The manual `_harmony.Patch(...)` at line 447 was already binding the Postfix correctly, but the static `_service`/`_heroAdapter` fields stayed `null` — so the Postfix's `_heroAdapter?.GetClanColorInfo(...)` always returned null and the postfix early-exited. World-map party icons now receive clan colors as designed.
- **#158 BannerColor AgentVisuals.Create missing LogWarning fallback (P3 audit-impl)** — `Main/SubModule.cs:459` added `else IoC.Resolve<IModLogger>().LogWarning(...)` mirroring the sibling fallbacks on `MobilePartyVisual` (line 451) and SettlementGuards spear/guard data (lines 437, 442). If `AgentVisuals.Create` is renamed or moved by a future TaleWorlds patch, the silent no-op now becomes a one-line diagnostic.

### Phase 0-9a audit artifacts checkpoint

Committing the cumulative deliverables of the TAOM feature audit (Phases 0-8 manifest/wiring/cluster reviews + Phase 9a verification) as one atomic artifact commit so the next Phase 9b fix-batch session has a clean diff.

- **`docs/audits/`** — 33 files: `README.md`, `feature-manifest.md` (43 features classified), `wiring-matrix.md` (Phase 1), `cluster-{gamemodels,campaign-behaviors,harmony-patches,ui,cross-feature}.md` (Phases 2-6), `test-coverage.md` (Phase 7), `docs-gaps.md` (Phase 8), `phase-{1-9}-kickoff.md` (per-phase briefs), `session-prompts.md`, `triage-input.json` + per-batch JSONs (raw `gh issue list` snapshots — reproducibility), `triage-input-index.txt`, `triage-results.md` (Phase 9a master) + per-batch detail (`triage-results-{A1,A2,B,C,D}.md`), `phase-9-fix-queue.md` (77 remaining VALID issues grouped by category).
- These artifacts document the multi-phase audit that produced 78 GitHub issues (`audit-impl`, `audit-wiring`, `audit-tests`, `audit-docs`). Phase 9a verification confirmed 95% audit accuracy (1 STALE + 1 sub-FP + 2 SEVERITY-DRIFT of 78). Phase 9b is consuming the resulting queue across multiple sessions.

### Phase 9b — close 4 audit-docs issues (Category 5: Doc updates)

First Phase 9b batch after the 9a verification (which validated 78 audit findings, closed #154 as STALE, and produced the 77-issue fix queue in `docs/audits/phase-9-fix-queue.md`). Doc-only edits; build + tests untouched at 1903/1903.

- **#196 Execution doc missing (P1)** — wrote `docs/features/execution.md` from `TEMPLATE.md`. Documents the alignment-aware execution feature: `Patch14_Execution` patches + `TaomExecutionRelationModel` GameModel + `IAlignmentService` + `IOnExecutionAction` decision hook + `alignment.json` config (18 kingdoms mapped to free/evil/neutral). Cross-references the existing `alignment-aware-execution.md` deep-dive doc. This silences the `detect-docs-gaps.sh` SessionStart hook that has flagged Execution on every session since Phase 0.
- **#197 CompanionTactics stale build-disabled note (P3 — drift-reclassified from P2)** — removed the `TEMP-SMARTCAVALRY-EXCLUDE` paragraph from `docs/features/companion-tactics.md`. Commit `0cc457f` (2026-05-07) restored the integration 6 days before the Phase 8 audit; the doc was stale at audit time. Codex verified.
- **#198 AdvancedCombat stale "no tests" claim (P2)** — updated `docs/features/advanced-combat.md` Tests section to reflect `BoneCollisionServiceTests.cs` (11 tests). Documented remaining gaps (`SpatialGrid`, `CustomAttacksUtils`, `SpatialGridDebugService.RenderDebugVisualization` — cross-referenced to #185).
- **#199 Warg stale "no dedicated test files" claim (P2)** — updated `docs/features/warg-combat.md` Tests section to reflect `WargAttackServiceTests.cs` (7 tests). Cross-referenced #178 (ADR-007 blocker — the 2 sealed-`Agent` methods remain untestable until `IWargAttackService` is refactored to accept `IAgentAdapter`).

### Fix: wire Messengers IoC + CampaignBehavior (#121)

Encyclopedia hero click crashed because `Main/IoC.cs::Configure()` never invoked `MessengerIoC.RegisterMessengerFeature` and `Main/SubModule.cs::OnGameStart` never added `MessengerCampaignBehavior` to the campaign starter. Commit `03a41b6` shipped the Messengers module + tests + docs + localization with a commit body that literally stated "does NOT include the IoC/SubModule wiring" — and no gate caught it. Only the in-game NRE did.

- **`Main/IoC.cs`** — added `using TAOM.Features.Messengers;` and `MessengerIoC.RegisterMessengerFeature(container);` in `Configure()` (sort position next to QuickActions / EquipPresets / CompanionTactics / FiefManagement).
- **`Main/SubModule.cs::OnGameStart`** — added `campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.Messengers.MessengerCampaignBehavior>())` after the CompanionTactics behavior. Registered unconditionally so saves round-trip pending messengers even when `EnableMessengers` is OFF (disabled = inert, not absent — flipping the MCM toggle mid-save must not lose pending dispatches).
- **`docs/audits/`** — this fix is also the seed for the multi-phase TAOM feature audit project. `feature-manifest.md` (43 features classified) + `phase-1-kickoff.md` already written in Phase 0; Phase 1 (wiring matrix) probes every other feature for the same class of miss. Tracked as label `audit-wiring`.

### Preventive measures from scene-scripts CS_Road RCA (commit 75ccd57)

Three rule/skill updates to prevent the systemic patterns surfaced by `docs/reviews/rca-scene-scripts-cs-road-2026-05-13.md` from shipping again:

- **`.claude/skills/codex-verify/SKILL.md` + `.claude/skills/deep-review/SKILL.md`** — added Step 6 / Step 3e "Root Cause Analysis (MANDATORY — BLOCKING GATE before commit)" with explicit instructions to write `docs/reviews/rca-<feature>-<date>.md` BEFORE the closing commit. The harness-facts rule + `feedback_root_cause_mandatory.md` both label RCA as a blocking gate, but neither skill body prompted the action — that's why I shipped scene-scripts without RCA. Skill bodies now make the mandate explicit, with cross-references to the meta-RCA that documents the previous miss.
- **`.claude/rules/csharp-architecture.md` "Config Providers MUST Validate"** — extended scope from "user-editable JSON/XML" to also cover MCM settings AND editor-visible `[EditableScriptComponentVariable]` fields on engine-discovered classes (`ScriptComponentBehavior`, `GameModel`, `CampaignBehaviorBase` subclasses). All three categories are functionally identical (user-editable, untrusted, flow into comparisons + native engine calls), but the rule's documented scope was only category 1. The `FiniteFloatValidator` countermeasure has now shipped THREE times (Career cooldown #31, EditorCacheRebuild #38, scene-scripts CS_Road 2026-05-13) — the third occurrence was the scene-scripts NaN-gate miss that this update specifically closes.

### Cleanup: remove legacy editor-mode integration from EditorCacheRebuild

Now that the singleplayer MCM trigger is the live production path (verified end-to-end with full rebuild ~7 min, resume after crash, navmesh-CRC-delta auto-detection), the editor-mode entry point is dead code. Removing it.

- **Deleted `Main/Features/EditorCacheRebuild/Hooks/Patch37_CacheBuildOverride.cs`** — the Harmony patch targeting `NavigationCache<SettlementRecord>.GenerateCacheData()`. The patch never functioned in practice: in singleplayer it threw `ArgumentException: The given generic instantiation was invalid` during Harmony's `UpdateWrapper` IL emission (known Harmony edge case with closed generics over private nested types); in editor mode it would have worked but third-party community mods (Harmony, UIExtenderEx, MCMv5, ButterLib) opt out of editor activation and crash when forced. The patch was being caught + swallowed in `SubModule.cs` and logged as expected — effectively a permanent warning in every singleplayer launch with zero value. Removed the file + the now-empty `Hooks/` directory.
- **Cleaned up `Main/SubModule.cs`** — removed the try/catch around `_harmony.PatchCategory("Patch37_EditorCacheRebuild")` and the explanatory comment. With the patch class gone, the registration is dead code.
- **Cleaned up `Main/_Module/SubModule.xml`** — removed the `<Tag key="DedicatedServerType" value="none" />` + `<Tag key="IsNoRenderModeElement" value="false" />` pair. These were added to enable C# SubModule activation in editor mode so `OnSubModuleLoad` would fire and our Harmony patches would attach when the user clicked the editor button. With the editor path gone, the tags are inert. Restored the default singleplayer-only activation.
- **Updated `CLAUDE.md`** — removed the `Patch37_EditorCacheRebuild` row from the harmony patch categories table. Simplified the `EditorCacheRebuild` entry in the Key Paths section to describe the single MCM-trigger path only (no more "Path A primary / Path B legacy" framing). Acknowledged that the directory name is now misleading; rename deferred (mechanical ~30-file refactor, zero behavioral benefit).
- **Simplified `docs/features/editor-cache-rebuild.md`** — single-path component diagram, dropped the "Path B (LEGACY)" section, refreshed Dependencies to remove the SandBox.View editor types, updated the Tests section with actual current counts (116 EditorCacheRebuild tests, 1903 project-wide), refreshed Performance table with measured times from live in-game runs (~7 min full rebuild measured, was "~30 min target").

What's retained (still load-bearing, used by the MCM path): `NavigationCacheAdapter` reflection chain (T-agnostic — works for `NavigationCache<Settlement>` via `SandBoxNavigationCache`), `CacheBuilderService`, Phase 1/2 builders (serial + parallel), `SmokeTestGate`, `CheckpointSerializer`, `SettlementDiffer`, `ValidationReportWriter`, and the reserved scaffolding in `Caching/` with its test coverage. The `Caching/` orphans (`PathReuseCache`, `PersistentPathCache`) stay because removing them also drops their tests — that breaks the simplicity-criterion "deletion holds parity" rule.

Build green, 1903/1903 tests pass (EditorCacheRebuild subset 116/116). Constraint: in-game MCM trigger is the only entry point.

### Restore: TAOM editor-mode loading after a misdiagnosed cleanup

A first attempt at debugging an editor-mode crash today concluded (wrongly) that TAOM was intended to be singleplayer-only and that the wEditor mirror should be deleted. That conclusion was premature. The actual root cause of the morning's crash was simpler: the launcher's editor profile had been launched with only `*TAOM*` in `_MODULES_` — the four community-mod dependencies (Bannerlord.Harmony, Bannerlord.UIExtenderEx, Bannerlord.MBOptionScreen, Bannerlord.ButterLib) were not active, so the .NET resolver couldn't find `Bannerlord.UIExtenderEx` when `Assembly.GetTypes()` scanned TAOM.dll, producing the `ReflectionTypeLoadException` → `Debug.FailedAssert` crash dialog.

Earlier in the same session (12:33 today), TAOM.dll had loaded successfully in the editor — the `taom_debug_2026-05-12_12-33-10.log` proves OnSubModuleLoad ran (localization, diplomacy, alignments, troop weights, MainMenuCustomizer all initialized). At that point the launcher had the community mods active. The morning's crash was a launcher-state regression, not a code regression.

Reversal of the misdiagnosis:

- **Restored `Modules/TAOM/bin/Win64_Shipping_wEditor/`** as a manual mirror of `Win64_Shipping_Client/` (TAOM.dll + companion DLLs: DryIoc, MCMv5, Newtonsoft.Json, BehaviorTrees, BehaviorTreeWrapper, MinHook.x64, TAOM.NativeSkinFixes, plus .pdb). `Bannerlord.BuildResources` `Basic.targets` only auto-deploys to Client; the wEditor copy stays a manual `cp -v Win64_Shipping_Client/* Win64_Shipping_wEditor/` step after each rebuild.
- **Restored `Modules/TAOM/SubModule.xml` editor-mode tags** (`DedicatedServerType=none` + `IsNoRenderModeElement=false`) that commit `5269507` had removed alongside the Patch37 cleanup. Restoring these lets `TAOM.SubModule.OnSubModuleLoad` fire in the editor's no-render context — necessary for the engine to scan TAOM.dll and discover its `ScriptComponentBehavior` subclasses (CS_Road). Patch37 stays deleted; we don't need it for editor mode and it never worked there anyway (the IL emission for closed generics over private nested types crashed).
- **Did NOT restore** `TAOM.Dependencies` or `TAOM_Online` wEditor mirrors — both were over-mirroring leftovers; the editor doesn't need them.
- **`docs/features/scene-scripts.md` Editor compatibility section rewritten** to document the actual requirement: launcher's editor profile must enable Bannerlord.Harmony + Bannerlord.UIExtenderEx + Bannerlord.MBOptionScreen + Bannerlord.ButterLib alongside TAOM. The four community mods' SubModule.xml files already have editor-mode tags (carried over from this project's early editor-mode work), so they activate when the launcher includes them.

Confirmed working in `rgl_log_22468.txt` (10:18 today): TAOM.dll loads cleanly when all four community mods are active in the launcher, no `Loader Exceptions`, no `Error while getting types`, editor opens to its scene picker UI.

Build green, 1903/1903 tests pass (no code changes — this was a deploy + XML state restoration).

### Feature: scene scripts library — `CS_Road` procedural mesh generator (clean-room port)

Map authors now have a procedural road/river mesh generator they can attach to scene entities in Bannerlord's built-in scene editor. Drop a `CS_Road` script onto an entity, point it at a named scene Path, set width/material/UV options, click GENERATE — the engine builds a quad-strip mesh along the path with adaptive sample spacing. Live mode auto-regenerates every 0.5s while you tweak path control points.

- **Behavioural inspiration:** Alliance multiplayer mod (`Byak0/Alliance@version/0.6.0.0:Alliance.Common/Extensions/CustomScripts/Scripts/CS_Road.cs`, ~380 lines, GPL v3). TAOM did a **clean-room rewrite** — read the source once, extracted a behavioural spec (`docs/scene-scripts/specs/cs-road.md`), implemented from the spec without re-reading Alliance source. Cross-check pass confirmed no algorithmic structure collisions; the only identifier overlaps (`_parsedCurve` field, `StepKey` struct) are natural English-language names from the spec, not copyrightable. Procedure documented in `docs/scene-scripts/ATTRIBUTION.md`.
- **Engine discovery via reflection.** Bannerlord v1.3.15 `ScriptComponentBehavior.CollectEditableFields` enumerates **public instance fields** (not properties) for editor exposure. CS_Road declares 16 editor-visible fields (`PathName`, `Width`, `ElevationOffset`, `StepCurve`, `Material`, `CustomColor`, `RepeatU/V`, `InvertU/V`, `RotateUV`, `FlowDirection`, `FlipFaces`, `Generate`/`Readme` as `SimpleButton`, `Live`). No IoC registration, no SubModule.xml entry — the engine finds the class by scanning loaded DLLs.
- **Thin entry point via aggressive helper extraction.** `CS_Road.cs` is 214 lines (down from 280) — the class body is irreducibly above the ADR-002 150-line ceiling because every editor knob must be a class field and every lifecycle method must be overridden in the same class. All algorithmic logic lives in pure C# helpers: `StepCurveParser`, `StepCurveEvaluator`, `RoadPathSampler`, `RoadGeometryBuilder`, `RoadMeshAttacher`, `HexColorParser`.
- **TaleWorlds API surface** verified via `ilspycmd` on installed v1.3.15 DLLs (decompiled folder is v1.4 — not usable for signature verification). Pinned outputs at `docs/scene-scripts/sigs/` cover `ScriptComponentBehavior`, `EditableScriptComponentVariable`, `ScriptComponentParams`, `SimpleButton`, `Scene`, `Path`, `Mesh`, `MetaMesh`, `GameEntity`. Key v1.3.15 detail: override methods on `protected internal virtual` base must be declared `protected override` (the `internal` part is inaccessible cross-assembly).
- **Adaptive sampling via StepCurve.** Format `{percent:step},{percent:step},...` (e.g., `"{0:0.5},{50:2},{100:0.5}"` = dense at start, sparse middle, dense at end). Lenient parser skips malformed pairs but keeps valid ones; falls back to default `(0,1)…(100,1)` only on zero parseable pairs. NaN/Infinity guards via `TAOM.Core.Validation.FiniteFloatValidator` on Width, ElevationOffset, RepeatU/V, totalDistance, and per-pair step values.
- **MetaMesh lifecycle.** Each regen tags its `MetaMesh` with name `"taom_cs_road_generated"`, removes the previously-tracked instance before adding the new one. `OnRemoved` override cleans up on script removal. Known limitation: if the script is removed AFTER a save, the generated MetaMesh persists in the scene with the tag name — map maker can remove manually.
- **Tests:** 67 unit tests across 5 pure helpers. `CS_Road.cs` itself is engine-bound; manual editor verification per the checklist in `docs/features/scene-scripts.md`.
- **Review record.** `/deep-review` (5 agents) ⇒ 4 PASS + 1 MED data-flow gap (missing warning on malformed StepCurve) → fixed. `/codex-verify` (Codex adversarial) ⇒ 3 MED + 2 LOW findings → all fixed (finite-float gates, MetaMesh naming + OnRemoved cleanup, RoadPathSampler extraction + 9 new tests, spec clarification on lenient parsing, test attribution headers).
- **Triage of the other 12 Alliance CustomScripts** (deep-dived but NOT ported in this PR): see `docs/features/scene-scripts.md` "Triage" section. Most depend on Alliance's custom `AnimationPlayer`, `EntityUtils.EnqueueTextPanel`, or `SynchedMissionObject` MP infrastructure that TAOM doesn't have.

Issue: [#119](https://github.com/haterade22/TAOM/issues/119). Research: `Byak0/Alliance@version/0.6.0.0:Alliance.Common/Extensions/CustomScripts/Scripts/CS_Road.cs`. Not-tested: `CS_Road.cs` (engine-bound; manual editor verification).

## 2026-05-12

### Feature: editor settlement distance cache rebuild — parallel + incremental + resumable

The Bannerlord Editor's `ComputeAndSaveSettlementDistanceCache` button rebuilds `Modules/TAOM_Map/ModuleData/DistanceCaches/settlements_distance_cache_Default.bin` by running `NavigationCache<SettlementRecord>.GenerateCacheData()` — an O(n²) all-pairs A\* pathfind over 863 settlements. On TAOM the vanilla run takes **~108 hours** wall-clock (Phase 1 ~6hr, Phase 2 neighbor cache ~102hr at ~30 min/index across 204 fortifications). Confirmed via the May 11 editor log: Phase 2 was at index 32/204 after 16.5hr; remaining ~86hr.

This feature reduces a full rebuild to ~30 minutes via Harmony-patched parallel orchestration of the vanilla algorithm. Incremental rebuilds (≤30 settlements changed) target ~30 sec by recomputing only affected pairs. Crashes are recoverable via Phase-1 → Phase-2 checkpointing. Every build produces a structured JSON validation report.

- **Patch surface:** `[HarmonyPatch] Patch37_CacheBuildOverride` Prefix-returns-false on `NavigationCache<SettlementRecord>.GenerateCacheData()`. Target method resolved via `Type.GetType("SandBox.View.Map.SettlementPositionScript+SettlementRecord, SandBox.View")` → `typeof(NavigationCache<>).MakeGenericType(...)` → `AccessTools.Method`. Runtime cache (`NavigationCache<Settlement>` — different closed generic) is untouched, so live game cache loading is unaffected.
- **NavigationCacheAdapter** wraps the `object`-typed cache instance via reflection (the editor's `SettlementRecord` and `SettlementPositionScriptNavigationCache` are both `private sealed nested class` in `SandBox.View.dll`, so no direct typing is possible). Exposes `RunClosestSettlementCache`, `GetAllRegisteredSettlements`, `GetFortificationsForNeighborDetection`, `AddClosestEntrancePair` (serial path), `ComputeClosestEntrancePair` / `WriteComputedPair` (parallel split), `CheckBeingNeighbor`, `AddNeighbor`, `SerializeCache`, `DeserializeCache`, `GetSceneCrcValues`. Method-info discovery happens once at construction; per-call cost is just `MethodInfo.Invoke`.
- **`ParallelPhase1Builder` + `ParallelPhase2Builder`** use `Parallel.For` over the outer settlement loop, buffer per-pair compute results in `ConcurrentBag<PairComputeResult>`, then sequentially apply them via lock-protected adapter writes. Pattern mirrors vanilla `DefaultTeamDeploymentPlan._navigationPath`'s `ThreadLocal<NavigationPath>` thread-safety idiom for the engine pathfinder (the only documented precedent for parallelizing `Scene.GetPathBetweenAIFaces`).
- **`SmokeTestGate`** picks 10 random fortification pairs (deterministic seed), runs them once serially as a baseline and once across N threads, compares distances against `smokeTestDistanceTolerance` (1e-4 default). If max delta exceeds tolerance → log warning, fall back to `parallelism=1` for the rest of the build. Catches the YELLOW case where the native pathfinder turns out to mutate hidden state under concurrent reads.
- **`CheckpointSerializer`** writes `settlements_distance_cache_Default.ckpt.bin` (via vanilla `Serialize`) + `.ckpt.meta` (JSON with sceneCrc + navMeshCrc + phaseCompleted) between Phase 1 and Phase 2. On next Build, validates CRCs match the live scene; if so, `DeserializeCache` loads Phase 1 state and skips directly to Phase 2.
- **`SettlementDiffer` + `ChangedSettlementsFilter`** enable incremental Phase 1. Sidecar `settlements_snapshot.json` stores per-settlement `{ id, gateX/Y/face, portX/Y/face, hasPort, isFortification, sceneCrc, navMeshCrc }`. On next Build, diff against current state — if `Added + Moved + Removed ≤ incrementalMaxChanged` and CRCs match, run Phase 1 only on pairs touching changed settlements. Phase 2 always runs fully (corridor scan correctness — adding a settlement can invalidate any existing neighbor pair whose path passes near the new position; spatial indexing for partial Phase 2 deferred to a future iteration).
- **`ValidationReportWriter`** emits `last_rebuild_report.json` after every build: timestamp, mode (full / incremental / resumed / cancelled), durations per phase, settlement counts, smoke test result, max delta. Structured + diffable for trust.
- **`CacheRebuildConfig`** JSON validated per `CLAUDE.md "Config Providers MUST Validate"` rule: 17 fields with range checks on parallelism, checkpoint cadence, incremental threshold, spatial radius, smoke-test pair count, distance tolerance, log verbosity. Any invalid value reverts to default with summary warning. Default `parallelism=4` (conservative; `Environment.ProcessorCount` cap is the upper bound).
- **Files:** `Main/Features/EditorCacheRebuild/` (30+ files across `Caching/`, `Checkpoint/`, `Diff/`, `Hooks/`, `Phase1/`, `Phase2/`, `Validation/`), `Main/Adapters/INavigationCacheAdapter.cs` + `NavigationCacheAdapter.cs`, `Main/_Module/ModuleData/configs/cache_rebuild_config.json`, `Main/IoC.cs` + `Main/SubModule.cs` wiring, `TAOM.Tests/Features/EditorCacheRebuild/` (96 tests). Verified against current run state: 204 fortifications + 559 villages + 0 ports, so editor's NavigationType iteration skips Naval/All passes entirely — only `Default` runs.

Constraint: `SettlementRecord` is private nested → entire adapter is reflection-driven, no direct type references on the editor types possible.
Research: `E:\Decompiled_Bannerlord\Modules\SandBox.View\SandBox.View.Map\SettlementPositionScript.cs`; `TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache<T>` (v1.3.15 signatures matched v1.4 via `ilspycmd`); engine threading verdict YELLOW (no `_MT` variant of `GetPathDistanceBetweenAIFaces` exists; smoke-test gate is the safety net).
Not-tested: full end-to-end editor run (gated on Phase 0 — wait for current vanilla run to finish so we have `known_good_cache.bin` for byte-equal regression).

### Fix (deep-review pass): three findings from /deep-review on the editor cache rebuild feature

Pre-commit `/deep-review` caught one showstopper, one standards violation, and one cross-system inconsistency. All three fixed in the same session:

- **CRITICAL — `_navigationType` reflection used `GetField`, but v1.3.15 declares it as a property.** Verified via `ilspycmd` on `TaleWorlds.CampaignSystem.dll`: `protected MobileParty.NavigationType _navigationType { get; private set; }`. `GetField` returned null → `MissingFieldException` at `NavigationCacheAdapter` constructor → `Patch37` catch-block swallowed it and fell back to vanilla. The TAOM-parallel path would have never executed. Fixed: switched to `_navTypeProperty = _closedCacheType.GetProperty(...)` + `PropertyInfo.GetValue` in the getter. Files: `Main/Adapters/NavigationCacheAdapter.cs`.
- **Standards (ADR-002) — service-locator anti-pattern in `NavigationCacheAdapter.TryLogConstruction`.** Added during the logging pass as `IoC.Resolve<IModLogger>()`. Fixed: adapter constructor now takes optional `IModLogger? logger = null` parameter; `Patch37_CacheBuildOverride.Prefix` (the boundary) injects the logger when constructing the adapter. Files: `Main/Adapters/NavigationCacheAdapter.cs`, `Main/Features/EditorCacheRebuild/Hooks/Patch37_CacheBuildOverride.cs`.
- **Cross-system inconsistency — `SortedPathKey` sort order was inverted vs vanilla `NavigationCacheElement<T>.Sort`.** Vanilla places PORT first when ids match (`swap iff num >= 0 && (num != 0 || !s1.IsPortUsed)`); our key placed GATE first. Today this is dormant because the cache write goes through vanilla's `Sort` via reflection (not `SortedPathKey`), but it would have produced cache-miss bugs in the v2 path-reuse wiring. Fixed: replicated vanilla's exact swap condition. Files: `Main/Features/EditorCacheRebuild/Caching/SortedPathKey.cs` + matching test reversal.
- **Disputed (false positive) — Agent 5 Trace 3 claimed vanilla `Serialize(filePath)` is unreachable after our Prefix returns false.** Refuted by re-reading `SettlementPositionScript.cs:1185-1187`: the Serialize call is in the OUTER `SaveSettlementDistanceCacheEditor` method, not chained off `GenerateCacheData`. Prefix-returns-false only skips `GenerateCacheData`'s body; subsequent statements in the caller run normally on the mutated cache instance. Recorded as DISPUTED with citation; no code change.
- **HIGH performance — fixed:** `[ThreadStatic]` argument-array pools eliminate ~2.2M `object[]` allocations across a full build (~20-30 MB GC churn). Per-thread arrays of size 2/3/4 are reused across all reflection invocations (`AddClosestEntrancePair`, `ComputeClosestEntrancePair`, `WriteComputedPair`, `CheckBeingNeighbor`, `AddNeighbor`). Safe because no reflection target invokes callbacks that re-enter the adapter — verified by tracing every reflected method's body (none call back). Files: `Main/Adapters/NavigationCacheAdapter.cs`.
- **HIGH performance — fixed:** `ConcurrentBag<PairComputeResult>` and `ConcurrentBag<(s1,s2)>` swapped for `ConcurrentQueue<>` in both parallel builders. ConcurrentBag has thread-local internal storage that makes single-threaded enumeration O(n × threads); ConcurrentQueue has cheaper FIFO enumeration. Saves ~50-100 ms on the post-Parallel.For flush phase. Files: `Main/Features/EditorCacheRebuild/Phase1/ParallelPhase1Builder.cs`, `Main/Features/EditorCacheRebuild/Phase2/ParallelPhase2Builder.cs`.

### Fix (Codex review #38 pass — 6 additional findings): incremental + resume correctness, NaN config, editor-mode NRE risk

`/codex:review` (gpt-5.5, xhigh reasoning, independent) ran after the Claude `/deep-review` pass and returned 2 P1 + 2 P2 + 2 P3, all confirmed and fixed in same session. See `docs/reviews/codex-adversarial-editorcacherebuild-2026-05-12-review.md` for the findings file and `docs/reviews/REVIEW-LOG.md` Review 38 for the full root-cause table.

- **P1 — Incremental dup-key throw.** Vanilla `SetSettlementToSettlementDistanceWithLandRatio` ends in `Dictionary.Add` (not Set/replace) per ilspycmd decompile of v1.3.15. Incremental rebuild deserialized the full prior distance dict, then Phase 1 `RunFiltered` recomputed pairs touching changed settlements — every such pair already existed in the dict → `ArgumentException`. Fix: new `INavigationCacheAdapter.RemoveDistanceEntriesFor(HashSet<string> ids)`; `CacheBuilderService` calls it AFTER `DeserializeCache` and BEFORE Phase 1 to remove every entry (outer OR inner key) whose StringId is in the changed set. Files: `Main/Adapters/{INavigationCacheAdapter, NavigationCacheAdapter}.cs`, `Main/Features/EditorCacheRebuild/CacheBuilderService.cs`.
- **P1 — Stale `_fortificationNeighbors` + Phase 0 overwrite on resume/incremental.** Vanilla `GenerateNeighborSettlementsCache` opens with `_fortificationNeighbors.Clear()`; our parallel Phase 2 builders don't. Vanilla `Deserialize` ALSO replaces `_closestSettlementsToFaceIndices` — meaning a freshly-computed Phase 0 result was thrown away when incremental/resume deserialized. Two fixes: (a) Phase 0 (`RunClosestSettlementCache`) now SKIPPED when `willDeserialize` is true (CRC-verified deserialize provides it); (b) new `INavigationCacheAdapter.ClearFortificationNeighbors()` called in `CacheBuilderService` whenever we deserialized (defensive in resume mode, required in incremental). Files: same as above.
- **P2 — Patch37 vanilla-fallback ran on partially-mutated cache.** When `service.Build` threw mid-flight, Patch37 caught and returned `true` to "fall back to vanilla". But by then Phase 0 had already populated `_closestSettlementsToFaceIndices`; vanilla `GenerateClosestSettlementToFaceCache` then re-ran and hit `SetClosestSettlementToFaceIndex` → `Dictionary.Add` on already-populated dict → second exception. Fix: catch-block now `return false` (skip vanilla on mutation). User must re-click the editor button to retry from a fresh cache instance. Documented in the catch-block. File: `Main/Features/EditorCacheRebuild/Hooks/Patch37_CacheBuildOverride.cs`.
- **P2 — `CampaignVec2.Face` editor-mode NRE risk.** `SettlementSnapshotStore.Save` was reading `s.GatePosition.Face.FaceIndex` for diff comparison. `CampaignVec2.Face` getter calls `Campaign.Current.MapSceneWrapper.GetFaceIndex(this)` — `Campaign.Current` may be null in editor mode (vanilla editor cache builder never touches `.Face`; it uses `Scene` directly). Fix: removed `GateFace`/`PortFace` integer fields from `SettlementSnapshot`. Diff now compares positions only via `ToVec2()` (pure cached-position read, no Campaign dependency). Face index is derivable from position via the scene if ever needed. Files: `Main/Features/EditorCacheRebuild/Diff/{SettlementSnapshot, SettlementSnapshotStore, SettlementDiffer}.cs`.
- **P3 — Float config validators accept `NaN`/`Infinity`.** `parsed.SmokeTestDistanceTolerance < 1e-8f || > 1e-2f` evaluates `false` for `NaN` (all NaN comparisons return false), so NaN sneaks past validation. Then `maxDelta > NaN` is also always false → smoke-test gate silently disabled. Same pattern caught earlier in Career cooldown review #31. Fix: `IsFiniteNumber` helper + apply to both float config fields before range checks. File: `Main/Features/EditorCacheRebuild/CacheRebuildConfigProvider.cs`.
- **P3 — `SortedPathKey` test gap on degenerate self-pairs.** Sort-equivalence tests covered cross-id cases and same-id-mixed-port; same-id-same-port wasn't enumerated. Code is correct (verified against vanilla `NavigationCacheElement<T>.Sort`); added two regression tests `Ctor_SameIdSameGateGate_Canonicalized` + `Ctor_SameIdSamePortPort_Canonicalized`. File: `TAOM.Tests/Features/EditorCacheRebuild/Caching/SortedPathKeyTests.cs`.
- **Disputed (verified false positive):** Agent 5 of the pre-Codex `/deep-review` had claimed vanilla `Serialize(filePath)` is unreachable after our Prefix returns false. Codex re-decompiled `SaveSettlementDistanceCacheEditor` and confirmed `Serialize` is in the OUTER method (not chained off `GenerateCacheData`), so it runs normally on our mutated cache. Documented as DISPUTED in the Codex review file and `AGENTS.md`.

Build green, 96/96 EditorCacheRebuild tests pass, 1800/1800 total. The 2 P1 fixes only fire in incremental/resume paths — full rebuild from cold cache (the default flow on first use) was already correct.

### Preventive measures (Codex review #38 root-cause prevention)

The 6 findings break into 4 recurring patterns. Installing prevention so the same categories of bugs can't ship again.

- **NaN/Infinity in float config — STRUCTURALLY PREVENTED.** This is the SECOND time the bug has shipped (Career cooldown review #31 was the first; both relied on bare `< min || > max` checks that NaN sneaks past). Action:
  - New `TAOM.Core.Validation.FiniteFloatValidator` static helper with `IsFinite`/`IsFiniteInRange`/`IsFiniteAtMost`/`IsFiniteAtLeast`. 15 unit tests covering NaN, ±Infinity, edge values, regression cases for both classes (range and at-most).
  - `CacheRebuildConfigProvider` refactored to use `IsFiniteInRange` for `IncrementalSpatialRadius` and `SmokeTestDistanceTolerance`.
  - `RevoltTuningConfigProvider` retrofitted — same NaN gap on `SettlementOwnerDifferentCultureLoyaltyEffect > 0f` and `GovernorDifferentCultureLoyaltyEffect > 0f`. Now uses `IsFiniteAtMost(value, 0f)`.
  - `.claude/rules/csharp-architecture.md` "Config Providers MUST Validate" rule updated — explicit step 4 now says "For every `float`/`double` field: reject `NaN` and `±Infinity` BEFORE the range check. Use `FiniteFloatValidator` — never write bare `< min || > max` checks on floats." Bug-shipping history cited (review #31 and #38) so the rule's existence is justified.
  - Files: `Main/Core/Validation/FiniteFloatValidator.cs` (new), `TAOM.Tests/Core/Validation/FiniteFloatValidatorTests.cs` (new), `Main/Features/EditorCacheRebuild/CacheRebuildConfigProvider.cs`, `Main/Features/RevoltTuning/RevoltTuningConfigProvider.cs`, `.claude/rules/csharp-architecture.md`.
- **Add-only API confusion in deserialize-then-mutate flows — captured in memory.** Findings 1, 2, 3 all share root cause: assumed "Set" semantics from method name, actual vanilla setter is `Dictionary.Add` which throws on duplicate. New memory: [feedback_decompile_vanilla_setter_before_deserialize_mutate.md](C:\Users\mikew\.claude\projects\c--Users-mikew-source-repos-TAOM\memory\feedback_decompile_vanilla_setter_before_deserialize_mutate.md). Indexed in MEMORY.md so future Claude sessions auto-load it for any feature that deserializes vanilla cache structures.
- **TaleWorlds struct properties that dereference `Campaign.Current` — captured in memory.** Finding 4 was about `CampaignVec2.Face`. New memory: [feedback_campaign_coupled_property_in_editor.md](C:\Users\mikew\.claude\projects\c--Users-mikew-source-repos-TAOM\memory\feedback_campaign_coupled_property_in_editor.md). Lists the specific offender (`CampaignVec2.Face` needs `Campaign.Current.MapSceneWrapper`) and the safe alternative (`ToVec2()` for raw scalars). Future TAOM editor-mode features will pick this up.
- **AGENTS.md updated** with three new "Bugs Codex caught" patterns for the next Codex review's reference: add-only dict semantics, partial-state vanilla fallback, position-only-vs-face-resolved snapshot. Last-updated stamp bumped to 2026-05-12 / Review 38.
- **Test coverage:** SortedPathKey degenerate self-pairs now have explicit tests (2 added). FiniteFloatValidator has 15 tests covering every documented use-case. No similar test gaps remain in the EditorCacheRebuild feature.

Total prevention surface: 1 new shared helper (`FiniteFloatValidator`) + 1 rule update (csharp-architecture.md) + 2 new memory notes + 1 retrofit of an existing provider. Build green; 1818/1818 tests pass (was 1800 before — added 15 helper tests + 2 SortedPathKey tests + 1 elsewhere).
- **Tolerated orphans (per simplicity-criterion scope):** Agent 5 flagged 8 config fields with no consumer (`checkpointEvery`, `enablePathReuse`, `enablePersistentPathCache`, `incrementalSpatialRadius`, `enableDebugQualityCheck`, `enableUiOverlay`, `phase1SkipReversePathfind`, `logVerbosity`) and 2 orphan types (`IEditorSceneAdapter`, `PathReuseCache`/`PersistentPathCache` pair). These correspond to dropped Phases 9/12/13 and reserved v2 path-reuse scaffolding. The feature doc explicitly documents them as reserved; not deleted to preserve test coverage and future hook points. Re-evaluate in v2 if not wired.

Build green, 96 EditorCacheRebuild tests pass, 1800/1800 total.

### Feature: in-game MCM trigger for distance cache rebuild — pivots away from editor-mode integration

The original editor-mode integration test (Phase 14) blocked on a Bannerlord ModuleManager-level crash when third-party community mods (Harmony, UIExtenderEx, ButterLib, MCMv5, ButterLib variants) were force-activated in editor mode — those mods opt out of editor activation by default and crash when forced. Rather than maintain a fragile per-mod editor compatibility matrix, pivoted to a singleplayer MCM-driven trigger that reuses the existing parallel build pipeline against the live campaign's `MapSceneWrapper`.

- **New service:** `IRuntimeCacheRebuildService` + `RuntimeCacheRebuildService` in `Main/Features/EditorCacheRebuild/`. Gates on `Campaign.Current != null`, uses `Interlocked.CompareExchange` for single-run lock, spawns the build on `Task.Run`, writes output atomically via `.tmp → final` rename with `.prev` backup preserved. All deps injected via constructor (no service locator). Registered as `Reuse.Singleton` in `EditorCacheRebuildIoC`.
- **MCM entry point:** new `Map Tools / Distance Cache Rebuild` group in `TaomSettings.cs` with a `SettingPropertyButton` action property `RebuildDistanceCacheAction`. The static lambda is the boundary — wraps `IoC.Resolve<IRuntimeCacheRebuildService>().Trigger()` in try/catch with `Colors.Red` `InformationMessage` on failure (MCMv5 silently swallows uncaught exceptions; the wrap surfaces them).
- **Runtime closed-generic compatibility:** the existing `NavigationCacheAdapter` was reflection-driven and already T-agnostic. It works against `NavigationCache<Settlement>` (runtime, via `SandBoxNavigationCache`) identically to `NavigationCache<SettlementRecord>` (editor) — verified that `Settlement` implements `ISettlementDataHolder` and the reflection chain (`WalkToNavigationCacheBase` → generic args → typed `MethodInfo` finders) works for both closed generics. Patch37 and the runtime service operate on disjoint closed generics, so no double-execution risk.
- **Comprehensive logging:** every build emits a unique 6-hex correlation ID (`[RuntimeCacheRebuild#A4F2C1]`) prefixing all log lines. Pre-flight diagnostics cover environment (machine, CPU count, .NET version, GC mode), campaign snapshot (game id, start time, settlement counts by type), output path resolution (existing file size + modified time, drive free space), and stale `.tmp` / interrupted-write detection (final missing + `.prev` exists triggers explicit warning with recovery instructions). 5-step build script with per-step timing. `SmokeTestGate` logs serial vs parallel ms/pair + speedup factor + worst-pair diagnostic. `ParallelPhase1Builder` + `ParallelPhase2Builder` log first-pair/first-neighbor heartbeats via `Interlocked.CompareExchange` (one-time, not per-iteration) — confirms pathfinder reachability from worker threads within milliseconds. Memory snapshots before/after each phase via `GC.GetTotalMemory(forceFullCollection: false)`. `AggregateException` unwrapping with inner stack traces if `Parallel.For` workers crash.
- **Output atomicity + verification:** `WriteOutputAtomically` writes to `.tmp` first, then atomically: rename existing `final → .prev` + rename `.tmp → final`. `VerifyOutputRoundTrip` constructs a fresh `SandBoxNavigationCache`, calls `Deserialize` on the written file, counts distance + neighbor entries, and compares against `result.Phase1.PairsComputed` / `result.Phase2.NeighborPairsAdded` with a 10% tolerance. Shortfall → explicit `LogError` with `.prev` restoration instructions. Catches truncated-mid-record serialization that vanilla `Deserialize` might silently accept at a record boundary.
- **`/deep-review` pass — 3 MEDIUM fixes applied same session:** (1) MCM lambda exception wrap (Data Flow Trace 1b), (2) round-trip verification with expected-count comparison (Data Flow Trace 5), (3) `ConcurrentQueue.Count` replaced with tracked `Interlocked.Increment` counter for pre-flush log lines in both Phase builders. (4) interrupted-write startup diagnostic. 0 HIGH findings, 0 architecture violations. Compatibility agent vs Data Flow agent disagreed on Patch37 runtime behavior — Data Flow resolved correctly (patch attaches in singleplayer but `GenerateCacheData` is never called outside editor → dormant, not a startup stall).
- **Files:** `Main/Features/EditorCacheRebuild/IRuntimeCacheRebuildService.cs` (new), `Main/Features/EditorCacheRebuild/RuntimeCacheRebuildService.cs` (new), `Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs`, `Main/Features/TaomSettings.cs`, `Main/Features/EditorCacheRebuild/Phase1/ParallelPhase1Builder.cs`, `Main/Features/EditorCacheRebuild/Phase2/ParallelPhase2Builder.cs`, `Main/Features/EditorCacheRebuild/Validation/SmokeTestGate.cs`, `Main/_Module/SubModule.xml`.

Constraint: Bannerlord community mods (Harmony, UIExtenderEx, MCMv5, ButterLib) crash when force-activated in editor mode — they opt out via `Tags`-less SubModule.xml and don't implement editor-mode safety. Cannot test cache rebuild from editor mode without forking each dependency. In-game MCM trigger sidesteps the entire ModuleManager pain.
Rejected: standalone navmesh.bin + pathfinder reverse-engineering — would require reimplementing TaleWorlds' native A\* over the triangulated mesh including region-switch cost model and excluded-face filters; large attack surface for subtle correctness bugs. In-game MCM piggybacks on the proven-correct engine pathfinder.
Not-tested: full end-to-end singleplayer trigger with active campaign (gated on Phase 0 — wait for existing 4.5-day vanilla cache run to finish so we have `known_good_cache.bin` for byte-equal regression).

Build green, 1818/1818 tests pass.

### Fix (Codex review #39): RuntimeCacheRebuild MCM-pivot follow-up — verification result + atomic write + dead-config cleanup

`/codex-verify` against the 3-commit MCM-trigger pivot (since `a502ade`) returned 0 P1, 2 P2, 2 P3. All confirmed and fixed in same session.

- **P2 — VerifyOutputRoundTrip returned void; success popup ran unconditionally.** When verification was refactored from "throw on failure" to "log and continue" during the comprehensive-logging work, the caller's "BUILD COMPLETE" popup was never gated on the result. A shortfall or deserialize-throw would log loudly but the user still saw "Cache rebuild COMPLETE. Load the next save to use it." Resume mode also had a blindspot: `result.Phase1.PairsComputed == 0` (Phase 1 came from checkpoint) short-circuited the distance-count comparison, so a structurally valid but logically truncated file passed silently. Fix: `VerifyOutputRoundTrip` now returns `VerificationResult { Ok, Reason, ActualDistanceCount, ActualNeighborCount }`. Caller branches on `Ok` — on failure, emits red `Colors.Red` `InformationMessage` with `.prev` restoration instructions and returns from `RunBuild` without the success summary. Resume blindspot fixed by capturing `adapter.EnumerateExistingDistances().Count()` immediately before serialization as the expected count when Phase 1 came from checkpoint. Files: `Main/Features/EditorCacheRebuild/RuntimeCacheRebuildService.cs`.
- **P2 — Three-step rename in WriteOutputAtomically had a crash window.** The old sequence (`Delete .prev → Move final → .prev → Move .tmp → final`) is three filesystem ops, none atomic as a transaction. A process kill between steps 2 and 3 left `final` missing entirely. Fix: `File.Replace(tempPath, finalPath, backupPath, ignoreMetadataErrors: true)` when `final` exists — single atomic Win32 `ReplaceFile` call. Kept `File.Move(tempPath, finalPath)` only for the first-build case where no existing final. The "atomic write" promise in the feature doc is now actually atomic. Files: same.
- **P3 — Shipped JSON exposed 8 dead config fields as if they were active.** `checkpointEvery`, `enablePathReuse`, `enablePersistentPathCache`, `incrementalSpatialRadius`, `enableDebugQualityCheck`, `enableUiOverlay`, `phase1SkipReversePathfind`, `logVerbosity` — all corresponded to dropped/future-phase scaffolding from the original 17-phase design (Phases 9 spatial index, 12 path reuse, 13 multi-pass quality check, UI overlay). Most misleading: `logVerbosity` validated successfully but never affected logger output. Fix: stripped the 8 fields from `Main/_Module/ModuleData/configs/cache_rebuild_config.json` (active fields only). Kept all fields in `CacheRebuildConfig.cs` with `<summary>Reserved for...</summary>` XML doc comments so the C# API stays stable for tests and future phases. Updated feature doc's config table to list active fields only with a "Reserved fields" sub-section. Files: `Main/_Module/ModuleData/configs/cache_rebuild_config.json`, `Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs`, `docs/features/editor-cache-rebuild.md`.
- **P3 — Tests intercepted SpawnBuild and missed the production code path.** The `TestableRuntimeCacheRebuildService.SpawnBuild` no-op override was the right minimal pattern for testing `Trigger()`'s gate logic without spinning up `Task.Run`, but the seam also skipped `RunBuild`, `VerifyOutputRoundTrip`, `WriteOutputAtomically`, and the `finally _runningFlag = 0` cleanup. None of those were unit-tested. Fix: made `VerifyOutputRoundTrip` and `WriteOutputAtomically` `internal virtual` so tests can invoke them directly. Added 7 new tests: 3 atomic-write scenarios (`NoExistingFinal_PromotesTempViaMove`, `ExistingFinal_AtomicReplacePreservesPrev`, `StaleTempExists_DeletesBeforeWriting`) + 4 verification scenarios (`DeserializeThrows_ReturnsFailureResult`, `DistanceShortfall_ReturnsFailureResult`, `CountsMatch_ReturnsSuccessResult`, `NeighborSymmetricStorage_ComparesAgainstDoubledExpectation`). Files: `TAOM.Tests/Features/EditorCacheRebuild/RuntimeCacheRebuildServiceTests.cs`.

Build green, 1850/1850 tests pass (was 1829 — added 7 new RuntimeCacheRebuildServiceTests for total of 18, was 11). Codex's `RunBuild` end-to-end orchestration coverage gap (the `finally _runningFlag = 0` cleanup path) deferred — would need a deeper refactor; remaining gap is logged in AGENTS.md.

### Tracking issue opened from review #39 follow-up audit

[#120 — EditorCacheRebuild: extend NavigationType iteration for NavalDLC / port support](https://github.com/haterade22/TAOM/issues/120). Vanilla `SettlementPositionScript.SaveSettlementDistanceCacheEditor` iterates `{ Default, Naval, All }` when NavalDLC is active or `GetMapIsNavalDLC()` returns true. TAOM currently has 0 ports across all 863 settlements, so `Default`-only rebuild is correct today, but a future map with coastal port settlements (Umbar, Harad coast, Dol Amroth, Mithlond) would need 3-way rebuild. Filed during /codex-verify vanilla parity audit.

### Preventive measures (review #39 root-cause prevention)

The 4 findings split into 4 distinct categories. Installing prevention so the same patterns can't ship again:

- **Void-returning verification with downstream success-popup consumer** — added to AGENTS.md "Bugs Codex caught": when refactoring a void-throwing check into a logged check, audit all downstream consumers that previously relied on the exception. The structural correctness signal must flow back through the caller, not get buried in logs.
- **Multi-step file rename masquerading as atomic** — added to AGENTS.md: on Windows/.NET Framework, prefer `File.Replace(temp, final, backup, ignoreMetadataErrors: true)` over composed `File.Move` sequences when claiming atomic write semantics. The single Win32 `ReplaceFile` is genuinely transactional; composed Moves are not.
- **Dead config fields in shipped JSON** — added to AGENTS.md: every field in shipped JSON must have at least one production consumer that actually responds to it. Reserved/scaffolding fields stay in the C# class (with XML doc comments marking their intended phase) but are stripped from the JSON to avoid misleading tuners.
- **Test seam skipping production paths** — added to AGENTS.md: when intercepting a virtual method to make code testable, audit what the seam SKIPS and ensure those paths have alternate coverage. The skip is fine, but the production paths it skips need their own tests via narrower seams (internal virtual on individual methods).

Total prevention surface: 4 new patterns in AGENTS.md "Bugs Codex caught" section + REVIEW-LOG.md Review 39 RCA + this CHANGELOG entry. Tests now at 1850/1850.

## 2026-05-11

### Process: port `think-before-coding` rule from karpathy-skills; sharpen `surgical-changes` + `goal-driven-execution` wording in CLAUDE.md

Reviewed [forrestchang/andrej-karpathy-skills](https://github.com/forrestchang/andrej-karpathy-skills) (124k★ — packages Karpathy's four behavioral principles for Claude Code / Cursor distribution). Three of the four were already absorbed in the 2026-05-07 autoresearch port (`simplicity-criterion.md`, autonomous-loop stewardship, worktree isolation). One genuine gap remained — assumption-surfacing before the first Edit — plus two principles where the upstream phrasing was sharper than ours.

- **`.claude/rules/think-before-coding.md`** (new, always-load, no `paths:` per harness-facts loader rules). Fires when a non-trivial request admits multiple reasonable interpretations and Claude cannot infer the right one from files/commits/CLAUDE.md/sibling files. Includes a TAOM-specific "when NOT to ask" guard (trivial/mechanical work, routing decisions, conventions already in ADRs) — the upstream rule does not address the opposite failure mode of over-questioning, which we've hit in past sessions.
- **CLAUDE.md "Edit scope discipline"** subsection added under Working Discipline. Two paragraphs: traceability rule ("every changed line should trace directly to the user's request — don't 'improve' adjacent code") and vague-to-testable rule ("convert vague asks into testable objectives BEFORE the first Edit"). Cross-references `/investigate` Phase 1 and `/verify`.
- **CLAUDE.md Scoped Rules table** — added rows for `think-before-coding.md` and the previously-undocumented `simplicity-criterion.md` (pre-existing docs gap caught by this audit).

Skipped: bundled `/karpathy-principles` skill, three-file Cursor/CLAUDE.md/SKILL.md sync convention, TSV experiment logging, 5-minute training window — none map onto TAOM's workflow per the simplicity criterion.

## 2026-05-07

### Process: import three workflow disciplines from karpathy/autoresearch

Reviewed [karpathy/autoresearch](https://github.com/karpathy/autoresearch) (autonomous LLM-pretraining experiment loop, March 2026) and adopted three rule sharpenings — skipped a new `/experiment` skill because TAOM iteration has no single numeric fitness metric to drive a research loop. Files: `CLAUDE.md`, `.claude/rules/simplicity-criterion.md` (new), `.claude/rules/harness-facts.md`.

- **NEVER STOP + crash judgment** in CLAUDE.md "Autonomous-loop stewardship". Adds an explicit prohibition on the "should I keep going?" interruption (autoresearch's `program.md` framing — "the human might be asleep") and a trivial-vs-fundamental crash heuristic. The existing trust model said "continue established work" but didn't forbid the interruption.
- **`simplicity-criterion.md`** as a new always-load rule (no `paths:` field per the loader docs in `harness-facts.md`). Turns "no over-engineering" into a Yes/No matrix: tiny win + complexity = reject; equal + simpler = keep; deletion that holds parity = always keep. Closes a recurring `/deep-review` failure mode where agents preserve scaffolding "just in case" — flagged across multiple Codex review cycles, most recently EquipPresets review #5 on 2026-05-06.
- **Worktree-isolation prevention rule** in `harness-facts.md` tied to the existing parallel-port build-watcher RCA (2026-05-06). Codifies `isolation: "worktree"` on parallel `Agent` calls that may edit overlapping single-owner files (csproj, IoC.cs, SubModule.cs). autoresearch's `.gitignore` independently confirms the same pattern (`worktrees/`, `queue/`, per-session `CLAUDE.md`/`AGENTS.md` "generated per-session by launchers"). Cross-linked from the parallel-port section so anyone reading the RCA finds the prevention.



### Fix: SubModule load NRE — defer Formation.SetMovementOrder patches to mission start

Bannerlord crashed during mod load with `NullReferenceException` inside `MovementOrder..ctor(MovementOrderEnum)` while Harmony was applying `Patch31_SmartCavalryAI` in `OnSubModuleLoad`. Root cause: the v1.3.15 `MovementOrder` struct's type initializer (`.cctor`) constructs static instances (`MovementOrderNull`, `MovementOrderCharge`, …) whose ctor reads `Mission.Current.CurrentTime`. JIT prep on a Harmony patch whose postfix takes a `MovementOrder` parameter forces the type to load — but `Mission.Current` is null in `OnSubModuleLoad` (and in `OnGameInitializationFinished`, which is where `Patch35_CompanionTactics`' sibling `Formation.SetMovementOrder` postfix would have crashed identically once Patch31 was fixed). Solution: a new shared category `Patch_MissionTime_SetMovementOrder` collects both postfixes (`Patch31_FormationSetMovementOrder`, `Patch35_Formation_SetMovementOrder`) and is applied once from `OnMissionBehaviorInitialize` behind a static `_missionTimePatchesApplied` guard — by which time `Mission.Current` is set and the cctor succeeds. Files: `Main/Features/SmartCavalryAI/Hooks/Patch31_FormationSetMovementOrder.cs`, `Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs`, `Main/SubModule.cs`. Build clean, all 1704 tests pass.

### Docs: backfilled 5 missing feature docs

Closed the four `Main/Features/<X>` directories the `detect-docs-gaps.sh` SessionStart hook had been flagging on every boot (`Arena`, `BattleBalance`, `BattleScenes`, `WeatherBoundsGuard`) plus one the hook missed but that was nonetheless undocumented (`LocalizationOverride` — the existing `localization.md` covers TAOM's added translation strings, not the `MBTextManager.GetLocalizedText` Harmony override). Each new doc fills the `docs/features/TEMPLATE.md` skeleton with verified-from-source detail (file inventories, exact patch targets, config schemas, test paths + counts) so future sessions don't re-derive architecture from scratch. `BattleScenes` doc clearly marks the feature as **DISABLED** (gated on `TAOM_Map` integration; `_harmony.PatchCategory("Patch0_BattleScenes")` is commented out at SubModule.cs:115-116) so the next session investigating "why isn't this loading" can stop in 30 seconds. New files: `docs/features/arena.md`, `battle-balance.md`, `battle-scenes.md`, `localization-override.md`, `weather-bounds-guard.md`. Hook now reports a single residual gap (`Execution`), which is a false positive — `alignment-aware-execution.md` already covers it; teaching the hook that alias is optional follow-up.

### Fix: EquipPresets — Codex review #2026-05-07 fix pass (Patch33)

Codex adversarial review of the EquipPresets port returned 9 findings: 2 CRITICAL, 3 HIGH, 3 MEDIUM, 1 LOW. All confirmed findings fixed; the 6 Known Suspects all addressed (3 disputed by Codex with vanilla-source evidence — no code change needed).

**CRITICAL fixes:**
- **Load path now goes through vanilla `InventoryLogic.AddTransferCommands`.** The original-module port (and Claude's first deep-review) shipped a direct `equipment[slot] = element` mutation. Codex's vanilla decompile of `SPInventoryVM.EquipEquipment` showed the correct path: `TransferCommand.Transfer(...)` factory + batch submit. Vanilla auto-deposits the displaced equipped item to inventory, consumes inventory items, fires `AfterTransfer` to refresh slot VMs, and applies the slot-fit / mount-harness gates. Without this, equipping from inventory duplicated items (no roster consumption) and overwriting an equipped slot lost the previous gear (no deposit). The new flow lives in `InventoryScreenAdapter.LoadEquipment` — the service now builds a list of `PresetSlotRequest`s and delegates; all TaleWorlds types stay inside the adapter per ADR-007.
- **`TaomSettings` 3 EquipPresets properties restored.** Coordination hook had stripped them between sessions; provider was dereferencing absent properties. Now: `EnableEquipmentPresets` (default true), `MaxPresetsPerCharacter` (1–20, default 10), `EquipPresetsDebug` (default false) under group `Inventory/Equipment Presets`, `GroupOrder = 33`.

**HIGH fixes:**
- **EquipPresets fully wired.** `IoC.cs` registers `EquipPresetsIoC.RegisterEquipPresetsFeature(container)`; `SubModule.cs` calls `_harmony.PatchCategory("Patch33_EquipPresets")` in `OnGameInitializationFinished` and `campaignStarter.AddBehavior(IoC.Resolve<EquipmentPresetCampaignBehavior>())` unconditionally in `OnGameStart` so SyncData round-trips when the toggle is OFF (matches the MCM "presets are inert (preserved in save)" promise).
- **Empty-slot clearing on Load.** `EquipmentSlotAdapter.Capture` now emits one snapshot per slot (0..11) including empty-itemId sentinels for empty slots. `LoadEquipment` translates an empty `ItemStringId` request into an unequip `TransferCommand` (slot → PlayerInventory). A "no shield" preset can now actually clear a shield from a hero who has one.
- **Save-from-civilian-view now captures both sets.** Previously, `IncludesCivilianEquipment` was set from `_screen.IsViewingCivilianEquipment` — if the player saved while viewing the civilian tab, the snapshot also bundled hidden battle equipment, and Load mutated both sets. Now: `PromptSaveName` always saves the full hero loadout (battle + civilian + mount). The MCM hint copy and the dialog text agree on this.

**MEDIUM fixes:**
- **`Hero.BattleEquipment` / `CivilianEquipment` dead-equipment guard.** Vanilla returns `Campaign.Current.DeadBattleEquipment` / `DeadCivilianEquipment` shared singletons when the hero's backing equipment is null. `EquipmentSlotAdapter.Capture` now reference-checks against those singletons and refuses to read from them — otherwise a captured "preset" would mirror dead-character defaults rather than the live hero's loadout.
- **`Equipment.IsItemFitsToSlot` enforcement.** Vanilla's `Equipment[index]` setter calls `IsItemFitsToSlot` but ignores the return — a tampered save or item-XML drift could put a helmet in a weapon slot. `InventoryScreenAdapter.LoadEquipment` now invokes `Equipment.IsItemFitsToSlot(slot, item)` before issuing a `TransferCommand` and reports `LoadEquipmentResult.InvalidSlots` for rejections.
- **Dead `SetItemLocked` API removed.** `IInventoryScreenAdapter.SetItemLocked` was leftover from the SlotLocked plumbing Codex flagged in the prior pass; documentation still claimed "Used by Load" but no consumer existed. Deleted from interface and concrete; if a future feature wants pre-existing-lock awareness it can be reintroduced with a proper consumer.

**LOW fix:**
- **`RestoreFromSerializableState` null-normalizes.** Drops null hero keys, drops null preset entries, replaces null `Items` / `CivilianItems` with empty lists. Robust against future save-format migration edge cases.

**6 Known Suspects from the Codex prompt:**
1. `PromptSaveName` includeMount=true hardcode — addressed by docs + the new "save complete loadout" semantic.
2. `TextObject.SetTextVariable(string, string)` chainability — DISPUTED (Codex confirmed it returns `this`).
3. `ActiveHeroStringId` null-leak — DISPUTED (vanilla `SPInventoryVM` only assigns `_currentCharacter` for hero characters).
4. `OnGameLoaded` orphan pruning empty live-set — DISPUTED (existing guard correctly returns 0).
5. Modifier preservation chain — CONFIRMED (validation pre-pass kept; race-path documented).
6. GauntletLayer z-order 1000 — CONFIRMED (no TAOM/vanilla collisions; vanilla layer is 15).

**Tests:** 56 EquipPresets tests in TAOM.Tests/Features/EquipPresets/ (4 files), all green. Full suite 1542/1542. Behavioral tests for the new InventoryScreenAdapter contract (`LoadEquipment`) including: pre-validate-modifier path, request-pass-through, empty-itemId clearing, includeMount filtering, invalid-slot aggregation, both-equipment-set application. Plus 5 new normalization tests for `RestoreFromSerializableState` (null keys, null presets, null Items lists, all-null pruning).

**Coordination caveat:** ported in parallel with QuickActions, FiefManagement, SmartCavalryAI, CompanionTactics, MixedFormations. The coordination hook auto-applied `<Compile Remove>` lockouts on the csproj when sibling sessions had transient build errors; lockouts removed once each owning session verified its module compiles clean. EquipPresets restored in `Main/TAOM.csproj` and `TAOM.Tests/TAOM.Tests.csproj`.

## 2026-05-06

### Feat: QuickActions — port external sibling module into Main/Features/ (Patch34)

Inventory "Sell All" replaced with a 4-option multi-action inquiry (Sell Damaged / Sell Low Value / Unequip All / vanilla) plus per-save inventory-search-box toggle. Issue: [#114](https://github.com/haterade22/TAOM/issues/114).

**Four Harmony patches under `Patch34_QuickActions`:**
- `Patch34_SellAllItemsMenu` (Prefix on `SPInventoryVM.ExecuteSellAllItems`) — opens `MultiSelectionInquiryData`. The "Sell All (Vanilla)" callback uses a thread-static `_bypassQuickActions` flag and re-enters `ExecuteSellAllItems()` so vanilla `TransferAll` runs unmodified — preserves capacity-budget, settlement-mode (`TransferAllForSettlement`), full-stack, sort, zero-count cleanup.
- `Patch34_SPInventoryVMCapture` (Postfix on ctor) — captures active VM into `InventoryVMAdapter`.
- `Patch34_SPInventoryVMSearchApply` (Postfix on `RefreshCallbacks`) — applies per-save `IsSearchAvailable` on inventory open.
- `Patch34_SPInventoryVMFinalize` (Postfix on `OnFinalize`) — clears active-VM reference defensively.

**v1.3.15 verification removed the original module's reflection layer.** The 1.2.x source used 8-probe + 5-probe reflection chains for the right-pane item list and `SPItemVM.ProcessSellItem`. `ilspycmd` against installed v1.3.15 confirmed both are public vanilla — direct property access only.

**`IInventoryVMAdapter` introduced as load-bearing for feature 6 EquipPresets.** Both features access `SPInventoryVM`; consolidating active-VM capture in one adapter prevents duplicate-reflection drift.

**`IPlayerEquipmentAdapter` extended** with `TryUnequipAllPlayerSlots()` iterating 12 `EquipmentIndex` slots × battle + civilian. The inventory adapter routes through `InventoryLogic.TransferCommand` per slot when active (vanilla `AfterTransfer` rebuilds rows + slot VMs); falls back to direct mutation via `ItemRoster.AddToCounts(EquipmentElement, int)` (modifier-preserving overload) when no inventory active.

**`IInventoryItemAdapter.StackAmount`** added. `TrySellItem` sets `spItem.TransactionCount = StackAmount` before invoke so a stack of 50 sells 50 units.

**Audio:** `IQuickActionsAudioPlayer` wraps `SoundEvent.PlaySound2D("event:/ui/transfer")`.

**`InventorySearchCampaignBehavior`** holds per-save bool via `SyncData("TAOM_IsInventorySearchAvailable")`. Seeds from MCM on `OnNewGameCreatedEvent` / `OnGameLoadedEvent`; reconciled per campaign frame via `CampaignEvents.TickEvent`. Apply happens on inventory-open via `Patch34_SPInventoryVMSearchApply`, not on tick.

**15 MCM settings** under `Inventory/Quick Actions` (GroupOrder 30/31/32) — all consumed.

**Tests:** 53/53 QuickActions tests across 3 files (34 service + 7 behavior + 9 preset). Coverage: skip-guard exhaustion for every filter flag, threshold matrix, modifier-preservation, audio invocation, confirmation flow, null-adapter graceful degrade, SyncData seed/reconcile, stack-amount regression coverage.

**Two-stage review pipeline:**
- `/deep-review` (5 parallel Claude agents) caught and fixed: CRITICAL IoC/SubModule wiring (parallel-port lockout reverted edits), HIGH `IsFiltered` filter gap, MEDIUM stale-VM lifecycle, MEDIUM Horse/HorseHarness slots skipped.
- `/review-codex` (Codex CLI 17m28s — [docs/reviews/codex-adversarial-quickactions-2026-05-06.md](docs/reviews/codex-adversarial-quickactions-2026-05-06.md)) caught 3 additional bugs (full RCA at [docs/reviews/rca-quickactions-2026-05-06.md](docs/reviews/rca-quickactions-2026-05-06.md)):
  - HIGH — "Sell All (Vanilla)" hand-rolled the loop, dropped capacity/settlement/full-stack/sort/cleanup. Fix: thread-static bypass flag.
  - HIGH — `TrySellItem` sold 1 unit per stack (`TransactionCount` default 1). Fix: adapter exposes `StackAmount`, sets before invoke.
  - MEDIUM — `UnequipAll` bypassed `InventoryLogic.AfterTransfer`. Fix: route through `TransferCommand`.

**Three feedback memories codified for future sessions:** `feedback_vanilla_reentry_via_bypass_flag.md`, `feedback_static_delegate_reads_param_state.md`, `feedback_route_via_engine_command_when_ui_active.md`. Unifying root cause: "engine-bypass anti-pattern" — code mutating engine state via paths that bypass vanilla's UI/refresh/update contract.

### Fix: MixedFormations — Codex adversarial review findings (navmesh validation + thread safety)

After `/deep-review MixedFormations` (5-agent core, returned PASS on standards/compatibility/completeness/data-flow), `/review-codex MixedFormations` (Codex CLI 0.128.0, run 2026-05-06) produced TWO additional findings the deep-review missed — 1 HIGH + 1 MEDIUM. Both confirmed via `ilspycmd` against installed v1.3.15 and fixed in same session per the "no silent deferrals" rule. Codex review file preserved at [docs/reviews/codex-adversarial-mixedformations-2026-05-06.md](docs/reviews/codex-adversarial-mixedformations-2026-05-06.md) (reconstructed from stdout because Codex's `apply_patch` was rejected by the read-only sandbox).

**FINDING 1 (HIGH) — Patch30 bypassed vanilla navmesh availability check.** [`Patch30_FormationGetOrderPositionOfUnit.Prefix`](Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs) returned false to skip vanilla after building a `WorldPosition` from layout math + `Scene.GetGroundHeightAtPosition`. The vanilla Hold branch delegates to `GetOrderPositionOfUnitAux`, which validates the candidate via `Mission.IsFormationUnitPositionAvailable` and falls back to `unit.GetWorldPosition()` if unavailable. Our skip dropped that gate — custom layout positions could land on cliffs, walls, siege props, or non-navigable terrain. **Fix:** patch now calls `mission.IsFormationUnitPositionAvailable(ref candidate, team)` before setting `__result`. If unavailable → returns true (vanilla handles via its own fallback).

**FINDING 2 (MEDIUM) — Cache + assignment mutations on the hot Prefix path were not thread-safe.** [`FormationLayoutService`](Main/Features/MixedFormations/FormationLayoutService.cs) used regular `Dictionary` for `_layoutByFormation` and `_assignmentCache`, plus mutated `SlotAssignment.ByAgentIndex` for new agents — all from the worker-thread Patch30 hot path. Vanilla shows clear multi-threading markers via `ilspycmd`: `Formation.OrderPositionLock`, `IsFormationUnitPositionAvailableAuxMT` uses `using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))`, `_MT` suffix on positioning helpers. **Fix:** added `private readonly object _lock = new();` and wrapped all dict mutations + reads on the hot path. Two regression tests added.

**RCA + durable lessons codified** (per the cross-session memory contract from SiegeDismount review #34):

1. New feedback memory `feedback_replicate_vanilla_safety_gates_in_prefix.md` — when a Harmony Prefix returns false to skip vanilla, decompile the FULL call chain (entry method + every helper it delegates to) and replicate every safety gate.
2. New feedback memory `feedback_detect_engine_threading_via_mt_suffix.md` — Bannerlord names multi-threaded helpers with `_MT` suffix; before patching `Formation`/`Mission`/`Scene`/positioning methods, grep for these markers and lock or use immutable state if present.

Both memories indexed in `MEMORY.md`; auto-loaded every future Claude session.

Net: 38 MixedFormations tests pass (+2 thread-safety regression tests).

### Fix: MixedFormations — deep-review findings (hot-path service caching + future-proof switch)

After the initial port, `/deep-review MixedFormations` (5-agent parallel review) returned PASS on standards/compatibility/completeness/data-flow but flagged 1 MEDIUM and 1 LOW efficiency/quality finding. Both fixed in same session.

**MEDIUM — `IoC.Resolve<IFormationLayoutService>()` in Patch30 hot path.** Fires per-unit-per-formation-position-recalculation — up to 40,000× per frame in worst-case 200-unit formations. **Fix:** [`Patch30_FormationGetOrderPositionOfUnit`](Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs) now stores the service in a `private static IFormationLayoutService? _service` and uses `_service ??= IoC.Resolve<>()`.

**LOW — `LayoutPositioner.BuildInitialAssignment` switch had no `default:`.** A future 6th `FormationLayoutType` value would silently produce an empty assignment. **Fix:** added `default: throw new ArgumentOutOfRangeException(...)`.

Two known-limitations documented in [docs/features/mixed-formations.md](docs/features/mixed-formations.md): layout persists for the entire mission once assigned (composition-change immune); cycle hotkey within first ~1 second silently does nothing.

### Feat: MixedFormations — port external sibling module into Main/Features/ (Patch30)

Refactored the developer-built `MixedFormations` module (#2 of 7 dropped at `Downloads/Features_fixed/`) into TAOM's adapter/service/IoC pattern.

**What it does:** when a formation contains both melee and ranged units AND it's holding position (`MovementOrder.MovementStateEnum.Hold`), reorder the units per the chosen layout: Infantry-front-Ranged-back (default), Ranged-front-Infantry-back, Ranged-on-the-wings (Infantry center), or Checkerboard. Auto-applies a default layout to "mixed" formations every 1s; player can cycle layouts via configurable hotkey (default `L`).

**Architecture:**
- [`LayoutPositioner`](Main/Features/MixedFormations/LayoutPositioner.cs) — pure-function slot-assignment math (4 layout algorithms); fully unit-testable
- [`FormationLayoutService`](Main/Features/MixedFormations/FormationLayoutService.cs) — singleton; owns per-formation layout dict + assignment cache + cycle/auto-apply
- [`MixedFormationsMissionBehavior`](Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs) — engine bridge; per-frame tick, 1s default-apply, every-frame hotkey poll
- [`Patch30_FormationGetOrderPositionOfUnit`](Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs) — Harmony Prefix
- [`IFormationAdapter`](Main/Adapters/IFormationAdapter.cs) — NEW; load-bearing for SmartCavalryAI (feature #3) and CompanionTactics (feature #7). Wraps `Formation` properties; service never sees `Formation` directly (ADR-007)
- MCM: 4 settings under `Battle Tactics / Mixed Formations` group folded into [`TaomSettings.cs`](Main/Features/TaomSettings.cs)

**Two dead settings dropped on port** — per the `feedback_user_facing_promise_must_match_code.md` memory rule: the original module exposed `InfantryRowDepth` (1–10, default 3) and `RangedRowDepth` (1–10, default 2) settings with HintText promising to control row depth, but tracing through decompiled code showed they were never read. `filesPerRow` is computed from formation `Width / (Interval + 1)`. Both settings removed on port.

**Tests:** 36 unit tests in `LayoutPositionerTests` (11 tests) and `FormationLayoutServiceTests` (25 tests).

Source material: `Downloads/Features_fixed/_decompiled/MixedFormations/MixedFormations.decompiled.cs`. Mathematical layout algorithms preserved verbatim; developer's threshold values (≥10 total, ≥5 minority, ≥20% minority share) preserved.

Closes #112.



### Feat: MixedFormations — port external sibling module into Main/Features/ (Patch30)

Refactored the developer-built `MixedFormations` module (#2 of 7 dropped at `Downloads/Features_fixed/`) into TAOM's adapter / service / IoC pattern. Replaces a standalone Bannerlord module with `Main/Features/MixedFormations/` so it ships as part of the TAOM DLL.

**What it does:** when a formation contains both melee and ranged units AND it's holding position (`MovementOrder.MovementStateEnum.Hold`), reorder the units per the chosen layout: Infantry-front-Ranged-back (default), Ranged-front-Infantry-back, Ranged-on-the-wings (Infantry center), or Checkerboard. Auto-applies a default layout to "mixed" formations every 1s; player can cycle layouts on the selected formations (or all if none selected) via configurable hotkey (default `L`).

**Architecture:**
- [`LayoutPositioner`](Main/Features/MixedFormations/LayoutPositioner.cs) — pure-function slot-assignment math (4 layout algorithms + mid-mission newcomer assignment); fully unit-testable
- [`FormationLayoutService`](Main/Features/MixedFormations/FormationLayoutService.cs) — singleton; owns per-formation layout dict + assignment cache + cycle/auto-apply orchestration; cleared on `OnEndMission`
- [`MixedFormationsMissionBehavior`](Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs) — engine bridge; per-frame tick, accumulates 1s for default-apply, every-frame hotkey poll
- [`Patch30_FormationGetOrderPositionOfUnit`](Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs) — Harmony Prefix on `Formation.GetOrderPositionOfUnit`; queries service for plane position; if non-null, builds `WorldPosition` via `Mission.Current.Scene.GetGroundHeightAtPosition` and skips vanilla
- [`IFormationAdapter`](Main/Adapters/IFormationAdapter.cs) — NEW; load-bearing for SmartCavalryAI (feature #3) and CompanionTactics (feature #7). Wraps `Formation.{CountOfUnits, OrderPosition, OrderPositionIsValid, Direction, Width, Interval, IsHolding, Units}`. Service never sees `Formation` directly (ADR-007)
- MCM: 4 settings under `Battle Tactics / Mixed Formations` group — Enable, DefaultLayout (0..3), CycleHotkey, Debug. Folded into [`TaomSettings.cs`](Main/Features/TaomSettings.cs) per the consolidation rule.

**Two dead settings dropped on port** — per the [`feedback_user_facing_promise_must_match_code.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_user_facing_promise_must_match_code.md) memory rule (codified after SiegeDismount Codex review #34): the original module exposed `InfantryRowDepth` (1–10, default 3) and `RangedRowDepth` (1–10, default 2) settings with HintText promising to control row depth. Tracing them through the decompiled code showed they are never read anywhere — `filesPerRow` is computed from formation `Width / (Interval + 1)`. Rather than ship the user-facing-promise mismatch, both settings were removed on port. If row-depth control is desired later, that's a Phase 2 enhancement with a real implementation.

**No keyword-based scene detection** — per the [`feedback_substring_keyword_matches_external_data.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_substring_keyword_matches_external_data.md) memory rule, this feature uses no string substring matching against engine state; layouts are gated purely by `MovementOrder.MovementStateEnum.Hold` (an authoritative engine flag).

**Tests:** 36 unit tests in [`LayoutPositionerTests`](TAOM.Tests/Features/MixedFormations/LayoutPositionerTests.cs) (11 tests covering all 4 layouts, slot non-overlap, narrow-formation sqrt fallback, mid-mission newcomer assignment) and [`FormationLayoutServiceTests`](TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs) (25 tests covering: 5 gating paths, layout get/set/cycle full wraparound, 5 mixed-detection threshold cases, default-applier paths, mission-end cleanup). Build green, 1447/1447 total tests pass.

Source material: [`Downloads/Features_fixed/_decompiled/MixedFormations/MixedFormations.decompiled.cs`](Downloads/Features_fixed/_decompiled/MixedFormations/MixedFormations.decompiled.cs). Mathematical layout algorithms preserved verbatim (block placement, wing splitting, checkerboard parity); developer's threshold values (≥10 total, ≥5 minority, ≥20% minority share) preserved.

Not-tested: `FormationAdapter` and `Patch30` (require live `Formation` and `Scene`); covered by in-game golden-path verification per [docs/features/mixed-formations.md](docs/features/mixed-formations.md#verification).

### Docs: CLAUDE.md — Patch29_CCBodyProperties row updated to list third target

User explicitly authorized CLAUDE.md edit. Added `CharacterCreationCultureStageVM.OnCultureSelection` to the Patch29 row's target list and updated the Feature column to mention "culture-stage-VM body re-apply" alongside the existing two intercepts. The row now accurately reflects the 3-patch architecture deployed in `efb2eaa` and finalized in `e5d8fc3` — the OnCultureSelection postfix is the canonical hook (LOTRAOM-1.2 `OnCultureSelected` equivalent for v1.3) that re-applies the configured body after vanilla `InitializePlayersFaceKeyAccordingToCultureSelection` overwrites it with the culture XML default.

### Feat: Messengers — port LOTRAOM messenger system to TAOM (1.3.15)

Adds a hero-to-hero messenger system: dispatch a paid messenger from the encyclopedia hero page (or in-person dialog), the messenger travels for N in-game days at a speed scaled to map size, and on arrival the player gets a "Speak / Dismiss" inquiry that opens a real conversation mission (settlement-aware: enters the settlement scene if the target is in one, otherwise opens a field conversation; restores player position on mission end). Random ambush "messenger lost" rolls during travel. Public hooks `IMessengerService.SendMessenger(Hero)` / `CanSendMessenger(Hero, out TextObject)` for future cross-feature integration.

Ported from LOTRAOM (Bannerlord 1.2.12) with TAOM conventions: adapter discipline (service uses `HeroSnapshot` POCO, never sealed `Hero`), primitive-dict `SyncData` (no `SaveableTypeDefiner`), MCM via `TaomSettings.Messengers`, JSON advanced tunables in `messenger_config.json` validated per the "Config Providers MUST Validate" rule, all 12 TAOM-supported localization languages.

**1.3.15 API drift caught and applied:**
- `IMissionListener.OnInitialDeploymentPlanMade(BattleSideEnum, bool)` removed → replaced by `OnDeploymentPlanMade(Team, bool)`
- `TextObject.Empty` removed → `TextObject.GetEmpty()`
- `MobileParty.Position2D` setter removed → `MobileParty.Position = new CampaignVec2(vec, isOnLand: true)`
- `IMapPoint.Position2D` (Vec2) renamed → `IMapPoint.Position` (CampaignVec2 — `.ToVec2()` to convert) — **caught at compile time, not in initial research**
- `CampaignTime` ctor became internal → use `CampaignTime.Now.ToDays` for elapsed-time math (store dispatch time as `double` days, not ticks)
- `OpenConversationMission` gained 5th optional `isMultiAgentConversation` param (default false; existing 4-arg call still compiles, no change required)

**Architecture (15 files, ~370-line behavior + 100-line service + 5 supporting types):**
- `MessengerCampaignBehavior` (boundary) — registers events, implements `IMissionListener`, registers 6-line dialog tree, orchestrates settlement-vs-field encounter routing, restores player position via one-shot `TickEvent` after `OnEndMission`. Touches sealed types directly.
- `MessengerService` (Reuse.Singleton, pure logic) — `CanSendMessenger`, `RollAccident`, `AdvancePosition`, `CalculateMessengerSpeed`. Tests injected with NSubstitute mocks.
- `MessengerStateStore` — `Dictionary<heroId, PendingMessenger>`, `Serialize` → `Dictionary<string,string>` (`"days|x|y|arrived"`), `TryDeserialize` drops malformed entries with logged warning.
- `MessengerConfigProvider` (validates) — range-checks `accidentChancePerHour` ∈ [0,1] and `travelSpeedMultiplier` ∈ [0.1, 10], reverts + warns on invalid.
- `MessengerSettingsProvider` — wraps the 4 new MCM properties (`EnableMessengers`, `MessengerGoldCost`, `MessengerTravelDays`, `MessengerAccidents`).
- UIExtenderEx: prefab extension appends a `<ListPanel>` containing a "Send Messenger" button after `RichTextWidget[@Text='@InformationText']` in `EncyclopediaHeroPage`; mixin exposes `IsMessengerAvailable` / `SendMessengerCost` / `SendMessengerHint` / `SendMessengerActionName` data sources and `ExecuteSendMessenger` click command.

**Deep-review fixes applied in-session (2 HIGH, 2 MEDIUM):**
1. **HIGH (latent bug):** `Hero.FindFirst` iterates `Campaign.Current.Characters` (incl. dead/disabled), not `AllAliveHeroes`. If a target died after dispatch, `HandleArrivedMessenger`→`IsTargetAvailableNow` would return false → `WaitForNextTick` indefinitely (messenger pile-up). Added a permanent-unavailability branch (`!target.IsAlive || HeroState.Disabled`) that fires `NotifyMessengerLost` + new `taom_messenger_recipient_gone` localization key + `RemoveFromList`. Distinct from the temporary "in MapEvent" path which still defers.
2. **HIGH (perf):** `OnHourlyTick` allocated `new List<string>()` every campaign hour. Replaced with reusable `_toRemoveScratch` field cleared per tick.
3. **MEDIUM (perf):** `IMessengerStateStore.GetAll()` allocated a new `List<>` per call. Returns `_messengers.Values` (live `Dictionary.ValueCollection`) — zero allocation, `IReadOnlyCollection<>` surface preserved.
4. **MEDIUM (perf):** `MessengerEncyclopediaMixin.OnRefresh` allocated `HintViewModel`+`TextObject` every encyclopedia refresh. Cached the four state-independent hints at construction; rejection-reason hint keyed by `MessengerValidationResult` enum so it only re-allocates on rejection-class transition.

**Codex review round 1 (1 CRITICAL disputed, 3 HIGH fixed, 3 MEDIUM):**
- HIGH: `MessengerCampaignBehavior` is `Reuse.Singleton`, so a single instance survives across campaigns within the same Bannerlord process. `_dialogsRegistered=true` set by campaign 1 would have suppressed `AddDialogOptions` in campaign 2. Added `_lastSessionStarter` tracking + `_justLoadedFromSave` flag — when starter changes, reset all per-campaign instance state; clear `_store` only on fresh new game (loaded games already have correct state via SyncData).
- HIGH: arrival-time validation only screened dead/disabled, but send-time validation rejected fugitive + several inactive states. A target that became fugitive mid-flight could pass through and trigger `StartMessengerConversation` with no settlement / no party. Permanent-loss check now covers `!IsAlive`, `Disabled`, `IsFugitive`, `!IsActive && !IsWanderer`, `!IsActive && IsWanderer && HeroState != NotSpawned`.
- HIGH: `Vec2` (TaleWorlds.Library) leaked across the service boundary. Replaced with TAOM-owned `MapCoord` struct (X/Y/Invalid/Zero/IsValid/Length/Normalized/+/-/*); behavior converts `Vec2 → MapCoord` at the boundary. Tests + service + domain are now free of TaleWorlds types.
- MEDIUM: `<` and `>` range checks both fail for NaN, so `accidentChancePerHour: NaN` would propagate NaN through accident roll and speed calc. Validate now rejects `IsNaN || IsInfinity` before range check.
- MEDIUM: `EnableMessengers` was only checked at registration. Mid-game disable left dialog hook + tick loop active. Added gates to `SendMessenger`, `CanSendMessenger`, `OnHourlyTick`, `DialogCondition_CanSend`.
- DISPUTED (CRITICAL "fat behavior"): the behavior IS the TaleWorlds boundary per ADR-002/ADR-007; pure logic delegates to service; line count is genuine engine-coupled orchestration. Deep-review's standards agent independently confirmed compliance. Documented inline.
- DISPUTED (MEDIUM "Append-as-child"): UIExtenderEx 2.12.0 enum is `{Prepend, ReplaceKeepChildren, Replace, Child, Append, Remove}` — `Child` is into-as-last-child; `Append` is sibling-after. Codex round-2 self-review confirmed the dispute by citing the official UIExtenderEx docs.

**Codex review round 2 — self-review of round-1 fixes (1 HIGH regression caught + 3 MEDIUM):**
- HIGH (regression): conditional registration in `SubModule.cs` (`if (Settings.EnableMessengers) AddBehavior`) caused saves with pending messengers to lose state when loaded with the toggle off — vanilla `CampaignBehaviorManager` only persists registered behaviors. Fix: register unconditionally; runtime gates already enforce "frozen when disabled" semantics.
- MEDIUM: a player-edited negative `MessengerGoldCost` would pass validation (gold check is `playerGold < cost`) and `GiveGoldAction.ApplyBetweenCharacters(player, null, -100)` would GRANT the player 100 gold while still queuing a messenger. Same with non-positive travel days forcing instant arrival. Fix: `MessengerSettingsProvider` now clamps `MessengerGoldCost` (10–500) and `MessengerTravelDays` (1–10) — out-of-range reverts to default.
- MEDIUM: `EnableMessengers` flipping OFF between the initial dialog line (`DialogCondition_CanSend`) and the cost line (`DialogCondition_HasGold`) let the player click "Send" → silently no-op → dialog still advanced to the success line. Fix: `DialogCondition_HasGold` now re-checks `EnableMessengers`.
- MEDIUM: `double.TryParse(NumberStyles.Float)` accepts `NaN` / `Infinity` / `-Infinity`. Tampered save with `NaN|0|0|0` would parse cleanly, then `current - NaN` never reaches `>= travelDays` → hero stuck as already-pending forever. Also `MapCoord.IsValid` only rejected NaN, while `Vec2.IsValid` rejects both NaN and Infinity (parity gap). Fix: `PendingMessenger.TryDeserialize` rejects non-finite for all three numeric fields; `MapCoord.IsValid` matches `Vec2.IsValid` semantics.

**Tests:** 61 new unit tests across 3 files (55 initial + 3 NaN/Infinity config + 3 non-finite deserialize). 1411/1411 total tests pass. Coverage: every `MessengerValidationResult` rejection path, every accident-roll boundary, every position-math edge case, every config-validation rule (incl. NaN/Infinity for both fields), every non-finite save-format input.

**Localization:** 13 string files (1 EN base in `taom_messenger_strings.xml` + 12 language variants matching TAOM's existing language coverage convention). 29 keys with prefix `taom_messenger_*`. 12 `language_data.xml` files updated.

**Test infrastructure update:** existing `AllLanguageDirs_HaveExactlyFiveLanguageFiles` test renamed to `*Six*` (now 6 entries: module + wanderer + companion + cc + career + messenger); new test enforces every language declares the messenger entry.

**GitHub Issue:** #109 — feat(messengers): port LOTRAOM messenger system to TAOM (1.3.15)

### Feat: Player starting gold + CC equipment persistence (port from LOTRAOM `StartingEquipmentGold`)

Adds two adjacent capabilities the LOTRAOM 1.2.12 `StartingEquipmentGold/` module provided that TAOM had only half-built: configurable per-culture **player starting funds** at character-creation finalize, and **persistence** of the youth option's equipment roster onto `Hero.MainHero.BattleEquipment` / `CivilianEquipment` (previously the CC preview was visual-only — the player exited CC with vanilla default equipment regardless of the option chosen).

**Why this exists:** The existing `StartupResources` feature explicitly skipped the player clan (`StartupGoldService.cs:40 if (hero.IsPlayerClan) continue;`) — only NPC lords got gold. And `NarrativeMenuBuilder.UpdateYouthEquipment` mutated the CC preview character but never wrote to the player's persistent equipment slots. New campaigns started with vanilla default 1000 denars and vanilla default starting equipment regardless of culture or youth option.

**Architecture (XML/JSON-driven, not LOTRAOM's hard-coded C# dictionary):**

- **Gold:** new `playerGold="…"` attribute on `<Culture>` rows in [`startup_resources_config.xml`](Main/_Module/ModuleData/startup_resources/startup_resources_config.xml). Per-culture only (per the user's scope choice this session). Range-validated `[0, 10_000_000]` per the "Config Providers MUST Validate" rule — out-of-range, non-numeric, or sign-flipped values revert to 0 with a logged warning. Missing attribute defaults to 0 silently. New service [`PlayerStartupGoldService`](Main/Features/StartupResources/PlayerStartupGoldService.cs) reuses the existing `IGoldGiftAdapter` (which already wraps `GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, true)`).
- **Equipment:** new ADR-007 adapter [`IPlayerEquipmentAdapter`](Main/Adapters/IPlayerEquipmentAdapter.cs) wraps `MBEquipmentRoster.AllEquipments` filter by `IsBattle`/`IsCivilian` and `Equipment.FillFrom` mutate-in-place. Service [`PlayerEquipmentService`](Main/Features/CharacterCreation/PlayerEquipmentService.cs) builds the roster ID via the existing TAOM convention `player_char_creation_{culture}_{titleType}_{m|f}` (promoted from `NarrativeMenuBuilder.BuildEquipmentRosterId` to a shared helper [`PlayerEquipmentRosterIds`](Main/Features/CharacterCreation/PlayerEquipmentRosterIds.cs)). Adapter returns an enum `PlayerEquipmentApplyResult` so the service surface stays free of sealed TaleWorlds types.
- **Wiring:** both services injected into `CharacterCreationContentService` and called from `OnCharacterCreationFinalize` after `AssignCareer`. Reads `selectedCulture.StringId` and `manager.CharacterCreationContent.SelectedTitleType` directly (not via `Hero.MainHero.Culture` — see plan risk note about the in-flight finalize-order culture override).

**API verification (v1.3.15 vs v1.2.12 LOTRAOM source):**

Run `ilspycmd` on installed v1.3.15 DLLs before writing the adapter. Two drifts caught:
1. `MBEquipmentRoster.GetBattleEquipments()` / `GetCivilianEquipments()` (LOTRAOM 1.2 surface) **don't exist** in v1.3.15 — the public surface is `AllEquipments` + filter by `Equipment.IsBattle` / `IsCivilian` properties.
2. LOTRAOM wrote to `CharacterObject.PlayerCharacter.FirstBattleEquipment.FillFrom(...)`. In v1.3.15 the same backing object is exposed cleaner via `Hero.MainHero.BattleEquipment.FillFrom(...)` (the `CharacterObject.FirstBattleEquipment` getter on a Hero just delegates to `HeroObject.BattleEquipment` — same Equipment instance, cleaner v1.3 surface).

The `GiveGoldAction.ApplyBetweenCharacters(Hero giverHero, Hero recipientHero, int amount, bool disableNotification = false)` signature matches LOTRAOM's call exactly — already in production use via the existing `GoldGiftAdapter`.

**Tests:** 28 new + extended unit tests, all green. 1340/1340 total tests pass.
- 5 new `StartupResourcesConfigProviderTests` cases — `playerGold` parsed, negative rejected, over-cap rejected, non-numeric rejected, missing attribute silent
- 8 new `PlayerStartupGoldServiceTests` — culture match (case-insensitive), unknown culture warn, zero-gold skip, null/empty culture/hero no-ops, info-log content
- 9 new `PlayerEquipmentServiceTests` — male/female roster suffix, null/empty input no-ops, all four `PlayerEquipmentApplyResult` branches mapped to correct log levels
- 6 existing `CharacterCreationContentServiceTests` — updated for the new constructor signature (added `IPlayerStartupGoldService` and `IPlayerEquipmentService` dependencies)

**Initial culture seeds for `playerGold`:** Elven 8,000–10,000 (Rivendell/Lothlorien wealthiest), Dwarf 7,500, Dark factions 6,000, Human Good kingdoms 5,000, Tribal/Eastern 4,000. Tunable in [`startup_resources_config.xml`](Main/_Module/ModuleData/startup_resources/startup_resources_config.xml) — edits require Bannerlord process restart (singleton config cache), not save-load.

**Codex Phase 3 self-review of fixes (2026-05-06, post-commit `ab0910f`):**
- **[HIGH] `shaghana`/`abanissa` narrative menu coverage missing.** Codex Phase 3 traced the player flow end-to-end and caught a dead-end: both kingdoms are CC-selectable per `cultures.json` but have ZERO entries across all 5 narrative menu JSONs (parents/childhood/education/youth/adulthood). A player picking them at the culture step renders an empty narrative page; vanilla CC throws on advance from empty `SelectionList`. The `playerGold` rows added earlier this session are functionally dead because finalize is unreachable. **Out of scope for #110** (this is narrative menu authoring, not gold/equipment); filed as [#111](https://github.com/haterade22/TAOM/issues/111) with three remediation options. Added a defensive XML comment in `startup_resources_config.xml` flagging the gap explicitly so future tuners do not think the rows are functional. Per "no silent deferrals" rule, the deferral is recorded in: GitHub issue #111, RCA bug I, this CHANGELOG entry, and an in-line XML comment.
- **[LOW] XML header comment misattributed `influence` to NPC lords.** `StartupInfluenceService` actually applies to eligible CLANS (not lords). Corrected the comment; future tuners reading the config now understand the consumer correctly.

**Codex adversarial-review fixes (2026-05-06, post-deep-review):**
- **[P1] Civilian-equipment guard targeted the wrong dead singleton.** The deep-review fix in `PlayerEquipmentAdapter.cs` compared `hero.CivilianEquipment` against `Campaign.Current.DeadBattleEquipment` — but in v1.3.15 `Hero.CivilianEquipment` falls through to `Campaign.Current.DeadCivilianEquipment` (a separate singleton, re-verified via `ilspycmd`). The civilian guard never tripped, so calling `FillFrom` on an uninitialized-civilian hero would have corrupted the shared `DeadCivilianEquipment` for the rest of the session. Fixed by tracking `deadBattle` and `deadCivilian` separately and checking each slot against its own singleton.
- **[P2] `shaghana` and `abanissa` kingdoms missing from startup_resources_config.xml.** Both are full **independent kingdoms** in the Harad region (Shaghâna = "the eastern reach of Harad", 9 NPC lords; Âbanissa = "the deep south of Harad", 8 NPC lords) — registered in [`taom_spkingdoms.xml`](Main/_Module/ModuleData/taom_spkingdoms.xml) with their own rulers (Taskral / Châjaphân), banner keys, settlements, and CC-selectable cultures. They were missing from startup config — meaning every Shaghana/Abanissa lord NPC was getting 0 startup gold and 0 influence on a new game, and any player picking those cultures got 0 starting funds. Added rows with `gold="50000" influence="100" playerGold="4000"` matching the Harad tier (`aserai`). The first version of this fix incorrectly described them as "Aserai-region cultures with no NPC clans" — corrected after user pointed out they are full peer kingdoms.
- **Documented Claude/Codex disagreement worth a memory entry:** the Claude `taleworlds-researcher` agent reported earlier that BOTH `BattleEquipment` and `CivilianEquipment` getters fall back to `DeadBattleEquipment`. That was wrong — Codex re-decompiled and found the correct `DeadCivilianEquipment` separate fallback. Lesson: when one agent's API claim contradicts another, re-run `ilspycmd` rather than trusting the more confident agent. The Claude data-flow agent also flagged shaghana/abanissa but dismissed them as "may be intentional zero-gold cultures" — Codex was right to push back.

**Deep-review fixes (Agent 5 data-flow trace, 2026-05-06):**
- Added `<Culture id="empire" .../>` with `playerGold="4000"` to startup config — Dunland (CC-selectable per `cultures.json`) was missing from the seed XML and would have silently granted 0 gold.
- Changed `taom_youth_sturgia_1` (Royal Guard of Dale) `title_type` from `"retainer"` to `"guard"` — vanilla SandBox `sandbox_equipment_sets.xml` has no `sturgia_retainer` roster pair, so the first sturgia youth option would have shipped with no equipment applied. `guard` matches both the option's text ("Royal Guard of Dale") and an existing roster.
- Routed `CareerMenuService.GetCareerMenuCharacterArgs` (the career-screen visual preview) through the new shared `PlayerEquipmentRosterIds.Build` helper instead of inlining the roster-ID format string. Eliminates the third independent construction of the `player_char_creation_*` convention.
- Added `Campaign.Current.DeadBattleEquipment` guard to `PlayerEquipmentAdapter.ApplyRosterToPlayer`. `Hero.BattleEquipment` falls through to a process-wide shared `DeadBattleEquipment` singleton when the hero's `_battleEquipment` is null; calling `FillFrom` on that singleton would corrupt equipment for every dead/uninitialized hero in the session. MainHero at CC finalize is always initialized so this is defensive — but the adapter accepts any `heroId` and shouldn't expose the foot-gun to future callers.

**Out of scope (deliberate):** per-youth-option gold (per-culture only this session), starting items / starting troops (LOTRAOM had this; CareerSystem covers troop starts in TAOM), MCM live retuning. The visual `UpdateYouthEquipment` preview is preserved unchanged — it's orthogonal to persistence.

**Pre-existing tech debt noted by deep-review (NOT fixed this session, separate cleanup):** `CharacterCreationContentService.AssignCareer` resolves `ICareerCreationHandler` and `ICareerRegistry` via `IoC.Resolve<>` (lines ~218, 235) — service-locator anti-pattern flagged by Standards agent. Pre-dates this session. Should be lifted to constructor injection in a follow-up.

Plan: [`C:\Users\mikew\.claude\plans\please-investigate-this-that-lovely-pine.md`](../../.claude/plans/please-investigate-this-that-lovely-pine.md)
GitHub issue: [#110](https://github.com/haterade22/TAOM/issues/110)
Root cause analysis: [docs/reviews/rca-player-startup-2026-05-06.md](docs/reviews/rca-player-startup-2026-05-06.md) — 7 bugs in 1 session across 3 systemic root cause classes (enumeration from existing-config-rows-not-source-of-truth; insufficient decompilation of property bodies; ID classification by assumption instead of grep). Two new memory entries created: `feedback_enumerate_from_source_of_truth.md`, `feedback_classify_by_grep_not_by_assumption.md`.

Constraint: youth-option title_type strings (`retainer`, `warrior`, etc.) must match between `youth_menu.json` and the equipment XML roster IDs — typos surface as a "roster not found" warning at finalize and the player gets vanilla equipment. No crash.

Research: `GiveGoldAction.ApplyBetweenCharacters` (TaleWorlds.CampaignSystem.Actions), `MBEquipmentRoster.AllEquipments` (TaleWorlds.Core), `Equipment.FillFrom` (TaleWorlds.Core), `Hero.BattleEquipment` / `CivilianEquipment` (TaleWorlds.CampaignSystem), `CharacterCreationContent.SelectedTitleType` (TaleWorlds.CampaignSystem.CharacterCreationContent).

Save-compat: Player gold + equipment writes happen at CC finalize on new-game start only — no save-format changes, no impact on existing saves.



### Fix: SiegeDismount — Codex adversarial review HIGH findings

After `/deep-review` produced a passing verdict and we fixed two HIGH findings on the data-flow path, `/review-codex` (Codex CLI 0.128.0, run 2026-05-06) produced THREE additional findings — two HIGH, one MEDIUM. All three confirmed and fixed in the same session per the "no silent deferrals" rule. The Codex review file is preserved at [docs/reviews/codex-adversarial-siegedismount-2026-05-06.md](docs/reviews/codex-adversarial-siegedismount-2026-05-06.md) (reconstructed from stdout because Codex's `apply_patch` was rejected by the read-only sandbox).

**FINDING 1 (HIGH) — scene-name keyword fallback still matched 24 vanilla siege center scenes.** The `/deep-review` pass narrowed `SceneSiegeKeywords` from `[siege, wall, gate, assault, breach]` to `[siege, assault, breach]`. Codex grep found that `siege` still matches 24 vanilla `Location id="center"` entries in [settlements.xml](Main/_Module/ModuleData/settlements.xml) — `empire_siege_001`, `khuzait_castle_siege_001`, `sturgia_castle_siege_001` etc. Those scenes can be loaded as non-combat Missions (settlement-center cinematics, story events) where `IsSiegeBattle=false`, falsely clobbering the player's mount. **Fix:** removed the keyword fallback entirely. [`SiegeDismountService.IsSiegeMission`](Main/Features/SiegeDismount/SiegeDismountService.cs) now returns `isSiegeBattle` directly. Modded siege scenes that don't set the engine flag won't trigger the feature — documented requirement. Tests rewritten: 9-row data-test pinning the new contract against vanilla and TAOM scene names.

**FINDING 2 (HIGH) — `ItemModifier` was dropped on auto-remount.** I documented this as a "known limitation" in the deep-review pass. Codex pointed out that the modifier-preserving [`ItemRoster.AddToCounts(EquipmentElement, int)`](Main/Adapters/PartyMountInventoryAdapter.cs) overload exists in v1.3.15 (verified via `ilspycmd`); the bare `(ItemObject, int)` overload internally drops the modifier. **Fix:** [`MountSnapshot`](Main/Features/SiegeDismount/Models/MountSnapshot.cs) now carries the full `EquipmentElement` (internal — TaleWorlds types stay inside the implementation; `IMountSnapshot` interface unchanged). [`PlayerMountAdapter.Capture`](Main/Adapters/PlayerMountAdapter.cs) uses the full-data constructor; [`PartyMountInventoryAdapter.Deposit/Withdraw`](Main/Adapters/PartyMountInventoryAdapter.cs) and [`PlayerMountAdapter.Restore`](Main/Adapters/PlayerMountAdapter.cs) use the `EquipmentElement` overload via concrete-type cast. A "Sharp" or "Damaged" horse now round-trips correctly.

**FINDING 3 (MEDIUM) — `DismountKeepOnMap` was a silent no-op despite MCM hint promising "horse on map, player on foot".** Inherited bug — the original developer's decompiled module had the same pre-existing no-op. Full implementation requires `Mission.SpawnAgent` plumbing not in Phase 1 scope. **Fix:** documented honestly. Mode 1 logs a `LogWarning` explaining it's "Reserved / equivalent to Vanilla until somebody implements the actual map-side horse spawn." MCM dropdown label and hint text updated to "(currently equivalent to Vanilla — full implementation deferred)" so the user-facing promise matches reality. Enum value retained for save-compat.

**RCA / Preventive actions** (per `/review-codex` Phase 3e — three Why-We-Missed analyses recorded in the review file):

1. Future feature ports interpreting scene names: grep across ALL `ModuleData/*.xml` for substring overlap, not just feature-specific custom XML.
2. When an adapter touches an inventory or equipment slot that vanilla treats as `EquipmentElement`-shaped, prefer the `EquipmentElement`-overload of the inventory API. Search the API surface for both before settling on the simpler `ItemObject` overload.
3. When porting a feature with multiple modes: read the user-facing strings (MCM hints, dropdown labels) and trace them to the implementation. If the promise doesn't match the code, either fix the code or fix the promise — never ship the mismatch.

Net: 33 SiegeDismount tests pass (same count — replaced false-positive scene-name tests with new IsSiegeBattle-only tests; added KeepOnMap warning test; otherwise behavior preserved). 1405/1405 total tests green.

### Fix: SiegeDismount — deep-review HIGH findings (false-positive dismount + config validation)

Two HIGH findings from `/deep-review` Agent 5 (Data Flow), fixed in the same session per the "no silent deferrals" rule:

**GAP 1 — out-of-range MountBehavior int silently captured mount with no action.** A user manually editing `ModuleData/MCM/Global/TAOM.json` to set `SiegeMountBehavior` outside `[0, 3]` produced an undefined enum value. The switch had no `default:` case, so `_capturedSnapshot` got set but no clear/deposit/restore fired — the player's mount data was read but no effect occurred. Fix: added `default:` case to the switch in [`SiegeDismountService.OnMissionStart`](Main/Features/SiegeDismount/SiegeDismountService.cs) that logs `LogWarning` and treats unknown values as a full no-op. Two regression tests cover the path. Per `csharp-architecture.md` "Config Providers MUST Validate" rule.

**GAP 2 — false-positive siege detection on real TAOM castle scenes.** The keyword fallback `IsSiegeMission` matched substrings `gate` and `wall`, falsely firing for [`castle_orthanc_gate`](Main/_Module/ModuleData/custom_settlements.xml#L74) (Isengard's Orthanc Gate castle) and [`castle_gundabad_wall`](Main/_Module/ModuleData/custom_settlements.xml#L344) (Gundabad Wall castle) — both real TAOM `Location id="center"` scenes used during normal castle visits. With `DismountKeepOnMap` or `DismountToInventory` modes, the player's mount would have been incorrectly removed during a non-siege visit. Fix: narrowed `SceneSiegeKeywords` to `siege`, `assault`, `breach` only — removed `gate` and `wall`. Real sieges hit `Mission.IsSiegeBattle = true` directly; the keyword fallback is only for modded/custom siege scenes that fail to set that flag. Four data-row regression tests cover the false-positive scenes.

**KL 1, KL 3 — state hygiene.** `OnMissionEnd`'s early-return path now clears the stale `_capturedSnapshot` so the singleton doesn't carry mount-id strings between missions. Added a guard in `OnMissionStart` for the theoretical case where `HasMount()` returns true but `Capture()` returns an empty snapshot. Three regression tests.

Net: 33 SiegeDismount tests pass (+9 from this fix). 1404/1404 total tests green. Saving the deep-review findings cost less than 30 minutes; in-game discovery would have cost a player having their mount silently disappear when visiting Orthanc Gate.

### Feat: SiegeDismount — port external sibling module into Main/Features/

Refactored the developer-built `SiegeDismount` module (one of seven dropped at `Downloads/Features_fixed/`) into TAOM's adapter / service / IoC pattern. The original was a standalone Bannerlord module with its own `SubModule.xml`, `MissionBehavior`, and MCM settings; this commit replaces it with `Main/Features/SiegeDismount/` so it ships as part of the TAOM DLL with the same MCM, logging, and toggle conventions as the rest of TAOM.

**What it does:** when a siege mission begins, the player's mount + harness are auto-handled per the user's MCM choice — Vanilla (no change), KeepOnMap, ToInventory, or AutoRemount-after-siege (default). Eliminates the on-horseback-in-fortress-courtyard immersion break for LOTR sieges (Helm's Deep, Minas Tirith, Erebor's gates).

**Architecture:**
- [`SiegeDismountService`](Main/Features/SiegeDismount/SiegeDismountService.cs) — pure state machine, fully unit-testable
- [`SiegeDismountMissionBehavior`](Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs) — thin engine bridge; reads `Mission.Current.IsSiegeBattle` + `SceneName` and delegates
- [`IPlayerMountAdapter`](Main/Adapters/IPlayerMountAdapter.cs) + [`IPartyMountInventoryAdapter`](Main/Adapters/IPartyMountInventoryAdapter.cs) — ADR-007 wrappers over `Hero.MainHero.BattleEquipment` and `MobileParty.MainParty.ItemRoster`. Service never sees `EquipmentElement` or `ItemObject`
- [`IMountSnapshot`](Main/Features/SiegeDismount/Models/IMountSnapshot.cs) — opaque token between adapter and service
- MCM settings folded into [`TaomSettings.cs`](Main/Features/TaomSettings.cs) under group `Battle Tactics / Siege Dismount` (3 settings: Enable, Behavior dropdown 0-3, Debug)
- No Harmony patches — pure `MissionBehavior` integration

**Logging:** every lifecycle event hits `IModLogger` per the mandatory cross-cutting logging contract from the integration plan. `LogInfo` on enable/disable + siege detection + restore. `LogDebug` (gated by `SiegeDismountDebug` MCM toggle) for per-mode decisions. `LogError` for all caught exceptions on adapter calls — never silent.

**Tests:** 24 unit tests in [`SiegeDismountServiceTests`](TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs) covering disable paths, all four behavior modes, scene-name siege detection (5 keyword variants), idempotent end, and four logging contracts. Build green, 1340/1340 tests pass.

Source material: [`Downloads/Features_fixed/_decompiled/SiegeDismount/SiegeDismount.decompiled.cs`](Downloads/Features_fixed/_decompiled/SiegeDismount/SiegeDismount.decompiled.cs). Original developer's behavior preserved verbatim — same modes, same defaults, same scene-name keywords.

Not-tested: `PlayerMountAdapter` and `PartyMountInventoryAdapter` (require live `Hero.MainHero` and `MobileParty.MainParty`); covered by in-game golden-path verification per [docs/features/siege-dismount.md](docs/features/siege-dismount.md#verification).

Constraint: mount/harness `ItemModifier` (durability/quality bonus) is dropped on auto-remount because Phase 1 stores only `StringId`. Documented as known limitation — upgrade to a modifier-preserving snapshot is a follow-up if any player reports it.



### Docs: CCBodyProperties — feature doc rewrite + seed config + memory entry (in-game verified)

User confirmed the OnCultureSelection postfix made the configured culture body visible in-game (issue #108 closed). Documentation updated to reflect the final 3-patch architecture and the call-chain lessons learned.

- Rewrote [docs/features/character-creation-body-properties.md](docs/features/character-creation-body-properties.md) — Architecture / Solution Approach now describes all three Patch29 hooks (`SetSelectedCulture` postfix, `OnCultureSelection` postfix, `RefreshAgentVisuals` BodySync prefix), the engine-side `if (IsPlayerCharacter && IsHero)` guard on `UpdatePlayerCharacterBodyProperties` that drove the two-step write pattern in the adapter, and the LOTRAOM-1.2 → TAOM-1.3 hook-evolution context. Added "Lessons Learned" section so future modders touching CC body state can skip the same iterations. Component diagram redrawn.

- Populated [Main/_Module/ModuleData/charactercreation/cc_body_properties.xml](Main/_Module/ModuleData/charactercreation/cc_body_properties.xml) with 17 cultures (6 vanilla XSLT + 11 TAOM custom) using the bodies the user reused from LOTRAOM 1.2.12. All elf cultures (`mirkwood`, `lothlorien`, `rivendell`) share the same `ElfBodyProp` per LOTRAOM convention. `erebor` uses the dwarf body. Generic-human cultures (`battania`/Khand, `sturgia`/Barding, `dale`, `umbar`, `mordor`, `isengard`) share the human silhouette.

- Updated memory [feedback_taleworlds_vm_setter_decompile.md](https://github.com/haterade22/TAOM) with a "Call-chain analogue" section. The lesson: decompile-the-body is insufficient when vanilla has multiple coordinated writers on the same state — the original `SetSelectedCulture`-only patch was clobbered by `CharacterCreationCultureStageVM.OnCultureSelection`'s `InitializePlayersFaceKeyAccordingToCultureSelection`, four reflective hops away from the entry point. Decompile every vanilla writer on the same code path; patch the LAST writer (or downstream of it). Added a 1.2 → 1.3 hook-migration note: TaleWorlds moved several CC virtuals (`OnCultureSelected`, equipment hooks) from `SandboxCharacterCreationContent` overrides (1.2) to `ICharacterCreationContentHandler` interface methods (1.3) plus stage-VM template methods. A 1.2 mod ported to 1.3 must re-find each hook's new location, not just port the signatures of the entry point you happened to know about.

Constraint: CLAUDE.md `Patch29_CCBodyProperties` row update (third target `CharacterCreationCultureStageVM.OnCultureSelection`) deferred — auto-mode classifier blocked the documented session-override mechanism for this turn (it was allowed earlier when the user explicitly said "Update Claude.md", but not this turn's general "Please update your documentation"). The row currently lists 2 targets; should read 3. User can re-authorize CLAUDE.md edits if the row is needed.

Build green, 1294/1294 tests pass.

### Fix: CharacterCreation — race dropdown defaults to Races[0] on first FaceGen open per culture

In-game verification of the race-filter feature surfaced two follow-up bugs that escaped both the deep-review and the Codex adversarial pass.

**Bug 1: dropdown order followed engine order, not config order.** [`FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap`](Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs) iterated `allRaces` (the engine's `FaceGen.GetRaceNames()` array) and added entries when present in the allow-list. Engine ordering puts `human` at index 0, so for cultures whose allow-list also contains `human` (Mordor, Isengard, Gundabad, Dol Guldur, the elven cultures), the resulting `globalIndices` map started with the engine's first match — `human` — even though `cultures.json` listed the lore-canonical race first. The dropdown surfaced human in position 1 of the visible list.

**Bug 2: dropdown defaulted to human even after the order was fixed.** Vanilla `FaceGenVM.Refresh(bool)` line 1779 sets `_selectedRace = _faceGenerationParams.CurrentRace`, which the engine initializes to `0` (human) regardless of culture. For Isengard's allow-list `[uruk_hai, berserker, human]`, `MapGlobalIndexToFiltered(0, [...])` correctly resolved to filtered position 2 (human). The original force-switch logic only fired when the current race was *not* in the allow-list — but human IS in Isengard's allow-list, so no switch happened, and the dropdown header showed human even though the user expected uruk_hai (Races[0]) as the default.

**Fix 1 (commit `2ccbdfc`):** `BuildGlobalIndexMap` now iterates the **allow-list** (config order) and resolves each name to its engine index via a name → index dictionary. Result preserves cultures.json order. Two existing rebuilder tests had their expectations flipped from engine-order to allowed-order; two new regression tests pin Mordor and Isengard specifically.

**Fix 2 (commit `896ace5`):** Per-`FaceGenVM`-instance session tracking via `ConditionalWeakTable<FaceGenVM, RaceFilterSession>` records the last applied culture id. On the first Apply for a given culture, force-switch to filtered position 0 (Races[0]) when the current race isn't already there. Subsequent Apply calls (gender/age changes that trigger `Refresh(true)`) preserve the player's selection. Decision logic extracted into pure helper [`ShouldForceSwitchToDefault(currentFilteredIdx, firstApplyForThisCulture)`](Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs) for testability — four new tests cover not-allowed-always-switch, first-apply-non-default-switches, first-apply-already-default-no-op, subsequent-apply-preserves.

In-game verified: Isengard now defaults to `uruk_hai`, Mordor to `uruk`, Gundabad to `pale_uruk`, Dol Guldur to `dg_uruk`, the elven cultures to `elf`. Player race choice persists across mid-CC navigation; switching culture resets to the new culture's Races[0].

1294 / 1294 tests passing (was 1288 before these two fixes).

Why review missed it: data-flow agent traced `_selectedRace` through `Refresh → 1779 → MapGlobalIndexToFiltered` and saw the human value resolve cleanly to a valid filtered position — that's the success path. The agent did not enumerate "what does the player *expect* the default to be?" against "what does the engine initialize to?". Codex did decompile `FaceGenVM.Refresh` but flagged a different issue (the OnPropertyChangedWithValue reflection bug). Both reviewers verified mechanical correctness; neither traced default-state expectations to UX outcome. Memory entry [feedback_filter_order_and_default.md](../../.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_filter_order_and_default.md) codifies the lesson for future sessions.

### Fix: CCBodyProperties — vanilla overwrites our body AFTER our SetSelectedCulture postfix

In-game testing after the previous fix still showed vanilla silhouette. Tracing in `taom_debug_*.log` confirmed our service applied `vlandia` body successfully, but the visible character was still vanilla. Decompile of `CharacterCreationCultureStageVM.OnCultureSelection(CharacterCreationCultureVM)` in installed v1.3.15 reveals:

```csharp
public void OnCultureSelection(CharacterCreationCultureVM selectedCulture)
{
    InitializePlayersFaceKeyAccordingToCultureSelection(selectedCulture);   // ← writes culture default body
    ...
}

private void InitializePlayersFaceKeyAccordingToCultureSelection(CharacterCreationCultureVM selectedCulture)
{
    if (selectedCulture.Culture.DefaultCharacterCreationBodyProperty != null)
    {
        CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties(
            selectedCulture.Culture.DefaultCharacterCreationBodyProperty.BodyPropertyMax,
            CharacterObject.PlayerCharacter.Race,
            CharacterObject.PlayerCharacter.IsFemale);
        Hero.MainHero.Culture = selectedCulture.Culture;
    }
}
```

TAOM's `FactionMap.CultureSettingService.SetCultureOnCharacterCreation` invokes:
1. `content.SetSelectedCulture(culture, charCreation)` reflectively → our SetSelectedCulture postfix applies our body
2. `cultureVM.ExecuteSelectCulture()` reflectively → routes through `OnCultureSelection` → vanilla `InitializePlayersFaceKeyAccordingToCultureSelection` writes the culture XML default body OVER ours

The body we just wrote is clobbered moments later, before any visual refresh. This is invisible at the API surface — it only emerges by tracing the call chain from `ExecuteSelectCulture` through the per-culture-VM's `_onSelection` delegate back into the stage VM's `OnCultureSelection` template method.

Fix: added sibling Patch29 hook [CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs](Main/Features/CharacterCreation/Hooks/CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs) — Harmony postfix on `CharacterCreationCultureStageVM.OnCultureSelection(CharacterCreationCultureVM)`. Runs AFTER vanilla overwrites the body with the culture XML default, re-applies our configured body via the same `ICCBodyPropertiesService.ApplyForCulture(stringId)`. The original SetSelectedCulture postfix stays in place as a safety net for any code path that bypasses `OnCultureSelection`.

Reference: this is the same approach LOTRAOM (Bannerlord 1.2.12) used by overriding `SandboxCharacterCreationContent.OnCultureSelected` — that virtual hook was refactored out of `CharacterCreationContent` (which is now sealed) and replaced by `ICharacterCreationContentHandler.OnStageCompleted` plus the stage-VM-side `OnCultureSelection` template method. Patching the new location is the v1.3 equivalent of LOTRAOM's 1.2 override. Pointer was provided by user — `C:\Users\mikew\Source\Repos\LOTRAOM\Main\Features\CampaignStart\CampaignStartGlobals.cs` and surrounding files.

Build green, 1294/1294 tests pass. Adapter intentionally untested (engine-boundary code); verification is in-game only.

### Fix: CCBodyProperties — body never visible in-game (regression from review fix #2)

In-game testing showed the configured culture body never reached the FaceGen preview — the player saw the vanilla starting silhouette regardless of which culture they selected. Logs confirmed the patch fired correctly (`Faction confirmed: Kingdom of Rohan -> Rohirrim` followed immediately by `CCBodyPropertiesProvider: Loaded 1 culture body-property entries` and `CCBodyPropertiesService: applied culture body for 'vlandia'`), so the chain Provider → Service → Adapter was intact. The break was at the engine boundary: `CharacterObject.UpdatePlayerCharacterBodyProperties` is fully no-op'd when its internal guard (`if (IsPlayerCharacter && IsHero)`) does not pass.

Per `ilspycmd` against installed v1.3.15 `TaleWorlds.CampaignSystem.dll`, the `CharacterObject` override is:

```csharp
public override void UpdatePlayerCharacterBodyProperties(BodyProperties properties, int race, bool isFemale)
{
    if (IsPlayerCharacter && IsHero)   // ← entire body wrapped
    {
        HeroObject.StaticBodyProperties = properties.StaticProperties;
        HeroObject.Weight = properties.Weight;
        HeroObject.Build = properties.Build;
        base.Race = race;
        HeroObject.IsFemale = isFemale;
        CampaignEventDispatcher.Instance.OnPlayerBodyPropertiesChanged();
    }
}
```

Note the override does NOT call base, so when the guard fails, `BodyPropertyRange.Init(properties, properties)` from `BasicCharacterObject` also does not run. Result: nothing changes anywhere.

The original adapter wrote `Hero.MainHero.StaticBodyProperties / Weight / Build` directly AS WELL as calling `UpdatePlayerCharacterBodyProperties` — those direct writes were the safety net that made the feature work in scenarios where the guard fails. Review fix #2 removed them as "redundant" based on a deep-review Agent 2 finding that quoted the override's body without the wrapping guard. The 3 lines were not redundant — they were the actual mechanism.

Restored the 3 direct Hero scalar writes in [PlayerBodyPropertiesAdapter.cs](Main/Adapters/PlayerBodyPropertiesAdapter.cs), with a comment explaining why: "CharacterObject.UpdatePlayerCharacterBodyProperties is gated by `if (IsPlayerCharacter && IsHero)` … the override no-ops silently. Always write Hero.MainHero scalars directly so Hero.BodyProperties returns the configured key regardless of guard state. Calling the override second gives us OnPlayerBodyPropertiesChanged when the guard does pass." Two-step pattern: direct writes first (always work), then `UpdatePlayerCharacterBodyProperties` (fires event when guard passes).

`Hero.BodyProperties` is computed: `new BodyProperties(new DynamicBodyProperties(Age, Weight, Build), StaticBodyProperties)`. `CharacterObject` (when `IsHero == true`) overrides `GetBodyPropertiesMin / Max` to return `HeroObject.BodyProperties`, so FaceGen reads through to our written scalars. No reliance on `BodyPropertyRange.Init` having fired.

This is the **same systemic pattern** as `feedback_taleworlds_vm_setter_decompile.md` (decompile the SETTER BODY, not just signature; vanilla guards mask call-site assumptions). The memory file has been updated with this case as a method-level analogue of the property-setter case it already documents. The deep-review skill quoted only the body content, not the wrapping guard — Agent 2's verification was incomplete in a way that survived the human-readable review.

Build green, 1294/1294 tests pass. The adapter is intentionally untested (thin engine wrapper); verification for this fix is in-game only — start a new CC, pick the culture configured in `cc_body_properties.xml`, advance to FaceGen, confirm the silhouette matches the body key.

Constraint: TaleWorlds engine guards are invisible at the API surface. Decompile-body discipline is the only defense.

### Feat: CharacterCreation — per-culture default BodyProperties on the CC screen (XML-driven)

When the player picks a culture during Character Creation, the player-character preview now adopts a TAOM-defined `BodyProperties` key string for that culture instead of the vanilla random-within-min/max default. The body re-applies on every culture change, mirroring vanilla's "switch culture resets body" mental model. Cultures not configured fall back to vanilla behavior with no errors.

Configuration lives in a single XML file under `Main/_Module/ModuleData/charactercreation/cc_body_properties.xml` — paste the `<BodyProperties version="4" key="..."/>` element exactly as produced by the in-game `BodyProperties.ToString()` (or copied from a save/face-customizer export). The provider validates the key length (must be 128 hex chars), warns on duplicate culture ids (last-wins), and skips entries with missing/empty/malformed data while logging structured warnings to `rgl_log.txt`.

Architecture follows the SettlementGuards/RevoltTuning template — `IPathService` + `IModLogger` constructor injection, IoC singleton, null-safe lookup. The hook is a thin Harmony postfix on `TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationContent.SetSelectedCulture` (verified via `ilspycmd` against installed v1.3.15) that delegates to `ICCBodyPropertiesService`. The service orchestrates lookup → adapter; the adapter wraps `BodyProperties.FromString` parsing and applies via `CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties` — which (per v1.3.15 ilspycmd verification) internally writes `HeroObject.StaticBodyProperties / Weight / Build` AND fires `CampaignEventDispatcher.OnPlayerBodyPropertiesChanged`, so a single call covers all required state mutations.

A sibling Patch29 hook on `CharacterCreationNarrativeStageView.RefreshAgentVisuals` per-frame syncs the career-menu player `NarrativeMenuCharacter`'s body from `Hero.MainHero.BodyProperties` (because that menu's character is constructed with a captured body at CC initialization, before any culture selection fires Patch29).

21 new unit tests cover the provider (14 — file-missing, malformed XML, missing id, missing/empty/short key, duplicate id last-wins, case-insensitive lookup, age/weight/build attribute preservation, caching) and the service (7 — orchestration, no-op when not configured, parse-failure warning, null/empty cultureId guards, exception swallowing). All 1288 repo tests pass.

Seed config covers `vlandia` (which is Rohan in TAOM's XSLT mapping) with the user-provided body key. Adding new cultures is a pure-XML edit — no rebuild required, but a Bannerlord restart is needed because the provider is `Reuse.Singleton` (cached for the process lifetime, not per-save).

Research: `ilspycmd` on installed v1.3.15 `TaleWorlds.CampaignSystem.dll` — `CharacterCreationContent.SetSelectedCulture(CultureObject, CharacterCreationManager)` confirmed. `TaleWorlds.Core.dll` — `BodyProperties.FromString(string, out BodyProperties)` returns bool; accepts both `<BodyProperties .../>` and `<BodyPropertiesMax .../>` element forms. `BasicCharacterObject.UpdatePlayerCharacterBodyProperties(BodyProperties, int race, bool isFemale)` calls `BodyPropertyRange.Init(properties, properties)` (min == max) plus sets Race/IsFemale.

Save-compat: no persistent state. The override only affects the live CC preview; once CC finalizes, the body is persisted to the save normally. Existing saves are unaffected.

Not-tested: live in-game verification of the body silhouette per culture (next launch).

GitHub: #108. Deep-review verdict NEEDS-FIXES — all in-session findings (3 of 4) implemented before this entry was finalized; details in the "Fix: CCBodyProperties — review-driven hardening" entry below. The 4th finding (race-stomp during FaceGen) was dismissed-as-not-applicable because `SetPlayerRace` at CC finalize is authoritative.

Constraint: CLAUDE.md `Patch29_CCBodyProperties` row update deferred — `config-protection.sh` hook blocks CLAUDE.md edits without explicit user authorization at the hook layer.

### Fix: CCBodyProperties — review-driven hardening (issue #108)

`/deep-review` Agent 5 (Data Flow) flagged 4 findings on the body-properties feature; 3 fixed in same session, 1 dismissed-as-not-applicable.

1. **Doc/code mismatch on `age=` attribute (MEDIUM):** XML comment claimed age was "honoured if present" but `parsed.Age` was silently dropped — `Hero.Age` is computed from `BirthDay`, which we do not touch. Fix: removed the misleading claim from [cc_body_properties.xml](Main/_Module/ModuleData/charactercreation/cc_body_properties.xml) header; documented that `age=` is parsed by vanilla but not applied.

2. **Redundant `Hero.MainHero` writes (LOW):** Adapter wrote `StaticBodyProperties`, `Weight`, `Build` to `Hero.MainHero` after calling `playerChar.UpdatePlayerCharacterBodyProperties(...)`. Per ilspycmd verification of v1.3.15 `CharacterObject.UpdatePlayerCharacterBodyProperties`, that override already writes the same three properties to `HeroObject` AND fires `CampaignEventDispatcher.OnPlayerBodyPropertiesChanged` — which our duplicate writes silently bypassed. Fix: dropped the 3 redundant assignments from [PlayerBodyPropertiesAdapter.cs](Main/Adapters/PlayerBodyPropertiesAdapter.cs); the event now fires correctly.

3. **Career menu preview body stale after culture change (LOW-MEDIUM):** `CareerMenuService.RegisterCareerMenu` constructs the player `NarrativeMenuCharacter` once at CC initialization (before any culture is selected). Patch29 wrote the new body to `Hero.MainHero` and `CharacterObject.PlayerCharacter` but did not propagate to that captured snapshot — Patch20's existing `RefreshAgentVisuals_Patch` only syncs `Race`, not body. Fix: added sibling Patch29 hook [CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs](Main/Features/CharacterCreation/Hooks/CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs) — per-frame prefix that finds `NarrativeMenuCharacter.StringId == "player_career_character"` and syncs its body from `Hero.MainHero.BodyProperties` when it differs. Reflection lookup of `_characterCreationManager` cached in static field per `harmony-patches.md`.

4. **Race=0 stomp during FaceGen (LOW, dismissed):** Adapter passes `playerChar.Race` to `UpdatePlayerCharacterBodyProperties`, which writes it back into `playerChar.Race`. On first culture-pick this is read-then-write-same-value (no-op). On re-entry, it preserves whatever race was set before. `SetPlayerRace` at CC finalize is authoritative and runs last, so any transient stale value during FaceGen is overwritten. No change needed.

### Feat: CharacterCreation — culture-restricted race dropdown (re-implemented Patch9_RaceFilter)

The Character Customization screen now filters the **Race** dropdown to the races permitted by the selected culture. Erebor → `[dwarf]` only. Mordor → `[uruk, orc, human]`. Mirkwood / Lothlorien / Rivendell → `[elf, human]`. Isengard → `[uruk_hai, berserker, human]`. Gundabad → `[pale_uruk, goblin, orc, human]`. Dol Guldur → `[dg_uruk, goblin, orc, human]`. Vanilla, Umbar, Gondor, Shaghana, Abanissa → `[human]`.

The previous Patch9 attempt patched `FaceGen.GetRaceNames()` directly and broke `FaceGenVM` because the VM uses the array index of `GetRaceNames()` as the engine's global race ID — filtering shifted indices and decoupled the dropdown from the race table. That patch shipped as a no-op (file note at `FaceGen_GetRaceNames_Patch.cs:7-8`) and the dropdown stayed unfiltered.

The new patch ([FaceGenVM_Refresh_RaceFilter_Patch.cs](Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs)) postfixes `FaceGenVM.Refresh(bool clearProperties)`. After the vanilla code at line 1925 has built `RaceSelector = new SelectorVM(GetRaceNames(), _selectedRace, OnSelectRace)`, the postfix:

1. Reads the active `CharacterCreationManager.CharacterCreationContent.SelectedCulture.StringId` via `Game.Current.GameStateManager.ActiveState as CharacterCreationState`.
2. Resolves `ICultureRaceFilterService` from IoC and gets the allow-list for that culture.
3. Builds a parallel `globalIndices: List<int>` mapping filtered position → engine race index.
4. Constructs a fresh `SelectorVM<SelectorItemVM>` containing only allowed races, with its `_selectedIndex` set via reflection (bypassing the public setter to avoid firing `_onChange` during construction).
5. Wires a wrapped `_onChange` callback. When the user picks a filtered position, the wrapper looks up the global index, mutates `s._selectedIndex` to that global value via reflection (bypassing the public setter to avoid recursion), invokes vanilla `OnSelectRace`, then restores the field. Vanilla `OnSelectRace`'s body — `_selectedRace = s.SelectedIndex` — therefore reads the correct global index, and its downstream `UpdateRaceAndGenderBasedResources` → `UpdateFace(-20, _selectedRace)` chain updates `_faceGenerationParams.CurrentRace` correctly via `SetRaceGenderAndAdjustParams` (line 2130).
6. If the player's pre-existing `_selectedRace` isn't in the allowed set (e.g., culture changed mid-CC), forces a single switch to the first allowed race, guarded by a `[ThreadStatic]` flag so the recursive `Refresh(true)` triggered downstream cannot loop.

The race-filter mapping is **not** a separate config file — it reuses the existing `Main/_Module/ModuleData/charactercreation/cultures.json` `races` arrays (already loaded by `CultureCreationDataProvider`). To retune, edit `cultures.json` directly. To add a new race to a culture, add the race ID to that culture's `races` array. No code change required.

Two cultures had their `races` arrays trimmed to match the user's spec: Mordor lost `goblin`, Isengard lost `saruman`. Those races still exist in `monsters.xml` and remain available for NPCs and existing saves — only the player-facing CC dropdown is restricted.

Removed dead code from the prior failed attempt: `FaceGen_GetRaceNames_Patch.cs`, `IOnGetRaceNames` (empty marker interface), `GetRaceNamesHook` (empty class), `GetRaceNamesHookTests.cs` (asserted nothing useful), and the `IOnGetRaceNames → GetRaceNamesHook` IoC registration.

24 new tests cover the filter service: per-culture allow-lists, case-insensitive matching, fallback for unknown cultures, fallback for empty `Races` arrays, single-warning-per-culture deduplication. Service is fully unit-testable via `ICultureCreationDataProvider` substitution.

Research: ilspycmd on installed v1.3.15 `TaleWorlds.MountAndBlade.ViewModelCollection.dll` — `FaceGenVM.Refresh`, `OnSelectRace`, `_raceSelector`, `_selectedRace`; `TaleWorlds.Core.dll` SelectorVM/SelectorItemVM (verified against decompiled v1.4 since ilspycmd 9.1 cannot resolve generic type names against v1.3.15).

Constraint: `FaceGenVM` is sealed; the patch uses `AccessTools.Field` reflection to mutate private state, cached in static fields per `harmony-patches.md`.

Not-tested: live in-game verification of the dropdown contents per culture (next launch).

Save-compat: no persistent state. Pure UI filter applied during character creation only.

### Fix: CharacterCreation — `SetPlayerRace` honors player's FaceGen race choice (review-driven, same session)

`/deep-review` Agent 5 (Data Flow) caught a HIGH gap: [`CharacterCreationContentService.SetPlayerRace`](Main/Features/CharacterCreation/CharacterCreationContentService.cs) unconditionally assigned `cultureData.Races[0]` at finalization, ignoring the player's FaceGen race selection. Pre-existing bug (not introduced by the new filter) but elevated in user impact: now that the filter exposes meaningful choices like Mordor `[uruk, orc, human]`, a player who picks "human" would still get `uruk` applied at game start. Fix: `SetPlayerRace` now reads the hero's current race (Bannerlord assigns `Hero.CharacterObject.Race` from FaceGen output before finalize runs), accepts it if it's in the culture's allowed list, and only falls back to `Races[0]` otherwise. `IHeroRosterAdapter` gained a `GetHeroRace(string heroStringId)` method. Three new unit tests: preserves-allowed-choice, falls-back-when-disallowed, case-insensitive matching.

### Refactor: CharacterCreation — DI cleanup + extracted pure helpers (review-driven)

`/deep-review` Agent 1 (Standards) flagged `IoC.Resolve` inside `FaceGenRaceSelectorRebuilder`, one step removed from the patch boundary. Refactored: the patch ([FaceGenVM_Refresh_RaceFilter_Patch.cs](Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs)) now resolves `ICultureRaceFilterService` and `IModLogger` via lazy-cached statics at the boundary and passes the service to `FaceGenRaceSelectorRebuilder.Apply(faceGenVM, filterService)` as a parameter. The rebuilder no longer references `IoC` at all. This also addresses Agent 3's MEDIUM perf finding (one IoC.Resolve per session vs one per click).

Agent 5 LOW finding: extracted three pure static helpers from the rebuilder — `BuildGlobalIndexMap(string[], IReadOnlyList<string>)`, `MapFilteredIndexToGlobal(int, IReadOnlyList<int>)`, `MapGlobalIndexToFiltered(int, IReadOnlyList<int>)` — covering the index-translation logic that was previously trapped in a closure. Added [FaceGenRaceSelectorRebuilderTests.cs](TAOM.Tests/Features/CharacterCreation/FaceGenRaceSelectorRebuilderTests.cs) with 12 tests including a round-trip property test (filtered → global → filtered = identity) and case-insensitive intersection coverage.

Net test count for the feature: 52 (24 filter service + 12 rebuilder helpers + 16 SetPlayerRace + existing). All 1266 repo tests pass.

Constraint: cannot update [CLAUDE.md](CLAUDE.md) line 307 (Patch9_RaceFilter target should change "Various" → "FaceGenVM.Refresh") — `config-protection.sh` hook blocks edits without explicit user request. Deferred to user.

Deferred: GitHub issue creation deferred — user has standing "no git actions unless explicitly asked" instruction; CLAUDE.md mandates an issue per feature. Awaiting user authorization to run `gh issue create`.

### Fix: CharacterCreation — Codex Review 33 confirmed bugs (HIGH + MEDIUM)

Adversarial Codex review of the race-filter feature returned 2 confirmed findings; both fixed in this session.

**F1 (HIGH) — RaceSelector replacement did not notify Gauntlet.** [FaceGenRaceSelectorRebuilder.cs:71-72](Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs) (pre-fix) mutated the private `_raceSelector` field via reflection, then attempted to fire the property-change notification by reflectively invoking `OnPropertyChangedWithValue(object, string)` on `FaceGenVM`. The actual method on the `ViewModel` base is generic `OnPropertyChangedWithValue<T>(T, string) where T : class`. `AccessTools.Method` looking up by `(typeof(object), typeof(string))` returns `null` (Codex verified empirically against installed v1.3.15 + Harmony 2.4.2). The notification never fires; Gauntlet's `GauntletView.OnViewModelPropertyChangedWithValue` is never called; the dropdown UI stays bound to the prior unfiltered selector. Initial construction can mask this because `BodyGeneratorView.LoadMovie("FaceGen", DataSource)` reads the field directly after construction — but any subsequent `Refresh(true)` (every race change, every FaceGen reopen) silently rebinds the UI to vanilla's full selector. Fix: replaced the field-mutation + reflection-notify pair with `faceGenVM.RaceSelector = newSelector`. The vanilla setter (FaceGenVM.cs:986-990) handles both the field assignment AND the correctly-typed property-change notification. Removed `_raceSelectorField` and `_onPropertyChangedWithValueMethod` static caches and corresponding `EnsureFields` lookups.

**F2 (MEDIUM) — invalid race ID could be silently coerced to "human" and accepted.** [CharacterCreationContentService.cs:243](Main/Features/CharacterCreation/CharacterCreationContentService.cs) (pre-fix) called `_raceManager.GetRaceNameFromId(faceGenRaceId)` without validating the ID first. `RaceManager.GetRaceNameFromId` (RaceManager.cs:126-131) silently returns `"human"` as fallback for unknown IDs with only a warning log. `SetPlayerRace` accepted that fallback name, checked it against the culture's allow-list, and for cultures that allow `"human"` (Mordor, Gundabad, DolGuldur, Isengard, vanilla cultures, etc.) preserved the original invalid integer. `Hero.CharacterObject.Race` accepts arbitrary integers; downstream engine calls (`FaceGen.GetBaseMonsterFromRace`, body property generation) would receive a junk race ID. Fix: gate `faceGenChoiceAllowed` on `_raceManager.IsValidRaceId(faceGenRaceId)` BEFORE resolving the name. Three existing `SetPlayerRace` tests updated to stub `IsValidRaceId(...).Returns(true)`. New regression test `SetPlayerRace_InvalidFaceGenRaceId_DoesNotPreserve_FallsBackToCultureDefault` asserts an invalid ID falls back to the culture default even when the fallback name is allowed.

Build green. 1288/1288 tests passing.

Reviews captured: [docs/reviews/codex-adversarial-charactercreation-racefilter-2026-05-06.md](docs/reviews/codex-adversarial-charactercreation-racefilter-2026-05-06.md), [docs/reviews/REVIEW-LOG.md](docs/reviews/REVIEW-LOG.md) Review 33 (with full Phase 3e root-cause analysis), [AGENTS.md](AGENTS.md) (added 2 lessons + Codex run-mode caveat).

Process note: Codex went off-scope mid-review and started implementing a separate `Patch29_CCBodyProperties` feature unrelated to the race-filter scope. Those changes were preserved (functional and tested). One build error in Codex's new patch (`CultureObject` namespace missing) was fixed. The scope drift is documented in REVIEW-LOG and AGENTS.md to keep Codex's review focus from silently expanding in future runs.

## 2026-05-04

### Process: RCA + prevention for the shader-precompilation initial-zero latch miss

The visible-progress fix shipped one commit ago corrected a bug that should have been caught by `/deep-review` Agent 5 (Data Flow Tracing) and the prior Codex 2026-04-14 review — both walked happy-path examples starting from `count=100` and never enumerated the `count=0` first-frame state where the bug fires. The pattern is a **state-machine sentinel collision** — the "uninitialized" sentinel value (`_lastShaderCount = -1`) was indistinguishable from the real terminal value (`0`) when compared against the first poll observation.

Three artifacts so the next observation-driven static-state machine doesn't ship the same class of bug:

1. **RCA document** — [docs/reviews/rca-shader-precompilation-initial-zero-latch-2026-05-04.md](docs/reviews/rca-shader-precompilation-initial-zero-latch-2026-05-04.md). Full timeline, why each layer of review missed it, the fix, lessons captured, and prevention items (taken vs deferred).

2. **Mandatory rule** in [.claude/rules/harmony-patches.md](.claude/rules/harmony-patches.md) — new "Static State Machines: Sentinel-Collision Check" section. When a patch holds static state across frames AND drives that state from polling external values (engine counts, file sizes, MBObjectManager queries, vanilla VM properties), the four boundary states must be enumerated before writing change-detection logic: sentinel (state 1) / first observation (state 2) / in-progress (state 3) / terminal (state 4). When state 2 and state 4 share an encoding (the typical case), require an additional `_hasObservedWork`-style flag set the first time state-3 is observed, and only fire terminal-state actions when `current == terminal && _hasObservedWork`.

3. **Deep-review Agent 5 prompt** in [.claude/skills/deep-review/SKILL.md](.claude/skills/deep-review/SKILL.md) — new "5b. Observation State Machines (BOUNDARY ENUMERATION)" trace category. Sibling to the existing rule 5 (Lifecycle Completeness), explicitly distinct: lifecycle asks *when does this entity die?*, observation asks *what values can the poll return, in what order, and which transitions mean what?*. Both are needed; one is not a substitute for the other. Includes the shader-precompilation case as the worked example.

Memory file `feedback_observation_state_matrix.md` (in user-scoped memory, not in this repo) captures the lesson for future sessions.

This entry intentionally precedes the visibility-fix entry below — the prevention work belongs at the head of the day in case anyone walks the log forward in time.

### Fix: ShaderPrecompilation — visible per-second progress UI + initial-zero latch race (#106 follow-up)

In-game test of the prior tuning fix surfaced a separate, pre-existing bug: the loading screen showed no shader-progress text at all on warm-cache machines. Tracing the patch logic against the user's `taom_debug_*.log` showed why:

`LoadingScreen_ShaderProgress_Patch._lastShaderCount` is initialised to `-1` by `ResetForNewBattle()`. On the first frame the postfix runs after `IsShaderBattleActive` flips on, the engine has often not started queuing shaders yet (warm cache, fast load) — `Utilities.GetNumberOfShaderCompilationsInProgress()` returns 0. The patch then took `0 != -1` as a "change" and entered the count-zero branch, which calls `TaomShaderGameManager.ResetShaderBattleActive()` — disabling the patch before any real work arrived. Subsequent frames where the count actually rose hit the `!IsShaderBattleActive` early-return and never wrote anything to the loading screen. Net result: blank loading text for the entire compile, then the deployment phase opened. From the user's view, "all I see is a loading screen and that is it."

Added a `_hasObservedWork` flag set the first time `remaining > 0`. `ResetShaderBattleActive` is now only called when transitioning from positive to zero (true completion), not when zero is observed before any work has queued. Deep-review's data-flow agent traced the "dropped to zero after positive" path but didn't trace "starts at zero, goes positive" — same class of off-by-one as the abort-latch leak fixed earlier in this session.

Also reworked the progress display so users can actually see the work happening:

- Loading screen text now reads `Compiling shaders... 1234 remaining (elapsed: 2m 15s) ...` and re-writes once per second whether the count moved or not. The trailing dots cycle 1–4 each second so liveness is visible even when the compiler holds steady on a heavy material. Vanilla loading text is left intact during the pre-queue window; we only stamp ours once shaders are actually queued.
- New `taom_debug_*.log` markers: `First shaders queued: N remaining` (when the queue first goes positive), `Progress: N remaining (elapsed: ...)` every 30 s during the run, `Compilation complete after Xm Ys` when the count returns to zero. Post-mortem grep for these confirms the precompile actually finished without needing to watch the loading screen live.
- Throttling: text update gated to 1 Hz, file log gated to 30 s. No per-frame string allocation; constant-bounded GC pressure.

Stuck detection unchanged — still fires only when `remaining <= StuckTailRemainingMax` and the count has held steady past `StuckAbortSeconds` (600 s). The 1 Hz update means the "stuck Ns, aborting in Ms" warning text stays current to within one second.

Single-file change, [LoadingScreen_ShaderProgress_Patch.cs](Main/Features/ShaderPrecompilation/Hooks/LoadingScreen_ShaderProgress_Patch.cs); no service-layer impact, no new tests required (entry-point per ADR-008).

Not-tested: live in-game verification of the new text appearing during a precompile run (next launch).

Save-compat: no persistent state. Safe on any save.

### Fix: ShaderPrecompilation — eliminate silent character drop + relax premature stuck-abort (#106, follow-up to #57)

Multiple users reported the main-menu "Pre-compile Shaders" button "doesn't work" — they ran the 20–70 minute process, saw it complete, then still hit mid-game stutter on the same character types it was supposed to cover. Root cause was three tuning bugs flagged but under-rated by Codex Review 2026-04-14:

1. **Silent character drop (primary cause).** `MaxTroopsPerSide=2000` × 2 sides = 4000 slots, with `SoldierCopies=4` capping at ~1000 unique soldiers. The service feeds in ~1600 TAOM characters + vanilla characters across all loaded cultures (the cultureId filter accepts every culture), so the tail of the character list was silently skipped. The skip count was logged at `LogWarning` to `rgl_log` only — invisible to users. Raised `MaxTroopsPerSide` 2000 → 3000 (6000 total slots) and `SoldierCopies` 4 → 2; fits the full TAOM + vanilla character set. The `2` keeps statistical equipment-variant coverage (each `AddCharacter(troop, count)` randomises across the troop's `BattleEquipments` list).
2. **Premature 120 s auto-abort.** `LoadingScreen_ShaderProgress_Patch` called `MBGameManager.EndGame()` whenever the count held steady for 120 s. Bannerlord's shader compiler is single-threaded native code; one heavy material can legitimately hold for several minutes on slower CPUs, so the abort fired moments before completion on a meaningful slice of the user base. Raised `StuckAbortSeconds` 120 → 600 and `StuckWarnSeconds` 30 → 300, and added a new `StuckTailRemainingMax = 5` guard so stuck-detection only fires when the engine is genuinely stalled on the last few shaders — large-count pauses no longer auto-abort.
3. **Static state not reset between runs.** `SubModule._shaderTickAccumulator` and `_lastShaderCount` were never reset; clicking "Pre-compile Shaders" a second time in the same Bannerlord process could suppress the first toast. Added explicit reset in the `InitialStateOption` Start callback before `MBGameManager.StartNewGame`.

**Deep-review follow-up.** Cross-system data-flow agent caught a fourth gap missed by the initial pass: when the auto-abort branch fires `MBGameManager.EndGame()`, `TaomShaderGameManager.IsShaderBattleActive` was never cleared. Any shaders still in flight when the user next opened a loading screen (new campaign, custom battle) would have inherited TAOM's "Compiling shaders... N remaining" text override on that unrelated screen. Fixed in the same change by calling `ResetShaderBattleActive()` immediately before `EndGame()`.

Doc consolidation: `docs/features/shader-precompilation.md` Configuration table updated with all six tunable constants and a "Why the constants were tuned" subsection. The component diagram, key-files table, tests list, and "How to Add Coverage" section were also de-staled (the doc was carrying a `MaxTroopsPerSide=500` figure from before the 2026-04-14 TOR-inspired rework, and a "filters non-bandit cultures" claim that contradicted the actual code, which intentionally includes bandits for full mesh coverage).

ShaderPrecompilation tests: 7/7 green. No new tests required — the changed code is in entry-point classes (`TaomShaderGameManager`, the Harmony patch) which are not unit-testable per ADR-008. The service-layer tests already cover the data path that feeds them.

Not-tested: live in-game verification that the new constants compile all characters within the slot budget on a real install (requires running the full 20–70 min process and inspecting `rgl_log` for `[ShaderPrecompilation] Loaded N characters` with zero `M characters skipped`).

Save-compat: no persistent state involved. Safe on any save. Users who previously ran the old precompilation should re-run it once after this update to pick up the previously-dropped characters.

### Fix: CareerSystem — wall-clock-precise cooldown tick + reject NaN/Infinity tuning (Codex Review 31)

Two MEDIUM findings from the Codex adversarial pass on the cooldown rework:

1. **Cooldown drained slower than wall clock on long frames.** `OnMissionTick` used a single-bucket accumulator (`if (_acc >= 1f) Tick(1f)`) carried over from the prior charge-based 1Hz scheduler. A 2.5-second frame (alt-tab return, GC pause) drained only 1 second of cooldown, queuing the remaining 1.5 seconds for the next bucket — so a configured 30-second cooldown could take 35-40 seconds to release under load. Even on smooth play, up to ~1 second of quantization delay was possible depending on activation timing relative to the bucket. Replaced the accumulator with per-frame `_abilityService.Tick(heroId, dt)`. `CareerAbility.Tick` already clamps via `Math.Max(0f, CooldownRemaining - dt)` so fractional `dt` is correct. Two regression tests added: `Tick_LargeDt_DrainsFullElapsedTime` (single 2.5s frame) and `Tick_FractionalDt_AccumulatesAcrossFrames` (60×16ms).

2. **`ParseGlobalTuning` admitted `NaN` / `±Infinity`.** `float.TryParse` accepts these IEEE-754 specials. The downstream `<= 0` and `> 3600` range gates BOTH evaluate false for `NaN`, so a NaN cooldown reached `CareerAbility.Activate`, set `CooldownRemaining = NaN`, and made `IsOnCooldown => CooldownRemaining > 0f` permanently false (NaN comparisons always return false) — every V keypress then activated the ability. Added explicit `float.IsNaN(seconds) || float.IsInfinity(seconds)` check ahead of the range gates with warning + default fallback. Three regression tests cover `NaN`, `+Infinity`, `-Infinity`.

Both findings folded into AGENTS.md so future Codex passes target the same blind spots: tick-rate vs wall-clock semantics on user-visible timers, and IEEE-754 special-value enumeration for user-facing float validation.

### Feat: CareerSystem — uniform 30s cooldown timer + "still charging" feedback (#103)

The career ability system shifted from charge-based (`DamageDone` / `Kills` / `DamageTaken` accumulators) to a uniform 30-second cooldown timer. All 50 careers now start ready at battle open, fire on `V`, then lock for 30 seconds. Cooldown duration is configurable via a new `<Global cooldown_seconds="30" />` element in `taom_ability_tuning.xml`, validated `(0, 3600]` with warning + default fallback.

- `CareerAbilityService` injects `ICareerConfigProvider` and forces `ChargeType.CooldownOnly` for every career; reads cooldown duration from tuning XML.
- New `ICareerAbilityService.GetCooldownRemaining(heroId)`. New `CareerAbility.ReadyProgress01` (0→1 progress for HUD bar).
- Pressing `V` while still on cooldown emits a throttled gray *"Career ability still charging — Ns remaining"* message instead of a silent no-op (was: silent failure, hard to diagnose).
- HUD widget refresh: per-mission cache for ability name + sprite path eliminates per-frame `TextObject` construction and string interpolation in `OnMissionTick` (caught by `/deep-review` Agent 3).

**Cleanup pass alongside the rework.** Removed `ChargeType` and `MaxCharge` from `CareerDefinition`, the `charge_type` and `max_charge` attributes from all 50 entries in `taom_careers.xml`, dead `Cooldown` and `SpriteName` fields from `AbilityTemplateData`, the dead `SetMaxCharge` mutation block, and the no-op `AddCharge` calls in `OnScoreHit` / `OnAgentRemoved`. Service-layer `AddCharge` removed from `ICareerAbilityService` (the model-level `CareerAbility.AddCharge` stays — preserved as regression-guard for any future re-introduction).

**Architecture pass.** Three CareerSystem `GameModel` overrides (`TaomClanTierModel`, `TaomAgentStatCalculateModel`, `TaomAgentApplyDamageModel`) converted from lazy-cached `IoC.Resolve` to constructor injection of `ICareerPassiveService`, registered from `SubModule.cs`. `CharacterDeveloperCareerMixin` resolves services once in the constructor (boundary pattern) instead of per-call.

26 new tests across `CareerAbilityTests`, `CareerAbilityServiceTests`, and `CareerConfigProviderTests`. 176 / 176 CareerSystem tests green.

Follow-ups filed: #101 (41 ability-icon PNGs still missing — only 9 of 50 sprites render), #102 (`CareerPerkMissionBehavior.cs` 302 LOC ADR-002 refactor).

Save-compat: no persistent state changed (cooldown state is mission-scoped). Safe on any save.

### Fix: Custom Battle commander dropdown ignored faction selection — now filters per-culture, capped at 3

The Custom Battle commander dropdown listed every TAOM lord across every culture regardless of which faction was picked, making selection impractical and disconnecting the visual faction choice from the available leader pool.

**Root cause.** Vanilla `CustomBattleSideVM.RefreshValues()` iterates `CustomBattleData.Characters` and adds every entry to `CharacterSelectionGroup.ItemList`. TAOM's `CustomBattleData_Characters_Patch` returned the full TAOM lord pool (matched by `^lord_[A-Za-z0-9]+_[A-Za-z0-9]+$` regex) without per-faction filtering, and vanilla's `OnCultureSelection` callback only updates banner colors — it never re-filters the dropdown. Net effect: full unfiltered list at all times.

**Fix.** New singleton `ISideCommanderFilter` resolves a culture's commanders via the existing `CustomBattleService.GetCommanderIdsForFaction(factionId, takeMax)` (extended with `OrderBy(Id)` for deterministic ordering and a `takeMax` cap). Two new Harmony postfixes on `CustomBattleSideVM` rebuild `CharacterSelectionGroup.ItemList` from the filter:

- `Patch19_CustomBattles / CustomBattleSideVM_OnCultureSelection_Patch` — postfix on the private `OnCultureSelection(BasicCultureObject)`; rebuilds the dropdown when the user clicks a faction.
- `Patch19_CustomBattles / CustomBattleSideVM_RefreshValues_Patch` — postfix on `RefreshValues()`; defensive layer for refresh events triggered by language/resolution changes.

`CustomBattleSideVM_Constructor_Patch` was extended to invoke the `OnCultureSelection` callback explicitly with `TaomFactionSelectionVM.SelectedItem.Faction` after the FactionSelectionGroup swap, so the initial-paint dropdown aligns with the actually-visible faction (vanilla `SelectFaction(0)` doesn't fire the callback).

Cap is `SideCommanderFilter.MaxCommandersPerCulture = 3`. Both patches log a `LogWarning` if a culture has zero matching commanders so future lords.xml culture-tag mismatches surface in `rgl_log.txt` instead of silently regressing to the unfiltered list.

11 new unit tests across `CustomBattleServiceTests` (cap, deterministic order, fewer-than-cap, zero-cap) and `SideCommanderFilterTests` (null/empty culture, cap propagation, null-resolution filtering, empty result).

**Codex Review 30 fix (P1).** The first version of the rebuild did `ItemList.Clear() + AddItem(*N) + SelectedIndex = 0`, but `SelectorVM<T>.SelectedIndex` setter early-returns when `value == _selectedIndex`. Vanilla initializes `_selectedIndex = 0` and most users click another faction without first deselecting, so the post-rebuild assignment was a no-op — `SelectedItem` (and downstream `CustomBattleSideVM.SelectedCharacter`) kept pointing at a `CharacterItemVM` that had just been removed from `ItemList`, and the battle would launch with the wrong commander. Fixed by extracting the rebuild into `Hooks/CommanderSelectorRebuilder.Apply`, which mirrors vanilla `SelectorVM.Refresh()`'s pattern: reset `_selectedIndex = -1` (cached `FieldInfo` via `AccessTools.Field`) before assigning the real index. Both filter postfixes now go through this helper. New rule under `.claude/rules/gui-ui.md` codifies the pattern for any future TaleWorlds VM mutation.

Save-compat: no campaign state involved; UI-only behavior. Safe on any save.

### Fix: CC parent agents not rendering for custom-race cultures (Erebor, Mordor, Mirkwood, etc.)

When playing as a custom-race culture, the "You were born into a family of..." parents stage rendered a broken visual — single sideways/T-pose figure with bare feet — instead of the two upright parents. Bug surfaced across every dwarf/uruk/orc/elf-race culture.

**Root cause (two layered):**

1. **Race mismatch at action-set lookup.** Vanilla `AddParentsMenu` captures `CharacterObject.PlayerCharacter.Race` at menu-construction time (when it's still 0=human). `CharacterCreationNarrativeStageView.CreateAgentVisual` then uses that captured `character.Race` to compute the action-set name (`as_<race>_facegen`), but separately uses the *current* `PlayerCharacter.Race` to set the agent's body skeleton. After the player picks dwarf at FaceGen, the agent renders with a dwarf skeleton trying to play animations from `as_human_facegen` → broken pose.
2. **Stale 1.2 action-type names in LOTRLOME_Armory.** `LOTRLOME_Armory/ModuleData/action_sets.xml` was authored against Bannerlord 1.2 which used `act_character_creation_male_default_0..6` and `_female_default_0..6`. Bannerlord 1.3 renamed those to `_default_standing`, `_side_to_side_1`, `_mother_front`, `_father_sitting`, `_side_to_side_2`, `_side_to_side_3`, `_hugging`. Even with the race lookup fixed to `as_dwarf_facegen`, none of the new 1.3 action types exist in that action_set → animation lookup fails.

**Change A — race-sync prefix.** Harmony `[HarmonyPrefix]` on `CharacterCreationNarrativeStageView.RefreshAgentVisuals` (added under `Patch20_NarrativeHorseGuard` category in `Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`) iterates the current menu's `NarrativeMenuCharacter` list and calls `UpdateBodyProperties(bodyProperties, currentPlayerRace, isFemale)` on each before vanilla spawns the agent visuals. Now the action-set lookup resolves to `as_<race>_facegen` matching the agent's body skeleton.

**Change B — 1.3 action-type aliases in LOTRLOME_Armory.** Added 7 male + 7 female alias actions to every facegen action_set in `LOTRLOME_Armory/ModuleData/action_sets.xml` (dwarf, dwarf_female, orc, orc_female, uruk, uruk_female, uruk_hai, uruk_hai_female, berserker, nazghul, dg_uruk, etc. — 12 sets total). New names map to the same `anim_father_0..6` / `anim_mother_0..6` files the existing `_default_0..6` actions already use. NOTE: this lives outside the TAOM repo; future LOTRLOME_Armory updates will overwrite it.

**Change C — Erebor parent equipment.** Updated all 14 Erebor parent rosters (`mother/father_char_creation_<occupation>_erebor` × 7 occupations) in `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml` so mothers wear `sk_dwarf_dress_normal_a` and fathers wear `sk_dwarf_tunic_noble_a` instead of identical leather chest pieces.

**Cleanup — removed 5 dead duplicate XMLs from TAOM repo:**
- `Main/_Module/ModuleData/action_sets.xml` (~105K lines)
- `Main/_Module/ModuleData/monsters.xml` (~1.7K lines)
- `Main/_Module/ModuleData/Races/action_sets.xml` (~353K lines)
- `Main/_Module/ModuleData/Races/monsters.xml` (~1.8K lines)
- `Main/_Module/ModuleData/Races/skins.xml` (~200K lines)

Bannerlord auto-loads root-level `action_sets.xml` / `monsters.xml` / `skins.xml` from each module, but the `Races/` subdirectory copies were never registered and never loaded. The root-level copies were stale duplicates of the LOTRLOME_Armory versions (no TAOM-unique monster IDs; `comm -23` set diff was empty). Cleaning removes ~660K lines of unused XML.

Save-compat: no field changes; pure rendering + animation lookup + asset cleanup. Safe on any save.

Not-tested: visual rendering of CC parents — verified live by player testing.

Research: `E:/Decompiled_Bannerlord/Modules/SandBox.GauntletUI/.../CharacterCreationNarrativeStageView.cs` (`CreateAgentVisual` line 290–293), `Core/TaleWorlds.Core/.../ActionSetCode.cs` (`GenerateActionSetNameWithSuffix`), `Core/TaleWorlds.Core/.../NarrativeMenuCharacter.cs` (`UpdateBodyProperties` API).

### Fix: SpecialResources hot-path log spam — dedupe ResolveResource DEBUG by (kingdom, culture)

A 2026-05-04 debug log review found 1,751 of 2,531 lines (69%) were the same `[SpecRes] Resolved resource 'caster' via culture 'gondor' (kingdom '' had no match)` line, firing several times per map-tick from `MapInfoVM.OnRefresh` tooltip rebuilds. The DEBUG line was useful during kingdom-vs-culture resolution development but adds zero diagnostic value once resolution is steady-state.

**Change:** `SpecialResourceService.ResolveResource` now tracks logged `(kingdomId, cultureId)` keys in a `HashSet<string>` and only emits the DEBUG line on first hit per key. Transitions still log; identical repeat calls are silent.

**Tests:** 6 new tests in `SpecialResourceServiceTests.cs` cover first-call logs, second-identical-call suppresses, all three branches (kingdom-hit / culture-fallback / no-match), and independent keys logging independently.

**Net effect:** ~1–6 SpecRes DEBUG lines per session instead of thousands. Real signal stays visible; log files shrink ~70%.

### Fix: FactionMap banner_flag.png ERROR on CC entry — empty defaults + demote LogError

Same 2026-05-04 log review caught `[ERROR] [Banner] File not found: ...banner_flag.png` firing once during the CC culture-stage `BannerWidget` initialization. `"banner_flag"` was a placeholder default with no matching PNG asset, set in 4 places (widget internal, VM, model, service fallback). The widget's `BannerImage` setter resets `_loadFailed` when the value changes, so the real banner loads successfully on data-bind — but the spurious ERROR log misled readers into thinking the FactionMap was broken.

**Change A — empty defaults:** all four `"banner_flag"` defaults → `""`. The existing `IsNullOrEmpty(_bannerImage)` short-circuit at `BannerWidget.TryLoadTexture` line 249 silently skips the load until a real bound value arrives.
- `Main/Features/FactionMap/Widgets/BannerWidget.cs` — internal default.
- `Main/Features/FactionMap/ViewModels/FactionSelectionVM.cs` — VM backing field.
- `Main/Features/FactionMap/Models/FactionSelectionResult.cs` — model property default.
- `Main/Features/FactionMap/FactionSelectionService.cs:108` — `!hasBanner` fallback (regions without `GameFaction`) now returns `""` instead of a non-existent placeholder name.

**Change B — demote LogError → LogDebug** for the file-not-found case at `BannerWidget.cs:267`. Other ERROR paths in the widget (engine returning null, exceptions) stay at ERROR — those aren't recoverable. Added `FactionMapPaths.LogDebug` helper.

**Test:** existing `FactionSelectionServiceTests.SelectRegion_NonPlayableFaction_HidesBanner` extended with `Assert.AreEqual("", result.BannerImage)` to lock the empty-fallback behavior.

Save-compat: no field changes; pure code/log severity. Safe on any save.

## 2026-04-23

### Feature: Spider Mount — orc rider on giant spider (warg-pattern mount path)

Spider is now a fully-mountable creature equipped via the standard Bannerlord HorseItem system, in addition to the C# spawner path (below). An uruk Dol Guldur trooper rides a giant spider into battle exactly like Isengard wargs work.

**Changes:**
- `LOTRLOME_Armory/ModuleData/monsters.xml` — added `rider_sit_bone="chest_m"` and `Mountable="true"` to the spider Monster.
- `LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` — 3 spider mount HorseItems (`spider_mount_a1`/`a2`/`a3`, mapping to material variants `m_mordor_spider_a1/a2/a3_mtl`). Cosmetic variants only — `a3` (Brood Mother) has higher stats (HP+100, charge_damage 7 vs 5).
- `Main/_Module/ModuleData/troops/troops_dolguldur.xml` — `dg_giant_spider_rider` (level 32, race=dg_uruk, default_group=Cavalry, occupation=Soldier, culture=dolguldur). Three EquipmentRoster entries randomly select between the 3 spider variants. Equipped with halberd + shield + mace + full elite armor, mirroring `dg_fell_warg_rider` skill profile.

**What works:**
- Custom Battle: select Dol Guldur → "Giant Spider Rider" appears in Cavalry slot. Troop spawns as uruk on a real spider (Monster.spider correctly applied because vanilla HorseItem spawn path resolves through `Monster.spider` not race resolution).
- Party templates: NOT yet wired (deferred for this v1). Spider riders won't appear in AI Dol Guldur lord armies until added to `taom_partyTemplates.xml`.
- Recruitment / VolunteerRecruitmentService: NOT yet wired (deferred). Cannot recruit spider riders from settlements yet.

**Architecture notes:**
- The orc rider is the soldier (occupation="Soldier", race="dg_uruk"). The spider is their MOUNT in the equipment slot. This is the warg pattern.
- Bannerlord cannot host non-humanoid creatures as direct troops; the mount-with-rider approach is the engine-native way to get a spider in player-controllable battle.
- The C# spawner path (below) is independent and complementary — it spawns rider-less standalone hostile spiders for ambient encounters.

**Known limitation:** No spider saddle/harness item exists yet. The HorseHarness slot is empty in all 3 EquipmentRoster entries. Visual will show the orc directly on the spider's back without saddle geometry. Future: author a `spider_saddle` HorseHarness item.

Constraint: rider_sit_bone="chest_m" is a best-guess on cephalothorax. Visual bobbing/clipping may need tuning after first in-game test.

Save-compat: New troop entry only — no field changes to existing entities. Safe load on any save.

Research: Decompiled `Mission.SpawnAgent` confirms `agentBuildData.AgentMonster` (resolved from HorseItem.Monster) is honored at spawn time; mount-path bypass of race resolution works correctly.

### Feature: Spider — AI hostile mob via direct Mission.SpawnAgent

Wires Erkam's `LOTRLOME_Armory` spider Monster + skeleton + 23 animations into actual gameplay. Custom Battle missions now spawn 5 hostile giant spiders on the enemy team 1 second after start, each driven by a behavior tree that attacks player agents in melee with bone-collision-detected fang bites.

**Architecture (mirrors `Main/Features/Warg/`):**
- `Main/Features/Spider/SpiderSpawnerService.cs` — `Mission.Current.SpawnAgent(AgentBuildData.Monster(spider))` with anchor character `taom_spider_creature` (humanoid race for engine compatibility, visual overridden by `Monster()`)
- `Main/Features/Spider/SpiderAttackService.cs` — bite damage formula + `CustomAttack` with fang bone indices
- `Main/Features/Spider/SpiderMissionBehavior.cs` — Custom-Battle-gated lifecycle, attaches `SpiderTree` BT to each spider
- `Main/Features/Spider/SpiderBehaviorTree.cs` — minimal: idle if no enemy near, otherwise bite + sleep
- 4 BT element files in `Main/Features/Spider/BehaviorTreeElements/`
- `Main/Adapters/IAgentAdapter.cs` — added `IsSpider()`, `IsSameTeam()`, `Health`, `State`, `GetBaseArmorEffectivenessForBodyPart()`
- `Main/_Module/SubModule.xml` — added optional `<DependedModule Id="LOTRLOME_Armory" />` and registered `characters/spider_creature.xml`

**ADR-007 fix:** Unlike `IWargAttackService`, `ISpiderAttackService` exposes `IAgentAdapter` (not raw `Agent`) — the attack/hit/spawn service is fully mockable without a live engine.

**Tests:** 20 new tests in `TAOM.Tests/Features/Spider/` — all green. Damage formula, skip-guard exhaustion, spawn validation, position math.

**Open items (v2):** Fang bone indices (`SpiderConfig.FangBoneIndex*`) are placeholders copied from warg — needs runtime probe to identify the actual spider skeleton bones for `joint5_l`, `joint5_r`, `joint12_m`. Campaign integration (Mirkwood scene triggers, Dol Guldur party templates) deferred until Custom Battle smoke test passes.

Constraint: Bannerlord's NPCCharacter race resolution is hardcoded humanoid-only — non-humanoid creatures cannot be direct troops. C# spawner via Mission API was the only viable path. The anchor `taom_spider_creature` exists solely to satisfy `AgentBuildData`'s `BasicCharacterObject` requirement; it never appears in party templates or troop pickers (`hidden_in_encyclopedia="true"`, `is_basic_troop="false"`).

Research: `tools/extract_fbx_bones.js` Node.js extractor confirmed 62-bone parity between updated `sk_spider_forest_c.fbx` and the 23 animation FBX files (Erkam's commits ca6f4cc5 + later strip). Engine lowercases bone names on import, so the skeleton's lowercase suffixes vs the animations' uppercase suffixes resolve correctly.

Save-compat: new troop entry only — no field changes to existing entities. Safe load on any save.

Not-tested: `Mission.SpawnAgent`, `BehaviorTreeAgentComponent` attachment, BT tick — engine-coupled, covered by in-game smoke test.

Research: `Mission.SpawnAgent(AgentBuildData, bool)`, `AgentBuildData.Monster(Monster)`, `AgentControllerType.AI` — verified via `ilspycmd` on installed `TaleWorlds.MountAndBlade.dll` (v1.3.15).

## 2026-05-01

### Feature: KEYforce Gondor armor revamp — 99 new items + 13 regional troop equipment refits (#99)

3D artist KEYforce shipped armor meshes for 8 previously-uncovered Gondor regions (Lossarnach, Pinnath Gelin, Harondor, Anfalas, Serelond, Lebennin, Belfalas, Lamedon). All meshes are now wired into `LOTRLOME_Armory` and 107 Gondor troops across 13 regions have new equipment loadouts following the artist's per-tier armor + weapon guide at `E:\repos\lotraom-assets\tools\gondor_armors_and_troops.txt`.

**Armory additions (Steam install path):**
- `LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/head_armors.xml` — 39 helmets (Pinnath Gelin 5, Harondor 6, Anfalas 7, Serelond 4, Lamedon 17 incl. lord-tier hero gear)
- `body_armors.xml` — 42 chests across all 8 missing families
- `shoulder_armors.xml` — 9 pauldrons (Lossarnach 3, Serelond 6)
- `arm_armors.xml` — 5 bracers (Lossarnach 1, Serelond 4)
- `leg_armors.xml` — 4 Serelond greaves
- All 99 items use the `STAT_TIERS` table from phase-1 `tools/generate_gondor_armor.py` (consistent with existing Anorien/MT/Osg/Cair/Ith items)

**Troop equipment changes (`Main/_Module/ModuleData/troops/troops_gondor.xml`):**
- 98 troops: equipment loadouts swapped to new region-specific armor per artist's progression tables
- 5 troops deleted (Lossarnach noble branch retired): `gondor_loss_noble`, `_axeman`, `_axeguard`, `_axewarden`, `_high_axewarden`. The mainline axebearer line covers the same role.
- 9 troops: equipment already matched the target loadout (no-op)
- 6 out-of-scope regions (Arndir, Methir, Blackroot Vale, Ringlo Vale, Tolfalas, Pelargir, Linhir, Calembel, Dol Amroth) untouched — KEYforce will ship gear for them later

**Cross-system updates:**
- `VolunteerRecruitmentService.cs` — `castle_EW8`, `castle_EW12`, `clan_empire_west_5` recruitment now upgrades into `gondor_loss_axebearer` (was deleted `gondor_loss_noble`)
- `settlement_guards_config.xml` — Lossarnach castle guard pool swaps `_axeguard` (deleted) for `_vet_axebearer` mainline equivalent
- `tools/generate_gondor_troops.py` — removed Lossarnach noble line definitions so re-runs don't recreate deleted troops

**New tooling:**
- `tools/generate_gondor_armor_phase2.py` — sibling to phase-1 generator; idempotent author of the 99 missing items, defaults to Steam install path
- `tools/apply_gondor_troop_revamp.py` — mechanically applies the 107-troop equipment blueprint produced by 4 parallel planning agents; preserves Horse/HorseHarness on cavalry, deletes orphan blocks, removes upgrade_target references
- `tools/validate_gondor_refs.py` — gates the underwear bug; cross-checks every `sk_gd_*` reference in `troops_gondor.xml` against Armory IDs (PASS = 155 refs, 0 missing)

**Verification:**
- Build: 0 errors (703 pre-existing nullable warnings unchanged)
- Tests: 1162 pass / 1 pre-existing unrelated MainMenuCustomizer localization mismatch from #96 (84/84 VolunteerRecruitment tests pass)
- Cross-reference: 0 missing item references — no underwear bug

**Decisions:**
- `sk_dg_ano_grvs_*` in source-of-truth treated as artist typo; mapped to existing `sk_gd_ano_grvs_*`
- Save-compat skipped (new mod version permits troop deletes/renames per user direction)
- 4 weapon slots maximum (Item0–Item3) honored — Belfalas/Osgiliath archers drop a quiver to make room for shield+sword

Research: phase-1 `STAT_TIERS` table reused verbatim for stat consistency; LOTRLOME_Armory item XML format matched against existing entries.
Save-compat: troop IDs deleted (5) — incompatible with v1.2/early-v1.3 saves carrying those IDs; new mod version intentionally breaks compat.
Not-tested: in-game visual check (manual spot-check pending in custom battle for Anorien T6 Knight, Lossarnach T6 Vet Guard, Serelond T7 Phalanx, Lamedon T6 Hill-Warden, MT T9 Fountain Guard).

## 2026-04-29

### Fix: NRE in CareerSystem mission behavior on Custom Battle launch (#97)

Launching any non-campaign mission (Custom Battle Minas Tirith repro'd) crashed at `TaleWorlds.CampaignSystem.Hero.get_MainHero()` from `CareerPerkMissionBehavior.OnMissionTick`. Root cause: v1.3.15's `Hero.MainHero` getter is `CharacterObject.PlayerCharacter.HeroObject` with no internal null guard, and `CareerPerkMissionBehavior` was being registered to every mission unconditionally (gated only on service availability, not mission type). The existing `if (hero == null) return;` was unreachable — the throw happened on the line above.

Two-layer fix:

- **Registration gate** in `Main/SubModule.cs:426` — `OnMissionBehaviorInitialize` now requires `Campaign.Current != null` to register the behavior. Custom Battle / Tutorial / Editor / Multiplayer missions skip the entire behavior, including HUD allocation.
- **Per-method defense in depth** in `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` (4 methods: `OnMissionTick`, `MutateTemplate`, `OnScoreHit`, `OnAgentRemoved`) — added `if (Campaign.Current == null) return;` semantic gate, and replaced `var hero = Hero.MainHero;` with `var hero = CharacterObject.PlayerCharacter?.HeroObject;` to bypass the unsafe getter. Codex independent review specifically flagged that `Campaign.Current` alone is correlated, not identical, to the actual precondition — both layers are needed.

`Mission.IsCampaignMission` is not available on v1.3.15 (added in v1.4); `Campaign.Current != null` is the canonical idiom.

**Tests:** 153/153 CareerSystem tests pass; full suite unchanged.
**Deep review (5 agents):** STANDARDS PASS, COMPATIBILITY PASS (4 v1.3.15 APIs verified via `ilspycmd`), EFFICIENCY PASS (net reduction in custom-battle work), DATA FLOW PASS (7 flows traced, 0 gaps).
**Codex independent review:** APPROVE after second pass.

**Side effect (.codex/config.toml):** `approval_policy = "unless-allow-listed"` → `"on-failure"`. Codex CLI 0.125.0 renamed the variant; the old name throws on load. Picked `on-failure` as the closest semantic equivalent for review/verification workflows.

Research: `Hero.MainHero` getter in `TaleWorlds.CampaignSystem.Hero`; `Campaign.Current` getter; v1.3.15 vs v1.4 API drift on `Mission.IsCampaignMission`.
Save-compat: No save format impact — registration-time and runtime guards only.
Constraint: `Hero.MainHero` getter has no internal null guard on v1.3.15.

### Feature: Code-side string localization — Main Menu + CC Narratives + Career System (#96)

Migrated the last meaningful classes of hardcoded in-game text into the localization XML system after Polish and Spanish translators flagged the gaps. Three change patterns: (1) wrap C# `new TextObject(literal)` calls with `{=KEY}default` syntax, (2) extract two new source loc XMLs (`taom_cc_strings.xml` 772 entries, `taom_career_strings.xml` 2,050 entries), (3) scaffold per-language stubs across all 12 supported languages. Total translatable strings now ~4,780 (was ~1,950).

**C# changes (3 files):**
- `MainMenuCustomizerService.cs:19` — `"Enter The Age Of Men"` → `{=taom_main_menu_new_game}Enter The Age Of Men`
- `CareerScreenVM.cs:65-70, 113-114` — 6 hardcoded UI labels (Career, Done, Tier 1/2/3, Career Ability) and the Free Points format string wrapped in `new TextObject("{=KEY}default")`. Free Points uses `SetTextVariable("COUNT", ...)` so translators can reposition the placeholder.
- `NarrativeMenuBuilder.cs:76-77` — every CC narrative entry wraps text+description with interpolated `{=taom_cc_<string_id>_text/desc}` keys derived from the JSON `string_id` field.

**Generated source XMLs:**
- `taom_cc_strings.xml` — 772 entries extracted from `charactercreation/{parents,childhood,youth,education,adulthood}_menu.json` (text + description per entry)
- `taom_career_strings.xml` — 2,050 entries extracted from `career_system/{taom_careers,taom_ability_templates,taom_career_choices}.xml`. The career data files already used inline `{=KEY}default`; this file gives translators a single discoverable list.

**Infrastructure:**
- 8 new entries in `taom_module_strings.xml` for the C# UI labels (taom_main_menu_new_game, taom_career_screen_title, taom_career_done, taom_career_ability_label, taom_career_tier1/2/3, taom_career_free_points)
- `SubModule.xml` registers both new XMLs as GameText paths (Campaign/CampaignStoryMode/CustomGame/EditorGame)
- All 12 `language_data.xml` files updated to declare 5 LanguageFile entries (module + wanderer + companion + cc + career)
- 24 new stub translation files (12 langs × 2 new files)
- PL and SP populated with English templates for the 2 new files; PL retains the translator's existing translations on the original 3 files

**Tests:**
- `LanguageDataXmlTests.cs` — count test renamed `HaveExactlyThreeLanguageFiles` → `HaveExactlyFiveLanguageFiles` (3→5), plus 2 new presence tests `AllLanguageDirs_HaveCcStringsFile` and `AllLanguageDirs_HaveCareerStringsFile`

**Tooling:**
- `tools/generate_translation_template.py` — `SOURCES` list now covers all 5 file types
- `docs/localization/TRANSLATOR_GUIDE.md` — counts and tables updated; Known Limitations explicitly lists the remaining gaps (CareerChoice/Group display names, CareerButtonPrefab embedded label)

**Process:**
- Added `Main/_Module/ModuleData/Languages/SP.zip` to `.gitignore` (translator backup artifact, not a project file)

**Deep review (5 agents):** STANDARDS PASS, COMPATIBILITY PASS (3 verified, 0 incompatible), EFFICIENCY PASS (all changed sites cold-path), DATA FLOW PASS (6 flows traced, 0 gaps, 0 inconsistencies). Closes #96.

Research: TextObject `{=KEY}default` parsing in `MBTextManager.GetLocalizedText` (TaleWorlds.Localization).
Save-compat: No save format impact — all changes are display-string layer.

## 2026-04-27

### Tooling: FBX -> 4-XML weapon-build pipeline (#95)

Added `tools/build_weapon_xml.py` and the `tools/weapon_xml/` package to automate the four-file weapon-authoring process the Armory historically did by hand: `LOTRLOME_crafting_pieces.xml`, `LOTRLOME_items/LOTRAOM_weapons.xml`, `crafting_templates.xslt`, `weapon_descriptions.xslt`. Project-agnostic — output target resolved via flag, `weapon_xml.toml`, or interactive prompt. XML manifest format mirrors the output schema; auto-derives piece IDs / mesh refs / `body_name` / culture from FBX mesh names + manifest hints. Idempotent: re-running with the same manifest is zero-diff. Supports both crafted (4-piece) and single-piece (Bow/Javelin/Throwing) weapons.

25 unit tests cover classification, manifest parsing, render shape, idempotency, and end-to-end pipeline. Smoke-tested against the real LOTRLOME_Armory ModuleData: existing weapon = no-op; fresh manifest = clean diffs across all four files. Documented in `docs/features/weapon-xml-pipeline.md`.

**Fixes from in-session deep-review (3 HIGH + 2 MED):**
- XSLT self-heal: previous design gated XSLT inserts on `new_piece_ids`, so a partial first run (pieces written, XSLTs not) silently orphaned pieces forever. Pipeline now passes ALL piece IDs to the XSLT step and relies on the per-entry idempotency guard already in `render_xslt`. Regression test: `test_xslt_self_heal_after_partial_first_run`.
- `body_name` auto-derivation: `bo_<mesh>` was wrong for `sm_`-prefixed weapons (Armory drops `sm_` in collision names; keeps `wm_`). Extracted to `classify.derive_collision_name`. Regression tests cover both prefix cases.
- Atomic writes: `_write_deltas` now writes all four files to `<path>.tmp.<pid>` first, then `os.replace` once all temp writes succeed. A crash mid-flight no longer leaves partial state.
- Newline preservation: paired `newline=""` on read and write so original CRLF/LF style survives edits (cleaner git diffs).
- Culture resolution wired: explicit `culture=` -> `classify.detect_culture_from_id` (prefix) -> `config.prompt_culture` (interactive, defaults to `empire`). The `interactive_culture` parameter is no longer dead. Regression test: `test_culture_resolved_from_prefix_when_absent`.

### Fix: Codex review #29 on Tier 2/3 adoption (#94)

Codex adversarial pass on `79350f2` (Tier 2/3 adoption from Pass 4 of the ecosystem-review chain) caught **1 HIGH + 2 MED + 2 LOW + 1 process gap**. Review file: `docs/archive/codex-reviews-2026-04/codex-adversarial-tier2-3-2026-04-26.md`. All addressed.

**HIGH (real prevention theater — same class as review #28):**
- `.claude/hooks/suggest-compact.sh` shipped with a bare `*"git commit"*` substring matcher. The codified rule against this exact pattern lives in `harness-facts.md` "Git invocation forms" (added in `2c4d414`) and was loaded into every session — but I didn't apply it when writing new commit detection in `79350f2`. **The prevention rule existed but wasn't applied to its own first user.** Replaced the matcher with the canonical two-stage pattern (reject `commit-tree`/`commit-graph`, then match `git commit` and `git -X ... commit`). Smoke-tested 5/5 cases (bare commit, `git -C path commit`, `git -c key=val commit`, `commit-tree` rejection, `git push`). Strengthened `harness-facts.md` to mark the pattern MANDATORY for new hooks; added grep-before-ship discipline; added new audit-checklist item to `/skill-stocktake`.

**MEDIUM:**
- `/scope-check` scope-reduction "prohibition" rule was prose-only. Codex correctly flagged: without a deterministic verifier, it's aspirational. Relabeled as **GUIDANCE (aspirational)** with explicit note that there's no hook or plan-vs-delivery diff backing it.
- `/skill-stocktake` checklist drift: missed the post-#28 codified rules for amend-exemption pattern and DOC-BACKED vs EMPIRICAL labeling. The audit was certifying against a stale checklist. Added 2 new sections: "Hook integrity" (commit-form patterns + amend exemptions) and "Documentation labeling" (DOC-BACKED vs EMPIRICAL).
- (Promoted from suspect 2) `/scope-check effort: low` was directly attenuating the inline reasoning the new scope-reduction classification depends on. Unlike `/deep-review` which dispatches subagents, `/scope-check` thinks inline. Removed `effort: low` (defaults to inherit). Added stocktake checklist item: `effort: low` should NOT be set on skills doing significant inline reasoning.

**LOW:**
- `/context-save` SKILL.md freeze interaction note had its conditional backwards. Said "if you've frozen to `.claude/`, the write will be blocked" — actually freezing to `.claude/` ALLOWS the write because `.claude/state/context/` is INSIDE `.claude/`. Rewrote correctly: only freeze scopes that EXCLUDE `.claude/state/context/` block.
- This CHANGELOG entry corrects the prior-commit's "validator will auto-bump" wording. The tool requires explicit `--fix`. Doc drift, not runtime defeat.

**Process:**
- Created issue #94 retroactively for this fix.
- Counter validator auto-bumped via `bash tools/audit-review-counter.sh --fix`. AGENTS.md now matches REVIEW-LOG.md at 29 reviews / 83 bugs.

REVIEW-LOG.md row #29 added with full per-finding RCA per harness-facts.md rule 4. Closes #94.

### Feature: Erebor Runes Texture-Stamping Pipeline

End-to-end pipeline for adding dwarven runes, knot-trims, and heraldic
motifs to Erebor architecture by stamping AI-generated black-on-white
masks onto base PBR textures.

**Stamper** (`tools/stamp_erebor_runes.py`) — 5 modes via `METALS` dict +
carved-groove constants:

- `carved` — engraved groove. Diffuse darkens, normal indents, specular dampens.
- `gold` — warm yellow inlay (Bandos-Warforge hero look).
- `silver` — neutral cool metal inlay.
- `bronze` — copper-orange inlay (forge / smithy iconography).
- `mithril` — pale silver-blue Tolkien "true-silver" inlay.

Two placement kinds:

- `centered` — hero stamps at configurable scale + position.
- `band` — horizontal trim bands with optional tiling and Y-position.

Auto-crop trims white margins from AI-generated masks before scaling, so
mask aspect maps correctly onto the base texture. Per-channel processing
respects mixed channel resolutions (Erebor base is 4096 d/n + 2048 s).

**Mask cleaner** (`tools/runes/clean_ai_mask.py`) — threshold + median
filter + downsample to 1024×1024 grayscale. Handles raw MJ V7 / Recraft
v4 Pro outputs cleanly.

**Mask library scaffolding:**

- `tools/runes/raw_ai/` (gitignored) — drop point for AI-generated raws
- `tools/runes/masks/{hero,filler}/` — cleaned mask library
- `tools/runes/reference/mirkwood_stone_engraved_*.png` — vendored from
  LOTR_Map as PBR-channel calibration reference
- `tools/runes/manifest.json` — base × mask × mode catalogue with
  `placement` block schema
- `tools/runes/ai_prompt.txt` — Recraft / MJ prompt templates + Erebor
  motif catalogue + locked web-UI settings

**Documentation:** `docs/kitbash/erebor/runes.md` covers the pipeline,
naming convention, mode behaviour, and Tier-1 / Tier-2 authoring paths.

**Constraint:** Bannerlord's vanilla `decal_sets.xml` system targets
ephemeral runtime decals (blood, footsteps) — wrong tool for
authoring-time architectural detail. We stamp into PBR triples instead.

**Research:** `LOTR_Map\AssetSources\mirkwood\Kitbash\textures\
mirkwood_stone_engraved_*.png` — proof-of-concept that engraved stone
PBR sets work with the same naming style (`_d/_n/_s/_h`).

**Save-compat:** None — pure asset-pipeline tooling.

**Not-tested:** In-engine readability of stamps under torchlight on Erebor
test scene; mesh-variant authoring (Tier 1 — `sm_dw_*_runic_a1.fbx`) not
yet started.

## 2026-04-26 (latest+2)

### Feature: Tier 2 + 3 picks from Claude Code ecosystem review (#93)

Following Tier 1 adoption + the prevention infrastructure, implementing the remaining 7 actionable picks from the 8-repo review documented in `~/.claude/plans/review-the-repo-at-fluttering-church.md`. (Picks #2 retired earlier as moot, #8 deferred as overkill for solo dev, #11/#12/#13/#15/#16/#17 already done in fbfd25a.)

**New skills (4):**
- `/agent-introspection-debugging` — 4-phase self-debug for failing agent runs (looping, drifting, burning tokens). Complements `/investigate` (which is for code bugs). Source: everything-claude-code. Pick #6.
- `/context-save` — snapshot working state (git, in-flight tasks, decisions, files in flight) to `.claude/state/context/<timestamp>.md` so a future session can resume without re-deriving decisions. Pair with `/context-restore`. Source: gstack. Pick #7.
- `/context-restore` — load most recent (or named) snapshot. Cross-checks against current git state. Source: gstack. Pick #7.
- `/skill-stocktake` — periodic quality audit of installed skills + agents. Quick scan (recent only) or `full`. Catches decay (broken refs, stale paths, bloated descriptions) before sessions silently degrade. Source: everything-claude-code. Pick #10.

**New subagents (3):**
- `debugger` — generic systematic debugging for non-TAOM issues (tooling, scripts, build infra). Use `/investigate` for TAOM C#. Source: VoltAgent/awesome-claude-code-subagents. Pick #19.
- `error-detective` — cross-system error correlation when one root cause manifests as multiple symptoms. Adapted from microservices framing to TAOM-feature framing (e.g., shared TaleWorlds API, lifecycle phase, culture/race ID). Source: same. Pick #19.
- `refactoring-specialist` — behavior-preserving structural refactoring with TAOM ADR rules baked in. Iron rule: tests green before AND after. Boundary vs `/deslop` (deletion-first) and `code-architect` (greenfield design) explicit in the agent definition. Source: same. Pick #19.

**Hook upgrade:**
- `suggest-compact.sh` — added boundary-aware suggestions on top of existing threshold-based ones. Now nudges `/compact + /context-save` at task transitions (`git commit`, `./build.ps1`, `dotnet test`, `git push`) with throttling (≥10 calls between boundary suggestions). Pick #9.

**Frontmatter additions:**
- `effort: high` added to `/deep-review` (4-6 parallel review agents — needs the compute budget).
- `effort: low` added to `/scope-check` (lightweight assessment, doesn't need max effort).
- Verified `effort:` field is documented in current Claude Code skill schema (`https://code.claude.com/docs/en/skills`). Pick #14.

**Scope-reduction prohibition:**
- Added rule to `/scope-check` SKILL.md: don't silently drop scope. When a proposed change exceeds the current task, list every concern, classify, and present a phase split (do-now vs follow-up) for user decision. The third option — "drop Y silently" — is explicitly NOT on the menu. Source: gsd-build/get-shit-done's planner-source-audit pattern. Pick #18.

**Routing table:**
- Added 8 new rows to CLAUDE.md Skill Routing covering all of the above (proactive + soft-suggest tiers).

**Already done in prior commits (not part of this issue):**
- Pick #2 retired (Claude Code already does two-layer skill injection natively).
- Picks #11, #12, #13, #15, #16, #17 done in fbfd25a (sharpening rules).

**Deferred:**
- Pick #3 (three-layer compression) — 94% headroom on Opus 4.7, no real constraint.
- Pick #8 (persistent task DAG with `blockedBy`) — TodoWrite is sufficient for solo dev; the DAG infrastructure is overkill for our scale.

**Verification:**
- `/context-budget` after additions: eager 58,843 → 63,061 tokens (+4,218, +7%). Worst-case 76,036 → 86,620. Headroom held at 94% / 91% on Opus 4.7 (1M).
- All new skills/agents have ≤30-word descriptions per `harness-facts.md`.
- All new files staged and tracked (the new pre-commit hook from b7e7188 will block any gitignored slip-ups).
- Counter validator will auto-bump REVIEW-LOG → AGENTS.md.

Closes #93.

## 2026-04-26 (latest+1)

### Process: Retroactive Full RCA on Codex Review #28 + Preventives

User caught a process gap: Phase 3e of `/review-codex` ("Root Cause Analysis for EACH confirmed bug") was only run for the HIGH+MED-1 findings on Codex pass 3 (b7e7188 → 5fd9719). The other 6 findings (MED-2, MED-3, LOW-1/2/3/4 + 1 process gap) got fixes but not the systemic "why missed / preventive" analysis. Same conflation I'd just RCA'd: severity ≠ importance for systemic learning.

Retroactive corrections:
- `docs/reviews/REVIEW-LOG.md` row #28 RCA table extended from 2 grouped roots to 9 individual rows. Each finding now has Bug / Category / Why missed / Preventive action.
- `.claude/rules/harness-facts.md`: added two new sections capturing the systemic lessons that came out of the full RCA:
  - **Git invocation forms hooks must handle** — explicit table (bare commit, `-m`, `--amend`, `-F`, `git -C path commit`, `git -c key=val commit`, plus `commit-tree` rejection) with the reference pattern the prevention hooks use. Future hook authors no longer have to discover these by getting them wrong.
  - **Amend exemptions in pre-commit hooks (recursion-risk pattern)** — codifies the lesson from the HIGH bypass: don't blanket-skip amends; choose either post-amend file-set logic (for diff-based gates) or no exemption at all (for working-tree gates).
- `.claude/rules/harness-facts.md` "How this rule changes how you work":
  - Added rule #4 — `/review-codex` Phase 3e applies to EVERY confirmed bug, not just HIGH. The meta-lesson the user surfaced.
  - Added rule #5 — DOC-BACKED vs EMPIRICAL labeling convention for any fact in this file or any other rule. Vague "verified" claims age into wrong assumptions (caught on the project-slug rule in pass 3).
- `.claude/skills/context-budget/scan.sh`: comment on `extract_description` documenting the multiline YAML limitation Codex flagged (MED-3, deferred fix).
- `CLAUDE.md` Completion Workflow Phase 4: explicit note that the GitHub issue must exist BEFORE the closing commit, not after — Codex caught us creating issue #92 retroactively for b7e7188.

No code-behavior changes; this commit is doc + rule additions plus the retroactive RCA. Counter unchanged (28 reviews, 77 bugs).

## 2026-04-26 (latest)

### Fix: Codex Adversarial Review of the Prevention Infrastructure (#92)

User asked "we did our review?" — answer was no. Dispatched Codex pass on `b7e7188` with explicit recursion-risk framing: could a bug in the prevention infrastructure defeat the prevention it's supposed to enable?

Verdict: yes. 1 HIGH, 3 MEDIUM, 2 LOW + 1 process gap. Review at `docs/archive/codex-reviews-2026-04/codex-adversarial-prevention-2026-04-26.md`. All addressed.

**HIGH (real prevention theater):**
- Both new pre-commit hooks blanket-skipped `git commit --amend`. My "amends modify a prior commit's responsibility" rationale was wrong — amend-as-workflow is common ("oops, forgot a file, amend it in"). The hooks exempted exactly the case they were supposed to catch. Even worse: a two-step bypass (unrelated commit + amend with `.claude/`) would defeat both gates.
  - **CHANGELOG hook fix:** replaced blanket exemption with logic that evaluates the post-amend file set (staged ∪ HEAD). If `.claude/` files are in the post-amend commit and CHANGELOG.md isn't, block. If CHANGELOG is already in HEAD (carried over from the prior commit), allow.
  - **Tracked-files hook fix:** removed the exemption entirely. Working-tree state isn't amend-affected — a gitignored file on disk is just as broken in an amended commit as a fresh one.

**MEDIUM:**
- Both hooks missed `git -C path commit` and `git -c key=val commit` (substring match `*"git commit"*` doesn't match these). Broadened to `*"git commit"* | *"git -"*" commit"*`. Reject `git commit-tree`, `commit-graph` (different commands) explicitly.
- Bloat lint bypassed by multiline YAML descriptions (`description: |` block). No current skill uses this; deferred.

**LOW:**
- harness-facts.md said hooks "will warn" but they actually hard-block — corrected to "hard-block" with explanation.
- harness-facts.md missing `disable-model-invocation: true` exception for skill description loading — added.
- harness-facts.md presented the project-slug derivation rule as fact — actually empirical (Claude Code docs only say "derived from the git repository"). Relabeled as empirical with derived-then-fallback recommendation.
- audit-review-counter.sh regex tolerated only the exact wording "N Codex reviews total, M bugs found". Hardened to anchor on summary keywords (`total | so far | conducted | completed`) and extract numbers via keyword anchoring rather than first-N (caught a subtle bug during testing where "19-27. 27 Codex reviews total" yielded `19, 27` instead of `27, 71`).

**Process:**
- Created retroactive GitHub issue (#92) for the prevention bundle. Original ship of `b7e7188` skipped this — Codex flagged it.

Verified:
- TEST G: amend on a throwaway branch with HEAD lacking CHANGELOG and amend adding `.claude/` — hook BLOCKS correctly. The HIGH bypass is fixed.
- `git -C path commit` and `git -c key=val commit` both detected.
- `git commit-tree` correctly skipped (different command).
- Counter validator reports `27 reviews, 71 bugs` matching across both files.

REVIEW-LOG row #28 added; counter advanced to 28 reviews / 77 bugs found.

## 2026-04-26 (later)

### Process: Prevention Infrastructure for Recurring Harness Bugs

Across three Codex/deep-review passes on the Tier 1 productivity-skills adoption (efbde5b, 5df21ea, 4964299), 19 issues clustered into 5 recurring categories. Built mechanical prevention for each so the same class of bug cannot ship again.

**New rules** (auto-loaded into every conversation):
- `.claude/rules/harness-facts.md` — pinned source-of-truth for Claude Code load semantics (skill descriptions eager, bodies lazy; hooks scoped to skill activation; rules without `paths:` always-load) with doc URLs. Future harness edits check against this file first; if reality disagrees, this file gets updated FIRST.
- `.claude/rules/external-skill-ports.md` — per-field validation checklist scoped to `.claude/skills/**/SKILL.md`. Catches port-drift bugs (`triggers:` field, lifecycle assumptions, hardcoded values, gitignored bin/ scripts) before they ship. Includes a Tier 1 adoption case study.

**New pre-commit hooks**:
- `check-changelog-changed.sh` — hard-blocks `git commit` when `.claude/`, `CLAUDE.md`, or `AGENTS.md` is staged but `CHANGELOG.md` is not. Skips amends. The pre-existing `check-changelog-updated.sh` was a Stop-time *reminder* — easy to ignore. This new hook is enforcement.
- `check-claude-files-tracked.sh` — hard-blocks `git commit` when files exist on disk under `.claude/{skills,agents,rules,hooks}/` but are gitignored or untracked. Catches the `bin/check-freeze.sh` regression class (a generic gitignore pattern silently excluded a load-bearing script).

**New tool**:
- `tools/audit-review-counter.sh` — recomputes "N reviews, M bugs found" from `REVIEW-LOG.md` and verifies `AGENTS.md` matches. `--fix` flag updates AGENTS.md in place. Catches manual arithmetic errors (we shipped 64 when correct was 65). Counter math is now mechanical, not eyeballed.

**Lints upgraded**:
- `scan.sh` now flags skill descriptions over 30 words (previously only flagged for agents). Catches description-creep that re-occurred after every fix in the Tier 1 chain.

**Verified**:
- Counter validator caught the 26→27, 65→71 mismatch from review #27 and auto-fixed AGENTS.md.
- check-changelog-changed correctly blocks (`.claude/` staged + no CHANGELOG) and allows (CHANGELOG also staged).
- check-claude-files-tracked correctly blocks on untracked new files; both hooks correctly skip on `git commit --amend`.
- scan.sh bloat lint runs without false positives on current setup (no skill exceeds 30w).

## 2026-04-26

### Process: Adopt Tier 1 Productivity Skills from Claude Code Ecosystem Review

Reviewed 8 community Claude Code repos (gstack, everything-claude-code, gsd-build/get-shit-done, learn-claude-code, claude-code-best-practice, awesome-claude-code-subagents, claude-code-system-prompts, x1xhlol/system-prompts) for harness improvements. Productivity-biased; security-flavored picks deferred. Plan file: `~/.claude/plans/review-the-repo-at-fluttering-church.md`.

**New skills:**
- `/context-budget` — token audit across `.claude/`, MCP, CLAUDE.md (scan.sh + SKILL.md). First baseline at `docs/archive/research-prompts-2026-04/context-budget-baseline.md`: ~64K tokens, 94% headroom on Opus 4.7 1M.
- `/freeze` — hard-block Edit/Write outside a chosen directory using inline PreToolUse hooks declared in skill frontmatter. Pair with `/unfreeze`.
- `/unfreeze` — release the freeze boundary.
- `/investigate` — six-phase root-cause workflow with TAOM-specific failure patterns (Harmony, MCM, save-load, decompile drift). Auto-engages `/freeze`.

**Retry budget rules** added to `/build-fix` skill and `feature-builder` agent: 4-attempt hard stop on the same error. `/build-fix` escalates to `/investigate` for structural issues, `/research` for TaleWorlds API drift, or surfaces environment failures (don't auto-fix infra).

**Sharpening rules:**
- New `.claude/rules/environment-failures.md` (always-loaded via `**/*` glob) — report environment failures, never auto-fix infra.
- `.claude/rules/csharp-architecture.md` — added stale-file re-read rule.
- `CLAUDE.md` Working Discipline section — fork discipline (no peeking at fork output, no fabricated results), autonomous-loop stewardship (continue work, don't initiate), TodoWrite quality bar.
- `CLAUDE.md` Skill Routing — phrase-to-skill mapping with strong-proactive / soft-suggest / never-auto tiers, plus confidence gates on `/deslop` and `/deep-review`.

**Cross-references** chain the workflow: feature-builder suggests `/freeze` upfront, `/build-fix` escalates to `/investigate`, `/new-feature` recommends scope-lock, `/deep-review` fix-loop suggests `/freeze` for module-confined fixes.

**Decision gate triggered:** Picks #2 (two-layer skill injection) and #3 (three-layer compression) deferred — 94% headroom means neither addresses a real constraint. Re-evaluate on smaller-context model migration or if skill/MCP counts grow significantly.

### Fix: Self-Review (Codex Pass 2) Findings on Pass-1 Fixes

Second Codex pass on `5df21ea` flagged 0 HIGH, 1 MEDIUM, 3 LOW + 1 process violation. Self-review at `docs/archive/codex-reviews-2026-04/codex-selfreview-tier1-fixes-2026-04-26.md`. All addressed in this third commit:

- `scan_memory()` locator was substring-matching project basename, which collided on this machine (TAOM, TAOM-Online, taommod). Replaced with exact Claude project slug derivation from full repo path; substring search retained as fallback only when slug derivation misses.
- 25KB byte cap was computed but never enforced in `scan_memory()` token estimate. Now enforced via `head -c 25600 | head -200` slice.
- "Lazy tok" column header was misleading (it printed full body, not the lazy delta). Renamed to "If-invoked" with explicit footer note that the WORST_CASE total adds only the delta.
- `ilspy` MCP server tool count was hardcoded as 8; verified actual is 4 (`decompile_assembly`, `list_types`, `generate_diagrammer`, `get_assembly_info` per `server.py`). Updated count and tagged each `SERVER_TOOLS` entry with EXACT vs HEURISTIC source.
- `/freeze` and `/investigate` descriptions had crept back to 31w during the prior phrase-into-description move. Trimmed to 21w and 23w respectively.
- AGENTS.md bug counter said 26 reviews / 64 bugs; correct math is 65 (57 prior + 7 confirmed + 1 bonus from review #26). Reconciled.
- This CHANGELOG entry covers both `5df21ea` (which was committed without an entry, violating CLAUDE.md "Documentation Requirements" — Codex caught this in self-review) and the present third-fix commit.

### Fix: Deep-Review Findings on the Adoption Itself (commit-on-commit)

Deep-review of `efbde5b` surfaced 4 HIGH findings, all addressed in follow-up:
- `check-freeze.sh` was excluded by `.gitignore`'s `bin/` pattern (intended for `Main/bin/` .NET output). Moved to `.claude/skills/freeze/check-freeze.sh`; updated SKILL.md hook command paths in both `/freeze` and `/investigate`.
- `check-freeze.sh` JSON output didn't escape backslashes/quotes — Windows paths with `\` would have produced invalid JSON. Added `_json_escape` helper. Also added absolute-path validation (fail-open if state file is malformed).
- Skill descriptions were 39w (freeze) and 47w (investigate). Trimmed to ~15w each (loaded into every Task spawn). Added `triggers:` arrays preserved from gstack source for natural-language activation.
- Skill Routing table added confidence gates to `/deslop` (only if clearly redundant) and `/deep-review` (only for C# changes ≥2 files), added `/migration-status` row, fixed `/unfreeze` trigger phrase, added ship-sequence soft-suggest with `/codex-verify`/`/review-codex`. Renamed "auto-invoke" → "proactively invoke" (clarifies tool permission semantics).
- `scan.sh` MCP loop hardened (whitespace-only line check + verbose warning when unknown server defaults to 15-tool estimate).

Sources adopted from: garrytan/gstack (freeze, investigate, working discipline rules), affaan-m/everything-claude-code (context-budget), Cursor/Devin/Piebald-AI prompt extracts (retry budget, fork discipline, autonomous-loop, stale-file rules).

Verified: `check-freeze.sh` 4/4 boundary tests pass including raw-Windows-path JSON validity check.
Not-tested: slash-command invocation in live Claude Code session (boundary script verified directly).

## 2026-04-20

### Fix: CareerScreenVM Service-Locator Anti-Pattern (8 test failures)

`CareerScreenVM` was resolving `ICareerConfigProvider` inline via `IoC.Resolve<T>()` and guarding `IModLogger` with a `try { IoC.Resolve<IModLogger>() } catch { }`. DryIoc isn't configured in unit tests, so every test that exercised `RefreshValues()` past the "no career set" guard threw `NullReferenceException` — 8 of 9 `CareerScreenVMTests` failing silently as "pre-existing."

- `CareerScreenVM` — added `ICareerConfigProvider` and `IModLogger` as constructor parameters; deleted the two inline `IoC.Resolve<ICareerConfigProvider>()` calls and the try/catch logger resolution
- `GauntletCareerScreen` — resolves both services at the boundary and passes them down; `CloseScreen()` now uses the cached `_logger` instead of re-resolving
- `CareerScreenVMTests` — `Setup()` mocks both new deps; `CreateVM()` passes them through
- Test suite: **1161 passed, 0 failed** (was 1153/8)

### Process: Mechanize No-Service-Locator Rule in Deep Review

Root cause of the above: the rule "Constructor injection only — no service locator in services" existed in `.claude/rules/csharp-architecture.md` but wasn't checked by the deep-review standards agent, so `/deep-review` passed while 8 tests failed.

- `.claude/skills/deep-review/SKILL.md` — Agent 1 (Standards Compliance) now grep-checks for `IoC.Resolve<` outside the six allowed boundary locations (Harmony patches, `ScreenBase` subclasses, `CampaignBehaviorBase` ctors, `GameModel` ctors, `SubModule.cs`, static `OpenXxx()` helpers). A `try { Resolve } catch { }` guard is explicitly called out as still-a-violation.
- Memory: `feedback_no_service_locator_in_services.md` — prevention rule plus reminder that "pre-existing test failures" are never background noise; investigate or track immediately.

### Feature: Revolt Tuning

Softens vanilla Bannerlord's revolt mechanic for LOTR's constant settlement flips. Vanilla punishes different-culture ownership at -3/day loyalty and revolts at loyalty ≤ 15 — in TAOM, where Gondor↔Mordor and Rohan↔Isengard towns change hands regularly, this spawned rebel clans every few weeks.

- New `RevoltTuning` feature with `IRevoltTuningConfigProvider` (Newtonsoft JSON, cached singleton, graceful fallback to defaults)
- JSON config at `Main/_Module/ModuleData/configs/revolt_tuning_config.json` — all four thresholds tunable without recompilation
- `TaomSettlementLoyaltyModel` extended with four new property overrides driven by the config:
  - `RebellionStartLoyaltyThreshold`: 15 → 5
  - `RebelliousStateStartLoyaltyThreshold`: 25 → 10
  - `SettlementOwnerDifferentCultureLoyaltyEffect`: -3.0 → -1.0
  - `GovernorDifferentCultureLoyaltyEffect`: -1.0 → -0.5
- Existing cultural feat bonuses (Gondor, Erebor, Lothlórien, Rivendell, Rohan) preserved
- Semantic validation in `RevoltTuningConfigProvider.Validate` — rejects out-of-range thresholds, inverted threshold ordering, and sign-flipped penalties; logs warning and falls back to defaults for invalid fields
- 13 unit tests: JSON parse, missing-file / malformed-JSON / empty-object fallbacks, partial-config merge, caching, default-value spec, plus 7 validation guardrail cases (out-of-range, negative threshold, ordering inversion, positive owner/governor penalty, valid-values-no-warning)

Research: `DefaultSettlementLoyaltyModel` (v1.3.15 via ilspycmd), `RebellionsCampaignBehavior`
Reviews: `/deep-review` (5 agents, 1 MEDIUM perf + 1 LOW thread-safety fixed), `/codex:adversarial-review` (1 HIGH no-validation + 1 MEDIUM cache-lifetime — both addressed)
Not-tested: GameModel entry point — verified live per ADR-008

### Feature: Defender Trebuchets

Siege defenders can now construct trebuchets on the campaign-map siege UI, matching the attacker engine list for parity. Built with Minas Tirith's upcoming siege scene in mind but applies to all defenders.

- New `TaomSiegeEventModel` (extends `DefaultSiegeEventModel`) — adds `Trebuchet` to `GetAvailableDefenderSiegeEngines`
- Preserves vanilla Engineering perk gating (Stonecutters / SiegeEngineer) for `FireBallista` / `FireCatapult`; `Trebuchet` is ungated for defenders (mirrors attacker availability)
- `FireTrebuchet` intentionally skipped — v1.3.15 getter bug returns the non-fire Trebuchet field
- Registered in `SubModule.OnGameStart` alongside the existing `SiegeDefenseBehavior`

Research: `DefaultSiegeEventModel.GetAvailableDefenderSiegeEngines`
Not-tested: GameModel entry point — verified in-game via siege management UI per ADR-008

## 2026-04-18

### Enhancement: Career Ability AoE — Extended to Ranged + Cavalry

Previously only Infantry ability buffs applied to nearby friendly troops. Ranged and Cavalry are now AoE too — activating any ability buffs the hero plus all nearby allies within the ability's radius. Every archetype feels like a commander aura now.

- `IAbilityExecutionContext` gained `ApplyAllyRangedBuff` (speed + ranged damage + draw speed) and `ApplyAllyCavalryBuff` (mount speed + charge damage + damage)
- `MissionAbilityExecutionContext` refactored to a shared `ApplyAoeBuff` helper that gathers nearby allies, clones an ally-buff template, and merges a hero accumulator
- `TaomAgentStatCalculateModel` now applies all ally buff fields for non-hero agents (previously only `DamageBonus`)
- All 50 ability templates standardized to `radius="50"` for consistent AoE size (was a mix of 8/10/12/50)
- Removed 6 dead interface methods: `ApplySpeedBuff`, `ApplyDamageBuff`, `ApplyResistanceBuff`, `ApplyDrawSpeedBuff`, `ApplyMountSpeedBuff`, `ApplyChargeDamageBuff` (no callers remained after the AoE refactor)
- Tests updated: `RangedAbilityExecutorTests` and `CavalryAbilityExecutorTests` now assert the new `ApplyAllyRangedBuff` / `ApplyAllyCavalryBuff` calls with correct argument ordering

## 2026-04-16

### Feature: Career Ability Execution — Phase IV Complete

Replaced 3 pilot ability executors with a complete role-based archetype system covering all 50 careers. Every career now fires a real in-battle effect when pressing V.

- 3 archetype executors: InfantryAbilityExecutor (AoE troop buff), RangedAbilityExecutor (self ranged buff), CavalryAbilityExecutor (self + mount buff)
- All 50 careers mapped to archetypes in CareerSystemIoC (16 cultures, 3 per culture + 2 extras for Mordor/Harad)
- XML-driven tuning via `taom_ability_tuning.xml` — all balance values configurable without recompilation
- Infantry: +damage given, -damage taken for all nearby troops (AoE via ally buff tracker)
- Ranged: +movement speed, +ranged damage, +bow draw speed (self)
- Cavalry: +mount speed, +charge damage, +damage given (self + mount)
- New `ActiveBuffs` fields: DrawSpeedBonus, MountSpeedBonus, ChargeDamageBonus, DamageReductionBonus
- Ally buff system: agent-index-keyed dictionary in CareerAbilityBuffTracker, read by TaomAgentStatCalculateModel for all human agents
- SoundEvent.PlaySound2D integration (silently skips unregistered FMOD events)
- Deleted old pilot executors: BloodrageExecutor, StealthExecutor, StampedeExecutor
- Constructor-injected IMutationService/ICareerHeroAdapterFactory in CareerPerkMissionBehavior (removed IoC.Resolve from hot path)
- Removed string interpolation from OnScoreHit/OnAgentRemoved debug logs (per-hit GC pressure)

## 2026-04-14

### Feature: Career Selection in Character Creation

Added a 6th narrative menu stage to character creation that lets players choose their career from culture-eligible options. Previously the system auto-assigned the first eligible career with no player choice.

- New "Career" stage appears after adulthood — shows 2-4 career options filtered by the player's selected culture
- Each career grants thematic skill and attribute bonuses during CC (e.g., Ranger of Ithilien gives Bow + Scouting + Cunning)
- 50 career entries in `career_menu.json` matching all 50 careers in `taom_careers.xml`
- Fallback "No specialization" option for cultures without careers (shaghana, abanissa) prevents empty-menu crash
- Backward compatible — legacy saves without career selection still auto-assign first eligible career
- Uses Bannerlord's `AddNewMenu()` API to insert into the narrative menu chain — no Harmony patches needed

**New files:** CareerMenuService, CareerMenuDataProvider, CareerMenuOptionDefinition, career_menu.json
**Tests:** 21 tests (CareerMenuServiceTests + CareerMenuDataProviderTests)

### Feature: Career Screen UI — Portraits, Ability Icons, and Sprite Atlas

Added AI-generated career portraits and ability icons for Gondor and Rohan (6 portraits, 6 ability icons). Created dedicated `ui_taom_career_system` sprite atlas to prevent career images from overflowing the main `ui_taom` atlas.

- **Gondor portraits:** Ranger of Ithilien, Captain of Osgiliath, Knight of Belfalas
- **Rohan portraits:** Marksman of Aldburg, Eotheod Windrider, Watchman of Stangard
- **Ability icons:** Ambush, Hold the Line, Stampede, Light Fletching, Warcry of Eorl, Stand Fast
- **Sprite atlas:** New `ui_taom_career_system` category registered in Config.xml with `<AlwaysLoad />`
- **Sprite dimensions:** Portraits 800x400, ability icons 256x256 (2x widget size for sharpness)
- **ChatGPT/Midjourney prompts:** Documented in `tools/comfyui/chatgpt_career_prompts.md` and feature docs

### Fix: Career Screen Bugs (6 issues)

- **IGameStateListener crash:** `GauntletCareerScreen` didn't implement `IGameStateListener`, causing NRE in `GameState.HandleInitialize()` when opening career screen from character developer
- **Localization tags not resolved:** Career name, description, ability name, choice descriptions all showed raw `{=key}Text` strings — wrapped in `TextObject().ToString()` across `CareerScreenVM`, `CareerChoiceObjectVM`, `CareerChoiceGroupObjectVM`
- **Ability name showing template ID:** `AbilityName` displayed `ranger_of_ithilien_ability` instead of "Ambush" — now resolves display name via `ICareerConfigProvider.GetAbilityTemplate()`
- **Description overlapping portrait:** Added `MarginTop="15"` to career description ScrollablePanel
- **Choice groups collapsed:** `ExtendablePanel` default width was 80px (collapsed) — changed to 750px (expanded)
- **Sprite atlas overflow:** Career images (1024x1024) overflowed main `ui_taom` atlas corrupting other UI — moved to dedicated `ui_taom_career_system` atlas

### Rename: Captain of Pelargir → Captain of Osgiliath

Renamed across all XML (careers, ability templates, choice trees), JSON (career_menu), and tests. Updated description from naval/maritime to infantry/urban combat. Ability renamed from "Sailing" to "Hold the Line".

### Fix: Ability Template Standardization

Standardized all Gondor and Rohan ability values to consistent template:
- **Ranged careers:** +20 ranged damage, radius 50, duration 8s
- **Infantry careers:** +20 melee damage, radius 50, duration 8s
- **Cavalry careers:** +20 charge damage (mounted) + 10 melee (troops), radius 50, duration 8s
- Renamed Watchman ability from "River Navigator" to "Stand Fast"

### Fix: Castar Spelling

Corrected Gondor special resource display name from "Caster" to "Castar" in `special_resources_config.xml`.

### Fix: In-Game Testing — Career Screen + Map Bar + Sprite Pipeline

Verified in-game on Gondor campaign. Fixed 6 runtime issues discovered during testing:

- **Career button sprite:** removed extra `TAOM\` prefix from sprite path — now correctly references `CareerSystem\career_button_placeholder` per TAOMSpriteData.xml registration
- **Career screen crash:** converted from `ScreenManager.PushScreen` to `GameStateManager.PushState` (TOR pattern), and added `ExecuteDone()` to close CharacterDeveloper before pushing career state
- **Map bar resource display:** fixed mixin hook from `"RefreshValues"` (one-time) to `"Refresh"` (per-frame, TOR pattern); fixed icon_sprite paths with `SpecialResources\` prefix; reverted to `SecondaryInfoItems.Add()` with proper `MapInfoItemVM` (TOR pattern — works with vanilla code)
- **Map bar tooltip:** rich tooltip now shows resource name/cap, tier status, daily change breakdown (income vs upkeep), and per-event earning rates
- **Shader precompilation:** confirmed working in-game — shader count decreasing steadily

**Verified working (Gondor):** Career button with sprite, Caster resource on map bar with tooltip, shader precompilation progress

## 2026-04-13

### Feature: Career System Overhaul + TOR Parity — 23 LOTR Careers + System Upgrades

Redesigned career system based on gap analysis against The Old Realms (TOR) Warhammer mod. Replaced 21 generic careers across 7 factions with 23 lore-accurate LOTR careers, each with full choice trees (31 choices per career).

**New careers by faction:**
- Gondor: Ranger of Ithilien, Captain of Pelargir, Knight of Belfalas
- Mordor: Black Uruk Captain, Mulkerhili Cultist, Snaga Rider, Olog-Hai Warchief (new Monster class)
- Rohan: Marksman of Aldburg, Eotheod Windrider, Watchman of Stangard
- Dunland: Avanc-luth Raider, Wolfskin Hunter, Clanguard Rider
- Rhun: Codyan Legionaire, Lokhas Drus Marksman, Balchoth Kan
- Harad: Tribesman of Jelut, Pezarsani Javelineer, Mahud Beast Rider, Far Harad Halftroll (Monster)
- Khand: Blademaster of Ren, Steppe Bowmaster, Chariot Warlord

**System upgrades (TOR parity):**
- Wired 3 cross-system passives: CustomResourceGain, CustomResourceUpkeepModifier, CustomResourceUpgradeCostModifier — careers now affect special resource economics
- New TaomAgentApplyDamageModel: ArmorPenetration, Resistance, ShruggedOff passives now functional in combat
- New TaomClanTierModel: CompanionLimit passive now functional
- Differentiated all 11 special resource earning rates per faction identity (no more identical values)
- Career screen UI rewrite: TOR-pattern expandable panels, career portrait, ability icons, lock chains, +/- selection buttons, hover interactions

**Totals:** 50 careers, 300 choice groups, ~1,550 choices, 50 ability templates

## 2026-04-10

### Feature: Fork NativeSkinFixes — covers_head Morph Fix + Hair/Beard Cloth Physics

Forked community NativeSkinFixes mod into TAOM. Fixes two Bannerlord native engine bugs by hooking C++ functions in TaleWorlds.Native.dll via MinHook:

- **covers_head jazz hands fix**: Helmets with `covers_head="true"` no longer break hand grip animations. The hook forces Face_mesh creation for the GPU morph pipeline while suppressing face rendering via the render list.
- **Hair/beard cloth physics**: Hair and beard meshes with cloth simulation data now animate with physics instead of rendering as static geometry. The hook rescues orphaned cloth from the cloth factory and registers it for both rendering and simulation.

**Architecture**: C++ native DLL (`TAOM.NativeSkinFixes.dll`) with 3 MinHook detours + C# P/Invoke interop layer. All 7 RVAs verified against Bannerlord v1.4.0. Transactional install with rollback on partial failure.

**Files**: `Dependencies/ThirdParty/NativeSkinFixes/` (C++ source + MinHook), `Main/Features/NativeSkinFixes/` (C# interop)

## 2026-04-09

### Fix: Dependencies Audit — 7 Bugs Fixed Across Harmony Fork + UIExtenderEx

Full audit of 1,442 vendored files across 7 subsystems. Found and fixed:
- ConfigurableArrayPool.Bucket.Return() — audited and confirmed correct (initial false-positive retracted)
- ReadOnlySequence.GetFirstBuffer() computed wrong length for string-backed sequences (unmasked bit 31)
- DependentHandle CAS loop inverted (infinite loop on successful compare-exchange)
- ThrowHelper.CreateThrowNotSupportedException() ignored error message parameter
- BrushFactoryManager.Create() null dereference on malformed brush XML
- UIExtender.Disable() log message said "Enable" instead of "Disable"
- PrefabComponent.PathForMovie() threw KeyNotFoundException on missing movie name
- Excluded 2 dead code files (HashHelpers.cs, StreamExtensions.cs)

Also identified (no fix needed): HarmonyLib BuildCategoryCache misleading condition, AccessTools silent null returns, PatchInfoSerialization BinaryFormatter thread safety, MonoMod dead platform paths (CoreCLR/Mono/ARM)

### Feature: Fork Harmony 2.4.2 into TAOM.Dependencies — Zero External Module Dependencies

Forked Harmony 2.4.2 (including MonoMod.Core, MonoMod.Utils, Mono.Cecil, Iced.Intel) source into `Dependencies/ThirdParty/Harmony/`. TAOM now ships fully self-contained with zero external module requirements — no Bannerlord.Harmony module needed.

- Decompiled fat `0Harmony.dll` (1,392 files, ~48K LOC) and compiled into `TAOM.Dependencies.dll`
- Fixed 900+ decompilation artifacts (missing backing fields, unsafe context, ref-assign scope, readonly struct, IntPtr null-coalescing, local function scoping)
- Added 3 safety features: `UnpatchAll(null)` guard, duplicate Harmony detection, load-order assertion
- Excluded `TaleWorlds.CampaignSystem.dll` reference from Dependencies (its `Helpers` namespace shadowed MonoMod's `Helpers` class)
- Updated `PatchProcessor.VersionInfo` to recognize `TAOM.Dependencies` assembly name
- Removed `Bannerlord.Harmony` from SubModule.xml dependencies and launch profiles
- Created `/harmony-update` skill for automated upstream merge workflow
- All 1055 tests pass, all 61 Harmony patches compile against forked types

### Feature: Internalize MCM and UIExtenderEx — Zero BUTR Dependencies

Removed 3 external BUTR library dependencies (MCM, ButterLib, UIExtenderEx). Harmony was the last remaining external dependency (now also forked -- see above).

**Phase 1: MCM Replacement**
- Replaced `AttributeGlobalSettings<TaomSettings>` with plain JSON singleton using Newtonsoft.Json
- 29 settings preserved with identical names/types/defaults, loaded from `ModuleData/configs/taom_settings.json`
- All 33 consumer callsites unchanged (`TaomSettings.Instance?.Property ?? default`)
- Eliminates ButterLib crash on Bannerlord 1.4.0 (`HotKeyManager.RegisterInitialContexts` signature change)
- 7 new tests (load, save, round-trip, defaults, malformed, partial, empty)

**Phase 2: UIExtenderEx Replacement**
- Built `Core/UI/` mixin infrastructure: `ViewModelMixinSupport`, `WrappedPropertyInfo`, `WrappedMethodInfo`, `WidgetPrefabPatcher`
- Gauntlet property/command injection via cloned `_propertiesAndMethods` dictionary with wrapped PropertyInfo/MethodInfo
- Harmony postfix on `WidgetPrefab.LoadFrom()` for prefab modifications (no transpiler needed)
- Rewrote 6 UI files: CareerSystem (button + mixin), SpecialResources (bar + mixin), TimeAcceleration (button + mixin)
- Deleted redundant `ViewModel_ExecuteCommand_CareerScreen_Patch.cs` (commands now injected via WrappedMethodInfo)

**Bugs caught by review process (5 total):**
- CRITICAL: `WidgetPrefab_LoadFrom_Patch` had no `HarmonyPatchCategory` and was never activated
- HIGH: `ExecuteOpenCareerScreen` fired twice (old postfix + new injected method)
- MEDIUM: `{ExtraFastForwardHint}` DataSource binding needed `WidgetAttributeValueTypeBindingPath`, not `Binding`
- MINOR: TimeAcceleration mixin missing `OnRefresh()` in constructor postfix
- MINOR: Bare exception catch in TaomSettings missing logging

### Feature: TAOM.Dependencies Pre-Native Module

Created a separate `TAOM.Dependencies` module that loads before Native to apply UIExtenderEx system patches at the correct time.

- Separate `.csproj` and `SubModule.xml` with `ModulesToLoadAfterThis` — load order: Harmony -> TAOM.Dependencies -> Native -> SandBox -> TAOM
- Sets `UIConfig.DoNotUseGeneratedPrefabs = true` before any prefabs load — the missing piece causing transparent banner backgrounds
- Triggers UIExtenderEx's static constructor which applies 5 system Harmony patches (BrushFactory, WidgetFactory, UIConfig, WidgetPrefab, ViewModel)
- Forked UIExtenderEx code (43 files) moved from `Main/ThirdParty/` to `Dependencies/ThirdParty/`
- TAOM's main `SubModule.cs` calls `UIExtender.Create/Register/Enable` after Dependencies loads

**Verified in-game:** Settlement nameplates render with colored diamond backgrounds; all custom brushes, widgets, and prefab overrides working without external UIExtenderEx.

### Fix: CanMakeAlliance Override for Racial Enmity

Added `CanMakeAlliance` override to `TaomAllianceModel` to enforce hard alliance blocks for permanently hostile factions. Previously only alliance scores were modified (via lore modifier), meaning extreme vanilla factors could theoretically override the penalty. Now uses `IDiplomacyService.IsAllianceAllowed()` as a hard gate.

### Tooling: Bannerlord 1.4.0 Decompilation & Compatibility System

Bannerlord updated to v1.4.0. Built reusable decompilation tooling and a full compatibility review system.

- `tools/Decompile-Bannerlord.ps1` — batch decompiles all 72 Bannerlord DLLs into organized folder structure (Campaign/, Core/, Engine/, etc.) with `--DryRun` support
- `tools/Diff-BannerlordAPI.ps1` — scans TAOM source for all 108 TaleWorlds types referenced, diffs only those files between version trees, produces structured change report
- `/compat-check` skill — orchestrates diff script + 3 parallel review agents (Harmony patches, GameModel overrides, reflection targets), compiles prioritized remediation report
- Decompiled v1.4.0 to `E:\Decompiled_Bannerlord\` (7,961 .cs files), backed up v1.3.15 to `E:\Decompiled_Bannerlord_v1.3.15\`
- New DLL in 1.4.0: `TaleWorlds.ServiceDiscovery.Client` (Network/)

### Fix: Bannerlord 1.4.0 API Compatibility (3 breaking changes)

Compatibility review found 37 changed types across 108 TAOM references. 3 compile-breaking changes fixed:

- `TaomAllianceModel.GetScoreOfStartingAlliance` — removed `IFaction evaluatingFaction` parameter (dropped in v1.4.0 base class)
- `TaomBattleRewardModel.CalculateRenownGain` — added `float renownMultiplierForWinnerSide` and `bool includeDescriptions` parameters (added in v1.4.0 base class)
- `SpecialResourcesBehavior.OnHideoutCompleted` — added `HideoutBattleEndState endState` parameter (event delegate changed in v1.4.0)

### Verified Safe (no changes needed)

- Mission.RegisterBlow signature unchanged — warg combat safe
- GuardsCampaignBehavior.PrepareGuardAgentDataFromGarrison intact — settlement guards safe
- All 25+ CharacterTableau/CharacterSpawner reflection fields verified intact
- AgentVisuals.Create 5-parameter overload confirmed
- TaomKingdomDecisionPermissionModel compatible with new bidirectional call-to-war checks
- CultureSettingService dynamic reflection targets all present
- 20+ Harmony patches confirmed safe with unchanged targets
- Full report: `docs/migration/compat-check-v1.4.0.md`

## 2026-04-08

### Feature: Named Companion System

XML-driven system for placing lore-significant characters as recruitable wanderer companions in specific settlements. 18 named companions across 7 cultures (Gondor, Erebor, Mirkwood, Rivendell, Rohan, Harad, Isengard).

- Uses `is_hero="true"` + `occupation="Wanderer"` — invisible to vanilla CompanionsCampaignBehavior, triggers vanilla recruitment dialog automatically
- Converted 18 LOTRAOM special wanderers to new system with race corrections (6 elves were missing `race="elf"`, 2 uruk_hai were missing race)
- Custom backstory dialog per companion (126 strings, 7 per companion)
- JSON config for spawn settlements, race, enable/disable per companion
- `NamedCompanionBehavior` places companions on new game, re-pins on load with recruited-companion guard
- Fixed Hero.Deserialize NullReferenceException — `faction="Faction.neutral"` required on Hero entries
- Fixed 6 deleted LOTRAOM Armory item IDs replaced with LOTRLOME_Armory equivalents
- 13 service tests + 7 config provider tests (20 total)

### Fix: Wanderer Race Attributes

Added correct `race=` XML attributes to 40 wanderer templates that were spawning as human regardless of culture.

- 30 elven wanderers (Rivendell/Mirkwood/Lothlorien): added `race="elf"`
- 10 Dol Guldur wanderers: fixed `race="orc"` to `race="dg_uruk"`, fixed `BodyProperty.fighter_empire` to `BodyProperty.fighter_dolguldur`
- Native `BasicCharacterObject.Deserialize()` reads `race=` from XML — no C# changes needed
- Existing `RacePersistenceService` handles save/load automatically

### Process: Entity State Matrix + Skip-Guard Exhaustion

New documentation standards from Codex Review #23 root cause analysis:

- `csharp-architecture.md`: Entity State Matrix required for any OnGameLoaded behavior that mutates Hero state
- `tests.md`: Skip-Guard Exhaustion — every guard clause needs a test for every entity state that should be skipped
- `REVIEW-GUIDE.md`: MISS-1 failure pattern (load-path mutation without state enumeration)
- `review-codex` skill: enhanced Known Suspects and verification with lifecycle state checks

### Feature: Per-Settlement Guard System

XML-driven guard customization that replaces vanilla's culture-only guard spawning with per-settlement troop pools. Guards in Minas Tirith are now Fountain Guards and Citadel Guards; Osgiliath has Dome Guards; Dol Amroth has Swan Guards, etc.

- Harmony prefix on `GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList` (private) injects settlement-specific guard characters via `SettlementGuardService` with settlement→clan→culture fallback chain
- Harmony prefix on `GuardsCampaignBehavior.GetSuitableSpear` (private static) provides per-culture spear item mapping, replacing the vanilla hardcode of battania=northern vs all-else=western
- XML config at `settlement_guards/settlement_guards_config.xml` with 14 Gondor settlements, per-spawn-point troop mapping, weighted random selection, and 16 culture spear mappings
- 27 tests (13 config provider + 14 service) covering fallback chain, weighted selection, spawn-point filtering, spear resolution
- Save compatible (no SyncData — guards spawn fresh every settlement entry)

### Process: Config ID Validation & Reflection Caching Rules

Root cause analysis from Codex review #22 identified 3 process gaps:

- Added "Config ID Cross-Reference (MANDATORY)" section to `.claude/rules/xml-data.md` with culture StringId mapping table (custom LOTR names vs XSLT engine IDs)
- Added reflection caching rule to `.claude/rules/harmony-patches.md` — `AccessTools.Method` must be cached in `Initialize()`, never in hot paths
- Added `ConfigIdValidationTests.cs` (11 tests) — validates all config culture IDs against known valid set, catches lore-vs-engine ID mistakes at test time

### Fix: CC Parent Equipment Rosters for shaghana & abanissa

Added missing Character Creation equipment rosters for `shaghana` and `abanissa` cultures. Without these, BL's parent narrative stage silently reverted the hero's culture to the vanilla default, breaking career auto-assignment.

- Added `shaghana` culture items (T1-T2 Harad: steppe raider aesthetic) and `abanissa` culture items (T3-T4 Harad: palace dynasty aesthetic) to `tools/generate_char_creation_equipment.py`
- Fixed script OUTPUT_PATH bug (was writing to wrong directory)
- Fixed 5 invalid Gondor item IDs that didn't exist in LOTRLOME_Armory (`sk_gondor_lossarnach_boots_a`, `cts_gondor_boot`, `gondor_solider_helm`, `citidel_guard_gloves`, `gond_spear2` → replaced with verified IDs)
- Regenerated `taom_char_creation_equipment.xml`: 550 → 660 rosters (55 per culture × 12 cultures)
- All item IDs validated against LOTRLOME_Armory and SandBoxCore

### Feature: Career System — Full Implementation (Phases 1-6)

Complete career/class progression system inspired by TOR_Core, adapted for LOTR. Mordor Warboss as pilot career.

**Phase 1 — Foundation:** Domain types (11 enums + data classes), `ICareerHeroAdapter` wrapping `Hero`, `ICareerDataService` for per-hero career state CRUD, `CareerPersistenceBehavior` with SyncData, DryIoc IoC wiring.

**Phase 2 — Registry & Logic:** XML config loading (`CareerConfigProvider`), career registry with eligibility checks and level-based tier gating (T2@10, T3@20), mutation calculator system (5 built-in calculators: flat, skill_scaling, level_scaling, replace, multiply), passive service with per-hero effect caching.

**Phase 3 — Campaign Integration:** `CareerCampaignBehavior` (auto-assigns first eligible career by culture on session launch, cache refresh, hero level-up notifications, career cleanup on death), `CareerCreationHandler` (CC integration — sets career + root choice), `CareerSwitchService` (culture-validated career switching with choice reset).

**Phase 4 — Battle & Abilities:** `CareerAbility` class (6 charge types: CooldownOnly, DamageDone, Kills, DamageTaken, Healed, Custom), `CareerAbilityService` (per-hero ability state), `MutationService` (clone + mutate ability templates via calculator registry), `CareerPerkMissionBehavior` (per-second tick, kill-based charge accumulation). Self-only abilities in v1.

**Phase 5 — GameModel Integration:** Career passives wired into 8 existing GameModels (PartySizeModel, PartyMoraleModel, BattleRewardModel, PartyWageModel, PartyTroopUpgradeModel, RaidModel, PartySpeedModel, SmithingModel) via `CareerPassiveHelper.ApplyFactor/ApplyFlat`. `ICareerHeroAdapterFactory` for GameModel boundary.

**Phase 6 — UI:** `CareerScreenVM` hierarchy (CareerScreenVM → CareerChoiceGroupObjectVM → CareerChoiceObjectVM), `GauntletCareerScreen` (GlobalLayer with GauntletLayer), `CharacterDeveloperCareerMixin` (UIExtenderEx [ViewModelMixin] for career button), `CareerButtonPrefab` (UIExtenderEx [PrefabExtension] injecting career button into CharacterDeveloper TopPanel), CareerScreen.xml prefab (two-panel layout with 3-tier choice tree).

**Pilot Data:** Mordor Warboss career with "Rally the Horde" ability (Kills charge type), 6 choice groups across 3 tiers (Brutality, Dominion, Scavenger, Warlord, Siegemaster, Tyrant), 31 total choices with passives covering 15+ PassiveEffectTypes.

**Architecture:** Plain C# classes (not PropertyObject), XML-driven career definitions, hybrid mutation system (XML params + C# calculator registry), UIExtenderEx for UI injection, adapter pattern at all sealed-type boundaries. 103 unit tests across 11 test files.

**Files:** `Main/Features/CareerSystem/` (28 files), `Main/Adapters/` (4 files), `Main/_Module/ModuleData/career_system/` (3 XML configs), `Main/_Module/GUI/Prefabs/CareerSystem/` (1 prefab), `TAOM.Tests/Features/CareerSystem/` (11 test files). 7 existing GameModels modified.

### Feature: Per-Kingdom Special Resource System (#73)

Data-driven per-faction resource system gating elite troop upgrades. All 18 kingdoms covered with 11 unique resources.

**Phase 1 — Core:** Earning (battle/raid/siege/prisoner/tournament/hideout/daily town income), spending (T6+ upgrade gating via Patch26, pending transaction with cancel support), map bar UI (UIExtenderEx MapInfoVM mixin + custom SpecialResourceSpriteWidget), SyncData persistence with composite `heroId:resourceId` keys. Culture-based fallback for kingdomless players.

**Phase 2 — Polish:** Troop desertion when resources hit 0 (10% per type daily, min 1), center-screen desertion warning, low-resource warning at <10% cap, green chat notifications for all earning events (battle/raid/siege/prisoner/tournament/hideout).

**Phase 3 — All Kingdoms:** 11 unique resources across 18 kingdoms. Shared balance for faction groups (War Spoils for Mordor/Isengard/Gundabad/Dol Guldur, Elven Wine for Rivendell/Lothlorien/Mirkwood, War Drums for Harad/Shaghana/Abanissa). XML schema supports many-to-one kingdom/culture mappings via nested `<Kingdom>` and `<Culture>` child elements. Same earning rates for all resources.

**Resources:** War Spoils (4 orc factions), Gems (Erebor), Caster (Gondor), Marks (Rohan), Elven Wine (3 elven factions), Lake Fish (Dale), War Drums (3 Harad factions), Tribal Relics (Khand), Dunlending Ale (Dunland), Plunder (Umbar), War Banners (Rhun).

**Architecture:** SpecialResourceService + StorageService + ConfigProvider (XML-driven). CampaignBehavior hooks 8 events. Harmony Patch26 (3 patches: InitializeUpgrades, AddCommand prefix, UpgradeTroop postfix). Comprehensive `[SpecRes]` logging throughout. 46 unit tests.

**Files:** `Main/Features/SpecialResources/` (18 files), `Main/_Module/ModuleData/special_resources/` (2 XML configs)

### Fix: Codex Adversarial Review — 6 Bugs Fixed (#72)

Codex adversarial review compared TAOM SpecialResources against TOR_Core CustomResources. 5 Codex findings confirmed, 1 ship-blocker found independently.

- **SHIP-BLOCKER:** `kingdom_id="mordor"` in XML config was wrong — runtime ID is `empire_s`. Feature was completely inert.
- **CRITICAL:** Upgrade spending was immediate (postfix), not transactional. Cancel party screen lost resources permanently. Added pending transaction pattern with Begin/Commit/Cancel session lifecycle.
- **HIGH:** Added `AddCommand` prefix to clamp upgrade count before execution (prevents free upgrades from stale UI).
- **HIGH:** `OnRaidCompleted` awarded resources for any AI raid, not just player raids. Added `IsPlayerMapEvent` guard.
- **MEDIUM:** Deleted dead `TaomSpecialResourceModel` (registered but never called).
- **LOW:** Added cap enforcement on save load (`ClampAll` in `RestoreData` path).
- **Tests:** 10 new tests (34 total) covering pending transactions, cancel recovery, budget clamping, and root-cause prevention.

## 2026-04-06

### Feature: LOTR-Themed Minor Factions

Replaced all 14 vanilla minor factions with lore-appropriate Middle-earth equivalents via XSLT overrides and localization strings. No C# changes — pure data work.

**Mercenary clans:** Ghilman → Serpent Guard (Harad), Legion of the Betrayed → The Grey Company (Dúnedain), Skolderbroda → Axemen of Erebor (Dwarves), Company of the Golden Boar → Corsair Blades (Umbar)

**Mafia factions:** Beni Zilal → The Blind Eye (Harad), Wolfskins → Variag Ravagers (Khand), Brotherhood of the Woods → Dunlending Reavers (Dunland), Hidden Hand → The Mouth's Servants (Mordor), Lake Rats → Wreckers of the Long Lake (Esgaroth)

**Sect:** Embers of the Flame → Cult of the Lidless Eye (Black Númenórean)

**Nomads:** Jawwal → The Sand-Riders (Harad), Karakhergit → The Wild Easterlings (Rhûn), Forest People → The Drúedain (Woses), Eleftheroi → The Beornings (Anduin vale)

**Settlement remaps:** Dunlending Reavers → castle_EN3 (Tûr Morva), Wild Easterlings → castle_RU10 (Nîrakh), Beornings → castle_M1 (Glad Thaw). Culture change: Dunlending Reavers from vlandia → empire (Dunland).

**Files:** `spclans.xslt` (14 templates), `taom_module_strings.xml` (42 strings)

## 2026-04-06

### Quality: Full Codebase Adversarial Review — 25/25 Features

Systematic Codex + Claude adversarial review of entire TAOM codebase. 16 reviews across 5 waves, prompt evolved v1→v6, accuracy improved 33%→81%.

**41 bugs found and 37 fixed across all features:**

- **CulturalFeats** — Forest speed terrain gate, caravan EffectBonus convention, null instance guard
- **BannerColorPersistence** — Fail-safe defaults (??true→??false), unique color RGB inversion, sentinel removal
- **TroopProgression** — Garrison IsGarrison gate, weighted healthy count, Rohan wage share
- **Diplomacy** — Missing kingdoms in alignment.json, Honor bypass for independent players, WarPhase session restore
- **FactionMap** — ModifyMenuCharacters side effect, stale banner sprite
- **CustomBattles** — Commander regex accepting alpha lord IDs
- **CharacterCreation** — Stale horse placeholder on culture switch
- **RaceAge** — comesOfAge=18 standardized, becomeOld set per-race
- **BattleBalance** — Config key fixes (rohan→vlandia, dol_guldur→dolguldur), test DataRows
- **HeroRace** — ActionSetCode BaseMonster/StringId preference, EyeHeight init retry
- **BannerInjection** — Kingdom ID exclusion for ruler banners
- **AdvancedCombat** — Bone tick decoupled from 2s grid update throttle
- **Warg** — Late-spawn BT attachment, FirstAttack flag consumption, team filter on rage targets
- **ShaderPrecompilation** — Abort latch reset on completion
- **TimeAcceleration** — Turbo restore before early returns
- **StartupResources** — Per-subsystem idempotent completion tracking
- **Infrastructure** — Kingdom color update without InitializeKingdom, MissionAdapter cache clear, FileLogger drain-before-dispose, AtmospherePersistence startup validation

Review process docs: `docs/reviews/REVIEW-GUIDE.md`, `docs/reviews/REVIEW-LOG.md`, `docs/reviews/REVIEW-PLAN.md`

---

## 2026-04-05

### Enhancement: BannerColorPersistence — Agent Visual & Conversation Color Coverage (PocColor Integration)

Extends BannerColorPersistence with deeper 3D battle scene and conversation color coverage, informed by PocColor Randomizer Revival (v1.3.4) analysis. Adds 5 new patches and an agent color store.

- **Agent Color Store** (`IAgentColorStore`/`AgentColorStore`) — `ConcurrentDictionary<int, ClanColorInfo>` keyed by agent index; registered per-agent in `Mission.SpawnAgent` Postfix + `Agent.EquipItemsFromSpawnEquipment` Prefix; cleared via `AgentColorStoreCleanupBehavior` on mission end
- **AgentVisuals.Create** (manual patch, View DLL) — disables `AddColorRandomness` when explicit clan colors are set, preventing engine HSB variation from overriding deterministic clan colors
- **MapConversationTableau** (2 manual patches, SandBox.View.dll) — `SpawnOpponentLeader` and `SpawnOpponentBodyguardCharacter` Postfixes inject clan colors into conversation scene `AgentVisualsData`
- **OrderOfBattleHeroItemVM.RefreshInformation** Postfix — rebuilds `CharacterCode` with clan colors (bypasses `CampaignUIHelper`)
- Config: 2 new flags `EnableAgentVisualColors`, `EnableConversationTableauColors` in `banner_color_config.json`
- 9 new tests (4 AgentColorStore + 2 service flag tests + 3 existing); 804 total passing

### Tooling: Codex Integration — Independent AI Verification

Added OpenAI Codex as an independent code reviewer alongside Claude Code via the `codex-plugin-cc` plugin. Codex operates with equivalent project knowledge (via `AGENTS.md`) but no shared session context, providing genuine second-opinion reviews.

- `.codex/config.toml` — Codex project config (o4-mini, MCP servers: filesystem, git, ilspy)
- `AGENTS.md` — Distilled project rules for Codex (architecture, ADRs, adapters, harmony patches, GameModels, XSLT, testing)
- `/codex-verify` skill — Dispatch background Codex verification while Claude continues building
- `/deep-review --codex` flag — Full review combining Codex pre-review + 4 Claude agents
- Updated `CLAUDE.md` with Codex integration section, enhanced completion workflow

### Feature: TimeAcceleration — Configurable Campaign Map Speed (BetterTime replacement)

Native implementation of BetterTime mod (Nexus #2849) functionality, removing the external dependency. Adds configurable campaign map time acceleration with three speed tiers and a visible Extra Fast-Forward button on the time bar via UIExtenderEx.

- **Space** → configurable fast-forward multiplier (default 4×), preserves current time mode
- **E key** → extra fast-forward multiplier (default 8×), forces fast-forward mode
- **Ctrl+Space** → turbo multiplier (default 16×), held; saves and restores prior speed/mode on release
- **Extra Fast-Forward button** — UIExtenderEx prefab patches insert a new button on the MapBar time panel; mixin data-binds `IsExtraFastForwardActive` for visual state via `MapTimeControlVM.RefreshValues()` hook
- MCM settings: 3 integer sliders (1–128) in "Time Acceleration" group
- Direct DLL reference to installed `Bannerlord.UIExtenderEx` module (no NuGet); `SubModule.xml` dependency declared with `LoadBeforeThis`
- `OnApplicationTick` drives per-frame input detection via `IMapInputAdapter` / `ITimeControlAdapter` abstractions
- ADR-007 compliant: adapter interfaces expose no TaleWorlds types; `InputKey` and `CampaignTimeControlMode` contained within adapter implementations
- 14 unit tests; 795 total passing

### Feature: BannerColorPersistence — UI color persistence, drift guard, BannerPaste

Comprehensive integration of banner color persistence into TAOM. Replaces the old postfix `Banner_TryGetBannerDataFromCode_Patch` with a superior transpiler that skips the `RemoveRange` call entirely, adds drift guard patches to prevent vanilla from overwriting lore-accurate banners mid-campaign, and ensures the player's custom clan colors persist across all UI screens (inventory, party, character sheet, encyclopedia, battle, etc.).

- **Patch15_BannerLayerLimit** — replaced postfix with IL transpiler on `Banner.TryGetBannerDataFromCode`; skips `RemoveRange` rather than re-parsing strings post-removal; configurable via `EnableLayerLimitTranspiler`
- **Patch24_BannerDriftGuard** — Prefix on private `Clan.UpdateBannerColorsAccordingToKingdom` returns false when enabled; Postfix on `Clan.UpdateBannerColor` syncs kingdom colors when the ruling clan updates (prevents WotR from resetting injected banners)
- **Patch23_BannerColorPersistence** — 11 postfix/transpiler patches ensuring `CharacterCode.Color1/2` reflect the player's clan colors across: `CampaignUIHelper`, `SandBoxUIHelper`, `SPInventoryVM`, `PartyVM`, `HeroViewModel`, `PartyCharacterVM`, `ClanPartyItemVM`, `Mission.SpawnAgent`, `CampaignSceneNotificationHelper`, `Banner.GetFirstIconColor`; BannerPaste (Ctrl+C/V in banner editor)
- **MobilePartyVisual** patch applied manually via reflection (private method in SandBox.View.dll)
- `BannerColorConfig` + `banner_color_config.json` — all 5 feature flags defaulting to `true`; `BannerColorService` is pure logic, no TW types; `IBannerHeroAdapter` wraps `CharacterObject`/`Hero`/`Clan` at the boundary
- Deleted `BannerLayerExpander.cs` and its Postfix patch (replaced by transpiler)
- 16 unit tests; 795 total passing

### Feature: SiegeDefense — Timed Settlement Defense Events

When a town belonging to the player's kingdom (or a kingdom the player is serving as mercenary) is besieged, a popup fires asking whether to help defend. Accepting starts a 3-day CampaignTime window; if the player arrives at the settlement while the siege is still active, they receive a relation boost and influence reward. The tracked settlement shows the native visual tracking circle on the campaign map.

- `CampaignEvents.OnSiegeEventStartedEvent` drives detection — no Harmony patches
- `IPlayerContextAdapter` wraps `Clan.PlayerClan` (sealed) to check kingdom membership and mercenary service dynamically; eliminates the previous static `WatchedFactionIds` config list
- `VisualTrackerManager.RegisterObject(settlement)` adds the native tracking circle on accept; `RemoveTrackedObject` cleans up on siege end, expiry, or reward grant
- Filter: towns only (not castles or villages), player must have a kingdom, duplicate-suppressed per settlement
- Config: `ModuleData/siege/siege_defense_config.json` — response window days, reward amounts, explicit `WatchedSettlementIds` override
- MCM: "Siege Defense" group — enable/disable toggle, response window (1–14 days)
- 17 unit tests; all existing 766 tests pass

## 2026-04-04

### Fix: MainMenuCustomizer — Restore save buttons, fix duplicate Pre-compile Shaders (#55)

- "Saved Games" and "Continue Campaign" were incorrectly hidden — restored; only "New Campaign" (StoryModeNewGame) is now hidden
- `OnBeforeInitialModuleScreenSetAsRoot` fires on every main menu visit (including returning from a game); `AddInitialStateOption("TaomPrecompileShaders")` was unguarded, causing duplicate "Pre-compile Shaders" entries — wrapped in `GetInitialStateOptionWithId` null-check
- Updated 5 tests to assert correct hide/keep/rename behaviour per option ID

### AI Strategic Intelligence — Phase 2: Border Proximity Harmony Patch

Adds `Patch22_ArmyTargeting` — Harmony Postfix on `AiMilitaryBehavior.CalculateDistanceScoreForBesieging` to fix the final blocker: if a target settlement has no topological fortification neighbors from the attacking faction, vanilla returns `bestDistanceScore = 0` before our `TaomTargetScoreModel` is ever called (score × 0 = 0).

- Postfix substitutes a configurable floor score (default 0.15) when `bestDistanceScore == 0` and the target is in the faction's priority list
- New MCM setting: "Border Proximity Floor" (0.0–1.0, default 0.15) — set to 0.0 to disable
- New `IArmyTargetingService.IsInPriorityList(factionId, settlementId)` method used by the patch
- Patch degrades gracefully if IoC not initialized (try/catch, returns without modifying score)
- 3 new tests; 766 total passing

### AI Strategic Intelligence — Evil Faction Aggression + Large Map Distance Compensation

Extends `TaomTargetScoreModel` with two new levers to fix evil faction passivity on the large TAOM map.

- **Strength gate bypass** (`FactionAggressionMultipliers`): inflates `ourStrength` before the vanilla `2× defender` hard gate fires — a multiplier of 2.0 lets a faction besiege at 1:1 parity. Mordor/Isengard = 2.0×, Gundabad/Dol Guldur = 1.75×, Rhun = 1.5×
- **Distance compensation** (`FactionDistanceRangeMultipliers`): post-multiplier for priority-list targets that vanilla would suppress via the `num21` distance curve (distant targets otherwise score ~11× lower than adjacent ones). Only applies to settlements already in the faction's priority list
- **MCM**: "Evil Faction Aggression Scale" (0.5–3.0) and "Long-Range Priority Boost Scale" (1.0–5.0) sliders allow global tuning at runtime
- Both features disabled via the existing "Enable AI Strategic Intelligence" toggle
- All config in `army_targeting.json` — hot-reloadable, no code change needed to tune
- O(1) hot path: all lookups pre-built at service construction, zero allocations per call
- 8 new tests; 763 total passing

## 2026-04-03

### Localization Infrastructure — Community Translation Support (#65)

Adds `Languages/` directory structure so non-English players can contribute TAOM translations without any code changes.

- **37 new XML files**: English anchor (`language_data.xml`), 12 per-language manifests (`FR/DE/RU/SP/PL/IT/TR/BR/JP/KO/CNs/CNt`), 24 stub translation files (2 per language)
- **1,773 strings are translatable**: 596 faction/culture/UI strings (`taom_str_*` keys in `taom_module_strings.xml`) + 1,177 wanderer backstory entries (`aom_*` keys in `taom_wanderer_strings.xml`)
- **Auto-discovered by engine**: no `SubModule.xml` or C# registration needed — Bannerlord scans `ModuleData/Languages/` at startup
- **English fallback**: non-English players with empty stubs see clean English text, no `???` strings
- **15 structural tests** in `LanguageDataXmlTests.cs` guard against malformed translator contributions
- Language IDs verified against `Native/ModuleData/Languages/` vanilla files
- See `docs/features/localization.md` for the full translator workflow

### AI Strategic Intelligence — Army Commitment + Faction Priority Lists

Adds `TaomTargetScoreModel` (`DefaultTargetScoreCalculatingModel` override) that prevents Besieger army AI from thrashing targets every 3 hours.

- **Commitment stickiness**: current target receives a configurable score multiplier (default 4×) so an alternative must be 4× better before the army diverts
- **Faction priority lists**: JSON config maps faction culture → ordered settlement list; earlier entries receive `MaxPriorityBoost` (default 3×) decaying linearly to 1× at the end; 9 factions configured: Mordor (EW), Isengard (V), Gundabad (M→S→E→R), Dol Guldur (`dolguldur`, L→S→M→E→R), Rhun/Easterlings (`khuzait`, E→S), Gondor (interleaved ES+A), Dunland (`empire`, V→EW), Dale (`sturgia`, RU→DG), Erebor (RU)
- Only applies to `Army.ArmyTypes.Besieger`; Raider and Defender armies remain fully reactive
- O(1) priority lookup via pre-built `Dictionary<string, Dictionary<string, int>>` at service construction (no hot-path `List.IndexOf`)
- MCM group "AI Strategic Intelligence": enable/disable toggle + Commitment Multiplier (1–10) + Priority List Boost (1–5)
- Targeting key uses **faction StringId** (`empire_s`, `empire_w`, `empire`) not culture StringId — Mordor/Gondor/Dunland all share `Culture.empire` so culture was ambiguous
- 12 new tests, 740 total passing



### Split Harad into Three Kingdoms — Harwan, Shaghâna, Âbanissa (#63)

Split the single Harad faction (all on vanilla `aserai`) into three independent kingdoms following the Umbar pattern. Harwan stays on `Culture.aserai`/`Kingdom.aserai` with its 9 original clans; Shaghâna and Âbanissa are fully independent kingdoms.

- Verified `spclans.xslt` already carries only Harwan's 9 clans — no trimming needed
- Added `Kingdom.shaghana` and `Kingdom.abanissa` to `TAOM_spkingdoms.xml` with titles (Taskralan/Châjaphân), diplomacy, and owner lords
- Added `Culture.shaghana` and `Culture.abanissa` to `taom_spcultures.xml` with NPC notary references and harad troop inheritance
- Added 17 clan entries to `characters/clans.xml` (9 Shaghâna: Ezarkia–Acammes; 8 Âbanissa: "House of" dynasties)
- Added 17 lord hero entries to `characters/lords.xml` (lord_SH1_1–SH9_1, lord_AB1_1–AB8_1)
- Created `characters/npcs_shaghana.xml` — 26 notable NPCs (merchants, preachers, artisans, gang leaders, rural notables, headmen)
- Created `characters/npcs_abanissa.xml` — 26 notable NPCs with Far Harad/dynastic house flavor
- Registered both NPC files in `SubModule.xml`
- Extended `VolunteerRecruitmentService` with shaghana/abanissa culture fallback pools and all 17 clan mappings (harad_levy/harad_noble, 7/3 weights)
- Added 21 new tests for culture fallback and all 17 clan IDs — 727 tests passing
- Reassigned settlements across A6–A14 region and FH1–FH9 to new culture/clan owners in `TAOM_Map/ModuleData/settlements.xml` (castle_U5 Zamarzîr intentionally left as `clan_aserai_14`/`Culture.umbar` — Umbar border holding)
- Added all module strings: 17 lord names, 17 clan names, 52 NPC display names, kingdom/culture descriptors to `taom_module_strings.xml`
- Added `shaghana` and `abanissa` entries to `charactercreation/cultures.json` (starting settlements: town_A6 Zajâna / town_A14 Damudûr)

### Fix: CulturalFeats + TroopProgression Models — Remove Static TextObject Field Initializers (#62)

- All 13 GameModel overrides used `private static readonly TextObject CultureText = GameTexts.FindText("str_culture")`, which compiles to an implicit `.cctor()` (static constructor). Replaced with `private static TextObject? _cultureText; private static TextObject CultureText => _cultureText ??= GameTexts.FindText("str_culture");` — no `.cctor()` generated, cached after first call, no per-tick overhead
- Affected: `TaomBattleRewardModel`, `TaomBuildingConstructionModel`, `TaomClanFinanceModel`, `TaomFoodConsumptionModel`, `TaomPartyMoraleModel`, `TaomPartySizeModel`, `TaomPartySpeedModel`, `TaomPartyTroopUpgradeModel`, `TaomRaidModel`, `TaomSettlementLoyaltyModel`, `TaomSettlementProsperityModel`, `TaomVillageProductionModel`, `TaomPartyWageModel`
- **Note:** This does NOT fix the BannerlordTogether startup crash. Root cause analysis confirmed the crash is in vanilla `DefaultClanFinanceModel..cctor()` (16 `Game.Current.GameTextManager.FindText(...)` static initializers), triggered by BT's `Harmony.PatchAll()` calling `RuntimeHelpers.PrepareMethod` during `OnSubModuleLoad` when `Game.Current` is still null. Fix requires BT to defer patching to a later hook. See `docs/features/bannerlord-together-compat.md`.

### Fix: Harad Split — Restore Original Clan Banner Keys + Add Missing Files

Follow-up fixes to the Shaghâna/Âbanissa split:

- **Banner keys**: All 17 new clan entries (clan_shaghana_1–9, clan_abanissa_1–8) had placeholder banner keys. Restored original keys copied from their source clans (clan_aserai_10–26) which held the real designed banners
- **Education templates**: Added 6 `child_education_templates_stage_2_page_0_branch_{0-5}_{culture}` entries each for `Culture.shaghana` and `Culture.abanissa` to `taom_education_character_templates.xml` — without these the character creation education stage crashes for players starting as these cultures
- **Removed duplicate clans**: Deleted `clan_aserai_10–26` from `clans.xml`, `lord_A10_1–A26_1` from `heroes.xml` and `lords.xml`. These old aserai entries were never removed when the new `clan_shaghana_*` / `clan_abanissa_*` entries were created, causing all 26 clans to appear under Harwan instead of 9
- **Added `docs/features/kingdom-creation.md`**: Authoritative guide covering all 13 required files, naming conventions, filing order, inheritance table, SubModule.xml registration, and 3 known crash scenarios (including the heroes.xml omission and banner key placeholder pitfall)

## 2026-04-02

### Compat: BannerlordTogether Passive Compatibility Pass

- Added `[HarmonyPriority(Priority.High)]` to `DeclareWarAction_ApplyInternal_Patch` and `MakePeaceAction_ApplyInternal_Patch` so TAOM's racial enmity and War of the Ring constraints validate before BT syncs the action to clients
- Confirmed TAOM runs on Bannerlord 1.3.15 (BT's minimum requirement) with no observed failures
- Added `docs/features/bannerlord-together-compat.md` — setup guide, known limitations, conflict analysis, testing checklist
- Updated `docs/migration/TRACKING.md` with 1.3.15 compatibility status note

### Fix: ShaderPrecompilation — Stuck-Shader Auto-Abort + Countdown UI (#57)

- A shader stuck at "1 remaining" could block indefinitely with no way to exit
- After 30s stuck at the same count: shows "stuck Xs (aborting in Ys)" countdown in the loading screen text
- After 120s stuck: calls `MBGameManager.EndGame()` to abort and return to the main menu automatically
- `TaomShaderGameManager.IsShaderBattleActive` flag scopes the timeout to TAOM shader battles only
- Note: TaleWorlds exposes no API for which shader is stuck — only the count is available

### Feat: Named Hero Civilian Equipment — Sauron, Witch-King, Nazgul, Khamul, Nazgul V1, Glorfindel (#61)

- Added dedicated `*_civ_equipment` roster entries for all named Mordor and Rivendell heroes so they appear in their unique armor in civilian/settlement scenes
- `sauron_civ_equipment`, `witchking_civ_equipment`, `nazgul_civ_equipment`, `khamul_civ_equipment`, `nazgul_v1_civ_equipment` added to `taom_equipment_sets_mordor.xml`
- `glorfindel_civ_equipment` added to `taom_equipment_sets_rivendell.xml`
- Updated `lords.xslt` (10 entries) and `lords.xml` (Glorfindel) to reference the new civ roster IDs instead of generic `mordor_civ_template_default_*`/`rivendell_civ_template_default_*`

### Feat: All-Culture Lords Civilian Equipment Pass — Lords Always in Battle Gear (#59)

- Systematically replaced all `*_civ_template_*` lord civilian templates across 13 cultures with exact mirrors of their `*_bat_template_medium_*` battle loadouts
- Cultures updated: Umbar, Dunland, Rohan, Lothlorien, Dale, Harad, Isengard, Dol Guldur, Gundabad, Mordor, Rhun, Mirkwood, Rivendell
- Lords now appear in full armor (weapons, helm, body, cape, gloves, greaves, horse/mount) in both battle and town/settlement scenes
- Named hero civilian outfits preserved: Theoden, Thranduil, Legolas
- Erebor and Gondor were completed in prior sessions (#56, #58)

### Fix: BannerInjection — Fire Once Per Game Start/Load Instead of Every Session Launch

- `BannerInjectionBehavior` was subscribed to `OnSessionLaunchedEvent`, which fires on every return from a battle or mission to the campaign map — causing the full kingdom/clan loop to run (and log) after every fight
- Swapped to `OnNewGameCreatedEvent` + `OnGameLoadedEvent` so injection fires exactly once: on new game creation and on save load
- No behavioral change for players — banners are campaign-level data that persist across sessions; re-injection after battles was unnecessary

### Feat: ShaderPrecompilation — Pre-compile Shaders at Main Menu (#57)

- Mid-game stutter when encountering new armor/mesh combinations (first-time shader compilation) eliminated by pre-warming the cache before campaign start
- New **"Pre-compile Shaders"** button on the main menu (order index 100) launches a hidden custom battle containing all TAOM characters from all 13 non-bandit cultures
- Bannerlord's renderer compiles all unique material shaders as it renders each character; loading screen shows "Compiling shaders... N remaining" with live countdown
- Progress text updated only when count changes — avoids per-frame string allocation in `LoadingWindowViewModel.Update()` postfix
- `Patch21_ShaderPrecompilation` / `TaomShaderGameManager` / `ShaderPrecompilationService`; all 14 v1.3.12 APIs verified via decompilation

### Feat: Gondor Equipment Pass — Lords in Battle Gear + Noble Coat/Jerkin Variety (#58)

- Gondor lords now wear full battle armor in civilian scenes — `gondor_civ_template_default_a/b/c/d/e` updated to mirror their `gondor_bat_template_medium_*` counterparts (weapons, helm, chest, cape, gloves, greaves, horse)
- Boromir (`boromir_civ_equipment`) and Faramir (`faramir_civ_equipment`) civilian outfits unchanged (intentional character-specific looks)
- 8 new civilian items added to LOTRLOME_Armory (`gondor_noble_coat_a/b`, `gondor_noble_coat_a/b_slim`, `gondor_noble_jerkin_a/b`, `gondor_noble_jerkin_a/b_slim`) — light cloth stats, `Civilian="true"` flag
- All Gondor civilian NPCs (craftsmen, tavern, services, beggars, dancers, merchants, notables, headmen) switched from `ithilien_jerkin_*` / `boromir_jerkin` to new noble coats/jerkins and `lossarnach_coat`
- Female-coded NPCs (`tavern_wench`, `female_beggar`, `female_dancer`, `townswoman_*`, `village_woman_*`) use slim variants
- Armorer and ransom broker retain chainmail second roster (appropriate for role); gang bodyguard chainmail kept
- All 26 notables spread across the full item range for visual variety

### Feat: Erebor Equipment Pass — Lords in Battle Gear + Full Dress/Tunic Variety (#56)

- Dwarf lords now wear full battle armor in civilian scenes (town/settlement) — `erebor_civ_template_default_a/b/c/d/e` updated to mirror their `erebor_bat_template_medium_*` counterparts (weapons, helm, chest, cape, bracers, greaves)
- Male-coded civilian NPCs (townsman, blacksmith, weaponsmith, barber, beggar and family variants) switched from dresses to `tunic_normal_a/b`
- Female-coded NPCs (townswoman, village_woman, female_beggar, female_dancer, tavern_wench and family variants) spread across dresses `e–i`
- Neutral NPCs (villager, teenagers, musician, tavernkeeper, merchant) given two civilian roster entries each (dress + tunic) for random variety
- Notable preachers (`_5/_6/_7`) and gang leaders (`_12/_13`) updated to dresses `e–i`
- Rural notables (`_21/_22`) and headmen (`_2/_3`) upgraded to `tunic_noble_a/b/c` to reflect their status
- All 9 dresses (a–i) and both tunics (a–b) now in use; noble tunics (a–c) introduced for notable NPCs

### Feat: MainMenuCustomizer — Hide Campaign, Rename Sandbox (#55)

- Bannerlord main screen exposed "Campaign" (vanilla story mode) alongside "Sandbox" — misleading for a total conversion mod
- `OnBeforeInitialModuleScreenSetAsRoot` override calls `Module.CurrentModule.OverrideInitialStateOption` twice: sets `isHidden: () => true` on `campaign_single_player`, renames `sandbox_single_player` to "Enter The Age Of Men"
- Original action, disabled-state delegates, and order index preserved on both overrides
- `IModuleMenuAdapter` / `ModuleMenuAdapter` wraps `Module.CurrentModule` static API; `MainMenuCustomizerService` holds no TaleWorlds references

## 2026-03-31

### Feat: TaomTournamentModel — Increased Tournament Frequency (#52)

- Vanilla bucketed each town into 1 of 3 week-slots per season, suppressing tournaments to ~1 per 1–3 seasons
- `GetTournamentStartChance`: removed week-gate, replaced linear formula with diminishing-returns step curve tuned for LOTR campaigns where lords are rarely at peace (1 lord=45%, 2=75%, 3=90%, 4+=100%)
- `GetTournamentEndChance`: extended grace period from 10 → 20 days, slowed ramp from 0.05 → 0.033/day — tournaments stay active longer
- All tuning values extracted as `internal const` for testability and future MCM exposure

### Feat: TaomTournamentModel — Culture-Specific Tournament Prize Items (#52)

- `DefaultTournamentModel.GetEliteRewardItems` returned a hardcoded list of 31 vanilla items — none exist in TAOM; elite prizes were silently empty
- `GetRegularRewardItems` filtered by gold value range, missing most LOTRLOME_Armory items
- Both methods now dynamically scan `Items.All` filtered by settlement culture + `item.Tierf` threshold (regular: 2–4, elite: 4+)
- Cultures without armory entries (lothlorien, dale, khand) fall back to `base` gracefully
- Called once per tournament win — not a hot path; no performance impact

### Feat: TaomTournamentModel — Per-Participant Culture Armor (#52)

- `DefaultTournamentModel.GetParticipantArmor` used settlement culture for ALL participants (heroes, lords, filler troops) — human lords in Erebor tournaments received dwarf chainmail on human skeletons
- Root cause (confirmed via decompilation): vanilla ignores the `participant` parameter entirely; no race/culture check exists anywhere in the tournament pipeline
- New `TaomTournamentModel : DefaultTournamentModel` overrides `GetParticipantArmor` to try participant's own culture first, then falls back to vanilla (settlement culture → empire)
- Data-driven: each culture's `gear_practice_dummy_*` already has skeleton-appropriate gear; no explicit race mapping needed
- New files: `Main/Features/Arena/Models/TaomTournamentModel.cs`, `TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs`

### Fix: Arena Practice Crash — All 13 TAOM Cultures (#49)

- `ArenaPracticeFightMissionController.AddRandomWeapons` crashed with `ArgumentOutOfRangeException` for all TAOM custom culture arenas
- Root cause: all 39 `weapon_practice_stage_{1-3}_{culture}` EquipmentRosters were tagged `civilian="true"` → `BattleEquipments` returned empty list → `RandomInt(0)` crashed
- Fix: removed `civilian="true"` from all 39 rosters, added tier-appropriate weapons (Stage 1: T2, Stage 2: T3, Stage 3: T4 swords) to `Item0` slot
- Affected files: `npcs_{erebor,gondor,mordor,rivendell,mirkwood,lothlorien,isengard,gundabad,dolguldur,umbar,rohan,harad,rhun}.xml`

### Fix: Dwarf Character Creation — 3 Cascading Crashes (#50)

- **Crash 1 (NRE):** `GetYouthMenuNarrativeMenuCharacterArgs` unconditionally reads `DefaultEquipment[Horse].Item.StringId` — crashed when Erebor CC rosters had no horse
- **Crash 2 (ArgumentNullException):** `SpawnNonHumanNarrativeMenuCharacter` called `MBObjectManager.GetObject<T>(null)` — horse scene character had uninitialized IDs when horse NarrativeMenuCharacterArgs was skipped
- **Lore fix:** Removed `Horse`/`HorseHarness` slots from all 16 `player_char_creation_erebor_*` non-civilian EquipmentSets
- **Patch20_NarrativeHorseGuard:** Two new Harmony patches in `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`
  - Prefix on `GetYouthMenuNarrativeMenuCharacterArgs`: skips horse entry when `DefaultEquipment[Horse].Item == null`
  - Finalizer on `SpawnNonHumanNarrativeMenuCharacter`: suppresses `ArgumentNullException("key")` from null horse item ID
- Pattern is data-driven — any future no-mount culture works automatically by omitting horse slots from CC equipment

### Fix: Arena Practice Clothes Crash + Culture-Specific Clothing (#51)

- `ArenaPracticeFightMissionController.AddRandomClothes` crashed (NRE) for all TAOM custom culture arenas
- Root cause: all 13 `gear_practice_dummy_{culture}` characters had only `civilian="true"` EquipmentRosters → `RandomBattleEquipment` returned null → null dereference
- Fix: removed `civilian="true"` from all 13 characters, updated item IDs to be culture-appropriate (dwarves use tunic not dress, mirkwood/lothlorien use rivendell items, dale uses sturgia, khand uses dunland armory, dunland/rhun updated from vanilla to TAOM armory items)
- Added missing `gear_practice_dummy_lothlorien` entry (was absent — fell back to empire clothes)
- Affected files: `npcs_{erebor,gondor,isengard,mordor,rivendell,dolguldur,mirkwood,gundabad,harad,dunland,rhun,dale,khand,lothlorien}.xml`

### Fix: TaomPartyHealingModel NRE in Arena Practice (#52)

- `GetSurvivalChance` crashed (NRE at line 34) when an agent died during arena practice
- Root cause: `party` parameter is null in arena practice context (no campaign party exists); line `party.Owner?.Culture ?? party.Culture` dereferences null `party`
- Fix: added `if (party == null) return vanillaSurvival;` guard before config/culture access in `TaomPartyHealingModel.cs`
- Vanilla base model handles null `party` gracefully; cultural survival bonuses simply don't apply in arena context

### Fix: Dwarf Character Creation — Remaining Stage NREs (#50 continued)

- **Root cause (full picture):** `CharacterCreationCampaignBehavior` has 6 `Get*NarrativeMenuCharacterArgs` methods; 3 of them unconditionally dereference `DefaultEquipment[Horse].Item.StringId`. Each fires on a separate CC screen click, producing a new NRE each time.
- **Adult stage** (`GetAdultMenuNarrativeMenuCharacterArgs` line 2819): added Prefix returning `"player_adulthood_character"` (age 20)
- **Age selection stage** (`GetAgeSelectionMenuNarrativeMenuCharacterArgs` line 3298): added Prefix returning `"player_age_selection_character"` (age = `StartingAge`)
- `Patch20_NarrativeHorseGuard` now has 4 patches (3 Prefixes + 1 Finalizer) covering all crash sites — decompilation confirmed no further horse-reading methods exist in the class

## 2026-03-28

### awesome-claude-skills Cherry-Pick: ADR Scaffolding & Atomic Commit Workflow

Reviewed 13,152 skills from the awesome-claude-skills marketplace. 45 of 47 filtered candidates were skipped (wrong language, wrong domain, or already covered). Two genuine gaps filled:

- **New skill:** `/new-adr [name]` — auto-numbers from existing `docs/adrs/`, reads `000-template.md` for exact format, pre-fills Context from `git log --oneline -10` + CHANGELOG, writes `docs/adrs/NNN-name.md`, reminds to fill Decision/Consequences/Examples and update README.md
- **New skill:** `/commit-split` — inspects staged + unstaged + untracked files, groups by TAOM-specific heuristics (feat/test/data/docs/chore), confirms grouping with user, then executes each atomic commit with 50/72-rule messages, optional trailers, and staged diff review per commit
- **Updated CLAUDE.md:** Skills table updated with both new skills

### oh-my-claudecode Cherry-Pick: Researcher Safety, Deslop, Deep-Review Adversarial Mode, Commit Trailers

Reviewed the oh-my-claudecode repository (19 agents, 32 skills, MCP bridge). Most components require the OMC MCP bridge and were skipped. Cherry-picked 5 zero-infrastructure patterns adapted for TAOM's C#/.NET stack.

- **Updated agent:** `taleworlds-researcher.md` — added `disallowedTools: [Write, Edit, NotebookEdit]` so the researcher can never accidentally modify code; added decompilation fallback chain (ILSpy MCP → ilspycmd CLI → grep) with 3-failure circuit breaker
- **New skill:** `/deslop [path]` — regression-safe C# AI-slop cleanup: requires green tests first, deletion-first ordering (dead code → comments → null guards → inline single-use methods → extract duplicates), TAOM-specific slop patterns table
- **Updated skill:** `/deep-review` — added Step 2b adversarial escalation: when Agent 1 finds a CRITICAL adapter-pattern violation, a 5th agent launches in adversarial mode to confirm the violation, map blast radius, and produce minimum surgical fix plan
- **Updated CLAUDE.md:** `/deep-review` added to Critical Rules table (mandatory before every C# commit); commit trailers convention added (`Constraint:`, `Rejected:`, `Not-tested:`, `Research:`, `Save-compat:`)
- **Fixed:** `deep-review/SKILL.md` frontmatter `argument-hint` YAML quoting

## 2026-03-27

### Feature: Custom Battles

- TAOM Custom Battle support: all TAOM cultures, commanders, and troops available in Custom Battle mode
- 5 Harmony patches (Patch19_CustomBattles) replacing vanilla factions/commanders/troops with TAOM content
- Dynamic faction loading from ObjectManager (cultures with settlements, non-bandit)
- Dynamic commander loading with filtering (excludes companions, children, tutorial, vanilla commanders)
- Formation-to-troop mapping using culture militia/elite troop definitions
- Team-fix MissionBehavior preventing friendly fire in custom battles and custom sieges
- Custom battle GUI prefabs (already existed) now backed by service layer
- New IObjectManagerAdapter for testable ObjectManager access
- 29 new tests covering service logic and hook behavior

### Fix: Custom Battle NRE crash on screen init

- Root cause: lord characters and cultures were only registered for Campaign game type, not CustomGame
- CustomBattleSideVM.OnCharacterSelection crashed with NullReferenceException when Characters list was empty
- Fix: registered SPCultures (XSLT + custom), lords (XSLT + TAOM) for CustomGame/EditorGame in SubModule.xml
- Added safety fallback in Characters patch — falls back to vanilla if TAOM commander list is empty
- Fixed commander filtering: added "wanderer" and "notable" to exclusion list (wanderers/notables have is_hero=true but aren't lords)
- Fixed faction selector UI: `CustomBattleFactionSelectionVM` isn't a `SelectorVM`, so the dropdown couldn't work. Created `TaomFactionSelectionVM` subclass with `ExecuteSelectNextFaction`/`ExecuteSelectPreviousFaction` commands, injected via Harmony postfix on `CustomBattleSideVM` constructor. UI now uses arrow buttons matching the character selector pattern.

### Feature: Custom Culture Feats (Expanded)

- **59 custom feats** across 11 cultures (10 custom + Rohan XSLT), up from initial 30
- Party size feats: Mordor/Gundabad +30%, Dol Guldur +25%, Isengard +20%, Gondor +10%
- Food consumption feats: Rivendell/Mirkwood/Lothlorien -15%, Dol Guldur +10%
- Settlement loyalty feats: Gondor/Erebor +1/day, Lothlorien/Rivendell/Rohan +0.5/day
- Party morale feats: Gondor/Rohan/Erebor +5, Mirkwood/Lothlorien +3
- Smithing energy cost feats: Erebor -30%, Isengard -20%
- Tariff income feat: Umbar +15%
- Raid damage feats: Mordor/Gundabad +25%, Isengard +20%
- Rohan custom C# feats (replacing vanilla Vlandia): -15% mounted cost/wage, -10% speed when >50% infantry
- Erebor production feat changed from +30% animal-only to +10% ALL production
- Isengard construction speed flipped from -15% penalty to +15% bonus (industrial might)
- 7 new GameModel overrides: TaomPartySizeModel, TaomFoodConsumptionModel, TaomSettlementLoyaltyModel, TaomPartyMoraleModel, TaomSmithingModel, TaomClanFinanceModel, TaomRaidModel
- Feats registered via Harmony postfix on `Campaign.InitializeDefaultCampaignObjects()` (Patch18_CulturalFeats)
- 16 total GameModel overrides consuming feats
- Extended TaomPartyWageModel with Rohan mounted wage reduction (scaled by mounted troop fraction)
- Extended TaomPartySpeedModel with Rohan infantry speed penalty
- XSLT updated: Dunland uses Battanian feats, Rohan uses custom C# feats
- 64 tests verifying feat registration structure and property correctness

### Enhancement: Diplomacy & Alliance System Logging

- Added diagnostic logging to diplomacy enforcement hooks (`AllianceActionHook`, `PeaceActionHook`)
- Added initialization logging to `DiplomacyBehavior` and `WarOfTheRingBehavior`
- Added null-hook warnings to all 3 diplomacy Harmony patches for debugging initialization issues
- LogInfo for blocked actions (alliance end, war declaration, peace), LogDebug for allowed actions

### Fix: Warg Combat System — BT Runtime Failures

- **Bug:** Wargs never attacked in combat — 10x `ArgumentException` in `BehaviorTrees.dll`
- **Root cause 1:** `OnBehaviorInitialize` is never called for behaviors added during `SubModule.OnMissionBehaviorInitialize` in Bannerlord 1.3.12. `BTRegister.RegisterClass("WargTree")` never ran, so every `BehaviorTreeAgentComponent` failed to build its tree.
- **Fix:** Moved initialization from `OnBehaviorInitialize` to first `OnMissionTick` call via `_initialized` flag
- **Root cause 2:** `WargBehaviorTree` constructor line 30 (`Rider.GetValue().Formation`) threw NRE when warg had no rider at tree construction time
- **Fix:** Changed to `agent.RiderAgent?.Formation` (null-safe)
- **Safety net:** Added manual `comp.OnTickAsAI(dt)` loop in case engine doesn't call `OnTickAsAI` for mount agents
- **Verified:** 10 Dol Guldur Fell Warg-Riders in combat — all trees build successfully, wargs attack

## 2026-03-26

### Feature: Warg Combat System — Autonomous Warg AI (#44)

- **New feature:** Wargs are now autonomous combat agents with their own behavior tree AI, attacking enemies independently and entering rage mode when damaged
- **Ported from:** LOTRAOM's warg combat system, adapted for Bannerlord 1.3.12 APIs
- **Rage mode:** 10% chance on >10 damage — warg takes over control for 2-3 attacks, then returns to rider
- **Architecture:** BehaviorTree framework (pre-compiled DLLs) + SpatialGrid spatial partitioning + bone-based collision detection + reflection-based Mission.RegisterBlow
- **New adapters:** IAgentAdapter/AgentAdapter, IMissionAdapterFactory (mission-scope agent wrapping)
- **New services:** IWargAttackService (damage calc), IBoneCollisionService, ISpatialGridDebugService
- **Dependencies:** Alliance.Wargs (XML data), BehaviorTrees.dll, BehaviorTreeWrapper.dll
- **1.3.12 fixes:** MBAgentVisuals (renamed), WeakGameEntity (RegisterBlow reflection), OnMainAgentChangedDelegate signature, CombatLogData constructor, AIScriptedFrameFlags qualification
- **Files:** ~50 new C# files across Adapters/, Features/AdvancedCombat/, Features/Warg/
- **Cultures affected:** Gundabad, Dol Guldur, Isengard (7 warg-mounted troops)

### Feature: Troop Weight System — Elite Unit Party Capacity

- **New feature:** Elite/supernatural units consume more party capacity, preventing armies of pure elite troops
- **Weights:** Cave trolls (4x), legendary elf commanders (3x), all elves/warg riders/elite guards (2x), standard troops (1x default)
- **Mechanism:** Harmony postfixes on `PartyBase.NumberOfAllMembers`, `NumberOfRegularMembers` + 2 UI patches for recruitment and party screens
- **Config:** `ModuleData/TroopWeights/troop_weights.xml` — data-driven weight assignments for ~80 troop types across all cultures
- **MCM toggle:** "Enable Troop Weight" in Troop Weight settings group (enabled by default)
- **Architecture:** `ITroopWeightService` + `TroopWeightXmlLoader` + 4 hook implementations + 4 Harmony patches (`Patch17_TroopWeight`)
- **Ported from:** LOTRAOM's TroopWeight feature, adapted to TAOM conventions (static Initialize pattern, IPathService, simplified caching)
- **Stability fix:** Removed TroopRoster-level patches (fired on every roster in the game, caused IndexOutOfRange spam + freeze during loading). PartyBase-level patches are sufficient.
- **Fix:** Null-safe MCM guard prevents NRE when MCM is not loaded

### Feature: Atmosphere Persistence for Forced-Atmosphere Scenes

- **New feature:** Scenes with "forceatmo" in their name bypass campaign weather, preserving scene-embedded atmosphere
- **Ported from:** LOTRAOM's `AtmospherePersistence` feature (originally from The Old Realms mod)
- **1.3 refactor:** Replaced fragile string-based patch (`ScriptingInterfaceOfIMBMission`) with type-safe `Mission.Initialize()` prefix
- **Architecture:** Static `AtmosphereOverrideService` + thin Harmony patch (`Patch16_AtmospherePersistence`), follows `WeatherBoundsGuard` pattern
- **Tests:** 7 new tests for scene name detection (null, empty, case-insensitive, position variants)

### Feature: Startup Resources — Culture-Based Gold & Influence Distribution

- **New feature:** Lords receive startup gold and clans receive startup influence at new game creation, configured per culture via XML
- **Config:** `ModuleData/startup_resources/startup_resources_config.xml` — data-driven, all 15 cultures with gold (500K–6M) and influence (50–2000)
- **Architecture:** `StartupResourcesBehavior` fires at `OnNewGameCreatedPartialFollowUpEvent` index 1, delegates to `StartupGoldService` and `StartupInfluenceService`
- **Adapters:** `IStartupHeroAdapter`, `IGoldGiftAdapter`, `IClanStartupAdapter` wrap TaleWorlds sealed types
- **Tests:** 22 new tests covering config parsing, gold distribution, influence distribution, and behavior trigger logic
- **Ported from:** LOTRAOM's `StartupFunds` and `StartingInfluence` features

### Fix: NullReferenceException on Minor Faction Hero Spawning

- **Fixed:** Game crash (`NullReferenceException` at `CharacterObject.get_StealthEquipments()`) when spawning minor faction heroes (e.g. Ghilman) on new campaign start
- **Root cause:** Bannerlord v1.3 added `default_stealth_equipment_roster` attribute to cultures; the 4 XSLT-transformed cultures (Dunland, Harad, Rohan, Rhun) were missing it while the 10 custom cultures in `taom_spcultures.xml` had it
- **Fix:** Explicitly set `default_stealth_equipment_roster` in all 4 XSLT culture templates in `spcultures.xslt`

### Everything-Claude-Code Cherry-Pick: Developer Workflow Hooks & Skills

Reviewed the everything-claude-code repository (125+ skills, 28 agents, 60 commands) and adapted the most valuable patterns for TAOM's C#/Bannerlord workflow.

- **New skill:** `/build-fix [error]` — incremental dotnet build error fixer with C#/Bannerlord-specific error patterns (CS0246, CS0115, CS0234, etc.), one error at a time, minimal diffs
- **New skill:** `/verify [quick|full]` — comprehensive build + test + git verification with structured pass/fail report
- **New hook:** `config-protection.sh` (PreToolUse Edit|Write) — blocks AI edits to CLAUDE.md, Directory.Build.props, settings.json, and ADR files without explicit user request
- **New hook:** `suggest-compact.sh` (PreToolUse *) — counts tool calls per session, suggests `/compact` at 50 calls then every 25 after
- **New hook:** `mcp-health-check.sh` (PreToolUse mcp__*) — blocks MCP tool calls to servers marked unhealthy in last 60 seconds
- **New hook:** `mcp-health-mark.sh` (PostToolUseFailure mcp__*) — marks MCP server as unhealthy after failed tool call, 60s backoff
- **Updated hook:** `check-build-before-commit.sh` — added `--no-verify` flag blocking to protect pre-commit hooks
- **Updated agents:** `taleworlds-researcher.md` and `feature-builder.md` — added iterative retrieval (3-cycle progressive refinement) guidance
- **Updated:** `CLAUDE.md` with model routing table (Opus/Sonnet/Haiku guidance)
- **Updated:** `settings.json` with 4 new hook entries (config-protection, suggest-compact, mcp-health-check, mcp-health-mark)

### Claude Code Session Hooks, Agent Audit Logging & Scope-Check Skill

Cherry-picked ideas from the Claude Code Game Studios template and adapted them to TAOM's workflow. Adds session awareness, context recovery, agent tracking, and a scope assessment tool.

- **New hook:** `session-start.sh` (SessionStart) — prints branch, last 5 commits, latest CHANGELOG features, uncommitted file counts, and TODO/FIXME count on fresh session startup. Skips on resume/compact/clear.
- **New hook:** `pre-compact.sh` (PreCompact) — dumps all modified/staged/untracked files before context compaction so the file list survives summarization.
- **New hook:** `log-agent.sh` (SubagentStart) — silently logs every subagent invocation (type, ID, timestamp) to `.claude/logs/agent-audit.log`.
- **New skill:** `/scope-check [change]` — read-only assessment that classifies a proposed change as GREEN (natural extension), YELLOW (adjacent work), or RED (scope creep) based on CHANGELOG themes, recent commits, and in-progress work.
- **Updated:** `settings.json` with SessionStart, PreCompact, SubagentStart hook entries
- **Updated:** `.gitignore` with `.claude/logs/` exclusion
- **Updated:** `CLAUDE.md` hooks and skills tables, `agent-teams.md` troubleshooting and limitations sections

## 2026-03-25

### Remove "The" Prefix from Kingdom/Faction Names (#38)

Fixed in-game messages displaying awkward text like "The Erebor have formed an alliance with the Imladris" and "Daeron of the Mirkwood". The "The" came from two sources: TAOM's own formal name strings and vanilla localization templates designed for plural names like "Vlandians".

- **Stripped "The"** from 12 `str_faction_formal_name_for_culture.*` strings (e.g., "The Clans of Dunland" → "Clans of Dunland")
- **Overrode ~30 vanilla localization templates** in `taom_module_strings.xml` using GameText last-write-wins mechanism
- **Categories overridden:** diplomacy notifications, siege/raid news, battle results, faction titles, policy decisions, alliance/war decisions, peace warning prompts, minor faction dialogue
- **Grammar fixes:** adjusted plural verbs to singular ("have formed" → "has formed") for proper noun kingdom names
- **DLL token overrides** for policy/alliance messages (reuse same `{=TOKEN}` IDs) — needs in-game verification

### Alignment-Aware Execution System

Replaced vanilla Bannerlord's one-size-fits-all lord execution penalties with LOTR-thematic alignment logic. Free Peoples executing servants of Sauron now incur zero honor or relation penalties with allies. Same-alignment executions are treated as kinslaying with 50% harsher penalties.

- **New feature:** `Main/Features/Execution/` — full execution override system (12 new files)
- **GameModel override:** `TaomExecutionRelationModel` replaces `DefaultExecutionRelationModel` — alignment-aware relation penalties
- **Harmony patches:** `KillCharacterAction.ApplyInternal` (thread-local context) + `TraitLevelingHelper.OnLordExecuted` (honor penalty skip)
- **Alignment data:** `Main/_Module/ModuleData/execution/alignment.json` — 16 kingdoms mapped to Free/Evil/Neutral
- **Cross-alignment kills:** 0 honor penalty, 0 relation penalty with executor's allies
- **Kinslaying (same-alignment kills):** 1.5x vanilla penalties (-90 same-clan, -45 friend, -15 faction)
- **Neutral kingdoms (Umbar):** treated as enemy by both sides
- **28 new tests** covering AlignmentService and ExecutionActionHook
- **Documentation:** `docs/features/alignment-aware-execution.md`
- **Modified:** `IoC.cs`, `SubModule.cs` (registration + Patch14_Execution category)

### Child Equipment Templates for Custom Cultures

Added child equipment roster templates for all 10 custom TAOM cultures to prevent NullReferenceException during offspring delivery and ensure children spawn with culture-appropriate clothing.

- **New file:** `taom_child_equipment_templates.xml` — 60 equipment rosters (6 per culture: noble/townsman/villager × male/female)
- **Cultures covered:** gondor, erebor, rivendell, mirkwood, lothlorien, isengard, gundabad, mordor, dolguldur, umbar
- **Item selection:** lightest civilian items from each culture's Armory (tunics, dresses, boots)
- **Fallback sharing:** lothlorien reuses rivendell items, umbar reuses gondor items
- **Safety net:** existing `GetCivilianEquipment_Patch` Harmony patch retained as a defensive fallback
- Registered in SubModule.xml as EquipmentRosters

## 2026-03-21

### Erebor & Iron Hills Troop Tree Restructure

Complete overhaul of the Erebor faction troop trees based on artist specifications (41 new troops):

**Erebor Regular (T2-T6, 8 troops):** Miner → Militia → Skirmisher/Company branches → Bowman/Fighter → Mattock Warrior/Warrior terminals. Leather-to-chain armor progression.

**Erebor Noble (T3-T9, 13 troops):** Noble → Ranger/Longbeard branches → Archer line (Veteran Archer T6) + Infantry line (Guard → Shield-Guard → Gate Warden → Royal Warden T9) + 2H line (Axe-Guard → Veteran Axe-Guard → Shield-Breaker T8). Plate armor progression.

**Erebor Oathsworn (T7-T9, 3 troops):** Special rare line with legionary helmets. Oathsworn → Legionary → Royal Legionary. Chariots planned for future.

**Iron Hills Regular (T2-T6, 8 troops):** Recruit → Militia → Skirmisher/Company → Bowman/Fighter → Axe Warrior/Warrior. Uses Iron Hills items (sm_dwarf_iron_sword, iron shields, iron armor).

**Ironpass Regional Noble (T2-T7, 9 troops):** Recruit → Warrior → Infantry/Arbalest branches → Axeman → Veteran Axeman → Mountain Guard (T7). Uses crossbows and tower shields with Iron Hills heavy armor.

**Integration:**
- Old 47 troops orphaned (upgrade_targets cleared) for save compatibility
- Updated all 9 Erebor party templates with new troop IDs
- Updated spcultures: basic_troop=erebor_reg_miner, elite_basic_troop=erebor_noble
- Added Erebor settlement/clan/culture mappings to VolunteerRecruitmentService (13 settlements, 7 clans, 3-tier culture fallback)
- 24 new recruitment tests added (63 total passing)
- All item IDs validated against LOTRLOME_Armory

### Khamul's Troop Tree (Dol Guldur)

Added complete Khamul human troop tree (T4-T9, 14 troops total):
- 8 new troops: Shadow Initiate → Disciple → Infantry/Archer split → Warden/Marksman → 3-way elite split
- Updated 6 existing troops with Khamul-specific equipment
- Shadow Initiate marked as `is_basic_troop` — standalone entry point
- All Khamul troops are human (no race attribute), using `fighter_dolguldur` face template
- Added Khamul troops to DG party template + recruitment service

### Dol Guldur Troop Tree Fixes

- Fixed Goblin Skirmisher Bow skill 80 → 10 (was leftover from Ranged role)
- Removed `is_basic_troop` from `dg_warg_scout` (now upgrade from Orc Recruit)

## 2026-03-20

### Fix Siege Camp IndexOutOfRangeException

- Added Harmony Prefix patch on `BesiegerCamp.GetSiegeCampPartyPosition` to guard against empty `siegeCamp1GlobalFrames`
- Settlement "Gwígar" (and potentially others) has no `siege_camp_1` scene entities, causing `IndexOutOfRangeException` when a party starts a siege
- Patch swaps camp2 frames into camp1 slot when camp1 is empty, preserving vanilla positioning logic
- Falls back to settlement gate position if both camp frame arrays are empty

### Fix Villager Party Settlement Menu NRE

- Added battle equipment rosters to all 13 custom villager NPCs across all cultures
- Villagers only had `civilian="true"` equipment, causing `FirstBattleEquipment` to return null
- `CampaignUIHelper.GetCharacterCode` crashes on `.Clone()` when rendering the settlement party overlay
- Cultures fixed: Gondor, Dale, Erebor, Dunland, Dol Guldur, Gundabad, Harad, Isengard, Mordor, Rhûn, Rivendell, Mirkwood, Khand

### Fix Clan Owner NRE Crash

- Created 17 unique Harad lord heroes (`lord_A10_1` through `lord_A26_1`) for clans `clan_aserai_10`-`clan_aserai_26`
- Created 5 unique Umbar lord heroes (`lord_U2_1` through `lord_U6_1`) for clans `clan_umbar_2`-`clan_umbar_6`
- All 22 clans previously shared placeholder owners (`lord_3_1` / `lord_U1_1`), causing orphaned clans with null Kingdom at runtime and NRE in `ChangeKingdomAction.ApplyInternal`

### Fix Orphaned Clan Owners — Missing XSLT Faction Reassignment

- Fixed 9 custom clans whose owner heroes still had vanilla faction assignments in `heroes.xslt`
- Added `faction` attribute to XSLT templates for: `lord_6_21`-`lord_6_24` (Rhûn clans 10-13), `lord_1_34` (Faramir → Garvirionath), `lord_1_48` (Khamûl → Hîondrûs), `lord_4_23` (Marhad), `lord_4_28` (Morcargas), `lord_V11_l` (Deáfringas)
- Updated `spclans.xslt` to reassign vanilla clan owners for `clan_vlandia_7` (→ `lord_4_23_1`), `clan_vlandia_10` (→ `lord_4_28_1`), `clan_vlandia_11` (→ `lord_V11_u`)
- Also moved family members (spouses/children) to correct custom clans via `heroes.xslt`
- Root cause: `CharacterRelationCampaignBehavior.OnClanChangedKingdom` NRE when `oldKingdom` is null

### Fix Gondor Equipment — Replace Armory_2-Only Items

Replaced 367 equipment item references across 10 files that pointed to items only
available in `LOTRLOME_Armory_2` (not in `LOTRLOME_Armory` which TAOM depends on).
Characters in CC, NPCs, lords, and troops were appearing in underwear because the
body/head/leg/arm/cape items didn't exist at runtime.

**Item mapping (29 items replaced):**
- Body: `gondor_noble_coat_a/b` → `ithilien_jerkin_long/_var`, `gondor_noble_jerkin_a/b` → `ithilien_jerkin_short`/`boromir_jerkin`, `gond_tab_9ld` → `cts_gondor_armor3`, `citidel_guard_armor1/2/4` → `sk_gd_mns_citadel_chest_*`/`sk_gd_ano_inf_chest_heavy_a`, `fountain_armor1` → `sk_gd_mns_fount_chest_heavy_a`, `gondor_king_armor` → `sk_gd_ano_inf_chest_heavy_b`
- Head: `citidel_guard_helmet1/3/5` → `sk_gd_mns_cita_helmet_heavy_a/b`/`sk_gd_mns_noble_helmet_heavy_a`, `fountain_guard_helmet` → `sk_gd_mns_fount_helmet_heavy_a`
- Leg: `citidel_guard_boots/_light` → `sk_gd_ano_grvs_inf_med_a/_light_a`, `fountain_guard_boots` → `sk_gd_ano_grvs_noble_med_a`, `gondor_nobke_boots` → `sk_gd_ano_boots_a`
- Arms: `citidel_guard_gloves/bracers/bracers_shield` → `sk_gd_ano_gloves_a`/`sk_gd_ano_bracer_inf_med_a`/`sk_gd_ano_bracer_noble_med_a`, `gondor_nobke_bracers` → `sk_gd_ano_bracer_noble_heavy_a`
- Cape: `citidel_guard_armor_pauldrons/_light` → `sk_gd_ano_pauld_inf_heavy_a/_med_a`, `fountain_guard_pauldrons` → `sk_gd_ano_pauld_cape_fount_elite_a`, `fountain_shoulders2` → `sk_gd_ano_pauld_noble_med_a`, `gondor_nobke_pauldrons` → `sk_gd_ano_pauld_noble_heavy_a`

**Files modified:** `taom_char_creation_equipment.xml`, `taom_equipment_sets_gondor.xml`, `npcs_gondor.xml`, `npcs_umbar.xml`, `troops_gondor.xml`, `troops_umbar.xml`, `troops_rohan.xml`, `troops_rivendell.xml`, `taom_wanderer_equipment.xml`, `lords.xml`

Also removed non-existent `spc_wanderer_rohan_9` reference from `spcultures.xslt`.

### Fix Null Object Reference Errors

- Added missing `spc_wanderer_rohan_9` wanderer (definition, skill set, backstory strings)
- Reassigned Gondor heroes (lord_EW_9/14/23/20) from non-existent clans 15-18 to existing empire_west clans 10-13
- Reassigned Mordor heroes (lord_M16_1/17_1/18_1) from non-existent clans 16-18 to existing empire_south clans 10-12
- Fixed Easterling caravan templates: `caravan_template_khuzait` → `caravan_template_rhun` (matching Rohan pattern)

### Rhûn Troop Generator

Created `tools/generate_rhun_troops.py` — Python generator replacing manually-maintained XML with
113 troops across 11 unit groups:
- **Easterling Regular** (T1-T5, 13 troops) — `sk_rh_loke_` spiky/east armor
- **Loke-Rim Noble** (T3-T7, 14 troops) — `sk_rh_loke_` half-plate → plate, role-specific helmets
- **Dragon-Wrath** (T5-T9, 14 troops) — `sk_rh_drag_` half-plate → plate
- **Wainriders** (T3-T7, 8 troops) — `sk_rh_loke_` lamellar/arch helmets
- **Black Sun Mercenaries** (T2-T8, 11 troops) — `sk_rh_drag_` lamellar (shock) / spiky (archer)
- **Darkhûn Mercenaries** (T2-T8, 11 troops) — `sk_dg_khml_` half-plate (inf) / lamellar (cav)
- **Sagarûn** (T3-T7, 10 troops) — Loke scalemail (marines) / Drag scalemail (naffatun/arbalest)
- **Balcoth** (T2-T6, 9 troops) — Easterling Regular armor
- **Far-Rhun** (T3-T7, 9 troops) — Easterling Regular armor
- **Kharaghûl** (T2-T7, 10 troops) — Easterling Regular armor
- **Militia** (T2-T3, 4 troops) — old easterling armor (preserved)

Deleted `troops_rhun_new.xml` (superseded) and removed its SubModule.xml entry.
Updated `rebalance_troops.py` to process `troops_rhun.xml` (was skipped when old/new coexisted).

### Dol Guldur Troop Tree Restructure

Restructured all three non-Khamul DG troop lines to match artist spec:

**Goblin line** — converted from linear chain to branching tree:
- Renamed "Goblin Slave" display to "Goblin Runt" (ID unchanged for save compat)
- Added 3 new troops: Goblin Harrier (T2 melee), Goblin Impaler (T4 melee), Goblin Fellbow (T5 ranged)
- Runt now splits into Harrier (melee branch) and Crawler (ranged branch)
- Skirmisher moved to melee branch (Infantry), retooled equipment from bows to melee weapons
- Hunter now upgrades directly to Archer (was Skirmisher)

**Orc line** — connected Warg branch:
- Orc Recruit now upgrades to both Orc Gnasher AND Warg Scout (was Gnasher only)
- Removed Orc Scout branch from Orc Warrior upgrade path (Warrior → Reaver only)
- Orc Scout and Orc Archer kept as orphaned troops for save compatibility

**Uruk line** — display name corrections:
- "Uruk Warrior" (T3) renamed to "Uruk Fighter" to match spec
- "Uruk Veteran Warrior" (T4) renamed to "Uruk Warrior" to match spec

Updated ALL Dol Guldur party templates:
- `kingdom_hero_party_dolguldur_template`: added Harrier, Archer, Impaler, Fellbow stacks
- `kingdom_hero_party_outlaw_dolguldur_template`: added Harrier
- `patrol_party_dolguldur_template_level_1`: added Harrier
- `patrol_party_dolguldur_template_level_3`: added Khamul Shadow Warden + Marksman
- `rebels_dolguldur_template`: added Harrier
- `vassal_reward_troops_dolguldur`: added Khamul Shadow Infantry + Archer

Added `.claude/rules/troops.md` — troop management checklist, race attributes, party template types, save compatibility rules.

## 2026-03-19

### Khamul's Troop Tree (Dol Guldur)

Added complete Khamul human troop tree (T4-T9, 14 troops total):
- 8 new troops: Shadow Initiate → Disciple → Infantry/Archer split → Warden/Marksman → 3-way elite split
- Updated 6 existing troops (Veiled Knight/Guard/Marksman, Shadow Knight/Guard/Bowman) with Khamul-specific equipment
- Shadow Initiate marked as `is_basic_troop` — standalone entry point, disconnected from generic DG feeder troops
- All Khamul troops are human (no race attribute), using `fighter_dolguldur` face template
- PLATE armor line (Guard/Knight), SPIKY armor line (Reaper/Archer)

Integration:
- Added Khamul troops to `kingdom_hero_party_dolguldur_template` party template
- Added Dol Guldur settlement/clan/culture mappings to `VolunteerRecruitmentService` (with tests)
- Removed Khamul upgrade targets from generic `dg_warden` and `dg_marksman` feeder troops

### Gondor Old Asset Cleanup

Removed 66 orphaned armor item entries from LOTRLOME_Armory gondor XMLs whose FBX source
files were deleted in lotraom-assets commit `defb2642`:
- head_armors.xml: -31 items (citadel helmets, fountain helmets, old soldier helmets)
- body_armors.xml: -14 items (citadel/fountain/king/noble armor, old tabard)
- shoulder_armors.xml: -9 items (citadel/fountain/king/noble/old pauldrons)
- arm_armors.xml: -5 items (citadel bracers/gloves, king/noble bracers)
- leg_armors.xml: -7 items (citadel/fountain/king/noble/old boots)

Fixed 4 militia troops referencing deleted body armor (gondor_noble_jerkin_a/b,
gond_tab_9ld, gondor_noble_coat_a) — replaced with sk_gd_ano_chainmail_* items.

Added 10 missing armor items (total now 93): 3 elite body, 5 shoulders, 2 elite bracers.

Replaced 13 additional old Gondor items with `sk_gd_*` equivalents across all equipment sets
(troops, lords, NPCs, wanderers, char creation, equipment sets):
- 7 helmets → `sk_gd_ano_inf_helmet_med_a` / `heavy_a` / `sk_gd_ano_noble_helmet_med_a`
- 1 body → `sk_gd_ano_chainmail_half_a`
- 2 shoulders → `sk_gd_ano_pauld_inf_med_a`
- 1 arm → `sk_gd_ano_bracer_noble_med_a`
- 1 leg → `sk_gd_ano_boots_a`
- Removed all 79 orphaned items from both lotraom-assets and Steam armory XMLs

Cleanup script: `tools/cleanup_deleted_gondor_armor.py`

### Gondor Equipment Pass — 6 Guided Groups + Scaffolding

Created 83 new armor item definitions (`sk_gd_*` prefix) in LOTRLOME_Armory for 6 guided groups:
- **Anorien Regular** — Generic infantry base armor (chainmail → heavy chest progression)
- **MT Citadel Guard** (T5-T8) — Citadel-specific chest/helmet progression
- **MT Fountain Guard** (T9) — Elite fountain helmet + cape+pauldron combo
- **Osgiliath** (T3-T7) — Branch-specific helmets (Infantry/Dome Guard vs Longbow)
- **Cair Andros** (T3-T7) — Branch-specific helmets (Pike vs Warden)
- **Minas Ithil** (T5-T9) — Noble armor progression, Moon Guard at T9

Refactored remaining 17 region equip functions to tier-based dictionary structure:
- 20 dict sets (LOSS_*, PEL_*, DA_INF_*, etc.) with empty slots ready for future armor guides
- `_apply_region_armor()` helper falls back to GENERIC_* when dict values are empty
- All region-specific weapons preserved (axes, swan knight spears, etc.)
- Generator: `tools/generate_gondor_armor.py` (--dry-run / --apply)

### New Gondor Troop Tree

Replaced the existing 77-troop Gondor tree with a comprehensive 182-troop tree spanning 23 unit groups across 18 sub-regions:

**8 Regular Lines** (village recruitment): Lossarnach, Lebennin, Lamedon, Belfalas, Pinnath Gelin, Anfalas, Harondor, Anorien
**15 Noble Lines** (notable recruitment): Lossarnach Noble, Pelargir, Calembel, Ringlo Vale, Dol Amroth, Linhir, Tolfalas, Arndir, Blackroot Vale, Serelond, Lond-Galen, Methir, Minas Ithil, Cair Andros, Osgiliath, Minas-Tirith

- 24 is_basic_troop roots for recruitment
- Skills balanced via rebalance_troops.py (Gondor cultural modifiers + weapon specializations)
- Equipment reused from existing Gondor item pool, themed by sub-region
- Generator script: `tools/generate_gondor_troops.py`
- Notable elite units: Swan Knights (T9), Fountain Guard (T9), Moon Guard (T9)

**Note**: spcultures.xml and partyTemplates.xml references not yet updated — old troop IDs still referenced.

## 2026-03-15

### Bug Fix — Character Creation Race Display (#22)

Non-human races (dwarf, elf, uruk, etc.) displayed as human models during character creation. Two root causes:

**Race filtering broke FaceGenVM** — The `FaceGen_GetRaceNames_Patch` postfix filtered `GetRaceNames()` globally, but `FaceGenVM` uses array index as global race ID. Filtering shifted all indices (dwarf→uruk, uruk→orc, nazgul→goblin).
- Disabled race filtering in `FaceGen_GetRaceNames_Patch` (now a no-op, all races shown in dropdown)
- Removed `CharacterTableau_SetRace_Patch` race index mapper prefix (no longer needed)
- Stripped `FilterRaceNames` and `MapFilteredIndexToGlobalId` from `GetRaceNamesHook` / `IOnGetRaceNames`
- Simplified `CharacterCreationIoC` — removed filter/mapper wiring

**Body property templates pointed to human** — 7 non-human cultures had `default_character_creation_body_property` set to empire (human) template instead of race-specific templates.
- Updated `taom_spcultures.xml`: erebor→`fighter_erebor`, rivendell→`fighter_rivendell`, mirkwood→`fighter_mirkwood`, lothlorien→`fighter_rivendell`, isengard→`fighter_uruk_hai`, gundabad→`fighter_gundabad`, dolguldur→`fighter_dolguldur`

**Secondary fix** — Female action set name had double underscore in `CharacterTableau_RefreshCharacterTableau_Patch` (`as_dwarf_female__warrior` → `as_dwarf_female_warrior`).

240 tests passing.

## 2026-03-12

### Bug Fix — Youth Equipment Differentiation (Phase 6)

Fixed bug discovered during in-game testing of character creation:

**Youth equipment all identical** — Youth narrative options were not setting `SelectedTitleType`, causing all options to produce the same equipment regardless of selection.
- Added `TitleType` property to `NarrativeOptionDefinition` model
- Updated `NarrativeMenuBuilder.BuildOption()` to set `SelectedTitleType` when `title_type` is present (vs `SetParentOccupation` for parent menus)
- Updated `NarrativeDataProvider.ParseOption()` to parse `title_type` from JSON
- Added `title_type` to all 91 entries in `youth_menu.json` mapping each option to a career (retainer, guard, hunter, infantry, skirmisher, bard, mercenary)

### Feature — Character Creation Equipment Rosters (Phase 5)

Created culture-specific equipment rosters for all 10 custom cultures, replacing the temporary `EquipmentCultureRemap_Patch` Harmony workaround.

- `tools/generate_char_creation_equipment.py` — Python generator producing 550 equipment rosters from per-culture item mappings
- `ModuleData/taom_char_creation_equipment.xml` — 550 rosters (55 per culture × 10 cultures)
  - 2 parent fallback (`none`), 12 parent occupation, 24 childhood/education age, 16 adult career, 1 show per culture
- Items sourced from LOTRLOME_Armory module with culture-appropriate low-tier gear
- Lothlorien uses Rivendell items; Umbar uses Rhun/Easterling items
- Registered in `SubModule.xml` as `EquipmentRosters` node
- Removed `EquipmentCultureRemap_Patch.cs` and `Patch8_CharacterCreation` from `SubModule.cs`

### Feature — Character Creation Narrative System (Phases 1-3)

Ported LOTRAOM character creation system to TAOM's Bannerlord 1.3.x handler-based API (`ICharacterCreationContentHandler`). Replaces vanilla Calradia narrative text with LOTR-themed lore for all 16 cultures.

**Phase 1 — Feature Scaffold + Culture Registration (8 new C# files):**
- `CharacterCreationIoC.cs` — DI registrations for feature services
- `CharacterCreationRegistrationBehavior.cs` — CampaignBehavior listening for `OnCharacterCreationInitializedEvent`
- `TaomCharacterCreationContentHandler.cs` — `ICharacterCreationContentHandler` at priority 1050 (after SandBox 800)
- `ICharacterCreationContentService.cs` / `CharacterCreationContentService.cs` — Core logic: culture registration, menu management, finalization
- `ICultureCreationDataProvider.cs` / `CultureCreationDataProvider.cs` — Loads `cultures.json` with caching
- `Models/CultureCreationData.cs` — POCO for per-culture race, settlement, body property data
- Registers 10 custom cultures via `AddCharacterCreationCulture()` (6 vanilla already registered by SandBox)
- Integration: `IoC.cs` + `SubModule.cs` updated

**Phase 2 — Parents Stage (4 new files):**
- `INarrativeDataProvider.cs` / `NarrativeDataProvider.cs` — Generic JSON loader with `ConcurrentDictionary` cache
- `NarrativeMenuBuilder.cs` — Maps JSON definitions to v1.3 `NarrativeMenuOption` objects with skill/attribute resolution
- `Models/NarrativeOptionDefinition.cs` — POCO for narrative option data
- `parents_menu.json` — 96 options (6 per culture x 16 cultures) with LOTR lore text
- Removes vanilla parent options, adds TAOM options with culture-filtered `OnCondition` delegates

**Phase 3 — Childhood + Youth Stages (2 new data files):**
- `childhood_menu.json` — 6 universal LOTR-themed options (no culture filter)
- `youth_menu.json` — 91 culture-specific options (5-6 per culture x 16 cultures)
- Refactored `NarrativeDataProvider` to support generic `LoadMenuOptions(menuName)` pattern
- `NarrativeMenuBuilder` handles universal options (empty `culture_id` = null condition = always visible)
- Education, Adulthood, Age stages keep vanilla SandBox content (non-culture-specific)

**Data files (4 JSON):**
- `ModuleData/charactercreation/cultures.json` — 10 custom culture definitions
- `ModuleData/charactercreation/parents_menu.json` — 96 parent narrative options
- `ModuleData/charactercreation/childhood_menu.json` — 6 childhood narrative options
- `ModuleData/charactercreation/youth_menu.json` — 91 youth narrative options

**Phase 4 — Finalization: Player Race Setting (1 new test file):**
- Added `IRaceManager` + `IHeroRosterAdapter` dependencies to `CharacterCreationContentService`
- `SetPlayerRace()` uses first race from `CultureCreationData.Races[]` (defaults to "human" if empty/null)
- Called from `OnCharacterCreationFinalize()` after teleport to starting settlement
- `CharacterCreationContentServiceTests.cs` — 5 tests (first race, single race, empty/null races, logging)

**Tests (25 new):**
- `CultureCreationDataProviderTests.cs` — 9 tests (JSON parsing, caching, lookup)
- `NarrativeDataProviderTests.cs` — 11 tests (multi-menu loading, caching, culture filtering)
- `CharacterCreationContentServiceTests.cs` — 5 tests (race setting logic)

**Total:** 193 narrative options across 3 stages, 213 tests passing

### Lords Skill Rebalancing (Phase 2)

- Created `tools/rebalance_lords.py` — baseline + cultural modifier balancing for all 914 lords
- Processes both `lords.xslt` (389 vanilla-transform lords) and `characters/lords.xml` (525 custom lords)
- 12 archetypes derived from vanilla `sandbox_skill_sets.xml`: ruler, warrior_knight, warrior_infantry, warrior_ranged, tactician, siege_engineer, politician, manager, spymaster, scholar, trader, dandy
- Cultural modifiers for 13 cultures: 6 vanilla (dunland, dale, harad, rohan, mirkwood, rhun) + 7 custom (dolguldur, erebor, gundabad, isengard, lothlorien, rivendell, umbar)
- Age scaling: peak at 25-50, gentle decline after 55
- Junior lords (rookie skill_template) at 60% of senior baselines
- 10 legendary lords (Nazgul/Sauron/Witch-King) at 2.5x ruler baseline
- Non-combat archetypes (politician, manager, scholar) now correctly have LOW combat / HIGH non-combat skills
- Combat archetypes (warrior_knight, warrior_infantry, warrior_ranged) have HIGH combat / LOW non-combat
- CLI: `--dry-run`, `--apply`, `--export-csv`

### Lords XSLT Completion (Phase 1)

- Completed `lords.xslt` with all vanilla attributes explicit (was 2-3, now 9-11 per template)
- Added 16 missing lords: 7 dead lords, 9 new Vlandia/Rohan lords (skipped main_hero)
- Total templates: 396 (up from 380)
- Created `tools/complete_lords_xslt.py` for regeneration with `--dry-run`, `--apply`, `--export-csv`
- Exported lord attribute inventory to `tools/lords_inventory.csv`
- No passthrough attributes remain — every attribute is now visible and editable in the XSLT

### Tooling — Claude Code Capabilities Overhaul

**Custom Skills (4 new slash commands):**
- `/research [Class]` — Decompile and analyze TaleWorlds classes via ilspycmd
- `/new-feature [Name]` — Scaffold feature modules with IoC, services, adapters, tests
- `/xslt-check [file]` — Validate XSLT against SandBoxCore vanilla XML
- `/migration-status` — Summarize v1.2 -> v1.3 migration progress

**Path-Scoped Rules (5 new rules):**
- `.claude/rules/xslt.md` — XSLT passthrough, SandBoxCore reference (scoped to `**/*.xslt`)
- `.claude/rules/adapters.md` — Adapter pattern enforcement (scoped to `Main/Adapters/**`)
- `.claude/rules/tests.md` — TDD naming, AAA pattern, coverage (scoped to `TAOM.Tests/**`)
- `.claude/rules/xml-data.md` — NPC naming, region codes (scoped to `ModuleData/**/*.xml`)
- `.claude/rules/harmony-patches.md` — Patch rules, thin entry points (scoped to `Main/**/Hooks/**`)

**Custom Agents (2 new agents):**
- `.claude/agents/taleworlds-researcher.md` — Specialized decompilation and analysis agent
- `.claude/agents/feature-builder.md` — Feature scaffolding following TAOM architecture

**Hook Enhancements:**
- Added `check-changelog-updated.sh` Stop hook — reminds to update CHANGELOG.md at session end
- Enabled agent teams via `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` env var

**Permission & Settings Improvements:**
- Expanded permission allowlist with `dotnet test`, `dotnet build`, `git log/diff/status/branch`
- Added VS Code extensions: `vscode-dotnet-runtime`, `redhat.vscode-xml`, `github.vscode-pull-request-github`
- Enhanced VS Code settings: bracket pair colorization, test peek view, XML validation

**Build Configuration:**
- Added `Directory.Build.props` — centralizes shared MSBuild properties (TargetFramework, LangVersion, Nullable, GameFolder)
- Removed duplicated properties from `TAOM.csproj` and `TAOM.Tests.csproj`

**GitHub CI/CD:**
- Added `.github/workflows/build.yml` — validates XML, XSLT, and JSON well-formedness on every push/PR
- Build & Test job conditional on `BANNERLORD_GAME_DIR` repo variable (requires game DLLs)

**GitHub MCP Server:**
- Added GitHub MCP to `.mcp.json` — enables PR, issue, actions, and code search from Claude

**CLAUDE.md Optimization:**
- Slimmed from 198 to 136 lines — moved detailed XSLT rules, TaleWorlds Research Protocol, and verbose sections to scoped rules and skills
- Added Skills, Scoped Rules, and Custom Agents sections
- Saves ~30% context window on every conversation start

### Tooling — Claude Code Hooks

- Added pre-commit build check hook (`.claude/hooks/check-build-before-commit.sh`) — blocks `git commit` if `dotnet build` fails
- Added C# edit notification hook (`.claude/hooks/notify-csharp-edit.sh`) — logs modified C# file paths to session
- Created `.claude/settings.json` with hook configuration
- Enabled hooks globally (removed `disableAllHooks: true` from global settings)

## 2026-03-11

### Tooling — Developer Environment & AI Workflow Improvements

**VS Code project config (3 new files):**
- `.vscode/tasks.json` — Build (Ctrl+Shift+B), Build+Test, Run Tests tasks with `$msCompile` problem matcher
- `.vscode/extensions.json` — Recommends Claude Code, C# DevKit, XML, PowerShell extensions
- `.vscode/settings.json` — Hides bin/obj/.vs from explorer, enables format-on-save

**Editor formatting (1 new file):**
- `.editorconfig` — Enforces 4-space C# indent, 2-space XML/JSON indent, CRLF line endings, trim trailing whitespace

**Serena MCP per-project configuration:**
- Created `.mcp.json` for TAOM — Serena symbolic code navigation now targets TAOM's C# codebase
- Created `.mcp.json` for Achaea — Serena continues targeting LEVI-Achaea
- Removed Serena from global MCP config (was always pointing at Achaea regardless of project)

**Claude Code configuration cleanup:**
- Removed 5 stale one-off permission entries from global `settings.json`
- Removed 3 stale permission entries from project `.claude/settings.local.json`
- Added 4 new memory files: user profile, feedback (SandBoxCore reference, XSLT passthrough), external references
- Updated MEMORY.md index with new memory file links

**CLAUDE.md updates:**
- Added VS Code config, .editorconfig, and .mcp.json to Key Paths table
- Added MCP Servers section documenting Serena, sequential-thinking, and context7

### Feature — Interactive Faction Selection Map

Ported external LOTRAOM_FactionMap feature into TAOM as `Main/Features/FactionMap/`. Replaces vanilla character creation culture selection with a clickable Middle-earth map (36 regions, 18 factions, 6-pass rendering with animations).

**Architecture (46 new C# files):**
- Models: FactionData, RegionData, LandmarkDef, FactionSelectionResult, HoverStateChange (5 POCOs/DTOs)
- Services: FactionConfigProvider, FactionRegistryService, LandmarkService, CultureResolverService, FactionSelectionService, FactionHoverService (6 TDD services + interfaces)
- Adapter: ICultureObjectAdapter/CultureObjectAdapter wrapping MBObjectManager
- ViewModels: FactionSelectionVM (thin, <200 lines) + 4 sub-VMs (TraitItem, BonusItem, PerkItem, LandmarkItem)
- Widgets: PolygonWidget (6-pass renderer), BannerWidget, FactionImageWidget, MapContainerWidget, RuntimeSprite
- Hooks: 3 Harmony patch pairs (Constructor/Tick/Finalize) on CharacterCreationCultureStageView using hook interface pattern
- Infrastructure: FactionMapIoC, FactionMapPaths, FactionMapStaticBridge

**Data & Assets:**
- `factions.json` — 29 factions with culture IDs mapped to TAOM's 16 cultures (10 custom + 6 remapped vanilla)
- `regions.json` — 36 clickable map regions with bounding boxes and polygon vertices
- 111 PNG sprite assets (banners, faction images, highlights)
- FactionMap.xml brushes, CharacterCreationCultureStage.xml prefab, sprite registration XML

**Tests (45 new tests):**
- FactionConfigProviderTests (6), FactionRegistryServiceTests (9), FactionSelectionServiceTests (12), FactionHoverServiceTests (7), CultureResolverServiceTests (6), LandmarkServiceTests (5)

**Review fixes (9 issues resolved):**
- Added explicit `[HarmonyPostfix]`/`[HarmonyPrefix]` attributes to all 3 Harmony patches (were relying on method name convention only)
- Added comments explaining dynamic `TargetMethod()` pattern for View assembly types
- Extracted FactionDisplayHelper from FactionSelectionVM (263→150 lines)
- Extracted ICultureSettingService/CultureSettingService from CultureStageViewCreatedHook (205→146 lines)
- Extracted FactionDataParser from FactionConfigProvider (161→119 lines)
- Fixed LandmarkService thread safety (lazy init → constructor initialization)
- Added IModLogger to CultureObjectAdapter for exception logging
- Converted PolygonWidget to file-scoped namespace
- Updated all `game_faction` values in factions.json to TAOM culture IDs (gondor, erebor, mordor, rivendell, etc.)
- Added 7 edge-case tests (malformed JSON, color fallbacks, difficulty bounds, logging verification)

**Modified existing files:**
- IoC.cs — Added FactionMapIoC registration
- SubModule.cs — Added FactionMapPaths initialization + Patch7_FactionMap category
- TAOM.csproj — Added AllowUnsafeBlocks, System.Numerics.Vectors package

### Website — Weapon Balance Data Corrections

- Fixed Rhun avgMelee from 66 to 69 (was using simple average instead of weighted average across rhun+khuzait cultures)
- Fixed Rhun meleePercent from 97% to 101% to match corrected average
- Demoted Dol Guldur from A-tier to B-tier for Shock Troops (no longer justified with -3 pts weapons)
- Demoted Dol Guldur from A-tier to B-tier for Line Breakers (same reason)
- Removed 22 stale percentage-based weapon references from balance-overview.astro (140%, 120%, 118%, etc.)
- Updated Overview section in weapon-balancing.astro from old percentage system to points-based narrative

### Website — Balance Overview Page

- Added `/mod-info/balance-overview` page with faction power rankings across all three balance axes (troop skills, armor, weapons)
- Added Balance Overview card to mod-info index page
- Faction Power Comparison table with S-D grading for 12 non-elven cultures + 3 elven cultures (separate section)
- Iron Hills and Erebor graded individually (not combined)
- Balance Triangle visual explaining the three-axis system

### Website — Infantry Subcategories & Tier Lists

- Added 7 tier lists: Overall Infantry, Front Line, Shock Troops, Line Breakers, Skirmishers, Cavalry, Ranged
- Gaming-style S-D tier format with per-culture reasons
- Updated all tier list descriptions to reference actual troop equipment loadouts (Item0-Item4 from NPC XML)
- Troop role classification based on actual equipment: sword+shield = frontline, 2H weapon = shock/linebreaker, throwing weapon = skirmisher, bow/crossbow = ranged
- Key findings from equipment analysis:
  - Dunland: 28 of 30 infantry carry throwing weapons (S-tier skirmisher, D-tier frontline)
  - Dol Guldur: 17 ranged troops (S-tier ranged), 22 shield troops, 5 linebreakers
  - Erebor/Iron Hills: zero throwing troops, zero cavalry — pure heavy infantry
  - Rohan: 18 infantry shield troops (Westfold, Westmarches, Edoras) — B-tier frontline, not D

### Weapon Rebalancing — Points-Based System

- Replaced percentage-based weapon modifiers with points-based craftsmanship system
- Each culture gets points above/below global average melee damage (68):
  - Noldor (Rivendell): +10, Sindar (Lothlorien): +9, Erebor/Iron Hills: +5
  - Mirkwood: +4, Gondor: +3, Rhun: +2, Arnor: +2
  - Isengard: 0 (baseline), Rohan: 0 (polearms +3), Harad: 0
  - Gundabad: -1, Mordor: -2, Dunland: -2, Dol Guldur: -3
- Applied 217 blade piece modifications via `rebalance_weapons.py --apply`
- Rohan polearms get separate +3 point bonus for cavalry lance superiority
- Hero/legendary weapons exempt from modifiers (18 pieces)
- Bows excluded — to be handled separately later
- Updated `weapon-balancing.astro` with new per-culture data and craftsmanship narrative
- Updated `balance-overview.astro` weapon grades to reflect new system
- New philosophy: weapon quality reflects craftsmanship (elves = best, dwarves = great, evil = crude)

### Website — Rename Goblins to Dol Guldur Orcs

- Renamed 'Goblins' to 'Dol Guldur Orcs' across weapon-balancing, troop-balancing, armour-balancing, and balance-overview pages
- Preserved 'Goblin' in troop names (Goblin Hunter, Goblin Slave) and race descriptions

### Armor Modifier Revisions

- Gundabad protection: -2 → 0 (holds dwarven cities, access to dwarven forges)
- Dol Guldur protection: -1 → 0 (fortress-forged plate from Sauron's armories)
- Rivendell protection: +6 → +5 (on par with dwarves, not above)
- Gondor protection: 0 → +1 (Numenorean smithing tradition)
- Re-ran `rebalance_armor.py --apply` on 83 armor files (2,368 items)
- Updated `balance-overview.astro` armor grades: Gundabad D→B, Dol Guldur C→B
- Updated `armour-balancing.astro` culture detail cards with new values and lore

---

## 2026-03-10

### Website — Database Landing Page & Lord Database Fixes

- Added `/database` landing page with overview cards matching mod-info style (Troops, Lords, Armoury, Weaponry)
- Added "Overview" link to Database dropdown nav
- Fixed lord database: culture group headers now start collapsed by default
- Fixed bug where collapsed culture headers disappeared — `filterRows()` was checking display state instead of filter match
- Removed 48 generic militia troops (militia archer/spearman/veteran variants) from website troop data across 12 cultures; keeps named militia troops (gondor_militiaman, rohan_westfold_militiaman, harad_militia, easterling_militia)

### Armor Rebalancing — 2,368 Items Across 17 Cultures

Comprehensive armor stat rebalancing using a uniform baseline + cultural modifier formula, mirroring the troop skill rebalancing system.

**Approach:**
- Created `tools/rebalance_armor.py` — Python script with baseline armor values per tier (civilian/light/medium/heavy/elite/lord) and per-slot (head/body/arm/leg/shoulder), plus cultural modifiers
- Tier detection via keyword matching on item names/IDs with value-based fallback
- Numbered variants (I, II, III...) get +1 armor progression within each tier
- Material type corrected: light=Leather, medium=Chainmail, heavy+=Plate

**Baseline body armor values:** civilian=5, light=20, medium=32, heavy=42, elite=50, lord=60

**Cultural Identities:**

| Culture | Protection Mod | Weight Mult | Identity |
|---------|---------------|-------------|----------|
| Erebor | +4 | 1.05x | Master dwarven smiths |
| Iron Hills | +5 | 1.10x | Heaviest dwarven armor |
| Rivendell | +6 | 0.70x | Finest elven masterwork |
| Mirkwood | +5 | 0.65x | Lightest elven craft |
| Lothlorien | +5 | 0.70x | Golden wood craft |
| Gondor | +0 | 1.00x | Reference culture |
| Rohan | -2 | 0.90x | Lighter for mounted |
| Isengard | +2 | 1.15x | Industrial heavy |
| Mordor | -1 | 1.10x | Crude mass-produced |
| Gundabad | -2 | 1.15x | Crude but heavy |
| Harad | -3 | 0.85x | Desert light armor |
| Dunland | -2 | 0.95x | Hill-folk |

**Files modified:** 83 armor XMLs in `taommod/src/data/armory/` + `tools/rebalance_armor.py`
**Item count:** 2,368 armor items across 17 cultures, 5 armor slots

---

### Troop Progression — Level 51 Support (TroopProgression Feature)

Ported LOTRAOM's extended troop tier system to TAOM for Bannerlord 1.3. Raises the troop tier cap from vanilla's 6 (level 31+) to 10 (level 51+), enabling meaningful differentiation across all troop levels produced by the rebalance script.

**C# Implementation (10 files):**
- `TaomCharacterStatsModel` — GameModel override: `MaxCharacterTier => 10` (vanilla 6). Vanilla `GetTier()` formula `Ceiling((level-5)/5)` clamped to `[0, MaxCharacterTier]` naturally produces tiers 7-10 for levels 36-55
- `TaomPartyWageModel` — GameModel override: extended tier-based wages (T0=1 through T10=30) and level-bracket recruitment costs (L1=10 through L51=3600, L52+=4000). `MaxWagePaymentLimit` raised to 20,000 (vanilla 10,000). Includes mounted surcharge (1.3x) and mercenary/gangster/caravan guard multipliers
- `TaomVolunteerModel` — GameModel override: `MaxVolunteerTier => 6` (vanilla 4), allowing higher-tier volunteers
- `TroopCostService` / `ITroopCostService` — wage and recruitment cost calculations using primitives only (no sealed types)
- `VolunteerTierService` / `IVolunteerTierService` — volunteer tier configuration
- `TroopProgressionIoC` — DryIoc feature registration
- 37 `TroopCostServiceTests` + 2 `VolunteerTierServiceTests` = 39 new tests

**Tier-to-level mapping (with MaxCharacterTier=10):**

| Tier | Levels | Wage | Recruitment Cost |
|------|--------|------|-----------------|
| 0 | 1-5 | 1 | 10-20 |
| 1 | 6-10 | 2 | 20-50 |
| 2 | 11-15 | 3 | 50-200 |
| 3 | 16-20 | 5 | 200-400 |
| 4 | 21-25 | 8 | 400-600 |
| 5 | 26-30 | 12 | 600-1000 |
| 6 | 31-35 | 15 | 1000-1500 |
| 7 | 36-40 | 18 | 1500-2100 |
| 8 | 41-45 | 20 | 2100-2800 |
| 9 | 46-50 | 25 | 2800-3600 |
| 10 | 51-55 | 30 | 3600-4000 |

**Integration:** GameModels registered via `CampaignGameStarter.AddModel()` in `SubModule.OnGameStart` — "last model wins" semantics ensure TAOM overrides vanilla defaults.

**Not yet ported from LOTRAOM (future work):** culture feat wage modifiers (6 factions), `GetTotalWage` faction modifiers, race bonus wage hooks, settlement-specific volunteer pools.

---

### Troop Skill Rebalancing — All 13 Culture Files (545 troops)

Comprehensive skill rebalancing across all troop trees using a uniform baseline + cultural modifier formula. Previously, skills were wildly inconsistent: Rhun had placeholder 150 values, Rivendell had 300+ at level 21 (3x peers), Umbar/Dunland cavalry were 0.5x average, and 40 militia entries had zero skills.

**Approach:**
- Created `tools/rebalance_troops.py` — Python script with baseline skill tables per level/group (Infantry, Ranged, Cavalry, HorseArcher) and per-culture modifiers
- Baseline tables define center values for 11 level tiers (1-51) across 8 combat skills
- Cultural modifiers (±5-10 for standard factions, +25-50 for elven factions) give each culture distinct identity
- Weapon specialization detection swaps primary/secondary weapon skills based on troop names (crossbow, pike, sword, axe)
- Militia entries now use level 21 baselines of their culture instead of all-zero skills
- Regex-based XML replacement preserves all formatting, comments, and non-skill attributes

**Cultural Identities:**

| Culture | Strengths | Weaknesses |
|---------|-----------|------------|
| Erebor | TwoHanded +20, Athletics +10, OneHanded +10, Polearm +10, Throwing +10 | Riding -20 |
| Iron Hills | TwoHanded +20, Polearm +20, OneHanded +15, Athletics +10, Throwing +10 | Riding -5 |
| Gondor | OneHanded +10, Athletics +5, Riding +5, TwoHanded +5, Polearm +5 | Throwing -10 |
| Rohan | Riding +20, Polearm +10, Throwing +2 | Crossbow -10, Athletics -5, Bow -5 |
| Isengard | TwoHanded +15, Polearm +15, Athletics +10, OneHanded +10, Crossbow +10, Throwing +10 | Riding +5 |
| Mordor | TwoHanded +5, Throwing +5 | Athletics -5, Riding -5, Polearm -5, Bow -5, Crossbow -5 |
| Harad | Riding +15, Bow +10, OneHanded +5 | TwoHanded -10, Polearm -5 |
| Rhun | Riding +18, Polearm +15, Athletics +5 | Bow -10, Crossbow -10, Throwing -5 |
| Dunland | Athletics +20, Throwing +15, OneHanded +5, TwoHanded +5 | Riding -5 |
| Dol Guldur | OneHanded +5, TwoHanded +5 | Riding -10, Bow -5, Crossbow -5 |
| Gundabad | TwoHanded +10, Athletics +5, Polearm +5, Throwing +5 | Bow -10, Crossbow -10, Riding -5 |
| Rivendell | All combat +30-40 (elite High Elves) | — |
| Mirkwood | Bow/Crossbow/Throwing +50, Athletics +45, OneHanded +40 (elite) | — |
| Lothlorien | Bow/Crossbow/Throwing +35, Athletics +35, Polearm +30, OneHanded +30 (elite) | — |
| Umbar | Athletics +10, OneHanded +10, TwoHanded +5 | Riding -15 |

**Files modified:** 13 troop XMLs + `tools/rebalance_troops.py`
**Troop count:** 545 troops across Dol Guldur (50), Dunland (45), Erebor (47), Gondor (71), Gundabad (30), Harad (29), Isengard (38), Mirkwood (17), Mordor (28), Rhun (91), Rivendell (28), Rohan (57), Umbar (14)

---

### Website — Culture Theming & Troop Balancing Page

Updated the taommod website with culture-specific color theming across all data tables and the troop balancing page.

**Troop Balancing Page (`troop-balancing.astro`):**
- Renamed all 15 cultures to lore-accurate names (Gondorians, Rohirrim, Longbeards, Ironfists, Noldorin, Silvan, Sindar, Uruk-Hai, Mordor Orcs, Gundabad Orcs, Goblins, Haruze, Easterlings, Dunlending, Umbarean)
- Added culture-colored backgrounds to comparison table cells and culture detail cards
- Updated identity descriptions with lore text (Gondor regional specializations, Erebor/Iron Hills weapon preferences, Rohan cavalry focus, evil faction creature notes)
- Culture badges styled with per-culture colors

**Culture Color Scheme (across all pages):**
- Erebor: blue-gold `#6a9fd4` / `rgba(106, 159, 212)`
- Iron Hills: dark red/clay `#a04030` / `rgba(160, 64, 48)`
- Gundabad: cool gray `#7a8a9a` / `rgba(122, 138, 154)`
- Harad: red `#c43c3c` / `rgba(220, 20, 60)`
- Easterlings/Rhun: golden `#d4a24c` / `rgba(212, 162, 76)`
- Other cultures retain established colors

**Files modified:** `src/styles/global.css` (data-table culture row colors), `src/pages/mod-info/troop-balancing.astro` (full page overhaul)

---

## 2026-03-06

### Banner Injection Feature

Ported LOTRAOM's Banner Injection system to TAOM for Bannerlord 1.3. Re-applies custom `banner_key` values to Kingdom and Clan objects on every session launch, preventing banner reversion on save/load cycles. Leverages 1.3 public setters (no reflection needed).

**C# Implementation (18 files):**
- `BannerInjectionService` — core injection logic: loads config, compares runtime banners to XML, sets + invalidates visuals for mismatches
- `BannerExclusionService` — tracks player-modified banners via `IDataStore` persistence to avoid overwriting player edits
- `BannerConfigProvider` — parses `banner_key` from 4 sources: `taom_spkingdoms.xml`, `spkingdoms.xslt`, `characters/clans.xml`, `spclans.xslt`. Handles both inline XML attributes and `xsl:attribute` XSLT patterns
- `BannerInjectionBehavior` — thin `CampaignBehaviorBase`, fires injection on `OnSessionLaunchedEvent`
- `IKingdomBannerAdapter` / `KingdomBannerAdapter` — wraps `Kingdom.All`, `Kingdom.Banner` setter, visual invalidation
- `IClanBannerAdapter` / `ClanBannerAdapter` — wraps `Clan.All`, `Clan.Banner` setter, ruling clan detection
- `GauntletBannerEditorScreen_OnDone_Patch` — Harmony postfix detects player banner edits, marks clan as player-modified
- `BannerInjectionIoC` — DryIoc registration for all banner services
- 8 `BannerConfigProviderTests` + 5 `BannerExclusionServiceTests` + 13 `BannerInjectionServiceTests` = 26 new tests

**XSLT Changes:**
- Added vanilla `banner_key` attributes to all 73 clan templates in `spclans.xslt` (across 8 culture groups) in anticipation of future clan rework
- Each template excludes `banner_key` from pass-through to prevent duplication

### Notable NPCs — Culture-Specific Notables

Replaced vanilla Empire notable NPCs with culture-specific notables for all 10 custom cultures. Previously all settlements (including orc/elf/dwarf) spawned human Empire notables as merchants, artisans, preachers, etc.

- Created 26 notary NPCs per culture matching vanilla occupation distribution: 10 Merchant, 3 Preacher, 2 Artisan, 6 GangLeader, 2 RuralNotable, 3 Headman
- Each NPC has correct race, `is_template="true"`, varied voices, traits, and culture-appropriate equipment
- Updated `taom_spcultures.xml` — replaced `spc_notable_empire_*` references with culture-specific `spc_notable_{culture}_*` in all 10 `notable_templates` blocks + culture-level `merchant_notary`/`artisan_notary`/`preacher_notary`/`rural_notable_notary` attributes
- Created `characters/npcs_lothlorien.xml` and `characters/npcs_umbar.xml` (new files — these cultures had no NPC file)
- Registered new files in `SubModule.xml`

### XSLT Fixes

- Fixed XSLT attribute filters for aserai→Harad, vlandia→Rohan, khuzait→Rhun — replaced 60+ attribute exclusion filters with `<xsl:apply-templates select="@*"/>` passthrough pattern
- Fixed child element duplication across all 4 XSLT cultures — `vassal_reward_items`, `banner_bearer_replacement_weapons`, `default_policies`, `male_names`, `female_names`, `clan_names` now excluded from passthrough
- Fixed 23 corrupted accent characters in `taom_wanderers.xml` (double-encoded UTF-8: `Ã»`→`û`, `Ãª`→`ê`, `Ã³`→`ó`, `Ã¡`→`á`, `Ã­`→`í`)

### Faction & Culture Strings

Added comprehensive faction/culture strings for all 16 cultures, fixing "ERROR: Text with id str_faction_ruler doesn't exist!" and replacing vanilla culture names/descriptions with LOTR-themed content.

- Created `taom_module_strings.xml` — 272 strings across 17 types for 16 cultures:
  - Faction strings (12 types): ruler titles, noble titles, faction adjectives, formal/informal names
  - Culture descriptions (16): LOTR lore text for character creation
  - Culture rich names (16): e.g. "Rohirrim", "Dwarves", "Galadhrim"
  - Culture adjectives (16): e.g. "Dunlending", "Rohirric", "Dwarven"
  - Player parent names (32): LOTR-themed father/mother names for character creation
- Created `module_strings.xslt` — removes vanilla strings for 6 remapped cultures (empire→Dunland, vlandia→Rohan, battania→Khand, khuzait→Rhûn, aserai→Harad, sturgia→Dale)
- Updated `SubModule.xml` — registered both new GameText files

### Wanderer/Companion System — Complete Implementation

Implemented a full companion/wanderer system for all 14 kingdoms. Wanderers spawn in taverns, can be recruited, and have unique backstories, skills, and equipment.

**Batch 1 — LOTRAOM Conversion (6 kingdoms, 69 wanderers)**
- Extracted and converted wanderer data from LOTRAOM source files
- Gondor (13), Mordor (15), Gundabad (10), Isengard (10), Erebor (12), Rohan (9)
- Created `taom_wanderers.xml` — NPCCharacter templates with `occupation="Wanderer"`
- Created `taom_wanderer_skill_sets.xml` — 69 SkillSet definitions
- Created `taom_wanderer_equipment.xml` — 6 kingdom-specific companion equipment rosters
- Created `taom_wanderer_strings.xml` — 530 backstory dialogue strings
- Created `tools/extract_wanderers.py` — extraction/conversion script

**Batch 2 — Generated Wanderers (8 kingdoms, 80 wanderers)**
- Generated wanderers for kingdoms without LOTRAOM data
- Rivendell (10), Mirkwood (10), Lothlorien (10), Dol Guldur (10), Dunland (10), Harad (10), Rhun (10), Umbar (10)
- 10 archetype roles per kingdom: Engineer, Warrior, Scout, Healer, Trader, Rogue, Tactician, Smith, Cavalryman, Archer
- Added 80 NPCs, 80 skill sets, 8 equipment rosters, 640 backstory strings
- Created `tools/generate_batch2_wanderers.py` — generation script

**Culture Wiring**
- Updated `taom_spcultures.xml` — renamed `notable_templates` to `notable_and_wanderer_templates` for all 10 custom cultures, added wanderer template references
- Updated `spcultures.xslt` — replaced vanilla wanderer passthrough with LOTR wanderer references for Rohan (vlandia), Dunland (empire), Harad (aserai), Rhun (khuzait)
- Registered 4 new XML files (wanderers, skill sets, equipment, strings) in `SubModule.xml`

### Phase 1 Completion — Remaining Kingdoms

**Isengard**
- Added 4 militia troops (spearman, archer/crossbow, veteran variants) with uruk_hai race
- Added 46 NPCs (`npcs_isengard.xml`) — townsman, villager, guard, merchant, tavern staff, etc.
- Added 10 equipment rosters (`taom_equipment_sets_isengard.xml`) — 5 battle + 5 civilian
- Added 12 party templates in `taom_partyTemplates.xml`
- Wired all Isengard-specific refs in `taom_spcultures.xml` (replaced Sturgia placeholders)
- Added 6 education character templates + 98 education equipment templates

**Mordor, Rohan, Dunland, Harad, Rhun**
- Added 46 NPCs each (`npcs_{kingdom}.xml`)
- Added 10 equipment rosters each (`taom_equipment_sets_{kingdom}.xml`)
- Added militia troops for Rohan, Dunland, Harad, Rhun (4 per kingdom)
- Added 12 party templates each for Harad, Rhun, Isengard
- Wired culture-specific refs in `taom_spcultures.xml` and `spcultures.xslt`
- Created `tools/generate_xslt.py` — XSLT generation script

### Bug Fixes

- Fixed XSLT AVT conflict — escaped 469 `{=id}text` localization strings in literal element attributes as `{{=id}}text` to prevent XPath evaluation errors during XSLT compilation
- Fixed duplicate item `dunland_caerdh_pauldron__elite_a` in LOTRLOME_Armory `shoulder_armors.xml`
- Fixed duplicate monster `uruk_settlement` in LOTRLOME_Armory `monsters.xml`

---

## 2026-03-05

### Phase 1 — Kingdom Infrastructure (First Batch)

**NPC Characters**
- Created NPC files for Erebor, Rivendell, Mirkwood, Dol Guldur, Gundabad (`npcs_{kingdom}.xml`)
- Each kingdom has ~46 NPCs: townsman, villager, guard, merchant, tavern staff, etc.

**Equipment Rosters**
- Created per-kingdom equipment sets for Erebor, Rivendell, Mirkwood, Dol Guldur, Gundabad
- 5 battle + 5 civilian templates per kingdom using kingdom-specific armor and weapons

**Party Templates**
- Created `taom_partyTemplates.xml` with initial party template definitions

**Education Templates**
- Created `taom_education_character_templates.xml` and `taom_education_equipment_templates.xml`

**Troop Updates**
- Added militia troops for Erebor, Rivendell, Mirkwood, Dol Guldur, Gundabad
- Updated existing troop files with correct body properties and militia references

**Culture Wiring**
- Updated `taom_spcultures.xml` with kingdom-specific NPC, troop, equipment, and party template references

### Other

- Added Warsails naval mod integration guide (`docs/warsails-custom-map-guide.md`)
- Settlement data backup

---

## 2026-02-14

### Settlement Names

- Created `tools/Apply-SettlementNames.ps1` — script to apply LOTR settlement names from mapping file
- Applied LOTR names to `settlements.xml`

### Battle Scene Diagnostics

- Added `MBMapScene_GetBattleSceneIndexMap_Patch` — diagnostic patch for index map retrieval
- Added `MapScene_Load_DiagnosticPatch` — diagnostic patch for battle scene loading

---

## 2026-02-11

### Battle Scenes

- Implemented battle scene system (`sp_battle_scenes.xml`)
- Added `Campaign_InitializeScenes_Patch` — Harmony patch to load custom battle scenes
- Added guards and error handling for map loading

### Settlements & Locations

- Updated settlement data and clan/kingdom starting positions
- Updated `spclans.xslt` and `spkingdoms.xslt` with settlement references
- Fixed typo in `settlements.xml`

---

## 2026-02-10

### Settlement Tooling

- Created `tools/Settlement-Breakdown.ps1` — script to categorize and summarize settlements
- Created `tools/Generate-SceneEntitiesDoc.ps1` — script to generate scene entity documentation from scene file
- Updated `docs/scene-entities.md` with generated documentation
- Created `settlements.xslt` — XSLT stylesheet to transform and filter Settlement elements
- Updated settlement data

---

## 2026-02-09

### Settlements

- Added Far Harad region support with new castle and village entries
- Updated gate positions for Far Harad settlements
- Updated scene entity counts and corrected entity names in documentation

### Documentation

- Added `docs/ai-includes/agent-teams.md` — guide for using agent teams for parallel work
- Updated `CLAUDE.md` with agent teams section

---

## 2026-02-07

### Settlements

- Created initial `settlements.xml` with 658 settlements generated from scene.xscene
- Created `tools/Generate-Settlements.ps1` — settlement generation script from scene data
- Created `docs/scene-entities.md` — scene entity reference documentation for towns, castles, villages

---

## 2026-01-30

### Bug Fixes

- Updated Gondor male names for accuracy and consistency

---

## 2026-01-29

### Race System — HeroRace Feature

Implemented custom race handling for non-human characters (dwarves, orcs, uruk-hai).

**Core Infrastructure**
- Created `RaceManager` — domain service for race position configuration
- Created `ReflectionService` — infrastructure service for accessing internal TaleWorlds types
- Created `PathService` / `ModulePathAdapter` — module path resolution
- Created `FaceGenAdapter` / `IFaceGenAdapter` — adapter for sealed FaceGen types
- Created `FileLogger` — file-based logging

**HeroRace Feature**
- `CharacterSpawnerService` — handles character spawning with correct race
- `CharacterTableauService` — handles character portrait rendering with race
- `RacePositionConfigurationService` — manages per-race eye height and position config
- `EyeHeightAdjustmentHook` — adjusts eye height based on race
- `RacePersistenceService` / `RacePersistenceBehavior` — saves/loads race data with campaigns
- `HeroRosterAdapter` — adapter for hero roster access

**Harmony Patches**
- `CharacterSpawner_InitWithCharacter_Patch` — prefix patch for character spawning
- `CharacterTableau_RefreshCharacterTableau_Patch` — patch for portrait rendering
- `CharacterTableau_SetRace_Patch` — patch for race assignment
- `FaceGen_GetBaseMonsterFromRace_Patch` — patch for monster/race resolution
- `ActionSetCode_GenerateActionSetNameWithSuffix_Patch` — action set generation patch

**Tests**
- Added unit tests for `RaceManager`, `ReflectionService`, `FileLogger`
- Added tests for `RacePersistenceBehavior`, `RacePersistenceService`

**Race Data**
- Created `Races/action_sets.xml` — custom action sets for non-human races
- Created `Races/monsters.xml` — monster definitions for custom races
- Created `Races/skins.xml` — skin definitions for race visual data
- Created `TAOM_bodyproperties.xml` — body property templates for all kingdoms

**Voice System**
- Added voice definitions for Dwarf, Uruk-hai, and Uruk races
- Added ~430+ sound files (WAV/MP3) for battle cries, pain, death, commands
- Created `module_sounds.xml` — sound module registration

**Troop Race Attributes**
- Added `race="dwarf"` to Erebor/Iron Hills troops
- Added `race="orc"`, `race="uruk_hai"` to Mordor, Gundabad, Isengard, Dol Guldur troops

---

## 2026-01-28

### Lords, Clans & Heroes

- Added clans, heroes, and lords for Gondor, Rohan, Rhun, and other kingdoms (`characters/clans.xml`, `characters/heroes.xml`, `characters/lords.xml`)
- Added female Isengard and Umbar lords for child generation
- Added spouses for existing lords in Empire and Vlandia factions
- Fixed faction names in `spclans.xslt` to include diacritics (e.g., Rhûn)
- Fixed clan cultures from Gondor/Mordor to Empire where needed
- Updated banner keys and kingdom color attributes
- Updated starting positions for cultures and fixed Dol Guldur owner
- Created `scripts/replace_equipment_templates.py` — replaces custom LOTRAOM equipment templates with vanilla equivalents

### Troop Trees

- Added initial troop XML files for all 14 kingdoms
- Refactored troop files: removed redundant race attributes, fixed encoding issues
- Moved troop files from root `ModuleData/` to `ModuleData/troops/` subdirectory
- Fixed invisible characters in XML declarations
- Registered all troop XML nodes in `SubModule.xml`

### Race Infrastructure

- Created `Races/action_sets.xml`, `Races/monsters.xml`, `Races/skins.xml`
- Created `tools/Generate-ActionSets.ps1` — action set generation script
- Created `project.mbproj` — module project file

---

## 2026-01-27

### Kingdoms & Cultures

- Created `taom_spcultures.xml` — custom culture definitions for 10 new kingdoms (Gondor, Mordor, Gundabad, Isengard, Erebor, Rivendell, Mirkwood, Dol Guldur, Lothlorien, Umbar)
- Created `taom_spkingdoms.xml` — custom kingdom definitions
- Added initial clan and hero data
- Created `scripts/lowercase-pngs.ps1` — utility to rename PNG files to lowercase

---

## 2026-01-25

### Lords Migration

- Enhanced lords data with skill templates and face tags
- Consolidated lords XSLT (`lords.xslt` replacing `splords.xslt`)
- Created `scripts/add-face-tags.ps1` and `scripts/add-skill-templates.ps1`

---

## 2026-01-24

### Project Foundation

- Initial commit: minimal Bannerlord 1.3 mod skeleton
- Set up project structure: `Main/`, `TAOM.Tests/`, `docs/`, `scripts/`
- Created `CLAUDE.md` — project rules and AI instructions
- Created `README.md`
- Created `build.ps1` — build script
- Set up MSTest + NSubstitute test project

### XSLT Transformations

- Created `spkingdoms.xslt` — renames 8 vanilla kingdoms to LOTR equivalents
- Created `spcultures.xslt` — renames 6 vanilla cultures to LOTR equivalents with custom name lists
- Created `spclans.xslt` — renames 73 vanilla clans to LOTR equivalents
- Created `lords.xslt` — transforms 380 lords (names, skills, traits, BodyProperties)
- Created `heroes.xslt` — transforms 415 hero biographies

### Characters

- Created `characters/lords.xml` — 504 new LOTR lords not in vanilla
- Created `characters/heroes.xml` — new LOTR heroes not in vanilla
- Created `characters/clans.xml` — ~101 new LOTR clans not in vanilla
- Created lord extraction and XSLT generation scripts

### Documentation

- Created Architecture Decision Records (ADRs 001-009)
- Created AI include docs: architecture, patterns, TDD, research workflow, code quality, security
- Created migration documentation: tracking, XML schema changes, v1.3 API changes, ROT-Core analysis
- Created testing guide
