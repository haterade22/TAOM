#!/usr/bin/env bash
# audit-review-counter.sh
# Recompute Codex-review counters from REVIEW-LOG.md and check that
# AGENTS.md's "Lessons From Prior Reviews (N reviews, M bugs found)"
# header matches.
#
# Why: in the Tier 1 fix chain we shipped AGENTS.md with "26 reviews,
# 64 bugs" while REVIEW-LOG.md said 65. Manual arithmetic errors are
# the failure mode. This script makes counters mechanical.
#
# Usage:
#   bash tools/audit-review-counter.sh        # report only
#   bash tools/audit-review-counter.sh --fix  # update AGENTS.md in place

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG="$REPO_ROOT/docs/reviews/REVIEW-LOG.md"
AGENTS="$REPO_ROOT/AGENTS.md"

[[ ! -f "$LOG" ]] && { echo "REVIEW-LOG.md not found at $LOG"; exit 1; }
[[ ! -f "$AGENTS" ]] && { echo "AGENTS.md not found at $AGENTS"; exit 1; }

# Extract the totals line from REVIEW-LOG.md. Tolerate minor wording drift
# but anchor on a summary keyword (total | so far | conducted | completed)
# so we don't accidentally match a per-row entry that happens to mention
# numbers and "review" / "bug".
# Anchor on the canonical summary line: contains "reviews total" AND
# "across codebase". This avoids matching narrative paragraphs that quote
# old totals in code spans (caught during testing where the validator
# picked up a quoted "27 Codex reviews total, 71 bugs" from a review
# narrative instead of the current 28/77 summary line).
LOG_TOTAL=$(grep -iE '[0-9]+[[:space:]]+(codex[[:space:]]+)?reviews?[[:space:]]+(total|so far|conducted|completed).*across codebase' "$LOG" | tail -1)
if [[ -z "$LOG_TOTAL" ]]; then
    # Fallback: any summary-keyword match (prior canonical wording).
    LOG_TOTAL=$(grep -iE '[0-9]+[[:space:]]+(codex[[:space:]]+)?reviews?[[:space:]]+(total|so far|conducted|completed)' "$LOG" | head -1)
fi
if [[ -z "$LOG_TOTAL" ]]; then
    echo "[audit-review-counter] Could not find totals line in REVIEW-LOG.md."
    echo "Expected pattern: a line with 'N reviews total ... M bugs' (any wording)."
    exit 1
fi

# Extract numbers anchored on their keywords — NOT just first-N numbers from
# the line, because lines like "19-27. 27 Codex reviews total, 71 bugs" have
# unrelated numbers (range markers) earlier.
LOG_REVIEWS=$(echo "$LOG_TOTAL" | grep -oiE '[0-9]+[[:space:]]+(codex[[:space:]]+)?reviews?' | head -1 | grep -oE '^[0-9]+')
LOG_BUGS=$(echo "$LOG_TOTAL" | grep -oiE '[0-9]+[[:space:]]+bugs?' | head -1 | grep -oE '^[0-9]+')

# Extract the AGENTS.md header. Same flexibility: parens optional, wording loose.
AGENTS_HEADER=$(grep -iE "^### Lessons.*[0-9]+[[:space:]]+reviews?.*[0-9]+[[:space:]]+bugs?" "$AGENTS")
if [[ -z "$AGENTS_HEADER" ]]; then
    echo "[audit-review-counter] Could not find counter header in AGENTS.md."
    echo "Expected: '### Lessons From Prior Reviews (N reviews, M bugs found)' or similar."
    exit 1
fi

AGENTS_REVIEWS=$(echo "$AGENTS_HEADER" | grep -oiE '[0-9]+[[:space:]]+reviews?' | head -1 | grep -oE '^[0-9]+')
AGENTS_BUGS=$(echo "$AGENTS_HEADER" | grep -oiE '[0-9]+[[:space:]]+bugs?' | head -1 | grep -oE '^[0-9]+')

echo "REVIEW-LOG.md: $LOG_REVIEWS reviews, $LOG_BUGS bugs"
echo "AGENTS.md:    $AGENTS_REVIEWS reviews, $AGENTS_BUGS bugs"

if [[ "$LOG_REVIEWS" == "$AGENTS_REVIEWS" && "$LOG_BUGS" == "$AGENTS_BUGS" ]]; then
    echo "OK: counters match."
    exit 0
fi

# Mismatch.
echo
echo "MISMATCH detected:"
[[ "$LOG_REVIEWS" != "$AGENTS_REVIEWS" ]] && echo "  reviews: AGENTS=$AGENTS_REVIEWS LOG=$LOG_REVIEWS (delta $((LOG_REVIEWS - AGENTS_REVIEWS)))"
[[ "$LOG_BUGS" != "$AGENTS_BUGS" ]] && echo "  bugs:    AGENTS=$AGENTS_BUGS LOG=$LOG_BUGS (delta $((LOG_BUGS - AGENTS_BUGS)))"

if [[ "${1:-}" == "--fix" ]]; then
    NEW_HEADER="### Lessons From Prior Reviews ($LOG_REVIEWS reviews, $LOG_BUGS bugs found)"
    OLD_HEADER="### Lessons From Prior Reviews ($AGENTS_REVIEWS reviews, $AGENTS_BUGS bugs found)"
    # Convert Git Bash path to Windows path so native Python can open it.
    AGENTS_NATIVE="$AGENTS"
    if command -v cygpath >/dev/null 2>&1; then
        AGENTS_NATIVE=$(cygpath -w "$AGENTS" 2>/dev/null || echo "$AGENTS")
    fi
    python3 -c "
import sys
p = sys.argv[1]
old = sys.argv[2]
new = sys.argv[3]
s = open(p, encoding='utf-8').read()
if old not in s:
    sys.exit('header not found in file: ' + old)
open(p, 'w', encoding='utf-8').write(s.replace(old, new, 1))
print('AGENTS.md updated:', new)
" "$AGENTS_NATIVE" "$OLD_HEADER" "$NEW_HEADER"
    exit 0
fi

echo
echo "Re-run with --fix to update AGENTS.md in place."
exit 1
