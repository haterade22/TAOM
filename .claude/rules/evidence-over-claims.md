# Evidence over claims

Technical truth over social comfort. Verify a finding before acting on it, prove a result before
claiming it, and produce every fact from something you read this turn. Demonstrate agreement,
success and knowledge; never perform them.

## A. Receiving findings

A review finding (Codex, a `/deep-review` lens, a subagent, the user) is a hypothesis, not a verdict.

1. **Verify it against the code before implementing it.** Re-read the file or signature it names.
   When two agents disagree about a TaleWorlds API, re-run `taom-src`; don't side with the more
   confident one.
2. **Push back with evidence** when the change would break behaviour, the reviewer lacked context
   the code contradicts, or it fails `simplicity-criterion.md`. One technical line, not a refusal.
3. **When you were wrong, say so briefly and fix it:** "Checked `GetCharacterWage`: it falls back to
   `DeadBattleEquipment`. Fixing."
4. **Spot-check a subagent's load-bearing claims before relaying them**, security claims above all.
   Relaying an unchecked report is the same failure as trusting an agent's "done".

## B. Verify before "done"

Before claiming built, passing, fixed or done: name the command that proves it, run it now, read the
exit code and output, then state the claim with that evidence, or report the failure with it. Name
what the run did not cover (no in-game check, one build) beside the claim.

Not verification: an earlier run, "should pass", a linter pass (it does not compile), a subagent's
self-report, your own confidence. Stop and verify if you are about to type "Done!" or commit on an
unrun build.

This gates claims, not edits. The cadence that keeps it cheap: a compile after each edit; the filtered
suite while iterating (`python tools/graphify_taom.py tests "<changed type or file>"` prints its
`dotnet test --filter` line); and at each work-item boundary, before the review gate and after any
generator ran, the full suite, C# and `tools/tests` (`/verify` Steps 2 and 2b). That is the run you quote.

Batch related `taom-src` lookups into one command. A verification you ran this turn and have not
invalidated is still evidence; re-running it adds none.

## C. Never fabricate

The rule is AGENTS.md "Evidence, never invention", and it covers ids, paths, line numbers, versions
and API behaviour as much as counts and hashes. The traps behind past fabrications:

1. **Writing the summary before its evidence exists.** Read the proving output, confirm it, then
   write the doc, CHANGELOG or commit; never in the same tool batch as the commands that produce it.
2. **Filling a failed or empty read with a guess.** A failed read is a stop sign. The Read tool
   cannot see a Git Bash `/tmp` path, so put scratch files in the session scratchpad.
3. **Trusting your own earlier result from memory.** A cancelled or errored call leaves no trace but
   your belief that it ran. Re-run `git log`, re-read the file.
4. **Reporting an empty search as absence.** No hit proves absence only inside what was searched:
   name the scope (which build, which modules, which folders) with the negative result. A capped
   result (a Grep `head_limit`, `truncated: true`) proves neither a complete list nor a total: say it
   was capped, or re-run it uncapped.
