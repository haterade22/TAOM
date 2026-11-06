# RCA: Lothlorien and Snow Orc troop trees (deep review, 2026-11-05)

## Top line

KEYforce's branch gave Lothlorien its own troop trees and Gundabad a Snow Orc tree. A first static check
on 2026-11-05 said the tree was registered and wired. That was true for the troop files and false for the
promise they exist to keep. A tier 1 `/deep-review` (Codex at ultra, six Claude lenses) then found one
CRITICAL, three HIGH and about a dozen MEDIUM problems. One of the HIGH ones was caused by the follow-up
work itself: the enlistment rosters were reseeded before the militia troops existed.

Every confirmed finding below is fixed, recorded as a decision, or listed as a follow-up with its reason.

Scope reviewed: merge commit `e09f04ef8` (base `974e60bb2`) plus the uncommitted follow-up in the working tree.
Codex log: `docs/reviews/raw/codex-prereview-lorien-snow-orcs-2026-11-05.md`.

## Findings

| # | Sev | Finding | Category | Why missed | Status |
|---|---|---|---|---|---|
| F1 | CRITICAL | The 23 `lorien_armors` packages had no `RuntimeDataCache` entry in the live Armory. The client skips a mesh package that has no entry (observed; the native code that decides it is not traced), so the Lorien pieces would draw nothing, on troops, three lords and the crown guard. | Unversioned asset not loadable | The contributor's notes say the cache check could not run on his machine. The first check looked at XML references and mesh names, which pass without it. `check_rdc_entries.py` is not in any gate that runs on a merge. | Fixed: 53 entries copied from the lotraom-assets mirror; the checker reports `without-rdc=0`. |
| F2 | HIGH | The Lothlorien culture block still bound Rivendell's default army, three patrols, militia, rebels and vassal reward. Joining Lothlorien paid out Rivendell troops. | Half-finished swap | `CulturePartyTemplateTests` allows sharing another culture's TAOM templates on purpose. No test pinned "Lothlorien owns its military templates". | Fixed: 7 Lorien templates, bound; `LothlorienOwnTreeDataTests` pins them. |
| F3 | HIGH | Lorien enlistment rosters were seeded before the militia existed: 9 of 14 cells, so an archer recruit got a spear and shield and no bow. | Ordering of generator runs | My own sequencing. The enlistment generator has no `--check`, so no gate compares the shipped rosters with the generator. | Fixed: reseeded after the final troop file; 14 cells; the archer recruit carries `ladder_lothlorien_bow_t2`. |
| F4 | HIGH | The changed default party templates of clan Grishuk and the Wardens are saved state (`Clan._defaultPartyTemplate`), and the engine loads clan definitions only for a new campaign. Lord skills and kit are saved too. | Save compatibility | The change was read as a data edit. | Decision (Mike, 2026-11-05): new campaigns only; stated in the feature doc and the commit body. |
| F5 | MEDIUM | Eight Lorien troops paired a shield with the two-handed `he_sword` (`requires_no_shield`). | Engine rule in data | `audit_polearm_shield_parity.py` fails only for polearms by default; the two-handed class was meant to join once #450 closed and never did. | Fixed in the generator; the strict audit lists no Lorien or Snow Orc troop. |
| F6 | MEDIUM | Seven lord-only armour pieces were `is_merchandise="true"`, so markets of level 2 and 3 could stock them. | Generator intent not enforced | The generator docstring said "for lords" and nothing wrote it. | Fixed in the generator and the live Armory (backups beside the files). |
| F7 | LOW (rated MEDIUM here at first) | All 19 Lorien chests carried `has_gender_variations="true"` with no female mesh. | Generator flag | No gate runs `audit_gender_variation_flags.py`, and the audit itself exits 0 on the defect it reports. | Fixed in the generator and the live Armory; the audit reports 0. Correction from the 2026-11-06 deep review: a mesh lookup that finds no `_female` or `_converted` variant falls back to the base mesh and logs nothing, so `true` was already harmless here and the flip changed nothing a player sees. |
| F8 | MEDIUM | No troop weight rows for the 24 Lorien troops of level 31 and above; they weighed 1.0 each. | Missing consumer | The weight test checks only troops that have a row. | Fixed: 24 rows by the level rule; a test pins them. |
| F9 | MEDIUM | Career kits and `build_clan_specs.py` still mapped Lothlorien to Rivendell. | Alias outlived its reason | The alias tables are separate from the troop files. | Fixed for career kits (6 roster ids, 26 changed lines) and clan specs. See the follow-ups for the character-creation kit. |
| F10 | MEDIUM | The armour generators wrote the live Armory with no backup, no parse and no root check; the troop generators wrote silent zero skills for a new id and bare line feeds. | Tool safety | The generators were accepted as the contributor wrote them. | Fixed: `tools/_generated_files.py` (backup, parse, `--check`), `ensure_exists`, a zero-skill warning, a parse check, CRLF. |
| F11 | MEDIUM | The merge raised the external loc baseline by 98 per language, and 7 more rows made the gate fail. | Ratchet absorbed debt | The baseline was regenerated, not translated. | Decision (Mike, 2026-11-05): raise the baseline, translate later; row added to #763. |
| F12 | MEDIUM | No test, feature doc, feature-map row or issue for the feature. | Documentation duty | The branch arrived as a contributor change. | Test, doc and map row added. No issue filed (see follow-ups). |
| F13 | LOW | Stale text: about 14 comments and docs said Lothlorien fields Rivendell's troops; two comments gave the wrong owner of Mount Gram; half of the horse archers drew an infantry helmet; four bare line feeds. | Prose outlived the data | Searches covered code, not prose. | Fixed. |

