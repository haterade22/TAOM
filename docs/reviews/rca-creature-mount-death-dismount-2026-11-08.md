# RCA: creature-mount dismount-before-death (Patch47 widened), 2026-11-08

**Trigger:** a v2.0.29.5 player crash on Bannerlord 1.4.8 (2026-11-07). A warg bite went
`WargAttackService.HandleWargTargetHit` -> `CustomAttacksUtils.TakeDamage` -> `Mission.RegisterBlow` ->
`Agent.HandleBlow` -> `Agent.Die` and faulted inside native `MBAPI.IMBAgent.Die`. The battle held 122 wargs
and the player's war rams. The victim was not logged and the bundle carries no native offset.

**Change:** Patch47 (`Agent_Die_SpiderDismount_Patch`) hard-dismounted before `Die` for spiders only. It now
keys on `CreatureMounts.IsCreatureMount`: every mount Monster except `horse`, `horse_2`, `camel` and `mule`.

**What the review proved (Lens 2, native disassembly of the installed 1.4.8 `TaleWorlds.Native.dll`):** native
`set_mount_agent(-1)` clears both link indices, and native `die`'s only two mounted branches are gated on exactly
those indices, so the sever skips the mounted branches for any mount. It did NOT show that a war ram, elk or warg
reaches a fault: the change is a route-around for every creature mount, not a fix for a proven ram or warg fault.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | MED | `AccessTools.Method(typeof(Agent), "SetMountAgent")` was unpinned by `ReflectionSiteBindingTests` and the snapshot, and failed silent. The change widened what rides on it from 1 creature to 10 | reflection-site | The site predates the change; widening a guard was treated as a predicate edit, not as raising the stakes of the binding it calls | Pinned (DataRow + `reflection-sites.md` row), overload typed, one red line when missing. Lesson below |
| 2 | MED | `/new-creature-mount` step 6 said "extend Patch47's monster predicate"; after the inversion that edit would EXCLUDE the new creature and switch its guard off | stale-instruction | Scope-claim sweep covered docs/features and the registry, not `.claude/skills` | Skill reworded (add a test DataRow, never add to the list). Lesson below |
| 3 | MED | Patch48's comment and spider.md said "spider-only to match Patch47", now false | stale-instruction | Same sweep gap: a sibling guard justified by the changed guard's scope | Reason rewritten as an open decision (widening it changes gameplay: rams are remountable) |
| 4 | LOW | Branch 2 asked the predicate about every dying agent; a race id ("human") returns true, contradicting its summary | contract | Predicate written for a mount id, then called on `__instance.Monster` without the mount precondition | Rider read first; the summary states the precondition |
| 5 | LOW | `CreatureMounts`, `WarRamMissionBehavior`, `elephant.md` stated the fault or coverage more strongly than the evidence | overclaim | Comments written from the hypothesis before the engine lens weighed it | Reworded to "victim not logged", "never run in a battle" |
| 6 | LOW | `harmony-il.md`, `blow-diagnostics.md`, `.ai/review-reference.md` still described Patch47 as spider-only | stale-instruction | Same sweep gap as 2 and 3 | Updated |

## Root-cause pattern

Findings 2, 3 and 6 share one cause: widening a guard changes every sentence anywhere that states its scope,
and the edit swept only the obvious docs. `git grep` for the category name (`Patch47`) across the whole tree,
`.claude/`, `.ai/`, sibling patch comments and lessons included, finds them all; it was run by the Data flow
lens, not by the author.

## Why each agent missed these

Both lenses that ran (5 Data flow, 2 Engine compatibility) caught them; nothing reached the review unseen.
Lenses 1, 3, 4 and 6 were SKIPPED BY TIER (tier 1 first wave, no CRITICAL or HIGH).

## Not proven (owed)

- In game, with Blow Diagnostics ON (Patch63 stamps victim and mount before Patch47 severs): a ridden warg
  dying, a warg rider dying, the player dying on a war ram, a mahout dying.
- On the desktop with Ghidra: the callback-slot names Lens 2 tagged [Likely], and the native offset of any
  repeat crash mapped onto `die` (0x600b00 to 0x6023b1 in DLL sha256[:16] 2a5e0e0b15513ebb).
- FOLLOW-UP no issue: Patch48 widening is a gameplay decision for the maintainer, asked in this session.
- FOLLOW-UP no issue: `HandleWargTargetHit`'s `catch (Exception)` swallowed the access violation and kept a
  broken battle running (24 follow-on parallel-tick faults, then a crash at mission end); a separate change.
