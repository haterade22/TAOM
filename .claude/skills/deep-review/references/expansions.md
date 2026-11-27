# Step 2c expansion triggers

Step 2c of `.claude/skills/deep-review/SKILL.md` checks the core agents' findings and the scope against this list.

**Launch additional agents when:**
- Agent 5 (Data Flow) finds gaps → launch per-gap investigation agents to trace the full chain and propose fixes
- Multiple XML config files changed → launch one agent per config file to cross-reference all consumers
- Multiple Harmony patches changed → launch one agent per patch to verify target method signatures and side effects
- Multiple GameModel overrides changed → launch one agent to verify all overrides are registered and don't conflict
- Any agent reports >3 issues → launch a focused agent on just those files to determine root cause
- Feature spans >3 features/ subdirectories → launch per-feature agents with full context of that feature
- The change adds a mission type or a new agent spawn path → launch one agent (Lens 2 method) on every TAOM behavior and `[DefaultView]` that attaches to all missions, judged in the new context (no main agent, no team AI, late spawns, no campaign); per-file lenses cannot see this (Fords of War: a pre-existing warg formation bug became the normal path, `rca-fords-of-war-2026-10-31.md` D4)

(Script tooling is no longer an expansion: the Tooling correctness lens in Step 2 covers every script under `tools/` or `.claude/hooks/`, read-only ones included.)
