# Think before coding

Before the first edit on a non-trivial request, say in one line what you are about to do and
assume: *"Patching `Formation.SetMovementOrder`. Assuming a Postfix."* If an assumption is load-bearing and uncertain, and a wrong guess
would waste real work, use `AskUserQuestion` first.

**It fires** when the request admits several reasonable readings that the files, commits, loaded
instructions and sibling conventions cannot settle. Signals: a vague verb (refactor, clean
up, improve), a system named without its version or mode, an outcome without a
boundary.

**Don't ask** about trivial or mechanical work, routing you can decide (which skill, agent or DLL),
anything an ADR, rule or quick run settles (a sealed-type adapter is not a question: ADR-007),
or a cheap edit you can revert in seconds. Over-asking is its own failure.

**Make the goal testable before the first edit.** "Fix the bug" becomes "write a failing test that
reproduces the NRE, make it pass, keep `TAOM.Tests` green".

**Open-ended work** (several viable designs): do the analysis, state the recommendation and its
reason in one line, apply it, and say how to undo it (Mike, 2026-10-15). Git, public actions, paid AI,
deleting his files and mandatory review gates still need his word. When Mike must decide: one item per
message in chat (what, pros, cons, recommendation); a catalogue of dozens goes in a filterable HTML
report with batch answers (Mike, 2026-09-29, 2026-10-18).

**Standing word (Mike, 2026-11-12):** do what you recommend, repo-required issues too; ask only where you
have no recommendation. The git, review-gate, paid-AI and deletion limits above stay.

**Never drop part of the request silently.** If something must be cut to fit, name it and offer a
split (now, or a follow-up issue); dropping it is Mike's decision.

**Reuse before writing** C#: the ladder (engine, existing service, one-line delegation, then the
minimum) is in `csharp-patterns.md`, which loads with any `.cs` file.
