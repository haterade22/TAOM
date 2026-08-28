# RCA: Patch71_HeroResetEquipmentsGuard (#486), 2026-08-20

**Fix:** firing a FieldCommission-promoted companion threw `NullReferenceException` inside vanilla
`Hero.ResetEquipments`, leaving `RemoveCompanionAction.ApplyInternal` half-executed. Guarded by a
prefix that defers to the engine for vanilla-shaped templates and fills the slots itself otherwise.

**Why this RCA exists:** the fix itself was sound, but the review gate returned eleven confirmed
findings against it, ten of them in code written this session. They are not independent slips. Five
of the seven trace to one habit: **applying a house pattern without re-checking that the pattern's
precondition holds at this particular call site.** That is the durable lesson.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | MED | The prefix's `catch` returned `true`, deferring to vanilla on any internal fault. At this call site vanilla is a guaranteed NRE on a hero already de-clanned and de-partied, so the error path re-created the exact torn state the patch exists to prevent. | Harmony | Copied the "any exception falls through to vanilla" shape from `Patch8_SiegeCampGuard` and `Late_ActionSetOverride` without asking whether vanilla was a safe default HERE. In both donors it is. | New lesson in `lessons/harmony-il.md`: defer-to-vanilla-on-error is only safe when vanilla is a safe default. Registry entry now states the departure and why. |
| 2 | MED | `Fill` called `source.Clone()` before `FillFrom`. `FillFrom` only reads the source, so the clone was a throwaway 12-slot allocation per filled slot. | Efficiency | The engine's own `ResetEquipments` clones, so the clone was copied across without reading `FillFrom`'s body to see that the semantics differ (assign-a-clone vs copy-in-place). | Read the body of the engine method you are replacing, not just its shape. `FillFrom` is now documented in-file as read-only on its source. |
| 3 | LOW | `Report` built a `List<string>` and called `string.Join` on every invocation, then discarded the result behind a dedup gate inside the callee. | Efficiency | The gate and the work lived in different methods, so neither read showed both. | Dedup gate moved to the top of the single reporting method, before any string work. |
| 4 | LOW | The warning text said "reset from its battle kit instead" even when the missing slot WAS battle, in which case nothing was reset from anything. | Diagnostics | The message was written for the common case (missing civilian) and never re-read against the case where the fallback source itself is absent. | Message now branches on whether a battle kit existed, and says what actually happened. |
| 5 | MED | No binding drift-guard for `CharacterObject.FirstBattleEquipment` / `FirstCivilianEquipment` / `FirstStealthEquipment`, the three properties the prefix actually dereferences. | Testing | The binding tests were written from the patch's `Hero`-facing surface. The `CharacterObject` reads happen one hop out through `Hero.Template` and were not enumerated. | Enumerate drift-guards from the dereference list, not from the primary type. `[DataRow]` ×3 added. |
| 6 | MED | Nothing pinned `MBEquipmentRoster.EmptyEquipment`, even though the argument for `ResolveStealth` needing no try/catch rests entirely on `AllEquipments` substituting it for an empty roster. | Testing | The reasoning was verified once against the decompile and then treated as settled. An engine-behaviour assumption load-bearing for a guard's safety was left unpinned. | New lesson: when a guard's safety argument depends on engine behaviour rather than on your own code, pin that behaviour with a binding test whose failure message says what breaks. |
| 7 | LOW | `CanDeferToEngine` takes three booleans; only four of eight combinations were pinned. | Testing | Sampled the cases that felt meaningful instead of exhausting a small finite domain. | Replaced with an eight-row exhaustive truth table. |
| 8 | MED | `Patch71` cached `IModLogger` in a static with no `ResetForUnload()`, and was not in `SubModule.OnSubModuleUnloaded`'s reset sweep. After a reload-in-process the cached logger points into a disposed IoC container and every warning is dropped. | Lifecycle | Straight recurrence of the bug class Codex review #46 already found and fixed for FOUR sibling patches in that exact method. The sweep was never consulted when adding a fifth patch with the same caching pattern. | `ResetForUnload()` added and wired into the sweep. The pattern is now five-for-five, which is the argument for a gate rather than vigilance. |
| 9 | LOW | The dedup set was keyed on bare `hero.StringId`. `CampaignObjectManager` restarts its StringId sequence per campaign, so the same promotion in a second campaign in one process collides and its first warning is swallowed. | Diagnostics | "Unique id" was read as globally unique without checking the generator's scope. | Key is now `UniqueGameId/heroId`, and `ResetForUnload` clears the set. |
| 10 | MED | The hook itself had zero behavioural coverage against the 80% hook-coverage rule: only the pure helper and reflection drift-guards were tested. | Testing | Assumed the whole hook needed a live campaign. Only `Prefix` does; `Fill` takes four `Equipment` objects, and `Equipment` is constructible standalone. | `Fill` made `internal`, six behavioural tests added covering the singleton guard, the retype flag and the leave-alone branch. |
| 11 | LOW | `HeroCommissionAdapter` performs the identical getter-then-`FillFrom` mutation with no guard and no comment, relying implicitly on an invariant established one call frame earlier. | Docs | Patch71's author recognised the hazard and guarded it; the adapter was left asymmetric because the reasoning lived only in the patch. | Comment added at the adapter naming the invariant and warning against copying the pattern. |

