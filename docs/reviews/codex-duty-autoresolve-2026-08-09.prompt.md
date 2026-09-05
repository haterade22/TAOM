# Adversarial review: TAOM enlistment — field duties converted from travel to an auto-resolving roll

You are an independent adversarial reviewer on a Mount & Blade II: Bannerlord **v1.4.7** total-conversion mod. Find real, demonstrable defects. Do not restate the design, do not praise, do not report style preferences. **A finding is only worth writing if you can name the file, the line, the concrete failure, and the player-visible consequence.**

## Ground rules

- Installed DLLs are authoritative for engine signatures: `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/*.dll`. A decompile cache is at `C:\Users\mikew\.taom-src\v1.4.7\*.cs`.
- The code **builds with 0 errors** and the **full suite is 6273 passing / 0 failing**, `validate_moduledata.py` PASS. So do not hunt for missing types. Hunt for the layer underneath: **code that compiles, passes, and is still wrong.**
- The change is on `origin/bannerlord-1.4.5`, commits `7c93e0fd` and `084a3b8c`. Use `git show 084a3b8c` and `git show 7c93e0fd`.

## What changed and why

TAOM's "Enlistment" lets the player serve as a soldier in an NPC lord's party. Their own party is parked on the commander: `IsActive=false, IsVisible=false`.

**Field duties used to DETACH the player.** `FieldDutyRuntime.Start` called `RestorePresence()` — making a one-man, troop-less party visible and active on the campaign map — then spawned a looter party to hunt or picked a settlement to travel to, and waited DAYS.

A live session on 2026-08-08 recorded the consequence: duty started 22:02:38, player captured 22:03:19. The duty then outlived the captivity (state went DetachedOnDuty → Captive → Attached while the record kept its duty) and would have charged trust for a failure the player could not avoid. That is issue #428.

**Now:** accept a duty, it occupies N hours, one `ISkillCheckService` roll decides success. The player never detaches. `DutyMechanic` / `DutyTargetKind` / `DutyTargetAi`, the spawn/destroy path and the settlement-arrival path are all deleted.

## Specific hypotheses to attack

Confirm or refute each **from the code**, and say which.

1. **The negative property is the whole design. Try to break it.** Find ANY path — `FieldDutyRuntime`, `DutyOrchestrationService`, `EnlistmentDutyBehavior`, the reconciler, the load normalizer — that can still make the player's party visible or active *because of a duty*. A source-scan test (`FieldDutyRuntime_HasNoPresenceOrSpawnDependencies`) bans the obvious tokens in that one file; that is not a proof about the system.

2. **Save-compat is the highest-risk area.** `EnlistedDetachedOnDuty` was RETIRED, not deleted: the enum member and value 4 survive, and `EnlistmentRecord.ToPersistedState` coerces it to `EnlistedAttached`. Verify that coercion genuinely runs on the **parse** path and not only on serialize. Then attack it: what happens to a save that is mid-duty at the moment of upgrade — `ActiveDutyId` set, `ShiftEndDay` null, plus retired tokens `dutyParty=` / `dutyTown=` / `dutyDeadline=` / `dutyFood=`? Is the player ever left detached-and-visible with nothing to bring them home? Is the duty slot ever occupied forever, blocking all future offers? Read `EnlistmentLoadNormalizer`, `EnlistmentStore.Deserialize`, `ServiceContentRecord.TryParse`.

3. **The orphaned looter parties.** Old saves may contain parties spawned by the deleted `SpawnLooterParty` (StringId prefix `taom_enlist_duty`). Nothing destroys them now — `DestroyParty` was removed. I judged this acceptable (they are ordinary bandit parties the engine already manages). **Argue against that** if you can: is there any way an orphaned party's continued existence corrupts state, leaks, or confuses a system that still expects the duty link?

4. **The captivity guard.** `HourlyUpdate` explicitly cancels when `State == EnlistedPlayerCaptive`, because `EnlistmentRecord.IsEnlisted` INCLUDES that state so the discharge guard does not catch it. Verify that claim about `IsEnlisted`. Then find the other states with the same shape: is `CommanderUnavailable` (a 7-day grace during which `RestorePresence` IS called) handled correctly, or can a duty resolve while the player has no commander?

5. **The roll.** `Resolve` calls `ISkillCheckService.Passes(primary, secondary, trust, (int)Rank * RankBonusPerLevel, difficulty)` with skills from the row's `supportSkills`. Check: what happens when `supportSkills` is empty or has an unknown skill id (the provider is supposed to skip such rows — verify it actually does, and that the runtime is safe if one slips through)? Is the difficulty range (data ships 45–76) actually winnable at Recruit rank with low skill and low trust, and not trivially auto-passed at Sergeant? Compute it: the roll is `skill + max(0,trust)*2 + rankBonus + Next(0..50) >= difficulty`.

6. **Reward chokepoint.** Success and failure both go through `IServiceRewardService.Grant`. Verify a duty cannot pay twice, cannot pay on cancel, and that `DutySuccesses`/`DutyFailures` cannot drift from what was actually granted. Note `DutySuccesses` feeds a promotion gate.

7. **NaN / degenerate values.** `ShiftEndDay` is a persisted `double?`. Trace every path where a non-finite or absurd value could reach it, and confirm the gate `!(nowDays >= ShiftEndDay.Value)` fails safe rather than resolving instantly or never.

8. **Dead code and lies.** `IDutyWorldAdapter` lost most of its members. Is anything left referencing something that no longer exists conceptually? Are there comments or docs that now describe behaviour the code no longer has? (This repo shipped a stack overflow because a comment asserted a safety property the code lacked — treat stale comments as findings.)

9. **Did any test get weaker?** The old `FieldDutyRuntimeTests` had 38 tests; the new file has ~16, and 4 orchestration tests were deleted. Read `git show 084a3b8c -- TAOM.Tests` and identify anything that was genuinely GUARDING something still reachable, which is now unguarded.

## Output

Group by severity: **P1** (wrong behaviour a player will hit), **P2** (wrong under a reachable edge case), **P3** (latent / maintainability). For each:

```
[P?] <file>:<line> — <one-line defect>
Evidence: <what you read that proves it — quote code or the decompiled signature>
Consequence: <what the player experiences>
Fix: <the minimal change>
```

End with **VERDICT: SHIP / FIX FIRST**, then separately list which of the nine hypotheses you checked and found NOT to be problems, so I know what is cleared rather than merely unexamined. If you think a premise in this prompt is wrong, say so directly.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
