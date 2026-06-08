---
name: migration-status
description: Check and summarize the v1.2 to v1.3 Bannerlord migration progress
---

# Migration Status Check

Read and summarize the current migration status from @docs/migration/TRACKING.md

## Report Format

1. **Overall Progress** — Percentage complete, total items remaining
2. **Completed Items** — Brief list of what's done
3. **Remaining Items** — Detailed list with priority assessment:
   - Settlement XML
   - Troop XML (2 files)
   - Item XML
   - Equipment XML
   - Code Changes (TBD scope)
4. **Blockers** — Any known blockers or dependencies between remaining items
5. **Recommended Next Steps** — What to tackle next based on dependencies and impact

Also check:
- @docs/migration/v1.3-api-changes.md for API differences
- @docs/migration/XML-SCHEMA-CHANGES.md for schema updates
