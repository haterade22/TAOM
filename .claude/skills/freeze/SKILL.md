---
name: freeze
description: Lock all file edits to a single directory for the rest of the session. Hard-blocks Edit/Write outside the chosen path. Use when fixing one feature and you don't want agents drifting into adjacent code. Pair with /unfreeze to release.
allowed-tools:
  - Bash
  - Read
  - AskUserQuestion
hooks:
  PreToolUse:
    - matcher: "Edit"
      hooks:
        - type: command
          command: "bash ${CLAUDE_PROJECT_DIR}/.claude/skills/freeze/bin/check-freeze.sh"
    - matcher: "Write"
      hooks:
        - type: command
          command: "bash ${CLAUDE_PROJECT_DIR}/.claude/skills/freeze/bin/check-freeze.sh"
    - matcher: "NotebookEdit"
      hooks:
        - type: command
          command: "bash ${CLAUDE_PROJECT_DIR}/.claude/skills/freeze/bin/check-freeze.sh"
---

# /freeze — Restrict Edits to a Directory

Hard-block any Edit, Write, or NotebookEdit to a file outside the chosen directory for the remainder of the session.

Adapted from [garrytan/gstack/freeze](https://github.com/garrytan/gstack/tree/main/freeze). Uses the inline-hooks-in-skill-frontmatter pattern: the PreToolUse hooks above only fire while this skill is active — no global `settings.json` change.

## When to use

- Fixing one feature and want zero risk of touching unrelated code
- Running `/deep-review` or `feature-builder` and want scope-locked
- Debugging — pair with `/investigate`, which auto-engages this hook
- Long sessions where context drift could cause edit creep

## Setup

1. Use `AskUserQuestion` to ask the user which directory to lock to:
   - Question: *"Which directory should I restrict edits to? Files outside this path will be blocked."*
   - Free-text input — the user types a path. Common choices for TAOM:
     - `Main/Features/<FeatureName>/` — single feature scope
     - `Main/Adapters/` — adapter layer only
     - `Main/_Module/ModuleData/` — XML data only
     - `TAOM.Tests/` — tests only

2. Resolve to absolute path and persist to the state file:

```bash
FREEZE_INPUT="<user-provided-path>"
# Resolve relative paths against the project root
if [[ "$FREEZE_INPUT" != /* && "$FREEZE_INPUT" != [A-Za-z]:* ]]; then
    FREEZE_DIR="$(cd "$FREEZE_INPUT" 2>/dev/null && pwd)"
else
    FREEZE_DIR="$(cd "$FREEZE_INPUT" 2>/dev/null && pwd)"
fi

if [[ -z "$FREEZE_DIR" || ! -d "$FREEZE_DIR" ]]; then
    echo "Could not resolve '$FREEZE_INPUT' to a directory."
    exit 1
fi

STATE_DIR="${CLAUDE_PROJECT_DIR}/.claude/tmp/freeze"
mkdir -p "$STATE_DIR"
echo "$FREEZE_DIR" > "$STATE_DIR/freeze-dir.txt"
echo "Freeze boundary set: $FREEZE_DIR"
```

3. Confirm to the user:

> Edits are now locked to `<resolved-path>/`. Any Edit, Write, or NotebookEdit outside this directory will be hard-blocked. Run `/unfreeze` to release.

## How it works

The PreToolUse hooks declared in this skill's frontmatter activate the moment the skill is invoked. On every Edit/Write/NotebookEdit tool call, `check-freeze.sh` runs:

1. Reads the freeze-dir state file (`.claude/tmp/freeze/freeze-dir.txt`)
2. Extracts `file_path` from the tool call's JSON input
3. Resolves both paths to absolute, normalizes slashes (Windows + Git Bash compatible)
4. If the file is outside the freeze boundary → returns `{"permissionDecision":"deny",...}` to block
5. Otherwise → returns `{}` to allow

The hook is silent on success and verbose only when blocking, so freeze adds zero noise to normal work.

## What's blocked vs. allowed

| Tool | Behavior |
|------|----------|
| Edit | Blocked outside boundary |
| Write | Blocked outside boundary |
| NotebookEdit | Blocked outside boundary |
| Read, Glob, Grep | Always allowed (read-only) |
| Bash | Always allowed — `sed`, `tee`, `>` etc. can still bypass freeze. **This is not a security boundary.** |
| MCP server tools (Serena's `replace_content`, etc.) | Not blocked — these are separate tools. Use Edit/Write for changes you want freeze-protected. |

## Example session

```
User: /freeze
Skill: Asks "Which directory?" → user answers "Main/Features/CareerSystem/"
       Sets boundary to C:/Users/mikew/source/repos/TAOM/Main/Features/CareerSystem/
       
User: Edit Main/Features/CareerSystem/Services/CareerService.cs
       → ALLOWED (inside boundary)

User: Edit Main/Adapters/HeroAdapter.cs
       → BLOCKED: "[freeze] Blocked: ... is outside the freeze boundary (.../CareerSystem/)"

User: /unfreeze
       → Boundary cleared
```

## Notes

- Trailing-slash normalization prevents `/Main` from matching `/Main_old`
- Symlinks resolved before comparison (POSIX-portable, works in Git Bash)
- The state file is in `.claude/tmp/` (gitignored) — boundary clears if you delete the file or run `/unfreeze`
- This is a **productivity guard, not a security boundary**. A determined Bash command (`echo > /etc/passwd`) can still touch anything the user has write access to. The point is to catch accidental drift by the agent, not to protect against malicious actions.
