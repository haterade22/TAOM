#!/usr/bin/env bash
# _stop_reminder.sh: sourced by the four Stop reminder hooks. Never registered itself.
#
# WHY THIS EXISTS
# Claude Code sends a Stop hook's stderr (on exit 0) and its plain stdout to the debug log only;
# Claude never sees either (hooks docs, "Exit code 0"; .claude/rules/harness-facts.md
# "Visibility"). Until plan 011 all four reminders printed there and none ever arrived. The Stop
# channel TAOM uses is JSON on stdout (exit 2 with stderr also reaches Claude):
#   {"decision":"block","reason":"..."}
# Claude then answers the reason in one more response (hooks docs, "Stop decision control").
#
# LOOP GUARD
# The Stop payload's stop_hook_active is true when Claude is already continuing because a Stop
# hook blocked. Every caller stays silent then, so a reminder never blocks twice in a row. The
# harness also ends the turn after 8 consecutive blocks. The guard sits just before the block,
# never at the top: Claude often ends the streak in that continuation (builds, adds the entry),
# and a caller that exited early kept its marker and muted the next streak (plan 011 review).
#
# USAGE (every Stop reminder)
#   source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
#   INPUT=$(cat)
#   cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0
#   ...detect; when the condition has cleared, remove the streak marker...
#   taom_stop_hook_active "$INPUT" && exit 0
#   taom_stop_block "<hook name>: <what is true> <what to do> <how to decline in one line>"
#   ...then write the streak marker.
#
# No Python here: Stop fires on every turn, and a regex on the raw payload is enough. An escaped
# copy of the key inside last_assistant_message (\"stop_hook_active\") cannot match, because the
# closing quote must follow the key directly.

# Returns 0 when the payload says Claude is already continuing from a Stop hook block.
taom_stop_hook_active() {
    local re='"stop_hook_active"[[:space:]]*:[[:space:]]*true'
    [[ "$1" =~ $re ]]
}

# Prints the block decision with $1 as the reason. Escapes backslash and double quote, and turns
# CR, LF and TAB into spaces, which covers every character an authored reason contains.
# tools/test_hooks.sh 7a checks the output parses as JSON with the reason intact.
taom_stop_block() {
    local r="$1"
    r=${r//\\/\\\\}
    r=${r//\"/\\\"}
    r=${r//$'\r'/ }
    r=${r//$'\n'/ }
    r=${r//$'\t'/ }
    printf '{"decision":"block","reason":"%s"}\n' "$r"
}
