# X closure: waves 2 and 3 (plans 117 to 127)

Closure check, 2026-11-04. No plan was edited. Line numbers are in the plan files as they stand on disk today.

## What I read

- `verify/X-consistency-waves-2-3.md` in full, and all 28 `fixes.json` items for plans 117 to 127.
- Every line of `git diff --no-index --unified=2 <backup> <plan>` for plans 117 to 127.
- Both sides of each changed hand-off, by grep and excerpt: 117/118, 117/120, 117/142, 118/122, 118/144, 120/123,
  120/135, 121/134, 121/142, 123/127, 123/134, 125/133, 136/143, plus `plans/README.md` (table and merge notes).
- Scripted scans of plans 117 to 127: em and en dashes (0 in all 11), `git checkout` with a bare double dash (0),
  he/him/his (only the soldier, a lord and yotthani, never the maintainer), `dotnet build|test` without the flags
  (only `dotnet restore`, quoted comment text and the elided `dotnet test ...` at `122:1793`), subject length of
  every `v2.0.28` subject in the added text (none over 72).
- Git, to test the facts the fixes rest on:
  - `a27c6b338`: its runsettings and pins; 8 + 3 = 11 tests; two harness tests; no later 1.5.x touch up to `e45f6df2c`.
  - `ecaa67d26`: its two test-file hunks; 7 `NameplateCull` lines, which matches plan 123's Verify.
  - The full `Ported-from` SHAs for `a27c6b338`, `af990ca76` and `ecaa67d26`.
  - The MCM pin format in `Main/TAOM.csproj`, and `module-dependencies.md:772`.
  - The anchor heading in `battle-load-diagnostics.md`.
  - All 12 language blobs at `1b00d1acd`: one closing `  </strings>` each, and each parses.
  - `b747cf398` against `963218f7f` for `shadowfax-screens.md`.
- The arithmetic of plan 122's shifted totals (11, 30, 39, 42, 46, 48, 57) and plan 127's (TA + 25, T0 + 77): they hold.

## What I did not read or run

- Plan bodies outside the diffs and the excerpts above. A defect that shows only in an unedited step and names no
  other plan is not covered.
- Plans 128 to 145 beyond the hand-off excerpts. The X45 closure covers them.
- Nothing was built, tested or simulated in a worktree. The language helper's preconditions were checked on the blobs,
  and the helper itself was not run.
- MCM 5.12.3 was not read (plan 123 carries a STOP for it). `GameStateManager` and `ScreenManager` citations in plan
  120's new paragraph were not re-read on the installed DLLs. Its fixer reports them as dump readings.

## Result

Tracked items: 16 per-plan findings, 5 programme issues, 28 fix-list items, 49 in all. **46 closed, 3 open**: finding 12
and its fix-list item 123 #3 (the fix landed but its Verify cannot pass), and programme issue 1. Items 3 to 5 and 6 to 10
below are new, found in the edits or left as residuals. No refuted item falls in this scope.

## Open items

### 1. MED: plan 123 Step C2, the Verify cannot pass (lines 267, 900-906, 1227)

Plan 120 Step C4 item 2 writes `NameplateCullSettingsProviderTests` into `docs/reference/harmony-patch-registry.md`,
section `## Patch104_NameplateCull` (`120:1446`). Plan 123 Step C2 replaces the name only in
`docs/features/nameplate-cull.md`, and its Verify and Done criterion require
`git grep -n "NameplateCullSettingsProviderTests" -- Main TAOM.Tests docs` to print nothing. The registry hit makes both
fail once the fold is done correctly.

**Fix:** add a bullet to Step C2 after the `nameplate-cull.md` bullet:
`` `docs/reference/harmony-patch-registry.md`, section `## Patch104_NameplateCull`: in its Tests list replace `NameplateCullSettingsProviderTests` with `CampaignHotPathSettingsProvidersTests`. ``
Add the registry to the Part C docs in Scope (`:267`) and to the step's `git add`.

### 2. MED: plan 124 line 275, the checkout edit made the rule contradict itself

The bare double dash removal turned the prohibition into
``Never write `git checkout ... <path>` or `git restore` ``, directly after line 274 prescribes
`git checkout HEAD <path>` and line 273 `git checkout --ours <path>`. An executor cannot obey both.

**Fix:** replace it with `Never write a git checkout with a bare double dash, and never git restore (the dangerous-git hook asks on those forms).`

### 3. MED: plan 125 lines 1701-1703, a same-wave conditional with no owner on the other side

The note says WP123, if it merges after this WP, adds the two `ShadowfaxScreensSettingsProvider` rows. `Shadowfax` appears
0 times in plan 123, there is no edge, and `125:780-781` routes the skip to this note. If WP125's base lacks the test file, the
rows land nowhere and the hot-path gate never covers that provider. This breaks the rule at `README.md:103`.

**Fix:** replace the first sentence of the bullet with:
``**WP123 (same wave)**: Step 6 item 6 reads this branch's base. If it was skipped (the hand-off says so), the integrator applies item 6 after both WPs merge, as one commit, and the Filtered run of `CampaignHotPathSettingsProvidersTests` must pass.``
Keep the sentence about `SettingsFingerprintTests`.

