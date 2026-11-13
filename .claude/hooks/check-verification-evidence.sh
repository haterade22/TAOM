#!/bin/bash
# Stop hook: remind to verify when C# source changed but no build/test has run
# since the last source edit. Enforces the "verification before done" half of
# .claude/rules/evidence-over-claims.md.
#
# Why it stays: with no CI, this is the only automatic nudge before a done claim.
#
# Channel: one JSON {"decision":"block","reason":...} on stdout per unbuilt streak, through
# _stop_reminder.sh, the Stop channel TAOM uses (exit-0 stderr goes to the debug log;
# until plan 011 this hook wrote there and nothing arrived). Silent when stop_hook_active is
# true, which only withholds the reminder: the marker still clears when a build ran during that
# continuation, or the next streak would stay muted. Always exits 0, and any internal failure
# exits 0 with no output (fail open).
#
# Two paths, chosen by the payload's session_id (#1138; the ledger is described in
# _session_ledger.sh):
#
# 1. Session id present (the normal case): judge the session, not the tree. Another session's
#    dirty C# once made 16 of 32 Stop blocks in a week (docs/reviews/harness-retro-2026-11-10.md
#    P1). Two checks, in order:
#    a. The session's own edits. The candidates are the distinct *.cs paths (case folded) in
#       .claude/logs/sessions/<id>.edits, which record-session-edit.sh writes, newest first. One
#       counts when the file exists, is newer than <id>.verified (which mark-verification-run.sh
#       touches; a missing marker counts every file), and git status in its own folder still
#       lists it, so a reverted edit stays silent. -nt is checked first and git runs only for
#       those, at most 50 times, inside the 10 s registration. The reason names up to three.
#    b. Only when (a) finds nothing: C# no ledger holds. The recorder sees only Edit and Write,
#       so C# changed through Bash or PowerShell (sed -i, git checkout --theirs, a heredoc, a
#       generator) never reaches a ledger. A dirty *.cs in the payload cwd's work tree (as in
#       path 2) counts when it is newer than <id>.verified (before the session's first build:
#       newer than .verification-ran, the pre-#1138 rule) and no other session's ledger holds
#       it, in this checkout's ledger folder or the work tree's own. An edit made outside any
#       session (an IDE edit) counts too, as it did before #1138; a session that has built is
#       reminded about such a file newer than its own build even after another session's build.
#       Limits: a claim has no age, so a file another session edited in the last 7 days stays
#       exempt after this session changes it through a shell; a session launched in a third
#       checkout is not seen. The reason names up to three files.
#    Streak markers: <id>.reminded for (a), <id>.tree.reminded for (b), so a dismissed (b)
#    reminder never mutes the session's own next edit.
#
# 2. No session id (fallback, keeps the reminder alive): the tree-wide check. Which tree: the
#    payload's cwd, when it resolves to a work tree (a session that entered a worktree edits there,
#    while CLAUDE_PROJECT_DIR stays at the main checkout); otherwise the project. A dirty *.cs file
#    NEWER than .claude/logs/.verification-ran (touched by mark-verification-run.sh when dotnet
#    build/test or build.ps1 runs) reminds; a missing marker makes bash "-nt" true for any existing
#    file. Streak marker: .claude/logs/.verification-reminded. The markers stay under
#    CLAUDE_PROJECT_DIR/.claude/logs by absolute path, because mark-verification-run.sh writes them
#    there.
#
# Doc/rule/XML-only sessions never match the *.cs case, so they stay silent.
#
# Muting: to avoid nagging on every Stop while the same edits stay unbuilt, the reminder fires
# ONCE per unbuilt streak. The streak marker records "already reminded"; it is cleared the moment
# there is nothing left to verify (a build ran, or the edits were reverted), which re-arms the
# reminder for the next fresh edit.

source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
INPUT=$(cat)
# Anchor to the project, not the inherited cwd: mark-verification-run.sh writes its marker
# under CLAUDE_PROJECT_DIR, and tools/test_hooks.sh runs this hook against sandboxes.
cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0

LOGS="$(pwd)/.claude/logs"
MARKER="$LOGS/.verification-ran"
REMINDED="$LOGS/.verification-reminded"
BUILD="run dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= or dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= and read the output (.claude/rules/evidence-over-claims.md); a subagent's report does not count"

# $1 streak marker, $2 reason: block once per streak. The marker's folder is made on first use.
remind_once() {
  [[ -f "$1" ]] && return 0
  taom_stop_hook_active "$INPUT" && return 0
  taom_stop_block "$2 This fires once per unbuilt streak."
  mkdir -p "${1%/*}" 2>/dev/null
  touch "$1" 2>/dev/null || true
}

