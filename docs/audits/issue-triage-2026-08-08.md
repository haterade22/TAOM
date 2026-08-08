# Issue triage — all 147 open issues, 2026-08-08

HEAD at verification: `828bf941` on `bannerlord-1.4.5`. Engine pin **v1.4.7**. 147 open issues against 260 closed, zero open pull requests.

> **HEAD moved during the run.** A concurrent session landed `e0f4f21d` (enlistment localization) and
> `0e06834c` (a CLAUDE.md note on unpopped auto-stashes) while triage was in flight, so the branch tip
> is now `0e06834c`. Every verdict and every closing comment cites `828bf941`, which is the commit the
> evidence was actually read at. Only #418 sits near the new work, and `e0f4f21d` strengthens rather
> than contradicts its closure.

Every open issue was checked against the repository at HEAD to answer one question: **is this still an issue?** The tracker had drifted into a work log — most bodies are past-tense engineering reports written when work was planned or finished, so for a large share the real question was not "is this broken?" but "did anyone press close?"

## Outcome

| | Count |
|---|---|
| Closed | **74** |
| Kept open | 67 |
| Escalated (needs a decision from you) | 6 |
| — status comment posted | 52 |
| — labelled only, no comment | 21 |

Comments were posted where something had genuinely changed. Issues whose situation had not moved were labelled instead — a comment saying "nothing has changed since January" carries no information and costs a notification. Issues updated within the last week already carry a human comment more current than triage could produce, so those were labelled too rather than buried.

### Reasons

| Reason | Count | Disposition |
|---|---|---|
| `shipped-and-verified` | 69 | CLOSE |
| `blocked-ingame` | 29 | KEEP / ESCALATE |
| `partial` | 16 | KEEP / ESCALATE |
| `blocked-external` | 11 | KEEP / ESCALATE |
| `valid-unstarted` | 9 | KEEP / ESCALATE |
| `obsolete-premise` | 4 | CLOSE |
| `blocked-decision` | 4 | KEEP / ESCALATE |
| `parked-by-design` | 3 | KEEP / ESCALATE |
| `superseded-by` | 1 | CLOSE |
| `answered` | 1 | KEEP / ESCALATE |

### Labels applied

- `triage-needs-ingame` — 29
- `triage-keep-valid` — 25
- `triage-blocked-external` — 11
- `triage-blocked-decision` — 5
- `triage-parked` — 3
- `question` — 1

## Method

Three stages with four gates. **Agents produced verdicts; they never called a `gh` write command.** Every mutation was executed by one deterministic paced script from a verdict file that was read first, so no agent could close an issue on its own.

1. **Evidence index** — 16 artifacts turning per-issue judgement calls into lookups: both CHANGELOG files merged into one ordinal stream, a doc-wide back-reference map, author-declared exit criteria, repo-ownership, culture aliasing, commit ancestry, the blocker graph, and the full issue/PR number space.
2. **Cluster triage** — 18 agents over subsystem clusters built from body content, not number ranges, so coupled issues were judged together.
3. **Adversarial refutation** — 12 agents paired to *failure modes* rather than to issues, each given the claim but **not** the first pass's evidence, and told to refute. 174 independent claim-checks over 76 proposed closures, averaging 2.3 per closure.

### What the refutation caught

2 proposed closures were killed, both for the same reason — an author-declared exit criterion that the first pass overrode:

- **#285** — The code half of the claim is right, but the claim's load-bearing clause is false. Patch49_ArmyGatheringNreGuard is a [HarmonyFinalizer] on [HarmonyPatch(typeof(Army), "FindBestGatheringSettlementAndMoveTheLeader")] swallowing only NullReferenceException, registered at SubModule.cs:343; Patch50_DropFlaggedItemGuard is a Finalizer on Agent
- **#370** — REFUTED. The wiring half of the claim is true — I verified every row of the Files table at HEAD, including PatchShield consuming CoopPresence/PatchShieldPolicy at its install gate, unpatch gate and protected-owner prefixes; SaveShield's two SaveShieldPolicy.ShouldSwallow calls; the census invoked from SubModule.cs; and the ModulesToLoadAf

A further 37 closures were allowed to proceed but carry a caveat in their closing comment — the fix ships, and something about it is not what the issue's body claims.

## Failure modes the process was built to survive

Each of these had already produced a wrong answer in this repo at least once.

