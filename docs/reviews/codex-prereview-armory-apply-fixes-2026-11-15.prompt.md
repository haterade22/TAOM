# Codex re-review: fixes after the live Armory review (#1271, #1288), 2026-11-15

You are the reviewer (`.ai/roles/reviewer.md`). Review only: change no file in the repo or the game
install, run no `--apply`, no git command that changes anything. Report defects with evidence.

The first review of the written Armory is in `docs/reviews/raw/codex-prereview-armory-apply-2026-11-14.md`
(end of file). Since then, uncommitted in this worktree (`git status --short`, `git diff`) and in the
live Armory (`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory`):

1. A HIGH finding of that round was fixed: `tools/generate_lothlorien_armor.py` and
   `tools/generate_snow_orc_armor.py` priced their 97 pieces by the tier word in the id, so worn
   pieces sat below their wearers' band. A worn piece is now priced at the band of its lowest wearer
   (`rebalance_armor.worn_band`, shared with `_roster_first_plan`); an unworn piece keeps its id
   word; the Mallorn kit is one band above either. Both generators were then applied to the live
   Armory, and `armour_classes.xml` was regenerated. Check: every worn generator piece against
   `level_to_band` of its anchor in `tools/data/armor_roster_tiers.json`; no troop above
   64 / 77 / 69 / 59 (head, body, arm, leg) with the new values; no unworn piece changed; the
   generators' `--check` passes; the tests would fail on the old rule.
2. A new tool, `tools/apply_armour_cover_flags.py`, with its spec `tools/armour_cover_flags.json`
   (rules over a snapshot of every cover setting of all Armory armour items) and tests. It was run
   with `--apply --backup-tag covers-20261114`: eight settings on five crown and circlet items
   (`sk_ar_art_crown_king_a`, `sk_dwarf_dain_crown_a`, `rivendell_helmet_lord_circlet_silver`,
   `rivendell_helmet_lord_circletmask_silver`, `rivendell_helmet_lord_circletmask_silvergold`) now
   show all hair and the full beard. Check the tool: can `--apply` change anything but the listed
   attributes on the listed items; BOM and line endings; the backup name; a second run; `--check`
   exit codes 0, 1 and 2; `--snapshot` writing only the repo file; a rule that contradicts another;
   an attribute the engine reads that the tool does not hold. Compare the three live head armour
   files with their `.bak-covers-20261114` copies: only those eight attribute values differ.
3. `Main/_Module/ModuleData/troops/troops_umbar.xml` was restored to HEAD (the repair had given
   `umbar_elite_root0000` vanilla Aserai armour above the Umbar curve), and a full
   `generate_enlistment_rosters.py --apply` was reverted because it rewrote rosters that
   `promote_borrowed_cultures.py` owns. Check that the tree is consistent: `python tools/data_gates.py`,
   `python tools/validate_moduledata.py`, the generator sync tests.
4. Docs now state that the apply ran (`docs/features/armor-balance.md`, "Applied on 2026-11-14"):
   any number there that disagrees with the files, any claim of a step that did not run.

Output: findings by severity (P1, P2, P3) with file, item id, evidence and a fix; then what you
verified; then what you could not verify.
