#!/bin/bash
# SessionStart hook: Print recent project context on fresh startup
# Startup prints the full context; compact prints only recent Dead ends (resume/clear print nothing)

# The bounded probes below (the Armory tpac scan, the graphify status and its background-refresh start, the native-build archive check, the worktree list) share one deadline counted
# from this first command, so together they leave the tail (git log, diff, ls-files) only
# a short slice of the time: three 4 s probes, their kill grace and a slow tail could pass 10 s, and a
# killed hook prints nothing (Codex review 2026-10-16). EPOCHREALTIME's digits are microseconds; it is
# empty in a bash older than 5.0, and each probe then gets its full 4 s.
T0=${EPOCHREALTIME//[!0-9]/}
SS_DEADLINE_US=7500000
SS_PROBE_BUDGET_MAX_US=4000000   # the ceiling of "$SS_PROBE_BUDGET"; tools/test_hooks.sh 3 reads it
ss_probe_budget() {   # sets SS_PROBE_BUDGET (seconds) to what the next probe may spend; fails under 0.5 s
  local left=$SS_PROBE_BUDGET_MAX_US
  if [[ -n $T0 ]]; then
    left=$(( SS_DEADLINE_US - ${EPOCHREALTIME//[!0-9]/} + T0 - 500000 ))
    (( left > SS_PROBE_BUDGET_MAX_US )) && left=$SS_PROBE_BUDGET_MAX_US
  fi
  (( left >= 500000 )) || return 1
  printf -v SS_PROBE_BUDGET '%d.%06d' $(( left / 1000000 )) $(( left % 1000000 ))
}
INPUT=$(cat)

# Parse source from stdin JSON
if command -v jq >/dev/null 2>&1; then
  SOURCE=$(echo "$INPUT" | jq -r '.source // empty')
else
  SOURCE=$(echo "$INPUT" | grep -oE '"source"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
fi

# The memory folder carries the main checkout's name, also for a session in a sibling worktree.
main_checkout_name() {
  local common
  common=$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null)
  common=${common%/.git}
  basename "${common:-$PWD}"
}

# Work claims (#1248). Two sessions once ran the same fix at once: the later one could not see that
# the first already owned it. Before each task or sub-task a session claims it with
# tools/work_claims.py; this lists the live claims of the other sessions, on a fresh start, after
# a compaction, a resume or a /clear, so a session that starts later or moves on to its next task sees them. The payload's
# session_id leaves out the session's own claims. Bounded by $1 seconds; fail open, never silent.
ss_claims() {
  local budget=${1:-3} claims rc
  [[ -f tools/work_claims.py ]] || return 0
  [[ -n "${PYBIN:-}" ]] || source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh" 2>/dev/null
  echo "DECONFLICT: before each task or sub-task, run python tools/work_claims.py claim <issue or key> --note \"<what>\"; HELD (exit 3) means another session owns it: stop and ask that session or Mike. Also ask whether the result you just got back matters to the job in hand; if not, go on."
  if [[ -z "${PYBIN:-}" ]]; then
    echo "NOTE: work claims UNCHECKED this session (no safe python)."
    return 0
  fi
  TAOM_SID=""
  source "$(dirname "${BASH_SOURCE[0]}")/_session_ledger.sh" 2>/dev/null && taom_session_id "$INPUT"
  claims=$(timeout -k 0.5 "$budget" "$PYBIN" tools/work_claims.py list --others --brief ${TAOM_SID:+--session "$TAOM_SID"} 2>/dev/null)
  rc=$?
  if (( rc != 0 )); then
    echo "NOTE: work claims UNCHECKED this session (tools/work_claims.py exited $rc)."
  elif [[ -n "$claims" ]]; then
    printf '%s\n' "$claims"
  fi
}

# After compaction, re-surface recent Dead ends (run-protocol.md; CLAUDE.md item 8). SessionStart is
# the only compact-time hook whose stdout reaches Claude (harness-facts.md "Visibility"). One find
# bounded at 3 s, run folders and memory cards edited in the last two days,
# at most 40 lines; a heading "## Dead ends" or a bold "**Dead ends:**" line opens a section.
if [[ "$SOURCE" == compact ]]; then
  cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0
  DE=$(timeout -k 1 3 find plans/_audit "$HOME"/.claude/projects/*-"$(main_checkout_name)"/memory -maxdepth 2 \
       -type f \( -name PROGRESS.md -o -path '*/features/*.md' \) -mmin -2880 \
       -exec awk 'FNR==1{s=0} /^## /{s=/^## Dead ends/; next} /^\*\*[^*]+\*\*/{s=/^\*\*Dead ends/} s&&NF{print FILENAME": "$0; if(++n>=40) exit}' {} + 2>/dev/null)
  [[ -n "$DE" ]] && printf 'Dead ends recorded in the last two days (do not retry these):\n%s\n' "$DE"
  ss_claims 3
  exit 0
fi

# A /clear or a resume starts the next task, so it gets the claim list too (#1248), and nothing else.
if [[ "$SOURCE" == resume || "$SOURCE" == clear ]]; then
  cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0
  ss_claims 3
  exit 0
fi

# Only run on fresh startup
if [[ "$SOURCE" != "startup" ]]; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0

echo "=== TAOM Session Context ==="
if ss_probe_budget; then
  ss_claims "$SS_PROBE_BUDGET"
else
  echo "NOTE: work claims UNCHECKED this session (session-start time budget spent)."
fi

# No branch line and no recent-commit list here: the harness puts the branch, the status and the
# last five commits into every main session as gitStatus, and again after a compaction.

# Hook-toolchain health. Every gate in this repo parses its stdin JSON, so if no safe
# interpreter resolves, every gate quietly turns off and looks exactly like a clean run.
# Announce it rather than degrade in silence (hook-authoring.md, "fail open but NEVER
# fail silent"). On 2026-08-31 the opposite failure cost ~20 minutes per Bash call:
# `python3` resolved to a Microsoft Store App Execution Alias that never exits.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh" 2>/dev/null || PYBIN=""
# Two separate conditions, deliberately NOT ANDed. The gates with no jq path
# (check-claude-files-tracked, check-commit-subject-version, check-doc-config-drift,
# check-moduledata-validation) call "$PYBIN" unconditionally, so they
# die on a missing python whether jq is present or not. ANDing the two conditions hid
# exactly that case. tools/test_hooks.sh 5b2 fails when a python-only gate's file name is
# missing from the echo lines below (it ignores comments, this one included).
if [[ -z "${PYBIN:-}" ]]; then
    echo ""
    echo "!!! HOOK TOOLCHAIN DEGRADED: no safe python resolved. !!!"
    echo "    The python-only gates are failing OPEN right now: check-bash-length,"
    echo "    check-claude-files-tracked, check-commit-subject-version, check-doc-config-drift,"
    echo "    check-graphify-usage, check-moduledata-validation."
    echo "    validate-push cannot judge a push: it asks on any force marker and allows the rest."
    if ! command -v jq >/dev/null 2>&1; then
        echo "    jq is absent too, so every other JSON-parsing gate is open."
    fi
    echo "    Check that a real python precedes the WindowsApps aliases on PATH."
    echo "    Diagnose: bash tools/test_hooks.sh"
fi
# The tracked commit-msg hook runs only when core.hooksPath names .githooks (a clone sets it once).
# Silent when it does; a relative value reads from the project root, where this hook has cd'd.
P=$(git config core.hooksPath 2>/dev/null)
[[ -n "$P" && -f "$P/commit-msg" ]] || echo "NOTE: core.hooksPath does not name .githooks, so commit-msg is NOT running. Run: git config core.hooksPath .githooks"
# MEMORY.md regrows between sweeps and loads in every subagent. These are ADR-011's limits, the ones
# tools/context_budget_scan.sh reports, but nothing runs that scan at session start.
for MEM_F in "$HOME"/.claude/projects/*-"$(main_checkout_name)"/memory/MEMORY.md; do
    [[ -f "$MEM_F" ]] || continue
    MEM_B=$(wc -c < "$MEM_F")
    MEM_L=$(wc -l < "$MEM_F")
    if [[ $MEM_L -gt 40 || $MEM_B -gt 4096 ]]; then
        echo "NOTE: MEMORY.md is $MEM_L lines, $MEM_B bytes, over ADR-011's 40 lines and 4 KB. Move durable rules to the repo; delete pushed cards."
    fi
    break
done
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
GAME_DIR_RESOLVED=""
for candidate in "${BANNERLORD_GAME_DIR}" "$DEFAULT_GAME_DIR"; do
  [[ -z "$candidate" ]] && continue
  if [[ -f "${candidate}/bin/Win64_Shipping_Client/Version.xml" ]]; then
    GAME_VERSION_XML="${candidate}/bin/Win64_Shipping_Client/Version.xml"
    GAME_DIR_RESOLVED="$candidate"
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
    echo "!!! Steam likely force-updated. Tell the user first, then run /engine-bump BEFORE any !!!"
    echo "!!! build, test or crash attribution: a test on an unacknowledged bump proves nothing. !!!"
  fi
fi

# Armory art drift. The Armory is unversioned here and loads loose Assets/**/*.tpac, so an art
# drop synced into the install (a KEYforce pull, a "TAOM Update" mirror commit, an editor
# re-import) touches nothing the commit hook or CI can see. The 2026-09-11 drop re-exported the
# elven bows under new names, 32 item refs kept the old ones, and a missing collision body is
# the #352 infinite mission load; the elf start hung for two days (#599) because the only gate
# was a separate command nobody ran after the sync. So: any tpac newer than the committed
# catalogue means the catalogue (and every ref audit derived from it) is stale, and the audit is
# owed. One bounded `find`, first hit wins, ~0.3 s over 4,400 files. Only `*_geo.tpac` counts:
# every metamesh and physics shape in the catalogue sits in a _geo pack (4,883 of 4,883 on
# 2026-09-15), and _tex / _mtl / _anm packs are re-saved by the editor constantly. Fail-open,
# never silent: an unresolvable install or a killed find says "unchecked", because silence reads
# as "no drift".
CATALOGUE="docs/reference/armory-catalogue/catalogue.tsv"
ARMORY_ASSETS=""
[[ -n "$GAME_DIR_RESOLVED" && -d "${GAME_DIR_RESOLVED}/Modules/LOTRLOME_Armory/Assets" ]] \
  && ARMORY_ASSETS="${GAME_DIR_RESOLVED}/Modules/LOTRLOME_Armory/Assets"
if [[ -z "$ARMORY_ASSETS" || ! -f "$CATALOGUE" ]]; then
  echo ""
  echo "NOTE: Armory art drift UNCHECKED this session (no LOTRLOME_Armory/Assets under the install, or no $CATALOGUE)."
elif ! ss_probe_budget; then
  echo ""
  echo "NOTE: Armory art drift UNCHECKED this session (session-start time budget spent)."
else
  NEWER_TPAC=$(timeout -k 0.5 "$SS_PROBE_BUDGET" find "$ARMORY_ASSETS" -name '*_geo.tpac' -newer "$CATALOGUE" -print -quit 2>/dev/null)
  FIND_RC=$?
  if [[ $FIND_RC -eq 124 || $FIND_RC -eq 137 ]]; then
    echo ""
    echo "NOTE: Armory art drift UNCHECKED this session (the tpac scan overran ${SS_PROBE_BUDGET%????} s)."
  elif [[ -n "$NEWER_TPAC" ]]; then
    echo ""
    echo "!!! ARMORY ART DRIFT: a tpac is newer than the committed catalogue !!!"
    echo "!!!   ${NEWER_TPAC#$ARMORY_ASSETS/}"
    echo "!!!   Run /armory-audit (python tools/audit_armory_refs.py --regen-catalogue) BEFORE any battle or"
    echo "!!!   tournament smoke, and repoint every MISSING_BODY row it names. A missing collision body"
    echo "!!!   is the #352 infinite load (#599)."
  fi
fi

# graphify code-graph freshness (#677). The graph lives outside the repo and each workflow step
# that reads it runs `refresh --if-stale` first, but a session that queries it ad hoc would
# answer from old code without knowing. One bounded status call (about 0.3 s); fail open, never
# silent, so an overrun or a missing interpreter says "unchecked" rather than nothing.
if [[ -n "${PYBIN:-}" && -f tools/graphify_taom.py ]] && ! ss_probe_budget; then
  echo ""
  echo "NOTE: graphify code graph freshness UNCHECKED this session (session-start time budget spent)."
elif [[ -n "${PYBIN:-}" && -f tools/graphify_taom.py ]]; then
  GRAPH_STATUS=$(timeout -k 0.5 "$SS_PROBE_BUDGET" "$PYBIN" tools/graphify_taom.py status --brief 2>/dev/null)
  GRAPH_RC=$?
  # A stale graph refreshes itself in a detached child (#995): one day of merges left it 645 files
  # behind, and every workflow step then paid the refresh or answered from old code. The wrapper
  # returns at once and prints why it did or did not start (opt-out, lock, Bannerlord running).
  # It runs before the status is printed, so that the status can drop its "refresh:" hint when the
  # refresh is already running: the hint told nearly every session to start a second one.
  GRAPH_AUTO_LINE=""
  if [[ $GRAPH_RC -eq 1 ]]; then
    if ss_probe_budget; then
      GRAPH_AUTO=$(timeout -k 0.5 "$SS_PROBE_BUDGET" "$PYBIN" tools/graphify_taom.py refresh --if-stale --detach 2>/dev/null)
      GRAPH_AUTO_RC=$?
      if [[ $GRAPH_AUTO_RC -eq 124 || $GRAPH_AUTO_RC -eq 137 || -z "$GRAPH_AUTO" ]]; then
        GRAPH_AUTO_LINE="NOTE: graphify background refresh UNCHECKED this session (the start overran ${SS_PROBE_BUDGET%????} s or printed nothing)."
      else
        GRAPH_AUTO_LINE="$GRAPH_AUTO"
      fi
    else
      GRAPH_AUTO_LINE="NOTE: graphify background refresh not started (session-start time budget spent)."
    fi
  fi
  echo ""
  if [[ $GRAPH_RC -eq 124 || $GRAPH_RC -eq 137 || -z "$GRAPH_STATUS" ]]; then
    echo "NOTE: graphify code graph freshness UNCHECKED this session (status overran ${SS_PROBE_BUDGET%????} s or printed nothing)."
  elif [[ "$GRAPH_AUTO_LINE" == *"background refresh started"* ]]; then
    # Split on newlines under set -f, never a here-string or a process substitution (hook-authoring.md).
    set -f; IFS=$'\n'; GRAPH_LINES=($GRAPH_STATUS); IFS=$' \t\n'; set +f
    for GRAPH_LINE in "${GRAPH_LINES[@]}"; do
      [[ "$GRAPH_LINE" == *"refresh: python tools/graphify_taom.py refresh"* ]] || printf '%s\n' "$GRAPH_LINE"
    done
  else
    printf '%s\n' "$GRAPH_STATUS"
  fi
  [[ -n "$GRAPH_AUTO_LINE" ]] && printf '%s\n' "$GRAPH_AUTO_LINE"
else
  echo ""
  echo "NOTE: graphify code graph freshness UNCHECKED this session (no safe python or no tools/graphify_taom.py)."
fi

# Native engine build archive (#1004). A Steam update replaces TaleWorlds.Native.dll, and
# `native_build_match.py --old` and crash triage against the previous build need its binary. The
# check is stat-only (about 0.1 s) and silent when both builds are archived; a changed or new DLL is
# archived by a detached child that this call does not wait for. It prints nothing when the install
# is missing (the drift check above already notes that). Fail open, never silent: a spent budget or
# an overrun says "unchecked".
if [[ -n "${PYBIN:-}" && -f tools/native_archive.py && -n "$GAME_DIR_RESOLVED" ]]; then
  if ! ss_probe_budget; then
    echo ""
    echo "NOTE: native engine build archive UNCHECKED this session (session-start time budget spent)."
  else
    NATIVE_ARCH=$(timeout -k 0.5 "$SS_PROBE_BUDGET" "$PYBIN" tools/native_archive.py --detach --game-dir "$GAME_DIR_RESOLVED" 2>/dev/null)
    NATIVE_ARCH_RC=$?
    if [[ $NATIVE_ARCH_RC -eq 124 || $NATIVE_ARCH_RC -eq 137 ]]; then
      echo ""
      echo "NOTE: native engine build archive UNCHECKED this session (the check overran ${SS_PROBE_BUDGET%????} s)."
    elif [[ -n "$NATIVE_ARCH" ]]; then
      echo ""
      printf '%s\n' "$NATIVE_ARCH"
    fi
  fi
fi

# Worktree pile-up. Every checked-out worktree (any path, not only .claude/worktrees/) costs a
# full copy of the tree on E:, and nothing else counts them. One bounded `git worktree list`
# (about 0.05 s); more than 15 prints the audit command. Pruning stays the maintainer's call.
# Fail open, never silent: a spent budget, an overrun or a failed call says "unchecked".
WT_MAX=15
if ! ss_probe_budget; then
  echo ""
  echo "NOTE: worktree count UNCHECKED this session (session-start time budget spent)."
else
  WT_LIST=$(timeout -k 0.5 "$SS_PROBE_BUDGET" git worktree list --porcelain 2>/dev/null)
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
# (docs/ai-includes/git-and-commits.md; happened 2026-08-07, and an `autostash` entry
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
# Same invisible-across-sessions shape as the unpopped auto-stash above, so the check is
# re-asserted every startup, never muted.
#
# Read-only, no network (git ls-remote at startup would be slow and fail offline), fail-open. The
# push half is named in the message instead, because `git push` does NOT push tags.
SUBMODULE_XML="Main/_Module/SubModule.xml"
if [[ -f "$SUBMODULE_XML" ]]; then
  # Anchored on `<Version value=` so DependedModuleMetadata's version="..." cannot match.
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
# scan, so it costs nothing of the probes' shared deadline. Fail open, never silent: a record whose
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
    echo "HARNESS RETRO DUE: last retro $RETRO_DATE ($(( (RETRO_NOW - RETRO_EPOCH) / 86400 )) days ago). Tell Mike once in your first reply and ask whether to run /harness-retro."
  fi
fi

# Uncommitted changes count
STAGED=$(git diff --cached --name-only 2>/dev/null | wc -l | tr -d ' ')
UNSTAGED=$(git diff --name-only 2>/dev/null | wc -l | tr -d ' ')
UNTRACKED=$(git ls-files --others --exclude-standard 2>/dev/null | wc -l | tr -d ' ')
echo ""
echo "Uncommitted: ${STAGED} staged, ${UNSTAGED} unstaged, ${UNTRACKED} untracked"

# Upstream review drift is a SessionStart hook of its own, upstream-watch.sh (2026-10-16): its network check gets a
# registration of its own instead of what this hook's other checks leave.

echo "==========================="
exit 0
