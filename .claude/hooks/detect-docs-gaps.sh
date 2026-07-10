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

# PascalCase → kebab-case (InitialChildGeneration → initial-child-generation)
pascal_to_kebab() {
  echo "$1" | sed -E 's/([a-z0-9])([A-Z])/\1-\2/g; s/([A-Z]+)([A-Z][a-z])/\1-\2/g' | tr '[:upper:]' '[:lower:]'
}

# Build flat list of existing doc basenames (without .md)
EXISTING_DOCS=$(find docs/features -maxdepth 1 -name '*.md' -not -name 'TEMPLATE.md' 2>/dev/null | sed 's|.*/||; s|\.md$||')

MISSING=()
for feature_dir in Main/Features/*/; do
  [[ ! -d "$feature_dir" ]] && continue
  name=$(basename "$feature_dir")
  kebab=$(pascal_to_kebab "$name")

  # Match if any existing doc basename contains the kebab root or vice-versa
  # (handles variants: RaceAge → race-age-system, Warg → warg-combat, TroopWeight → troop-weight-system)
  found=false
  while IFS= read -r doc; do
    [[ -z "$doc" ]] && continue
    if [[ "$doc" == "$kebab" || "$doc" == "${kebab}-system" || "$doc" == "${kebab}"* || "$kebab" == *"$doc"* ]]; then
      found=true
      break
    fi
  done <<< "$EXISTING_DOCS"

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