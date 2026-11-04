# Name Grammar Tags (Portuguese and Italian)

## Overview

TAOM's Brazilian Portuguese (BR) and Italian (IT) names for kingdoms, minor factions, clans, cultures and settlements start
with a grammar tag of the kind the game's own names carry (`{.NS}`, `{.MN}`, `{.MS}` and the like). The game's text code reads
that tag to choose an article, a preposition or a verb form, so a vanilla sentence that holds a TAOM name reads right in those
two languages. This is phase 1 of [#1023](https://github.com/haterade22/TAOM/issues/1023); French, German, Spanish, Russian
and Polish are not covered. Phase 1 includes the live TAOM_Map apply, done on 2026-10-26.

**The Italian half depends on #1026.** TAOM's `language_data.xml` files did not declare the text processors, so the Italian
processor never ran and the Italian tags had nothing to read. The fix is on trunk (commit `41aa2779b`, v2.0.35): TAOM's
`language_data.xml` files now declare each language's processor. Players get it only when a release also ships new TAOM_Map and
Armory packages, and the processor canary in the report header (see "How to verify in game") says whether the processor runs.

## Why This Exists

- **Vanilla behavior:** the game's own names carry a tag, with exceptions. `por_br_functions.xml` `GENDER` maps `{.MS}` 0,
  `{.FS}` 1, `{.MP}` 2, `{.FP}` 3, `{.NS}` 4 and `{.NP}` 5 and returns nothing for an untagged name, which the call then reads as
  the feminine form. Vanilla's BR towns and villages are `{.NS}`, its castles and its Empire kingdom short names `{.MS}`, its
  clans mostly `{.MP}`; its BR kingdom and culture names carry no tag at all. `ItalianTextProcessor` prints `{.di}`, `{.a}`,
  `{.da}`, `{.in}` and `{.su}` only for a tagged name, and its `GENDER` code maps `{.MN}` to 4 and `{.FN}` to 5. Vanilla's IT
  towns are `{.FN}`, its castles `{.MS}`, 79 of its 80 clans `{.MP}`.
- **TAOM requirement:** TAOM's names had no tag. In BR a vanilla call such as `{#GENDER(KINGDOM)}o {#}a {#}...` chose the
  feminine article ("Seu reino está em guerra com a Erebor"); in IT the preposition vanished ("dichiarerete guerra Mordor").
  The kept-rows file now lists 370 (language, id) pairs of vanilla keys, 280 of them in BR and IT, 301 for the reason
  `gender-tag` (`kept-vanilla-key-rows.txt`; the 2026-10-26 localization fix kept 369, and the BR clan row below made it 370).
- **Without this feature:** those pairs stay overridden, and every other vanilla row that shows a TAOM name in BR or IT keeps the
  wrong article or a missing preposition.

## Architecture

### Design Challenge

The tag has to reach the TextObject the vanilla code passes around, and it must not reach anything that prints the name raw. It
also must survive a translation run: the translator model never writes a tag, and the cache is shared by three modules.

### Solution Approach

The tag is added to the language files only. `tools/translation_name_tags.json` maps (language, id) to a tag, and
`tools/name_tags.py` adds it when a file is written. The translation cache and the overrides stay tag-free.

- **Plain proper name:** BR `{.NS}`, the form with no article. IT towns and villages `{.FN}`, as on vanilla's towns.
- **IT castles without a title take `{.MN}`.** The implied noun is "castello", masculine, as "regno" is for a kingdom. A castle
  led by a head noun keeps the noun's tag ("Castello di X" is `{.MS}`), and IT towns stay `{.FN}`. 146 IT castle tags moved from
  `{.FN}` to `{.MN}` on 2026-10-26, and a second live apply carried them to the live Italian file.
- **IT kingdoms, minor factions and clans take `{.MN}`, not `{.FN}`.** `{.MN}` and `{.FN}` both give the plain preposition, but
  the Italian `GENDER` code reads MN as masculine and FN as feminine, and 29 of the 30 vanilla IT selections differ between the
  two. A kingdom or clan name has an implied noun ("il regno", "il clan") that is masculine, so `{.FN}` would have given
  "Erebor è stata distrutta". A clan that is a plural people takes `{.MP}`, as vanilla's do.
- **A name led by a common noun** takes the noun's own tag: "Aldeia" `{.FS}`, "Acampamento" `{.MS}`, "Villaggio" `{.MS}`,
  "Insenatura" `{.FS}`, "Casa" `{.FS}`, "Regno" `{.MS}`. The longest phrase wins ("Alto Reino"), and a noun joined to the rest
  by hyphens is found through its first segment ("Prole-das-Rochas"). A plural noun takes a plural tag: BR "Gobelins",
  "Saqueadores" `{.MP}`; IT "Goblin", "Orchi", "Predoni" `{.MP}`. The noun tables hold 128 BR and 121 IT entries.
- **A culture name** is a plural people (`{.MP}`) or a place (plain).
- **A name that began with an article** lost it: "A Mão Branca" is now "Mão Branca" with `{.FS}`, "Os Lobos Vermelhos" is now
  "Lobos Vermelhos" with `{.MP}`, "Gli Spaccacranio" is "Spaccacranio" with `{.MP}`. The tag carries the article's gender and
  number, so a vanilla sentence adds its own article once. 58 BR and 59 IT names changed; their translation cache entries
  changed with them. The gender is a pin in the table.
- **Not tagged, listed for a human:** a row still in English ("House of Ravenhill", 40 per language), the Haradric titles
  "Taskralan" and "Châjaphân" (3 per language), and IT "Gondoriano", an adjective and not a noun.
- **Kingdom ruler titles and descriptions** are not names and carry no tag.
- **BR "clan eliminated":** vanilla's `str_clan_destroyed` (`WYHqMwwo`) leaves its fifth `GENDER` option empty, so a `{.NS}` clan
  would read with no verb. TAOM keeps a BR row for it with the fifth option filled ("foi eliminado"), registered like the other
  English-only overrides: an English row equal to vanilla's, and `BR WYHqMwwo gender-tag` in the kept-rows file.

### Component Diagram

```
tools/translation_name_tags.json   rules (curated) + tags (generated)
        |
tools/generate_name_tags.py        rules + inventory (live text for settlements) -> tags, lists undecided names
        |
tools/name_tags.py                 tag_row / retag_xml / plan_file (idempotent, parsed, planned before any write)
   /          |              \
rebuild_translation_files.py   translate_with_claude.py   python tools/name_tags.py
(TAOM files; TAOM_Map only      one write pass: cache and    (in-place pass, dry run by default)
 with --allow-live-write)       override fills tagged, fresh
                                model rows left plain

tools/name_tag_inventory.py        every name key by category, with BR and IT rows
tools/check_external_loc_coverage.py   reports the tags of the live TAOM_Map file (read only)

taom.print_kept_pairs <lang>       renders each kept pair in game, writes a report to Logs
```

## Configuration

### Config File: `tools/translation_name_tags.json`

| Field | Description |
|-------|-------------|
| `languages.<BR\|IT>.plain` | The proper-noun tag: `{.NS}` or `{.FN}` |
| `plain_by_category` | A different plain tag for a category (IT kingdoms, minor factions and clans: `{.MN}`) |
| `plain_by_kind` | A different plain tag for a settlement kind (IT castles: `{.MN}`) |
| `allowed` | The tags the language's text code reads |
| `head_nouns` | Common nouns (and phrases) that lead a name, with their tag. Curated |
| `proper_prefixes` | First words of proper names ("Barad", "Osgiliath"). Curated |
| `english_heads` | English common nouns; a row still equal to English and led by one is untranslated, not tagged. Curated |
| `articles` | Leading articles; a name led by one is left for a human, who drops the article and pins its gender |
| `exclude` | Ids that stay untagged on purpose, with the reason |
| `pins` | A human decision for one id, applied before every rule |
| `tags` | Generated: every id the rules decided |

### Current Values

2026-10-26, from the installed TAOM_Map and the repo (`python tools/name_tag_inventory.py`, `generate_name_tags.py`):

| Category | Name keys | BR tagged | IT tagged |
|----------|-----------|-----------|-----------|
| Kingdoms (name, short name, title) | 75 | 68 | 68 |
| Minor factions | 28 | 28 | 28 |
| Clans (clans.xml, spclans.xslt, culture clan lists) | 559 | 523 | 523 |
| Cultures | 25 | 25 | 24 |
| Settlements (live TAOM_Map) | 1086 | 1086 | 1086 |
| Lore town names (`LoreNames.cs`, the Namegiving feature) | 4 | 2 | 2 |

On 2026-10-27, after Ered Luin, `name_tag_inventory.py` counts 78 kingdom keys, 28 minor faction keys, 579 clan keys, 26
culture keys, 1140 live settlement names and 4 lore names (1855 in all), and `check_external_loc_coverage.py
--require-tags` reports all 1140 settlement names tagged in both languages. The other tagged columns were not re-measured.

Tagged rows in the repo language files: 674 BR (646 ids) and 673 IT (645 ids); an id counts once per file that holds it.

The four lore names are in the inventory. "Minas Ithil" and "Amon Lanc" are Sindarin names kept as written and take the plain
town tag. "Deadmen's Dike" and "Treegarth of Orthanc" are still English in every language, so the table excludes them until
they are translated.

Undecided: 57 BR and 58 IT (2026-10-27). Per language that is 52 rows still in English (11 of them Ered Luin's clan and
kingdom names, seeded in English until the translation run), 3 Haradric titles and the 2 lore names that are still English; IT also holds "Gondoriano", excluded as an adjective. `python tools/generate_name_tags.py --list-undecided`
prints each with its reason.

Settlement tags are judged on the live text, because that is what the apply tags. 47 live names (the Carn Dûm, Cardolan and
Rhudaur additions) and Ered Luin's 31 (2026-10-27) have no translation cache entry, so a rebuild writes their English default, which is what the live file
holds. `castle_AN5` differs the other way round (live "Vigia do Mitheithel", cache "Colinas do Vento"), so the live file is
tagged in place with `name_tags.py --target live-map`, never by rebuild; its tag is `{.FS}` in both languages.

**The live file is tagged.** On 2026-10-26, on Mike's word, `castle_ES1` lost its article ("O Morannon", "Il Morannon" became
"Morannon"), and `name_tags.py --target live-map --apply --allow-live-write` then tagged 1,086 rows per language, 2,172 in all.
`check_external_loc_coverage.py --require-tags` reports 1086/1086 in both languages and `generate_name_tags.py --check` passes.
The untagged originals are kept outside the install. The Italian castle change above is the one difference between the table and
the live Italian file until the next apply. Later applies tagged each new realm's names the same way, Ered Luin's 31 per language
on 2026-10-27 (backup `loc_settlements.xml.bak_nametags_20261027_214259` in the BR and IT folders); on 2026-10-27
`check_external_loc_coverage.py --require-tags` reports 1140/1140 in both languages.

