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
# Fail-open (per .claude/rules/harness-facts.md "TAOM hooks fail open"):
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
case "$COMMAND" in
    *"git commit-"*) echo '{}'; exit 0 ;;
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;
    *) echo '{}'; exit 0 ;;
esac

# The top level of the repository the commit runs in, not always the project root (_pybin.sh
# taom_git_repo_dir): until 2026-10-16 a worktree commit's staged XML was never validated, and the main
# checkout's was. A commit in another repository is not this gate's: it holds no TAOM ModuleData, and
# its tools/*.py are not TAOM's to run.
COMMIT_REPO=$(taom_git_repo_dir commit) || { taom_deny_multi_repo check-moduledata-validation; exit 0; }
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

# Only run when the commit touches ModuleData XML or XSLT (the validator's scope). XSLT joined
# with #626: lords.xslt carries 364 of the templated lords SKILL_TEMPLATE_MISMATCH judges, and a
# commit staging only it used to run no validator at all.
HAS_MD=0
# Split into an array under set -f, never a here-string: Git Bash 5.3 hangs forever on one of
# 65,536 to 65,663 bytes of text, and a name list that size is reachable (#681). An empty line is dropped,
# which is harmless: it matches no case arm.
set -f; IFS=$'\n'; STAGED_LIST=($STAGED); IFS=$' \t\n'; set +f
for f in "${STAGED_LIST[@]}"; do
    case "$f" in
        Main/_Module/ModuleData/*.xml|Main/_Module/ModuleData/*.xslt) HAS_MD=1; break ;;
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
# Measured 2026-09-21, warm file cache: 14.9 to 15.6s. build_registries 3.8s, validator.run
# 3.9s, MISSING_COLLISION_BODY 3.4 to 3.9s, COLLISION_BODY_BORROWED (#633) 3.5 to 3.9s,
# SCHEMA_INVALID 0.5s, SKILL_TEMPLATE_MISMATCH 0.25s. Each body pass is ~3.1s of
# validate_mesh_refs.extract_refs over 45 MiB of XML (1.3s of it a quadratic line count,
# see the #633 review); the 4,611-pack tpac TOC scan is 0.2s, not the ~3s the 2026-09-18
# ledger blamed it for. Cold file cache: unmeasured since the second body pass landed.
# History: 27s until 2026-08-31 (a quadratic regex in taom_schema.py over lords.xml),
# ~9s on 2026-09-18, 8.2s warm on 2026-09-19. The 60s/45s budget is left deliberately
# generous, about 3x headroom warm. Headroom costs nothing unless the work overruns, and
# being killed is the failure mode this gate has already suffered once.
ERR_FILE=$(mktemp 2>/dev/null) || ERR_FILE=/dev/null
OUT=$(timeout -k 2 45 "$PY" tools/validate_moduledata.py \
        --code BROKEN_ITEM_REF --code BROKEN_TROOP_REF --code UNKNOWN_CULTURE \
        --code DUPLICATE_NPC_ID --code DUPLICATE_CULTURE_ID --code DUPLICATE_ROSTER_ID \
        --code BROKEN_BODY_PROPERTY_REF --code LANDLESS_CULTURE --code MOUNTED_DWARF \
        --code SETTLEMENT_ECONOMY_FLOOR --code SETTLEMENT_ASSIGNMENT_DRIFT \
        --code UPGRADE_SKILL_REGRESSION \
        --code SKILL_TEMPLATE_MISMATCH --code UPGRADE_TIER_COLLAPSE --code UPGRADE_INDEX_EMPTY \
        --code MISSING_BODY_ARMOUR --code MISSING_EDUCATION_TEMPLATES \
        --code MISSING_HARNESS_FAMILY_TYPE --code HARNESS_FAMILY_MISMATCH \
        --code MOUNT_WITHOUT_HARNESS --code MISSING_COLLISION_BODY \
        --code COLLISION_BODY_BORROWED --code ITEM_SLOT_REJECTED \
        --code SCHEMA_INVALID --code ARMOUR_ACQUISITION_REF \
        --code CREATURE_GEAR_OBTAINABLE --code BROKEN_FACTION_REF \
        2>"$ERR_FILE")
RC=$?

# 124 = the inner timeout fired. Never report this as a pass; ask instead of blocking,
# because an overrun is an infrastructure fault and a hook's own fault must not hard-block.
if [[ $RC -eq 124 ]]; then
    [[ "$ERR_FILE" != /dev/null ]] && rm -f "$ERR_FILE"
    printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"[check-moduledata-validation] The ModuleData validator exceeded its 45s budget and was stopped. This commit is UNCHECKED for broken Item/NPCCharacter refs, unknown or landless cultures, and duplicate ids. This is NOT a pass. Run: python tools/validate_moduledata.py"}}'
    exit 0
fi

# Career kits (#743): taom_career_starting_equipment.xml is generated from the troop files, so a
# troop edit can leave it off its rule with nothing staged in it. 998f054c did, and the file sat
# adrift from v2.0.31 to v2.0.34 because its pin only ran by hand. ~0.8s; 10s bound keeps the
# hook inside its 60s registration. Exit 1 is drift and blocks; 2 (no install) fails open.
KIT_OUT=$(timeout -k 2 10 "$PY" tools/generate_career_kits.py --verify 2>&1)
if [[ $? -eq 1 ]]; then
    OUT=$(printf '%s\n%s\nRepair: python tools/generate_career_kits.py --apply, then python tools/wire_starter_kit_rosters.py --apply' "$OUT" "$KIT_OUT")
    RC=1
fi

# 3 = the validator crashed. Its own fault never blocks, so allow, and say so: an allow's stderr
# reaches no one (harness-facts.md "Visibility"), and no output would read as "checked, clean".
# The kit check above still ran and still blocks on drift; this only decides the no-block answer.
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

# Build a JSON-escaped deny message with the validator's findings (bounded).
MSG=$(printf '%s' "$OUT" | "$PYBIN" -c '
import sys, json
lines = [l for l in sys.stdin.read().splitlines() if l.strip()][-30:]
print(json.dumps(
    "[check-moduledata-validation] git commit BLOCKED: tools/validate_moduledata.py "
    "or tools/generate_career_kits.py --verify found ERROR-severity issues in staged "
    "ModuleData XML (broken Item/NPCCharacter ref, unknown culture, duplicate id, or a "
    "career kit off its rule). Fix them and re-stage. See details: "
    "python tools/validate_moduledata.py\n\n" + "\n".join(lines)))
' 2>/dev/null)
[[ -z "$MSG" ]] && { echo '{}'; exit 0; }

printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":%s}}\n' "$MSG"
exit 0
