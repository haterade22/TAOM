#!/bin/bash
# SubagentStop hook: stop a subagent from ending, once per unbuilt streak, when it edited C# that is
# still dirty and newer than its own last build or test. Enforces the "verification before done" half
# of .claude/rules/evidence-over-claims.md at the subagent boundary.
#
# Why it exists: a subagent could end with edited C# and no build. check-verification-evidence.sh
# (Stop) reminds only the parent session, once, after the subagent's report is already in. The
# maintainer wants "tests when a task is done" enforced by machine where the subagent ends.
#
# What it reads (the ledger is described in _session_ledger.sh): a subagent's tool payloads carry its
# parent's session_id plus its own agent_id, so record-session-edit.sh also appends each edit to
# <session id>.<agent id>.aedits and mark-verification-run.sh also touches
# <session id>.<agent id>.averified. The candidates are the distinct *.cs paths (case folded) in the
# agent's .aedits, newest first. One counts when the file exists, is newer than the agent's .averified
# (a missing marker counts every file), and git status in its own folder still lists it, so a reverted
# edit stays silent. -nt is checked first and git runs only for those, at most 50 times, inside the
# 10 s registration. The reason names up to three. No session id or no agent id: silent.
#
# Channel: one JSON {"decision":"block","reason":...} on stdout through _stop_reminder.sh. SubagentStop
# takes the same top-level decision as Stop: the block "prevents the subagent from stopping" (hooks
# docs, verified 2026-11-18; harness-facts.md "Hook lifecycle"). Always exits 0, and any internal
# failure exits 0 with no output (fail open). Silent when the payload says stop_hook_active is true.
# The SubagentStop payload carries that field, and its agent_id is the one the subagent's tool events
# carry (read in the installed Claude Code 2.1.322 binary, 2026-11-18; harness-facts.md). The once-per-
# streak marker below bounds the block on its own, so the hook does not depend on the field.
#
# Muting: the reminder fires ONCE per unbuilt streak, so it can never loop. The streak marker
# <session id>.<agent id>.areminded records "already reminded"; while it exists the hook is silent. It
# is cleared the moment nothing counts (the agent built, or its edits were reverted), which re-arms the
# reminder for the agent's next fresh edit.
#
# Limits: C# only, because the only verification marker is a dotnet build or test (or build.ps1). C#
# changed through Bash or PowerShell never reaches the ledger, so it is not seen. An agent's .aedits
# has no line cap: it lives as long as one agent, and the recorder's 7-day sweep deletes it. The hook is inert
# until a SubagentStop registration names it in .claude/settings.json (the maintainer's to add), and
# a live proof (a real subagent stopped by it) is owed after that registration.

source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
INPUT=$(cat)
# Anchor to the project, not the inherited cwd: the ledger is under CLAUDE_PROJECT_DIR, and
# tools/test_hooks.sh runs this hook against sandboxes.
cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0

source "$(dirname "${BASH_SOURCE[0]}")/_session_ledger.sh" 2>/dev/null || exit 0
taom_session_id "$INPUT"
taom_agent_id "$INPUT"
[[ -n "$TAOM_SID" && -n "$TAOM_AID" ]] || exit 0
taom_ledger_dir
A_EDITS="$TAOM_LEDGER_DIR/$TAOM_SID.$TAOM_AID.aedits"
A_VERIFIED="$TAOM_LEDGER_DIR/$TAOM_SID.$TAOM_AID.averified"
A_REMINDED="$TAOM_LEDGER_DIR/$TAOM_SID.$TAOM_AID.areminded"

# Muted already: one hit settles it. Otherwise name up to three.
WANT=3; [[ -f "$A_REMINDED" ]] && WANT=1
FOUND=()
if [[ -f "$A_EDITS" ]]; then
  # Distinct *.cs paths, case folded, newest edit first; the stored case is kept for the -f and git tests.
  CANDS=$(awk -F '\t' '{ a[NR] = $2 } END { for (i = NR; i >= 1; i--) { l = tolower(a[i]); if (l ~ /\.cs$/ && !(l in s)) { s[l] = 1; print a[i] } } }' "$A_EDITS" 2>/dev/null)
  # Split into an array under set -f, never a here-string (#681).
  set -f; IFS=$'\n'; CAND_LIST=($CANDS); IFS=$' \t\n'; set +f
  GIT_CALLS=0
  for f in "${CAND_LIST[@]}"; do
    [[ -f "$f" && "$f" -nt "$A_VERIFIED" ]] || continue
    (( GIT_CALLS >= 50 )) && break
    GIT_CALLS=$((GIT_CALLS + 1))
    d=${f%/*}; [[ -n "$d" ]] || d=/
    # Two pathspecs, as in check-verification-evidence.sh: icase, because the ledger keeps the case
    # the tool used; literal too, because git 2.55 matches no icase pathspec inside an untracked
    # folder two or more levels deep. The two options are pinned: a status.showUntrackedFiles=no
    # config would hide a new file (no block), and a status that refreshes the index takes index.lock.
    [[ -n "$(git --no-optional-locks -C "$d" status --porcelain --untracked-files=normal -- ":(literal)${f##*/}" ":(icase,literal)${f##*/}" 2>/dev/null)" ]] || continue
    FOUND+=("$f")
    (( ${#FOUND[@]} >= WANT )) && break
  done
fi

if (( ${#FOUND[@]} == 0 )); then
  # Nothing left to verify: re-arm the reminder for the agent's next fresh edit.
  rm -f "$A_REMINDED" 2>/dev/null || true
  exit 0
fi
[[ -f "$A_REMINDED" ]] && exit 0
taom_stop_hook_active "$INPUT" && exit 0
NAMES=""
for f in "${FOUND[@]}"; do NAMES+="${NAMES:+, }$f"; done
taom_stop_block "check-subagent-verification: this subagent edited C# after its last recorded build or test ($NAMES). Before you end, run dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= or dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= in your own work tree, and put the real output in your report. If you are not claiming the work is done, say so in one line. This fires once per unbuilt streak."
mkdir -p "$TAOM_LEDGER_DIR" 2>/dev/null
touch "$A_REMINDED" 2>/dev/null || true
exit 0
