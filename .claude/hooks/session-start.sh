#!/bin/bash
# SessionStart hook: Print recent project context on fresh startup
# Startup prints the full context; compact prints only recent Dead ends (resume/clear print nothing)

INPUT=$(cat)

# Parse source from stdin JSON
if command -v jq >/dev/null 2>&1; then
  SOURCE=$(echo "$INPUT" | jq -r '.source // empty')
else
  SOURCE=$(echo "$INPUT" | grep -oE '"source"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
fi

# After compaction, re-surface recent Dead ends (run-protocol.md). SessionStart is
# the only compact-time hook whose stdout reaches Claude (harness-facts.md "Visibility"). One find
# bounded at 3 s of the 10 s registration, run folders and memory cards edited in the last two days,
# at most 40 lines; a heading "## Dead ends" or a bold "**Dead ends:**" line opens a section.
if [[ "$SOURCE" == compact ]]; then
  cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0
  DE=$(timeout -k 1 3 find plans/_audit "$HOME"/.claude/projects/*"$(basename "$PWD")"*/memory -maxdepth 2 \
       -type f \( -name PROGRESS.md -o -path '*/features/*.md' \) -mmin -2880 \
       -exec awk 'FNR==1{s=0} /^## /{s=/^## Dead ends/; next} /^\*\*[^*]+\*\*/{s=/^\*\*Dead ends/} s&&NF{print FILENAME": "$0; if(++n>=40) exit}' {} + 2>/dev/null)
  [[ -n "$DE" ]] && printf 'Dead ends recorded in the last two days (do not retry these):\n%s\n' "$DE"
  exit 0
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

# Hook-toolchain health. Every gate in this repo parses its stdin JSON, so if no safe
# interpreter resolves, every gate quietly turns off and looks exactly like a clean run.
# Announce it rather than degrade in silence (hook-authoring.md, "fail open but NEVER
# fail silent"). On 2026-08-31 the opposite failure cost ~20 minutes per Bash call:
# `python3` resolved to a Microsoft Store App Execution Alias that never exits.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh" 2>/dev/null || PYBIN=""
# Two separate conditions, deliberately NOT ANDed. The gates with no jq path
# (check-bash-length, check-claude-files-tracked, check-commit-subject-version, check-doc-config-drift,
# check-moduledata-validation, check-native-dll-crt) call "$PYBIN" unconditionally, so they
# die on a missing python whether jq is present or not. ANDing the two conditions hid
# exactly that case. tools/test_hooks.sh 5b2 fails when a python-only gate's file name is
# missing from the echo lines below (it ignores comments, this one included).
if [[ -z "${PYBIN:-}" ]]; then
    echo ""
    echo "!!! HOOK TOOLCHAIN DEGRADED: no safe python resolved. !!!"
    echo "    The python-only gates are failing OPEN right now: check-bash-length,"
    echo "    check-claude-files-tracked, check-commit-subject-version, check-doc-config-drift,"
    echo "    check-moduledata-validation, check-native-dll-crt."
    echo "    validate-push cannot judge a push: it asks on any force marker and allows the rest."
    if ! command -v jq >/dev/null 2>&1; then
        echo "    jq is absent too, so every other JSON-parsing gate is open."
    fi
    echo "    Check that a real python precedes the WindowsApps aliases on PATH."
    echo "    Diagnose: bash tools/test_hooks.sh"
fi
# A WindowsApps interpreter winning the PATH race is the trap itself, not just a nuisance.
_wa=$(command -v python 2>/dev/null || true)
case "$_wa" in
    *[Ww]indows[Aa]pps*)
        echo ""
        echo "!!! WARNING: 'python' resolves to a Microsoft Store alias ($_wa)."
        echo "    That alias hangs forever from Git Bash. Hooks are guarded, but any"
        echo "    tools/*.py call you make by hand will wedge. Disable the alias in"
        echo "    Settings > Apps > Advanced app settings > App execution aliases."
        ;;
