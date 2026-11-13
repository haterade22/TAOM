# PowerShell Tool (Windows)

> The native PowerShell tool: when it is on, and its knobs. Extracted from CLAUDE.md 2026-07-18;
> refreshed against the docs 2026-10-08
> ([tools reference](https://code.claude.com/docs/en/tools-reference#powershell-tool)).

**Default:** on Windows with Git Bash installed, the tool is on by default for claude.ai and Console
accounts. When it is on, PowerShell is the primary shell and the Bash tool stays available for POSIX
scripts. Claude Code prefers `pwsh.exe` (PowerShell 7+), falls back to `powershell.exe`, and runs it
with `-ExecutionPolicy Bypass` at process scope only. `CLAUDE_CODE_USE_POWERSHELL_TOOL=0` turns it
off; `=1` forces it on.

**Trap: a Bash deny rule turns PowerShell off.** On Windows with Git Bash, any `Bash` deny rule,
scoped (`Bash(git push *)`) or bare, switches the PowerShell tool off for the session, because a
Bash rule does not restrict PowerShell. Before adding one, set `CLAUDE_CODE_USE_POWERSHELL_TOOL=1`
or add the matching `PowerShell(...)` deny rule. TAOM has no Bash deny rules today.

**Trap: Windows PowerShell 5.1 corrupts UTF-8 text on a read-modify-write.** `Get-Content -Raw`
in `powershell.exe` (5.1) reads a UTF-8 file without a BOM as Windows-1252, so
`$t = Get-Content f -Raw; [IO.File]::WriteAllText(f, $t.Replace(...))` writes every non-ASCII
character back as mojibake (`…` becomes `â€¦`, `ï` becomes `Ã¯`). A compile or `py_compile` still
passes, so nothing flags it. Edit source with the Edit tool; script a bulk replace in Python with
`encoding="utf-8"` on both sides; after any bulk edit, grep the changed files for `Ã`, `â€` and `Â`.
Repair: re-encode each damaged line as cp1252 and decode it as UTF-8. Several TAOM tools run under
5.1 (`powershell.exe -File ...` in the quadruped workflow), so the tool's `pwsh` default does not
remove the trap. Source: Kysaro's notes (`feedback_powershell_utf8_roundtrip.md`, 2026-10-21).

**Hooks:** a PreToolUse hook sees `tool_name` `"PowerShell"` and the command in
`tool_input.command`, so every shell gate registers as `Bash|PowerShell`
([harness-facts.md](../../.claude/rules/harness-facts.md)).

**Additional settings:**
| Setting | Location | Effect |
|---------|----------|--------|
| `"defaultShell": "powershell"` | `settings.json` | Routes interactive `!` commands through PowerShell (needs the tool on) |
| `"shell": "powershell"` | Hook definition | Runs that hook in PowerShell, whether or not the tool is on |
| `shell: powershell` | Skill frontmatter | Runs `` !`command` `` blocks in PowerShell (needs the tool on) |

Git Bash is still required on Windows for the Bash tool and every TAOM hook.
