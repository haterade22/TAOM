# Enlistment remediation — morning handoff (2026-08-08)

## Read this first: two things need your hand

### 1. Ten commits are local, not pushed — deliberately

`git push` was rejected (the remote moved). I did **not** rebase, because the working tree holds
20+ uncommitted files belonging to the concurrent session — `Main/Features/TaomSettings.cs`, all of
`Main/Features/FieldCommission/`, and `Main/Features/BattleLoadDiagnostics/`. `git pull --rebase`
auto-stashes the *whole* tree, and an unpopped auto-stash is exactly how a session's work silently
reverted yesterday morning — the incident that produced the CLAUDE.md multi-session git rules.
Their day's work was not worth the risk of pushing mine a few hours earlier.

**Once their work is committed, this is a 30-second fix:**

```
git pull --rebase && git push origin bannerlord-1.4.5
git stash list          # MUST be empty afterwards
```

### 2. Everything is green

**Full suite: 6036 passing, 0 failing.** (A FieldCommission test was red mid-session — that was the
other session's in-flight work and they have since fixed it. I never touched their files except
for mechanical constructor updates my own signature changes forced.)

Build deployed at 22:26, strings deployed (66 keys).

---


## The reviews found four things that would have reached you

Five deep-review agents plus an adversarial Codex pass ran over batches 0–10. Twelve findings, all
fixed and committed. Four are worth knowing about because they were **terminal or invisible** —
nothing would have told you they had happened except the symptom:

| What | Would have looked like |
|---|---|
| **Discharge inside a settlement the commander is not in** | You're released, and you can never leave the town. Not a stuck menu — the engine refuses to move a party with `CurrentSettlement` set, won't auto-exit the main party, and the Leave option it offers no-ops without an encounter. Survives save/reload. |
| **Save mid-battle, reload** | The battle never resolves. Ever. The redirect swallowed the `encounter` menu, and that menu is the only thing that advances *your* map event — the engine deliberately skips it in its own tick. |
| **Duty assigned while inside the commander's town** | You go invisible for four to six days, fail the duty, then reappear. Nothing un-hides a detached party. |
| **Three failed menu opens, ever** | The wait menu stops re-asserting itself for the rest of the session — and the next campaign too. The back-off check sat above its own reset. |

Plus: a NaN in one config value would have frozen you in "commander unavailable" permanently (the
sixth time that bug class has appeared here); a commander handle cached across a load would have
dragged you to a dead campaign's position at frame rate; the pump's throttle scaled with frame
rate, so at 144 fps it ran ~5× faster than designed; and deferred duty popups paid out without
re-checking that you were still enlisted.

**None of these were caught by tests.** All four came from reading the code against the decompiled
engine. That is the same lesson as the original bug: the adapter/engine seam is where these live,
and unit tests mock exactly that seam.
## What to test in-game

The build is deployed (21:52) and the strings are in the game's ModuleData (66 keys). A **new
campaign** is cleanest, but an existing save is the more valuable test for anything marked ⚠.

### A. The thing that was actually broken — battle joins

1. Enlist under any lord. Wait for him to fight. **You should be pulled into the battle.**
2. Same, but while he is **in an army**. (The original report: army battles never included you.)
3. Same, but where he **rides into a fight already in progress** — this path has its own edge
   (`OnPartyAddedToMapEventEvent`); the log line is `commander '<id>' joined a running map event`.
4. A **siege assault**. Watch for the assault staying an assault and not turning into a field
   battle outside the walls.

Log greps: `joined commander battle on side` = success. `could not join commander battle` = the
new guard refusing (correct in some cases, but tell me when).

### B. Settlement following — new, most visible

5. Follow your commander until his column **enters a town**. You should be **inside** it, on the
   service menu, and leave with him. Previously you stood invisibly outside the gate for the stop.
6. Look for `[EnlistDiag] FOLLOW: entered '<id>'` and `EXIT: left the settlement`.

### C. The wait menu — should now change while you watch it

7. Sitting on the service menu, the text should **change** when the column enters a settlement
   ("The column rests inside <name>") and when a battle starts. Any change at all proves the fix;
   before, the sentence was identical from oath to discharge.
8. It should also show rank, section, days served, standing, and pay owed.

### D. Dialog — three things that have never once fired for a player

9. **"Speak with your commander"** on the wait menu. Then check the conversation offers
   **reassignment** and the **quartermaster** — both shipped long ago and were unreachable.
10. Reassignment: the commander should now name your current section first, and the list should
    show the roles you are *not* in. ⚠ **This is your cavalry report** — if you were already in the
    horse, cavalry was correctly hidden and nothing said so. Confirm it reads sensibly now.
11. **"Ask your sergeant for work"** — either gives you a duty or says there is none.

### E. Leaving — should never surprise you

12. Ask to be released **before** 21 days served: you should get a popup stating how many days are
    owed and that leaving is desertion, with **Stay and serve**. Choosing Stay must leave you
    enlisted.
13. Ask again **after** 21 days: released honourably, and you should land somewhere usable — not
    parked, not menu-less.
14. ⚠ After any discharge, **click a lord**. This was broken before (a stuck `PlayerEncounter`).

### F. The new MCM switch

15. Options → Enlistment → **Enable Enlistment**. Turn it OFF *while serving*. You should be
    released honourably within the hour, not left invisible. Turn it back on; the enlist option
    returns.

### G. Save/load ⚠

16. Save while enlisted and parked, reload, confirm service resumes.
17. Save mid-battle while enlisted, reload.

---

## What I could not verify, and why

Everything above. Unit tests mock `IEncounterAdapter`, which is precisely why the original
never-joins bug survived three review rounds — the adapter/engine seam is the one place unit tests
structurally cannot reach. 659 green tests do not prove any of A–G.

## Not done

- **Batch 11 (content beats)** — not started.
- **12-language translation.** All 66 keys are registered, but `taom_enlistment_strings.xml` is not
  in `translate_with_claude.py`'s file list and `LanguageDataXmlTests` pins 10 language files per
  directory. `/localize` would currently translate nothing here. English fallbacks work.
- **84 runtime-built duty keys** (`taom_enlist_duty_{id}_{title|body|opta|optb|success|failure}`)
  are generated from data-row ids, so a literal-key grep cannot find them and none are registered.
  Needs a small generator, or those 14 duties ship English-only.

## Two planning assumptions that were wrong

Both caught by reading the installed 1.4.7 DLLs rather than trusting the plan:

- **The status-board root cause was refuted.** The plan blamed `IsMenuTextChanged` comparing
  `Attributes`. It actually compares an `int` to an `int?` (`0 != null` — always true), so the menu
  already re-rendered every frame. The text looked frozen only because `RefreshWaitText` ran once
  at menu init with a token that never changed. The planned `MenuContext.GameMenu.GetText()`
  plumbing would have been real code fixing a bug this menu does not have.
- **Batch 10's planned file list was unimplementable** — it named two entry points already at or
  over the ADR-002 150-line ceiling.

## Defects of my own, found and fixed overnight

- `EnlistmentDialogBehavior` hit 218 lines after Batch 6 (ADR-002 ceiling is 150) — split out.
- `taom_enlist_release_desert` carried two different English strings.
- The status budget was first written **outside** the pump's NaN-guard braces — one brace pair from
  a sixth instance of the NaN-gate bug class this codebase has already shipped five times.
- I committed 4 of the other session's files in `a26aecd2`; entangled and not separable without
  breaking the build at HEAD. Nothing of theirs was lost.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/enlistment.md](../features/enlistment.md)

<!-- backlinks-end -->