esac
unset _wa

# Game-version drift check (the 1.4.5->1.4.6 Steam force-bump cost a morning of
# misattributed crashes before anyone noticed). Pin lives in .claude/pinned-game-version.txt;
# on drift, warn loudly and point at /engine-bump.
#
# 2026-08-10: this check silently produced NOTHING on the v1.4.7->v1.4.8 bump, the exact event it
# exists to catch. `${BANNERLORD_GAME_DIR:-<literal>}` only substitutes the literal when the var is
# unset or empty -- a var that is SET but does not resolve in the hook's environment took the -f
# test straight to false, and the whole block fell through without a word. settings.json does not
# define BANNERLORD_GAME_DIR, so the hook inherits whatever the harness process happens to carry.
# Fix: try the env path, then ALWAYS fall back to the known install, and if neither resolves say so
# out loud. Still fail-open (never blocks) -- but no longer fail-SILENT, because silence here reads
# as "no drift" when it actually means "never checked".
DEFAULT_GAME_DIR="E:/Steam/steamapps/common/Mount & Blade II Bannerlord"
GAME_VERSION_XML=""
for candidate in "${BANNERLORD_GAME_DIR}" "$DEFAULT_GAME_DIR"; do
  [[ -z "$candidate" ]] && continue
  if [[ -f "${candidate}/bin/Win64_Shipping_Client/Version.xml" ]]; then
    GAME_VERSION_XML="${candidate}/bin/Win64_Shipping_Client/Version.xml"
    break
  fi
done
PIN_FILE=".claude/pinned-game-version.txt"
if [[ -z "$GAME_VERSION_XML" ]]; then
  echo ""
  echo "NOTE: could not read the installed game version (no Version.xml under BANNERLORD_GAME_DIR"
  echo "      or $DEFAULT_GAME_DIR) — engine drift is UNCHECKED this session, not absent."
elif [[ -f "$PIN_FILE" ]]; then
  INSTALLED=$(grep -oE 'v[0-9]+\.[0-9]+\.[0-9]+' "$GAME_VERSION_XML" 2>/dev/null | head -1)
  PINNED=$(tr -d '[:space:]' < "$PIN_FILE" 2>/dev/null)
  if [[ -n "$INSTALLED" && -n "$PINNED" && "$INSTALLED" != "$PINNED" ]]; then
    echo ""
    echo "!!! GAME VERSION DRIFT: installed $INSTALLED but TAOM is pinned to $PINNED !!!"
    echo "!!! Steam likely force-updated. Run /engine-bump BEFORE trusting any test run. !!!"
  fi
fi

# Worktree pile-up banner (ported from pstack). Each stale worktree costs disk and confuses a later
# session, so past 15 name the audit tool. One bounded git call; pruning is the maintainer's call.
WT_MAX=15
WT_LIST=$(timeout -k 0.5 2 git worktree list --porcelain 2>/dev/null)
WT_RC=$?
if [[ $WT_RC -ne 0 ]]; then
  echo ""
  echo "NOTE: worktree count UNCHECKED this session (git worktree list exited $WT_RC)."
else
  WT_N=$(printf '%s\n' "$WT_LIST" | grep -c '^worktree ' || true)
  if [[ "${WT_N:-0}" -gt $WT_MAX ]]; then
    echo ""
    echo "WORKTREES: ${WT_N} checked out (more than ${WT_MAX}). Run python tools/worktree_audit.py; prune only on Mike's word."
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
    echo "Worktrees: ${WT_COUNT} lingering under $WORKTREE_DIR. Run python tools/worktree_audit.py; prune only on Mike's word."
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