| Trap | The case that proves it |
|---|---|
| **The fix lives in another repository** | #352 and #364: the commit is on trunk with a CHANGELOG entry, and players do not get the fix until `LOTRLOME_Armory` is released. Structurally ineligible to close. |
| **An author-declared exit criterion outranks the project close bar** | #346's "Remaining before close — in-game smoke: 1…4", and the two closures the refuters killed. 42 issues carry such a gate. |
| **Both CHANGELOG files are newest-first** | #82's archive entry says "port all 7 native hooks **+ activate**"; a *later* entry disables it at the wiring level. Grep the archive alone and you close a parked feature. |
| **Culture ids are aliased** | `culture="Culture.rohan"` returns zero rows. Rohan's 68 NPCs are `Culture.vlandia`. The obvious grep says the data is missing. |
| **The last comment can retract the body** | #406's final comment opens "The commit hashes in my previous comment are wrong." |
| **A `#N` may be a closed, unmerged PR** | PRs #80, #271 and #403 are CLOSED-UNMERGED. A reference to one is evidence work did *not* ship. |
| **Branch names lie** | They get reused and rebased; ancestry was keyed on commit content. |
| **Two open issues can want opposite outcomes** | #60 asks for lords always in battle gear; #419 files exactly that shipped state as a bug. |
| **Fix shipped ≠ problem solved** | #317 shipped and was celebrated; a later entry says it fixed the wrong half of the loop, and #393 is open for the same symptom. |
| **Closing X can orphan Y's blocker** | #393 and #396 are both blocked on #391's in-game run. |
| **Absence of a citation proves nothing** | 1433 commits; ~100 ever used a `Closes` trailer, abandoned in June. ~35% of CHANGELOG headings name an issue. |
| **A commit can be entirely off-trunk** | #275 reads as fully shipped; its whole implementation is on an unmerged branch and `Main/Features/Music/` does not exist at HEAD. |

## Escalated — these need a decision from you

### #111 — fix(charactercreation): shaghana/abanissa player flow dead-ends — empty narrative menus crash on advance

Unchanged and still live: shaghana and abanissa are 2 of the 20 CC-selectable cultures in cultures.json yet hold zero option entries in four of the five narrative menus, and the engine indexer that dead-ends on that is still unguarded at the v1.4.7 pin.

