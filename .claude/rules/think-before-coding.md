---
description: Surface load-bearing assumptions before the first Edit. Ask if uncertain. Don't ask on trivial/mechanical work — that's the opposite failure mode.
---

<!-- NO paths: intentionally — always-load. See harness-facts.md "Rule loader (memory) semantics". -->

# Think Before Coding

Before writing code for a non-trivial request, state the load-bearing assumptions you're making in one sentence each. If any assumption is *both load-bearing and uncertain*, use `AskUserQuestion` BEFORE the first Edit/Write. If they're load-bearing but obvious-from-context, state and proceed.

## When the rule fires

The rule fires when the request *admits multiple reasonable interpretations* and you cannot infer the right one from:

- the files currently on disk,
- recent commits (last few entries of `git log`),
- CLAUDE.md / ADRs / scoped rules already loaded into context,
- conventions visible in sibling files.

Practical signals: the user used a vague verb (*"refactor", "clean up", "improve", "add support for"*), referenced a system without saying which version/mode, or described an outcome without specifying the boundary (*"make X faster" — for which call path? all of them?*).

## When NOT to ask (avoid the opposite failure mode)

Asking about every minor detail is its own bug — friction that erodes trust and slows the user down. Skip the surface-assumptions step for:

- **Trivial / mechanical work** — rename `foo` → `bar` per user spec, fix a typo, add a log line, run `/verify`, apply a diff the user already pasted.
- **Routing decisions Claude can make from context** — which skill to invoke, which agent type to spawn, which TaleWorlds DLL to decompile.
- **Conventions documented in CLAUDE.md / ADRs / scoped rules** — apply them, don't re-litigate. *"Should I use an adapter for this sealed type?"* is not a question — ADR-007 already answers it.
- **Recoverable / cheap-to-redo work** — a one-line Edit you can revert in seconds doesn't need an assumption-surfacing preamble.

## When the work is genuinely open-ended (lightweight design pass)

If the request is not just ambiguous but *open-ended* — multiple viable designs, no single obvious approach (a new feature, an architectural choice, *"how should we do X?"*) — do a short design pass before building. Not a silent guess; not a heavyweight spec process either:

- **Ask one question at a time, multiple-choice where possible.** `AskUserQuestion` is the tool — one focused decision per question beats a wall of open prompts. Stop once the load-bearing choices are settled.
- **Propose 2-3 approaches, each with a one-line trade-off, and a recommendation.** Let the user pick a direction before you invest in one: *"I'd go with B (simplest, fits ADR-007); A is faster but adds an adapter; C is most flexible but YAGNI."*

This is deliberately lighter than a formal design-doc-per-feature gate (rejected as too heavy for an expert solo dev on most TAOM work). It's the same discipline as the rest of this rule — surface the fork before committing to a branch — applied when the fork is a *design* choice rather than an *interpretation* choice. The "When NOT to ask" guard above still governs: trivial / mechanical / recoverable work skips this entirely.

## Reuse ladder (check what exists before writing new)

Once you know *what* the user asked for, walk this ladder top-down and stop at the first rung that satisfies the need. Writing new code is the LAST resort, not the first.

1. **Does TaleWorlds already provide it?** A GameModel hook, a `CampaignEvent`, an existing engine method — verify via `/research` / `taom-src` before assuming you must build it (Critical Rules: *Research First*, *Verify Before Reference*).
2. **Does an existing TAOM service / adapter already do it?** Reuse it (ADR-007 / ADR-002); never re-wrap a sealed type that already has an adapter, or duplicate logic a service already owns.
3. **Can it be a one-line delegation** into that existing service/model?
4. **Only then: write the minimum.** No interface-with-one-implementation, no "in case we need it later" plumbing (`simplicity-criterion.md` rejects both).

This is the *reuse-before-write* companion to `simplicity-criterion.md`'s *keep-or-reject* matrix: that rule judges a change after it exists; this ladder stops the unnecessary one from being written.

## How to apply

At the start of a non-trivial task, before the first Edit/Write, say in one line what you're about to do AND what you're assuming.

**Good examples:**

- *"Adding `IInventoryVMAdapter`. Assuming TAOM-owned interface (per ADR-007), not extension of vanilla `IInventoryViewModel`."*
- *"Porting `think-before-coding`. Assuming you want a single always-load rule, not a bundled skill — confirm before I write."*
- *"Patching `Formation.SetMovementOrder`. Assuming Postfix (not Prefix) since vanilla side effects must run first. Verify in `/research` if unsure."*

**Bad (silent) examples:**

- Writes the file with one of three plausible interpretations baked in, then waits for the user to notice the wrong call.
- Picks "make X faster" to mean micro-optimizing a hot loop when the user meant reducing allocator pressure across the feature.

If the assumption is uncertain enough that picking wrong wastes meaningful work (more than a few lines / a few minutes), use `AskUserQuestion` instead of stating-and-proceeding.

_Provenance (why this rule exists, relationships, sources): [docs/reference/rule-provenance.md](../../docs/reference/rule-provenance.md)._
