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
#   - a new docs/features/*.md with no `## How to verify in game` section that has a body; a doc
#     that `git add` stages in the SAME command is read from the command text (--also-new).
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
taom_commit_plumbing_only && { echo '{}'; exit 0; }
taom_names_commit || { echo '{}'; exit 0; }   # _pybin.sh: `git commit`, `git -C x commit`, any blanks

# The top level of the repository the commit runs in, not always the project root (_pybin.sh
# taom_git_repo_dir). A commit in another repository is not this gate's: its tools/*.py are not
# TAOM's to run.
COMMIT_REPO=$(taom_git_repo_dir commit) || { taom_deny_multi_repo check-doc-config-drift; exit 0; }
cd "$COMMIT_REPO" 2>/dev/null || { echo '{}'; exit 0; }
taom_is_project_repo "$COMMIT_REPO" || { echo '{}'; exit 0; }

# What the commit records (#987), read from the command with _shellwords.py commit-plan: whether it
# amends HEAD, and the paths of a path commit (`git commit -m x -- <paths>`, or paths after the
# message, without -a or -i). A path commit records those paths' working copies and not the index; an
# amend records the index against HEAD's parent, so a line HEAD itself added is new to it and is
# checked again. A path amend (`--amend -- <paths>`) is judged on its paths alone, so HEAD's lines in
# other files are not re-checked. The paths come back absolute (joined onto the call's directory), so
# they read the same from here. A reader that gives up (`unknown`, or no answer) leaves the index
# against HEAD, with an --amend anywhere in the text still counting, as it did before.
AMEND=0
COMMIT_PATHS=()
PLAN=$(printf '%s' "$INPUT" | "$PYBIN" "$TAOM_HOOKS_DIR/_shellwords.py" commit-plan 2>/dev/null)
set -f; IFS=$'\n'; PLAN_LINES=($PLAN); IFS=$' \t\n'; set +f
case "${PLAN_LINES[0]:-}" in
    amend) AMEND=1; COMMIT_PATHS=("${PLAN_LINES[@]:1}") ;;
    new)   COMMIT_PATHS=("${PLAN_LINES[@]:1}") ;;
    *)     [[ "$COMMAND" == *"--amend"* ]] && AMEND=1 ;;
esac

