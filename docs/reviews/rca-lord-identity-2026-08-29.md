# RCA: lord identity reconciliation, 2026-08-29

The changeset reconciled `characters/lords.xml` + `lords.xslt` (who a lord is) against
`characters/heroes.xml` + `heroes.xslt` (what the encyclopedia says). Nine review agents ran over
it. **Fourteen findings survived verification**, six of them defects the changeset itself
introduced. All fourteen are fixed; nothing was deferred.

The headline lesson is uncomfortable and worth stating plainly: **the gates written to catch this
defect class shipped with three of the same blind spots the class is made of.** A parse guard with
400 rows of slack, a comparison that trimmed one side only, and a rule generalised from the one
family it was tested on.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Six `lords.xslt` name values carried a trailing space. The registry sync trimmed the stylesheet side and compared against an untrimmed registry, read the six as changed strings, and overwrote their translations in all twelve languages with English. German lost `Nazgûl, der Dunkle Marschall` and `Gorwulf, der Eber`. | localization | Nothing compared `lords.xslt` to the registry. The one existing gate compared `characters/lords.xml` to the registry, a different pair of files. | `EveryLordNameInLordsXsltMatchesTheRegistry` now checks the text AND rejects stray whitespace. 49 locale rows and their cache rows restored from HEAD. |
| 2 | HIGH | The four new Gríma-family biographies were written into the twelve language files before the diacritic pass ran over the English, leaving `Grima Grimmoding` against a registry that said `Gríma Grimmóding`. A row that merely resembles the English is invisible to the translator forever (`cur_text == eng_text`). | localization | Ordering. The rows were correct when written and went stale when a later step edited only the English and the registry. No gate looks for near-misses. | `NoLanguageRowIsTheEnglishWithItsDiacriticsStripped`, with a shrink-only baseline of seven rows that already shipped that way. 48 rows given the registry's exact bytes. |
| 3 | HIGH | "The translations already carry the accents, so accent-only changes need no re-translation" was true for the Théoden family and false for 23 of 28 keys. Up to seven Latin locales were left spelling Ælle, Rúmil, Gûrtilm, Amdûr, Lûthkan and Cuthræd without diacritics, permanently. | localization | A rule generalised from the sample it was tested on. Rohan's bios were re-translated after an earlier accent pass; Rhûn, Khand and Harad's never were. | 140 rows had the accented token substituted in place, keeping the prose; the 28 cache rows were dropped so a rebuild cannot re-emit the stripped form. The lesson is the generalisation, not the keys. |
| 4 | HIGH | Eight parent links were inferred from id patterns and disproved by the ages: `lord_4_21_1` had a father the same age as himself, `lord_4_27_2` a father one year older. | data | Nothing checked ages, and the id pattern looked authoritative because it held for the Mordor heirs. | Parent links dropped where the ages disprove them; Théodwyn aged 26 to 45, which also fixes a pre-existing gap-5 link to Éomer. New `NoParentIsTooYoungToBeOne` with a 68-entry shrink-only baseline in `impossible-age-links-baseline.txt`. |
| 5 | HIGH | Adding Hero entries made five Gondor names collide between two lords who now both spawn (Elphir, Erchirion, Amrothos, Ivriniel, Belwen), and gave Gûlnak's son the name of Gûlnak's named rival (`lord_M16_11` Uznash against `lord_M14_1` Uznash, whose biography names Gûlnak). | data | Instantiating a dormant lord is not obviously a rename, so the collision never looked like one. | `lord_M16_11` renamed to Ushgar. New `NoTwoSpawningLordsOfOneCultureShareADisplayName`, scoped to one culture because cross-culture orc reuse is deliberate, with a 24-name baseline. The five Gondor collisions are recorded there as owed content work. |
| 6 | MED | De-marrying Elbet so Erkenbrand could marry Mérthú left Elbet holding three vanilla children by him. Mérthú at 29 could not be the mother of his 19-year-old son either. | data | The fix looked at the marriage and not at the children the marriage already had. | Mérthú aged to 41; `lord_4_18`, `lord_4_181`, `lord_4_19` re-pointed to her. |
| 7 | MED | Both new tests modelled `characters/lords.xml` as replacing the whole node, when `MBObjectManager.MergeElementAttributes` merges per attribute. Seventeen ids take `is_female` from `lords.xslt` because the plain XML never states one. | test | The load-order fact ("the plain XML wins") was true and the inference from it ("so it replaces the node") was not. Nobody decompiled the merge. | Both loaders now assign per attribute. All seventeen read `false` on both sides today, so no data was wrong; the eighteenth would have been. |
| 8 | MED | Seven Dol Guldur wives flipped to female kept `taom_north_orc_warrior_skills` while `taom_north_orc_female_skills` exists and the one correctly converted orc uses it. | data | The lesson names three things that move as a unit (sex, beard, body key) and `skill_template` is not one of them. | All seven repointed. The unit is four things, not three; the lesson file now says so. |
| 9 | MED | `lords.xslt` and `characters/lords.xml` ended up contradicting each other on `is_female` for `lord_3_13_2`, `lord_B8_l`, `lord_B8_s`. Runtime was correct, and both new gates apply the same precedence, so neither could see it. | data | A gate that models the winner cannot see a loser that disagrees. | Stylesheet corrected and all three added to `GENDER_OVERRIDES`. |
| 10 | MED | Three more names shipped as placeholders: `lord_5_7` "Khand PlaceHolder" (registered and translated ×12), `lord_L1_3` "Child Placeholder", `lord_M1_12` "PlaceHolder Child To Not Break Game". | data | RandomDude was found by reading a diff, not by a rule. Nothing greps for the word. | All three named (Cadwyr, Aerlin, Faelen). The biography gate now covers them, because a placeholder name never appears in a biography. |
| 11 | MED | `lord_6_23`'s biography named "Borlad", a lord that exists nowhere. It passed the name gate because the bio contains "House Hûz" and the lord is "Hûz-Margôz Hûz". | data | The gate substring-matches the given name; a house name containing it satisfies the check. | Biography corrected. The weakness is recorded below and is not yet gated. |
| 12 | MED | The parse guards were `> 1000` against 1401 and 1395 rows, so every template in either stylesheet could stop matching and the gates would still report green on the plain XML alone. | test | This is finding 12 of the sibling test, re-introduced one layer up in the same session that fixed it. | Per-source floors: 380 templates and 1150/980 elements. |
| 13 | LOW | `characters/lords.xml` duplicate ids were swallowed by the last write, so one definition reached no gate. | test | Dictionary assignment reads as parsing, not as a merge decision. | The loader now fails on a within-file duplicate. |
| 14 | LOW | `lord_rohan_10_1` was "Eomund Eoforing" while its own biography said "Éomund". | data | The accent map is built from lord names, so a name that lacks the accent cannot teach the map to add it. | Corrected. |

