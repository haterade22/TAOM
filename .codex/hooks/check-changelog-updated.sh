#!/bin/bash
# Stop hook: Warn if CHANGELOG.md hasn't been modified in this session
# This is a soft reminder, not a hard block

# Check if CHANGELOG.md has been modified (staged or unstaged)
if git diff --name-only HEAD 2>/dev/null | grep -q "CHANGELOG.md"; then
  exit 0
fi

if git diff --cached --name-only 2>/dev/null | grep -q "CHANGELOG.md"; then
  exit 0
fi

# Check if any C# or XML files were modified (indicating real work was done)
CHANGED_FILES=$(git diff --name-only 2>/dev/null)
if echo "$CHANGED_FILES" | grep -qE '\.(cs|xml|xslt|json)$'; then
  echo "REMINDER: CHANGELOG.md has not been updated this session. Project rules require updating CHANGELOG.md after every change session." >&2
fi

exit 0
