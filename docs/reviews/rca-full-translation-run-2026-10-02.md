# RCA: the full translation run of 2026-10-01 (retroactive deep review, 2026-10-02)

**Scope:** the translation slice of `0bd6abf3` (pushed): the name generator's five data-text categories, the 4,591
keys they registered, 36 XSLT keys, the wiring (SubModule.xml, 12 manifests, 60 language files), the translated rows
in all 12 languages and 3 modules, the 12 caches, two test classes and the docs. #704 and the Dale work in the same
commit are out of scope. Eight lenses in two waves (standards, engine compatibility, data flow, XML; completeness,
efficiency, design, tooling), all `deep-reviewer`.

**Top line:** no CRITICAL, no HIGH. The display path works for every new key (19 engine calls verified on the
installed v1.5.3). The worst defect was a registration that should not have happened: 36 keys the run took for
TAOM's own were SandBox's, and TAOM's machine rows replaced TaleWorlds' curated translations in all 12 languages
(Turkish grammar suffixes lost, a garbled "my lord" line in four languages). It is reverted and now gated. The rest
were gate gaps the widened scope exposed, and docs that described the run before measuring it.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | 36 `comment_strings.xslt`/`action_strings.xslt` keys registered in `taom_xslt_strings.xml`; they are SandBox keys, so 432 TAOM rows replaced vanilla's translations (TR `'{.e}` lost, DE/FR/BR/IT "meine mein Herr") | Wrong ownership claim | The registration script filtered on "no TAOM English row" only; the planning agent searched `SandBoxCore`, not `SandBox`, and that claim was relayed unchecked | Reverted (English file equals its pre-run bytes); `VanillaKeyOverrideTests` (LiveInstall); `/localize` Case C; lessons "Never register a vanilla key..." and "A delegated search's not found..." |
| 2 | MED | Patch25 (`MBTextManager_GetLocalizedText_Patch`) returns English for the 313 non-`taom_` keys of `taom_module_strings.xml` in every language; 56 of them are this run's data text (Abanissa, Shaghana), and the docs said every NPC name was translated | Pre-existing code; doc claim | The run's docs were written from the generator's output, not from what the engine shows | Docs carry the exception; issue drafted (fix: apply the overrides only in English); FOLLOW-UP |
| 3 | MED | RU `aom_dolguldur_male_name_38` "Ворг" + Devanagari; four older rows the same way (DE, TR x2, live Armory RU) | Model output | The writing-system gate classified five scripts and ignored every other letter | Rows and caches repaired; finding 4 |
| 4 | MED | `LanguageTextIntegrityTests.Script()` returned null for Devanagari, Arabic and IPA letters | Gate scope | Written for the 2026-09-25 incident's scripts only | Any other letter is "Other" and foreign in every language; four DataRows |
| 5 | MED | `LocalizationKeyConsistencyTests` floors (`> 10` sources, `> 1000` keys, `> 6300` sites) left at the old scale after the scan grew to 55 sources, 9,521 keys, 11,555 sites; fixed paths dropped by `.Where(File.Exists)` | Gate decay | The scan was widened, the floors were not re-measured | Floors at the measured scale, the XSLT half floored separately, each fixed path asserted |
| 6 | LOW | Splitting the six shared hero-bio keys renamed only the second site: old saves (EncyclopediaText is saved; heroes.xml loads only for a new campaign) now show the namesake's translated bio in 12 languages | Save compatibility | The split was judged on the English side, not on what a save keeps | Accepted by Mike as a known limitation (release note); lesson: rename both sites for saved text |
| 7 | MED | Nothing failed when a data file gained a `{=KEY}` without a regeneration | Missing ratchet | `UnregisteredLocalizationKeyBaselineTests` scans C# only | Generator `--check`; `ShippedTreeTests` runs it in CI |
| 8 | MED | The generator skipped a missing source and wrote an empty file over a registered one, exit 0 | Silent data loss | `continue` on a missing source; unconditional write | Missing source or empty glob exits 2 and writes nothing (tests) |
| 9 | LOW | Generator wrote `\r\r\n` (`write_text` after a `"\r\n"` join), wrote without parsing, took the `{=!}`/`{=*}` sentinels | XML I/O convention | Copied from the 2026-09-17 version | Bytes written after a parse, sentinels skipped (tests); the nine files regenerated, line endings only |
| 10 | LOW | Five `GameText` nodes for the data-text files: nothing reads `{=KEY}` rows through GameText; about 0.2 to 0.3 s per game start (measured from rgl logs, small sample) | Recipe followed blindly | The #572 recipe registered every English file as GameText | Nodes removed; recipe now says GameText only for `FindText` ids (13 older inert nodes: FOLLOW-UP) |
| 11 | LOW | Four hand-kept English source lists (translator 22, template 15, generator 13, rebuild 10) | Duplication | Each tool kept its own copy | `tools/_loc_sources.py` read by three tools; `test_loc_sources.py`; rebuild: FOLLOW-UP |
| 12 | LOW | Docs: totals stamped "2026-10-01" never measured (19,600; "19 Armory"); stale test names (Eleven/Thirteen/Seventeen); "biographies still untranslated" in four docs; wrong reason for the dead settlement files | Unmeasured claims | Written from the previous docs' numbers | Re-measured (TAOM 15,997 keys, Armory 4,611 rows in 22 files, Map 1,241) and corrected |
| 13 | MED | `/localize` had no case for inline data text, so the next hero or NPC would ship English | Missing procedure | The generator appeared in no skill | Case D added; Case B updated |
| 14 | MED | 484 Armory item, crafted-item and crafting-piece names have no English loc row, so no tool translates them; "all modules" was not met there | Scope gap | The translator and the coverage check iterate English rows only | Issue (#671 widened) drafted; FOLLOW-UP on Mike's word |
| 15 | MED | `0bd6abf3`'s subject and body describe Dale tests; the release note would omit this run | Commit metadata | Committed outside the session | The next commit body carries the release-note paragraph |
| 16 | LOW | Issue drafts: #478's female notables are not on this branch; a `localization` label that does not exist | Draft accuracy | Drafted from the memory card | Drafts corrected |
| 17 | LOW | The seeder copies the last row's indent and terminator (2-space, `\r\n` rows in 4-space `\r\r\n` files) | Tool hygiene | Pre-existing in `sync_missing_ids` | FOLLOW-UP |
| 18 | LOW | DE armoury rows mix formal and informal address; "dein Waffe des Fürsten" | Translation quality | No sense check covers register | FOLLOW-UP (#704's strings) |

## Root-cause pattern

Findings 1, 2, 6 and 12 share one cause: **the run decided what players see from the pipeline's files, not from the
engine's load order and save rules.** "Registered and translated" was taken to mean "shown", and three engine facts
broke that: vanilla rows load before TAOM's and lose to them (1), a TAOM patch overrides every language with English
(2), and a saved hero keeps its old key (6). Findings 4, 5 and 7 share a second cause: **a gate written for one
incident's scope, not widened when the data it covers grew.**

## Why each agent missed these at the time

The run had no deep review before its commit (it was committed outside the session). The planning-phase research agent
answered finding 1's question wrongly by searching one vanilla module; the session checked two of its claims and not
that one.

## Feedback memories to codify

None beyond the lessons in `docs/reviews/lessons/localization-ui.md` (five entries dated 2026-10-01 and 2026-10-02).
