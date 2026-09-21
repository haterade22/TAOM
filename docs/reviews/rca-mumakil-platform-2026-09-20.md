# RCA: the Mumakil war tower's crew platform (#627 phase 2)

**Scope.** Eight archers on the mumakil's three-deck war tower: a new prefab in `LOTRLOME_Armory/Prefabs` with its
repo snapshot, `TaomMumakilPlatform`, `TaomMumakilStandingPoint`, `MumakilCrewSpawner`, `MumakilCrewAgentOrigin`,
`MumakilConfig`, `MumakilMissionBehavior`, `MumakilPlatformTests`, the troop weight rows for both beasts, and the
docs that follow. The feature is a CLONE of the elephant's howdah crew, which shipped four days earlier, per the
one-feature-per-creature convention (Mike, 2026-09-20). Mike also settled the crew at eight (five main deck, two
upper, one crow's nest).

Two review passes ran: a design and standards pass, and an engine-compatibility plus data-flow pass on the
`deep-reviewer` agent. The second reports that it ran on Opus 5 rather than Fable because the account hit its Fable
limit; its findings were re-verified against the installed v1.5.3 DLLs before being acted on. **No CRITICAL. One
HIGH, already closed by a fix applied before the lens reported. One defect the lenses did not find, caught by
following up a question the review raised.**

After the fixes: the C# suite is green, and the prefab passes four independent geometric gates (headroom,
containment, spacing, deadband margin) that did not exist when the work started.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `Mat3.ApplyScaleLocal` MULTIPLIES an existing basis, and `Agent.Frame` is a native `GetRotationFrame` whose scale content cannot be established from managed code. If that basis already carries `AgentScale`, the platform is framed at 9.0x and every archer is teleported into the sky on frame one, every battle, with no log line | engine | The reference was written at `BodyLength=100`, so every shipped hour of the howdah ran at scale 1.0, where both answers are identical. The mumakil is the first place the difference is visible | The basis is orthonormalised before the scale is applied, which makes the result identical under either answer. `frameScale=` in the status line so one battle log settles it permanently. Lesson: `adapters-taleworlds-api.md` |
| 2 | HIGH | `mumakil_crew_main_4` sat directly under the upper deck's floor: 1.79 m of world headroom against a 1.92 m human capsule, so the archer's head was inside a `barrier` body. `BodyFlags.Barrier` is in the engine's missile-exclusion mask but NOT in its agent mask, so the engine shoves that archer every frame while its seat puts it back: the velocity failure that stops every bow draw | geometry | Found by me, not by a lens. The howdah has ONE deck, so no rule about decks above existed to clone, and the frame set was chosen against the two rules that did exist (on the deck, clear of a wall). Every gate passed | Frame moved to (0.700, -0.500), beside the upper deck. `NoCrewFrame_StandsUnderTheDeckAbove` gates it against the capsule top read from `Native/monsters.xml`, with the footprint inflated by the capsule radius. Lesson: `adapters-taleworlds-api.md` |
| 3 | MED | The first fix for #2 moved the frame forward to y -0.386, which cleared the deck above and left it 1.05 m from its neighbour. At a 0.45 m deadband two archers drifting at each other close to 0.15 m, inside the 0.74 m spacing rule | geometry | Fixing one geometric rule moved the frame through another one's margin. The spacing test passed (1.05 > 0.74) because it measures the authored distance, not the distance after drift | Second move, to a position that clears both. `TheDeadband_CannotEatTheSpacingMargin` now links the two tuning numbers so neither can be changed into the other. Lesson: `testing-qa.md` |
| 4 | MED | `TaomMumakilPlatform.mumakilRider` was write-only, and its doc asserted a consumer ("kept only so the crew can inherit its banner and colours") that does not exist: the crew take those from the rider captured in the spawn closure | dead code / claim | Cloned from the reference, where the equivalent field does have a reader. The comment was cloned with it and became false in the new context | Field and its two null-outs deleted |
| 5 | MED | `MumakilConfig.PlatformSeatDeadbandMetres` derived 0.45 m as "the howdah's 0.15 scaled by 3.0x, because this platform rides a 3.0x mount". The measured 0.15 is a property of the ARCHER (how far a seated agent settles from its frame), and archers are 1.0x on every beast | claim | The number was scaled by the one factor that was visibly different, without asking what the number measures | Constant deleted; the seat now calls `HowdahSeatMotion.SeatDeadbandFor(AgentScale)`, whose comment states the real reason the band scales (the LEVER ARM: decks 8 m behind and 14 m above the origin move several times further per frame for the same yaw rate) |
| 6 | MED | The visible tower is an `AdditionalMesh` handed to `agentVisual.AddMultiMesh`, i.e. bound to the beast's SKELETON, while the crew platform is framed from `Agent.Position` and `Agent.Frame`, which is root space. The two reference frames diverge with the walk cycle, and at 3.0x with an 8 m lever arm the divergence is several times the elephant's | engine | Not a defect and not fixable by this changeset: the same mismatch is why the elephant carries a deferred bone-tracking path. It was missed as a DOCUMENTED RISK, which is the part that mattered | Written into `docs/features/mumakil.md` as the first thing the smoke must measure, with the reason no deadband can fix it |
| 7 | LOW | `RepositionToMount` gated the engine-sourced `scale` for NaN and left `Agent.Position` ungated one line later, heading into the same native `SetFrame`. Same exposure in the reference's `RepositionToFixedOffset` | NaN gate | The new input got a gate because it was new. The rule says gate every float-to-decision path in a method you touch, not only the lines you added; this is the 7th instance of that class | Gate added in both files |
| 8 | LOW | `RepositionToMount` is public and dereferenced `mumakilAgent` behind a bare null check, while the tick path that normally reaches it is gated by `IsCurrentOccupant`. The one external caller is safe today | agent handle | The guard was placed at the call site rather than in the method, so it protected the caller that existed rather than the method | `IsCurrentOccupant` moved into the method |
| 9 | LOW | A missing Armory prefab logged one ERROR per rider per battle rather than once; the dedup set wrapped only the `catch` | logging | The dedup was cloned along with the `catch` it guards, and the new early-return path was written outside it | Dedup key added to the early return |
| 10 | LOW | `MumakilConfig.CrewTag` has no runtime consumer: the spawner selects seats by SCRIPT type. Every test selected by TAG. A ninth entity carrying the script without the tag would get a live archer and pass every test in the file, spacing and headroom included | testing | The tag was the obvious selector for an XML test, and it agreed with the script set on the day it was written | `TheTaggedFrames_AndTheScriptedFrames_AreTheSameEntities` asserts the two sets are one set. Lesson: `testing-qa.md` |
| 11 | LOW | `HowdahCrewLookupBanTests` filtered its violations with `v.Contains("Howdah")`. The ban exists because Custom Battle registers troops as `BasicCharacterObject` while the campaign registers `CharacterObject`, which is a fact about the MODE, not about elephants. The mumakil clone was exempt from it by name | testing | The gate was named after the feature that motivated it | Filter widened to `Crew`, class and test renamed, comment states that the trap belongs to the registration and not to one beast. Lesson: `testing-qa.md` |
| 12 | LOW | The MCM toggle read "Log Howdah Diagnostics" and described only the war elephant, while now gating the mumakil's platform log too | docs / UI | Same shape as #11: a label scoped to the feature that motivated it | Label and hint widened to "Crew Platform Diagnostics". The PROPERTY NAME was deliberately left as `EnableHowdahDiagnostics`: MCM's json2 persists per property, so renaming it would hand every existing install a fresh default |
| 13 | LOW | Three "Phase 1 has no platform crew" claims survived into the shipped feature, one of them the comment beside the deliberately empty `HorseHarness` slot, where it reads as "not yet" rather than "a harness deletes the tower" | docs | The status lines were written when the statement was true and are not where the work happens | All three rewritten; the harness comment now states the `ManeCoverType` mechanism and points at the `_HARNESSLESS_BY_DESIGN` exemption |