# Files in the commit. No blanket --amend skip (amend is commonly "oops, add a
# file" -- exactly the case to catch); include HEAD's files on amend.
STAGED=$(git -c core.quotePath=false diff --cached --name-only --no-renames 2>/dev/null)
if [[ ${#COMMIT_PATHS[@]} -gt 0 ]]; then
    # What a path commit records: the named paths' working copies against HEAD (against the index
    # while HEAD is unborn). A directory path lists only what changed in it, so it is no longer
    # relevant for the feature docs it merely holds.
    if git rev-parse -q --verify 'HEAD^{commit}' >/dev/null 2>&1; then
        PATH_FILES=$(git -c core.quotePath=false diff --name-only --no-renames HEAD -- "${COMMIT_PATHS[@]}" 2>/dev/null)
    else
        PATH_FILES=$(git -c core.quotePath=false diff --cached --name-only --no-renames -- "${COMMIT_PATHS[@]}" 2>/dev/null)
    fi
    # Paths that change nothing here: judge the index, as the hook did before it read paths.
    if [[ -n "$PATH_FILES" ]]; then STAGED=$PATH_FILES; else COMMIT_PATHS=(); fi
fi
if [[ $AMEND -eq 1 ]]; then
    STAGED=$(printf '%s\n%s\n' "$STAGED" \
        "$(git -c core.quotePath=false show HEAD --name-only --no-renames --pretty=format: 2>/dev/null)" | sort -u)
fi

# Only run when the commit touches a surface the gating checks read: a feature doc, a shipped
# ModuleData JSON, a version marker, an entry doc, an RCA, or the rule text the drift checks scan.
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
        CLAUDE.md|AGENTS.md|docs/ai-includes/*.md) RELEVANT=1; break ;;
        .claude/rules/*.md|.claude/agents/*.md|.claude/skills/*.md) RELEVANT=1; break ;;
        docs/modding/*.md|docs/reference/*.md|.ai/*.md|.agents/*.md) RELEVANT=1; break ;;
    esac
done
# A feature doc that `git add <doc> && git commit` stages in one command is not in the index yet:
# take every docs/features/<name>.md token of the command text and let the linter read it from the
# working tree (--also-new). Any error here leaves the list empty, which only skips the extra check.
ALSO_NEW=()
FEATURE_DOC_TOKEN='docs/features/[^/[:space:]"'"'"']+\.md'
set -f; ALSO_NEW_TOKENS=($(printf '%s\n' "$COMMAND" | grep -oE "$FEATURE_DOC_TOKEN" 2>/dev/null | sort -u | head -20)); set +f
for t in "${ALSO_NEW_TOKENS[@]+"${ALSO_NEW_TOKENS[@]}"}"; do
    ALSO_NEW+=(--also-new "$t"); RELEVANT=1
done

[[ $RELEVANT -eq 0 ]] && { echo '{}'; exit 0; }

# Locate python (fail open if absent).
PY="$PYBIN"
[[ -z "$PY" ]] && { echo '{}'; exit 0; }

# Run the drift gate. --drift-only runs just the checks that can block (config-example drift,
# version markers, the CLAUDE.md and AGENTS.md budget, RCA FOLLOW-UP lines) and exits 1 iff one fails;
# 0 otherwise. --staged makes the RCA check read the index, which is what a plain commit records.
# Only rc=1 blocks. The full --fail-on-drift run took 25 s on the laptop (2026-11-04), past the
# 20 s bound below; these checks take under a second.
#
# Inner bound, below the 30s registered timeout. A harness kill discards this hook's
# output, which reads as a clean pass; this gate was dead from 2026-08-31 when it ran at
# a 5s registration against a measured 7.8s runtime. Keep the overrun inside the hook.
LINT_ARGS=(--drift-only --staged)
if [[ $AMEND -eq 1 ]]; then
    # An amend replaces HEAD: what it adds is the index against HEAD's parent, or against the empty
    # tree when HEAD is the root commit.
    if git rev-parse -q --verify 'HEAD~1^{commit}' >/dev/null 2>&1; then
        LINT_ARGS+=(--dash-base 'HEAD~1')
    else
        LINT_ARGS+=(--dash-base 4b825dc642cb6eb9a060e54bf8d69288fbee4904)
    fi
fi
[[ ${#COMMIT_PATHS[@]} -gt 0 ]] && LINT_ARGS+=(--only-paths "${COMMIT_PATHS[@]}")
LINT_ARGS+=(${ALSO_NEW[@]+"${ALSO_NEW[@]}"})
OUT=$(timeout -k 2 20 "$PY" tools/lint_docs.py "${LINT_ARGS[@]}" 2>/dev/null)
RC=$?

# 124 = the inner timeout fired. Not a pass. Ask rather than block: an overrun is an
# infrastructure fault, and a hook's own fault must never hard-block the user.
if [[ $RC -eq 124 ]]; then
    ASK=$("$PYBIN" -c 'import sys, json
print(json.dumps("[check-doc-config-drift] The doc drift gate exceeded its 20s budget and was stopped. "
                 "This commit is UNCHECKED by the doc linter gating checks. This is NOT a pass. "
                 "Run: python tools/lint_docs.py " + sys.argv[1]))' "${LINT_ARGS[*]}" 2>/dev/null)
    [[ -z "$ASK" ]] && { echo '{}'; exit 0; }
    printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":%s}}\n' "$ASK"
    exit 0
fi

# A linter that cannot judge (3: it crashed; 2: a base it cannot resolve). A hook's own fault must never
# block, so allow, and say so where Claude reads it: an allow's stderr reaches no one, and no output
# would read as "checked, clean".
unchecked() {
    local why
    why=$("$PYBIN" -c 'import sys, json
print(json.dumps("[check-doc-config-drift] tools/lint_docs.py " + sys.argv[1] + ", so this commit is "
                 "UNCHECKED by the doc linter. This is NOT a pass. Run: python tools/lint_docs.py " + sys.argv[2]))' \
        "$1" "${LINT_ARGS[*]}" 2>/dev/null)
    [[ -z "$why" ]] && { echo '{}'; exit 0; }
    printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","additionalContext":%s}}\n' "$why"
    exit 0
}
[[ $RC -eq 0 ]] && { echo '{}'; exit 0; }
[[ $RC -ne 1 ]] && unchecked "could not judge (exit $RC)"

# Extract every `## ` section of the report (--drift-only prints only the gating checks, so the
# list of checks lives in the linter, not here), without the report-only [size-warn] lines. A section
# left with no finding line after that is dropped whole, header included. Bounded.
DETAILS=$(printf '%s\n' "$OUT" | awk '
function flush() { if (keep) printf "%s", buf; buf = ""; keep = 0 }
/^## / { flush(); f = 1 }
f { if ($0 ~ /\[size-warn\]/) next; buf = buf $0 "\n"; if ($0 ~ /^- /) keep = 1 }
END { flush() }' | head -40)

# Exit 1 with no finding to quote: the linter died while it loaded (a crash in its main exits 3).
[[ -z "$DETAILS" ]] && unchecked "exited 1 with no findings"

# The reproduction quotes the arguments this run passed, and a path commit reads the working copy, so
# its fix is the file, not the index.
FIX="then re-stage"
[[ ${#COMMIT_PATHS[@]} -gt 0 ]] && FIX="in the file itself (this commit reads the named paths' working copies), then commit again"
MSG=$(printf '%s' "$DETAILS" | "$PYBIN" -c '
import sys, json
lines = [l for l in sys.stdin.read().splitlines() if l.strip()][:36]
print(json.dumps(
    "[check-doc-config-drift] git commit BLOCKED by the doc linter (tools/lint_docs.py "
    + sys.argv[1] + "). Fix each finding quoted below, " + sys.argv[2] + "; each section names "
    "the rule it enforces. Details: python tools/lint_docs.py\n\n" + "\n".join(lines)))
' "${LINT_ARGS[*]}" "$FIX" 2>/dev/null)
[[ -z "$MSG" ]] && { echo '{}'; exit 0; }

printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":%s}}\n' "$MSG"
exit 0
