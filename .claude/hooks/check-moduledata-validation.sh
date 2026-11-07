#!/usr/bin/env bash
# check-moduledata-validation.sh
# PreToolUse (Bash and PowerShell) hook: when `git commit` is about to run with TAOM ModuleData
# XML staged, run the schema-driven validator and BLOCK the commit if it finds
# ERROR-severity issues (broken Item/NPCCharacter refs, unknown cultures,
# duplicate ids). Catches the underwear / dead-troop-ref / stale-culture /
# duplicate-id bug classes before they ship.
#
# Why: tools/validate_moduledata.py consolidates the per-task ref validators
# into one engine; this hook makes it run automatically on relevant commits
# instead of relying on anyone remembering it. See
# docs/features/moduledata-validation.md and .claude/rules/moduledata-validation.md.
#
# Scope: only ERROR-severity codes block (WARNINGs -- INVALID_ENUM,
# MISSING_CIVILIAN_TYPE, BROKEN_PARTY_TEMPLATE_REF, DUPLICATE_ITEM_DEF -- do
# not). Run `python tools/validate_moduledata.py` manually to see warnings.
#
# Fail-open (per .claude/rules/harness-facts.md "TAOM hooks MUST fail open"):
# ANY hook-internal failure -- no python, validator crash, missing game install
# (rc=2), nothing staged -- ALLOWS the commit. Only a genuine validator ERROR
# exit (rc=1) blocks. A validator crash exits 3 (validate_moduledata.py entry(); an
# uncaught exception would exit 1 and read as findings): allowed, but never silently,
# the traceback goes out as PreToolUse additionalContext beside the tool result.
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
taom_pybin_degraded "check-moduledata-validation" "ModuleData refs, landless cultures and duplicate ids" && { echo '{}'; exit 0; }

# The command as POSIX-shell text (plan 027): _pybin.sh taom_hook_command hands a PowerShell
# command back as the Bash text of the same command and names git `git` wherever it is the
# command (`GIT`, `git.exe`, a path), so the two-stage matcher below reads both shells.
COMMAND=$(taom_hook_command posix check-moduledata-validation)

# Two-stage git-commit matcher: handle `git -C/-c ... commit`; reject
# `git commit-tree` / `commit-graph`. Per .claude/rules/hook-authoring.md "Git invocation forms hooks must handle".
taom_commit_plumbing_only && { echo '{}'; exit 0; }
taom_names_commit || { echo '{}'; exit 0; }   # _pybin.sh: `git commit`, `git -C x commit`, any blanks

# The top level of the repository the commit runs in, not always the project root (_pybin.sh
# taom_git_repo_dir). A commit in another repository is not this gate's: its tools/*.py are not
# TAOM's to run.
COMMIT_REPO=$(taom_git_repo_dir commit) || { taom_deny_multi_repo check-moduledata-validation; exit 0; }
cd "$COMMIT_REPO" 2>/dev/null || { echo '{}'; exit 0; }
taom_is_project_repo "$COMMIT_REPO" || { echo '{}'; exit 0; }

# Files in the commit. No blanket --amend skip (amend is commonly "oops, add a
# file" -- exactly the case to catch); include HEAD's files on amend.
STAGED=$(git -c core.quotePath=false diff --cached --name-only --no-renames 2>/dev/null)
case "$COMMAND" in
    *"--amend"*)
        STAGED=$(printf '%s\n%s\n' "$STAGED" \
            "$(git -c core.quotePath=false show HEAD --name-only --no-renames --pretty=format: 2>/dev/null)" | sort -u)
        ;;
esac

