#!/bin/bash

# PostToolUse(Edit|Write): run the shield-vs-unusable-weapon gate after an edit that could
# introduce that defect.
#
# WHY THIS EXISTS
# A crafted weapon with no inline <Weapon> takes its usages from WeaponDescription membership,
# and the FIRST description listing every piece it uses becomes the primary (Crafting.cs:566-608).
# A polearm absent from OneHandedPolearm resolves to a requires_no_shield primary, so a roster
# pairing it with a shield produces a troop that holds the weapon through the pre-battle phase and
# then never draws it. Nothing errors and nothing logs. It shipped three times (#445 rosters, #449
# Dale spears, the Black Numenorean lance) and a player found each one.
#
# tools/audit_polearm_shield_parity.py catches it, but it needs the game install to resolve item
# data and SKIPs without one, so CI cannot run it. Nothing ran it automatically until this hook:
# see docs/reviews/lessons/build-tooling-workflow.md, "A gate sitting in an unmerged PR is not a
# gate", which is the lesson this hook is the fix for.
#
# CONTRACT
#   FAIL (exit 1)   -> print the FAIL block to stderr, once per distinct finding set
#   PASS (exit 0)   -> silent, and clear the mute so the next regression speaks
#   SKIP / no tool  -> print one line saying it could not run. NEVER silent: for a detection hook
#                      no output is read as "no findings", per hook-authoring.md (2026-08-10).
# Always exits 0. This is advisory; PostToolUse fires after the write and gates nothing.
#
# The WARN block (43 rosters pairing a shield with a two-handed sword/axe/mace, issue #450) is
# deliberately not reported. It is pre-existing, it is a roster decision rather than a data one,
# and surfacing it on every edit would be permanent noise.

# Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)

# Parse the edited path: jq preferred, grep fallback. Mirrors log-agent.sh: jq is NOT on PATH in
# this Git Bash install (verified 2026-08-20), so a bare `jq` call would make this hook inert.
if command -v jq >/dev/null 2>&1; then
  FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
else
  # Unescape the JSON backslash doubling that Windows paths always carry; jq -r does it for free.
  FILE_PATH=$(echo "$INPUT" | grep -oE '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 \
              | sed 's/.*"\([^"]*\)"$/\1/' | sed 's/\\\\/\\/g')
fi

[[ -z "$FILE_PATH" ]] && exit 0

# Resolve repo-relative paths against the project, not the cwd the hook happens to inherit
# (log-agent.sh convention). Without this the hook is wrong in a git worktree.
ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$ROOT" 2>/dev/null || exit 0

# --- relevance filter (cheap; the audit itself costs ~1.5s, so only pay it when it can matter) ---
# Three ways an edit can introduce or fix this defect: the registration (weapon_descriptions.xslt),
# the item definition (LOTRLOME_items), or the roster that pairs weapon with shield (any ModuleData
# XML carrying an <EquipmentRoster>). The roster test reads the file rather than matching a path
# pattern, because the audit rglobs every *.xml under the rosters root and rosters are not confined
# to troops/.
RELEVANT=""
case "$FILE_PATH" in
    *weapon_descriptions.xslt) RELEVANT="1" ;;
    *LOTRLOME_items*) RELEVANT="1" ;;
    *.xml)
        if grep -q "<EquipmentRoster" "$FILE_PATH" 2>/dev/null; then
            RELEVANT="1"
        fi
        ;;
esac
[[ -z "$RELEVANT" ]] && exit 0

TOOL="tools/audit_polearm_shield_parity.py"
STATE=".claude/logs/.polearm-gate-reported"

# Report only when the finding set CHANGES, so a burst of roster edits does not re-nag while a
# known finding stands, but a NEW regression speaks immediately. Mirrors check-deep-review.sh's
# already-handled early-exit, adapted to content rather than recency.
report_once() {
    local body="$1"
    local hash
    hash=$(printf '%s' "$body" | cksum 2>/dev/null | cut -d' ' -f1)
    if [[ -n "$hash" && -f "$STATE" ]] && [[ "$(cat "$STATE" 2>/dev/null)" == "$hash" ]]; then
        exit 0
    fi
    mkdir -p "$(dirname "$STATE")" 2>/dev/null
    [[ -n "$hash" ]] && printf '%s' "$hash" > "$STATE" 2>/dev/null
    printf '%s\n' "$body" >&2
    exit 0
}

PY="$PYBIN"

if [[ -z "$PY" ]]; then
    report_once "polearm/shield gate did not run: no python on PATH. Run '$TOOL' by hand before committing $FILE_PATH."
fi
if [[ ! -f "$TOOL" ]]; then
    report_once "polearm/shield gate did not run: $TOOL is absent from this tree (it landed on trunk 2026-08-20). A shield paired with a weapon the AI will not draw is silent in game."
fi

# Inner bound, below the 20s registered timeout. The audit measured 2.9s on 2026-08-31,
# but it was registered at 5s, so a cold cache or a busy machine could push it past the
# harness kill. A kill discards output, and for THIS hook silence is read as "no findings"
# (see the CONTRACT above), so the overrun has to be caught here where it can still speak.
OUTPUT=$(timeout -k 2 15 "$PY" "$TOOL" 2>&1)
STATUS=$?

if [[ $STATUS -eq 124 ]]; then
    report_once "polearm/shield gate exceeded its 15s budget and was stopped. This is NOT a pass: the roster you just edited is unchecked for a shield paired with a weapon the AI will never draw. Run '$TOOL' by hand."
fi

if [[ $STATUS -eq 1 ]]; then
    # The FAIL block only: from the FAIL header to the blank line that ends it.
    BLOCK=$(printf '%s\n' "$OUTPUT" | awk '/^FAIL:/{f=1} f{if(/^$/)exit; print}')
    [[ -z "$BLOCK" ]] && BLOCK="$OUTPUT"
    report_once "$BLOCK

These troops hold the weapon at spawn and draw a sidearm when the fight starts. Nothing logs it.
Register the pieces via 'tools/register_one_handed_polearms.py' (add the item to ONE_HANDED_ITEMS)
or change the roster. Never hand-edit the Armory XSLT: it is not in this repo and a module refresh
reverts it. Background: docs/reference/doc-lookup.md, 'Confirm a troop will actually FIGHT'."
elif [[ $STATUS -ne 0 ]]; then
    report_once "polearm/shield gate errored (exit $STATUS). First line: $(printf '%s\n' "$OUTPUT" | head -1)"
elif printf '%s\n' "$OUTPUT" | grep -q '^SKIP:'; then
    report_once "polearm/shield gate could not run: $(printf '%s\n' "$OUTPUT" | grep '^SKIP:' | head -1). This check needs the game install; it is not covered by CI."
fi

# PASS. Drop the mute so a future regression is reported rather than swallowed as a repeat.
rm -f "$STATE" 2>/dev/null
exit 0