## Root cause pattern: the files were checked, the promise was not

F1, F2, F3, F8 and F9 share one cause. Every gate that ran checks that a reference resolves or a file
is well formed. None checks who owns it. A tree can load, validate and pass more than 35,000 tests while its party
templates, its enlistment kits, its weights and its art cache still belong to someone else. The fix that
generalises is a test that states the promise, which is what `LothlorienOwnTreeDataTests` does for this
culture.

A second cause is specific to the follow-up: generator runs depend on each other (troops, then skills,
then enlistment). F3 shipped because the order was kept in my head and no command compared the output
with the generator.

## Why each pass missed these

- **First static check (mine):** read `SubModule.xml`, resolved Item references, ran the validator and the
  armory ref audit. All of those pass without art cache entries, without owned templates and without weights.
- **Codex:** found F2, F3, F4, F6 and the career kit alias with quotes from the installed DLLs. It did not look
  at `RuntimeDataCache`, weights or the live Armory file attributes beyond the lord-only pieces.
- **XML lens (half A):** found F1 by running the cache checker on the new art folder.
- **Data flow lens:** found the weights, the clan spec alias and the villager path by walking every consumer.
- **Tooling lens:** found F5's gate gap, F7, F10 and the pricing conflict by running the generators into scratch copies.
- **Engine lens:** proved that mounted archers can shoot and that the shield pairing is real data.
- **Skipped on purpose:** Standards, Efficiency and Design. The C# change is data tables only.

## Follow-ups and decisions left open

