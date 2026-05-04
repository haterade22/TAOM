# Codex Adversarial Review: Spider Feature (TAOM)

You are reviewing TAOM's new Spider feature. This is a Bannerlord 1.3.15 mod (LOTR total conversion).

## Feature description

The spider feature spawns AI-controlled giant spider agents directly into Custom Battle missions as hostile mobs. The engine cannot host non-humanoid creatures as ordinary NPCCharacter troops (race resolution is hardcoded humanoid-only via FaceGen.GetRaceIds). Workaround: a humanoid-race anchor character (`taom_spider_creature`, race="dg_uruk") satisfies AgentBuildData's BasicCharacterObject requirement; the visual is overridden at spawn time by AgentBuildData.Monster(spiderMonster). The spider engages enemies in melee via a custom bite attack with bone-collision damage detection. The pattern mirrors Main/Features/Warg/ but uses IAgentAdapter at the service boundary (corrects the ADR-007 violation present in IWargAttackService).

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar

Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale

NOTE: "rohan" is NOT a valid ID — Rohan uses "vlandia". "dol_guldur" is NOT valid — use "dolguldur".

## READ FIRST

- docs/features/spider.md (feature documentation, architecture)
- CHANGELOG.md (most recent entry under 2026-04-23)
- Main/_Module/SubModule.xml (LOTRLOME_Armory dependency declaration, character XML registration)
- Main/_Module/ModuleData/characters/spider_creature.xml (anchor NPCCharacter)

The feature also depends on data shipped in a separate module repo:
- E:/repos/lotraom-assets/shared/LOTRLOME_Armory/ModuleData/monsters.xml — Monster id="spider" (search for `id="spider"`)
- E:/repos/lotraom-assets/shared/LOTRLOME_Armory/ModuleData/action_sets.xml — `as_spider`
- E:/repos/lotraom-assets/shared/LOTRLOME_Armory/ModuleData/action_types.xml
- E:/repos/lotraom-assets/shared/LOTRLOME_Armory/ModuleData/monster_usage_sets.xml

## Known Suspects (CONFIRM or DISPUTE each)

1. **Mission API misuse — vanilla `SpawnAgent` may force-override Monster from BasicCharacterObject's race**: The whole approach relies on `AgentBuildData.Monster(spiderMonster)` overriding the character's default Monster derived from its race. If the engine internally re-derives the Monster from the character's race AFTER `Monster()` is called, the spider would spawn as a dg_uruk humanoid instead of a spider. Decompile `Mission.SpawnAgent(AgentBuildData)` and `AgentBuildData.AgentMonster` getter from the installed v1.3.15 DLL at `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll` to verify the Monster() override is honored at spawn time. This is the single most important question — if Codex disputes the workaround works, the entire feature is dead on arrival.

2. **Custom Battle gating false positive/negative**: `SpiderMissionBehavior.ShouldSpawnInThisMission()` uses `Mission.Current.GetMissionBehavior<CustomBattleAgentLogic>() != null` plus `Mission.Current.Mode == MissionMode.Battle`. Decompile `CustomBattleAgentLogic` and identify which mission types it's added to. Could it leak into campaign battles (false positive)? Could it be absent from Custom Battle in some scenarios (false negative)? The gate's intent is "only spawn in Custom Battle, never in campaign" — verify it actually achieves this.

3. **Anchor character bleed-through**: `taom_spider_creature` has `hidden_in_encyclopedia="true"`, `is_basic_troop="false"`, race="dg_uruk", culture="dolguldur". Could it appear in: Custom Battle troop picker, Encyclopedia, faction recruitment, party templates, conversation pools? The intent is that the anchor exists ONLY for AgentBuildData's BasicCharacterObject requirement and never appears in any UI or roster. Cross-check whether Bannerlord respects `hidden_in_encyclopedia` for Custom Battle pickers and whether `is_basic_troop="false"` excludes it from recruitment.