# Untagged module version. SubModule.xml's <Version> is what IdentityCollector stamps into every
# crash bundle as TaomVersion, so an untagged version cannot be resolved back to a commit -- that is
# how v2.0.11/.12/.14/.16/.17 became permanently unresolvable, with two crash reports citing v2.0.12.
#
# check-version-tagged.sh (Stop) already catches the bump itself, but it mutes per-VERSION via an
# on-disk marker, so it warns exactly once and then stays silent across every later session. On
# 2026-08-09 it fired 60s after the v2.0.20 bump, wrote .claude/logs/.version-tag-reminded, and went
# quiet for 33 commits while the tag was never created. Same invisible-across-sessions shape as the
# unpopped auto-stash above, so it gets the same treatment: re-asserted every startup, never muted.
#
# Read-only, no network (git ls-remote at startup would be slow and fail offline), fail-open. The
# push half is named in the message instead, because `git push` does NOT push tags.
SUBMODULE_XML="Main/_Module/SubModule.xml"
if [[ -f "$SUBMODULE_XML" ]]; then
  # Anchored on `<Version value=` so DependedModuleMetadata's version="..." cannot match.
  # Same expression as check-version-tagged.sh, so the two can never disagree about the version.
  MODULE_VERSION=$(grep -o '<Version value="[^"]*"' "$SUBMODULE_XML" 2>/dev/null \
                   | head -1 | sed 's/.*"\(.*\)"/\1/')
  if [[ -z "$MODULE_VERSION" ]]; then
    # Fail LOUD, not silent: no output here would read as "version is tagged".
    echo ""
    echo "NOTE: could not read <Version> from $SUBMODULE_XML -- release tagging is UNCHECKED this session, not clean."
  elif ! git rev-parse -q --verify "refs/tags/${MODULE_VERSION}" >/dev/null 2>&1; then
    echo ""
    echo "Release tag: module version ${MODULE_VERSION} has NO git tag. A version with no tag cannot be"
    echo "  traced from a player's crash report. Tag the commit that actually ships, then push the tag"
    echo "  separately (git push origin ${MODULE_VERSION}); /release runs the full sequence."
  fi
fi

# Harness retro cadence (#962). /harness-retro measures the harness from the session transcripts
# (docs/ai-includes/harness-retro.md), and a measurement nobody reads is no measurement, so this asks
# for one once the newest retro record is seven days old. One glob and one `date` call, no transcript
# scan. Fail open, never silent: a record whose
# date will not parse says UNCHECKED rather than nothing.
RETRO_LAST=""
for RETRO_F in docs/reviews/harness-retro-[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9].md; do
  [[ -f "$RETRO_F" ]] && RETRO_LAST=$RETRO_F
done
if [[ -z "$RETRO_LAST" ]]; then
  echo ""
  echo "HARNESS RETRO DUE: no retro on record; run /harness-retro."
else
  RETRO_DATE=${RETRO_LAST#docs/reviews/harness-retro-}
  RETRO_DATE=${RETRO_DATE%.md}
  RETRO_EPOCH=$(date -d "$RETRO_DATE" +%s 2>/dev/null)
  printf -v RETRO_NOW '%(%s)T' -1 2>/dev/null
  if [[ -z "$RETRO_EPOCH" || -z "$RETRO_NOW" ]]; then
    echo ""
    echo "NOTE: harness retro cadence UNCHECKED this session (could not read the date of $RETRO_LAST)."
  elif (( RETRO_EPOCH > RETRO_NOW )); then
    # A typo such as 2062 would otherwise silence the banner for decades.
    echo ""
    echo "NOTE: harness retro cadence UNCHECKED this session ($RETRO_LAST is dated in the future)."
  elif (( (RETRO_NOW - RETRO_EPOCH) / 86400 >= 7 )); then
    echo ""
    echo "HARNESS RETRO DUE: last retro $RETRO_DATE ($(( (RETRO_NOW - RETRO_EPOCH) / 86400 )) days ago); run /harness-retro."
  fi
fi

# Last 5 commits
echo ""
echo "Recent commits:"
git log --oneline -5 2>/dev/null || echo "  (no commits)"

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
