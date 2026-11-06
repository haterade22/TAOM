# claude-hud adoption review (2026-10-26)

Review of [jarrodwatts/claude-hud](https://github.com/jarrodwatts/claude-hud) through
`/adopt-external`. Mike asked for its information to be handy while he works. Issue
[#1012](https://github.com/haterade22/TAOM/issues/1012).

## What it is

A Claude Code status line command in Node (MIT), installed as a plugin with two commands
(`setup`, `configure`). It reads the status line payload on stdin and prints the model, branch,
a context bar and the subscription windows (5-hour and weekly, with reset times), and on request
lines for running tools, agents and todos, which it parses from the session transcript. Read
through a shallow clone (`main` at `cf95a32`); nothing was installed or run.

## Security

Safe to learn from. Its README states it makes no network requests and reads no credentials;
its optional `--extra-cmd` runs a shell command on every repaint only when
`CLAUDE_HUD_ALLOW_EXTRA_CMD=1` is set. Not installed: TAOM keeps its own status line.

## Decision

| # | Item | Decision |
|---|---|---|
| A1 | The 5-hour and weekly usage windows with reset times, an 80% mark, and the prompt cache state | Ported into `.claude/statusline.sh` as a second line, shown only when Claude Code sends `rate_limits` (#1012). Pure bash, no subprocess: the repaint stays at about 135 ms, the same as before |
| A2 | A latent bug found on the way | `ctx:` read the first `used_percentage` in the payload, which can be a usage window; it now reads the context window's own `remaining_percentage` |
| X1 | Install the plugin | Declined: a Node start on every repaint, and it would replace a status line that is tuned and documented |
| X2 | Tool, agent and todo lines | Not ported: they need the transcript parsed on every repaint, too slow for a bash status line. The VS Code agent map shows running agents |
| X3 | Cost lines | Not ported: TAOM uses a subscription, so a list-price cost misleads |
| X4 | The VS Code panel | The status line shows only in the terminal and the extension's terminal mode, and Mike works in the graphical panel. There, the prompt box shows context use and `/usage` shows the session and week bars with reset times. Mike chose `/usage` on 2026-10-26; an always-visible TAOM mod (a plugin can draw a status line above the prompt box, extension 2.1.299 or later) was offered and not built |

---
