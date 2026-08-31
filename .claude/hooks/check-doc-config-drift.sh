#!/usr/bin/env bash
# check-doc-config-drift.sh
# PreToolUse(Bash) hook: when `git commit` is about to run touching a feature doc,
# a shipped ModuleData JSON config, or a game-version marker, run the doc-linter's
# drift checks and BLOCK the commit if they find:
#   - config-example drift: a docs/features/*.md ```json example whose values disagree
#     with the shipped Main/_Module/ModuleData/**/*.json config it mirrors, or a doc
#     key absent from the shipped file (renamed/removed).
#   - version mismatch: CLAUDE.md's "Target: Bannerlord X" line or an API-snapshot
#     header that disagrees with .claude/pinned-game-version.txt.
#
# Why: this is the enforcement for the v1.4.7-bump deep-review finding -- flipping
# banner_color_config.json's EnableLayerLimitTranspiler default left the feature doc's
# example showing the old value, invisibly (docs aren't compiled/tested). Makes the
# whole "documented config/version drifted out of sync" class a hard gate instead of a
# thing someone has to remember to grep for. See tools/lint_docs.py check_config_example_drift
# / check_version_consistency + docs/reviews/rca-v1.4.7-bump-2026-07-08.md.
#
# Fail-open (per .claude/rules/harness-facts.md "TAOM hooks MUST fail open"):
# ANY hook-internal failure -- no python, linter crash, nothing relevant staged --
# ALLOWS the commit. Only a genuine drift finding (linter rc=1 on --fail-on-drift) blocks.
#
# Returns: {} to allow, {"permissionDecision":"deny","message":"..."} to block.

set -uo pipefail

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)

# Extract the bash command from tool_input (mirrors check-moduledata-validation.sh).
COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
    print(d.get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)

# Two-stage git-commit matcher: handle `git -C/-c ... commit`; reject
# `git commit-tree` / `commit-graph`. Per .claude/rules/harness-facts.md.
case "$COMMAND" in
    *"git commit-"*) echo '{}'; exit 0 ;;
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;
    *) echo '{}'; exit 0 ;;
esac

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }

# Files in the commit. No blanket --amend skip (amend is commonly "oops, add a
# file" -- exactly the case to catch); include HEAD's files on amend.
STAGED=$(git diff --cached --name-only 2>/dev/null)
case "$COMMAND" in
    *"--amend"*)
        STAGED=$(printf '%s\n%s\n' "$STAGED" \
            "$(git show HEAD --name-only --pretty=format: 2>/dev/null)" | sort -u)
        ;;
esac

# Only run when the commit touches a surface the drift checks care about:
# a feature doc, a shipped ModuleData JSON, or a version marker.
RELEVANT=0
while IFS= read -r f; do
    case "$f" in
        docs/features/*.md) RELEVANT=1; break ;;
        Main/_Module/ModuleData/*.json) RELEVANT=1; break ;;
        .claude/pinned-game-version.txt) RELEVANT=1; break ;;
        CLAUDE.md) RELEVANT=1; break ;;
        docs/reference/taleworlds-api-snapshot/*.md) RELEVANT=1; break ;;
    esac
done <<< "$STAGED"
[[ $RELEVANT -eq 0 ]] && { echo '{}'; exit 0; }

# Locate python (fail open if absent).
PY="$PYBIN"
[[ -z "$PY" ]] && { echo '{}'; exit 0; }

# Run the drift gate. --fail-on-drift prints the full report to stdout and exits 1
# iff there is config-example drift or a version mismatch; 0 otherwise. Only rc=1 blocks.
#
# Inner bound, below the 30s registered timeout. A harness kill discards this hook's
# output, which reads as a clean pass; this gate was dead from 2026-08-31 when it ran at
# a 5s registration against a measured 7.8s runtime. Keep the overrun inside the hook.
OUT=$(timeout -k 2 20 "$PY" tools/lint_docs.py --fail-on-drift 2>/dev/null)
RC=$?

# 124 = the inner timeout fired. Not a pass. Ask rather than block: an overrun is an
# infrastructure fault, and a hook's own fault must never hard-block the user.
if [[ $RC -eq 124 ]]; then
    printf '%s\n' '{"permissionDecision":"ask","message":"[check-doc-config-drift] The doc drift gate exceeded its 20s budget and was stopped. This commit is UNCHECKED for config-example drift, version-marker mismatches and the CLAUDE.md eager-load budget. This is NOT a pass. Run: python tools/lint_docs.py --fail-on-drift"}'
    exit 0
fi

[[ $RC -ne 1 ]] && { echo '{}'; exit 0; }

# Extract just the gating sections (drift / version / CLAUDE.md budget — the report's tail), bounded.
DETAILS=$(printf '%s' "$OUT" | awk '/^## (Config-example drift|Version mismatches|CLAUDE\.md budget)/{f=1} f' | head -40)

MSG=$(printf '%s' "$DETAILS" | "$PYBIN" -c '
import sys, json
lines = [l for l in sys.stdin.read().splitlines() if l.strip()][:36]
print(json.dumps(
    "[check-doc-config-drift] git commit BLOCKED: a documented config example / version "
    "marker drifted from the source of truth, or CLAUDE.md broke its eager-load budget "
    "(46KB file / 400-char table rows / 600-char prose lines — CLAUDE.md is an index; move "
    "detail to the linked doc). Sync the doc example to the shipped ModuleData config, the "
    "version marker to .claude/pinned-game-version.txt, or thin the CLAUDE.md row, then "
    "re-stage. Details: python tools/lint_docs.py\n\n" + "\n".join(lines)))
' 2>/dev/null)
[[ -z "$MSG" ]] && { echo '{}'; exit 0; }

printf '{"permissionDecision":"deny","message":%s}\n' "$MSG"
exit 0