4. **Bone collision indices are warg placeholders**: `SpiderConfig.FangBoneIndexPrimary=23, FangBoneIndexSecondaryLeft=37, FangBoneIndexSecondaryRight=43` are explicitly noted as placeholder values copied from the warg pattern. The spider's actual fangs are bones `joint5_l/r` and `joint12_m`. At runtime, the bone-collision system in CustomAttack will use indices 23/37/43 against the spider skeleton — these will resolve to whichever bones happen to be at those positions in the spider's bone array. Verify whether this is a cosmetic miss (bites visually wrong but still hit targets via the SpatialGrid range check) or a functional break (collision detection fails entirely because the bone indices don't exist in a smaller skeleton).

5. **`CustomBattleAgentLogic` reference fragility**: SpiderMissionBehavior.cs:68 references `CustomBattleAgentLogic` resolved via `using TaleWorlds.MountAndBlade;`. If TaleWorlds renames or moves this class in a future version, the gate silently returns false and no spiders spawn — no compile error, no runtime exception, just dead behavior. Is this a future-proofing concern worth a defensive null-check or fallback?

6. **Spawn timing race condition**: SpiderMissionBehavior.OnMissionTick spawns spiders when `_timeSinceStart >= 1f` AND `!_spawned` AND `ShouldSpawnInThisMission()` returns true. At t=1s, has the player team been formed? Is `Mission.Current.MainAgent` populated yet? If MainAgent is null, the reference position falls back to `Vec3.Zero` (origin), which could spawn spiders at scene origin rather than near the player. Trace the Mission lifecycle: when does MainAgent become non-null? When are Teams populated?

## File lists (TAOM repo)

### Spider feature (`Main/Features/Spider/`)
- SpiderConfig.cs (static config)
- ISpiderAttackService.cs + SpiderAttackService.cs (bite damage + hit handling)
- ISpiderSpawnerService.cs + SpiderSpawnerService.cs (Mission.SpawnAgent wrapper)
- SpiderBehaviorTree.cs (BT composition)
- SpiderMissionBehavior.cs (mission lifecycle hook, BT attachment)
- SpiderIoC.cs (DryIoc registration)
- BehaviorTreeElements/IBTSpiderBlackboard.cs
- BehaviorTreeElements/NoEnemyNearSpiderDecorator.cs
- BehaviorTreeElements/SpiderAttackTask.cs
- BehaviorTreeElements/OnSpiderDied.cs

### Adapters (modified)
- Main/Adapters/IAgentAdapter.cs (added IsSpider, IsSameTeam, Health, State, GetBaseArmorEffectivenessForBodyPart)
- Main/Adapters/AgentAdapter.cs (implementations)

### Wiring (modified)
- Main/IoC.cs (added SpiderIoC.RegisterSpiderFeature)
- Main/SubModule.cs (added new SpiderMissionBehavior() in OnMissionBehaviorInitialize)
- Main/_Module/SubModule.xml (added LOTRLOME_Armory optional dependency, registered characters/spider_creature.xml)

### Anchor data
- Main/_Module/ModuleData/characters/spider_creature.xml (NPCCharacter id="taom_spider_creature")

### Tests
- TAOM.Tests/Features/Spider/SpiderAttackServiceTests.cs (14 tests)
- TAOM.Tests/Features/Spider/SpiderSpawnerServiceTests.cs (6 tests)

### Documentation
- docs/features/spider.md
- CHANGELOG.md (entry under 2026-04-23)

## REQUIRED SECTIONS

### 1. VANILLA CODE — paste decompiled vanilla code as code blocks for:

- `TaleWorlds.MountAndBlade.Mission.SpawnAgent(AgentBuildData, bool)` — the full method body. We need to see whether it derives the Monster from the character or honors AgentBuildData.AgentMonster.
- `TaleWorlds.MountAndBlade.AgentBuildData.AgentMonster` getter — verify it returns the explicitly-set Monster (from `.Monster()`) and falls back to character.Monster only if unset.
- `TaleWorlds.MountAndBlade.AgentBuildData.Monster(Monster)` setter — verify it actually stores the Monster and is not silently ignored.
- `TaleWorlds.MountAndBlade.CustomBattleAgentLogic` — full class declaration. We need to know which mission types it's added to and whether it's a reliable gate for Custom Battle vs campaign.
- The full class signature of `BehaviorTreeAgentComponent` (in BehaviorTreeWrapper.dll) — verify the constructor `(Agent, string, params object[])` exists and that `Tree`, `OnTickAsAI(float)` are public members.

Decompile from the installed game DLLs. The decompiled folder at `E:/Decompiled_Bannerlord/` is v1.4 and may differ from the installed v1.3.15 — always verify against the installed DLL via `ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t Full.Type.Name`.

### 2. Feature-specific deep analysis — CONCRETE SCENARIOS

For each scenario, walk through the code and report what happens:

A. **First Custom Battle launch.** Player picks Empire vs Sturgia. SpiderMissionBehavior is added in OnMissionBehaviorInitialize. Mission ticks. At t=1s, what fires? Trace from OnMissionTick line 86 through Initialize() through ShouldSpawnInThisMission() through PickEnemyTeam() through SpawnSpiders. Does each step succeed? At what point could MainAgent or Teams be null/empty?

B. **Second Custom Battle launch in same session.** SpiderMissionBehavior was disposed via OnRemoveBehavior on first mission end. On second mission, a new instance is created. Does anything carry over? Check static state in SpiderConfig, in BTRegister.RegisterClass (does double-registering "SpiderTree" throw?), in SpatialGrid.Instance lifecycle. Re-launching the same class name is a known footgun.

C. **Mid-battle spider death.** Spider takes lethal damage. Walk through: who calls OnSpiderDied (constant event listener)? When is `_spiderComponents` pruned? Does the BT framework call our constant listener BEFORE or AFTER the agent is removed from Mission.Current.AllAgents? Is there a race where OnMissionTick still iterates _spiderComponents while a spider's IsActive() flips false?

D. **Player team has no enemies.** PickEnemyTeam returns null. SpawnSpiders is skipped. _spawned is set true. After spawn-skip, no spiders ever spawn even if enemy teams arrive later. Is this acceptable for Custom Battle (where teams are fixed at start)?

E. **LOTRLOME_Armory not loaded.** `<DependedModule Id="LOTRLOME_Armory" optional="true" />`. If LOTRLOME_Armory is missing/disabled, MBObjectManager.GetObject<Monster>("spider") returns null. SpiderSpawnerService logs an error and returns empty list. Verify: does the engine block TAOM from loading, or does TAOM load with the spider feature gracefully no-oping? Does the optional flag actually allow TAOM to load without LOTRLOME_Armory?

F. **Bone-collision attack with placeholder indices.** SpiderAttackService.SpiderAttack → spider.CustomAttack(action, boneIds=[23,37,43], radius=0.3, ...). At runtime, the BoneCheckDuringAnimation tracks bones at those indices on the spider skeleton. The spider has 62 LimbNodes; bone index 23 is some random middle-of-leg bone. Will collision detection still fire when the spider's mouth passes through a target, or will it only fire when index-23 (a leg bone) passes through? Trace SpatialGrid.Instance.GetNearAliveAgentsInRange + BoneCheckDuringAnimation — does the bone-position check actually require the specified bone to intersect a target, or is the bone list just a "preference"?

### 3. CONFIG CROSS-REFERENCE

Walk through every literal string in the Spider feature C# code and verify it matches a corresponding XML entry:

- `"spider"` (Monster id) — appears in SpiderSpawnerService.SpiderMonsterId; verify in monsters.xml
- `"taom_spider_creature"` (anchor id) — appears in SpiderSpawnerService.SpiderCharacterId and spider_creature.xml
- `"act_spider_attack_charge"`, `"act_spider_attack_front"` — in SpiderAttackService; verify in action_types.xml + action_sets.xml
- `"SpiderTree"` (BT class name) — in SpiderMissionBehavior + SpiderBehaviorTree; verify exact match (case-sensitive)
- `"as_spider"` (action_set id) — referenced indirectly via Monster's action_set attribute; verify in action_sets.xml
- `"spider"` (monster_usage id) — referenced via Monster's monster_usage attribute; verify in monster_usage_sets.xml

For the anchor character `taom_spider_creature`:
- race="dg_uruk" — verify dg_uruk is a valid race in skins.xml
- culture="Culture.dolguldur" — verify dolguldur is a valid culture (per cheatsheet, this is correct)
- BodyProperty.fighter_dolguldur — verify this template exists in TAOM_bodyproperties.xml

### 4. FINDINGS OR OBSERVATIONS

After the analysis, produce a findings list. For each finding:
- File:line
- Severity (CRITICAL / HIGH / MEDIUM / LOW)
- Confirmed reproduction or theoretical issue
- Suggested fix
- Whether it confirms or disputes one of the Known Suspects above

If you find ZERO issues beyond the Known Suspects, say so explicitly — "no additional findings beyond the suspects." Don't pad findings to make the review look productive.

## QUALITY GATES

This review is read-only. Do not make any code changes.

For every finding you make:
1. Quote the exact line(s) of TAOM source you're flagging
2. Quote the exact lines of vanilla source (decompiled) that justify the finding
3. State the runtime scenario where the bug would manifest

If you cannot satisfy all three, do not file the finding — file an OBSERVATION instead.

False positives we've seen on prior reviews:
- Codex assumed empire = Rohan (it's Dunland)
- Codex flagged code that intentionally matched vanilla as a bug
- Codex skipped sections it found difficult ("appears correct" without verification)
- Codex flagged vanilla v1.4 differences as bugs when reviewing v1.3.15 code

Avoid these patterns. If you cite the cheatsheet's IDs incorrectly or skip a vanilla decompilation, the finding will be dismissed.

## Prior review lessons

SUCCESSES:
- Config ID cross-reference caught rohan/dol_guldur mismatches in two prior reviews
- Vanilla decompilation caught missing fail-safe gates in three prior reviews
- Lifecycle tracing caught stale caches across save/load and across CustomBattle sessions

FAILURES:
- Codex assumed empire=Rohan (it is Dunland)
- Codex flagged vanilla-matching code as bugs
- Codex skipped hard sections (decompilation, full lifecycle trace) and just said "looks correct"
- Codex marked findings as HIGH severity without quoting both source files

## Output

Write your full review to: `docs/reviews/codex-adversarial-spider-2026-04-23.md`

Replace this prompt content with the review. Include all sections above as analysis output, not as prompt repetition.
