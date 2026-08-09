# Codex adversarial review — per-culture coverage guards + dead-member deletion (2026-08-09)

You are an independent reviewer. Your job is to **falsify** the claims below, not to confirm them.
Assume the author is competent and wrong somewhere. Report only findings you can support by quoting
the file you read.

Repository: `e:\repos\TAOM` (Bannerlord 1.4.7 total-conversion mod, C# / .NET Framework 4.7.2).
Branch `bannerlord-1.4.5`. Review commits `25a6eba3`, `83f970df`, `dc1bc4d8`, `721bb523` — use
`git show <sha>` and `git diff 9d2b8f24~1..HEAD` to see them.

## What was done, and the claims to attack

### 1. `25a6eba3` — deleted `IServiceAttachmentService.ExitSettlementForDuty` + its implementation

**Claim:** the member had zero callers after the field-duty rework, and deleting it removes a shape
the current design must not offer (leave a settlement, then `RestorePresence()` — the exposure #428
was filed for).

Attack: is it genuinely uncalled? Look for reflection, Harmony patching by name, DI-by-convention,
any test asserting the interface's member set, and any *external* consumer. Does anything still
need a settlement exit that restores presence — for example an enlisted player who is inside a
settlement when the commander leaves, or a discharge that happens indoors? `ExitSettlementForService`
ends in `ParkNear`; confirm that is correct for **every** remaining exit path, including discharge
and commander-death, not just the following case.

### 2. `83f970df` — registered 96 character-creation narrative strings for `goblin` / `mistymountainorcs`

**Claim:** `NarrativeMenuBuilder` composes keys at runtime (`$"{{=taom_cc_{StringId}_text}}"`), those
96 were unregistered, all other 16 cultures were complete, and the values now exactly mirror the
JSON.

Attack: verify the 96 count independently. Are there OTHER menus or key suffixes the new test does
not cover (`childhood_menu.json`, `career_menu.json` are excluded as culture-agnostic — is that
true?). Does `NarrativeMenuBuilder` compose any THIRD key beyond `_text` / `_desc`? Is
`taom_cc_strings.xml` actually the file the engine loads for these ids, and is it referenced from
every `Languages/*/language_data.xml`? Check the file's unusual `\r\r\n` line endings survived the
edit and that Bannerlord's XML reader tolerates them (the file already used them — confirm the new
lines match, and say so if they do not).

### 3. `dc1bc4d8` + `721bb523` — two new per-culture coverage test suites

`NarrativeCultureCoverageTests` keys on `cultures.json`; `EnlistmentRosterCultureCoverageTests` keys
on `is_main_culture="true"` in `taom_spcultures.xml`.

**This is the claim most worth attacking.** The two suites use DIFFERENT predicates for "which
cultures must be covered."

- Is `is_main_culture="true"` the right predicate for **enlistment**? Enlistment is about serving an
  NPC lord, so arguably the right predicate is "a culture with ordinary lord clans." Find a culture
  where the two disagree — one with enlistable lords that is not `is_main_culture`, or vice versa.
  The bandit/minor-faction cultures (`dunland_raiders`, `harad_raiders`, `rhun_raiders`,
  `gundabad_raiders`, `umbar_corsairs`, `erebor_warriors`, `gondor_soldiers`, `mirkwood_stalkers`)
  were excluded on the grounds that each has exactly one clan whose id equals the culture id.
  **Verify that reasoning holds** — is single-clan-with-matching-id actually the bandit shape in
  this repo, and can a player enlist with any of them?
- Do the regex-based parses in `EnlistmentRosterCultureCoverageTests` handle every form
  `taom_spcultures.xml` actually uses (self-closing vs paired `<Culture>`, attribute order,
  comments, XML entities)? A parse that silently matches nothing makes the test vacuous; there is
  one guard against that — is it sufficient?
- Both suites have a "stale documented exception" test. Can either be defeated — an exception that
  is resolved but still reported clean, or a false positive that would block a legitimate change?
- Are the rank tokens in `EnlistmentRosterCultureCoverageTests` still in sync with
  `EnlistmentRosterIds.RankToken`? What happens when a fifth rank is added?

### 4. The cross-cutting claim

**Claim:** there are exactly five per-culture coverage systems in TAOM (careers, narrative options,
narrative strings, education templates, enlistment rosters) and all five are now guarded by a rule
that enumerates from the culture data rather than a hand-maintained list.

Attack this hardest. **Find a sixth.** Anywhere the code or data assumes a per-culture entry exists
— party templates, notable templates, civilian equipment, tavern mercenaries, banners, music,
troop trees, settlement guard pools, name lists, body properties — and where a missing entry
degrades silently rather than failing loudly. A silent fallback is the tell.

**Claim:** there are exactly four runtime-composed localization key sites in the codebase
(`NarrativeMenuBuilder`, `FieldDutyRuntime` ×2, `ServiceStatusTextWriter`, `InquiryAdapter`), found
by grepping `"{=" +`, `"{=prefix" +`, and `$"{{=`. **Find a fifth form** — string.Format, a
`const` prefix concatenated elsewhere, `TextObject` built from a variable that already contains the
whole `{=key}default`, a key assembled across two statements.

## Rules

- **Verify before asserting.** Signatures come from `pwsh tools/taom-src.ps1 path <Type>` against the
  installed 1.4.7 DLLs, not from the decompiled dump, which can lag.
- Quote the file and line for every finding. A finding without a quote is a guess.
- Rank P1 (ships a bug) / P2 (wrong but contained) / P3 (correctness of reasoning, comments, docs).
- **Explicitly say so if a claim survives.** "Checked X, it holds, here is why" is a useful result.
- Ignore: `Main/bin`, `Main/obj`, `TAOM.Tests/bin`, `docs/reviews/raw`.
- Other sessions are working in this repo. Do not edit any file; report only.
