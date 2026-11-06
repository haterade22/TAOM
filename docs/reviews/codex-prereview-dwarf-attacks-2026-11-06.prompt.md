Adversarial pre-review of an uncommitted feature in E:\repos\TAOM (branch bannerlord-1.5.x, HEAD ab7e51acb). Issue #1087. Read-only: do not edit any file. Return the full report as your FINAL MESSAGE; do not write it to a file.

FEATURE: Dwarf Attacks. AI dwarves on foot (Monster "dwarf") get a per-agent behaviour tree that plays two bespoke actions from the unversioned LOTRLOME_Armory: act_dwarf_head_butt (enemy within 1.3 m) and act_dwarf_drop_kick (enemy 1.8 to 2.6 m; its clip carries a DisplacementUsage of 2.0 m). At the clip's impact fraction the tree deals Blunt knock-back damage to ONE enemy through CustomAttacksUtils.TakeDamage. The pattern copies TrollBruteForce (Main/Features/TrollBruteForce/) and the war ram's single-victim pick (Main/Features/ElephantLike/SingleVictimPick.cs). Two MCM settings (switch, damage percent). In-game evidence through IOutcomeLog ([Check] lines and counters).

Maintainer decisions (not open): a TAOM behaviour tree fires both attacks; clips hand-keyed in Blender; the player's own dwarf never attacks; one MCM switch and one damage slider; actions typed actt_kick.

READ FIRST: docs/features/dwarf-attacks.md, docs/reference/lotrlome-dwarf-attack-changes.md, docs/features/troll-brute-force.md, docs/features/war-ram.md ("The head-butt clip"), .claude/rules/csharp-architecture.md ("Mission-scope agent handles and the engine's threads"), docs/features/coop-interop.md (the AgentOwner rows and "tree seam").

CHANGED FILES:
Main/Features/DwarfAttacks/** (new: DwarfAttackService, DwarfAttacksConfig, DwarfAttacksSettingsProvider, DwarfAttackChecks, DwarfAttacksCombat, DwarfAttacksBehaviorTree, BehaviorTreeElements/*, Hooks/DwarfAttackHit, DwarfAttacksIoC, DwarfAttacksMissionBehavior)
Main/Features/TaomSettings.cs (the "Battle Tactics/Dwarf Attacks" group at the end), Main/IoC.cs and Main/SubModule.cs (one line each), Main/Features/CoopInterop/Missions/CoopMissionRoles.cs
TAOM.Tests/Features/DwarfAttacks/** (new) and gate edits in HotPathSettingsProvidersTests, CreatureTreeClockTests, BehaviorTreeMissionLogicInheritanceTests, CoopMissionRolePolicyTests, SettingsFingerprintTests, ServerSettingsSchemaTests
tools/gen_dwarf_attack_clips.ps1, tools/bind_dwarf_attack_actions.py, tools/blender/author_dwarf_attack_clips.py and their tests; tools/ingame_checks/1087.toml
docs/reference/lotrlome-armory-snapshot/action_types.xml and action_sets.xml (mirror the live Armory edit)
(Other sessions' uncommitted dire-wolf and Tripo files in the tree are OUT OF SCOPE.)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. Tree mechanics. DwarfAttackReadyDecorator returns false while the dwarf plays one of our actions. If the BehaviorTrees runtime re-evaluates a sequence's decorator on every tick while its task is Running, the attack is aborted before impact. The troll tree has the same shape: find the runtime code (BehaviorTrees / BehaviorTreeWrapper sources in the repo) and state when a decorator is evaluated. Also: does the tree builder copy BTBlackboardValue<DwarfAttack?> Chosen onto the nodes (how does the reflection copy match properties)?
2. Displacement. DwarfAttackHit judges the victim from the dwarf's Agent.Position at the impact. Does the drop kick clip's DisplacementUsage (with the displace_position flag) move Agent.Position during the clip, and does it respect the navmesh and walls (a dwarf on a siege wall top, at a ledge)? Native evidence (python tools/native_decompile.py) or UNVERIFIED.
3. actt_kick. Beyond StandingPoint.DoesActionTypeStopUsingGameObject, what does the engine do with an action of type Kick on channel 0 started by SetActionChannel: AI kick logic, the kick collision path that needs a CombatParameterId (the clips have none), Agent.IsInBeingStruckAction ranges, anything that could throw or null-read?
4. Victims. TakeDamage with knockDown on: the player, a troll (own skeleton), an agent in a tournament or arena practice mission, a hero in a duel. The shield-block test reads only channel 1 DefendShield, not the shield's facing. Which of these is a real defect?
5. Co-op and threads. Is the AgentOwner classification right (the tree seam in BehaviorTreeAgentComponent.TickOnMissionThread)? Does the blow from TakeDamage replicate, or double, in a co-op session? Any engine call here off the main thread?
6. Handles. Every held Agent goes through AgentSlotIdentity.IsCurrentOccupant before native access? The static MBList Buffer in DwarfAttackHit?
7. Settings. Both settings are counted simulation-relevant in the co-op fingerprint (SettingsFingerprintTests: 490 in TaomSettings, 333 covered). Correct? The damage clamp; MCM "(%)" in a display name.
8. Clip tool. tools/gen_dwarf_attack_clips.ps1: the field rules in Get-ClipWant / Get-ClipFault, the DisplacementUsage vector (0, 2.0, 0, 1) and end progress (EndFrame-1)/(S2-S1), the refusal paths, the checksum fix and re-read.
9. Bind tool. tools/bind_dwarf_attack_actions.py: byte faithfulness (BOM, CRLF), the refusals, idempotence, the --check gate; and the live insertion (backups action_types.xml.bak-dwarf-attacks-20261106-124630 and action_sets.xml.bak-... beside the live files under E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData).
10. Cost. Hundreds of dwarves each run a tree that scans GetNearbyEnemyAgents at 2.6 m about three times a second. Is GetNearbyEnemyAgents built on the same native query whose branch past 15 m scans every agent (docs/ai-includes/orientation.md "Native branches")? Any per-frame allocation?

REQUIRED SECTIONS: per suspect verdict with evidence; ENGINE (quote the installed v1.5.6 TaleWorlds members you rely on via the ilspy MCP or pwsh tools/taom-src.ps1 path <Type>); FINDINGS (severity CRITICAL/HIGH/MEDIUM/LOW, file:line, failure scenario); OBSERVATIONS.

QUALITY GATES: every claim cites a file and line you read; no invented API; missing evidence is UNVERIFIED.
