#!/usr/bin/env bash
# Quick dashboard: per-language cache size + last batch line + process status
# Usage: ./tools/translation_status.sh
set -e

# The language list is DERIVED from the shipped language directories, never hardcoded.
# A hardcoded list is what silently dropped PL from every status report for months: the
# same omission that kept PL out of every translator run until 2026-08-25. Adding a
# language now needs no edit here, and a language that exists on disk cannot be missed.
LANG_DIR="Main/_Module/ModuleData/Languages"

echo "=== TAOM Translation Status ==="

# Count TRANSLATION runs specifically. A bare `grep -c python.exe` counts every python
# process on the box, which in practice is one MCP server per open Claude session: it
# reported 48 while zero translation jobs were running. A dashboard that overstates
# activity is worse than none, because it makes you hold off on starting a real run.
running=$(tasklist /FO CSV /V 2>/dev/null | grep -ci "translate_with_claude" || true)
echo "Translation runs in flight: ${running:-0}"
echo ""

printf "%-5s  %12s  %s\n" LANG CACHE_SIZE LAST_LINE
echo "----  ------------  ----------------------------------------"

if [ ! -d "$LANG_DIR" ]; then
  echo "  language directory not found: $LANG_DIR (run from the repo root)"
  exit 1
fi

for path in "$LANG_DIR"/*/; do
  [ -d "$path" ] || continue
  lang=$(basename "$path")
  cache_file="tools/translation_cache/$(echo "$lang" | tr 'A-Z' 'a-z').json"
  log_file="/tmp/${lang}_run.log"
  if [ -f "$cache_file" ]; then
    size=$(stat -c %s "$cache_file" 2>/dev/null || echo 0)
  else
    size="NO CACHE"
  fi
  if [ -f "$log_file" ]; then
    last=$(grep -E "^    Batch|^  Summary|^  Total" "$log_file" 2>/dev/null | tail -1 | head -c 70)
  else
    last="(no log)"
  fi
  printf "%-5s  %12s  %s\n" "$lang" "$size" "$last"
done
echo ""
