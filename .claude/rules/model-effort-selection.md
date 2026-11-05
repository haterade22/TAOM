# Model & Effort Selection

**The main session does most of the work** (Mike, 2026-11-02). Delegate only independent work that
can run alongside it, a side job, or what a review gate requires, and give each subagent the
cheapest model and effort that fits.

| Tier | Agent | Use for |
|---|---|---|
| **Haiku, low** | `fast-reader` | File and code search, grepping, listing, reading and summarizing files, simple renames, formatting, boilerplate, running tests or builds and reporting results, small edits already well specified |
| **Sonnet, medium** | `implementer` | Most everyday coding: features from a clear plan, tests, normal bug fixes, refactors inside one module, docs, review of small diffs |
| **Opus, high** | `architect` | Architecture and design, ambiguous or cross-cutting changes, hard debugging with an unknown cause, security-sensitive code, data migrations, anything expensive to get wrong or hard to undo |
| **Fable** | none | Only the hardest reasoning problems, or after Opus has failed; never routine. Spawn with `model: "fable"` |

Specialists set their own tier in their definitions. An agent runs one model, so refactors, new
features and non-obvious debugging start with an `architect` plan, then a Sonnet specialist
executes it.

**Escalation and de-escalation:**

- Start at the lowest tier that plausibly fits.
- Wrong, incomplete or confused output gets one retry at the next tier up, never at the same tier.
- When an Opus task breaks into well-defined steps, hand those steps down to Sonnet or Haiku.

**Transparency:** before delegating, write one line naming the choice and why:
`→ fast-reader (Haiku/low): repo-wide search`.

**Standing word (Mike, 2026-11-05):** pick each spawn's model and effort yourself, without asking. That is the explicit ask the Agent tool's `effort` parameter needs; the `deep-reviewer` rule below still holds.

**Mechanics:** an agent definition sets `model:` and `effort:`; a spawn overrides them with the
Agent tool's `model` and `effort` parameters. Never pass `model` to `deep-reviewer`, and pass its
`effort` only as `/deep-review`'s tier rule says. Built-in
agents (Explore, Plan, general-purpose) carry no tier and run on the main session's model: name a
`model` on every such spawn.
