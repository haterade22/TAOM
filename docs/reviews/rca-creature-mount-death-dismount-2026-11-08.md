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
| 1 | MED | `AccessTools.Method(typeof(Agent), "SetMountAgent")` was unpinned by `ReflectionSiteBindingTests` and the snapshot, and failed silent. The change widened what rides on it from 1 creature to every non-vanilla mount | reflection-site | The site predates the change; widening a guard was treated as a predicate edit, not as raising the stakes of the binding it calls | Pinned (DataRow + `reflection-sites.md` row), overload typed, one red line when missing. Lesson below |
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
- Patch48 widening: decided and done on 2026-11-08, see "Decisions after the port review".
- `HandleWargTargetHit`'s `catch (Exception)` swallowed the access violation and kept a broken battle running
  (24 follow-on parallel-tick faults, then a crash at mission end): see "Decisions after the port review".

## v1.5.6 port (2026-11-08, issue #1101)

The fix came from `57ba233b6` on `bannerlord-1.4.5`. Patch47's file was the same blob on both lines.

**Differences on this line:** the `/new-creature-mount` skill does not exist here, so its step 6 edit went
to the Patch47 row of `docs/ai-includes/creature-mount-authoring.md`. `.ai/review-reference.md` does not name
Patch47 here, so finding 6 needs no edit there. `ElkMissionBehavior`, `AnimaliaMissionBehavior`,
`docs/features/elk.md` and the `HandleBlowAux` diagnostic comment repeated the old scope claim and are corrected.

**Re-verified on the installed v1.5.6 engine** (native sha256[:16] fa3eb85fcabc2e9e, Ghidra 12.2):

- `Agent.SetMountAgent(Agent)` is private and passes the mount index or -1 to native (`Agent.cs:5329`).
  `MountAgent` and `RiderAgent` return the cached fields. `Agent.Die` does managed bookkeeping, then calls
  `MBAPI.IMBAgent.Die` (`Agent.cs:4678-4694`).
- Native `set_mount_agent` (0x6E3E60) rejects a null first argument or an id below -1. With -1 it clears the rider link on the mount
  (`+0x558`, in 0x600B00) and the mount link on the rider (`+0x55c`, in 0x600E20).
- Native die (0x5ED980) tests those two fields for its mounted branches. Of its seven direct callees that receive
  the dying agent, 0x5ED2C0 and 0x5F02B0 read the fields, 0x600B00 writes `+0x558` only inside the dying-mount
  branch, and four do not use them.
- Vanilla Monsters are defined only in `Native` and `SandBox`; `NavalDLC` XML defines none. The rideable ids
  are `horse`, `horse_2` (inherits from `horse`), `camel` and `mule`. `camel_unmountable` and
  `mule_unmountable` have their own `<Flags>` without `Mountable`, which resets the inherited flags
  (`Monster.cs:569-578`). Patch47 acts only when a rider exists. The live Armory holds 16 mountable Monsters on
  2026-11-08, none with a vanilla id.
- Callback slots: 0x56BC00 fills the table through 0x634BF0, which stores delegate i at
  `DAT_180dabd78 + 0x28 + 8*i`. So `+0x68`, `+0xd8`, `+0xe0` and `+0xe8` are delegates 8 (`OnDismount`), 22
  (`UpdateAgentStats`), 23 (`UpdateMountAgentCache`) and 24 (`UpdateRiderAgentCache`). One step is unread:
  `GetDelegates()` must pass the array in index order.

**Still not proven:**

- Hypothesis: both mounted branches of native die set a pointer to null when the mapped action index is -1,
  then read through it. The dying-mount branch (`+0x558`) reads `+0x188` from it unconditionally. The value
  from 0x590C90 is also used as a table index with no check; `docs/features/spider.md` records a 1.4.6 fault of
  that shape (`RAX+RCX*4`). Clearing the link skips both branches. No crash offset connects this to the report.
