# TAOM Knowledge Index

**Start here.** This file is the topical map across [docs/](.) — read it instead of grepping when you need to find the canonical doc for any TAOM system. CLAUDE.md describes the working rules and architecture stance; this file maps the persistent knowledge.

## Quickstart paths

| If you are... | Go to |
|---|---|
| A fresh Claude session orienting itself | [CLAUDE.md](../CLAUDE.md) → this file → relevant topical section below |
| Looking for one feature's canonical docs | The "By major system" section below, or directly [docs/features/<name>.md](features/) |
| Debugging a regression | [reviews/](reviews/) (search for `rca-<feature>-<date>.md`), then auto-memory `feedback_<symptom>.md` files |
| Authoring a new feature module | [ai-includes/architecture.md](ai-includes/architecture.md) + [ADR-002](adrs/002-thin-entry-points.md) + [ADR-007](adrs/007-adapter-pattern.md), then pick the nearest existing feature doc as a template |
| Authoring a new culture's armor + troops end-to-end | [ai-includes/new-culture-authoring.md](ai-includes/new-culture-authoring.md) |
| Adding or fixing lord skills + traits (any culture) | [ai-includes/lord-skills-authoring.md](ai-includes/lord-skills-authoring.md) |
| Adding a rideable creature mount end-to-end (assets → XML → C# → validation) | [ai-includes/creature-mount-authoring.md](ai-includes/creature-mount-authoring.md): elephant+spider-distilled; the 1.4.6 lookup-hardening rules + 18-gotcha index. A **reskin of a vanilla skeleton skips Phases 1 to 5 entirely** (no clips, no action/usage XML, no animation data at all): worked example [features/war-ram.md](features/war-ram.md) |
| Researching a TaleWorlds API before editing | [ai-includes/taleworlds-research-guide.md](ai-includes/taleworlds-research-guide.md) + `pwsh tools/taom-src.ps1 path <Type>` |
| **Editing the mod data without writing C#** (balancing, adding or removing troops, armour, weapons, lords, cultures, kingdoms, settlements) | [modding/README.md](modding/README.md) and its 38 chapters |
| Adding text the player will read | [localization/TRANSLATOR_GUIDE.md](localization/TRANSLATOR_GUIDE.md) + [features/localization.md](features/localization.md) |
| Closing out a feature (build → review → ship) | CLAUDE.md "Completion Workflow" + [reviews/REVIEW-GUIDE.md](reviews/REVIEW-GUIDE.md) |
| Checking what was scored on each Codex review | [reviews/REVIEW-LOG.md](reviews/REVIEW-LOG.md) |

## By major system

### Character, race, body, & character creation
- [culture-playability-wiring](features/culture-playability-wiring.md): the 14-row checklist separating a *selectable* culture from a *playable* one, why `is_main_culture` is not the CC gate (vanilla hardcodes six StringIds), and the three failure modes that ship silently: no CC equipment, no starting denars, no eligible career. Also the **party-template binding contract**: the eight engine-read attributes that decide which troops a culture actually spawns, why a `spcultures.xslt` block silently inherits Calradia for anything it never names, the caravan child elements that union rather than replace, and the two bindings that are unguarded crash surfaces rather than merely wrong troops
- [character-creation](features/character-creation.md) — race-restricted CC dropdown, action_set requirements, narrative-stage flow, vanilla-aligned bonus budget (skill/attribute/focus per stage)
- [character-creation-body-properties](features/character-creation-body-properties.md) — per-culture default body properties on CC screen (Patch29)
- [character-selection](features/character-selection.md) — transpiler-driven race fallback in CC
- [race-age-system](features/race-age-system.md) — race-appropriate lifespans (elven immortality, dwarf/hobbit aging) via TaomAgeModel + TaomPregnancyModel
- [hero-race](features/hero-race.md) — race assignment + persistence on Hero; capture refuses to run when the FaceGen registry holds fewer than 2 races, because a co-op host without TAOM's modules reads every hero back as race 0 and that map rides the save transfer to fully-raced clients
- [offspring-race-inheritance](features/offspring-race-inheritance.md) — child race from parent races, race-aware hero creation defaults
- [initial-child-generation](features/initial-child-generation.md) — campaign-start child rolls
- [no-mount-cultures](features/no-mount-cultures.md) — suppress narrative horse crash on no-mount cultures (Patch20)
- [native-skin-fixes](features/native-skin-fixes.md) — managed wrapper for `TAOM.NativeSkinFixes.dll` (covers_head morph + hair/beard cloth sim)
- [kingdom-voices](features/kingdom-voices.md): per-race combat voice sets (barks, pain, death, formation shouts). Voice binds to **race, never culture**, so the Mannish kingdoms cannot be separated without C#; the three binding routes, the 62 voice types, and why TAOM's loose-`.wav` path needs no FMOD bank. Records the live race-to-voice table with its standing defects: seven races bound to nothing, and two whose adult female skin falls back to a vanilla voice (the earlier "diluted pool" reading was retracted 2026-08-25). Also records the 2026-09 clip-length defect, where multi-line compilations bound to native-fired slots made dwarves speak every 2 to 4 seconds, now gated by `tools/audit_voice_clip_lengths.py`
- [gui-sprite-system](features/gui-sprite-system.md) — sprite atlas conventions, verification before reference, the **decompile-verified sprite-bake pipeline** (no `pack0.tpac`; per-category `AssetSources` PNG + `Assets/_tex.tpac` + manifest) + end-to-end **Adding / Verifying a sprite** workflow (a new sprite needs the generator AND a render check — **baked ≠ visible**)

### Combat, AI, & battle
- [combat-mechanics](features/combat-mechanics.md) — TaomCombatMechanicsModel (single AgentApplyDamageModel slot): skill-based crush-through, monster/orc CTB, creature cleave + stagger immunity, weight-driven charge knockdown (`Monster.Weight` ratios), shield penetration, per-race modifier table
- [advanced-combat](features/advanced-combat.md) — SpatialGrid, BoneCollision, CustomAttacks subsystems
- [smart-cavalry-ai](features/smart-cavalry-ai.md) — player-team cavalry state machine (Form → Charge → PassThrough → Reform)
- [mixed-formations](features/mixed-formations.md) — heterogeneous formation layout system
- [banner-bearers](features/banner-bearers.md) — formations raise their faction standard; bearers keep their race. Drives the engine's native `BannerBearerLogic` via `TaomBattleBannerBearersModel` + one deployment-window `SetFormationBanner` call
- [companion-tactics](features/companion-tactics.md) — companion-driven formation overrides; `CancelStanceOnMove` postfix
- [warg-combat](features/warg-combat.md) — BT elements, WargAttackService, WargMissionBehavior
- [spider](features/spider.md) — spider creature combat (PAUSED: native render AV; fix = wolf's public SpawnMonster + un-split mesh)
- [elephant](features/elephant.md) — war-elephant trample + tusk auto-attacks + mount-lock (mount-lock + mechanic ported from ADOD_Beasts; cadence + per-kind randomized damage are TAOM's own rebalance) + [howdah-crew-mechanism](features/elephant/howdah-crew-mechanism.md) (UsableMachine crew platform; not yet ported)
- [war-ram](features/war-ram.md): the Dwarves' rideable war ram (#515) and **TAOM's first horse-skeleton reskin**. Both ram meshes and all eight bardings are skinned to the stock vanilla horse rig bone for bone, so the Monster is the `horse_2` shape (`base_monster="horse"` + `action_set="as_horse"` + four tuning attributes), Phases 1 to 5 of [creature-mount-authoring](ai-includes/creature-mount-authoring.md) are skipped and no animation data is authored anywhere. It inherits `Mountable`, `family_type=1`, `monster_usage="horse"` and **all twelve rein attributes**, the only TAOM mount with vanilla's complete rein surface, so it sidesteps that doc's gotcha #18 instead of adding to it. The trade: a reskin SHARES its action vocabulary with the engine, so "our code never fires this" stops implying "nothing fires this", which got the attack clip wrong twice. External data plane (untracked `LOTRLOME_Armory`): [lotrlome-war-ram-changes](reference/lotrlome-war-ram-changes.md)
- [ADOD_Beasts architecture and TAOM port](reference/adod-beasts-architecture-and-taom-port.md) — **the whole of ADOD_Beasts end-to-end** (lifecycle + the WHY) + line-by-line TAOM port comparison across all 4 subsystems; the 1.2.12→1.4.5 drift catalogue. Read this before re-decompiling ADOD_Beasts. Provenance row: [provenance-register.md](reference/provenance-register.md)
- [troop-weight-system](features/troop-weight-system.md): the elite tax: heavy troops cost more party capacity. Enforced by deflating the party-size limit in `TaomPartySizeModel`; displayed as capacity used (`19 / 20`) by six `Patch17_TroopWeight` postfixes. Counts read raw everywhere
- [battle-balance](features/battle-balance.md) — TaomMilitaryPowerModel, TaomCombatSimulationModel
- [battle-scenes](features/battle-scenes.md) — battle scene system (Patch0, currently DISABLED)
- [worldmap-battle-scene-grid](reference/worldmap-battle-scene-grid.md) — how field-battle terrain is chosen; the `worldmap_battle_scene_grid` texture is **baked into `Main_map`**, not loaded by filename; re-author + bake workflow
- [main-map-vista](reference/main-map-vista.md): the distant terrain drawn beyond the 1600x1600 node bounds, i.e. what turns white or checkerboard when zoomed out. All 14 `vista_*` attributes on the live `TAOM_Map` `<terrain>` element mapped to their Modding Kit fields, plus a 5-map reference table in which **`vista_normalmap` is empty in every one**. Vanilla drives its vista from a `.gts` tileset TAOM does not ship, so vanilla's values are not copyable. Records that Texture Inspector import flags are NOT the lever (a re-import changes only 103 GUID bytes per `.tpac`, settings survive intact) and that the Kit's own single-step `SceneObj/Backups/Main_map/` is the fastest forensic anchor for any scene regression
- [custom-battles](features/custom-battles.md) — TAOM factions/commanders/troops in custom battles (Patch19)

### Career & progression
- [career-system](features/career-system.md) — 50 careers × 16 cultures, XML-driven defs, ability + passive systems, career screen UI
- [career-cc-selection](features/career-cc-selection.md) — CC career-selection stage + archetype-driven starting equipment
- [troop-progression](features/troop-progression.md) — tier-by-tier upgrade rules, MaxCharacterTier 10
- [troop-tree-revamp](features/troop-tree-revamp.md) — multi-culture troop roster authoring discipline
- [black-numenorean](features/black-numenorean.md): Mordor's first human troops and first horse cavalry, a 13-troop T5-T9 line plus its 113 Armory entries. Records why a corrupted-Man troop carries **no** `race=` attribute (absent means index 0 means `human`), the `level = 5T + 1` tier formula, why the line is deliberately AI-only and what that costs in the recruitment-reachability test, and the three spec errors that had to be worked around (no universal `_slim`, cloth-only T7 capes, `_a`-only collision bodies)
- [volunteer-recruitment](features/volunteer-recruitment.md) — per-settlement / clan / culture recruitment pools (TaomVolunteerModel)
- [tavern-mercenaries](features/tavern-mercenaries.md) — culture-specific `<basic_mercenary_troops>`: how the town's daily offer is rolled (70/30 split, upgrade walk, tier-inverse stack size), and why the hires are dedicated leaf `*_merc` copies of each culture's rarest recruitment-pool troops
- [prisoner-recruitment](features/prisoner-recruitment.md) — no morale lost recruiting a prisoner of your own faction or alignment side (TaomPrisonerRecruitmentCalculationModel)
- [equip-presets](features/equip-presets.md) — save/load equipment preset overlay on inventory
- [lord-skills](features/lord-skills.md) — lore-driven SkillSets for every TAOM lord (~880 NPCs, 17 cultures, 35 archetypes); authoring guide [lord-skills-authoring.md](ai-includes/lord-skills-authoring.md)
- [enlistment](features/enlistment.md) — serve as a common soldier under a lord (#375): persisted service state machine (party presence is an OUTPUT, never state), single discharge pipeline that always restores presence, commander-funded wages with arrears, ranks/trust/reputation, 13 field duties over 5 mechanics + 11 interactive + 3 incidents, per-culture-per-rank issued armour. Patch66 menu guard
- [field-commission](features/field-commission.md) — promote a proven troop into a named companion (#376): fair-fight merit per troop type, spent only on a completed promotion, race allow-list, deferring companion cap, level-budgeted skills, suppressed while enlisted. MCM group with a master off switch + a diagnostics trace; never yet run in a live game

### Equipment & armor authoring
- [gondor-armor-revamp](features/gondor-armor-revamp.md) — Gondor armor authoring + roster swap (issue #99)
- [gondor-ithilien-ranger](features/gondor-ithilien-ranger.md) — Ithil Guard conditional + Ranger line
- [multi-culture-armor-revamp](features/multi-culture-armor-revamp.md) — Mordor/Isengard/Dol Guldur/Erebor/Rhun armor pass (issue #211)
- [weapon-xml-pipeline](features/weapon-xml-pipeline.md) — weapon XML generation + rebalancing (automated)
- [weapon-creation-workflow](ai-includes/weapon-creation-workflow.md) — manual Step A–Z guide: FBX → tpac → 4 XML files → validate → in-game (bows/shields = no decimals)
- [item-usage-features](reference/item-usage-features.md) — how `excluded_item_usage_features` picks a crafted weapon's animation set: full token table, the swing-only-head rule (maces need `thrust` excluded, axes don't), the reachable-union audit method
- [dale](features/dale.md) — Dale culture authoring (armor, troops, Lake-Town recruitment override) — proof-of-life for full-culture authoring
- [tournament-armor-assignment](features/tournament-armor-assignment.md) — per-participant culture armor in TaomTournamentModel
- [starting-equipment-tuning](features/starting-equipment-tuning.md) — keep CC starter gear cheap to resell: how item value actually works (`DefaultItemValueModel` exponential `2.75^tier` + explicit `value=` override), the per-culture `starter_*` clone-with-low-stats pattern (5/7/9 anchors), the generator + roster-wirer tools
- **Shield beside an unusable weapon (the silent one)**: a crafted weapon takes its usages from `WeaponDescription` membership and the FIRST match wins the primary, so a polearm absent from `OneHandedPolearm` resolves `requires_no_shield` and a shield-carrying troop simply never draws it after spawn. No error, no log, and the flag's only managed consumer is a tooltip, so the acting code is native. Shipped three times (#445, #449, the Black Numenorean lance) and a player found each. Gate: `python tools/audit_polearm_shield_parity.py`, run automatically by the PostToolUse hook `.claude/hooks/check-polearm-shield-parity.sh` (2026-08-20) because it needs the game install and CI cannot. Registration: `tools/register_one_handed_polearms.py`, never a hand edit of the Armory XSLT. Lessons: [data-content-cultures](reviews/lessons/data-content-cultures.md)
- [armory-shield-audit](reference/armory-shield-audit.md) — shield `item_usage` reference: `hand_shield` vs `shield` grip, the offhand-bone flag each requires, block-arc cost, and the two `body_name`s that look mistyped but must not be corrected
- See also: CLAUDE.md "Equipment & Armory" for canonical-folder table per item-ID prefix

### Sieges
- [siege](features/siege.md) — vanilla siege overrides
- [siege-defense](features/siege-defense.md) — watched-faction siege defense events with CampaignTime deadline
- [siege-trebuchets](features/siege-trebuchets.md) — TaomSiegeEventModel: defender Trebuchet option
- [siege-dismount](features/siege-dismount.md) — player dismount on siege entry; modifier-preserving horse storage

### Economy, settlements, resources
- [special-resources](features/special-resources.md) — 11 resources × 18 kingdoms, troop costs, save-compat (Patch26); earning is keyed on `MapEvent.PlayerSide == WinningSide` (participation, not command — the old leader-hero gate paid nothing to a player fighting inside another lord's army) and suppressed on a dedicated server
- [elite-emissary](features/elite-emissary.md) — buy a faction's elite troops at its capital for that faction's special resource; the sale is declined before charging on a non-authoritative co-op peer, where the resource charge would persist but the purchased troops would not survive the next resync
- [culture-marketplace](features/culture-marketplace.md) — daily LOTRLOME item injection by owner culture
- [settlement-guards](features/settlement-guards.md) — per-settlement guard pools, clan/culture fallback (Patch28)
- [settlement-nameplate-fade](features/settlement-nameplate-fade.md) — distance-based nameplate fade (Patch38)
- [revolt-tuning](features/revolt-tuning.md) — JSON-tunable revolt soft-nerf, TaomSettlementLoyaltyModel
- [culture-conversion](features/culture-conversion.md) — conquered fiefs gradually adopt the owner's culture: Settlement.Culture flip + notable replacement (#325) + converted-recruitment branch
- [settlement-food](features/settlement-food.md): why 70 of 72 towns started food-negative and could not hold a garrison (#546). Vanilla consumption is linear in prosperity while production is flat, so TAOM's high-prosperity map starves by arithmetic. Adds the prosperity-scaled `hinterlandFoodPerProsperity` production term (and its strict `< 1/prosperityFoodDivisor` invariant) and ships tuned defaults instead of vanilla ones. The original Troop-Weight garrison correction is an inert no-op today
- [settlement-economy](features/settlement-economy.md) — tunable town market-gold regen (drained markets recover), TaomSettlementEconomyModel (#317)
- [settlement-building-levels](features/settlement-building-levels.md) — lore+role starting building levels for all 221 towns/castles (data pass on the LIVE settlements.xml; author/dump/apply tooling)
- [caravan-trade](features/caravan-trade.md) — AI/player caravans range further, trade across the war, carry fuller baskets; 4 postfixes (Patch59) + TaomCaravanModel (#329)
- [economy-diagnostics](features/economy-diagnostics.md) — read-only instruments for the "broke town / parked caravan" pair: `taom.print_town_ledger` attributes a town's market-gold movement by flow (one `Patch68` recorder on `ChangeGold`, the pool's sole mutator, + 4 outermost-wins tags), `taom.print_caravans` names which engine gate holds each caravan. No gameplay change (#391)
- [startup-resources](features/startup-resources.md) — per-culture player startup gold/items
- [cultural-feats](features/cultural-feats.md) — 16 culture-feat GameModel overrides (Patch18)
- [party-template-sizing](reference/party-template-sizing.md): what `max_value` in `taom_partyTemplates.xml` actually controls. The max sum is the SPAWN ceiling (expected roster = the midpoint of the min and max sums, ratio drawn per party and independent of the template), not the steady-state size, which belongs to `PartySizeLimit` / `TaomPartySizeModel`. Current per-culture targets, which cultures share another's templates, the new-game top-up that also weights WHICH troops get added, and `tools/rebalance_party_template_maxes.py`

### Faction, kingdom, & diplomacy
- [diplomacy](features/diplomacy.md) — TaomDiplomacyModel for LOTR faction relationships
- [kingdom-creation](features/kingdom-creation.md) — TAOM kingdom + clan + lord authoring
- [lord-spawn-guard](features/lord-spawn-guard.md) — Patch65 + the Variag settlement retag: a landless culture CTDs the daily clan tick
- [faction-map](features/faction-map.md) — campaign map faction rendering
- [clan-heraldry](features/clan-heraldry.md): per-clan `color`/`color2`, which is the battlefield armour tint via Patch23, plus per-clan party templates. `clan_heraldry/*.json` + `tools/generate_clan_heraldry.py`, whose Gondor and Mordor specs have drifted and must not be re-applied
- [minor-factions](features/minor-factions.md) — minor factions catalog + rules
- [alignment-aware-execution](features/alignment-aware-execution.md) — race/alignment-aware execution penalties
- [marriage-alignment](features/marriage-alignment.md): a Free-aligned hero cannot marry an Evil-aligned one (#542, Boromir wed a Misty Mountain orc). Blocks in `TaomMarriageModel.IsCoupleSuitableForMarriage`, the chokepoint every marriage path funnels through; `Patch81` narrows the AI partner draw so Free clans keep their marriage rate
- [execution](features/execution.md) — TaomExecutionRelationModel + Patch14
- [banner-injection](features/banner-injection.md) — player banner persistence
- [banner-color-persistence](features/banner-color-persistence.md) — clan colors everywhere (Patch23 + Patch24)
- [named-companions](features/named-companions.md) — 18 lore companions as recruitable wanderers
- [war-of-the-ring](features/war-of-the-ring.md) — endgame WotR phase machine (Peace→IsengardWar→FullWar→WarEnded)
- [war-of-the-ring-momentum](features/war-of-the-ring-momentum.md) — Evil-vs-Good progress meter + on-map bar/popup + victory-ends-the-war (#327)
- [diplomacy](features/diplomacy.md), [army-targeting](features/army-targeting.md) — see also TaomTargetScoreModel + Patch22 (border proximity floor)

### Sandbox, lifecycle, & UI
- [supply-lines](features/supply-lines.md): resupply convoys from towns/castles/lords (yotthani port #505): real stock deducted, sources credited, full-screen Gauntlet order UI, TAOM's first custom PartyComponent
- [field-camp](features/field-camp.md): campaign-map camps (yotthani port #506): field/fortified/ambush/lookout, forage + morale, tpac visuals, map-overlay Make Camp, `taom_fcamp_` loc prefix (taom_fc_ is FieldCommission's)
- [refuge](features/refuge.md): movable player base raised from a camp (yotthani port #507): warden + garrison + stash, Refuge/Stronghold tiers, model-chain defence bonus, orphan-adopt on warden loss
- [uncapturable-heroes](features/uncapturable-heroes.md): Sauron and the Nazgul can never be taken prisoner, they escape as fugitives; why the race axis cannot find the Nine, and why death is deliberately still possible
- [main-menu-customizer](features/main-menu-customizer.md) — hide Campaign, rename Sandbox → "Enter The Age Of Men"
- [lotr-issues](features/lotr-issues.md) — **IMPLEMENTED** (2026-06-20) — all 43 vanilla procedural issues suppressed + replaced by 43 LOTR issues via XML-config + 3 generic templates (DeliverGoods/DeliverPersonnel/Combat); `RemoveBehaviors<T>` suppression + SaveableTypeDefiner (base 726900801); per-issue disposition matrix retained for provenance
- [encyclopedia](features/encyclopedia.md) — encyclopedia screen extensions, dispatch entry points
- [lord-identity-reconciliation](features/lord-identity-reconciliation.md): the two halves of a named lord (`characters/lords.xml` + `lords.xslt` say who he is, `characters/heroes.xml` + `heroes.xslt` say what the encyclopedia tells the player) and how they drift. Load order, the two non-interoperating localization tiers, and the inherited-vanilla-attribute trap that married Gríma Wormtongue to Éowyn
- [menu-link-colors](features/menu-link-colors.md) — game-menu hyperlinks recoloured by the linked object's culture (Patch64); 20 `Link.Taom.*` styles in `GameMenu.InfoText`, parchment contrast window pinned by test
- [quick-actions](features/quick-actions.md) — inventory "Sell All" multi-action menu (Patch34)
- [fief-management](features/fief-management.md) — custom GameState for fief management
- [arena](features/arena.md) — TaomTournamentModel with culture armor + prize pools
- [messengers](features/messengers.md) — paid messenger dispatch + travel arrival inquiry
- [shader-precompilation](features/shader-precompilation.md) — pre-compile shaders menu option (Patch21)
- [time-acceleration](features/time-acceleration.md) — campaign time scale knobs
- [atmosphere-persistence](features/atmosphere-persistence.md) — forced-atmosphere scenes (Patch16); exonerated as the `_forceatmo` battle-load crash cause (2026-06-19)
- [weather-bounds-guard](features/weather-bounds-guard.md) — weather bounds clamp (Patch10)
- [localization](features/localization.md): 12 languages × 3 modules, AI-translated via tools. Only the TAOM third is in git; the other 285 language XML (25 in `TAOM_Map`, 260 in `LOTRLOME_Armory`) live in the game install and a module reinstall reverts them silently. The sole in-repo gate is `python tools/check_external_loc_coverage.py`, a per-language untranslated-row ratchet that no hook or CI job runs
- [localization-override](features/localization-override.md) — per-language curated overrides
- [army-targeting](features/army-targeting.md) — besieger commitment stickiness, priority lists, border floor
- [mcm](features/mcm.md) — MCM options-screen top-to-bottom layout fix (Patch41 on UIExtenderEx `WidgetFactoryManager.CreateAndRegister`; #252)
- [save-load-diagnostics](features/save-load-diagnostics.md) — always-on `[SaveLoad]` lifecycle logging (Patch61, 15 hooks) — stamps the exact failing type/SaveId/chunk the engine's generic load-error dialog swallows; root-caused the v2.0.9 momentum save corruption

### Multiplayer & co-op

TAOM ships no multiplayer of its own; these cover behaving correctly when a third-party co-op mod (BannerlordCoop, Bannerlord Together) or a Bannerlord dedicated server is driving the campaign.

- [coop-interop](features/coop-interop.md) — the detection + authority seam every other feature gates on: presence (a co-op module is loaded) vs session authority (`IsAuthority` / `ShouldDeferToHost`) vs dedicated server (which binaries folder we loaded from) are three different questions; also assembly resolution, load order, client-side object creation, and why PatchShield skips install under co-op
- [player-possession](features/player-possession.md) — every co-op base discards the character-creation hero at the join hand-off, so TAOM's CC grants (race, culture gold, career, resource seed) all landed on a hero about to be thrown away; re-applies them to the hero the player actually controls, detected from `Hero.MainHero.StringId` alone so no co-op assembly is referenced
- [bannerlord-together-compat](features/bannerlord-together-compat.md) — multiplayer mod compat surface

### Infrastructure & tooling
- [reference/mcp-servers.md](reference/mcp-servers.md) — full MCP server table (9 servers incl. `imagine`), TaleWorlds research lookup-order detail, `.mcp.json` configuration, plugin-overlap routing; CLAUDE.md keeps the usage guide + a 4-line summary
- **Claude-config security auditor** — `tools/audit_claude_config.py` (behind `/security-scan`): scans `.claude/`, `.mcp.json`, `settings*.json`, `CLAUDE.md` for secrets / over-broad permissions / hook-exfil / MCP risk / prompt-injection, plus SkillSpector-derived skill-threat categories + Python-AST + clean-room YARA. Run on TAOM's own config before `/ship` or after a hook/permission/MCP change; run `--root <repo> --external` (full severity) to vet a FOREIGN skill BEFORE adopting it via `/adopt-external`. Adoption review: [adopt-skillspector](reviews/adopt-skillspector-2026-06-22.md).
- [reference/module-backup-sweep.md](reference/module-backup-sweep.md): getting backup sidecars out of what ships (`.bak` breaks the Cloudflare distribution). `tools/sweep_module_backups.ps1` moves them to a dated quarantine with a SHA256 manifest rather than deleting, because `LOTRLOME_Armory` and `TAOM_Map` are untracked and the sidecars are their only rollback. Two traps it encodes: a bare `*.bak` glob matched 18 of 658 files (the dated `.bak-<topic>` suffixes dominate), and a sidecar whose live sibling is gone is a **sole copy**, not a backup. Gated by `/release` Phase 2. The 2026-09-01 run moved 781 files, 937 MB
- [bannerlord-engine-and-toolchain](reference/bannerlord-engine-and-toolchain.md) — **the whole engine/toolchain**: shipping-vs-editor builds, managed-vs-native DLL split, verified tech stack (Mono, PhysX, Granite, DX11, DLSS), the managed↔native bridge, FBX→tpac pipeline, custom-creature workflow. `tools/decompile_bannerlord.ps1` (dual-build decompile) + `tools/pe_inspect.py` (see into native DLLs)
- **`reference/engine/` — phased engine study** (19 phases, COMPLETE; one process traced end-to-end from the decompile, what/how/why). The arc: campaign heartbeat → object → encounter → mission → agent → render, plus every cross-cutting system + the integration meta-layer:
  - [agent-spawn-and-render-pipeline](reference/engine/agent-spawn-and-render-pipeline.md) (Phase 1 — `SpawnAgent`/`SpawnMonster`→`CreateAgent`→`BuildAgent`→native `PreloadForRendering`; pins the spider render AV to the mesh, not the spawn code)
  - [animation-binding-and-playback](reference/engine/animation-binding-and-playback.md) (Phase 2 — `Monster.FillAnimationSystemData`→`CreateAgent` binds the rig; `SetActionChannel`→native plays it; the "won't animate" diagnostic chain)
  - [monster-model](reference/engine/monster-model.md) (Phase 3 — the `monsters.xml` schema: bone-index map by name, IK/capsules/flags/usage, base-monster inheritance; what TAOM authors per creature)
  - [mission-and-missionbehavior-lifecycle](reference/engine/mission-and-missionbehavior-lifecycle.md) (Phase 4 — the in-battle runtime: `MissionBehavior`/`MissionLogic` virtuals, `AddMissionBehavior` dispatch, lifecycle order; confirms the `: MissionLogic` gotcha)
  - [object-system-mbobjectmanager](reference/engine/object-system-mbobjectmanager.md) (Phase 5 — `MBObjectManager` `RegisterType`/`LoadXML`/`GetObject<T>(stringId)`; the data backbone; null-on-missing = the underwear bug; load-order-tolerant cross-module merge)
  - [save-system](reference/engine/save-system.md) (Phase 6 — `SyncData` vs `SaveableTypeDefiner`+`[SaveableField]`; the base+localId collision gotcha; TAOM composite-string idiom)
  - [gamemodel-system](reference/engine/gamemodel-system.md) (Phase 7 — `GetModel<T>` last-added-wins; `AddModel` inheritance vs decorator; register-after-defaults + one-override-per-type; TAOM's ~40 overrides)
  - [scene-gameentity-scriptcomponent](reference/engine/scene-gameentity-scriptcomponent.md) (Phase 8 — `GameEntity`/`ScriptComponentBehavior`/prefab; engine-discovery by class name; `[EditableScriptComponentVariable]` = config-must-validate)
  - [campaignevents-and-campaignbehavior](reference/engine/campaignevents-and-campaignbehavior.md) (Phase 9 — `CampaignBehaviorBase` `RegisterEvents`/`SyncData`; `CampaignEvents`/`IMbEvent` subscription; no-RemoveListener + no-OnGameEnd gotchas)
  - [item-equipment-model](reference/engine/item-equipment-model.md) (Phase 10 — `ItemObject`/`ItemComponent`/`HorseComponent.Monster`; `EquipmentIndex` slots (Horse=ArmorItemEndSlot=10); `EquipmentElement`=Item+Modifier → the modifier-preserving-overload rule)
  - [gauntletui-viewmodel-screen](reference/engine/gauntletui-viewmodel-screen.md) (Phase 11 — `ViewModel`↔movie via `GauntletLayer.LoadMovie`; `GameState`/`GameStateScreen`/`IGameStateListener`; UIExtenderEx mixins; the UI gotcha cluster)
  - [usable-machines](reference/engine/usable-machines.md) (Phase 12 — `MissionObject`→`UsableMissionObject`→`StandingPoint`/`UsableMachine` (`OnUse(Agent,sbyte)`, auto-collected `StandingPoints`, `IDetachment`/`IOrderable`); `SiegeWeapon` template; generalizes the howdah + its 4 v1.4.5 drifts)
  - [formations-and-team-ai](reference/engine/formations-and-team-ai.md) (Phase 13 — `Team`(8 formations, `TeamAIComponent`, `IsEnemyOf`→native `MBTeam`)→`TacticComponent`→`Formation`(`SetMovementOrder`@685, geometry, detached units)→`IFormationArrangement`→native positioning; the SmartCavalryAI/MixedFormations/CompanionTactics patch surface + the spider DivideByZero lead (count-division sites + guards))
  - [mount-and-rider-runtime](reference/engine/mount-and-rider-runtime.md) (Phase 14 — mount = own `Agent`, rider = another, linked via `Agent.Mount` two-phase → seated at `RiderSitBoneIndex`; `IsMount`=`AgentFlag.Mountable`; the 3 TAOM seating modes: cavalry `Mount` / elephant `Mount`+howdah-StandingPoints / riderless spider)
  - [agent-stats-and-driven-properties](reference/engine/agent-stats-and-driven-properties.md) (Phase 15 — `AgentDrivenProperties` (~99 `DrivenProperty` float channels read by native sim per tick) filled by `AgentStatCalculateModel.Initialize/UpdateAgentStats`; TAOM's `TaomAgentStatCalculateModel : SandboxAgentStatCalculateModel` = career passives + elephant mount-lock consolidated in one slot; engine-scale + NaN gotchas)
  - [campaign-object-graph](reference/engine/campaign-object-graph.md) (Phase 16 — `Hero`∈`Clan`∈`Kingdom` (IFaction), `MobileParty`→`PartyBase`, `Settlement`(town/castle/village, `OwnerClan`); the graph every TAOM campaign behavior mutates; ADR-007 adapters, `?.`-chains, non-saved `Settlement.Culture`, castle `.Village==null`; **entirely managed, no native boundary**)
  - [campaign-to-mission-bridge](reference/engine/campaign-to-mission-bridge.md) (Phase 17 — **the seam between the two halves**: `MobileParty` encounter→`EncounterManager`/`StartBattleAction`→`MapEvent`→`PlayerEncounter`→`CampaignMission.OpenBattleMission`→`MissionState.OpenNew` (CreateState+PushState, Phase 11)→Mission (Phase 4)→`SpawnAgent` (Phase 1); CasualtyHandler back to campaign; managed→native seam = `MissionState.OpenNew`)
  - [submodule-lifecycle-and-harmony](reference/engine/submodule-lifecycle-and-harmony.md) (Phase 18, **integration capstone** — the meta-layer: `MBSubModuleBase` lifecycle (`OnSubModuleLoad`/`OnGameStart`/`OnMissionBehaviorInitialize`/`OnSubModuleUnloaded`), Harmony owner/categories/Prefix-Postfix-Transpiler-Finalizer/deferred application, the 3 registration mechanisms (patch vs `AddModel` vs `AddBehavior`), Harmony-managed vs MinHook-native boundary, PatchShield)
  - [campaign-tick-time-and-party-ai](reference/engine/campaign-tick-time-and-party-ai.md) (Phase 19 — the campaign **heartbeat** closing the loop to Phase 17: `MapTimeTracker` advances `CampaignTime` at `TimeControlMode` speed; `Campaign.Tick`→periodic events (Phase 9) + `MapEvent` + staggered `MobilePartyAi` (`DefaultBehavior`/`TargetSettlement`) + `EncounterManager`→encounter; `CampaignTime`-as-deadline-unit, game-time-not-real-time, staggered-AI gotchas)
  - [issue-and-quest-system](reference/engine/issue-and-quest-system.md) (sibling reference, not a numbered phase — `IssueBase`/`QuestBase`/`IssueManager`/`QuestManager`: the procedural issue→quest pipeline, the 43-issue sandbox registration set (`SandBoxManager.Initialize` + `SandBoxSubModule`), `IssueModel` surface, `OnGameLoaded` auto-cancel + `SpecialQuestType`, `RemoveBehaviors<T>` suppression; backs the [lotr-issues](features/lotr-issues.md) implementation (shipped 2026-06-20))
  - [settlement-economy-food-prosperity](reference/engine/settlement-economy-food-prosperity.md) (sibling reference, not a numbered phase: `DefaultSettlementFoodModel`/`DefaultSettlementProsperityModel`: food = production − consumption (`Prosperity/40` + `garrison/20`), village food caps at 18/day, the prosperity death-spiral, **garrison regulars DO starve to death at 10%/day once production drops below `garrison/20`** (this entry said the opposite until 2026-09-06), caravans don't feed towns, storage caps reach 800/750 once buildings are counted; food section re-verified against installed v1.4.8, prosperity section still v1.4.5. Backs the [settlement-food](features/settlement-food.md) hinterland term + shipped tuning)
- [harmony-patch-registry](reference/harmony-patch-registry.md) — full per-category rationale/history/RCA links for every TAOM Harmony patch (Patch0–Patch61 + the deferred MovementOrder category); CLAUDE.md keeps only the thin routing table. Read the target patch's section before editing it.
- [bannerlord-animation-clip-flags](reference/bannerlord-animation-clip-flags.md) — the `AnimFlags` clip-flag system + per-clip-type recipe + full per-flag reference (all ~60); flags are baked into the `_anm.tpac`, NOT `action_types.xml`; the spider's clips ship with zero flags (= broken locomotion)
- [editor-cache-rebuild](features/editor-cache-rebuild.md) — parallel + incremental + resumable settlement distance cache rebuild
- [scene-scripts](features/scene-scripts.md) — engine-discovered ScriptComponentBehavior subclasses (CS_Road, etc.)
- [dev-console](features/dev-console.md) — the `taom.*` console command contract (`TaomConsole` dispatch, three guards, discovery audit) plus the command reference. Includes `taom.audit_settlement_entrances`, which flags settlements whose entrance sits on a navmesh island nothing can path to — `PathFaceRecord.IsValid()` is true for all of them, so only an island comparison finds them. **The auditor ships; the corrected coordinates do not exist yet** — they need one in-game campaign run, then apply to the LIVE `TAOM_Map/ModuleData/settlements.xml`
- [crash-report](features/crash-report.md) — crash report enrichment
- [mission-diagnostic](features/mission-diagnostic.md) — first-tick MissionBehavior dump + action-set capture for mod-conflict diagnostics
- [battle-load-diagnostics](features/battle-load-diagnostics.md) — phase-stamped battle-load lifecycle log + stall watchdog + next-session stall marker/notice; offline `tools/triage_battle_load.py` gives an equipment-vs-code verdict (#262)
- [blow-diagnostics](features/blow-diagnostics.md) — opt-in `[Blow]` combat-blow logging (Patch63_BlowDiagnostics) for damage/knockback triage
- [SAVE-REPAIR-GUIDE.md](SAVE-REPAIR-GUIDE.md) — save-file repair walkthrough (pairs with the save-repair tools in `tools/README.md`)
- [scene-entities.md](scene-entities.md) — scene game-entity inventory reference (regenerate via `tools/Generate-SceneEntitiesDoc.ps1`)
- [moduledata-validation](features/moduledata-validation.md): `tools/validate_moduledata.py` + `tools/taom_schema.py` + `tools/taom_query.py` + the `taom-moduledata` MCP server. **This is TAOM's XML cross-reference graph, not merely a linter** (consult it before concluding TAOM has no XML graph capability). It resolves `Item.` / `NPCCharacter.` / `Culture.` / `PartyTemplate.` refs and gates duplicate ids, civilian `equipmentType`, `default_group` and `LANDLESS_CULTURE`. **Scope is three modules but uneven:** schema contracts apply to the repo's 259 `Main/_Module/ModuleData` XML only, the ref sweep adds `LOTRLOME_Armory`'s 382 (641 files a run), and `TAOM_Map` contributes just `settlements.xslt` + `settlements.xml`, leaving its 1,012 `Culture.` refs unchecked
- [armoury-mesh-cleanup](features/armoury-mesh-cleanup.md): the 2026-08-28 asset deletion (179 meshes) and what wore them. **Corrected 2026-09-01: the Armory now has NO cooked tree at all** (0 `AssetPackages/*.tpac` against 4,364 loose `Assets/**/*.tpac`), so the two-tree disagreement described here is currently unreachable for this module and `Assets/` is the only source of truth. The trap still applies wherever a cooked tree exists: cooked packs rebuild only on demand, so deleted art keeps shipping and `validate_mesh_refs.py` returns PASS on broken data. Records the Gondor lord regional re-dressing, Easterling to Loke-Rim, and the removal of all 57 Erebor team-colour items
- [mesh-ref-validation](features/mesh-ref-validation.md) — `tools/validate_mesh_refs.py`: does every `mesh=` / `body_name=` in item + crafting-piece XML resolve to a packaged asset? A missing `bo_` body is a **confirmed** infinite-mission-load hang (#352) — run it after any weapon/armor authoring. A clean PASS only means "clean within `--items` scope"
- [doc-health-linter](features/doc-health-linter.md) — `tools/lint_docs.py` + `/lint-docs`: seven checks over `docs/` (dead links, stale version refs, orphan/missing feature docs, config-example drift, pin-vs-doc version mismatch, CLAUDE.md/AGENTS.md eager budget). Three of the seven block a commit. **A clean run is not proof of no rot** — the stale-version check's blind spots are listed in the doc and tracked as [#405](https://github.com/haterade22/TAOM/issues/405)
- **XSLT coverage (the standing gap)**: the three modules ship **16** stylesheets, 8 in the repo (1,063 `<xsl:template>` elements between them), 7 in `LOTRLOME_Armory`, 1 in `TAOM_Map`. Almost nothing models them. `taom_schema.py` opens exactly one, `TAOM_Map/ModuleData/settlements.xslt`, purely to test whether it strips vanilla settlements; every registry is built from authored source XML rather than transform output. Two now have real output coverage: `LordFamilyTransformTests` runs `lords.xslt` and `heroes.xslt` over the vanilla documents and checks the family graph the engine computes, which is the only way to see an attribute a template inherits rather than sets (see [lord-identity-reconciliation](features/lord-identity-reconciliation.md)). The `/xslt-check` skill reads its target under `Main/_Module/ModuleData/`, so the 8 external stylesheets are out of its reach, and its mapping table covers 6 of the repo's 8 (`action_strings.xslt`, `comment_strings.xslt` absent). Passthrough rules: [.claude/rules/xslt.md](../.claude/rules/xslt.md); the failure this keeps causing: [culture-playability-wiring](features/culture-playability-wiring.md)
- [doc-graph](features/doc-graph.md): query + audit *this* knowledge graph (`/doc-graph` skill + `tools/graph_query.py`): `explain` a doc's links, `path` between two docs, `metrics` (god nodes / bridges / orphans). [ADR-010](adrs/010-knowledge-base-architecture.md) Phase 5; adopted from graphify ([June review](reviews/adopt-graphify-2026-06-08.md), superseded by the [v8 trial](reviews/adopt-graphify-v8-2026-08-18.md)). Whole-codebase **C# structure** (blast radius, architectural hubs) is not this tool's job and is not a TAOM tool at all: see the v8 review's "How to actually use it"

## Architecture Decision Records (canonical project rules)

See [adrs/README.md](adrs/README.md) for the full table. **Mandatory** ADRs that gate code review:

- [ADR-002 Thin Entry Points](adrs/002-thin-entry-points.md) — entry points <150 lines, delegate to services
- [ADR-007 Adapter Pattern](adrs/007-adapter-pattern.md) — services use `IHeroAdapter` etc., NEVER `Hero` directly
- [ADR-008 Testability Requirements](adrs/008-testability-requirements.md) — business logic 100% unit-testable

Other standards: [ADR-001 XML config](adrs/001-xml-config.md), [ADR-003 No `#region`](adrs/003-no-regions.md), [ADR-004 No `[Obsolete]`](adrs/004-no-obsolete.md), [ADR-005 No `#if DEBUG`](adrs/005-no-preprocessor-directives.md), [ADR-009 Self-documenting code](adrs/009-self-documenting-code.md), [ADR-010 Knowledge-base architecture](adrs/010-knowledge-base-architecture.md).

## Review & RCA archive

- [reviews/REVIEW-GUIDE.md](reviews/REVIEW-GUIDE.md) — Codex adversarial review process (prompts, dispatch contract, scoring)
- [reviews/REVIEW-LOG.md](reviews/REVIEW-LOG.md) — running scorecard of every Codex review (35+ reviews, 96 bugs found)
- [reviews/REVIEW-PLAN.md](reviews/REVIEW-PLAN.md) — multi-feature review planning
- Per-feature RCAs live as `reviews/rca-<feature>-<date>.md` — read these BEFORE re-implementing related behaviour. Filename convention is grep-friendly.
- Auto-detected reviews: `reviews/codex-adversarial-<feature>-<date>.md` (prompt + output pairs).
- Balance/data audits (machine-generated, distinct from RCAs): `reviews/<topic>-audit-<date>.md`. Current: [reviews/cc-bonus-audit-2026-05-30.md](reviews/cc-bonus-audit-2026-05-30.md) — per-culture character-creation skill/attribute/focus totals vs vanilla budget; regenerate with `tools/audit_cc_bonuses.py`.
- Repo-wide audits live one directory over, in [audits/](audits/README.md) alongside the Phase 0–9 feature-audit series. Newest: [audits/issue-triage-2026-08-08.md](audits/issue-triage-2026-08-08.md) — all 147 then-open GitHub issues re-checked against HEAD; 81 closed, 66 left open, with per-issue evidence, the adversarial-refutation stage that killed 2 proposed closures, and the failure-mode table.
- Historical prompt/review archives (pre-convention material, kept verbatim): [archive/README.md](archive/README.md).
- External adoption reviews (one outside source folded into TAOM, distinct from RCAs): `reviews/adopt-<source>-<date>.md`. Current: [adopt-graphify](reviews/adopt-graphify-2026-06-08.md) (+ the [v8 trial-install re-review](reviews/adopt-graphify-v8-2026-08-18.md): measured against TAOM and **rejected** as the cross-domain graph and as a `doc_graph.py` replacement, because it emits zero `.xml` and zero `.xslt` nodes, and XML/XSLT coverage is a TAOM requirement rather than a preference; kept installed but unwired as an ad-hoc C# analysis aid), [adopt-ponytail](reviews/adopt-ponytail-2026-06-18.md), [adopt-skillspector](reviews/adopt-skillspector-2026-06-22.md) (NVIDIA SkillSpector → 6 deterministic skill-threat categories + Python-AST + clean-room YARA in `tools/audit_claude_config.py`, plus the `--external` foreign-skill vet), [adopt-mattpocock-improve-architecture](reviews/adopt-mattpocock-improve-architecture-2026-06-22.md), [adopt-mattpocock-teach-handoff](reviews/adopt-mattpocock-teach-handoff-2026-06-22.md). Executable procedure: [ai-includes/external-repo-adoption.md](ai-includes/external-repo-adoption.md).

## Migration history (v1.2 → v1.3 → v1.4.x)

- [migration/TRACKING.md](migration/TRACKING.md) — top-level migration audit trail
- [migration/v1.4.8-impact.md](migration/v1.4.8-impact.md) — **current bump (2026-08-10).** v1.4.8 changelog → TAOM surface → verdict matrix, the engine changes the changelog doesn't mention, and what the bump left owed. Previous bump: [migration/v1.4.7-impact.md](migration/v1.4.7-impact.md), same document shape
- [migration/v1.4.x-overview.md](migration/v1.4.x-overview.md) — current target migration plan
- [migration/api-diff-1.3.15-to-1.4.5.md](migration/api-diff-1.3.15-to-1.4.5.md) — API delta table
- [migration/XML-SCHEMA-CHANGES.md](migration/XML-SCHEMA-CHANGES.md) — XML schema changes between versions
- [migration/dr3-maintenance.md](migration/dr3-maintenance.md) — BUTR/MCM/ButterLib dependency pinning, smoke test, risk scenarios
- [migration/dependency-audit-2026-07-15.md](migration/dependency-audit-2026-07-15.md) — BUTR stack audit vs engine 1.4.7 + the applied update (ButterLib 2.11.0 / MCM 5.12.1 / UIExtenderEx 2.13.2, Native pin → 1.4.7); impl-DLL fallback explained, Patch41 keep-verdict evidence
- [migration/dual-dll-setup.md](migration/dual-dll-setup.md) — dual-DLL setup for cross-version testing
- [migration/dr3-mcm-internalization-plan.md](migration/dr3-mcm-internalization-plan.md), [migration/dr3-execution-handoff.md](migration/dr3-execution-handoff.md) — MCM/dependency internalization
- [migration/ROT-CORE-ANALYSIS.md](migration/ROT-CORE-ANALYSIS.md) — ToR_Core comparison

## AI / process guidance

- [reference/rules-catalog.md](reference/rules-catalog.md) — full `.claude/rules/` catalog (rule → scope → content, always-load vs path-scoped); CLAUDE.md keeps only the load-convention note
- [reference/rule-provenance.md](reference/rule-provenance.md) — the always-load rules' "why this exists / relationships / source" sections, moved out of the eager load; read when auditing or editing a rule
- [reference/provenance-register.md](reference/provenance-register.md): every third-party source TAOM derives from, with its license and derivation type. Read before porting anything, and see `.claude/rules/provenance.md` for the rule
- [ai-includes/architecture.md](ai-includes/architecture.md) — layer stack, IoC, lifetimes
- [ai-includes/patterns.md](ai-includes/patterns.md) — Hook / Strategy / GameModel patterns
- [ai-includes/agent-teams.md](ai-includes/agent-teams.md) — when to spawn parallel agents
- [ai-includes/tdd-enforcement.md](ai-includes/tdd-enforcement.md) — TDD workflow rules
- [ai-includes/testing-guide.md](ai-includes/testing-guide.md) — MSTest + NSubstitute patterns
- [ai-includes/research-workflow.md](ai-includes/research-workflow.md) — `taom-src` + decompilation workflow
- [ai-includes/taleworlds-research-guide.md](ai-includes/taleworlds-research-guide.md) — decompiled-source navigation
- [ai-includes/decompiled-code-analysis.md](ai-includes/decompiled-code-analysis.md) — reading decompiled output
- [ai-includes/iterative-problem-solving.md](ai-includes/iterative-problem-solving.md) — multi-pass refinement
- [ai-includes/multi-approach-validation.md](ai-includes/multi-approach-validation.md) — comparing alternative implementations
- [ai-includes/code-quality.md](ai-includes/code-quality.md) — non-negotiable C# quality rules
- [ai-includes/security.md](ai-includes/security.md) — input validation, boundary trust
- [ai-includes/new-culture-authoring.md](ai-includes/new-culture-authoring.md) — repeatable end-to-end culture authoring (Dale + Gondor)

## Cross-cutting concerns (where memory ↔ docs intersect)

These are recurring lessons the auto-memory system has captured. The memory files live outside the repo at `~/.claude/projects/<project-slug>/memory/` (auto-loaded on session start via `MEMORY.md`). When a memory entry says *"see CLAUDE.md X"* or *"see docs/features/Y"*, the doc here is the canonical truth; memory is the *lesson learned about how to use it*.

- **Equipment authoring discipline** — CLAUDE.md "Equipment & Armory" (canonical-folder table) + memory: `feedback_lotrlome_armor_cover_attributes`, `feedback_multi_folder_id_uniqueness`, `feedback_verify_troop_ids_against_canonical_xml`
- **Native C++ port discipline** — CLAUDE.md "Native C++ port discipline" + [features/native-skin-fixes](features/native-skin-fixes.md) + memory: `feedback_native_port_hot_path_audit`, `feedback_seh_filter_specificity`, `feedback_degraded_state_distinct_banner`
- **TaleWorlds API research** — [ai-includes/taleworlds-research-guide.md](ai-includes/taleworlds-research-guide.md) + memory: `feedback_taleworlds_vm_setter_decompile`, `feedback_codex_caught_api_misread`, `feedback_decompile_vanilla_setter_before_deserialize_mutate`, `feedback_imbevent_remove_one_unavailable`, `feedback_campaignbehavior_no_ongameend`, `feedback_movementorder_cctor_mission_current`
- **TAOM_Map live-vs-shadow trap** — CLAUDE.md "TAOM_Map settlements" + memory: `feedback_taom_map_live_vs_stale_shadow`, [reference/taom-map-settlement-naming.md](reference/taom-map-settlement-naming.md)
- **Adversarial-review discipline** — [reviews/REVIEW-GUIDE.md](reviews/REVIEW-GUIDE.md) + memory: `feedback_root_cause_mandatory`, `feedback_completion_workflow`, `feedback_dont_defer_high_review_findings`, `feedback_review_blindspots`
- **Validation at system boundaries** — memory: `feedback_editor_fields_are_config`, `feedback_clamp_nan_infinity_propagates`, `feedback_validate_before_lookup_with_fallback`

## Glossary / ID cheatsheet

- **Kingdom/culture ID mapping** — battania = Khand, empire = Dunland. Full table in memory `kingdom-culture-mapping.md`. *Do not* assume vanilla ID names map to vanilla LOTR factions.
- **Race system** — memory `races-system.md` (monsters.xml, skins.xml, action_sets.xml structure)
- **Banner ruling-clan convention**: `BannerInjectionService.InjectClanBanners` skips `IsRulingClan`, so `clan_<kingdom>_1` shows the kingdom banner regardless of any `spclans.xslt` override. Its `color`/`color2` still tint its own troops, and `SyncKingdomColors` pushes a ruling clan's colours back onto the kingdom banner. See [features/clan-heraldry.md](features/clan-heraldry.md).
- **Lord archetypes** — memory `lords-system.md` (914 lords × 12 archetypes × 13 cultures)

## Research notes

LLM-compiled wiki nodes derived from `docs/raw/`. See [research/README.md](research/README.md) for the structure and `/knowledge-compile` workflow.

- [karpathy-autoresearch](research/karpathy-autoresearch.md) — full review of Karpathy's autoresearch repo (10 files, 52 patterns extracted) + Tier-1/2/3 adoption map for TAOM. Source: `docs/raw/ai-research/karpathy-autoresearch/`.
- [reference/external-resources.md](reference/external-resources.md) — verified external resources to improve TAOM (official-vs-community Bannerlord docs hierarchy, BUTR deps, Harmony, LOTR/Tolkien lore + naming generators, comparable total-conversion mods, save-compat versioning). Cite this instead of re-searching the web.

## Player-facing copy (Discord)

Written for players, not developers — these are the posts that go out to the community. Keep the
engineering detail in `features/` and link outward from here.

- [releases/2026-08-monthly-discord.md](releases/2026-08-monthly-discord.md): August 2026 monthly recap for Patreon and Discord (Enlist, camps and refuges and supply lines, Player Switcher, Aura of Dread, Black Numenoreans, war ram and fell warg, the lord armour and identity pass, Map and Armoury). Covers the whole month including what v2.0.20/21/22 already announced; deliberately excludes the v1.5.0 line. Written in Discord markdown, with a paste-ready 5-message split (4,000 character Nitro cap) in [releases/2026-08-monthly-discord-chunks.md](releases/2026-08-monthly-discord-chunks.md)
- [releases/v2.0.20-discord.md](releases/v2.0.20-discord.md): v2.0.20 release notes (Enlistment, Blue Craig + Lindon as real cultures, nine kingdoms fielding Calradian troops, career screen, 4.8 GB memory saving, v1.4.8 support). Covers everything since the v2.0.15 tag; condenses the enlistment post below rather than repeating it. Every number quoted from shipped config or a CHANGELOG entry, verified 2026-08-12
- [releases/v2.0.15-discord.md](releases/v2.0.15-discord.md) — v2.0.15 release notes (Gondor overhaul, banner bearers, crash fixes, Polish first pass)
- [releases/enlistment-discord.md](releases/enlistment-discord.md) — Enlistment + Battlefield Promotions feature announcement (#375/#376). Every number in it is quoted from the shipped config; re-verify against `enlistment_config.json` / `field_commission_config.json` before posting if either has been retuned since
- [releases/player-switcher-discord.md](releases/player-switcher-discord.md): Play as an Existing Lord feature announcement (#514). Single player-facing message in raw Discord markdown, 3,308 characters against the 4,000 Nitro cap. Keeps the takeover path and the wanderer-adoption path as separate bullets, because the August round-up conflated them and described only the adoption path

## Modder handbook (ModuleData, file by file)

Written for a content author editing the XML without writing C#: attribute tables generated from the v1.4.8 deserializers, worked examples lifted from shipped files, and add / modify / delete recipes that each end in a validator command, what the change needs to take effect, and whether it needs code. Two read-only gates keep it honest, `tools/check_handbook_attributes.py` (an attribute the engine never reads, or one no table documents) and `tools/lint_handbook.py` (the contract itself).

- [modding/README.md](modding/README.md): the hub: an "I want to..." table, the reading order, and how to read a chapter
- [modding/editing-safely.md](modding/editing-safely.md): hand-edit hygiene: BOM, line endings, the .xml backup that loads as a duplicate id, the parser smoke test
- [modding/submodule-and-registration.md](modding/submodule-and-registration.md): how a file gets loaded at all: XmlName rows, project.mbproj soln ids, folder globbing, GameType filters
- [modding/load-order-and-dependencies.md](modding/load-order-and-dependencies.md): what must exist before what, forward-safe vs must-already-be-loaded references, cross-module merge, and when an edit reaches the game
- [modding/id-cheatsheet.md](modding/id-cheatsheet.md): culture and kingdom ids, race ids, slot names, enums, skills, traits, modifier groups, and which ids are save-bound
- [modding/file-catalogue.md](modding/file-catalogue.md): every ModuleData file in the three modules: registration, engine type, generating tool, live vs repo
- [modding/modules-overview.md](modding/modules-overview.md): the eight modules TAOM runs on, who owns what data, and which folder is authoritative
- [modding/module-taom.md](modding/module-taom.md): the code and data module: folders, the two registration channels, how the build deploys it
- [modding/module-map.md](modding/module-map.md): the campaign map module: scene, settlements, distance cache, and the prefab entity cap
- [modding/module-armory.md](modding/module-armory.md): the art and items module: the loose asset tree the engine reads, the item folders, the XSLT layer
- [modding/module-dependencies.md](modding/module-dependencies.md): the libraries module: Harmony, UIExtenderEx, MCM, and the version pairing that renders characters in bind pose when stale
- [modding/items-armor.md](modding/items-armor.md): armour items: the ItemObject and ArmorComponent tables, cover flags, modifier groups, price
- [modding/items-weapons-and-crafting.md](modding/items-weapons-and-crafting.md): crafted weapons, crafting pieces, weapon descriptions, and the first-match usage rule
- [modding/items-shields.md](modding/items-shields.md): shields: grip, block arc, the offhand bone each grip requires, and the collision body
- [modding/items-mounts-and-harness.md](modding/items-mounts-and-harness.md): the data side of a mount: Horse item, harness family_type, the Monster row, reskin vs bespoke
- [modding/troops.md](modding/troops.md): troop trees: skills, equipment rosters, upgrade targets, race, and the level ladder that sets tier and wage
- [modding/equipment-rosters.md](modding/equipment-rosters.md): the standalone roster files, the two civilian spellings, and per-slot mixing at spawn
- [modding/npcs-notables-and-townsfolk.md](modding/npcs-notables-and-townsfolk.md): the 26 notable slots and the service NPCs, and why an unregistered notable is unreachable
- [modding/wanderers-and-named-companions.md](modding/wanderers-and-named-companions.md): wanderer templates and the named lore companions
- [modding/lords-and-heroes.md](modding/lords-and-heroes.md): a named lord is two records with one id: add, rename, re-culture, re-family, retire
- [modding/skill-sets.md](modding/skill-sets.md): what a SkillSet is, the 18 skills and 25 traits, and what a number buys
- [modding/body-properties.md](modding/body-properties.md): faces and bodies: presets, the 128-hex key, race, tags, and the character-creation defaults
- [modding/cultures.md](modding/cultures.md): the largest element: every culture attribute, the child lists that union rather than replace, and the XSLT path
- [modding/party-templates.md](modding/party-templates.md): what min_value and max_value really do, and why an unbound template is dead data
- [modding/clans.md](modding/clans.md): the Faction element: tier, colours, banner, party template, home settlement
- [modding/kingdoms.md](modding/kingdoms.md): kingdom attributes, relationships and policies, and the configs that enumerate kingdom ids
- [modding/settlements.md](modding/settlements.md): the live map file: positions, owners, cultures, buildings, scenes, and the stale repo shadow
- [modding/banners-and-heraldry.md](modding/banners-and-heraldry.md): icon sheets, the palette, what a banner_key encodes, and how clan colour becomes armour tint
- [modding/strings-and-localization.md](modding/strings-and-localization.md): the {=KEY}Fallback rule, which file owns which prefix, and the cache that silently reverts an edited string
- [modding/configs-balance.md](modding/configs-balance.md): the balance configs: troop weights, resource costs, food, economy, revolt, combat, race age
- [modding/configs-factions-and-world.md](modding/configs-factions-and-world.md): the world configs: diplomacy, war of the ring, alignment, army targeting, guards, faction map
- [modding/recipe-add-a-culture.md](modding/recipe-add-a-culture.md): the largest job: a culture that is defined, settled, playable and fields its own troops
- [modding/recipe-add-a-kingdom.md](modding/recipe-add-a-kingdom.md): a new realm: filing order, relationships against every other kingdom, and the config fan-out
- [modding/recipe-add-a-race-or-creature.md](modding/recipe-add-a-race-or-creature.md): the five data surfaces of a race, and the reskin versus bespoke decision
- [modding/recipe-retire-content.md](modding/recipe-retire-content.md): removing things without breaking saves: what is save-bound, the obsolete stub, the reference sweep
- [modding/recipe-new-mod-from-zero.md](modding/recipe-new-mod-from-zero.md): the whole build in order, from an empty Modules folder to where TAOM is today
- [modding/balance-levers.md](modding/balance-levers.md): what each number changes, with the formula and the model that owns it
- [modding/validation-and-testing.md](modding/validation-and-testing.md): every gate a modder can run, sorted by safety, and what each one cannot see
- [modding/troubleshooting.md](modding/troubleshooting.md): one table: symptom, cause, chapter

## Community contributions (published on other sites)

TAOM knowledge written up for the wider modding community and hosted elsewhere. Source of truth
lives here; the published copy is downstream.

- [community/bannerlordmodding-lt/](community/bannerlordmodding-lt/): **Custom Creatures**, a six-page guide on adding creatures and mounts to Bannerlord (skeleton, animation clips, XML, troubleshooting, reference tables). LIVE on [docs.bannerlordmodding.lt](https://docs.bannerlordmodding.lt/guides/custom_creatures/) since 2026-09-01, with its own nav section. Covers the gap between that wiki's human-rigging page and its C# animation page, where nothing existed. Read the directory README before editing: the site's GitHub repo is a **daily mirror**, so corrections go to the maintainer, not to a PR against a repo that lags the live site

## Conventions

- File links use markdown `[text](relative/path.md)` syntax. No Obsidian `[[wikilinks]]`. See [ADR-010](adrs/010-knowledge-base-architecture.md) for rationale.
- Auto-generated "Referenced by:" footers sit at the bottom of most feature docs (117 of 120 as of 2026-08-07), written by `tools/build_backlinks.py`. They are bot-edited only — do not hand-author them.
- Source materials (papers, web clippings, decompiled notes) go in `docs/raw/`. Compiled wiki nodes derived from raw go in `docs/research/` — `/knowledge-compile` does that pass.
- This file is hand-curated. When a new feature doc is added, also add it here, in the right topical section. `python tools/lint_docs.py` flags orphan feature docs that no other doc references, plus dead links, stale version refs, missing feature docs, config-example drift, and CLAUDE.md/snapshot-vs-pin mismatches. **A clean run is not proof of no rot** — the stale-version check fires only on marker-word phrasing and does not match v1.4.5/v1.4.6 at all ([#405](https://github.com/haterade22/TAOM/issues/405)); see `.claude/skills/lint-docs/SKILL.md`.

---

*Knowledge-base architecture: see [ADR-010](adrs/010-knowledge-base-architecture.md). Plan: `.claude/plans/karpathy-constantly-posts-tips-wondrous-willow.md` (local, not in repo).*

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/010-knowledge-base-architecture.md](adrs/010-knowledge-base-architecture.md)
- [docs/features/doc-graph.md](features/doc-graph.md)
- [docs/modding/README.md](modding/README.md)
- [docs/reference/doc-lookup.md](reference/doc-lookup.md)
- [docs/reviews/adopt-graphify-2026-06-08.md](reviews/adopt-graphify-2026-06-08.md)
- [docs/reviews/adopt-graphify-v8-2026-08-18.md](reviews/adopt-graphify-v8-2026-08-18.md)

<!-- backlinks-end -->
