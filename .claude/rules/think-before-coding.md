# Think before coding

Before the first edit on a non-trivial request, say in one line what you are about to do and what
you are assuming: *"Patching `Formation.SetMovementOrder`. Assuming a Postfix, since vanilla's side
effects must run first."* If an assumption is both load-bearing and uncertain, and guessing wrong
would waste real work, use `AskUserQuestion` first.

**It fires** when the request admits several reasonable readings that the files, recent commits, the
loaded instructions and sibling conventions cannot settle. Signals: a vague verb (refactor, clean
up, improve, add support for), a system named without its version or mode, an outcome without a
boundary ("make X faster": which path?).

**Don't ask** about trivial or mechanical work, routing you can decide (which skill, agent or DLL),
anything an ADR, rule or quick run settles (an adapter for a sealed type is not a question: ADR-007),
or a cheap edit you can revert in seconds. Over-asking is its own failure.

**Make the goal testable before the first edit.** "Fix the bug" becomes "write a failing test that
reproduces the NRE, make it pass, keep `TAOM.Tests` green".

**Open-ended work** (several viable designs): do the analysis, state the recommendation and its
reason in one line, apply it, and say how to undo it (Mike, 2026-10-15). Git, public actions, paid AI,
deleting his files and mandatory review gates still need his word. When Mike must decide: one item per
message in chat (what, pros, cons, recommendation); a catalogue of dozens goes in a filterable HTML
report with batch answers (Mike, 2026-09-29, 2026-10-18).

**Never drop part of the request silently.** If something must be cut to fit, name it and offer a
split (now, or a follow-up issue); dropping it is the user's decision.

**Reuse before writing**, top-down, stopping at the first rung that works:

1. The engine already provides it (a GameModel hook, a `CampaignEvent`): verify with `taom-src`.
2. An existing TAOM service or adapter already does it (ADR-002, ADR-007).
3. A one-line delegation into that service would do.
4. Only then write the minimum: no interface unless it wraps an engine type (an adapter), a test
   fakes it, a second class implements it, or it narrows a wide service for a patch (a hook,
   AGENTS.md "Architecture"); no plumbing "for later" (`simplicity-criterion.md`).