Also caught, and worth recording separately because a repo gate found it rather than a reviewer:
`CoopVetoClassificationTests.EveryBoolPrefix_HasACoopDisposition` failed the build until `Patch71`
declared a co-op disposition. The gate did exactly its job on the first bool-returning prefix added
since it was written.

## Root-cause pattern: house patterns carry preconditions, and the preconditions are usually unwritten

Findings 1, 2 and 6 are the same mistake wearing different clothes. Each took something established
(the fall-through-to-vanilla catch, the clone-before-assign, "AllEquipments is never empty") and
reused it without re-deriving the condition that makes it true.

Finding 1 is the sharpest instance and the one worth generalising. TAOM has a genuine, correct
convention that a guard prefix should defer to vanilla when it cannot do its job. It holds because
in almost every case vanilla is a working default and TAOM's guard is an enhancement. `Hero
.ResetEquipments` is the case where that inverts: vanilla is a certain crash, the crash lands
mid-mutation, and TAOM's guard is the only thing standing between the player and a corrupt campaign.
The convention was applied on autopilot and produced an error path that reintroduced the bug.

The general form: **a fallback is only safe if you can state what the fallback DOES at this call
site.** "Falls through to vanilla" is a description of control flow, not of an outcome.

## Why each review agent missed these

- **Standards (Agent 1):** passed correctly. Nothing here is an ADR breach; findings 1 and 4 are
  semantic, and the file was inside the ADR-002 ceiling. Its rule set has no question of the form
  "is this error path's destination actually safe?" and arguably should not.
- **Compatibility (Agent 2):** verified 19 members, 0 incompatible, and independently proved the
  only-caller claim with a `strings` scan across 85 base DLLs and 23 module directories. It could
  not have caught 1 through 7: every member the patch touches genuinely exists and matches.
- **Efficiency (Agent 3):** caught 2 and 3, which are its remit, and correctly rated the
  unbounded `_reported` set as intentional by comparison with Patch65.
- **Completeness (Agent 4):** caught 5. Missed 6 and 7 because it checked that tests exist and cover
  the code's branches, not that they pin the code's unstated assumptions about the engine.
- **Data flow (Agent 5):** the highest-value agent, as usual. It found 8, 9, 10 and 11, none of
  which any other agent raised, by tracing static state across a process lifetime and comparing the
  two parallel equipment-fill paths against each other rather than reading either in isolation. It
  also independently confirmed the root cause, the category registration, the apply timing and the
  co-op classification. Worth noting what it did NOT flag: it verified that nothing in TAOM
  subscribes to `OnCompanionRemoved`, so the un-dispatched event was a correctness problem for
  vanilla listeners rather than for TAOM behaviour.
- **The design pass** (a Plan agent run before implementation, whose report arrived after the code
  was written) caught 1, 2, 4, 6 and 7. That ordering is itself a finding: the design review was
  dispatched during planning and its result was not waited for before implementing. Had it been,
  five of seven never reach the review gate.

## Preventive actions taken

0. `ResetForUnload()` on `Patch71`, wired into the unload sweep (finding 8), **and the sweep is now
   gated**: `ResetForUnloadSweepTests.EveryResetForUnload_IsCalledFromOnSubModuleUnloaded` scans
   `Main/` for every `public static void ResetForUnload()` and fails the build if
   `SubModule.OnSubModuleUnloaded` does not call it. Verified to actually fire by deleting the
   Patch71 call and watching it go red naming that class, then restoring. Five classes were being
   maintained by memory; they are now maintained by the build.
1. `lessons/harmony-il.md` gains the defer-to-vanilla-on-error lesson (finding 1).
2. `lessons/testing-qa.md` gains the pin-the-engine-assumption lesson (finding 6).
3. `docs/reference/harmony-patch-registry.md` now states Patch71's deliberate departure from the
   fall-through convention, so the next person to "fix" it back reads the reason first.
4. The `MBEquipmentRoster.EmptyEquipment` binding test carries a failure message naming exactly what
   breaks if the substitution goes away.

## Second pass

The fixes above were themselves reviewed (five agents: standards, an adversarial attack on the
changed error path, test validity, lifecycle/data flow, and documentation). That pass returned no
HIGH findings and confirmed the two riskiest changes, but it did find six more things, which is the
argument for reviewing fixes rather than only reviewing features.

| # | Sev | Finding | Outcome |
|---|-----|---------|---------|
| 12 | MED | `Fill_NullTarget_DoesNotThrow` passed `sharedDefault: null`, and `ReferenceEquals(null, null)` is true, so the guard short-circuited on that clause alone. The test would have stayed green with the `target == null` check it exists to pin deleted outright. | Fixed: the test now passes a non-null `sharedDefault`. A test that cannot fail is worse than no test, because it is counted as coverage. |
| 13 | MED | `CharacterObject.Culture` had no binding pin, and the obvious pin does not work: `CharacterObject` declares `public new CultureObject Culture`, shadowing the base, so an inherited-inclusive `AccessTools.Property` lookup is ambiguous and resolves to nothing. Found by writing the test and watching it fail. | Fixed with `AccessTools.DeclaredProperty` plus an assertion on the return type, which is the load-bearing part: only the derived `CultureObject` carries `DefaultStealthEquipmentRoster`. `IsHero`, `Campaign.Current` and `Campaign.UniqueGameId` were pinned in the same pass. |
| 14 | LOW-MED | The fault path logged at WARNING with `ex.Message` only, no stack trace, where the sibling `Patch69_TournamentEndGuard` logs its exception dump at ERROR. A backstop for a should-never-happen fault left nothing to root-cause from. | Fixed: ERROR plus the full `ex.ToString()`. |
| 15 | LOW | The co-op disposition string still justified `ReviewedSafe` partly on "the fallback fill is a plain Clone", which stopped being true when the clone was removed. The verdict held; the stated mechanism did not. | Fixed. A justification that describes code which no longer exists is how a future reviewer re-derives the wrong conclusion. |
| 16 | LOW | The in-file comment claimed `ApplyInternal` has "made the hero a fugitive" universally, but the prisoner sub-branch calls `EndCaptivityAction.ApplyByEscape` and lands on `Released`. Not reachable through the fire dialogue, so the risk argument is unaffected. | Comment narrowed to "on the ordinary non-prisoner path". |
| 17 | LOW | The two `.Clone(false)` calls left in `HeroCommissionAdapter` were proven non-load-bearing by the same decompile that justified dropping the patch's clone, leaving two parallel paths in one feature doing the same thing differently. | Dropped. I had declined this once as scope creep; two independent agents plus a proof of parity changed the call. |

The adversarial pass **confirmed** the catch-polarity change and strengthened the reasoning for it:
the old `return true` was not merely suboptimal, it was a residual defect. Because the exception is
consumed inside the prefix before the return value is chosen, deferring would have run vanilla's
identical dereference chain a second time with nothing changed in between, throwing again, this time
uncaught, aborting the governor teardown and the `OnCompanionRemoved` dispatch. It also established
that the "throw on the first line" counterexample I asked it to hunt for cannot be constructed:
`Hero.Template` is a bare field read.

## Process note

A design pass was dispatched in parallel with implementation rather than before it. Parallelising a
review against the thing it reviews wastes the review. Dispatch the design agent, then wait.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
