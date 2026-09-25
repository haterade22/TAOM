#!/bin/bash
# Stop hook: remind Claude to run /deep-review when reviewable work is dirty and no review ran
# recently. A reminder, not a gate: it cannot stop a commit.
#
# Channel: one JSON {"decision":"block","reason":...} on stdout per streak, through
# _stop_reminder.sh (see check-verification-evidence.sh). Silent when stop_hook_active is true,
# though the marker still clears then.
# Always exits 0; any internal failure exits 0 with no output (fail open).
#
# Muting, two layers:
#  1. A deep-reviewer run in the agent audit log within the last 8 h mutes it and clears the
#     streak marker. Recency-scoped (matching session-stop.sh's window): before 2026-07-12 this
#     grepped the whole never-rotated log, so months-old runs permanently muted the reminder.
#     If date arithmetic is unavailable, fall back to the whole-file grep.
#  2. .deep-review-reminded (plan 011): one reminder per streak. It clears when nothing
#     reviewable is dirty or a review is logged, which re-arms the reminder.

source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
INPUT=$(cat)
cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0

REMINDED=".claude/logs/.deep-review-reminded"
AUDIT_LOG=".claude/logs/agent-audit.log"
# log-agent.sh writes "[TS] agent_type=<type> agent_id=<id>" and nothing else, so the only
# evidence of a review is its reviewer's type. tools/test_hooks.sh section 7 drives both
# hooks together; change this pattern and that line format only as a pair.
PATTERN="agent_type=deep-reviewer"
if [[ -f "$AUDIT_LOG" ]]; then
  CUTOFF=$(date -d '-8 hours' '+%Y-%m-%d %H:%M:%S' 2>/dev/null)
  if [[ -n "$CUTOFF" ]]; then
    if awk -v c="$CUTOFF" -F'[][]' '$2 >= c' "$AUDIT_LOG" 2>/dev/null | grep -q "$PATTERN" 2>/dev/null; then
      rm -f "$REMINDED" 2>/dev/null || true
      exit 0
    fi
  elif grep -q "$PATTERN" "$AUDIT_LOG" 2>/dev/null; then
    rm -f "$REMINDED" 2>/dev/null || true
    exit 0
  fi
fi

# Any reviewable file modified or untracked means real work was done.
CHANGED_FILES=$(git diff --name-only 2>/dev/null)
UNTRACKED_FILES=$(git ls-files --others --exclude-standard 2>/dev/null)
ALL_FILES="$CHANGED_FILES"$'\n'"$UNTRACKED_FILES"

# C# and C++ code, and XML is code too (Mike, 2026-09-18). git cannot see edits in the live
# TAOM_Map / Armory installs, so those never trigger this; /deep-review Step 1 sweeps them.
if echo "$ALL_FILES" | grep -qE '\.(cs|cpp|h|xml|xsl|xslt|mbproj|json)$'; then
  if [[ ! -f "$REMINDED" ]]; then
    taom_stop_hook_active "$INPUT" && exit 0
    taom_stop_block "check-deep-review: C#, C++, XML, XSLT or JSON files are modified or untracked in this tree and no deep-reviewer agent has run in the last 8 hours (.claude/logs/agent-audit.log). Run /deep-review before committing that work. If the files belong to another session, or the work is not ready to commit, say so in one line. This fires once per streak; a logged deep-reviewer run re-arms it."
    mkdir -p .claude/logs 2>/dev/null
    touch "$REMINDED" 2>/dev/null || true
  fi
else
  # Nothing reviewable is dirty: re-arm for the next streak.
  rm -f "$REMINDED" 2>/dev/null || true
fi

exit 0