FOLLOW-UP: the `RuntimeDataCache` check is not part of the Armory audit. Seven packages outside this change also have no entry (Race 4, creature 2, northern 1), so a gate needs an allowlist first. no issue filed (public action, waits for the maintainer's word).

FOLLOW-UP: move the two-handed weapon class into the failing set of `audit_polearm_shield_parity.py` and let #526 ratchet the 122 existing rosters. no issue filed (public action, waits for the maintainer's word).

FOLLOW-UP: `generate_lothlorien_troops.py`, `generate_snow_orc_troops.py` and `generate_enlistment_rosters.py` have no `--check`, and the Lorien troop generator is not in `tools/tests/test_generator_regions_in_sync.py`. no issue filed (public action, waits for the maintainer's word).

RESOLVED (2026-11-06, Mike): the generator keeps KEYforce's values. `rebalance_armor.py` skips `sk_ll_` items through `is_generator_owned`, pinned by `test_a_generator_owned_item_is_left_to_its_generator`.

FOLLOW-UP: the character-creation retainer and mercenary kits (`taom_char_creation_equipment.xml`, `CULTURE_TO_FOLDER` in `generate_starter_kit.py`) and `tools/oneoff/generate_tavern_mercenaries.py` still use Rivendell gear for Lothlorien. no issue filed (public action, waits for the maintainer's word).

RESOLVED (2026-11-06, Mike): `villager_lothlorien` now upgrades into `lorien_scout`, and all 11 Lorien lords wear Lorien kit in battle (3 of the 4 unworn lord pieces are now worn).

FOLLOW-UP: Elite Emissary and `troop_resource_costs.xml` have no Lothlorien rows, and three unworn Mallorn medium pieces are classed medium although their stats are elite. These are maintainer decisions. Listed under "Not solved here" in #1085.

RESOLVED (2026-11-06, Mike): the Lorien bow line carries a `names` override, and the eight items are labelled "[Lórien] Longbow" in the live Armory.

FOLLOW-UP: the lotraom-assets mirror lacks `ladder_lothlorien_bow_t2` and `_t3` (the maintainer syncs the mirror). Listed under "Not solved here" in #1085.

FOLLOW-UP: the Snow Orc party template is sized 229 against a target of 260, `audit_gender_variation_flags.py` exits 0 on the defect it reports, and the Snow Orc armour items are in the live Armory and in the lotraom-assets mirror (commit `274f7c18f`). no issue filed (public action, waits for the maintainer's word).

## Feedback to codify

Added to `docs/reviews/lessons/data-content-cultures.md`: pin the promise of a new troop tree, and check the art
cache entries before saying new Armory art works.

## Round 2 (2026-11-06): what I got wrong, and the process that works

Round 1 above is the deep review and its fixes. Round 2 is the follow-up work (issue #1085: the villager, the lord kits, the armour owner, the bow names, then `covers_legs`) and a look back at my own mistakes across both days. Each row names the mistake, what it cost and the process that would have avoided it.

| # | What I got wrong | Cost | Right process |
|---|---|---|---|
| R1 | I told Mike the Lorien armour was in no repo and would break a clean checkout. It lives in the lotraom-assets repo, which he syncs into the live Armory by hand. | A wrong risk report and a correction from Mike. | Look for the source repo before calling anything unversioned: `git log -- <path>` in `E:\repos\lotraom-assets`. Never edit the mirror. |
| R2 | The first validator run in the review worktree read trunk data, because the tool derives its data root from its own location. | A clean result that described the wrong tree. | Run the tool from the checkout you changed, or pass its data root explicitly. |
| R3 | I seeded the Lorien enlistment rosters before the militia troops existed (9 of 14 cells), then ran `generate_enlistment_rosters.py --apply` without `--culture`, which rewrote about 6,700 lines in other cultures. | A reseed, a revert of the file and a deleted backup. | Troops, then skills, then enlistment, with `--culture <id> --reseed --apply`. The order is in [new-culture-authoring.md](../ai-includes/new-culture-authoring.md) 3d. |
| R4 | I removed `armour_from="rivendell"` from the culture marketplace config. `ArmourAcquisitionShippedDataTests` failed (Deep Seam material, donor mapping). | A revert, and a limit now listed in the feature doc. | Run the shipped-data tests before treating a data alias as dead. |
| R5 | My first fix for the shield and two-handed sword pairs used weak one-handed swords. Six Lorien troops fell under the melee ladder band (135 to 141 warnings). | A second pass with in-band swords by tier. | Take the replacement from `fix_melee_ladder.py --culture <id>` output, not from memory. |
| R6 | The Lorien armour generator wrote `covers_legs="false"` on all eight leg pieces, and my review of that generator never listed the attribute. | The leg skin mesh was drawn under every Lorien greave. Whether the shin showed depends on each mesh, and no in-game sighting is recorded. Found only when Mike asked for the fix. | Diff the attributes a generator writes per slot against a sibling culture's items before the first `--apply`. `test_generate_lothlorien_armor.py` pins it now. Uruk-hai shoes stay `false` on purpose (Mike, 2026-11-06). |
| R7 | My first `covers_legs` scan read four module folders and reported three vanilla leftovers. The whole `Modules` folder held four more (NavalDLC and `SandBoxCoreMP`). | A wrong "everything is done" for one turn. | A negative result names its scope, and for "every item" the scope is the whole install. |
| R8 | I started two suites as detached subshells, so the harness said "completed" while they ran, and a `pytest -x` baseline stopped at the first two failures and hid a third. | Two extra runs. | One background command that waits for both summary lines; a baseline without `-x`. |
| R9 | Round 2 (villager, five lord kit templates, the `rebalance_armor.py` skip, the bow labels) was committed without its own `/deep-review`, and the commit body did not say so. | A review gate CLAUDE.md asks for was skipped by default. | Review the new files, or write the waiver and its reason in the commit body. |
| R10 | Two sentences in the issue draft were mine and unchecked: that the Gundabad and Mirkwood villagers make the same level jump, and that the engine does not need adjacent levels. I read neither villager's level and no engine code. | The second sentence reached the public issue before I removed it. | Every factual sentence in an issue traces to a command or a file read this turn ([evidence-over-claims.md](../../.claude/rules/evidence-over-claims.md)). |
| R11 | I rewrote the five lord battle templates and called the lords done. `taom_lord_template_equipment.xml` is generated from the first battle set of the culture file, and its Lorien lord and ruler battle rosters kept Rivendell gold. The checklist I wrote (section 3d) did not name the file. | A new Lorien ruler, a rebel lord or a woman who comes of age as a lord would have worn Rivendell gold. | Trace consumers forward from the source file you changed (`grep -rn taom_equipment_sets_ tools/`), not from the diff. A derived file that cannot be regenerated safely is hand-ported and pinned by a test. |
| R12 | I set the Isengard starter boots to `covers_legs="true"` with a script that selected items by id; the decision was about a mesh, and three boots draw the Uruk-hai shoe mesh. The script also built a list meant to prove every changed item was a leg piece and never asserted it. | Three market items hid the shin under an open shoe. | Select by the property the decision names, assert the exact count, default to a dry run, and delete a guard nothing asserts. `tools/audit_leg_cover_flags.py` now enforces the rule. |
| R13 | I wrote counts and a location without a command behind them: "26 roster ids" (the file has 6 rosters; 26 lines changed), "56 of the 97" (97 is Lorien plus Snow Orc), and "Snow Orc armour exists only in the live Armory" (the mirror has it). | Three wrong statements in docs, a comment and the public issue. | CLAUDE.md already says a count names the command that prints it. Run the command, then write the number. |

The deep review of round 2 (2026-11-06) found more: [rca-lothlorien-round2-2026-11-06.md](rca-lothlorien-round2-2026-11-06.md) holds its findings, causes and the decisions still owed.

## The workflow that worked

1. Verify the first claim at its source (the repo, the log, the file) before reporting it.
2. Fix a generated value in its generator, apply it with the generator's own backup, and pin it with a test that fails when the generator regresses.
3. Run the whole suite and the data gates for a baseline, and attribute every failing test to a cause. Here: one game-build pin on this machine, and three tests that read another session's uncommitted Arthedain and Cardolan files.
4. Commit explicit paths only. Stage a shared file by hunk (`git apply --cached --unidiff-zero` of the filtered diff), never wholesale.
5. After the commit, `git fetch` and read `git rev-list --left-right --count HEAD...origin/<branch>`: the left number is what you are ahead, the right number what you are behind. When origin is ahead and another session has dirty files, merge origin instead of rebasing, because a rebase needs a stash and a stash of another session's work is never ours to take. Rerun the suites on the merged tree before the push.
6. Close the issue with the commit hash and `triage-needs-ingame` when the in-game check is still owed.

## Still owed after round 2

- The `/deep-review` of the round 2 files (R9) ran on 2026-11-06 (tier 1: Codex at `ultra` and four lenses). Its record is [rca-lothlorien-round2-2026-11-06.md](rca-lothlorien-round2-2026-11-06.md).
- The in-game checks in [the feature doc](../features/lothlorien-snow-orcs.md) "How to verify in game", steps 8 to 11, after a full restart and a new campaign.
- The lotraom-assets mirror lacks `ladder_lothlorien_bow_t2` and `_t3`, and the live Armory edits to the Lorien, Isengard and Mordor leg pieces and the Uruk-hai shoes are not in git. A reinstall or an art drop reverts them. The mirror is Mike's to sync.
- FOLLOW-UP: the 7 vanilla leg pieces with `covers_legs="false"` (`southern_moccasins`, `ladys_shoe`, `belted_leather_boots` in `SandBoxCore` and `SandBoxCoreMP`, `northman_raider_boot` in NavalDLC) are left as they are. No issue filed (a TAOM-side override of a vanilla item id has no precedent here and needs research first; waits for Mike's word).
