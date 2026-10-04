# RCA: Race Abilities (#730), deep review rounds 1 and 2, 2026-10-04

## Top-line

Race Abilities went through two `/deep-review` rounds. Round 1 (lenses 1, 2, 5, 7 on the Phase A working tree)
returned 1 HIGH, 4 MEDIUM and a run of LOW findings, all confirmed by re-reading the code and the v1.5.3 decompile,
and all fixed in commit `a498e357` before it was pushed, together with Phase B (the orc family), Phase C (men by
culture) and the telemetry logging. One more defect, a mission logic that would never have run, was caught by an
existing structural test before review. Round 2 reviewed `a498e357` against `569f6750` (all seven lenses); its
findings are recorded below.

## Round 1 findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | HIGH (lens 1) | `RaceAbilityRuntime` (300 lines) held game rules no test reached: the rally gate (the only thing keeping the player's character out of a kinsman's rally), the cavalry-closing geometry and its 3 m/s threshold, kill credit, the heal arithmetic, fallen-kin pruning | Testability, boundary bloat | I labelled the class "boundary, game-tested (ADR-008)" and let rules gather there, treating the label as a coverage exemption. The 80% Hooks row and ADR-002 apply to what the class does, not to what it is called | Moved every rule into `RaceAbilityService` (floats and flags in) with one test each; split the boundary into runtime, sensor, activator, ticker and deaths, each under 150 lines. Lesson appended: a boundary class that branches is a service in disguise |
| R2 | MED (lens 1) | The model facade `RaceAbilityHooks` held two untested rules: melee-only amplification and no reduction for a hit on the horse | Testability | Same as R1, one layer down: "the model body is a one-line delegate" was true only because the rule moved into the facade | `AmplifyHit(damage, effects, isMissile, isHorseCharge, isFallDamage)` and `Reduce(damage, effects, victimIsMount)` in the service, tested |
| R3 | MED (lens 2 F1, lens 5 T13) | The doc claimed shrug-off and knock-back resistance spare a Stand Fast dwarf a charge's knock-back. The engine sets knock-back for a frontal horse charge by a facing test and for every kick or bash before it reads shrug-off or `GetKnockBackResistance` (`MissionCombatMechanicsHelper.cs:56-71`); charges never call `DecideAgentShrugOffBlow` (`Mission.cs:6101-6133`) | Engine claim from one branch | I verified who READS the knock-back resistance, not every branch that PRODUCES a knock-back. The resistance reader exists, so the effect looked wired | Doc and code comments corrected (decision: no charge override, since what native does with a charge whose knock-back flag is cleared is UNVERIFIED). Knockdown resistance still keeps the dwarf on his feet. Lesson appended: trace every producer of the outcome you claim to prevent |
| R4 | MED (lens 5 T10) | Speed and acceleration effects landed on a mounted soldier's own stat bag; a rider moves at his horse's speed, so mounted elves got nothing and ram-riding dwarves paid no price | Repeat of #611 | My compounding check asked "is this property rewritten every update" but not "who reads it while mounted". The career system shipped the same bug and fixed it in #611 | `MountSpeedPercent` written on the horse's stats through its rider (`RaceAbilityHooks.ApplyStats` mount hop, `MountAgent.UpdateAgentProperties` on activation and phase change); the doc says "on foot only" for the rider fields. Second occurrence: lesson strengthened |
| R5 | LOW (lens 1, 2, 5) | `RaceAbilitiesMissionLogic` owned a `DeferredCallbackQueue` but did not call `MissionThreadGuard.MarkMainThread()` | Rule not loaded | The rule lives in `harmony-patches.md`, which loads when a `Hooks/` file is READ. I wrote the new file without reading a sibling, so the path rule never loaded (see the pattern below) | Added the call; `RaceAbilitiesWiringTests` pins it |
| R6 | LOW (lens 5 T22) | The heal on kill could write NaN into `Agent.Health` through `Math.Min` with a NaN limit | NaN gate (engine float arithmetic written back) | The decision gates were positive requirements, but this was arithmetic, not a gate, and I did not apply the exit check to it | `RaceAbilityService.HealOnKill` returns the old health unless the result is finite and higher; tested with NaN health and NaN limit |
| R7 | LOW (lens 5 T15) | Three config shapes were accepted and never read: kill effects in a `spent` block, a `spent` block without `spentSeconds`, a kill extension with no room to extend | Silent config | The validator checked ranges, not whether a consumer exists for the combination | Kill effects now read the current phase's effects; the other two warn at load, tested |
| R8 | LOW (lens 5 T16) | Fear on kill applied a flat morale drain, unlike the Dread Aura and signature strikes, which scale through the morale model and skip agents with no morale | Inconsistency with siblings | I wrote the fear before reading how the two existing fear sources apply theirs | Scaled through `BattleMoraleModel.CalculateMoraleChangeToCharacter` and gated by `DreadAgentGate.CanAffect`, as they do |
| R9 | LOW (lens 1) | Tree nodes resolved services per node (service locator) where the warg tree injects from `BuildTree`; an `IdleTask` duplicated `ReturnTrueTask`; an interface no test faked; two unused members; an `if` and an eagerly built crush context in model bodies | Conventions | Copied the older troll tree shape instead of the warg tree, which carries Mike's decision 32 | All applied; the decorator also gained the catch its own comment promised |
| R10 | LOW (lens 7) | The localization harvester adds one blank line before `</strings>` on every run (`tools/harvest_literal_loc_keys.py:157-163`) | Pre-existing tool bug | Not this change's code | The extra lines were trimmed from this change's diff; the tool fix is a follow-up |
| R11 | Caught by a test, pre-review | `RaceAbilitiesMissionLogic` gated itself in `OnBehaviorInitialize`, which never runs for a behavior a feature module adds (#606): the feature would never have started | Engine lifecycle | Same mechanism as R5: the #606 table lives in `harmony-patches.md`, which loads on reading a `Hooks/` file, and I only wrote one. `MissionBehaviorLifecycleTests` caught it at the first full-suite run | Gate decided lazily on the first tick, as `SignatureStrikesMissionLogic` does |

## Root-cause pattern: path rules load on Read, not on Write

R5 and R11 share one mechanism. Both rules were written down, in `harmony-patches.md`, whose `paths:` covers
`Main/**/Hooks/**`. Claude Code loads a path-scoped rule when a matching file is READ (`harness-facts.md`, "Rule
loader"). Writing a brand-new file under `Hooks/` never reads one, so neither rule entered the session until a
review lens quoted it. The structural test caught R11; nothing caught R5 until review.

**Preventive action:** before creating a new file under a rule-scoped directory, read one existing sibling in that
directory first, so the scoped rules load. Recorded as a lesson. A wiring test now pins `MarkMainThread`, and
`MissionBehaviorLifecycleTests` already pins R11's class.

## Why each round-1 lens did or did not catch what it caught

- **Standards (1):** found R1, R2, R5 and R9 by reading the coverage table and ADR-002 against what each class does.
- **Engine compatibility (2):** found R3 by opening `Mission.ChargeDamageCallback` and every branch of
  `DecideAgentKnockedBackByBlow`; verified 31 other engine claims.
- **Data flow (5):** found R3, R4, R6, R7 and R8 by asking, for every effect field, whether it reaches a consumer in
  both modes and for mounted soldiers.
- **XML (7):** found R10; checked every race key against the live `skins.xml`.

## Round 2 findings

All seven lenses reviewed `a498e357` against `569f6750`: standards, engine compatibility, data flow and XML first,
then efficiency, completeness and design. Each finding below was confirmed against the code or the v1.5.3
decompile before it was fixed; three are Mike's decisions and are documented as known limits meanwhile.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R12 | CRIT (lens 1) | `RaceAbilitiesMissionLogic.cs` was 159 lines, over ADR-002's 150 | Entry-point size | Each round-1 fix (the main-thread mark, the lazy gate, the tree counting) added a few lines to the entry point, and nobody measured it again. No gate measures a mission logic's length | The per-agent rules moved into the runtime and the constructor takes only the runtime: 142 lines. Lesson: re-measure every entry point a fix round touches. A ratcheted size gate is a follow-up (`TaomCombatMechanicsModel` is already 280 lines, so it needs a baseline) |
| R13 | MED (lenses 1, 4) | The container wiring test built engine lists (`MBList`, whose constructor is `throw null` in the CI reference assemblies) and carried no `RequiresGame` tag | CI test category | The engine construction is three levels down (container, then the runtime, then its parts' field initializers), invisible from the test's own lines. `tests.md` names "even through TAOM code", but only a trace finds it | Tagged. Lesson: a test that resolves a container graph executes every constructor in it; trace them before deciding the category |
| R14 | MED (lens 2 F1, lens 5 L7) | A horse a profiled soldier killed counted as a kill: Bloodlust extended, the killer healed, fear spread around the carcass | Agent-kind blind spot | I modelled the credit on vanilla's `KillCount`, which counts horses, and assumed the same-team gate would stop a horse. Mounts are spawned with no `Team` (`Mission.cs:4324`, only the rider gets `SetTeam` at `:4215`), so `null == team` failed nothing | `CreditsKill` requires a soldier (`Character != null`), passed in from the callback; a test pins a horse's death. Lesson: list every agent kind a removal callback carries (soldier, mount, riderless creature, the player) and the value of each field a rule reads for each |
| R15 | MED (lens 2 F2) | In campaign, Combat Mechanics floors any man hit by a full-speed charge before it reads knockdown resistance (`ChargeKnockdownService`, Branch A), so Wainrider Wall's and Variag Ferocity's x2 does nothing there | Cross-feature rule order | Round 1 traced every engine reader of the resistance, but not TAOM's own campaign override that decides before the engine does | Documented as a known limit; whether a live ability should hold in Branch A is Mike's decision |
| R16 | MED (lens 5 M1) | Scripted creature blows (`CustomAttacksUtils.TakeDamage`: the troll ring, warg bites, trample, the signature ring) write damage past the damage models, so no defensive effect applies | Parallel damage path | The data-flow trace followed the engine's blow pipeline only; TAOM's own synthetic-blow funnel bypasses it by design | Documented as a known limit; routing the funnel through the race hooks is Mike's decision |
| R17 | MED (lens 7) | Khand fields Rhun's troops (`spcultures.xslt:1348-1355`; `loke_rim_initiate` is `Culture.khuzait`), so Variag Ferocity reaches only Khand's lords and town guard, and the commit body's "Khand's Variags fight savage" was wrong. Umbar's levies are Harad troops too | Data meets roster | The live-install test proved each culture key exists, not that the culture fields soldiers. A culture-keyed config needs the troops that carry the culture, not the culture's definition | Documented (feature doc, roster tables). Decided by Mike 2026-10-04: leave Khand's ability as it is; he expects Khand's armies to use a lot of cavalry and chariots. A second pass corrected the carriers: Variag Ferocity also reaches Khand's caravan masters, its tavern mercenaries (`caravan_guard_khand`) and the Variag Ravagers (vanilla's Wolfskins, kept `battania`), and Umbar's own recruits, lords and armies are `umbar` (its militia, patrols, villagers, rebels and starting garrisons are Harad). Lesson: check a culture-keyed config against the cultures the TROOPS carry, after XSLT roster sharing, minor factions, caravans and volunteer pools included |
| R18 | MED (lens 7) | The culture-key test counted any `<Culture id>` element in ModuleData, so a wrong key such as `dale` (`cc_body_properties.xml:50`) would pass | Test soundness | Copied a regex that matches config rows of the same element name, the BannerBearers lesson's exact shape | The test reads `taom_spcultures.xml` (root `SPCultures`) plus the six re-skinned vanilla ids, and runs on CI |
| R19 | LOW (lens 4 F6, proven) | `Enum.TryParse` ORs a comma list into another kind: `"EnemyWithin,CavalryClosing"` became `RangedTargetWithin` silently | Parsing | I guarded the number case and not the flags case | Exact-name match, rows for a list, a number and an empty kind |
| R20 | HIGH (lens 3 E1) | `BehaviorTreeMissionLogic.OnMissionTick` copied the schedule with `List.AddRange`, which allocates an array the size of the schedule every frame; the race abilities put every profiled soldier on it (about 8 KB a frame at 1,000 trees) | Hot-path allocation | Round 1 ran wave one only (lenses 1, 2, 5, 7): no efficiency lens looked. The design weighed the tree's own tick, not the shared host's per-frame work that now scales with it | Element copy, with an IL pin that the tick never calls `AddRange` or `InsertRange` |
| R21 | MED (lens 3 E2, lens 5 L2) | The sensor scanned 30 to 40 m for soldiers whose own state already ruled the ability out (a Rohirrim on foot, a Dale soldier without a bow), widened enemy scans to kin radii, read closing speed for every profile, and walked fallen kin for every profile | Hot-path waste | Same as R20 | `RaceAbilityService.PlanScan`, with a test that the plan never changes `FiringTrigger`'s answer for any shipped profile across a grid of states |
| R22 | MED (lens 3 E3) | Scratch buffers on the singleton runtime's parts kept a finished battle's agents, and through their teams the mission, reachable | Lifecycle | `Clear()` reset the state and forgot the buffers | Every part clears at mission end; a wiring test pins it |
| R23 | MED (lens 3 E4) | `RaceAbilitySettingsProvider` resolved MCM's settings on every read, on every decision and every tick | Hot-path settings | I wrote it from an older provider shape; the cached pattern lives in `HotPathSettingsProvidersTests`, which no rule points a new provider to | Cached and read through; added to `HotPathSettingsProvidersTests` |
| R24 | MED (lens 3 E5) | The 30 s report clock summed the telemetry (all its locks and two allocations) every frame before asking the time | Hot-path waste | Same as R20 | `IsDue` first |

LOW, all fixed or documented: the console command moved to `Cheats/` and became `print_race_abilities` (lens 1);
the scan rule, the morale price and the live counts moved out of boundary code into the service and the store
(lens 1); one test class per file, `RepoPaths`, one bad-value row per numeric effect with a completeness test, two
dead members removed (lens 1); the JSON `enabled` switch now in the gate and the console (lens 5); telemetry labels
that say what they count (lens 5); the deaths guarded against a throw inside the engine's removal loop (lens 5);
unread config now warns (goblin `kinRaces`, `spent.moraleOnEnd`, half a fear pair, a 0% kin bonus) and Stand Fast
dropped a knock-back resistance its shrug-off made unreachable (lenses 5, 6); a fall keeps its damage under
reduction as the doc said (lens 2); nine doc and comment corrections (lens 2: "cannot rout" is "cannot panic",
fear resistance is campaign-only, the dismount path, `MissileSpeedMultiplier`, who keeps one tree per agent);
the issue cited, the template's Changelog and GitHub Issue sections, and the console row (lens 4); and the lens 6
design proposals applied: one tree node instead of a decorator, task and blackboard, one percentage helper,
`WieldedWeapon`, the resolver's redundant id check, the mission logic's single dependency.

**Not applied:** replacing the behaviour tree with a plain decision loop (lens 6 proposal 1) reverses the design
Mike approved, a tree per race; it stays his call. Dropping the task's second slot check (lens 3 E8) was offered
only if a profile shows the tick matters. Skipping the closure for a horse's death (lens 3 E9) would bypass
`CreditsKill`'s new soldier rule; the rule stays in the service, where it is tested. **Waiting on Mike:** the two
known limits above (R15, R16), per-race fear resistance (lens 6 proposal 2), one copy of the profiles instead of
JSON plus `RaceAbilityDefaults` (proposal 3), and reading "took damage" from the engine's hit stamps (proposal 7).
He settled R17 on 2026-10-04: Khand's ability stays as it is.

### Convergence

One `deep-reviewer` checked the applied fixes against `a498e357` for standards and parity: parity confirmed on all
nine behaviour-preserving fixes (the scan plan, the percentage helper, the merged tree node, the cached settings,
the report clock, the cleared buffers, the cached reporter and guarded deaths, the slimmer mission logic, the
schedule copy), and the six intended changes do what they say and nothing more. It found five LOW defects, all
fixed: a comment still naming the deleted decorator, a missed command rename in a test comment, a resolver test
header and stub describing the removed id check, a comment broken mid-sentence in `TaomAgentStatCalculateModel`
(back to 147 lines), and the moved "took damage" gate's owed NaN test (now `RaceAbilityService.TookDamage`, with
NaN rows). Full suite after the fixes: 13,925 passed, 1 known failure (`EveryLanguage_DeclaresARowForEveryEnglishKey`:
the 20 new strings await #731, plus 9 older tournament keys), 5 skipped.

## Root-cause pattern 2: the wave that never ran

R20, R21, R23 and R24 were all found by the efficiency lens, which round 1 never launched: round 1 ran wave one
(standards, engine, data flow, XML) and stopped. The skill's table runs completeness and design always and
efficiency whenever C# is in scope. Lesson: a review round is every lens in scope; stopping after the first wave
is a partial review, and its RCA says so.

## Root-cause pattern 3: rules keyed on "an agent" without its kind

R14 (a horse's death) and lens 5's L1 (a riderless creature invisible to the triggers) share a shape: a rule
written for soldiers met every kind of agent the engine passes. A removal callback, a proximity query and a hook
all carry soldiers, mounts, riderless creatures and the player. Lesson: for every engine callback or query a
rule reads, write down which agent kinds arrive and what each field holds for each kind.
