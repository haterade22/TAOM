# RCA: the prisoner recruit gate's co-op stand-aside, registry not updated (2026-11-19)

## Top-line

Commit 96ea28e4a applied the maintainer's #563 decision A: while a co-op session is live,
`PartyUpgradeResourceCheckHook.ClampRecruitCount` returns the requested count, so the Patch26 party-screen
prisoner gate lets every recruit through. The code, its tests, the feature doc, one behaviour comment and
the `CoopVeto` entry were updated. The Patch26 entry in `docs/reference/harmony-patch-registry.md`, the
crash and co-op triage entry point, and the comment above the patch's own `GateRecruit` still described a
clamp on every prisoner recruit. The tier 1 deep review (Lens 2, Lens 5; Codex not run) found the registry
gap; the lead found the comment twin. No runtime defect: one LOW finding, fixed in the review follow-up
commit. An RCA file is written because the cause repeats one already recorded for plan 019.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| D1 | LOW | `harmony-patch-registry.md:225` and `PartyScreenLogic_AddCommand_Patch.cs:71-73` described the prisoner clamp without its co-op stand-aside | Stale prose after a decision (repeat) | The change sat in the hook, not the patch file, so the edit was planned around the hook and the feature doc. Nobody grepped the patch's name or the old claim ("clamps `PartyCommandCode.RecruitTroop`") across `docs/reference/` and the patch file | When a decision changes what a patch does, grep the patch class name and its category (`Patch26`) in `docs/reference/harmony-patch-registry.md` and the patch file, even when the code change sits in a hook or service; lesson in `lessons/misc.md` |

## Root-cause pattern

This is a recurrence. Plan 019's decisions (2026-09-24, `rca-nullable-ratchet-decisions-2026-09-24.md`
#2) closed a decision in the feature doc while the registry still called it open; the lesson in
`lessons/misc.md` ("A scope word in a claim is checked by a grep over that scope", its "Recurred" note)
says to grep for the old wording before committing. Here the old wording was a behaviour claim, not a
status word, and the code that changed was one layer below the patch the registry is keyed on. A grep for
the hook's class name finds no registry line; a grep for the patch's name or category does, and so does a
repo-wide grep for the issue number or the changed member (`ClampRecruitCount`).

## Why each agent missed it

- **The executor:** updated every text that named the hook or `#563` in the feature doc, and the
  `CoopVeto` text that names the patch, but not the registry or the patch's own comment, which name the
  patch and describe the clamp without the hook's class name.
- **Engine compatibility lens:** checked the doc and `CoopVeto` text against the code and found them
  accurate; its scope is engine claims, and it did not open the registry.
- **Data flow lens:** found the registry gap (its "decision text to docs" flow) but not the comment
  above `GateRecruit`, which it cited for the control flow only.
- **Standards, completeness and design lenses:** skipped by tier. Completeness would own this check.
- **Codex:** not run.

## Feedback memories to codify

None. The durable rule is the new entry in `docs/reviews/lessons/misc.md`.
