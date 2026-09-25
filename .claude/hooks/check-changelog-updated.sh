#!/bin/bash
# Stop hook: Warn if CHANGELOG.md hasn't been modified in this session
# This is a soft reminder, not a hard block
#
# Mirrors check-verification-evidence.sh: one JSON {"decision":"block","reason":...} on
# stdout per streak through _stop_reminder.sh (until plan 011 it wrote to stderr, which
# Claude never sees), silent when stop_hook_active is true, always exits 0 (fail open).
#
# Signal: a dirty .cs/.xml/.xslt/.json file (real work happened) while
# CHANGELOG.md is untouched in both the working tree and the index.
#
# Muting: fires ONCE per un-updated streak. A .changelog-reminded marker
# records "already reminded"; it is cleared the moment CHANGELOG.md becomes
# dirty/staged (or the session has no relevant work), re-arming the reminder
# for the next streak. Added 2026-08-05: this hook predated the
# hook-authoring.md "mirror the sibling's FULL convention set" rule and was
# the last Stop hook without muting (~45 words re-injected every turn).

source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
INPUT=$(cat)
taom_stop_hook_active "$INPUT" && exit 0
cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0

REMINDED=".claude/logs/.changelog-reminded"

# Check if CHANGELOG.md has been modified (staged or unstaged)
if git diff --name-only HEAD 2>/dev/null | grep -q "CHANGELOG.md"; then
  rm -f "$REMINDED" 2>/dev/null || true
  exit 0
fi

if git diff --cached --name-only 2>/dev/null | grep -q "CHANGELOG.md"; then
  rm -f "$REMINDED" 2>/dev/null || true
  exit 0
fi

# Check if any C# or XML files were modified (indicating real work was done)
CHANGED_FILES=$(git diff --name-only 2>/dev/null)
if echo "$CHANGED_FILES" | grep -qE '\.(cs|xml|xslt|json)$'; then
  if [[ ! -f "$REMINDED" ]]; then
    taom_stop_block "check-changelog-updated: tracked source files (.cs, .xml, .xslt or .json) are modified but CHANGELOG.md is unchanged in both the working tree and the index. AGENTS.md requires a CHANGELOG entry for every change session: add one before committing, or say in one line why this work needs none (for example, the files belong to another session). This fires once per streak."
    mkdir -p .claude/logs 2>/dev/null
    touch "$REMINDED" 2>/dev/null || true
  fi
else
  # No relevant work dirty: re-arm for the next streak.
  rm -f "$REMINDED" 2>/dev/null || true
fi

exit 0