## Root-cause pattern: a clone inherits the original's rules, not its reviews, and the rules are named after the original

Nine of the thirteen findings are one of two shapes, and both are specific to cloning a proven feature.

**The new instance has properties the original did not, and no rule exists for them.** The howdah has one deck,
scale 1.0, and a harness gate. The mumakil has three decks, scale 3.0, and no harness. Each difference produced a
finding: three decks produced #2 (headroom), scale 3.0 produced #1 (the relative-scale trap) and #5 (the deadband
derivation), and the absent harness produced #13's most dangerous line. The clone's own design doc anticipated
drift in the OPPOSITE direction, listing the risk that the clone would regress the original's rules, and mitigated
it well: every measured constant is referenced from the pure helpers rather than copied, and the seat's four engine
rules survived intact, which the review confirmed method by method. Nothing regressed. What the mitigation could
not cover is the rules that did not exist yet.

**A gate or a label named after the feature that motivated it exempts the clone silently.** #11 is the sharp case:
a ban written to catch exactly this bug was in the suite, passing, and structurally incapable of seeing the new
code because its filter said "Howdah". #12 is the same error in user-facing text. #10 is its cousin: a test
selector that agrees with the runtime selector today and has no reason to keep agreeing.

**Preventive action, both shapes:**

1. **When cloning a feature onto a bigger or different instance, write the difference list FIRST**, and ask of each
   entry what rule the original never needed. One line per difference in the feature doc before the first edit.
   Here the list was three long and two entries carried defects.