## Key Files

| File | Purpose |
|------|---------|
| `tools/translation_name_tags.json` | The rules and the generated tags |
| `tools/name_tags.py` | Applies the tags; `--lang`, `--target live-map`, `--apply`, `--allow-live-write` |
| `tools/generate_name_tags.py` | Builds `tags` from the rules; `--write` (prunes retired ids), `--check`, `--list-undecided` |
| `tools/name_tag_inventory.py` | The name keys by category and language; reconciles the live names with the cache |
| `tools/rebuild_translation_files.py` | Has a tagging step (TAOM rows always, the TAOM_Map file only with `--allow-live-write`) but is stale for the TAOM module: do not run it there. It always rewrites the TAOM_Map file, so a run without the flag strips the tags; `--dry-run` writes nothing |
| `tools/translate_with_claude.py` | Tags cache and override fills in one write pass (repo files always, outside files with `--allow-live-write`); fresh model output is not tagged; `placeholders_match` counts the grammar tags, so a dropped or added one is rejected |
| `tools/check_external_loc_coverage.py` | Reports the live file's tags (correct, missing, wrong, orphan); `--require-tags` makes a miss fail |
| `tools/Apply-MapVillageNames.py`, `tools/add_map_villages.py`, `tools/rename_map_settlements.py` | Live-map writers: they keep or add the BR and IT tag of the names they write; the rename also renames the table id. `tools/add_map_fortifications.py` writes its names without a tag: run `generate_name_tags.py --write` and `name_tags.py --target live-map --apply --allow-live-write` after it |
| `Main/Features/NameGrammarTags/` | The render service, the pair finder, the variable classifier, the binding plan, the canary, the report format |
| `Main/Features/DevConsole/Cheats/KeptPairCheats.cs` | `taom.print_kept_pairs` |
| `Main/Adapters/GrammarRendererAdapter.cs` | Stateless engine reads by id and the text render |
| `TAOM.Tests/Infrastructure/Localization/NameGrammarTagTests.cs` | The shipped rows carry their tags |