**The decision:** The owner picks Option A (author 4 menus × 2 cultures × ~6 options plus matching taom_char_creation_equipment.xml rosters, following the #269 precedent), Option B (drop the two entries from cultures.json), or Option C (empty-list fallback in NarrativeMenuBuilder).

### #118 — Editor settlement distance cache rebuild — parallel + incremental + resumable

The builder is complete, reviewed twice, and has a recorded production success in-game, but the author's remaining Phase 14 gate needs a `known_good_cache.bin` that exists nowhere and can only be produced by the ~108-hour vanilla run the feature exists to avoid.

**The decision:** A maintainer decision, not a task: accept the smoke-test equivalence (10 pairs, MaxDistanceDelta 0.0, plus round-trip verification) as the correctness proof and retire Phase 14, or commit to the ~108 h vanilla baseline run to produce `known_good_cache.bin`.

### #334 — Some questions about map mods

An external user's setup question — is `TAOM_Map` private/unreleased, or should it be derived from the old public `LOTR_Map`? — whose answer is a distribution decision the maintainer must make, not a code change this repo can carry.

**The decision:** The maintainer states publicly whether `TAOM_Map` is distributable and, if it is derivable from `LOTR_Map`, what `SubModule.xml` module-id / dependency / asset changes that requires. Per the cluster brief the maintainer has already written this answer himself — it just is not on the issue.

### #343 — Follow-up: 1H-only troops with Polearm-top skills (108 strict / 46 ties) + off-formula residuals + validator skill-vs-equipment check

All three sub-parts are untouched at HEAD and the largest one cannot start without an owner decision on how to redistribute Polearm excess on one-hander-only troops.

**The decision:** Owner picks the redistribution rule for 1H-only Polearm-top troops (swap Polearm<->OneHanded when strictly inverted, versus folding the excess into the tier curve) — nothing else can be coded until that choice is made.

### #345 — CTD on vanilla 'daughter found' quest — LotrIssues SandBox suppression silently fails (7/43 types unresolved)

The root cause (Part A) shipped via #355's commit, but Part B — the load-time sweep that cancels an *in-flight* vanilla quest, which is exactly the state #345 reports — exists only on an unmerged local branch, and the sweep that DID ship deliberately skips it.

**The decision:** A human policy call: either cancel accepted pre-fix vanilla quests on load (reversing the shipped keep-progress decision and merging the branch's planner), or declare the pre-2026-07-21-save village-mission case out of scope and say so in the issue. Nothing else can move until that is decided.

### #419 — Civilian equipment rosters are battle kit at source across 12 cultures

Verified true and completely untouched at HEAD -- every culture's `civ_template` roster is battle kit, the generated lord rosters copy it faithfully, and the Armory genuinely has no civilian garment for 11 of the 14 cultures.

**The decision:** Decide the intermediate civilian target for the 11 cultures with no civilian garment -- and decide it jointly with #60, whose shipped "lords always in battle gear" outcome this issue proposes to undo. Recommend `/author-armor` for step 3 once the target is chosen.

## Full verdict table

| # | Title | Verdict | Reason | Action |
|---|---|---|---|---|
| [#2](https://github.com/haterade22/TAOM/issues/2) | fix: TAOM lords had incorrect culture mappings for Dunland, Khand, and Rhun | **CLOSED** | `obsolete-premise` | close |
| [#8](https://github.com/haterade22/TAOM/issues/8) | Harmony NuGet compile reference mismatched runtime version (2.3.3 vs 2.4.2) | **CLOSED** | `shipped-and-verified` | close |
| [#9](https://github.com/haterade22/TAOM/issues/9) | Migration docs listed wrong Harmony version for Bannerlord 1.3 | **CLOSED** | `shipped-and-verified` | close |
| [#10](https://github.com/haterade22/TAOM/issues/10) | Add decompiled code analysis framework for AI-assisted mod development | **CLOSED** | `shipped-and-verified` | close |
| [#11](https://github.com/haterade22/TAOM/issues/11) | fix: horizontal characters caused by early Harmony patch timing | **CLOSED** | `shipped-and-verified` | close |
| [#12](https://github.com/haterade22/TAOM/issues/12) | FaceGen editor shows wrong beard/hair thumbnails for custom races | KEEP | `valid-unstarted` | comment |
| [#13](https://github.com/haterade22/TAOM/issues/13) | Hero races reset to human after loading a save game | **CLOSED** | `shipped-and-verified` | close |
| [#20](https://github.com/haterade22/TAOM/issues/20) | Build excludes Assets, AssetSources, and GUI/SpriteParts from module output | **CLOSED** | `shipped-and-verified` | close |
| [#21](https://github.com/haterade22/TAOM/issues/21) | feat: replace vanilla InitialChildGenerationCampaignBehavior with config-drive… | **CLOSED** | `shipped-and-verified` | close |
| [#22](https://github.com/haterade22/TAOM/issues/22) | fix: Youth equipment differentiation + race filtering in character creation | **CLOSED** | `shipped-and-verified` | close |
| [#39](https://github.com/haterade22/TAOM/issues/39) | Lothlorien banner truncated to 32 layers by TaleWorlds engine cap | **CLOSED** | `obsolete-premise` | close |
| [#44](https://github.com/haterade22/TAOM/issues/44) | feat: Port warg combat system from LOTRAOM | **CLOSED** | `shipped-and-verified` | close |
| [#56](https://github.com/haterade22/TAOM/issues/56) | feat: Erebor equipment pass — lords in battle gear + full dress/tunic variety | **CLOSED** | `shipped-and-verified` | close |
| [#58](https://github.com/haterade22/TAOM/issues/58) | feat: Gondor Equipment Pass — Lords in Battle Gear + Noble Coat/Jerkin NPC Var… | **CLOSED** | `shipped-and-verified` | close |
| [#60](https://github.com/haterade22/TAOM/issues/60) | feat: All-Culture Lords Civilian Equipment Pass — Lords Always in Battle Gear | **CLOSED** | `shipped-and-verified` | close |
| [#61](https://github.com/haterade22/TAOM/issues/61) | feat: named hero civilian equipment — Sauron, Witch-King, Nazgul, Khamul, Glor… | **CLOSED** | `shipped-and-verified` | close |
| [#62](https://github.com/haterade22/TAOM/issues/62) | Fix: CulturalFeats/TroopProgression models — static TextObject field triggers … | KEEP | `blocked-external` | comment |
| [#67](https://github.com/haterade22/TAOM/issues/67) | feat: Siege Defense — timed settlement defense events for player kingdom | **CLOSED** | `shipped-and-verified` | close |
| [#71](https://github.com/haterade22/TAOM/issues/71) | feat: LOTR-themed minor factions (mercenaries, mafias, sects, nomads) | **CLOSED** | `shipped-and-verified` | close |
| [#73](https://github.com/haterade22/TAOM/issues/73) | feat: per-kingdom special resource system (Mordor Scraps pilot) | **CLOSED** | `shipped-and-verified` | close |
| [#82](https://github.com/haterade22/TAOM/issues/82) | feat: fork NativeSkinFixes into TAOM — covers_head morph fix + hair/beard clot… | KEEP | `parked-by-design` | comment |
| [#88](https://github.com/haterade22/TAOM/issues/88) | feat: Career ability execution — Phase IV complete (50 careers, 3 archetypes) | **CLOSED** | `shipped-and-verified` | close |
| [#89](https://github.com/haterade22/TAOM/issues/89) | feat: defender trebuchets in siege management UI | KEEP | `partial` | comment |
| [#91](https://github.com/haterade22/TAOM/issues/91) | Adopt Tier 1 productivity skills from Claude Code ecosystem review | **CLOSED** | `shipped-and-verified` | close |
| [#92](https://github.com/haterade22/TAOM/issues/92) | Prevention infrastructure for recurring .claude/ harness bugs | **CLOSED** | `shipped-and-verified` | close |
| [#93](https://github.com/haterade22/TAOM/issues/93) | Tier 2 + 3 picks from Claude Code ecosystem review | **CLOSED** | `shipped-and-verified` | close |
| [#94](https://github.com/haterade22/TAOM/issues/94) | Codex review #29: prevention-theater fixes on Tier 2/3 commit (79350f2) | **CLOSED** | `shipped-and-verified` | close |
| [#95](https://github.com/haterade22/TAOM/issues/95) | Tool: FBX -> 4-XML weapon-build pipeline | **CLOSED** | `shipped-and-verified` | close |
| [#96](https://github.com/haterade22/TAOM/issues/96) | feat(localization): migrate code-side strings to localization XML | **CLOSED** | `shipped-and-verified` | close |
| [#97](https://github.com/haterade22/TAOM/issues/97) | fix: NRE in CareerSystem mission behavior on Custom Battle launch | KEEP | `blocked-ingame` | comment |
| [#101](https://github.com/haterade22/TAOM/issues/101) | CareerSystem: 41 ability icon sprites missing (only 9 of 50 render in HUD) | **CLOSED** | `shipped-and-verified` | close |
| [#111](https://github.com/haterade22/TAOM/issues/111) | fix(charactercreation): shaghana/abanissa player flow dead-ends — empty narrat… | ESCALATE | `blocked-decision` | comment |
| [#117](https://github.com/haterade22/TAOM/issues/117) | feat(companion-tactics): wire FormationPresets UI capture/apply against OrderO… | KEEP | `valid-unstarted` | label-only |
| [#118](https://github.com/haterade22/TAOM/issues/118) | Editor settlement distance cache rebuild — parallel + incremental + resumable | ESCALATE | `blocked-decision` | comment |
| [#120](https://github.com/haterade22/TAOM/issues/120) | EditorCacheRebuild: extend NavigationType iteration for NavalDLC / port suppor… | KEEP | `parked-by-design` | comment |
| [#210](https://github.com/haterade22/TAOM/issues/210) | [Migration] Bannerlord 1.3.15 → 1.4.5 | **CLOSED** | `obsolete-premise` | close |
| [#216](https://github.com/haterade22/TAOM/issues/216) | data(races): stamp race="elf" on all Rivendell / Mirkwood / Lothlórien NPCChar… | **CLOSED** | `shipped-and-verified` | close |
| [#221](https://github.com/haterade22/TAOM/issues/221) | DR3 stub modules: vanilla launcher dep IDs for Harmony/UIExtenderEx/ButterLib/… | **CLOSED** | `shipped-and-verified` | close |
| [#222](https://github.com/haterade22/TAOM/issues/222) | CrashReport feature: comprehensive crash diagnostic capture (BEW-inspired, TAO… | **CLOSED** | `shipped-and-verified` | close |
| [#228](https://github.com/haterade22/TAOM/issues/228) | feat(lords-skills): Abanissa — lore-driven skills + traits for 8 adult NPCs | **CLOSED** | `shipped-and-verified` | close |
| [#229](https://github.com/haterade22/TAOM/issues/229) | feat(lords-skills): Dale (Bardings) — lore-driven skills + traits for 82 adult… | **CLOSED** | `shipped-and-verified` | close |
| [#230](https://github.com/haterade22/TAOM/issues/230) | feat(lords-skills): Dol Guldur — lore-driven skills + traits for 59 adult NPCs | **CLOSED** | `shipped-and-verified` | close |
| [#231](https://github.com/haterade22/TAOM/issues/231) | feat(lords-skills): Dunland (Hillmen / Saruman's auxiliaries) — lore-driven sk… | **CLOSED** | `shipped-and-verified` | close |
| [#232](https://github.com/haterade22/TAOM/issues/232) | feat(lords-skills): Easterlings of Rhûn — lore-driven skills + traits for 71 a… | **CLOSED** | `shipped-and-verified` | close |
| [#233](https://github.com/haterade22/TAOM/issues/233) | feat(lords-skills): Erebor (Dwarves of the Lonely Mountain) — lore-driven skil… | **CLOSED** | `shipped-and-verified` | close |
| [#234](https://github.com/haterade22/TAOM/issues/234) | feat(lords-skills): Gondor — lore-driven skills + traits for 118 adult NPCs | **CLOSED** | `shipped-and-verified` | close |
| [#235](https://github.com/haterade22/TAOM/issues/235) | feat(lords-skills): Mount Gundabad — lore-driven skills + traits for 50 adult … | **CLOSED** | `shipped-and-verified` | close |
| [#236](https://github.com/haterade22/TAOM/issues/236) | feat(lords-skills): Harad (Haradrim Southrons) — lore-driven skills + traits f… | **CLOSED** | `shipped-and-verified` | close |
| [#237](https://github.com/haterade22/TAOM/issues/237) | feat(lords-skills): Isengard (Saruman) — lore-driven skills + traits for 34 ad… | **CLOSED** | `shipped-and-verified` | close |
| [#238](https://github.com/haterade22/TAOM/issues/238) | feat(lords-skills): Khand (Variags) — lore-driven skills + traits for 56 adult… | **CLOSED** | `shipped-and-verified` | close |
| [#239](https://github.com/haterade22/TAOM/issues/239) | feat(lords-skills): Lothlórien — lore-driven skills + traits for 3 adult NPCs | **CLOSED** | `shipped-and-verified` | close |
| [#240](https://github.com/haterade22/TAOM/issues/240) | feat(lords-skills): Mirkwood (Woodland Realm) — lore-driven skills + traits fo… | **CLOSED** | `shipped-and-verified` | close |
| [#241](https://github.com/haterade22/TAOM/issues/241) | feat(lords-skills): Mordor — lore-driven skills + traits for 97 adult NPCs | **CLOSED** | `shipped-and-verified` | close |
| [#242](https://github.com/haterade22/TAOM/issues/242) | feat(lords-skills): Rivendell (Imladris) — lore-driven skills + traits for 7 a… | **CLOSED** | `shipped-and-verified` | close |
| [#243](https://github.com/haterade22/TAOM/issues/243) | feat(lords-skills): Rohan — lore-driven skills + traits for 92 adult NPCs | **CLOSED** | `shipped-and-verified` | close |
| [#244](https://github.com/haterade22/TAOM/issues/244) | feat(lords-skills): Shaghana — lore-driven skills + traits for 9 adult NPCs | **CLOSED** | `shipped-and-verified` | close |
| [#245](https://github.com/haterade22/TAOM/issues/245) | feat(lords-skills): Umbar (Corsairs) — lore-driven skills + traits for 10 adul… | **CLOSED** | `shipped-and-verified` | close |
| [#246](https://github.com/haterade22/TAOM/issues/246) | DR3 Phase 4: BetaDeps parity — full adoption | **CLOSED** | `shipped-and-verified` | close |
| [#247](https://github.com/haterade22/TAOM/issues/247) | feat(bandit-management): LOTR bandit cultures + PlayerProgress scaling | **CLOSED** | `shipped-and-verified` | close |
| [#251](https://github.com/haterade22/TAOM/issues/251) | Bandit system: themed hideout descriptions + early-game density tuning | KEEP | `partial` | comment |
| [#264](https://github.com/haterade22/TAOM/issues/264) | fix(character-creation): family/clan name uses default culture instead of sele… | **CLOSED** | `shipped-and-verified` | close |
| [#272](https://github.com/haterade22/TAOM/issues/272) | feat(elephant): Harad war-elephant trample + mount-lock (1-for-1 ADOD, v1.4.5) | **CLOSED** | `superseded-by` | close |
| [#275](https://github.com/haterade22/TAOM/issues/275) | feat(music): culture-specific music integration — 17 cultures, 476 tracks, 6 c… | KEEP | `partial` | comment |
| [#277](https://github.com/haterade22/TAOM/issues/277) | fix(arena): dwarves spawn inside the horse as tournament cavalry | **CLOSED** | `shipped-and-verified` | close |
| [#278](https://github.com/haterade22/TAOM/issues/278) | feat(elephant): war-elephant behavior-tree AI attacks + recruitable rider | KEEP | `partial` | comment |
| [#279](https://github.com/haterade22/TAOM/issues/279) | feat(chariot): port ROT 8.0 war chariot as Rhun Wainrider ridden mount | **CLOSED** | `shipped-and-verified` | close |
| [#280](https://github.com/haterade22/TAOM/issues/280) | Custom Battle scene lotrtaom_iron_hills_01_forceatmo CTDs on every load (8/8, … | **CLOSED** | `obsolete-premise` | close |
| [#285](https://github.com/haterade22/TAOM/issues/285) | crash: AI siege-start NRE CTD on map tick + CultureMarketplace RemoveItem unde… | KEEP | `blocked-ingame` | comment |
| [#286](https://github.com/haterade22/TAOM/issues/286) | feat(recruitment): alignment-gated recruitment — opposed factions refuse to se… | **CLOSED** | `shipped-and-verified` | close |
| [#287](https://github.com/haterade22/TAOM/issues/287) | Battle-load CTD/hang: TAOM scenes lack precompiled shader caches -> runtime d3… | KEEP | `partial` | comment |
| [#288](https://github.com/haterade22/TAOM/issues/288) | crash: HarmonyException on 2nd game-init in one process (DeliverOffSpring tran… | **CLOSED** | `shipped-and-verified` | close |
| [#289](https://github.com/haterade22/TAOM/issues/289) | Settlement food: garrisons starve (Troop-Weight inflates garrison food consump… | **CLOSED** | `shipped-and-verified` | close |
| [#291](https://github.com/haterade22/TAOM/issues/291) | feat(lotr-issues): replace all 43 vanilla procedural issues with LOTR-authored… | KEEP | `blocked-ingame` | comment |
| [#296](https://github.com/haterade22/TAOM/issues/296) | feat: naval travel — sail across water without the Naval DLC | KEEP | `parked-by-design` | comment |
| [#299](https://github.com/haterade22/TAOM/issues/299) | fix(ui): save-load hero preview CTD (AccessViolation) on custom-race saves | **CLOSED** | `shipped-and-verified` | close |
| [#300](https://github.com/haterade22/TAOM/issues/300) | Crash: dwarf falling into water → CTD (standalone as_dwarf_warrior missing 423… | KEEP | `blocked-external` | comment |
| [#301](https://github.com/haterade22/TAOM/issues/301) | fix(faction-map): NRE in GenerateClanName on Rohan (vlandia) character-creatio… | **CLOSED** | `shipped-and-verified` | close |
| [#302](https://github.com/haterade22/TAOM/issues/302) | feat(custom-battles): curated per-faction commander lists in Custom Battle | KEEP | `blocked-ingame` | comment |
| [#303](https://github.com/haterade22/TAOM/issues/303) | Skip the campaign intro video on new game | **CLOSED** | `shipped-and-verified` | close |
| [#314](https://github.com/haterade22/TAOM/issues/314) | Crash: deterministic new-campaign CTD on v2.0.8.0 — PartyBase.get_Owner NRE on… | **CLOSED** | `shipped-and-verified` | close |
| [#317](https://github.com/haterade22/TAOM/issues/317) | SettlementEconomy: tunable town market-gold regeneration (towns drain to 0 and… | KEEP | `partial` | comment |
| [#318](https://github.com/haterade22/TAOM/issues/318) | LOTRLOME item-value rebaseline: computed values run ~2.2x vanilla (drain ampli… | KEEP | `valid-unstarted` | comment |
| [#319](https://github.com/haterade22/TAOM/issues/319) | CultureMarketplace: foreign-item filter deletes paid-for loot and resets the p… | KEEP | `valid-unstarted` | comment |
| [#320](https://github.com/haterade22/TAOM/issues/320) | CombatMechanics: crush-through, creature cleave/unstoppable, weight-based char… | KEEP | `blocked-ingame` | comment |
| [#321](https://github.com/haterade22/TAOM/issues/321) | Sauron: ground him (Infantry, no mount) + dedicated 'sauron' race (elf-based, … | KEEP | `blocked-ingame` | comment |
| [#325](https://github.com/haterade22/TAOM/issues/325) | CultureConversion: replace foreign-culture notables when a settlement converts | KEEP | `blocked-ingame` | comment |
| [#327](https://github.com/haterade22/TAOM/issues/327) | War of the Ring Momentum: Evil vs Good progress tracking, victory, and map UI … | KEEP | `blocked-ingame` | comment |
| [#329](https://github.com/haterade22/TAOM/issues/329) | feat(caravan): AI caravans range further, trade across the war, carry fuller b… | KEEP | `blocked-ingame` | comment |
| [#332](https://github.com/haterade22/TAOM/issues/332) | New-campaign infinite loading loop: CastleRecruitment CreateNotable NRE on mis… | KEEP | `blocked-ingame` | comment |
| [#333](https://github.com/haterade22/TAOM/issues/333) | CultureConversion: hold-timer restarts on every ownership change - contested f… | **CLOSED** | `shipped-and-verified` | close |
| [#334](https://github.com/haterade22/TAOM/issues/334) | Some questions about map mods | ESCALATE | `answered` | comment |
| [#335](https://github.com/haterade22/TAOM/issues/335) | fix(caravan-trade): caravans leave a town and immediately return (home rubber-… | KEEP | `blocked-ingame` | comment |
| [#336](https://github.com/haterade22/TAOM/issues/336) | crash/hang: shader precompile stuck on 1.4.7 — DeploymentMissionController.Set… | KEEP | `partial` | comment |
| [#337](https://github.com/haterade22/TAOM/issues/337) | refactor(troop-weight): weight the party-size limit instead of the member coun… | KEEP | `blocked-ingame` | comment |
| [#338](https://github.com/haterade22/TAOM/issues/338) | CTD: IndexOutOfRangeException in siege map tick — town_LN1 (Rivendell) had an … | KEEP | `blocked-external` | comment |
| [#339](https://github.com/haterade22/TAOM/issues/339) | CTD: AccessViolationException releasing tournament UI movie at exit (v2.0.12 p… | KEEP | `partial` | comment |
| [#340](https://github.com/haterade22/TAOM/issues/340) | Crossbow-armed troops generated with Bow-top skills (12 troops) + naffatun mis… | KEEP | `blocked-ingame` | comment |
| [#341](https://github.com/haterade22/TAOM/issues/341) | Two-hander troops generated with Polearm-top skills (59 troops across 12 cultu… | KEEP | `blocked-ingame` | comment |
| [#342](https://github.com/haterade22/TAOM/issues/342) | Mordor armor beats Gondor per tier (Black Uruk set over-curve + Gondor at base… | KEEP | `blocked-external` | comment |
| [#343](https://github.com/haterade22/TAOM/issues/343) | Follow-up: 1H-only troops with Polearm-top skills (108 strict / 46 ties) + off… | ESCALATE | `valid-unstarted` | comment |
| [#344](https://github.com/haterade22/TAOM/issues/344) | Troop names promise weapons their equipment lacks (Balcoth Axemen with scimita… | KEEP | `blocked-ingame` | comment |
| [#345](https://github.com/haterade22/TAOM/issues/345) | CTD on vanilla 'daughter found' quest — LotrIssues SandBox suppression silentl… | ESCALATE | `blocked-decision` | comment |
| [#346](https://github.com/haterade22/TAOM/issues/346) | Cave troll spawns as visible settlement guard in town/castle scenes | KEEP | `blocked-ingame` | comment |
| [#347](https://github.com/haterade22/TAOM/issues/347) | Author a Mordor settlement guard pool (content follow-up to #346) | KEEP | `valid-unstarted` | label-only |
| [#349](https://github.com/haterade22/TAOM/issues/349) | CTD: native crash during siege OrderOfBattle formation distribution (engine v1… | KEEP | `blocked-ingame` | comment |
| [#351](https://github.com/haterade22/TAOM/issues/351) | feat(banner-bearers): formations raise their faction standard, bearers keep th… | KEEP | `blocked-ingame` | comment |
| [#352](https://github.com/haterade22/TAOM/issues/352) | fix(armory): siege load hangs forever on two physics-body typos in crafting pi… | KEEP | `blocked-external` | label-only |
| [#353](https://github.com/haterade22/TAOM/issues/353) | feat(prisoner-recruitment): no morale lost recruiting prisoners of your own fa… | KEEP | `blocked-ingame` | label-only |
| [#354](https://github.com/haterade22/TAOM/issues/354) | Age-8 child education CTD: missing stage_2 education character templates (loth… | KEEP | `blocked-ingame` | comment |
| [#355](https://github.com/haterade22/TAOM/issues/355) | LotrIssues: 7 SandBox vanilla issues escape suppression in-game — CTD acceptin… | KEEP | `blocked-ingame` | label-only |
| [#357](https://github.com/haterade22/TAOM/issues/357) | Career pips mislabeled "troop regeneration" — map to TroopSurvival (die→wounde… | KEEP | `partial` | comment |
| [#358](https://github.com/haterade22/TAOM/issues/358) | feat(gondor): incorporate KEYforce noble armor item defs (2026-07-21 drop) — 1… | KEEP | `blocked-external` | comment |
| [#359](https://github.com/haterade22/TAOM/issues/359) | Modding Kit editor asserts at startup: rglConcurrentQueue overflow — TAOM_Map … | KEEP | `blocked-external` | comment |
| [#360](https://github.com/haterade22/TAOM/issues/360) | Siege CTD: AV in reinforcement banner-bearer spawn (unguarded slot-4 read + 2H… | KEEP | `blocked-ingame` | comment |
| [#364](https://github.com/haterade22/TAOM/issues/364) | [Gondor] Riding Caparison unequippable: harness missing <Armor family_type> de… | KEEP | `blocked-external` | comment |
| [#365](https://github.com/haterade22/TAOM/issues/365) | feat(specialresources): taom.add_special_resources console cheat | **CLOSED** | `shipped-and-verified` | close |
| [#369](https://github.com/haterade22/TAOM/issues/369) | feat(devconsole): taom.* developer console — shared contract + command suite | KEEP | `partial` | comment |
| [#370](https://github.com/haterade22/TAOM/issues/370) | feat(coopinterop): stop TAOM sabotaging BannerlordTogether, and make install d… | KEEP | `blocked-ingame` | label-only |
| [#371](https://github.com/haterade22/TAOM/issues/371) | Characters render prone ("bendy man") in every UI tableau on user machines | KEEP | `blocked-ingame` | label-only |
| [#375](https://github.com/haterade22/TAOM/issues/375) | feat: Enlistment — serve as a soldier in a lord's party (native rewrite) | KEEP | `partial` | label-only |
| [#376](https://github.com/haterade22/TAOM/issues/376) | feat: FieldCommission — battlefield promotion of troops into heroes (native Pr… | KEEP | `partial` | label-only |
| [#377](https://github.com/haterade22/TAOM/issues/377) | CareerSystem ability runtime: buff entry survives expiry, no active-duration s… | **CLOSED** | `shipped-and-verified` | close |
| [#378](https://github.com/haterade22/TAOM/issues/378) | Career button feedback: Id + Brush press states + click sound (incl. CareerScr… | **CLOSED** | `shipped-and-verified` | close |
| [#379](https://github.com/haterade22/TAOM/issues/379) | Unspent career points badge on the character screen career button | **CLOSED** | `shipped-and-verified` | close |
| [#380](https://github.com/haterade22/TAOM/issues/380) | Per-career keystone glyphs via data-driven keystone_icon (banner-icon medallio… | KEEP | `partial` | comment |
| [#381](https://github.com/haterade22/TAOM/issues/381) | Keystone branch exclusivity: one keystone per tier (deliberate design adoption… | **CLOSED** | `shipped-and-verified` | close |
| [#383](https://github.com/haterade22/TAOM/issues/383) | Ability damage attribution in the combat log (applied-bonus, not derived) | **CLOSED** | `shipped-and-verified` | close |
| [#384](https://github.com/haterade22/TAOM/issues/384) | Dev tool: mission HUD widget-tree layout dump behind a TaomConsole toggle | **CLOSED** | `shipped-and-verified` | close |
| [#385](https://github.com/haterade22/TAOM/issues/385) | Native CTD: facegen static-morph null-deref (TaleWorlds.Native.dll+0x58232c) —… | KEEP | `blocked-external` | label-only |
| [#386](https://github.com/haterade22/TAOM/issues/386) | [MemSample] periodic process+system memory telemetry in BattleLoadDiagnostics | KEEP | `partial` | label-only |
| [#387](https://github.com/haterade22/TAOM/issues/387) | native_crash_triage.py: --dump mode (minidump parse: faulting module, RVA, com… | **CLOSED** | `shipped-and-verified` | close |
| [#390](https://github.com/haterade22/TAOM/issues/390) | fix(content): 42 crafting-piece/shield meta-meshes referenced but missing from… | KEEP | `blocked-external` | label-only |
| [#391](https://github.com/haterade22/TAOM/issues/391) | feat(diagnostics): attribute town-gold drains and name why each caravan is par… | **CLOSED** | `shipped-and-verified` | close |
| [#392](https://github.com/haterade22/TAOM/issues/392) | Sprite generator registers clan_diamond_border_neutral but never packs it into… | KEEP | `partial` | comment |
| [#393](https://github.com/haterade22/TAOM/issues/393) | fix(economy): town market gold drains to ~0 daily — villager deliveries spend … | KEEP | `blocked-ingame` | comment |
| [#396](https://github.com/haterade22/TAOM/issues/396) | feat(caravan): rescue caravans that park permanently — the engine gives them n… | KEEP | `blocked-ingame` | comment |
| [#398](https://github.com/haterade22/TAOM/issues/398) | fix(assets): Uruk-Hai helmet/bracer/greave/pauldron meshes bundle hand+glove s… | KEEP | `blocked-external` | label-only |
| [#404](https://github.com/haterade22/TAOM/issues/404) | fix(tools): thirteen tools still hardcode the game install, nine of which writ… | **CLOSED** | `shipped-and-verified` | close |
| [#406](https://github.com/haterade22/TAOM/issues/406) | fix(enlistment): enlisted player never joins the commander's battles | KEEP | `partial` | label-only |
| [#407](https://github.com/haterade22/TAOM/issues/407) | fix(arena): tournament winner panel NREs on a null MapFaction or Culture | KEEP | `blocked-ingame` | label-only |
| [#408](https://github.com/haterade22/TAOM/issues/408) | fix(enlistment): bound the hourly battle-join retry so a failing join can't po… | KEEP | `blocked-ingame` | label-only |
| [#415](https://github.com/haterade22/TAOM/issues/415) | fix(fieldcommission): promoted companions — no dialogue, crash/freeze on inter… | KEEP | `blocked-ingame` | label-only |
| [#418](https://github.com/haterade22/TAOM/issues/418) | feat(localization): translate the 76 enlistment + battlefield-promotion string… | **CLOSED** | `shipped-and-verified` | close |
| [#419](https://github.com/haterade22/TAOM/issues/419) | Civilian equipment rosters are battle kit at source across 12 cultures | ESCALATE | `blocked-decision` | label-only |
| [#420](https://github.com/haterade22/TAOM/issues/420) | Eight docs claim verification against an 'installed' engine that has since mov… | KEEP | `valid-unstarted` | label-only |
| [#421](https://github.com/haterade22/TAOM/issues/421) | No CI verifies any PR: Build & Test is skipped, and nothing runs the Python su… | KEEP | `valid-unstarted` | label-only |
| [#422](https://github.com/haterade22/TAOM/issues/422) | Tournament team templates and the six XSLT-renamed cultures still point at van… | KEEP | `valid-unstarted` | label-only |

## By cluster

| Cluster | Issues | Closed | Kept |
|---|---|---|---|
| Lords-skills sweep | 18 | 18 | 0 |
| Town gold / item value / caravans | 8 | 1 | 7 |
| Enlistment + FieldCommission | 6 | 1 | 5 |
| CareerSystem + sprite atlas + deploy | 12 | 8 | 4 |
| Facegen / morph / tableau | 7 | 3 | 4 |
| Race, creature data, food, troop weight | 6 | 2 | 4 |
| Lords / culture / equipment rosters | 9 | 6 | 3 |
| Troop skills + balance | 5 | 0 | 5 |
| Battle-load / shader / external assets | 8 | 2 | 6 |
| Siege / banner-bearer CTDs | 5 | 0 | 5 |
| Mounts + combat mechanics | 6 | 3 | 3 |
| Settlement guards + tournament | 5 | 1 | 4 |
| New-campaign start + character creation | 7 | 4 | 3 |
| LotrIssues + culture conversion | 6 | 2 | 4 |
| Bandits, world, siege defense | 9 | 4 | 5 |
| Dev console + map / editor / naval | 11 | 4 | 7 |
| Harness, skills, tooling | 10 | 9 | 1 |
| Interop, CI, docs, migration | 9 | 6 | 3 |

---

_Every close is reversible with `gh issue reopen <N>`. The per-issue evidence is in each closing comment; the run ledger is `idx/ledger.jsonl` in the session scratchpad._
