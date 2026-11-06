#!/bin/bash
# SessionStart hook: Flag Main/Features/<X> directories with no matching docs/features/*.md
# CLAUDE.md mandates feature docs; this hook surfaces the gap at session start.
# Only runs on fresh startup (not resume/compact/clear).

INPUT=$(cat)

if command -v jq >/dev/null 2>&1; then
  SOURCE=$(echo "$INPUT" | jq -r '.source // empty')
else
  SOURCE=$(echo "$INPUT" | grep -oE '"source"\s*:\s*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
fi

[[ "$SOURCE" != "startup" ]] && exit 0

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0
[[ ! -d Main/Features ]] && exit 0
[[ ! -d docs/features ]] && exit 0

# Build flat list of existing doc basenames (without .md)
EXISTING_DOCS=$(find docs/features -maxdepth 1 -name '*.md' -not -name 'TEMPLATE.md' 2>/dev/null | sed 's|.*/||; s|\.md$||')
# Split into an array under set -f, never a here-string: Git Bash 5.3 hangs forever on one of
# 65,536 to 65,663 bytes of text (#681), and a killed gate fails open. An empty line is dropped,
# which is harmless here.
set -f; IFS=$'\n'; DOC_LIST=($EXISTING_DOCS); IFS=$' \t\n'; set +f

# PascalCase → kebab-case (InitialChildGeneration → initial-child-generation), for every folder in
# one sed and one tr. A pipeline per folder (echo, sed, tr, basename) started about 640 processes at
# 161 folders and ran 12.5 to 17.8 s against the 10 s registration, so the harness killed this hook at
# every session start and its warning never showed (harness retro 2026-10-21, #962).
FEATURES=()
for feature_dir in Main/Features/*/; do
  [[ -d "$feature_dir" ]] || continue
  feature_dir=${feature_dir%/}
  FEATURES+=("${feature_dir##*/}")
done
[[ ${#FEATURES[@]} -eq 0 ]] && exit 0
KEBABS=$(printf '%s\n' "${FEATURES[@]}" | sed -E 's/([a-z0-9])([A-Z])/\1-\2/g; s/([A-Z]+)([A-Z][a-z])/\1-\2/g' | tr '[:upper:]' '[:lower:]')
set -f; IFS=$'\n'; KEBAB_LIST=($KEBABS); IFS=$' \t\n'; set +f

MISSING=()
for i in "${!FEATURES[@]}"; do
  name=${FEATURES[i]}
  kebab=${KEBAB_LIST[i]}

  # Match if any existing doc basename contains the kebab root or vice-versa
  # (handles variants: RaceAge → race-age-system, Warg → warg-combat, TroopWeight → troop-weight-system)
  found=false
  for doc in "${DOC_LIST[@]}"; do
    [[ -z "$doc" ]] && continue
    if [[ "$doc" == "$kebab" || "$doc" == "${kebab}-system" || "$doc" == "${kebab}"* || "$kebab" == *"$doc"* ]]; then
      found=true
      break
    fi
  done

  [[ "$found" == false ]] && MISSING+=("$name → docs/features/${kebab}.md")
done

if [[ ${#MISSING[@]} -gt 0 ]]; then
  echo ""
  echo "=== Feature Docs Gap Detected ==="
  echo "Features without matching docs/features/<name>.md (CLAUDE.md requires one per feature):"
  for entry in "${MISSING[@]}"; do
    echo "  - $entry"
  done
  echo ""
  echo "Fix: write docs/features/<name>.md using docs/features/TEMPLATE.md, or add an alias doc."
  echo "================================="
fi

exit 0