## Dependencies

- `IKeptPairSourceAdapter` (Adapters): vanilla's and TAOM's language rows.
- `IGrammarRendererAdapter` (Adapters): `TextObject`, `Kingdom`, `Clan`, `Settlement`, `MBTextManager` and `LocalizedTextManager`
  (the header names the processor the active language's data builds, `CreateTextProcessorForLanguage(ActiveTextLanguage)`, not a
  private field). `TextObject.ToString()` catches every exception and returns "Error at id: ...", so the service treats that
  text as a failed render (`EngineText.IsError`); the adapter does not throw.
- `IReportFileAdapter` (Adapters): the report file in the game's `Logs` folder.

The command builds its service with `new` in `KeptPairCheats` and is not registered in `Main/IoC.cs`: the three adapters keep
no state, so there is nothing to share, and the registration would be one more line in a single-owner file.

## Tests

- `tools/tests/test_name_tags.py`: tagging, idempotence (never a double tag), head nouns (hyphens too), `carry_tag`, the table
  argument being required, the XML pass (`plan_file` and `write_planned`: line endings, BOM, both result guards), planning every
  file before the first write, the refusal of a row that still starts with an article, the live-write flag in both directions,
  one backup of the live file and only when its bytes change.
- `tools/tests/test_generate_name_tags.py`: the inventory (lore names too), `decide` for each rule, the Italian plain tag by
  category, the live text for settlements, `main()` on a temp table and a temp TAOM_Map (the `--check` stale-id report and the
  `--write` prune), and that the shipped tags equal what the rules produce.
