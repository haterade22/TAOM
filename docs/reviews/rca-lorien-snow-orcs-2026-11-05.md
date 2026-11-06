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
| F1 | CRITICAL | The 23 `lorien_armors` packages had no `RuntimeDataCache` entry in the live Armory. The client skips such a package whole, so all 77 Lorien pieces would draw nothing, on troops, three lords and the crown guard. | Unversioned asset not loadable | The contributor's notes say the cache check could not run on his machine. The first check looked at XML references and mesh names, which pass without it. `check_rdc_entries.py` is not in any gate that runs on a merge. | Fixed: 53 entries copied from the lotraom-assets mirror; the checker reports `without-rdc=0`. |
| F2 | HIGH | The Lothlorien culture block still bound Rivendell's default army, three patrols, militia, rebels and vassal reward. Joining Lothlorien paid out Rivendell troops. | Half-finished swap | `CulturePartyTemplateTests` allows sharing another culture's TAOM templates on purpose. No test pinned "Lothlorien owns its military templates". | Fixed: 7 Lorien templates, bound; `LothlorienOwnTreeDataTests` pins them. |
| F3 | HIGH | Lorien enlistment rosters were seeded before the militia existed: 9 of 14 cells, so an archer recruit got a spear and shield and no bow. | Ordering of generator runs | My own sequencing. The enlistment generator has no `--check`, so no gate compares the shipped rosters with the generator. | Fixed: reseeded after the final troop file; 14 cells; the archer recruit carries `ladder_lothlorien_bow_t2`. |
| F4 | HIGH | The changed default party templates of clan Grishuk and the Wardens are saved state (`Clan._defaultPartyTemplate`), and the engine loads clan definitions only for a new campaign. Lord skills and kit are saved too. | Save compatibility | The change was read as a data edit. | Decision (Mike, 2026-11-05): new campaigns only; stated in the feature doc and the commit body. |
| F5 | MEDIUM | Eight Lorien troops paired a shield with the two-handed `he_sword` (`requires_no_shield`). | Engine rule in data | `audit_polearm_shield_parity.py` fails only for polearms by default; the two-handed class was meant to join once #450 closed and never did. | Fixed in the generator; the strict audit lists no Lorien or Snow Orc troop. |
| F6 | MEDIUM | Seven lord-only armour pieces were `is_merchandise="true"`, so markets of level 2 and 3 could stock them. | Generator intent not enforced | The generator docstring said "for lords" and nothing wrote it. | Fixed in the generator and the live Armory (backups beside the files). |
| F7 | MEDIUM | All 19 Lorien chests carried `has_gender_variations="true"` with no female mesh. | Generator flag | No gate runs `audit_gender_variation_flags.py`, and the audit itself exits 0 on the defect it reports. | Fixed in the generator and the live Armory; the audit reports 0. |
| F8 | MEDIUM | No troop weight rows for the 24 Lorien troops of level 31 and above; they weighed 1.0 each. | Missing consumer | The weight test checks only troops that have a row. | Fixed: 24 rows by the level rule; a test pins them. |
| F9 | MEDIUM | Career kits and `build_clan_specs.py` still mapped Lothlorien to Rivendell. | Alias outlived its reason | The alias tables are separate from the troop files. | Fixed for career kits (26 roster ids regenerated) and clan specs. See the follow-ups for the character-creation kit. |
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

FOLLOW-UP: the Snow Orc party template is sized 229 against a target of 260, `audit_gender_variation_flags.py` exits 0 on the defect it reports, and the Snow Orc armour items exist only in the live Armory. no issue filed (public action, waits for the maintainer's word).

## Feedback to codify

Added to `docs/reviews/lessons/data-content-cultures.md`: pin the promise of a new troop tree, and check the art
cache entries before saying new Armory art works.