# Only run when the commit touches ModuleData XML (the validator's scope).
HAS_MD=0
# Split into an array under set -f, never a here-string: Git Bash 5.3 hangs forever on one of
# 65,536 to 65,663 bytes of text (#681), and a killed gate fails open. An empty line is dropped,
# which is harmless here.
set -f; IFS=$'\n'; STAGED_LIST=($STAGED); IFS=$' \t\n'; set +f
for f in "${STAGED_LIST[@]}"; do
    case "$f" in
        Main/_Module/ModuleData/*.xml) HAS_MD=1; break ;;
    esac
done
[[ $HAS_MD -eq 0 ]] && { echo '{}'; exit 0; }

# Locate python (fail open if absent).
PY="$PYBIN"
[[ -z "$PY" ]] && { echo '{}'; exit 0; }

# Run only the ERROR-severity checks. Validator exits 1 on ERROR, 0 clean,
# 2 bad-input. Only rc=1 blocks; everything else fails open.
# Keep this list in step with every ERROR-severity code in tools/taom_schema.py. It is an
# allowlist, so a new ERROR check is silently non-blocking until it is named here: on 2026-08-31
# UPGRADE_SKILL_REGRESSION, LANDLESS_CULTURE, MOUNTED_DWARF, SETTLEMENT_ECONOMY_FLOOR and
# BROKEN_BODY_PROPERTY_REF were all live ERROR checks that this hook let straight through.
# Inner bound, deliberately below the 60s registered timeout. A harness kill DISCARDS
# the hook's output, so an overrun there is indistinguishable from a clean pass. That is
# exactly how this gate died on 2026-08-31: it was re-registered at 5s against a 27.0s
# runtime, so every ModuleData commit silently skipped the check. Bounding the work here
# keeps the overrun inside the hook, where it can still speak.
#
# WP108 adds MISSING_COLLISION_BODY and COLLISION_BODY_BORROWED: each runs one
# validate_mesh_refs.extract_refs pass over the Armory XML (about 3.5 s each on the 1.5.x
# desktop, 2026-09-21). The laptop's measured total is in the WP108 validator commit.
# SCHEMA_INVALID adds about 0.5 s (1.5.x desktop, 2026-09-21).
#
# The validator ran in ~4s before WP108's mesh and body pass, and about 21 s on the laptop since. 16.5s of the old 27s figure was one
# quadratic regex in taom_schema.py scanning characters/lords.xml for a close tag that
# file does not contain (fixed 2026-08-31, byte-identical output). The 60s/45s budget is
# left deliberately generous. Headroom costs nothing unless the work overruns, and being
# killed is the failure mode this gate has already suffered once.
ERR_FILE=$(mktemp 2>/dev/null) || ERR_FILE=/dev/null
OUT=$(timeout -k 2 45 "$PY" tools/validate_moduledata.py \
        --code BROKEN_ITEM_REF --code BROKEN_TROOP_REF --code UNKNOWN_CULTURE \
        --code DUPLICATE_NPC_ID --code DUPLICATE_CULTURE_ID --code DUPLICATE_ROSTER_ID \
        --code BROKEN_BODY_PROPERTY_REF --code LANDLESS_CULTURE --code MOUNTED_DWARF \
        --code SETTLEMENT_ECONOMY_FLOOR --code UPGRADE_SKILL_REGRESSION \
        --code SKILL_TEMPLATE_SHADOWS_SKILLS --code UPGRADE_TIER_COLLAPSE --code UPGRADE_INDEX_EMPTY \
        --code MISSING_BODY_ARMOUR --code MISSING_EDUCATION_TEMPLATES \
        --code MISSING_HARNESS_FAMILY_TYPE --code HARNESS_FAMILY_MISMATCH \
        --code MOUNT_WITHOUT_HARNESS --code MISSING_COLLISION_BODY \
        --code COLLISION_BODY_BORROWED \
        --code SCHEMA_INVALID \
        --code BROKEN_FACTION_REF \
        --code ITEM_SLOT_REJECTED \
        --code TROOP_ID_REMOVED --code BROKEN_ROSTER_REF --code BROKEN_SETTLEMENT_REF \
        2>"$ERR_FILE")
RC=$?

# 124 = the inner timeout fired. Never report this as a pass; ask instead of blocking,
# because an overrun is an infrastructure fault and a hook's own fault must not hard-block.
if [[ $RC -eq 124 ]]; then
    [[ "$ERR_FILE" != /dev/null ]] && rm -f "$ERR_FILE"
    printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"[check-moduledata-validation] The ModuleData validator exceeded its 45s budget and was stopped. This commit is UNCHECKED for broken Item/NPCCharacter refs, unknown or landless cultures, and duplicate ids. This is NOT a pass. Run: python tools/validate_moduledata.py"}}'
    exit 0
fi

# 3 = the validator crashed. Its own fault never blocks, so allow, and say so: no output would
# read as "checked, clean".
if [[ $RC -eq 3 ]]; then
    CRASH=$(tail -n 15 "$ERR_FILE" 2>/dev/null | "$PYBIN" -c '
import sys, json
print(json.dumps(
    "[check-moduledata-validation] tools/validate_moduledata.py CRASHED (exit 3), so this commit is "
    "UNCHECKED for broken Item/NPCCharacter/Faction refs, unknown or landless cultures and duplicate ids. "
    "This is NOT a pass; the commit was allowed because a hook bug must not block. Fix the validator, "
    "then run: python tools/validate_moduledata.py\n\n" + sys.stdin.read()))
' 2>/dev/null)
    [[ "$ERR_FILE" != /dev/null ]] && rm -f "$ERR_FILE"
    [[ -z "$CRASH" ]] && { echo '{}'; exit 0; }
    printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","additionalContext":%s}}\n' "$CRASH"
    exit 0
fi
[[ "$ERR_FILE" != /dev/null ]] && rm -f "$ERR_FILE"

[[ $RC -ne 1 ]] && { echo '{}'; exit 0; }

# Exit 1 with no finding to quote: the validator died while it loaded. Allow, and say so.
if [[ -z "${OUT//[[:space:]]/}" ]]; then
    printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","additionalContext":"[check-moduledata-validation] tools/validate_moduledata.py exited 1 with no findings, so this commit is UNCHECKED by it. This is NOT a pass. Run: python tools/validate_moduledata.py"}}'
    exit 0
fi

# Build a JSON-escaped deny message with the validator's findings (bounded).
MSG=$(printf '%s' "$OUT" | "$PYBIN" -c '
import sys, json
lines = [l for l in sys.stdin.read().splitlines() if l.strip()][-30:]
print(json.dumps(
    "[check-moduledata-validation] git commit BLOCKED: tools/validate_moduledata.py "
    "found ERROR-severity issues (broken Item/NPCCharacter ref, unknown culture or duplicate id). "
    "The findings may sit in the live TAOM_Map or LOTRLOME_Armory install, which the validator also "
    "reads, rather than in the staged files. Either fix them, or ask the owner of that live edit; "
    "never bypass this hook. See details: "
    "python tools/validate_moduledata.py\n\n" + "\n".join(lines)))
' 2>/dev/null)
[[ -z "$MSG" ]] && { echo '{}'; exit 0; }

printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":%s}}\n' "$MSG"
exit 0
