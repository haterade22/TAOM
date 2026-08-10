# Morning handoff — 2026-08-09

> **SUPERSEDED, same day.** This was written mid-session, before the live playtest and before six
> more commits landed. Read it as a record of that moment, not as current state. What changed after
> it was written:
>
> - The **live session ran** — battle joins, two duties, a promotion, a camp incident, merit banding
>   and a commander-loss grace, with one `[ERROR]` in 3,452 lines (a mislabelled diagnostic, since
>   downgraded). So "nothing has run in a live game" is no longer true, and #375 / #424 / #428 all
>   closed on that evidence.
> - **Duty toasts were rebuilt again** (#436): the assignment toast is now conditional on real-time
>   shift length and the result toast is self-contained. The instruction below to "watch for an
>   assignment toast" is therefore wrong at time acceleration — by design.
> - **Enlistment diagnostics ship OFF.** The `[EnlistDiag]` trace this doc tells you to grep is
>   silent unless you turn it on in MCM.
> - **Commander loss became a moment** — a prioritized modal, the contract term waived, no wage or
>   promotion during the grace.
> - **PRs #440 and #442 merged**, and #443 was filed against #442's premise.
> - The `IDutyWorldAdapter` rename listed as owed below is still owed; the `ExitSettlementForDuty`
>   item was done (`25a6eba3`).
>
> Current state lives in [`docs/features/enlistment.md`](../features/enlistment.md) and the
> 2026-08-09 section of `CHANGELOG.md`.

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

## Part 1 — the duty rework

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

Reviews: Codex returned **FIX FIRST** (1 P1, 2 P2, 2 P3), the five-agent deep review 5 MED. All ten
fixed. **None was caught before commit, because I committed the C# without running `/deep-review`,
which CLAUDE.md marks Mandatory.** The gate works; I ran it late.

The uncomfortable one, in full in the RCA: hours after codifying *"a comment asserting a property the
code lacks"* as the defect class behind the #375 crash, I wrote a comment guaranteeing
`RankBonusPerLevel` was shared and could not drift — while a second definition sat in the file next
door. Both reviewers found it with one `grep`, immediately. I never ran that grep because I had just
written the alias and had no reason to doubt myself. That asymmetry is the entire value of an
independent pass.

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

## Part 2 — one bug turned out to be six

Finding 9 of the duty RCA was local: 26 duty-result toasts shipped unregistered because the key is
**composed at runtime** from the duty id, so no `{=key}` grep can see it — and
`GetLocalizedText` short-circuits on English, so it renders correctly for you and is broken only for
the other eleven languages.

I swept all four composed-key sites in the codebase. One more was live: **all 96 character-creation
narrative strings for `goblin` and `mistymountainorcs`** — two entire cultures, while the other
sixteen were complete (#432). English registered; the 12-language pass is owed and costs about
$2.16 by the tool's own estimate, so I did not run it on an unrotated key.

Then the pattern underneath became visible. **Six separate systems have shipped a per-culture gap,
and they are not the same cultures each time:**

| System | Missing | Issue | Guard |
|---|---|---|---|
| eligible careers | abanissa, shaghana, goblin, mistymountainorcs | review #24 | pre-existing test |
| narrative options | abanissa, shaghana | #111 (open since May) | added |
| narrative strings | goblin, mistymountainorcs | #432 | added |
| education templates | fixed earlier | #354 | validator rule |
| enlistment rosters | abanissa, shaghana | #431 | added |
| player CC equipment | goblin, mistymountainorcs + the 6 vanilla | #433 | none yet |

One cause: **every one of those coverage tables is hand-maintained, and each was completed at a
different moment.** Nobody forgot — the lists had no way to know. The tell is that
`generate_char_creation_equipment.py` *does* include shaghana and abanissa, the two cultures every
other table misses. It is not about which cultures are obscure.

So the three guards I added enumerate from the culture data itself (`cultures.json`,
`is_main_culture="true"`) rather than from a list someone must remember to extend. Each carries a
documented-exception list for the known gaps, plus a second test that **fails when an exception is
resolved** — including partial authoring, which is harder to notice than none at all. Every one was
verified RED before GREEN.

**No design decisions were made.** #111 (author content vs drop the cultures from `cultures.json`),
#431 (author rosters vs make the fallback genuinely neutral) and #433 (what the vanilla six should
wear) are all yours. What is added is the invariant, so culture 21 cannot arrive the same way.

## Issues

- **#428** duty rework — fixed, OPEN for the in-game gate above
- **#375** — updated: the crash surface is deleted, not guarded
- **#111** — re-verified still true at HEAD by counting; guard added; still needs your A/B/C call
- **#431** *(new)* — abanissa/shaghana lords issue Rohan militia gear; guard added
- **#432** *(new)* — the 96 CC strings; English landed, translation owed
- **#433** *(new)* — eight selectable cultures get no player starting equipment
- **#434** *(new)* — **317 of 567 TAOM localization keys are unregistered**, 258 of them
  CulturalFeats. All literals, so a grep would have found them at any point in two years; nobody ran
  it because English is correct and nothing complains. Ratcheted, not fixed
- **#427** merged (`7ac91bb1`) + root cause (`7c93e0fd`) · **#429** CHANGES_REQUESTED
- **#424** unblocked by #406 closing; still needs the F1–F8 look

## Reviews

| Pass | Verdict |
|---|---|
| Codex, duty rework | **FIX FIRST** — 1 P1, 2 P2, 2 P3. All fixed |
| Deep review, duty rework (5 agents) | 5 MED. All fixed |
| Deep review, PRs #427/#429 (6 agents) | HIGH on each. #427's fixed; #429's returned to the author |
| Codex, the coverage guards | **ISSUES FOUND** — 1 P1, 1 P2, 3 P3. All fixed |

The last one is worth reading. Its P1 was #433, which I had filed hours earlier from a different
direction — genuine convergence. The other four I had missed, and **two of them were my own comments
claiming things the code did not do** — on work explicitly about that defect class, hours after I
wrote the lesson about it:

- a test excluded `childhood_menu.json` because its keys were "literal, not composed"; childhood
  routes through the same builder as every other menu, so the guard had a hole exactly where its
  comment promised it did not;
- another said its rank list "mirrors `EnlistmentRosterIds.RankToken`" while hand-copying that
  method's current output.

Both now derive from production instead of asserting a faithful copy. The lesson gained a mechanical
form as a result: **when a test's comment claims it mirrors production, derive it from production** —
then the claim needs no comment and no reader.

It also caught that the unregistered-key scanner missed keys **in the exact form it was written to
find** (it required a quote before `{=`, so it skipped every `$"{{=...}}"`), and that
`taom_res_desertion` interpolated its runtime values into the default text — unlocalizable no matter
how well registered, since the translator's row has no slot for the number.

Five claims it checked and confirmed, including that `ExitSettlementForDuty` genuinely has no
callers and that the eight cultures excluded from the enlistment guard are real bandit clans
(`is_bandit`/`is_outlaw`), which enlistment gates out through `IsLord`.

## State

Suite **6284 passing / 0 failing** · `validate_moduledata.py` PASS · `lint_docs.py` clean ·
217 enlistment localization keys, all 12 languages id-identical to English.

`SubModule.xml`'s `<Version>` is untouched, so no release tag is owed from this work.

## Owed

1. The in-game duty check above, and #424's F1–F8 look.
2. **Translation** — the 96 CC strings (#432, ~$2.16) and, once registered, the 317 from #434
   (~$7). Both call the Anthropic API, and the key on this machine is the one that needs rotating.
   Neither was run unattended.
3. **`IDutyWorldAdapter` wants renaming** — four members of daily upkeep with one consumer, and
   "Duty" no longer describes it. Deliberately not done: it touches `Main/IoC.cs`, a single-owner
   file another session was working in.
4. Design calls on #111, #431, #433.

## Note on the other session

They were working most of the night too — release tagging, `/release`, and `AutoResolveDiagnostics`.
I held `CHANGELOG.md`, `SubModule.cs` and `IoC.cs` until they committed, and checked
`git status --porcelain` on `CHANGELOG.md` before each of my own edits to it.

One incident worth knowing: my earliest CHANGELOG entries were swept into **their** commit
`3b0e0831` ("docs(battleload): …"). Content is intact and correct in HEAD; only the attribution is
wrong. I caused it by writing the file with one command and committing with the next — the gap
CLAUDE.md warns about. I did not rewrite their commit to fix it, because that would be far more
destructive than a misleading message. Every later CHANGELOG edit was staged in the same command
that made it.
