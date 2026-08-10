#!/bin/bash
# SessionStart hook: Print recent project context on fresh startup
# Only fires on "startup" source — resume/compact/clear already have context

INPUT=$(cat)

# Parse source from stdin JSON
if command -v jq >/dev/null 2>&1; then
  SOURCE=$(echo "$INPUT" | jq -r '.source // empty')
else
  SOURCE=$(echo "$INPUT" | grep -oE '"source"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
fi

# Only run on fresh startup
if [[ "$SOURCE" != "startup" ]]; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0

echo "=== TAOM Session Context ==="

# Current branch
BRANCH=$(git branch --show-current 2>/dev/null || echo "unknown")
echo "Branch: $BRANCH"

# Game-version drift check (the 1.4.5->1.4.6 Steam force-bump cost a morning of
# misattributed crashes before anyone noticed). Pin lives in .claude/pinned-game-version.txt;
# on drift, warn loudly and point at /engine-bump. Fail-open: any missing file = silence.
GAME_VERSION_XML="${BANNERLORD_GAME_DIR:-E:/Steam/steamapps/common/Mount & Blade II Bannerlord}/bin/Win64_Shipping_Client/Version.xml"
PIN_FILE=".claude/pinned-game-version.txt"
if [[ -f "$GAME_VERSION_XML" && -f "$PIN_FILE" ]]; then
  INSTALLED=$(grep -oE 'v[0-9]+\.[0-9]+\.[0-9]+' "$GAME_VERSION_XML" 2>/dev/null | head -1)
  PINNED=$(tr -d '[:space:]' < "$PIN_FILE" 2>/dev/null)
  if [[ -n "$INSTALLED" && -n "$PINNED" && "$INSTALLED" != "$PINNED" ]]; then
    echo ""
    echo "!!! GAME VERSION DRIFT: installed $INSTALLED but TAOM is pinned to $PINNED !!!"
    echo "!!! Steam likely force-updated. Run /engine-bump BEFORE trusting any test run. !!!"
  fi
fi

# Stale-worktree visibility. Parallel-agent worktrees under .claude/worktrees/ are
# auto-cleaned only when unchanged; abandoned ones with edits linger (4 forgotten trees
# cost 22 GB by 2026-07-11). Surface the count so the leak can't hide. Read-only, count
# only (no `du` — keep startup fast on a large tree). Fail-open.
WORKTREE_DIR=".claude/worktrees"
if [[ -d "$WORKTREE_DIR" ]]; then
  WT_COUNT=$(find "$WORKTREE_DIR" -mindepth 1 -maxdepth 1 -type d 2>/dev/null | wc -l | tr -d ' ')
  if [[ -n "$WT_COUNT" && "$WT_COUNT" -gt 0 ]]; then
    echo ""
    echo "Worktrees: ${WT_COUNT} lingering under $WORKTREE_DIR — if abandoned, prune (git worktree remove) to reclaim disk."
  fi
fi

# Stash visibility. `git pull --rebase` auto-stashes the WHOLE working tree, including
# files another session is mid-edit on, and an auto-stash that is never popped reverts
# that session's work to HEAD with no error and no conflict — invisible data loss
# (CLAUDE.md "Multi-session git safety"; happened 2026-08-07, and an `autostash` entry
# was still sitting unresolved two days later). `git status` does not mention stashes, so
# nothing surfaced it. Read-only, count only. Fail-open.
STASH_COUNT=$(git stash list 2>/dev/null | grep -c . || true)
if [[ -n "${STASH_COUNT:-}" && "${STASH_COUNT:-0}" -gt 0 ]]; then
  echo ""
  echo "Stashes: ${STASH_COUNT} present — an unpopped auto-stash is silent data loss, not a to-do."
  git stash list 2>/dev/null | head -3 | sed 's/^/  /'
  echo "  Prove redundant before dropping: git show stash@{0}:<f> emits LF, so diff --strip-trailing-cr."
fi

# Last 5 commits
echo ""
echo "Recent commits:"
git log --oneline -5 2>/dev/null || echo "  (no commits)"

# Latest CHANGELOG entry (date + feature titles only)
echo ""
echo "Latest CHANGELOG:"
if [[ -f CHANGELOG.md ]]; then
  awk '/^## [0-9]/{if(found) exit; found=1; print; next} found && /^### /{print}' CHANGELOG.md
else
  echo "  (no CHANGELOG.md)"
fi

# Uncommitted changes count
STAGED=$(git diff --cached --name-only 2>/dev/null | wc -l | tr -d ' ')
UNSTAGED=$(git diff --name-only 2>/dev/null | wc -l | tr -d ' ')
UNTRACKED=$(git ls-files --others --exclude-standard 2>/dev/null | wc -l | tr -d ' ')
echo ""
echo "Uncommitted: ${STAGED} staged, ${UNSTAGED} unstaged, ${UNTRACKED} untracked"

# TODO/FIXME count in Main/
if [[ -d Main ]]; then
  TODO_COUNT=$(grep -rE 'TODO|FIXME' Main/ --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')
  echo "TODO/FIXME in Main/: ${TODO_COUNT}"
fi

echo "==========================="
exit 0
