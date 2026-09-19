# RCA: war ram head-butt binding (deep review, 2026-09-18)

## Top-line

`/deep-review` of the war ram head-butt binding (#618): C# `WarRamConfig.AttackActionName` from `act_horse_kick`
to `act_war_ram_butt`, three edits to the live `LOTRLOME_Armory` XML (`action_types.xml`, `action_sets.xml`,
`lotr_monster_war_ram.xml`), and two new tools (`tools/blender/transfer_clip_to_engine_rig.py`,
`tools/wire_anim_master_clip.ps1`). Seven agents were launched; four (API compatibility, data flow, the dedicated
XML review, tooling) died on a session rate limit and their checks were run by hand.

**The XML passed.** Diffs against the backups were exactly the intended insertions; line endings and BOM held;
the Armory XSLTs are identity copies that pass the new elements through; `id` is the first attribute of each new
set, which the engine's merge keys on (`CreateProcessedActionSetsXMLForNative` uses `FirstAttribute`); the
explicit `action_set` overrides the base monster's (`Monster.Deserialize`, TaleWorlds.Core 19348 then 19436); the
campaign-map mount visual calls `GetActionSet(monster.ActionSetCode + "_map")` and `MBGlobals.GetActionSet` throws
`Invalid action set code` on a miss, so `as_war_ram_map` is load-bearing and present. Every suffixed lookup through
`GenerateActionSetNameWithSuffix` resolves the ram by `BaseMonster` to `as_horse...` (TAOM's prefix on it keeps
vanilla's rule), so the Monster change does not reach those paths.

Findings: **3 MEDIUM, 4 LOW, 1 process**, all fixed or measured closed in the same session.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | Seven docs still showed the ram's Monster as `action_set="as_horse"` with "nothing authored" (two handbook chapters, `items-mounts-and-harness.md`, `recipe-add-a-race-or-creature.md`, `creature-mount-authoring.md`, `war-ram.md` sample and diagram, the ledger), plus `feature-map.md`, `INDEX.md`, the `WarRamConfig` class summary and the Armory `SubModule.xml` comment, after the user asked for every doc to be updated and was told it was done | Documentation sweep keyed on symbols, not on the changed value | The sweep searched by the NEW names (`act_war_ram_butt`, the tools) and by one old symbol (`act_horse_kick`) in files already open. The value that changed on disk, `action_set="as_horse"`, was never grepped. Two of the stale samples carry `<!-- example file=... -->` markers naming the live file, and `check_handbook_attributes.py` validates marker attribute NAMES against the engine dump, never VALUES against the file named, so it printed 0 findings | Lesson "A docs sweep for a changed value greps the old VALUE" (build-tooling-workflow). Follow-up (not done here): teach `check_handbook_attributes.py` to compare the values of a marker that names a live file |
| F2 | MED | `transfer_clip_to_engine_rig.py`: the export path for a skeleton whose list order is already depth-first never asserted the take name | Fixed the branch that failed, not the class | The `.001` take-name bug hit the reorder branch an hour earlier and was fixed there only | Assertion added to both branches. Covered by the lesson below on the `.001` class (animation-skeleton, fact 5 rule) |
| F3 | MED | `wire_anim_master_clip.ps1` docstring claimed the clip's RuntimeDataCache entry "stays valid" | Unmeasured claim written as fact | Written from the reasoning "GUIDs kept, so the .rdc name still matches"; nobody measured whether an .rdc caches clip content | Reworded, then measured the same afternoon: the Kit re-cooks a changed package's entry on its next load (the rewired clip's `.rdc` was rewritten at 14:11:55, 23 s after the Kit started). `evidence-over-claims.md` section C already covers the original slip |
| F4 | LOW | A second `-Apply` of `wire_anim_master_clip.ps1` overwrote `.bak-prewire` and `.bak-preskel`, losing the original | Backup not write-once | Copied `File.Copy(..., $true)` from a one-shot pattern | Backups are written once; delete on purpose to take a new one |
| F5 | LOW | Transfer tool: the engine rig was renamed back while the export rig still held the name (`_notused.001` in the WORK blend); a bad flag raised `SystemExit`, which the detached launcher's `except Exception` never logged; `order_parents` would crash with an opaque `IndexError` on a skeleton whose list does not start with a root | Rename order; exception class; missing precondition | The first two were visible only in a saved blend and a failing CLI run; the third needs a pathological skeleton | Restore after deleting the export rig; `RuntimeError`; explicit root-first check. Rerun reproduced the Armory export exactly (0.00 matrix difference) with a clean armature name |
| F6 | LOW | Armory `SubModule.xml` comment still described `action_set="as_horse"` | Same as F1 | Same as F1 | Fixed with a non-`.xml` backup |
| F7 | LOW (measured closed) | Found while applying the F3/F4 fix to the live clip: TpacTool's re-save of a Kit-written clip is not byte-identical. It writes the item version word as 5 (the Kit writes 6) and omits the Kit's 48-byte dependency tail (count 1 + the master's GUID + padding). The field dump sees neither | Re-serialisation invisible to a same-library read-back | The tool verified what it wrote by reading the fields back through TpacTool, the library that wrote them; a round trip through one parser cannot see what that parser does not model | Measured across every shipping creature clip: 24 chariot and 44 elephant clips are version 5, and 24 spider and 24 chariot clips have no dependency tail, and all play in game, so neither is load-bearing. Recorded in the tool's docstring. Lesson "Verify a re-serialised binary against the original bytes" (build-tooling-workflow) |
| F8 | Process | No GitHub issue before the work | Workflow | The work grew from a docs task into a feature inside one session | Filed #618 retroactively |

**Disputed, not a finding for this change:** the standards agent flagged `() => IoC.Resolve<IWarRamAttackService>()`
in `WarRamCombat`. The line predates this change and `ElephantCombat` and `MumakilCombat` carry the identical
deferred factory; it is out of this change's scope.

## Root-cause pattern

F1 and F7 are the same shape: **a check that reads the thing through the same lens that produced it.** The docs
sweep searched with the vocabulary of the new state, so it could not find text written in the old one; the tool
verified its binary with the library that wrote it, so it could not see bytes that library drops. The fix in both
cases is to look through the other lens: grep the old value, diff the original bytes.

## Why each agent missed these

- **Standards:** flagged only the pre-existing IoC factory; docs and tools are outside its checklist.
- **API compatibility:** died on the rate limit. Its engine checks (Monster override order, suffix derivation,
  `GetActionSet` throwing, the `_map` sites) were run by hand and all passed.
- **Efficiency:** correctly clean; nothing here runs on a hot path.
- **Completeness:** found `feature-map.md` and the `WarRamConfig` summary (F1, in part). Its greps were for
  `act_horse_kick` and "No animation data", the same symbol lens, so it missed the seven samples; the hand grep for
  `action_set="as_horse"` found them.
- **Data flow and XML:** died on the rate limit; completed by hand (the XML verdict above).
- **Tooling:** died on the rate limit; its checklist, run by hand, produced F2 to F5. F7 surfaced only when a fix
  was exercised against the live file, which no read-only review does.

## Feedback memories to codify

None beyond the two lessons: both rules are general and belong in `docs/reviews/lessons/build-tooling-workflow.md`.
