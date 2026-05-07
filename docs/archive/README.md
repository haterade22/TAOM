# docs/archive/

Historical documentation moved out of active namespaces (`docs/reviews/`, `docs/prompts/`, `docs/research/`, `docs/feature-port-prompts/`) once the work they describe shipped. Files are preserved here for audit trail; new work should not link to them — check `git log` for the original path.

## Contents

| Subdir | What's inside |
|--------|---------------|
| `codex-reviews-2026-04/` | Codex adversarial reviews + prompts from the April 2026 port wave (37 files, all referenced features now committed and stable). |
| `feature-port-prompts/` | Self-contained session prompts (5 + README) used to drive parallel sessions during the May 2026 7-feature port. The port is complete. |
| `research-prompts-2026-04/` | One-shot Codex research prompts and TOR comparison studies whose outputs have shipped (career system, special resources, settlement guards, etc.). |

## Adding to the archive

When archiving a finished review or research artifact:
1. `git mv docs/reviews/<file> docs/archive/<subdir>/`
2. Update any reference in `CHANGELOG.md`, `AGENTS.md`, `MEMORY.md`, or `.claude/projects/.../memory/` from the old path to the new.
3. Commit with message `docs(archive): move <feature> review under docs/archive/<subdir>/`.

If the file is cited from multiple places (rule of thumb: >3 stable references), prefer to keep it in the active namespace — archiving creates link-rot.
