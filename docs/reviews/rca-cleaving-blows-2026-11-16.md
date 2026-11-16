# RCA: Cleaving Blows review findings (deep review, 2026-11-16)

## Top-line

The `/deep-review` of the Cleaving Blows commit `2490ef5506` on `feat/cleaving-blows` ran two lenses, data flow and engine
compatibility. It found no HIGH or MEDIUM defect, and the findings below (A to H) are the LOW and doc items. Each was checked
against the code or the decompile before the fix. Codex was NOT run; it is owed before merge. The fixes are in the commits on top
of `2490ef5506`. The mod options and the English text are unchanged, so no language row changes.

## Findings

### A. The skill term read the victim's skill after his weapon was dropped

- **Root cause:** `CleavingBlowMomentum` runs after `RegisterBlow`. On a fatal hit the victim's `Die` path drops his weapons
  (`OnWeaponDrop` sets the equipment slot to Invalid, `Agent.cs:3458-3462` and `:5529-5532`), so `GetWieldedUsageItem(victim)` could
  return null and his skill read 0. The gap was then the attacker's whole skill, and every kill got the full +0.10 skill term.
- **Fix:** the skill term is 0 on a fatal hit; the kill bonus covers kills (`CleavingBlowService.CalculateMomentum`).
- **Validation:** `CleavingBlowServiceTests.AKill_NeverEarnsTheSkillBonus_...` and `ASkillLeadOnANonFatalHit_StillAddsTheSkillBonus`.
  The premise (the victim's skill reads 0 on a kill) is UNVERIFIED: in-game check 10 logs both skills with `[BlowDiag] cleave-fatal`.

### B. The context was built before the cheap gates

- **Root cause:** `CleavingBlowMomentum` and `CleavingBlowReaction` built the whole context (bone lookup, native passive-attack read,
  two skill reads, the victim's wield reads) before the service checked the attacker kind and the folded settings, so the default
  path (feature off, or an AI swing with the AI switch off) paid for every hit.
- **Fix:** the kind comes from the controller and the two folded settings are read first (`CombatMechanicsHooks.CleaveKindMayApply`);
  the context is built only for a kind that may apply. The comment was rewritten to match. The service still checks them.
- **Validation:** `CombatMechanicsHooksTests.CleaveKindMayApply_FollowsTheFoldedSettings` (a live `Agent` cannot be built, so the
  gate is pinned as the method the two seams call before the build).

### C. Max Targets 2 reached fewer men than vanilla

- **Root cause:** the slider ran 2 to 5, but vanilla's two-handed axe lands three hits (1, 0.5, 0.25), so a cap of 2 took hits
  vanilla would have landed.
- **Fix:** the minimum is 3 in the slider, the provider clamp, the JSON validation, the docs and the tests. Default stays 3, maximum 5.
- **Validation:** config and settings provider tests (2 now reverts to or clamps to 3); the service tests use a cap of 4 for the
  "stops at the cap" and "read live" cases.

### D. A claim read as fact

- **Root cause:** the chain counter's comment stated that native hands the returned momentum back unchanged, which is unverified.
- **Fix:** the comment is prefixed "Assumed, not verified (combat-mechanics.md UNVERIFIED 1):".

### E. The thread note was wrong

- **Root cause:** the docs and a test comment said no TAOM log observed the melee callback's thread. One run did
  (`FordsTowerHitPatch.cs:24-25`, run 20261101-112404: 76 melee blows through `OnRegisterBlow`, all on the main thread), and
  `scripted-melee-strikes.md:92` records yotthani's v1.5.3 decompile.
- **Fix:** the thread row and the test summary now say so, and that native promises nothing, so the counter locks anyway.

### F. The vanilla carry rule was incomplete in the doc

- **Fix:** `combat-mechanics.md` now says vanilla carries only outside a crush-through (0.3) and a couched or braced hit (0.5),
  for an axe or mace, on a non-thrust hit with the weapon's own bone.

### G. The vanilla reaction was described too coarsely

- **Fix:** the doc says the vanilla reaction stands: an axe sticks in a man it does not kill when the hit does under half his health,
  and slices through otherwise.

### H. A config field was missing from the config table

- **Fix:** `configs-balance.md` has the `includeMountedAttackers` row, with the note that the MCM hint's horseback line holds only
  while it is false. The line refs of the new rows were re-read after the edits.

## Not run

Codex (`/review-codex`) was not run and is owed before merge. Nothing here was checked in game.
