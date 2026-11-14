# RCA: committing the banner picker sprite regeneration (#1272)

Review of the commit that brings the Modding Kit's sprite regeneration back into the repo after 163 orphan
sprite PNGs left the install. The change is `GUI/TAOMSpriteData.xml` and GUI texture packages only, so it
ran as tier 2: lens 5 (data flow) and lens 7 (XML) at `effort: "high"`. Mike declined the Codex pass.
The XML came back clean: it parses to the same 9,849 elements as HEAD, the engine loader
(`TaleWorlds.TwoDimension.SpriteData.LoadSpriteDataFromFile`) reads category children by name, so the moved
`<AlwaysLoad />` and the 26,600 removed blank lines change nothing at load. Every sprite name still resolves.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The Kit re-saved all 13 GUI `_tex.tpac` files. I copied only the 3 files I expected to change, so `ui_custom_fonts_1/2/3_tex.tpac` stayed at their Oct 28 headers in the repo while the install's caches were compiled for the new ones. The next build would have deployed the old headers over them, the "reverted header meets a recompiled cache" case in `banner-icon-generation.md` (Lens 5, trace 6) | Partial sync of generated output | I judged which files changed from their source PNGs, not from a diff of the generator's output folders. `tools/sync_sprite_bake.ps1` exists for exactly this and four docs name it, but `banner-icon-generation.md` Step 4b, the doc for this work, did not. I made the same hand copy earlier in the session. | Ran `sync_sprite_bake.ps1`; repo and install GauntletUI folders now match. Step 4b gains item 5: bring the bake back with the sync tool, never by hand, and prune first. |
| 2 | MED | Sprite part and PNG `22004` exist with no `banner_icons.xml` row; the audit checks icon to sprite but never sprite to icon (Lens 7) | Gate gap, pre-existing | Not introduced here: the PNG dates from March 2026. The orphan cleanup removed install-only PNGs, and this one is tracked in the repo. | FOLLOW-UP #1274: delete it, regenerate, and add the reverse audit finding. |
| 3 | LOW | The blank lines in `TAOMSpriteData.xml` grew over six commits (749 to 26,601), all from one artist's commits; no tool in the repo writes the file (Lens 5, trace 7) | Out-of-repo tooling | Blank lines do not change the load, so no gate looks at layout. | FOLLOW-UP no issue: the producer is personal tooling outside the repo, and the Kit's regeneration removes the lines; ask the artist which flow edits the manifest before adding a gate. |

## Why each lens missed or caught these

- Lens 5 caught finding 1 because its brief asked whether the shipped headers match the caches, and it diffed
  the generator's output folders instead of trusting my list of three files.
- Lens 7 checked the files I named and their gates; it found finding 2 by matching all 236 sprite parts against
  the 235 icon rows.
- The gates (`data_gates.py`, `pack_banner_sheets.py --audit`, `check_rdc_entries.py`) all passed with the font
  headers stale: no gate compares the repo's GauntletUI packages with the install's.

## Lesson

Appended to `docs/reviews/lessons/build-tooling-workflow.md`: bring a generator's output back with the tool that
mirrors its folders, never file by file.
