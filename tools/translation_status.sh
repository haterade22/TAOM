#!/usr/bin/env bash
# Quick dashboard: per-language cache size + last batch line + process status
# Usage: ./tools/translation_status.sh
set -e

echo "=== TAOM Translation Status ==="
echo "Processes: $(tasklist 2>/dev/null | grep -c python.exe) python.exe"
echo ""
printf "%-5s  %12s  %s\n" LANG CACHE_SIZE LAST_LINE
echo "----  ------------  ----------------------------------------"
for lang in RU SP DE FR IT BR JP KO TR CNs CNt; do
  cache_file="tools/translation_cache/$(echo $lang | tr A-Z a-z).json"
  log_file="/tmp/${lang}_run.log"
  if [ -f "$cache_file" ]; then
    size=$(stat -c %s "$cache_file" 2>/dev/null)
  else
    size=0
  fi
  if [ -f "$log_file" ]; then
    last=$(grep -E "^    Batch|^  Summary|^  Total" "$log_file" 2>/dev/null | tail -1 | head -c 70)
  else
    last="(no log)"
  fi
  printf "%-5s  %12s  %s\n" "$lang" "$size" "$last"
done
echo ""
