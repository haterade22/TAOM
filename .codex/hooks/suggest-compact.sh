#!/bin/bash
# PreToolUse hook (*): Suggest manual compaction at logical intervals AND
# at task-boundary signals.
#
# Layered strategy (per Pick #9 from Tier 2 ecosystem-review adoption):
#   1. Threshold-based — every 50 tool calls, then every 25 after
#      (catches sessions that just keep going without natural breaks)
#   2. Boundary-aware — when a Bash command signals a task transition
#      (a successful /verify pass, a `git commit` succeeding, a feature
#      shipping). Suggest BEFORE the next feature, not mid-task.
#
# Boundary signals are softer than thresholds — they nudge the agent to
# /compact when it's safe (between tasks), reducing the chance that
# auto-compact fires mid-task.

INPUT=$(cat)

# Session-specific counter file
SESSION_ID="${CLAUDE_SESSION_ID:-default}"
SESSION_ID=$(echo "$SESSION_ID" | tr -cd 'a-zA-Z0-9_-')
COUNTER_FILE="/tmp/claude-tool-count-${SESSION_ID}"
BOUNDARY_FILE="/tmp/claude-last-boundary-${SESSION_ID}"
THRESHOLD=${COMPACT_THRESHOLD:-50}

# Read and increment counter
COUNT=1
if [[ -f "$COUNTER_FILE" ]]; then
  PREV=$(cat "$COUNTER_FILE" 2>/dev/null | tr -cd '0-9')
  if [[ -n "$PREV" && "$PREV" -gt 0 && "$PREV" -lt 1000000 ]]; then
    COUNT=$((PREV + 1))
  fi
fi
echo "$COUNT" > "$COUNTER_FILE" 2>/dev/null

# --- Threshold-based suggestions (legacy behavior) ---
if [[ "$COUNT" -eq "$THRESHOLD" ]]; then
  echo "[Compact] ${THRESHOLD} tool calls reached — consider /compact if transitioning phases." >&2
fi
if [[ "$COUNT" -gt "$THRESHOLD" ]]; then
  PAST=$((COUNT - THRESHOLD))
  if [[ $((PAST % 25)) -eq 0 ]]; then
    echo "[Compact] ${COUNT} tool calls — good checkpoint for /compact if context is stale." >&2
  fi
fi

# --- Boundary-aware suggestions (Pick #9) ---
# Inspect the tool input to detect task-boundary signals. Only fire one
# boundary suggestion per ~10 calls so we don't spam.
COMMAND=""
if command -v python3 >/dev/null 2>&1; then
  COMMAND=$(printf '%s' "$INPUT" | python3 -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
    print(d.get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)
fi

# Check throttle: don't re-fire boundary suggestion within 10 calls of last one
LAST_BOUNDARY=0
if [[ -f "$BOUNDARY_FILE" ]]; then
  LAST_BOUNDARY=$(cat "$BOUNDARY_FILE" 2>/dev/null | tr -cd '0-9')
  [[ -z "$LAST_BOUNDARY" ]] && LAST_BOUNDARY=0
fi
THROTTLE_OK=0
if [[ $((COUNT - LAST_BOUNDARY)) -gt 10 ]] || [[ $LAST_BOUNDARY -eq 0 ]]; then
  THROTTLE_OK=1
fi

if [[ $THROTTLE_OK -eq 1 && -n "$COMMAND" ]]; then
  # Detect task-boundary signals in the upcoming Bash command.
  # Use the canonical commit-detection pattern from .claude/rules/harness-facts.md
  # "Git invocation forms hooks must handle" — DO NOT regress to a bare
  # `*"git commit"*` substring match. The pattern catches `git commit`,
  # `git -C path commit`, `git -c key=val commit`, while rejecting
  # `git commit-tree` and `git commit-graph` (different commands).
  BOUNDARY_HINT=""

  # Step 1: reject commit-tree / commit-graph (different commands)
  IS_COMMIT_PLUMBING=0
  case "$COMMAND" in
    *"git commit-"*) IS_COMMIT_PLUMBING=1 ;;
  esac

  # Step 2: detect actual git commit invocations
  if [[ $IS_COMMIT_PLUMBING -eq 0 ]]; then
    case "$COMMAND" in
      *"git commit"* | *"git -"*" commit"* )
        BOUNDARY_HINT="just before a git commit"
        ;;
    esac
  fi

  # Other boundary signals
  if [[ -z "$BOUNDARY_HINT" ]]; then
    case "$COMMAND" in
      *"./build.ps1"* | *"dotnet build"* | *"dotnet test"* )
        # Build/test invocations often signal a phase boundary
        [[ "$COUNT" -gt 30 ]] && BOUNDARY_HINT="after build/test (good time to consolidate context)"
        ;;
      *"git push"* )
        BOUNDARY_HINT="after pushing (a fresh start makes sense for the next task)"
        ;;
    esac
  fi

  if [[ -n "$BOUNDARY_HINT" ]]; then
    echo "[Compact] Task boundary detected ${BOUNDARY_HINT}. Consider /compact + /context-save here — preserves decisions but trims in-context tokens for the next task." >&2
    echo "$COUNT" > "$BOUNDARY_FILE" 2>/dev/null
  fi
fi

echo "$INPUT"
exit 0
