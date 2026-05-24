#!/usr/bin/env bash
# Wrapper: runs translate_with_claude.py for a (lang, module) combo.
# Writes per-call summary to tools/translation_cache/_progress.log
#
# Usage: ./translate_all_remaining.sh <LANG> <MODULE>
#   e.g.: ./translate_all_remaining.sh RU TAOM_Map

set -e
LANG_CODE="${1:?usage: $0 LANG MODULE}"
MODULE="${2:?usage: $0 LANG MODULE}"

export ANTHROPIC_API_KEY=$(cat ~/.taom_anthropic_key)
LOG=tools/translation_cache/_progress.log
mkdir -p tools/translation_cache

echo "" >> "$LOG"
echo "=== $(date '+%Y-%m-%d %H:%M:%S')  Starting $LANG_CODE / $MODULE ===" >> "$LOG"

python -u tools/translate_with_claude.py --lang "$LANG_CODE" --module "$MODULE" --apply 2>&1 | tee -a "$LOG"

echo "=== $(date '+%Y-%m-%d %H:%M:%S')  Finished $LANG_CODE / $MODULE ===" >> "$LOG"