- That any creature except the spider reaches a fault on v1.5.6.
- Which thread reaches `Agent.Die` on the melee blow path. The warg bite path runs on the main thread: `AdvancedCombatBehavior.OnMissionTick` marks it with `MissionThreadGuard.MarkMainThread()`, then calls `TickBoneChecks`.
- The in-game checks listed above, now owed on v1.5.6 (#1101), and the behaviour differences below.

**What the sever changes, each unverified in game:**

- A rider who survives a dying creature no longer gets native's fall action, timers or re-check of the wielded
  usage (0x5ED980, the `+0x558` block; 0x604040 has no other caller on the sever path). A couched lance may stay
  wielded on foot.
- A creature whose rider dies keeps the cleared link and the AI flag, but no zeroed input or move-away goal
  (the `+0x55c` block).
- A rider killed on a tall mount (mumakil, war elephant) takes the mount's position at 0x5EAA80, with blend 0.
- On a fatal hit, read mount state from the `DIE` diagnostic line. `HandleBlowAux` runs after `Die`, so its
  `blow` line shows no mount.

## v1.5.6 port review findings

Tier 1 review of the port: Lens 5 Data flow and Lens 2 Engine compatibility. No CRITICAL or HIGH. Codex did not run
(the brief: no paid run without the maintainer's word), and none was authorized before the maintainer asked for the merge (2026-11-08).

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | MED | Branch 2 skips native's rider-landing block for every creature mount, and nothing named that cost or owed a check for the surviving rider | unstated-cost | The 1.4.8 review proved the sever skips the mounted branches, and treated skipping them as only a benefit. The owed list named the dying agent, not the rider who lives | Patch comment, this RCA, the registry row and #1101 name it. Lesson below |
| 2 | LOW | Native's riderless-creature goal is skipped when a rider dies | unstated-cost | Same as 1 | Listed above and owed in #1101 |
| 3 | LOW | This section's first draft had three wrong native statements: the id gate, the callee count, and the callback table reported as not found | evidence | Written from a first read of the gate and a hand count of callees, and the search for the table stopped at one failed xref | Corrected against 0x708550, 0x56BC00 and 0x634BF0 |
| 4 | LOW | "From 1 creature to 10" and "ten creatures" were false on this line (16 ids) | stale-count | A count written for 1.4.8 copied across the port | Number removed, as CLAUDE.md already asks |
| 5 | LOW | `CreatureMountsTests` lacked six live ids although the authoring doc asks for a row per creature | test-coverage | Rows were copied from 1.4.5, where those ids were not checked | Rows added |
| 6 | LOW | Mumakil, elephant and authoring docs justified a mounted-death pass by a native path Patch47 now bypasses | stale-rationale | `mumakil.md` does not contain the word Patch47, so the category grep cannot find it | One sentence in each. Lesson below |
| 7 | LOW | The `blow` diagnostic line of a fatal hit shows no mount after the sever | diagnostic | `HandleBlowAux` runs after `Die`, and the diagnostic was written before the guard covered creatures | `blow-diagnostics.md` says to read the `DIE` line |

**Root-cause pattern:** findings 1, 2 and 6 share one cause. A route-around was judged by the fault it avoids, and
not by the other work the skipped branch did.

**Why each agent missed these:** on 1.4.8, Lens 2 proved the branches were skipped and Lens 5 traced readers of the
link; neither was asked what the branch did for a surviving agent. On this port both lenses found them. Lenses 1, 3, 4
and 6 were SKIPPED BY TIER.

## Decisions after the port review (2026-11-08)

**Patch48 is widened to every creature mount** (the maintainer's decision). It uses the same predicate as Patch47. On
v1.5.6, native reads `CanDismount` (bit 11 of the blow flags) at 0x602170, and the dismount path it enables at 0x5EC2F0
uses the same unguarded action lookup as the mounted-death branches. Only the spider's hit crash is proven. The cost is
that a hit can no longer knock a ram, warg, elk or chariot rider off; dismounting and remounting by choice are unchanged.

**The `catch (Exception)` in `HandleWargTargetHit` stays.** The bite callback runs from the mission tick. `Mission.OnTick`
sets `tickCompleted = false` (`Mission.cs:3750`) and the next frame waits on it (`:3597`), so a rethrow would risk a hang,
as `lessons/harmony-il.md` records for the co-op safety nets. The catch now names the victim's Monster and the mount read before the blow, the fact
this report lacked. Trade-off: one helper of about ten lines, paid only on the failure path.

**Patch47's second branch stays as it is** until the in-game check owed in #1101 shows a surviving rider misbehaving.

## Patch48 and warg log review findings (2026-11-08)

Tier 1 review of the Patch48 widening and the warg log: Lens 5 Data flow and Lens 2 Engine compatibility. No CRITICAL or
HIGH, and neither lens found an engine incompatibility. Lenses 1, 3, 4 and 6 were SKIPPED BY TIER. Codex did not run.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | MED | `DescribeVictim` read the mount in the catch, after Patch47's sever cleared `_cachedMountAgent`, so a creature rider who faulted in native die logged `mount=none` | diagnostic | The new tests stubbed a mount that never changes, so nothing modelled the sever | The mount id is read before the blow; `HandleWargTargetHit_MountSeveredDuringBlow_LogStillNamesMount` clears the mount during the call |
| 2 | LOW | Patch48's comment said native attempts the dismount whenever the blow carries `CanDismount`; the attempt also needs an Active humanoid rider and no shrug-off | imprecise-comment | Text carried over from the spider-only guard | Comment corrected |
| 3 | LOW | `war-ram.md`, `elephant.md`, `mumakil.md`, `signature-strikes.md`, `fords-of-war.md` and `race-abilities.md` still promised that a hit dismounts a creature rider | stale-rationale | They describe the effect in words and do not name Patch48, so the category grep did not find them | Clause added to each; the lesson below says to grep the effect's words |
| 4 | LOW | `CreatureMounts.cs` and its test named only Patch47 as a consumer | stale-comment | The widening edit listed the patch file and not the predicate's own comment | Both patches named |
| 5 | LOW | `warg-combat.md` gave a test count with no date | stale-count | Tests were added and the count named no command or date | Count updated and dated |

**Repeat offender:** finding 1 is the hazard that finding 7 of the port review recorded for the `blow` line: state read
after the blow sees the sever. The port review recorded it one change earlier, and I did not apply it to the new log line.

**Not proven:** only `TaleWorlds.MountAndBlade.Launcher.exe.config` in `bin/Win64_Shipping_Client` sets
`legacyCorruptedStateExceptionsPolicy`, so whether a managed `catch` sees an access violation depends on the host exe
(UNVERIFIED). Patch48 on a rider of any creature except the spider has not run in game.

- FOLLOW-UP no issue: `SpiderAttackService.cs:80-84`, `BehaviorTreesCore.cs:74-77` and `SignatureStrikesMissionLogic.cs:109-111` also log no victim when a hit throws; this predates the fix and no report needs it yet.
