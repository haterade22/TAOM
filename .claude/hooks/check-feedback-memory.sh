#!/bin/bash

# PostToolUse (Edit|Write): a new feedback memory sits on one machine, where Codex, the laptop and any
# other client never see it, and the repo rule it overrides keeps saying the opposite. Prose
# alone has not held, because the harness itself tells an agent to save feedback. So this asks, when
# the file is written, where the rule belongs. It informs and never blocks.
#
# It reads the raw payload and starts no interpreter. A Write's tool_response names `create` or
# `update`, so editing a card stays silent. tools/tests/test_feedback_memory_hook.py pins each case.

INPUT=$(cat)

[[ "$INPUT" == *feedback_* ]] || exit 0

re='"tool_name"[[:space:]]*:[[:space:]]*"Write"'
[[ "$INPUT" =~ $re ]] || exit 0

# The path only, never the content: a file may mention a feedback memory without being one.
re='"file_path"[[:space:]]*:[[:space:]]*"[^"]*memory[\\/]+feedback_[^"\\/]*\.md"'
[[ "$INPUT" =~ $re ]] || exit 0

re='"type"[[:space:]]*:[[:space:]]*"update"'
[[ "$INPUT" =~ $re ]] && exit 0

printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"A correction that should hold on another client or machine goes in the repo. Name the rule, skill or doc that now holds it, or say why this is machine-local."}}'
exit 0
