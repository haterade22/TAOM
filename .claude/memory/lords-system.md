---
name: lords-system
description: Lords rebalancing system — two files (XSLT + XML), 914 total lords, 12 archetypes, 13 cultures, legendary tier
type: project
---

Lords are split across two files:
- `Main/_Module/ModuleData/lords.xslt` — 396 templates (389 active + 7 dead) transforming vanilla SandBox lords
- `Main/_Module/ModuleData/characters/lords.xml` — 525 custom TAOM lords (NOT in SubModule.xml yet, staged)

**Why:** Vanilla lords are overridden via XSLT identity transform; custom lords (new factions, dwarves, elves, orcs) are direct XML additions.

**How to apply:**
- `tools/complete_lords_xslt.py` — Phase 1: makes all vanilla attributes explicit in XSLT (no more passthrough)
- `tools/rebalance_lords.py` — Phase 2: balances skills for both files using baseline + cultural modifier + age scaling
- Both tools have `--dry-run`, `--apply`, `--export-csv` modes
- Run rebalance after any lord attribute changes (culture, age, skill_template)

**Archetype detection** from `skill_template` attribute: ruler, warrior_knight, warrior_infantry, warrior_ranged, tactician, siege_engineer, politician, manager, spymaster, scholar, trader, dandy

**Culture mapping:** empire→dunland, sturgia→dale, aserai→harad, vlandia→rohan, battania→mirkwood, khuzait→rhun, plus 7 custom (dolguldur, erebor, gundabad, isengard, lothlorien, rivendell, umbar)

**Legendary lords** (10): Nazgul + Sauron + Witch-King, detected by ID set + name patterns, get 2.5x ruler baseline
