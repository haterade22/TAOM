---
name: feedback_sprite_atlas_cleanup
description: When moving sprites between atlas categories, delete old PNGs from the game install folder too — not just the repo
type: feedback
---

When migrating sprites from one atlas category to another (e.g., ui_taom → ui_taom_career_system), the old PNGs in the game install folder must be manually deleted. The build only copies new files — it doesn't delete removed ones. The sprite generator scans all category folders on disk, so duplicate PNGs across categories cause "duplicate key" crashes in AddSpritePart.

**Why:** Spent 3 commits debugging this. The repo was clean but the game install at `E:\Steam\...\Modules\TAOM\GUI\SpriteParts\ui_taom\CareerSystem\` still had the old PNGs.

**How to apply:** When moving sprites between categories: (1) move in repo, (2) delete old folder from game install, (3) then run sprite generator.