- `tools/tests/test_rebuild_name_tags.py`: the rebuild's tagging and its flag (a language without tags never loads the table),
  the translator's `main()` with a fake provider (cache fills tagged, fresh output not, in a mixed file too; the table loaded
  once before any call; the live re-apply hint), and that a tag does not hide an untranslated English row from the translator.
- `tools/tests/test_translate_grammar_tags.py`: a dropped, added or changed grammar tag is rejected.
- `tools/tests/test_check_external_name_tags.py`: the live-file report (an article-led tagged row is wrong), orphans, and
  `main()` under `--require-tags`.
- `tools/tests/test_live_map_writers_name_tags.py`, `test_live_map_writers_main.py`: the three live-map writers keep the tag,
  `add_map_villages` tags its rows from the table once per run, and `rename_map_settlements` moves the table ids.
- `TAOM.Tests/Infrastructure/Localization/NameGrammarTagTests.cs`: every tagged BR and IT row starts with its allowed tag, a
  head-noun row carries its noun's tag, IT kingdom and clan names are masculine nouns, English, Chinese, Japanese, Korean and
  Turkish rows carry none, the cache stays tag-free. `test_generate_name_tags.py` also pins the IT castle rule and that the
  hyphenated BR clan "Prole-das-Rochas" needs no pin.