### 4. MED: `plans/README.md`, the Depends column misses every edge these fixes added (lines 148, 155, 156, 160, 166, 172, 173)

Programme issue 1 is closed for the rows it named and reopened by the new edges:

| Row | Depends column today | Must read |
|---|---|---|
| 115 | 107, 109 | add 108 |
| 122 | 104, 115 | add 118 |
| 123 | 105, 117 | add 120 |
| 127 | 105 | add 123 |
| 133 | 101, 104, 105 | add 112, 125 |
| 139 | 118, 126, 129, 130, 131 | add 136 |
| 140 | 104, 115, 131 | add 136 |

The Status blocks are authoritative and each Step 0 stops on a missing dependency, so the cost is a wasted lane rather than
wrong code. Table order already satisfies every new edge.

**Fix:** the orchestrator updates those cells when it regenerates the table, and does not hand out lanes from the stale cells.

### 5. MED: plan 133 Step B3.6, the feature doc stays false after #993 (lines 1121-1150)

WP125 transplants `docs/features/shadowfax-screens.md` from `b747cf398`. Its paragraph
`**Why this is safe next to BannerColorPersistence.**` says Patch23 writes `Color1` and `Color2`, and that the clan colours
never show on party rows. B3.1 ends both. `963218f7f` rewrote that paragraph, and plan 133 takes only the code comment:
`shadowfax-screens` does not appear in plan 133.

**Fix:** add to B3.6: replace that paragraph with the `963218f7f` paragraph
(`git show 963218f7f:docs/features/shadowfax-screens.md`, from `**Why this is safe` through `slower, not wrong.`). Do not
copy that file whole, because it carries the co-op split. Add `grep -c "ClanColoredCharacterCode.Recolor" docs/features/shadowfax-screens.md`
prints `1` to the Verify, and add the doc to Scope, the Stage list and the `963218f7f` Disposition row.

### 6. LOW: plan 122 line 1810, a same-wave conditional addressed to no one

``**WP119**: if this WP merged first, convert any private `FakeDataStore` ...`` names a same-wave WP with no edge, and plan
119 does not mention it. It breaks the rule at `README.md:103`; the file would still compile.

**Fix:** replace it with ``**Integrator, WP119**: if WP119 merges after this WP, convert any private `FakeDataStore` in its session-reset tests to `EngineDataStore` as B5 describes, as one commit after both merge.``

### 7. LOW: plan 130 lines 109-110, citations moved by the fixes to plans 123 and 127

`plans/123-always-on-cost-cuts.md:453` is now about `:461`. `plans/127-architecture-refactors.md:603` now lands on an unrelated
spec line (the architecture one-liner), and the `CombatMechanicsHooks` resolve is at `:566`.

**Fix:** cite by content: plan 123 Step A3 and plan 127's `b-code` spec, with no line numbers.

### 8. LOW: plan 123 line 765, a 1.5.x-only test name copied into 1.4.x

`CountingMcm.cs` line 13 at `bb68eeca3` reads `and the static readers they cannot construct (CreatureBanditTuningLiveTests).`
That test is 1.5.x-only, and no step rewrites the comment.

**Fix:** after the `git show` at `:765` add: replace that line with `/// and the static readers they cannot construct.`

### 9. LOW: plan 127 line 14, a leftover word from the removed branches

`the blob and shape checks in Steps 0 and B0` refers to the shapes this fix removed.

**Fix:** write `the blob and form checks in Steps 0 and B0`.

### 10. LOW: plan 108 line 238, the same stale owner finding 16 fixed at `:1591`

`regenerated later by /verify-bindings` should name the owner.

**Fix:** write `regenerated by WP137 Part D`.

## Checked and closed

- **Findings 1, 2:** WP118 is in WP122's Status and Step 0. A1 is the only path, and its sentence rewrite matches plan 118's
  wording. The D2 anchor and flags match the tool test's scope.
- **Finding 3:** plan 133 B3.6, its filter, Stage list and Disposition row are present, and its Status and prerequisites name
  WP125. Plan 125 points at it.
- **Findings 4, 5, 6:** owners are named on both sides. C1.4's old strings equal plan 117's new strings byte for byte, and
  the three map-profiler rows match plan 120's.
- **Finding 7:** plan 136 Step E3 carries the diff, a Verify and a commit.
- **Findings 8, 10, 11, 13, 14, 15, and the rest of 16:** as prescribed. The `bb68eeca3` item 3 row exists at `134:1729`, and
  `121:1531` is in plan 142's starting list.
- **Finding 9:** the rule line, subject and `122:1781` are corrected, and no other plan repeats the old rule.
- **Programme 2, 3, 4:** `README.md:103`, `:105` and the "Count pins" merge note at about `:276`.
- **Programme 5:** every dash in plans 102, 115, 130 and 144 is a quoted old string or a code literal.
- **Plan 120 item 3:** the Language rows helper, its Stage lists, Verify, STOP and Done criterion are all present. `ROWS` text
  matches the English rows, and Parts C and D add no key.
- **Plan 127:** the 14-path count, the four `A` and ten `M` split, the removed spec names and T0 + 77 all agree with what remains.
