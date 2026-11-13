# RCA: language pass for backlog #763 (deep review, 2026-11-12)

## Top-line

The project lead's language pass translated every row that still showed English in all 12 languages and all three
modules (the repo, the live TAOM_Map and the live LOTRLOME_Armory), on the lead's Anthropic key. It also pruned two
unused keys, relaxed nine wording tests from English equality to placeholder checks, and changed the writing-system
gate and a Lindon data test so that translated rows pass them. A tier 1 `/deep-review` ran lenses 5 Data flow,
2 Engine compatibility, 7 XML and Tooling at `effort: max`. They reported 2 HIGH, 9 MEDIUM and 7 LOW items. I checked
the row, cache or code behind each claim before acting. Both HIGH items and every MEDIUM item that changes what a
player reads are fixed in this change. A second wave (lenses 1, 3, 4 and 6 at `effort: "high"`) reviewed the fixes;
its results are below. Codex was not run: its usage is exhausted (the maintainer, 2026-11-12), so no Codex pass is owed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| H1 | HIGH | The `Identifier` mask that I added to `LanguageTextIntegrityTests.WritingSystemProblems` blanked any camelCase or underscore token before the word checks. CNt `TAOM_LD_Ability_Summon_Isengard` = "艾辛格的烏魯克haiOops" matched `[a-z]+[A-Z]...` and passed both shipped-row gates. Tooling, Lens 7 F2. | A gate widened past a real defect | The mask was written to clear five false positives (file names that #763 says to keep). I added only clean DataRows, so the test passed the two-edit test in one direction only. The 15:23 run had reported the row; the mask hid it at the next run. | The mask blanks a token only when a registered English row holds the same token. Two damaged DataRows (`haiOops`, a bare key) went RED first. The row and its cache entry are now "艾辛格的強獸人". Lesson added. |
| H2 | HIGH | 85 BR and 72 IT names lost their grammar tag. A translated name led by a head noun the rules did not know ("Forte", "Salão", "Rocca") was undecided, so `generate_name_tags.py --write` dropped its id and `name_tags.py` wrote no tag. BR then prints the wrong article ("A Salões de Thorin") and IT drops its preposition. Lens 7 F1, Lens 2 Finding 1. | A generated table shrinks and its gates compare against the table | Both tag gates (`--check`, `--require-tags`) check the rows against the table, not the table against the names. I read "PASS" and did not compare the tagged count with the count before the pass (1,265). | 64 BR and 49 IT head nouns, three proper prefixes and three pins; both languages are back to 1,265 of 1,265 live names, and 2 BR and 3 IT names stay undecided (all `exclude`). A gate that fails on a rise in undecided names is a follow-up. Lesson added. |
| M1 | MEDIUM | Harwan had four forms in CNs and three in CNt, against #763's note (CNs 哈尔万, CNt 哈爾萬): 59 CNs and 15 CNt rows. Lens 7 F3. | A per-key note not applied | The translator has no glossary; the notes reach the model only through the English. | Replaced in rows and caches, only where the English names Harwan. A glossary is a follow-up. |
| M2 | MEDIUM | The Fords of War key hint words Push, Hold, Fall back and Seize differently from the order buttons in 9 languages. Lens 7 F4. | Same as M1 | Same as M1. | The hint now copies the `TAOM_FOW_Order_*` words. |
| M3 | MEDIUM | RU and PL Long Memory put an undeclinable `{VILLAGE}` where the genitive is needed. Lens 7 F5. | Same as M1 | Same as M1. | "Жители деревни {VILLAGE}", "Mieszkańcy wioski {VILLAGE}". |
| M4 | MEDIUM | PL Bonds of Battle used a masculine verb for the player. Lens 7 F6. | Same as M1 | Same as M1. | "Walka ramię w ramię: {LORD}." and "Walka przeciwko: {LORD}." |
| M5 | MEDIUM | 34 TAOM_Map names (DG and M villages) showed an old placeholder with the id ("Aldeia do Castelo DG1_1") in 11 languages. Present before this pass. Lens 7 F7. | A stale row the translator cannot see | The translator selects only rows that equal the current English. | The rows were reset to English, their cache entries dropped, and the translator translated them. A gate for id tokens in names is a follow-up. |
| M6 | MEDIUM | `test_lindon_clan_expansion.py` accepted the old name: its Latin-script check passed any row that held "Harlond", and the old name was "Harlond". Tooling. | A relaxed test that no longer fails on the regression it guards | I relaxed the check to accept translations and did not run it against the old state. | A renamed row must differ from its namesake's row and from the old English name, in all 12 languages. A patched KO row equal to its namesake and a patched JP row equal to the old "Forlond" each fail the test. Lens 4 found that the namesake check alone missed the old English name in CNs, CNt, JP, KO and RU. |
| M7 | MEDIUM | All 12 caches held the old "Weather Hills" translation for `castle_AN5`, while the rows were right. The next run that resets that row would write the old name back. Tooling. | Cache and row out of step | The cache is keyed by id with no English hash. | Each entry set from its row. A sweep of every key in every language found no other cache entry that differs from its translated row. |
| M8 | MEDIUM | `check_stale_loc_rows.py` reads only staged changes, so a committed English edit (the 72715c417 kind) or a row that holds an older English text is not found. Lens 5. | A gate with a narrow input | Out of this change: the pass found and reset 31 such keys by hand. | Follow-up. |
| M9 | MEDIUM | `save_cache` truncates the cache file before it writes, and the CNt run crashed there with `Errno 22`. A crash during the write can leave a cache empty or partial. Tooling. | Non-atomic write | Out of this change: the translator is unchanged. | Follow-up: write a temp file, then `os.replace`. |
| L1 | LOW | 24 Seasoned Hosts hints write a decimal comma where #999 says to keep the numbers verbatim. What MCM shows is unverified. Lens 7 L1. | Per-key note | Same as M1. | Owed in game. |
| L2 | LOW | KO `aom_lord_LN3_4_name` read "님로델" (Nimrodel) for "Nimlothel". Lens 7 L2. | Model error | No gate compares names across keys. | 님로셀, in line with 님로시엘 and 님로손. |
| L3 | LOW | "Broadbeam" has up to six renderings per language. Lens 7 L3. | No glossary | Same as M1. | Follow-up with the glossary. |
| L4 | LOW | Some Latin-script names in CJK and RU came from the cache, not the model, so "needs LLM: 0" does not mean every row is translated. Lens 7 L4. | Cache reuse | By design for names kept as written. | The maintainer's call. |
| L5 | LOW | Two `.bak_nametags_*` backups in the live TAOM_Map language folders. Lens 7 L5. | Hygiene | | Deleted after the pass. |
| L6 | LOW | `special-resources.md` said the two old keys were unused and their new keys untranslated. Lens 7 L6. | Doc drift | | A dated history line records the removal and the translation. |
| L7 | LOW | IT `taom_fui_weaknesses` read "Punti debolI". Present before this pass. Lens 7 L7. | Typo | | "Punti deboli". |

Agent accuracy: Lens 2 rated H2 MEDIUM and Lens 7 rated it HIGH. Both described the same mechanism and the same
counts, and I confirmed it with `generate_name_tags.py --list-undecided` (90 BR, 78 IT undecided). I used the HIGH
rating because the engine prints the wrong article with no error. Lens 7 gave line numbers for the PL rows of F5 and F6
that were swapped; the keys were right.

## Root-cause pattern: a gate that compares against a file the change also writes

H1 and H2 have one shape. Each gate checked the translated rows against something this change also produced: the
mask came from the same edit that added the rows it was meant to excuse, and the tag table came from the same run that
translated the names. Both gates passed because their reference moved with the data. The check that would have caught
each one compares with the state before the change: the tagged count before the pass, and a damaged row the old gate
reported.

## Why each lens that ran did not prevent these

The four first-wave lenses found every item above; my own checks before the launch did not. The writing-system run
at 15:23 did report the H1 row, and I wrote the mask after that run without re-reading its report. `--require-tags`
reported PASS for H2 with the count down by 66 names, and I read the verdict, not the count.

## Second wave

Lenses 1 Standards, 3 Efficiency and 6 Design reported no HIGH or MEDIUM item on the fixes. Lens 4 Completeness
reported 3 MEDIUM items. Two are fixed in this commit: the Lindon test now also rejects the old English names (see M6),
and `name-grammar-tags.md` has its counts, the `castle_AN5` note and a changelog entry brought up to date. The third is
that origin carries 6 English keys this pass could not see (`taom_hero_auras_mcm_*` and `taom_battlehud_mcm_colour_mode*`);
they are translated in a second commit after the rebase. The maintainer asked for the commit while 20 other pushes waited,
so the other second-wave proposals below are not applied in this commit; each is a follow-up.

- Lens 6 P1 (KEEP): one Python gate that runs `placeholders_match` over every translated repo row (about 233,000;
  0 mismatches in the lens's prototype), in place of the five copied `Tokens()` helpers, which drop repeat counts.
- Lens 6 P2 (KEEP): the BR and IT exclusions of `taom_namegiving_lore_deadmens_dike` and `_treegarth` say "still
  English", and this pass translated both rows; add head nouns Vala, Fossa and Giardino and drop the exclusions.
- Lens 6 P3 (KEEP): `generate_name_tags.py` lets a "still English" exclusion expire once the row is translated.
- Lens 6 P4 (KEEP): the Lindon test compares each row with the tracked cache instead of with its namesake's row. The
  namesake check rests on an unchecked claim that every old row equals its namesake's row.
- Lens 3 L1: one cached English parse for `EnglishIdentifiers` and `NoTranslatedString_MixesWritingSystems`.
- Lens 1: `TheNoRoomHint_IsGenderNeutral_InTheCodeAndEveryLanguageFile` claims more than it checks; one in-method
  comment in `MarshalWordingTests`; seven class summaries still say the #763 run will fill the rows.

## Follow-up

- FOLLOW-UP: no issue: a gate that fails when a language's undecided name count rises above a recorded baseline
  (H2). Belongs with #763's next pass.
- FOLLOW-UP: no issue: a gate for id tokens in translated names, `[A-Z]{1,3}\d+_\d+` absent from the English (M5).
- FOLLOW-UP: no issue: `check_stale_loc_rows.py` against a commit range and against rows that hold an older English
  text (M8).
- FOLLOW-UP: no issue: atomic `save_cache` and `write_back` in `translate_with_claude.py` (M9).
- FOLLOW-UP: no issue: a translator glossary for #763's per-key names (Harwan, Broadbeam, the FOW orders) (M1 to M4, L3).
- FOLLOW-UP: no issue: one placeholder gate over every row (count and conditional structure), in place of the five
  copied `Tokens()` helpers; `placeholders_match` on cache and override fills (Lens 2 Q2, Lens 7 Q2).
- FOLLOW-UP: no issue: the second-wave items above (Lens 6 P1 to P4, Lens 3 L1, Lens 1), and Lens 4's LOW items: an
  unanchored camelCase alternative in `Identifier`, test names that claim more than they check, two Harlond spellings in
  CNs and KO. (Lens 4's feature docs that still said "English until #763" are updated in the follow-up docs commit.)
- FOLLOW-UP: #1129: per-language copies of the live Armory `loc_lothlorien.xml` (85 rows per language) wait for the
  maintainer's word.

## Watch in game

- BR and IT notices that name a translated settlement or clan ("Os Salões de Thorin", "delle Sale di Thorin").
- CNt Long Defeat ability names, CNs and CNt Harad troop prefixes, the Fords of War key hint in DE and IT.
- Label fit: 1,645 of 5,326 keys are at least 1.4 times their English length (`loc_length_report.py --since HEAD`).

## Feedback memories to codify

None. The lessons below are cross-feature rules and live in the lessons file.
