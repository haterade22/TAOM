#!/usr/bin/env bash
# check-doc-config-drift.sh
# PreToolUse (Bash and PowerShell) hook: when `git commit` is about to run touching a feature doc,
# a shipped ModuleData JSON config, a game-version marker or an RCA, run the doc-linter's
# drift checks and BLOCK the commit if they find:
#   - config-example drift: a docs/features/*.md ```json example whose values disagree
#     with the shipped Main/_Module/ModuleData/**/*.json config it mirrors, or a doc
#     key absent from the shipped file (renamed/removed).
#   - version mismatch: CLAUDE.md's "Target: Bannerlord X" line or an API-snapshot
#     header that disagrees with .claude/pinned-game-version.txt.
#   - a new FOLLOW-UP line in a staged docs/reviews/rca-*.md with no issue and no reason (#984).
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
# ALLOWS the commit. Only a genuine drift finding (linter rc=1 on --drift-only) blocks.
#
# Returns: {} to allow, {"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"..."}} to block.

set -uo pipefail

INPUT=$(cat)

# Prefilter: every decision below needs the word `commit` in the command (`git commit` or
# `git -C <dir> commit`), and Claude Code never escapes an ASCII letter, so a raw payload
# without the text `commit` cannot concern this gate. Exiting here skips the _pybin.sh probe
# and the parse (two Python starts) on every Bash call without the word, `git status` and
# `git log` included. Match the raw text, never a token regex: a newline before a command
# arrives as \n. tools/test_hooks.sh 4c checks both directions.
# Never skip on an escape: JSON writes a letter either literally or as a \u escape,
# so a payload holding any \u takes the full parse, and the raw test is safe
# whatever writes the payload.
[[ "$INPUT" == *commit* || "$INPUT" == *'\u'* ]] || { echo '{}'; exit 0; }

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

# Fail open, but never fail silent: for a gate, no output reads as "nothing to report".
taom_pybin_degraded "check-doc-config-drift" "doc/config drift and the CLAUDE.md budget" && { echo '{}'; exit 0; }

# The command as POSIX-shell text (plan 027): _pybin.sh taom_hook_command hands a PowerShell
# command back as the Bash text of the same command and names git `git` wherever it is the
# command (`GIT`, `git.exe`, a path), so the two-stage matcher below reads both shells.
COMMAND=$(taom_hook_command posix check-doc-config-drift)

# Two-stage git-commit matcher: handle `git -C/-c ... commit`; reject
# `git commit-tree` / `commit-graph`. Per .claude/rules/harness-facts.md.
case "$COMMAND" in
    *"git commit-"*) echo '{}'; exit 0 ;;
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;
    *) echo '{}'; exit 0 ;;
esac

# The top level of the repository the commit runs in, not always the project root (_pybin.sh
# taom_git_repo_dir). A commit in another repository is not this gate's: its tools/*.py are not
# TAOM's to run.
COMMIT_REPO=$(taom_git_repo_dir commit) || { taom_deny_multi_repo check-doc-config-drift; exit 0; }
cd "$COMMIT_REPO" 2>/dev/null || { echo '{}'; exit 0; }
taom_is_project_repo "$COMMIT_REPO" || { echo '{}'; exit 0; }

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
# a feature doc, a shipped ModuleData JSON, a version marker, or an RCA.
RELEVANT=0
# Split into an array under set -f, never a here-string: Git Bash 5.3 hangs forever on one of
# 65,536 to 65,663 bytes of text (#681), and a killed gate fails open. An empty line is dropped,
# which is harmless here.
set -f; IFS=$'\n'; STAGED_LIST=($STAGED); IFS=$' \t\n'; set +f
for f in "${STAGED_LIST[@]}"; do
    case "$f" in
        docs/features/*.md) RELEVANT=1; break ;;
        docs/reviews/rca-*.md) RELEVANT=1; break ;;
        Main/_Module/ModuleData/*.json) RELEVANT=1; break ;;
        .claude/pinned-game-version.txt) RELEVANT=1; break ;;
        CLAUDE.md) RELEVANT=1; break ;;
        docs/reference/taleworlds-api-snapshot/*.md) RELEVANT=1; break ;;
    esac
done
[[ $RELEVANT -eq 0 ]] && { echo '{}'; exit 0; }

# Locate python (fail open if absent).
PY="$PYBIN"
[[ -z "$PY" ]] && { echo '{}'; exit 0; }

# Run the drift gate. --drift-only runs just the checks that can block (config-example drift,
# version markers, the CLAUDE.md and AGENTS.md budget, RCA FOLLOW-UP lines) and exits 1 iff one fails;
# 0 otherwise. --staged makes the RCA check read the index, which is what the commit records.
# Only rc=1 blocks. The full --fail-on-drift run took 25 s on the laptop (2026-11-04), past the
# 20 s bound below; these checks take under a second.
#
# Inner bound, below the 30s registered timeout. A harness kill discards this hook's
# output, which reads as a clean pass; this gate was dead from 2026-08-31 when it ran at
# a 5s registration against a measured 7.8s runtime. Keep the overrun inside the hook.
OUT=$(timeout -k 2 20 "$PY" tools/lint_docs.py --drift-only --staged 2>/dev/null)
RC=$?

# 124 = the inner timeout fired. Not a pass. Ask rather than block: an overrun is an
# infrastructure fault, and a hook's own fault must never hard-block the user.
if [[ $RC -eq 124 ]]; then
    printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"[check-doc-config-drift] The doc drift gate exceeded its 20s budget and was stopped. This commit is UNCHECKED for config-example drift, version-marker mismatches, the CLAUDE.md eager-load budget and RCA FOLLOW-UP lines. This is NOT a pass. Run: python tools/lint_docs.py --drift-only --staged"}}'
    exit 0
fi

[[ $RC -ne 1 ]] && { echo '{}'; exit 0; }

# Extract just the gating sections (drift / version / CLAUDE.md budget / RCA — the report's tail), bounded.
DETAILS=$(printf '%s' "$OUT" | awk '/^## (Config-example drift|Version mismatches|CLAUDE\.md budget|RCA FOLLOW-UP lines with no issue or reason)/{f=1} f' | head -40)

MSG=$(printf '%s' "$DETAILS" | "$PYBIN" -c '
import sys, json
lines = [l for l in sys.stdin.read().splitlines() if l.strip()][:36]
print(json.dumps(
    "[check-doc-config-drift] git commit BLOCKED: a documented config example / version "
    "marker drifted from the source of truth, or CLAUDE.md broke its eager-load budget "
    "(46KB file / 400-char table rows / 600-char prose lines — CLAUDE.md is an index; move "
    "detail to the linked doc). Sync the doc example to the shipped ModuleData config, the "
    "version marker to .claude/pinned-game-version.txt, or thin the CLAUDE.md row, then "
    "re-stage. A new FOLLOW-UP line in an RCA must cite an issue or say no issue: and why. "
    "Details: python tools/lint_docs.py\n\n" + "\n".join(lines)))
' 2>/dev/null)
[[ -z "$MSG" ]] && { echo '{}'; exit 0; }

printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":%s}}\n' "$MSG"
exit 0
