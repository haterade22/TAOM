# Morning handoff — 2026-08-09

Overnight session. Everything below is committed and pushed to `bannerlord-1.4.5`.
**Nothing has run in a live game.** Restart Bannerlord to pick any of it up.

## The one thing to do first

**Take a field duty and watch what happens.** Duties were rebuilt from the ground up because of
what your log showed, and the whole design rests on a property only you can confirm:

1. Accept a duty → you should see an **assignment toast**, stay with the column, and remain
   **invisible on the map** for the shift (4–8 in-game hours).
2. Wait it out → a per-duty result toast, and the status board clears.
3. Bonus, if you can arrange it: get captured mid-duty. The duty should cancel with **no trust
   penalty**, logging `duty '<id>' cancelled (captive)`.

Grep afterwards for `[Enlistment.Duties]` — start, resolve and cancel all log now.

## What changed and why

### Field duties no longer detach you (#428)

Your session recorded the cost to the second: duty started 22:02:38, captured 22:03:19. The old
model called `RestorePresence()`, which made your one-hero, troop-less party **fully targetable in
contested territory** — for days, on 12 of 13 duties. Then the duty outlived the captivity and
would have charged you trust for a failure you were physically prevented from avoiding.

Duties are camp work now: orders, a few hours, one skill check. You never leave the column.

Two things fell out of that for free. The **#375 stack-overflow surface is gone**, not guarded —
nothing in the mod destroys a party any more, so the re-entrancy that killed your game cannot
occur. And the roll cost no new content: `supportSkills` was already authored for all 13 rows and
had **no consumer at all**, so per-duty skills came from data that was already there.

### PR #427 merged, then root-caused

The Rohan-gloves fix was correct but **revertible**: the roster is a generated artifact, and
`generate_enlistment_rosters.py` would have re-emitted the Gondor glove because
`rohan_edoras_militia` variant 0 still carried it. Three of that troop's five variants already used
the Rohan glove, so it was inconsistency, not styling. Fixed at the source.

### PR #429 — changes requested, **not** merged

The `MapResumed` half is good. The quit-to-load half has a HIGH: `OnGameEnd` reaches
`MBSubModuleBase` only via `Game.Destroy()`, which fires from `OnStateStackEmpty()` — too late on an
in-campaign load, which is the exact path it claims to fix. Its own cited log also contradicts one
stated claim. I suggested splitting it. Sternab's call.

## Reviews

| Pass | Verdict |
|---|---|
| Codex, duty rework | **FIX FIRST** — 1 P1, 2 P2, 2 P3. All fixed |
| Deep review, duty rework (5 agents) | 5 MED. All fixed |
| Deep review, PRs #427/#429 (6 agents) | HIGH on each. #427's fixed; #429's returned to the author |

**Ten findings on the rework, none caught before commit — because I committed the C# without
running `/deep-review`, which CLAUDE.md marks Mandatory.** The gate works; I ran it late.

The uncomfortable one, recorded in full in the RCA: hours after codifying *"a comment asserting a
property the code lacks"* as the defect class behind the #375 crash, I wrote a comment guaranteeing
`RankBonusPerLevel` was shared and could not drift — while a second definition sat in the file next
door. Both reviewers found it with one `grep`, immediately. I never ran that grep because I had just
written the alias and had no reason to doubt myself. That asymmetry is the entire value of an
independent pass.

## Issues

- **#428** — fixed, left OPEN for the in-game gate above
- **#427** — merged (`7ac91bb1`) + root cause (`7c93e0fd`)
- **#429** — CHANGES_REQUESTED
- **#424** — unblocked by #406 closing; still needs the F1–F8 look
- **#355, #334** — closed on evidence during the sweep; 19 others updated

## State

Suite **6274 passing / 0 failing** · `validate_moduledata.py` PASS · `lint_docs.py` clean ·
217 localization keys, all 12 languages id-identical to English (the 26 duty result toasts are
per-duty, not generic — the toast is the only thing you see of a duty now).

`SubModule.xml`'s `<Version>` is untouched, so no release tag is owed from this work.

## Owed

1. The in-game duty check above, and #424's F1–F8 look.
2. **`IDutyWorldAdapter` wants renaming** — it is four members of daily upkeep with one consumer,
   and "Duty" no longer describes it. Deliberately not done overnight: it touches `IoC.cs`, a
   single-owner file, and I would not put a rename in the same change as a behavioural rework.
3. `ExitSettlementForDuty` has zero callers and a doc describing the deleted deadline model.
4. Two cultures (`abanissa`, `shaghana`) have clans but no `enlist_*` rosters, so they fall through
   to `enlist_default_*` — which is **Rohan militia gear**. Same complaint that opened #427, by a
   different mechanism. Found by the review; not filed as an issue yet, since you may want the
   default kit made culture-neutral instead.

## Note on the other session

They were working the whole night too — release tagging, `/release`, and `AutoResolveDiagnostics`.
I held `CHANGELOG.md`, `SubModule.cs` and `IoC.cs` until they committed.

One incident worth knowing: my CHANGELOG entries were swept into **their** commit `3b0e0831`
("docs(battleload): …"). Content is intact and correct in HEAD; only the attribution is wrong. I
caused it by writing the file with one command and committing with the next — the gap CLAUDE.md
warns about. I did not rewrite their commit to fix it, because that would be far more destructive
than a misleading message.
