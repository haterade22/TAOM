#!/usr/bin/env bash
# check-moduledata-validation.sh
# PreToolUse(Bash) hook: when `git commit` is about to run with TAOM ModuleData
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
# exit (rc=1) blocks.
#
# Returns: {} to allow, {"permissionDecision":"deny","message":"..."} to block.

set -uo pipefail

INPUT=$(cat)

# Extract the bash command from tool_input (mirrors check-changelog-changed.sh).
COMMAND=$(printf '%s' "$INPUT" | python3 -c '
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

# Only run when the commit touches ModuleData XML (the validator's scope).
HAS_MD=0
while IFS= read -r f; do
    case "$f" in
        Main/_Module/ModuleData/*.xml) HAS_MD=1; break ;;
    esac
done <<< "$STAGED"
[[ $HAS_MD -eq 0 ]] && { echo '{}'; exit 0; }

# Locate python (fail open if absent).
PY=$(command -v python3 || command -v python || true)
[[ -z "$PY" ]] && { echo '{}'; exit 0; }

# Run only the ERROR-severity checks. Validator exits 1 on ERROR, 0 clean,
# 2 bad-input. Only rc=1 blocks; everything else fails open.
# Keep this list in step with every ERROR-severity code in tools/taom_schema.py. It is an
# allowlist, so a new ERROR check is silently non-blocking until it is named here: on
# 2026-08-31 UPGRADE_SKILL_REGRESSION, LANDLESS_CULTURE, MOUNTED_DWARF,
# SETTLEMENT_ECONOMY_FLOOR and BROKEN_BODY_PROPERTY_REF were all live ERROR checks that
# this hook let straight through.
OUT=$("$PY" tools/validate_moduledata.py \
        --code BROKEN_ITEM_REF --code BROKEN_TROOP_REF --code UNKNOWN_CULTURE \
        --code DUPLICATE_NPC_ID --code DUPLICATE_CULTURE_ID --code DUPLICATE_ROSTER_ID \
        --code BROKEN_BODY_PROPERTY_REF --code LANDLESS_CULTURE --code MOUNTED_DWARF \
        --code SETTLEMENT_ECONOMY_FLOOR --code UPGRADE_SKILL_REGRESSION \
        --code SKILL_TEMPLATE_SHADOWS_SKILLS \
        2>/dev/null)
RC=$?
[[ $RC -ne 1 ]] && { echo '{}'; exit 0; }

# Build a JSON-escaped deny message with the validator's findings (bounded).
MSG=$(printf '%s' "$OUT" | python3 -c '
import sys, json
lines = [l for l in sys.stdin.read().splitlines() if l.strip()][-30:]
print(json.dumps(
    "[check-moduledata-validation] git commit BLOCKED: tools/validate_moduledata.py "
    "found ERROR-severity issues in staged ModuleData XML (broken Item/NPCCharacter "
    "ref, unknown culture, or duplicate id). Fix them and re-stage. See details: "
    "python tools/validate_moduledata.py\n\n" + "\n".join(lines)))
' 2>/dev/null)
[[ -z "$MSG" ]] && { echo '{}'; exit 0; }

printf '{"permissionDecision":"deny","message":%s}\n' "$MSG"
exit 0