- `TAOM.Tests/Features/NameGrammarTags/`: the pair finder, the variable classifier, the binding plan, the canary, the report
  format and the service (with NSubstitute). The rendering itself is an in-game check.

## How to verify in game

**Preconditions:** a campaign in Brazilian Portuguese or Italian (Options, Language), cheat mode on in `engine_config.txt`,
the TAOM_Map install tagged (done 2026-10-26; "Re-applying to the live TAOM_Map" says how to confirm it), and for Italian the
#1026 fix.

1. Console (`Alt` + `~`): `taom.print_kept_pairs IT 4`. It answers `Rendered N kept pair(s) for 4 kingdom(s): ...` and a
   `Report:` path in the game's `Logs` folder; N should match the 108 IT lines of `kept-vanilla-key-rows.txt`. Do the same in
   BR (`taom.print_kept_pairs BR 4`, 172 lines). In a game running another language it names the language to switch to and
   renders nothing.
2. Read the report header first. `Text processor:` names the processor the active language's data builds and `Canary:` must say `PASS`: in IT
   `{.di}` before a bound `{.FN}Roma` prints "di Roma", in BR a `{#GENDER}` call picks the option of a `{.FS}` tag. `Canary: FAIL`
   means the processor ignores the tags (in IT that is #1026); every line below it proves nothing.
3. Read the blocks. Each shows vanilla's row and TAOM's row with real names bound, once with each plain kingdom or clan variable
   as its Name and once as its InformalName, and once with each `FACTION` variable as a clan. A block is `SAME` when all lines
   match, `DIFFERENT` when any pair differs, `FAILED` when a render threw. For a `DIFFERENT` block read the `vanilla:` lines: if
   the article or preposition is right in every variant, the pair can be deleted (step 6 of #1023, on Mike's word).
4. Declare a war between two kingdoms with `campaign.declare_war` (its `help` prints the format). Read the notification and
   the log line: the kingdom names carry the right article (BR) or preposition (IT).
5. Make a peace offer to the player's kingdom and read its text; give a fief to a clan through the kingdom decision and read the
   grant notification.
6. Let a village be raided (or raid one with a party) and read the raid notification in BR and in IT: it names the village and
   its owner with the right article or preposition.
7. Open the encyclopedia page of a kingdom, a clan and a settlement. The title shows no `{.` text. Type a kingdom name and a
   village name in the search box and confirm both are found.
8. On the map, read the nameplates of a town, a castle and a village with "Aldeia" (BR) or "Villaggio" (IT) in the name: no
   tag text, the head noun as before.
9. Load a save made before this change. Repeat step 4 on it. If a clan name in a vanilla sentence still reads without its
   preposition, the save kept the old text for that object: note which.

An unreachable step is reported with the command tried and the unmet precondition. It is never marked verified through another
path.

## How to Change a Name's Tag

1. The name is wrong: add its id to `pins` in `tools/translation_name_tags.json`, run `python tools/generate_name_tags.py --write`.
2. A whole class of names is wrong: add the noun to `head_nouns` (or the first word to `proper_prefixes`), run
   `python tools/generate_name_tags.py --write`, then read `--list-undecided`.
3. Apply it: `python tools/name_tags.py --lang all` (dry run), then `--apply`. It plans every file first and writes none when
   one fails. Git is the backup of a repo file.
4. After a translation run, rerun steps 2 and 3: a new translation may change a name's first word, which is why the translator
   leaves fresh model output untagged and prints the rows to re-tag.
5. `generate_translation_template.py --apply` overwrites a language file with English and so drops the tags; rerun step 3 after it.
6. A name that begins with an article: drop the article from the translation (the language file row and the cache entry), pin
   the gender the article gave, and rerun step 1.

## Re-applying to the live TAOM_Map

The live `loc_settlements.xml` of each language is a file in the game install, applied once on 2026-10-26 (see above). A
reinstall of the module reverts it ([moduledata-validation.md](moduledata-validation.md)), and so does any writer that skips the
tags. Re-apply with this procedure, on Mike's word:

1. **The article check.** Every tagged live row must start with the name, not an article. `python tools/name_tags.py
   --target live-map` (the dry run) warns about a row that still starts with one, and `--apply` exits 2 on it. Rewrite such a
   row without its article first; the table already pins the gender the article gave.
2. **The table.** `python tools/generate_name_tags.py --write`, then the dry run: `python tools/name_tags.py --target live-map`
   lists what the apply would change (0 rows when the file is current). `--apply` on that target is refused unless
   `--allow-live-write` is also given; it exits 2 when the live file is missing.
3. **The apply.** `python tools/name_tags.py --target live-map --apply --allow-live-write`. It backs the file up beside itself as
   `loc_settlements.xml.bak_nametags_<time>`, only when its bytes change.
4. **The confirm step.** `python tools/generate_name_tags.py --check` and `python tools/check_external_loc_coverage.py
   --require-tags`. The second reports `0` or the full count (`1140/1140` on 2026-10-27) of live names tagged per language; `--require-tags` makes a missing, wrong
   or orphan tag fail (exit 1) and a missing report exit 2.

**Writers that strip or skip the tags.** The tags are only as durable as the last tool that wrote the file:

- `tools/rebuild_translation_files.py` always rewrites the TAOM_Map `loc_settlements.xml`, and tags it only with
  `--allow-live-write`. After the live apply, a run without the flag strips every tag.
- `tools/translate_with_claude.py --module TAOM_Map` writes the same file and tags it only with `--allow-live-write`; when it
  writes a file outside the repo it prints the re-apply command of step 3.
- `tools/Apply-MapVillageNames.py`, `tools/add_map_villages.py` and `tools/rename_map_settlements.py` write names into the
  live files. Each keeps the tag the table has for the id, or the tag the old row carried; a new village has no table entry
  yet. After any of them, run steps 2 to 4.

Owed at the next live apply: the rebuild and the translator should keep the replaced row's tag when they rewrite a row the
table does not know, so a rewritten name never loses it. The three live-map writers already do.

## Changelog

- 2026-10-27 (Ered Luin): `generate_name_tags.py --write` added the Ered Luin rows to the tag table, `name_tags.py --lang
  all --apply` tagged its seeded BR and IT name rows, and a live apply tagged its 31 settlement names per language; the
  confirm gate reads 1140/1140 in both languages.
- 2026-10-26 (live apply): `castle_ES1` was rewritten without its article, then the live `loc_settlements.xml` was tagged
  (1,086 rows per language, 2,172 in all). The IT castle rule (`{.MN}` for an untitled castle) came after it, and a second
  apply re-tagged those 146 live Italian rows; the confirm gate reads 1086/1086 in both languages.
- 2026-10-26: phase 1 for BR and IT: the tag table and its generator, the apply step in the rebuild and the translator, the
  translator guard, the C# data test, `taom.print_kept_pairs`, the live-file report. No kept pair is deleted yet.
- 2026-10-26 (review wave 2): a tagged row that starts with an article is wrong in the report and refused by the apply; an
  engine "Error at id:" line counts as a failed render; the translator writes each file in one pass; the lore town names are in
  the inventory; the apply runbook starts with `castle_ES1` and lists the writers that strip tags.
- 2026-10-26 (review wave 1): IT kingdoms, minor factions and clans take `{.MN}`; names that began with an article lost it;
  settlement tags are judged on the live text; the render binds Name and InformalName, FormalName, a clan for `FACTION`
  variables and the culture's formal name, and prints a processor canary; one `--allow-live-write` flag gates every write
  outside the repo.

## GitHub Issue

- **Issue:** #1023, [Grammar tags on TAOM's translated names for Portuguese and Italian](https://github.com/haterade22/TAOM/issues/1023)
- **Status:** Open (phase 1 built and applied to the live TAOM_Map; the in-game render and the kept-pair deletions are owed, then the tags for DE, FR, SP, PL and RU; the Italian half waits on a release that ships the #1026 fix in the TAOM_Map and Armory packages)