2. **A gate names the thing it protects, never the feature that motivated it.** `Contains("Howdah")` should have
   been `Contains("Crew")` the day it was written, because the sentence explaining it already said "Custom Battle
   registers NPCCharacter as BasicCharacterObject". If the rationale does not mention the creature, the filter must
   not either.
3. **A test's selector must be the runtime's selector, or the difference must be asserted.** Selecting XML by tag
   while the game selects by script type is fine only with an assertion that the two sets are one set.

## Why each lens missed #2 and #3

Both geometric defects were found by following up a question the engine lens raised about capsule clearance, not by
a lens directly.

- **Engine compatibility** verified the beast's capsule against the deck undersides (E8, 0.59 m of clearance, and
  correct) and explicitly noted it had not measured horizontal extents. It looked DOWN from the deck to the beast,
  which is where the elephant's known failure lives, and not UP from a frame to the deck above.
- **Data flow** traced the prefab's frames to the doc's deck table to the test constants and found all three agree.
  They did agree. Agreement between three copies of the same numbers says nothing about whether the numbers put a
  body inside a solid.
- **XML / ModuleData** checked flags, parse, and the live-copy match. Geometry is not in its rule set.
- **The tests themselves** measured every rule that had been articulated. The gap was in the rule set, not the
  coverage.

The generalisable point: for a prefab that positions AGENTS, at least one gate must model the agent's BODY rather
than its origin. The spacing rule already did this horizontally (two capsule radii); nothing did it vertically
until now. The capsule is a three-dimensional object and every axis it occupies needs a rule.

## Not applied

| Finding | Why not |
|---|---|
| A one-frame window where a recycled agent index is read before the platform's next tick | Re-read the code and it does not exist. `RepositionToMount` is called from `OnTick` AFTER the `IsActive` and `IsCurrentOccupant` gates in the same tick, so there is no frame in which TAOM code reads a stale handle. The lens described a window between the engine's delete and the next tick, but no TAOM code runs in it. Finding #8 closes the only genuinely ungated path |
| `Main/_Module/SubModule.xml` declares no dependency on `LOTRLOME_Armory` | Pre-existing and repo-wide: every Armory item, the howdah prefab and this one all come from an undeclared module. `SubModule.xml` is single-owner, and the change wants its own issue rather than riding a feature commit |
| `tools/check_prefab_budget.py` counts only `TAOM_Map/Prefabs` while the engine's entity queue is global | Pre-existing, already a documented trap in CLAUDE.md. This prefab adds 12 entities, which is not what will exhaust a 131,072 queue |
| Deadband shipped at the scaled value rather than 0.15 | The lens could not establish whether the engine's bow-draw interruption keys on averaged or instantaneous velocity, and neither could I. The scaled value has the better argument (the lever arm) and now has a gate proving it cannot eat the spacing margin. The smoke measures `drift`, which distinguishes them |

## Owed

The in-game smoke, which is the only thing that can settle the reference-frame mismatch (#6), the downward-shooting
question, and the mission-end hang under real conditions. The ordered checklist is in
[`docs/features/mumakil.md`](../features/mumakil.md) "Owed in-game checks", and the first three items are cheap and
each invalidates the rest if it fails.
