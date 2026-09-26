#!/usr/bin/env bash
# check-graphify-usage.sh
# PreToolUse (Bash and PowerShell) gate: every graphify WRITE goes through
# tools/graphify_taom.py (#677). Denies a raw `graphify extract|update|cluster-only|label|
# install|hook|watch|...` and `graphify-mcp`; allows the read-only query verbs (explain,
# affected, path, god-nodes, query) and every command that only mentions graphify as data
# (a commit message, a grep pattern).
#
# Each raw write verb has a silent trap: extract leaks a graphify-out/ cache into the
# scanned repo (even with --out, on an incremental run), update discards about 5,100
# external-type nodes, cluster-only and label call an LLM, and the install verbs rewrite
# CLAUDE.md, AGENTS.md, settings.json or git hooks. The wrapper pins the flags that avoid
# each one; the reasons live in its judge_command(), which tools/tests/test_graphify_taom.py
# covers case by case.
#
# The judgement is Python (a quote-aware split per shell, prefix stripping, `bash -c` and
# `pwsh -Command` unwrapping), so this hook has no jq path: with no safe python it fails
# open and says so. Returns {} to allow, or a hookSpecificOutput deny.

set -uo pipefail

INPUT=$(cat)

# Prefilter: every decision below needs the text `graphify` in the command, and Claude Code
# never escapes an ASCII letter, so a raw payload without it cannot concern this gate.
# Match the raw text, never a token regex: a newline before the word arrives as \n.
# Never skip on an escape: a payload holding any \u takes the full parse
# (tools/test_hooks.sh 4c checks both directions).
[[ "$INPUT" == *graphify* || "$INPUT" == *'\u'* ]] || { echo '{}'; exit 0; }

# Resolve a safe Python (never a Microsoft Store alias: those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

# Fail open, but never fail silent: for a gate, no output reads as "nothing to report".
taom_pybin_degraded "check-graphify-usage" "raw graphify write commands" && { echo '{}'; exit 0; }

JUDGE="$(dirname "${BASH_SOURCE[0]}")/../../tools/graphify_taom.py"
if [[ ! -f "$JUDGE" ]]; then
  echo "[check-graphify-usage] $JUDGE is missing; raw graphify write commands NOT checked. Gate failed OPEN." >&2
  echo '{}'
  exit 0
fi

# Bounded inside the 10 s registration, so an overrun can still speak (hook-authoring.md).
OUT=$(printf '%s' "$INPUT" | timeout -k 1 5 "$PYBIN" "$JUDGE" gate 2>/dev/null)
RC=$?

if [[ $RC -eq 124 || $RC -eq 137 ]]; then
  printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"check-graphify-usage could not judge this command within 5 s. If it runs a graphify write verb (extract, update, cluster-only, label, install, hook), use python tools/graphify_taom.py refresh instead; a query verb is fine."}}'
  exit 0
fi
if [[ $RC -ne 0 || "$OUT" != \{* ]]; then
  echo "[check-graphify-usage] the judge failed (rc=$RC); raw graphify write commands NOT checked. Gate failed OPEN." >&2
  echo '{}'
  exit 0
fi

printf '%s\n' "$OUT"
exit 0