# cd into the work tree the payload's cwd names, when it resolves to one. The cwd is read from the
# raw payload (no Python on every turn): a JSON string, so a Windows path arrives with doubled
# backslashes. An escaped copy of the key inside last_assistant_message cannot match, because the
# closing quote must follow the key.
enter_payload_tree() {
  local re='"cwd"[[:space:]]*:[[:space:]]*"(([^"\\]|\\.)*)"' sl=/ cwd tree
  [[ "$INPUT" =~ $re ]] || return 0
  cwd=${BASH_REMATCH[1]}
  cwd=${cwd//\\\\/$sl}
  cwd=${cwd//\\\//$sl}
  tree=$(git -C "$cwd" rev-parse --show-toplevel 2>/dev/null)
  [[ -n "$tree" ]] && { cd "$tree" 2>/dev/null || true; }
}

# DIRTY_CS: the *.cs paths, relative to the current work tree's top, that git lists as changed,
# staged or untracked and that exist (a staged deletion is gone from disk). Split into an array
# under set -f, never a here-string: Git Bash 5.3 hangs forever on one of 65,536 to 65,663 bytes
# of text (#681).
list_dirty_cs() {
  local all f list
  all="$(git diff --name-only 2>/dev/null)"$'\n'"$(git diff --cached --name-only 2>/dev/null)"$'\n'"$(git ls-files --others --exclude-standard 2>/dev/null)"
  set -f; IFS=$'\n'; list=($all); IFS=$' \t\n'; set +f
  DIRTY_CS=()
  for f in "${list[@]}"; do
    [[ "$f" == *.cs && -f "$f" ]] && DIRTY_CS+=("$f")
  done
}

# Path 1: the session's own edits. A missing helper leaves TAOM_SID empty: the fallback runs.
TAOM_SID=""
source "$(dirname "${BASH_SOURCE[0]}")/_session_ledger.sh" 2>/dev/null && taom_session_id "$INPUT"
if [[ -n "$TAOM_SID" ]]; then
  taom_ledger_dir
  S_EDITS="$TAOM_LEDGER_DIR/$TAOM_SID.edits"
  S_VERIFIED="$TAOM_LEDGER_DIR/$TAOM_SID.verified"
  S_REMINDED="$TAOM_LEDGER_DIR/$TAOM_SID.reminded"
  # Muted already: one hit settles it. Otherwise name up to three.
  WANT=3; [[ -f "$S_REMINDED" ]] && WANT=1
  FOUND=()
  if [[ -f "$S_EDITS" ]]; then
    # Distinct *.cs paths, case folded, newest edit first; the stored case is kept for the -f and git tests.
    CANDS=$(awk -F '\t' '{ a[NR] = $2 } END { for (i = NR; i >= 1; i--) { l = tolower(a[i]); if (l ~ /\.cs$/ && !(l in s)) { s[l] = 1; print a[i] } } }' "$S_EDITS" 2>/dev/null)
    set -f; IFS=$'\n'; CAND_LIST=($CANDS); IFS=$' \t\n'; set +f
    GIT_CALLS=0
    for f in "${CAND_LIST[@]}"; do
      [[ -f "$f" && "$f" -nt "$S_VERIFIED" ]] || continue
      (( GIT_CALLS >= 50 )) && break
      GIT_CALLS=$((GIT_CALLS + 1))
      d=${f%/*}; [[ -n "$d" ]] || d=/
      # Two pathspecs: icase, because the ledger keeps the case the tool used and a pathspec is
      # case-sensitive; literal too, because git 2.55 matches no icase pathspec inside an
      # untracked folder two or more levels deep (a new feature's Main/Features/X/Domain/).
      [[ -n "$(git -C "$d" status --porcelain -- ":(literal)${f##*/}" ":(icase,literal)${f##*/}" 2>/dev/null)" ]] || continue
      FOUND+=("$f")
      (( ${#FOUND[@]} >= WANT )) && break
    done
  fi
  if (( ${#FOUND[@]} > 0 )); then
    NAMES=""
    for f in "${FOUND[@]}"; do NAMES+="${NAMES:+, }$f"; done
    remind_once "$S_REMINDED" "check-verification-evidence: this session edited C# after its last recorded build or test ($NAMES). Before calling the work done, $BUILD. If you are not claiming the work is done, say so in one line."
    exit 0
  fi
  # The own streak is over.
  rm -f "$S_REMINDED" 2>/dev/null || true

  # 1b: C# no ledger holds, with its own streak marker, so a dismissed reminder about such a file
  # never mutes the session's own next edit. Before the session's first build the baseline is the
  # last build of any session (.verification-ran, the pre-#1138 rule), so an old dirty file is
  # silent. The comparison folds case and a Git Bash /e/ to e:/ on both sides; ledger and git
  # paths already use / only.
  S_TREE_REMINDED="$TAOM_LEDGER_DIR/$TAOM_SID.tree.reminded"
  REF=$S_VERIFIED; [[ -f "$REF" ]] || REF=$MARKER
  WANT=3; [[ -f "$S_TREE_REMINDED" ]] && WANT=1
  enter_payload_tree
  list_dirty_cs
  UNCLAIMED=()
  NEWER=""
  for f in "${DIRTY_CS[@]}"; do
    [[ "$f" -nt "$REF" ]] && NEWER+="$f"$'\n'
  done
  if [[ -n "$NEWER" ]]; then
    TOP=$(git rev-parse --show-toplevel 2>/dev/null)
    # Claims: every other session's ledger in this checkout's folder and in the work tree's own
    # (a session launched in that worktree keeps its ledger there). The own ledger is skipped by
    # name, since git and CLAUDE_PROJECT_DIR can spell one folder two ways; a folder read twice is
    # harmless, because claims are a set.
    DIRS=("$TAOM_LEDGER_DIR")
    [[ -n "$TOP" ]] && DIRS+=("$TOP/.claude/logs/sessions")
    OTHERS=()
    for dd in "${DIRS[@]}"; do
      for l in "$dd"/*.edits; do
        [[ -f "$l" && "${l##*/}" != "$TAOM_SID.edits" ]] && OTHERS+=("$l")
      done
    done
    # The candidates go through ENVIRON, which awk takes verbatim (-v would process escapes). A
    # file that two git listings report (changed and staged) is named once.
    LEFT=$(TAOM_TOP="$TOP" TAOM_NEWER="$NEWER" awk -F '\t' -v want="$WANT" '
      function fold(p) { p = tolower(p); if (p ~ /^\/[a-z]\//) p = substr(p, 2, 1) ":" substr(p, 3); return p }
      { claimed[fold($2)] = 1 }
      END {
        top = ENVIRON["TAOM_TOP"]; n = split(ENVIRON["TAOM_NEWER"], c, "\n"); shown = 0
        for (i = 1; i <= n && shown < want; i++)
          if (c[i] != "" && !(c[i] in done) && !(fold(top "/" c[i]) in claimed)) { done[c[i]] = 1; print c[i]; shown++ }
      }' /dev/null "${OTHERS[@]}" 2>/dev/null)
    set -f; IFS=$'\n'; UNCLAIMED=($LEFT); IFS=$' \t\n'; set +f
  fi
  if (( ${#UNCLAIMED[@]} > 0 )); then
    NAMES=""
    for f in "${UNCLAIMED[@]}"; do NAMES+="${NAMES:+, }$f"; done
    remind_once "$S_TREE_REMINDED" "check-verification-evidence: C# in this tree changed after the last recorded build or test, and no session's ledger holds it ($NAMES), so it changed outside Edit and Write (a shell command, a merge, a generator or an editor). Before calling the work done, $BUILD. If these files are not this session's, or you are not claiming the work is done, say so in one line."
  else
    rm -f "$S_TREE_REMINDED" 2>/dev/null || true
  fi
  exit 0
fi

# Path 2: no session id, the tree-wide check.
enter_payload_tree
list_dirty_cs
NEEDS_REMINDER=0
for f in "${DIRTY_CS[@]}"; do
  # Dirty C# file edited more recently than the last verification run?
  if [[ "$f" -nt "$MARKER" ]]; then
    NEEDS_REMINDER=1
    break
  fi
done

if [[ $NEEDS_REMINDER -eq 1 ]]; then
  remind_once "$REMINDED" "check-verification-evidence: a C# file in this tree changed after the last recorded build or test (nothing newer in .claude/logs/.verification-ran). Before calling the work done, $BUILD. If the changed files belong to another session, or you are not claiming the work is done, say so in one line."
else
  # Nothing left to verify (built since the edit, or edits reverted): re-arm the
  # reminder for the next fresh edit.
  rm -f "$REMINDED" 2>/dev/null || true
fi

exit 0