## The root-cause pattern: a gate is only as good as the sample it was calibrated on

Findings 1, 3, 7 and 12 are one shape. In each, a rule was derived from the cases in front of the
author and applied to a population that did not match:

- The trailing-space bug (1) came from comparing a trimmed value to an untrimmed one, which is only
  safe if no value has stray whitespace. Six did.
- The accent rule (3) was verified against Théoden and Éomer and applied to Rhûn and Khand.
- The merge model (7) was inferred from a load-order fact rather than read out of the engine.
- The parse floor (12) was picked to be "comfortably below the row count" without asking which
  source contributes which rows.

**Prevent:** when a data rule is derived from examples, state the population it was checked against
and check the complement before generalising. Where the rule encodes engine behaviour, decompile
the engine rather than inferring from an adjacent fact. Where a guard is a threshold, derive it per
source so it cannot be satisfied by the wrong half.

## Why each agent missed what it missed

- **Standards** reported two findings, both disputed on verification: the em dashes it flagged in
  `tools/complete_lords_xslt.py` are pre-existing lines the changeset never touched, which
  `output-style.md` exempts, and the hardcoded game-dir fallback matches the sibling data test
  `CultureRaceConsistencyTests` exactly. Its rule set is C#-architecture shaped and this changeset
  is data, so it had almost nothing in scope.
- **Efficiency** produced three findings, two of them wrong and one dangerous: it proposed replacing
  `(.*?)` with `[^<]*?` in the template regex, which matches **0 of 396** templates because every
  body contains `<xsl:attribute>`. Applying it would have silently zeroed the gate. It also
  recommended caching an `XslCompiledTransform` that is constructed exactly twice, once per
  stylesheet. Measuring durations is not the same as reading what the code does.
- **Completeness** reported the localization state as a 100x discrepancy (5,410 rows) by counting
  every row equal to its English, which is dominated by a pre-existing backlog of proper nouns that
  legitimately do not change. The real figure for this changeset is 54.
- **Data flow** and **XSLT/data** were the highest-value agents and found findings 4, 5, 6, 8 and 9.
  Both traced the merged document rather than the files.
- **Adversarial test-quality** was the only agent that mutated the data and ran the gates, and it is
  the only one that found findings 11, 12 and 13. Proving a gate red is a different activity from
  reading it.
- **Localization** found 1, 2 and 3, all by comparing the twelve files against the registry
  key by key rather than trusting the sync's own report.

## Still open, recorded not fixed

- **The biography gate substring-matches a given name.** 89 lords share a given name with another,
  and 48 biographies pass on a form of four characters or fewer. `lord_6_23` proved this is
  exploitable in practice.
- **`LordFamilyTransformTests` runs on one machine.** CI gates the whole test job on a Bannerlord
  install, and `Assert.Inconclusive` is a pass. The only gate that can see vanilla-inherited family
  attributes protects a single workstation. A vendored fixture of the roughly 400 ids the overlays
  touch would fix it.
- **`EveryLordNameFallbackMatchesTheRegisteredEnglishText` judges 168 of 1184 entries**, skipping
  1016 whose keys are registered nowhere. That is the documented two-tier design
  (`characters/lords.xml` is largely English-only), but the gate reads as covering the roster.
- **Five Gondor name collisions and the two Wulf Celmundings** need a content decision.
- **68 pre-existing impossible parent ages** are baselined and untouched.

## Lessons added

`docs/reviews/lessons/data-content-cultures.md` gains the calibration lesson above and the
localization near-miss lesson from finding 2. The three lessons written before the review stand.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/lord-identity-reconciliation.md](../features/lord-identity-reconciliation.md)

<!-- backlinks-end -->
