---
description: Surface load-bearing assumptions before the first edit and ask when one is uncertain; never ask on trivial or mechanical work.
---

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
anything an ADR or rule already settles (an adapter for a sealed type is not a question: ADR-007),
or a cheap edit you can revert in seconds. Over-asking is its own failure.

**Make the goal testable before the first edit.** "Fix the bug" becomes "write a failing test that
reproduces the NRE, make it pass, keep `TAOM.Tests` green".

**Open-ended work** (several viable designs): ask one multiple-choice question at a time, and offer
two or three approaches, each with a one-line trade-off and a recommendation, before investing in
one.

**Reuse before writing**, top-down, stopping at the first rung that works:

1. The engine already provides it (a GameModel hook, a `CampaignEvent`): verify with `taom-src`.
2. An existing TAOM service or adapter already does it (ADR-002, ADR-007).
3. A one-line delegation into that service would do.
4. Only then write the minimum: no interface unless it wraps an engine type (an adapter), a test
   fakes it or a second class implements it; no plumbing "for later" (`simplicity-criterion.md`).
