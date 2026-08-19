Adversarial review of an uncommitted TAOM changeset. You are the independent verifier: assume the
author was wrong and prove it. Repo root is the working directory.

## What changed

```
M CHANGELOG.md
M Main/_Module/ModuleData/startup_resources/startup_resources_config.xml
M docs/features/startup-resources.md
M tools/rebalance_settlement_prosperity.py
M tools/taom_schema.py
?? TAOM.Tests/Features/StartupResources/StartupResourcesConfigCoverageTests.cs
?? tools/settlement_economy_floor.json
```

Plus one file OUTSIDE the repo and outside git, already written with `--apply`:
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml`
(a `.bak` sibling holds the pre-change bytes).

Start with `git diff` and read the two untracked files in full.

## What the change claims to do

1. **Retune per-culture AI startup gold and influence** in `startup_resources_config.xml`. Gold is
   granted per alive `Occupation.Lord` hero, influence per eligible clan, once, at
   `OnNewGameCreatedPartialFollowUpEvent` index 1. The new values are claimed to be derived as
   `gold = K x runwayDays x avgTroopWage` with `K = 55.93` and tiers 270/150/100/70 days.
   `avgTroopWage` is claimed to be measured per culture by:
   - resolving each noble clan's `default_party_template` from `Main/_Module/ModuleData/characters/clans.xml`
     and `Main/_Module/ModuleData/spclans.xslt` (applied over vanilla `SandBox/ModuleData/spclans.xml`),
   - weighting each `PartyTemplateStack` by `(min_value + max_value) / 2`,
   - mapping troop `level` to tier via `clamp(ceil((level - 5) / 5), 0, 10)` and then to a wage via
     the table in `Main/Features/TroopProgression/TroopCostService.cs`,
   - multiplying by party-wage feats only (mordor x1.20, gundabad x1.10, umbar x1.08), NOT
     garrison-wage feats.
2. **Raise settlement economy** for eight fief-starved cultures in the LIVE `TAOM_Map` module:
   towns to 4800, castles to 950, village hearth to 500, lift-only.
3. **Gate that external edit** with a new `SETTLEMENT_ECONOMY_FLOOR` check in `tools/taom_schema.py`,
   reading the same committed spec (`tools/settlement_economy_floor.json`) the writer uses.
4. **Add `StartupResourcesConfigCoverageTests`** so a lord-owning culture with no config row fails
   the build.

This change SUPERSEDES commit `4f72e160`, which the same day flattened the config to a uniform
250,000 gold / 1,000 influence (elves 500,000). Read that commit. The stated reason for reversing it
is that flat denars are not flat in effect because troop costs differ per culture.

## Attack these specifically

**A. Is the derivation actually reproducible from the repo?** Re-derive at least four cultures'
`avgTroopWage` yourself from the files named above and check the committed gold values follow
`round_to_5000(55.93 x days x wage)`. Report any culture whose value cannot be reproduced. Pay
attention to which clans bind which template: the claim is that 176 of 193 lord-party templates are
per-clan, and that `Culture.bluecraig` clans bind `..._goblin_bluecraig_N_template` (goblin rosters),
and that `battania` (Khand) binds no template of its own and falls through to
`kingdom_hero_party_rhun_template` via `spcultures.xslt`. Verify each of those three claims.

**B. `tools/rebalance_settlement_prosperity.py` correctness.** This writes a live external game file.
- Byte-level round trip: BOM preserved? CRLF preserved? Read `tools/README.md` "XML I/O convention"
  and `.claude/rules/moduledata-validation.md`; the mixed shape (text read + text write) is forbidden.
- The new `_attr_pattern` helper replaced a DOTALL `.*?` with `(?:(?!</Settlement>).)*?` and is now
  shared by the Town/prosperity and Village/hearth writers. Can it still match across a settlement
  boundary, match the wrong attribute, or partial-match a longer attribute name (e.g. `max_prosperity`)?
  Construct a counterexample if one exists.
- **Idempotency is a stated contract.** The existing code excludes `--preserve` and
  `--pin-zero-village` fiefs from the quantile ranking population precisely so a re-run is a no-op.
  The new culture-floor path claims to do the same via `frozen()`. Verify a second run with the same
  flags is genuinely a no-op, and specifically whether ADDING floored fiefs to `frozen()` changes the
  quantile targets of the REMAINING free fiefs versus a run without the flag (it shrinks the ranking
  population, which shifts ranks). If it does, is that a defect, and does it break the contract?
- Precedence: the code now claims quantile < pin < culture floor < preserve. Confirm the code
  actually implements that order and that no earlier assignment survives a later one incorrectly.
- Are `--culture-floor` and `--culture-floor-file` genuinely mutually exclusive, and are the caps
  (`PROSPERITY_CAP` 5600, `HEARTH_CAP` 825) enforced on every path including the file path?

**C. `SETTLEMENT_ECONOMY_FLOOR` in `tools/taom_schema.py`.**
- `build_settlement_economy` must honour TAOM_Map's unconditional `<xsl:template match="Settlement"/>`
  strip the same way `build_settled_cultures` does, or it scores vanilla's 494 deleted settlements.
  Verify the strip handling, the module load order, and the new `_SETTLEMENT_BLOCK_RE` block splitting
  (does a `<Settlement ...  />` self-closing element exist in the data? would it be silently dropped?).
- Does the check degrade correctly with no game install (the commit hook runs with none)?
- Could it produce false positives on hideouts or any settlement with no economy component?
- Is `_read_stripped` (comment stripping) the right reader here, and does the regex set behave on
  attributes in a different order than expected?

**D. `StartupResourcesConfigCoverageTests`.** Does it actually fail when a row is missing, or could
it pass vacuously (both sides empty, wrong path resolution under the test working directory, culture
attribute format mismatch)? The `RepoRoot` is computed as `AppDomain.CurrentDomain.BaseDirectory\..\..\..\..`.
Verify that resolves correctly for this project's test output layout.

**E. Consistency between the three surfaces.** `startup_resources_config.xml` header,
`docs/features/startup-resources.md`, and `CHANGELOG.md` all state numbers. Cross-check every number
in the prose against the actual committed values and against what the code would compute. Flag any
claim that is unsupported, including the "1.89x" and "3.6x" and "25x"/"17.6x" spread figures and the
"176 of 193" template count.

**F. Anything the author did not consider.** In particular: does raising village `hearth` have side
effects beyond income (militia, recruitment pools, food, village production, prosperity growth)? Does
raising town prosperity interact with `TaomSettlementEconomyModel` /
`settlement_economy_config.json` (TAOM uses a `25000 + P*12` gold equilibrium, not vanilla's
`10000 + P*12`)? Is 4800 safe against the documented >6000 negative-growth cliff once in-game growth
is applied?

## Rules of engagement

- Verify against the INSTALLED DLLs for any engine claim: `pwsh tools/taom-src.ps1 path <Type>`.
  `E:\Decompiled_Bannerlord\` may lag; installed DLLs are authoritative.
- Do not report style preferences. Report defects, unsupported claims, and risks.
- For each finding give: severity (P1/P2/P3), file:line, the concrete failure scenario, and the
  minimal fix.
- If you verify a claim and it holds, say so explicitly. A clean verification is a useful result.
- Say plainly if you could not check something and why.
