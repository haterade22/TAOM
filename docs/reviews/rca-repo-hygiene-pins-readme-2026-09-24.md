# RCA: plan 016, repo hygiene, MCP pins and README (2026-09-24)

## Top-line

Branch `improve/016-repo-hygiene-pins-readme`, diff `bec0389d..8a638831` (#657), reviewed by four
`/deep-review` lenses (standards, completeness, data flow, design) and one Codex adversarial pass
(gpt-6-astra, ultra). The change itself is sound: the nine denies moved intact, the three paths are
untracked and ignored, and every pin resolves. **Ten findings were confirmed, nine LOW and one NIT,
all in documentation, and all fixed on the branch** (report:
`docs/reviews/deep-review-016-repo-hygiene-pins-readme-2026-09-24.md`). They share one root: **the
change moved or pinned a fact in the file the plan named, and did not search for the other places
that state the same fact or depend on it.**

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| D1 | LOW | Both CHANGELOG entries under `## 2026-09-24`; commits dated 2026-09-25 | Convention inconsistency | The file already had 09-24 above 09-25, and the executor matched the top heading instead of today's | Moved; no rule (plan Step 9.1 already said it) |
| D2 | LOW | #657 not named in the entries or commits | Convention inconsistency | The plan named the issue only in prose | Added `(#657)`; no new rule (`completion-workflow.md:40`) |
| D3 | LOW | Pin policy (bump every copy, audit sees only `npx -y` in `.mcp.json`, re-derive the deny list) only in the plan | Other: standing duty with no home | The plan's "Maintenance notes" were treated as documentation | `mcp-servers.md` **Pins.** paragraph and deny derivation sentence |
| D4 | NIT | "Culture-specific" capitalised, unlike every sibling bullet | Convention inconsistency | Plan-prescribed text pasted as is | Lowercased |
| D5 | LOW | `.vscode/mcp.json.example` and the `kingdom-voices.md` snippet kept the unpinned launch strings | Other: incomplete propagation | The plan scoped the files the audit reads; nobody grepped the launch strings | Pinned; lesson "When a fact moves, grep every statement of it" |
| D6 | LOW | "Two things" recount omitted `.codex/config.toml`; "each machine" where the file is per checkout | Other: incomplete propagation | Recount done by removing one bullet, not by re-deriving the list | Fixed; same lesson |
| D7 | LOW | A lesson's **Prevent:** still sends deny edits to the untracked file | Other: incomplete propagation | Lessons were out of plan scope, so no grep reached them | Parenthetical added; same lesson |
| D8 | LOW | Migration note silent on branch switches to `bannerlord-1.4.5` and on new worktrees | Stale state / lifecycle | The note traced only merge and pull, not every way a checkout changes | Note and `development-machines.md` extended; lesson "Untracking a file another branch tracks" |
| D9 | LOW | Restore command `git show ... > file` writes UTF-16LE in Windows PowerShell 5.1 | Assumed an API worked a certain way | The command was written for Git Bash and read as portable | `git restore --source=...`; lesson "Give the command that writes the file itself" |
| D10 | LOW | ModuleData activation step says the server is already enabled | Other: incomplete propagation | The plan unlinked the filename and kept the sentence (Plan:627-630) | Reworded; lesson "When a fact moves" |

## Root-cause pattern

D5, D6, D7 and D10 are one pattern: the plan listed the files to edit, the executor edited exactly
those, and every other statement of the same fact stayed stale. A plan's scope list is a starting
point, not a search. D8 and D9 are the same blindness about the reader's environment: the note was
correct for the author's shell and the author's single branch.

## Why each agent missed these

- **Executor (plan 016):** followed the plan's file list and prescribed text. The plan never asked
  for a repo grep of the moved facts (`settings.local.json`, the launch strings, the "Three things"
  list).
- **Agent 1, Standards:** caught D1 to D4; D5 to D10 are content accuracy, outside its checklist.
  It raised D9 only as an out-of-lens note.
- **Agent 4, Completeness:** caught D1 to D3, D5, D6, D8 and D9; listed D7 as a follow-up nit
  because lessons were out of plan scope; missed D10.
- **Agent 5, Data flow:** caught D5 to D9 by tracing each moved fact to its readers; missed D10 and
  the CHANGELOG placement, which are not flows.
- **Agent 6, Design:** found D9 and D10 as proposals (P2, P7) and the pin rule (P5).
- **Codex:** found D10 only. It verified the moved content and the pins exhaustively but did not
  search outside the diff for other copies, and did not consider other shells or branches.

## Feedback memories to codify

Three lessons appended to `docs/reviews/lessons/build-tooling-workflow.md`:

1. When a fact moves, grep every statement of it (D5, D6, D7, D10).
2. Give the command that writes the file itself (D9).
3. Untracking a file another live branch tracks changes every switch and every new worktree (D8).

D1, D2 and D4 are one-off; existing rules already cover them.
