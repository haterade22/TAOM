# Plan 026: Move TAOM decision logic out of nine protected-virtual seams into their services, so unit tests run it

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: the orchestrator
> keeps that index and updates the 026 row from your report.
>
> **Where you work**: the worktree `E:/repos/taom-improve/wt-026` on branch
> `improve/026-seam-decision-logic`, checked out at `e452b0c7`. The plan was written at `31cc629f`;
> the worktree was then fast-forwarded to `e452b0c7` (plan 021 merged; no file under `Main` or
> `TAOM.Tests` changed), so every line number below holds there. If the orchestrator gave you a
> different worktree path or branch name, use those instead everywhere this plan names these.
> Every Read, Edit and Write path below is relative to that worktree (for example
> `E:/repos/taom-improve/wt-026/Main/Features/Refuge/WardenService.cs`). The main checkout
> `E:/repos/TAOM` holds another session's uncommitted work: never edit, build, stage or commit
> anything there. Run every command with the **Bash tool** and pass `timeout: 600000` to every
> `dotnet` command.
>
> **Copy every command whole, prefix included.** The Bash tool's working directory resets between
> calls, and it may reset to the main checkout `E:/repos/TAOM`. So every command in "Steps" and
> "Done criteria" begins with `cd E:/repos/taom-improve/wt-026 && `. The exceptions, which work
> from any directory as written: the drift check (it uses `git -C E:/repos/TAOM` on purpose), the
> `mkdir` of absolute scratch paths, the RED-step `grep` commands over absolute log paths, and
> `date +%F`. If you ever type a command yourself, start it with the prefix.
>
> **Totals lines**: `dotnet test` pads its counts (`Failed:     0, Passed:    55`), while most
> expected totals lines in this plan are written unpadded (`Failed: 0, Passed: 55`). Compare the
> numbers, never the spacing.
>
> **Disk**: the C: drive is nearly full. Every `dotnet` command carries
> `TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp ` (Step 0 creates
> that folder). Scratch files (logs, the commit message) go in
> `E:/repos/taom-improve/scratch/plans/026/`, never in the repo, never under `C:` or `/tmp`. Never
> create a clone, archive or copy of the repository. Commands that pipe `dotnet` into `tee` start
> with `set -o pipefail` and end with `; echo "exit=$?"`, so the printed `exit=` is dotnet's own
> exit code.
>
> **Editing**: all eight source files use CRLF line endings, and `RefugeService.cs`,
> `RefugeServiceTests.cs`, `CampService.cs` and `CampServiceTests.cs` start with a UTF-8 BOM. Edit
> them with the Edit tool only (never `sed -i`, never a script that rewrites the file). Keep the
> 4-space indentation you see. Several steps edit the same file one after another, so later steps
> anchor on text, not on line numbers; a line number given "at `31cc629f`" is the position before
> any step's edit.
>
> **This plan file** is untracked in the worktree: `git status` shows
> `?? plans/026-seam-decision-logic.md`. That is expected. Never stage, commit, edit or delete it.
> The plan's cold review may also sit untracked in the worktree, as
> `?? plans/_audit/2026-09-23-opus/plan-review-026.md`. That line is expected too, whether or not it
> is present; treat that file exactly like the plan file (never stage, commit, edit or delete it).
> Every `git status` expectation below allows it.
>
> **Full-suite timeout**: the full suite (about 10,600 tests) runs under the Bash tool's
> 600000 ms limit. If a `dotnet test` command returns without printing an `exit=` line, the tool
> killed it: that is a timeout, not a test failure. Re-run the same command with
> `run_in_background: true`, wait for the completion notice, then read the last 5 lines of its
> `tee` log and use those as the result.
>
> **Drift check (run first, from the main tree, before you touch the worktree)**:
>
> ```bash
> git -C E:/repos/TAOM fetch origin bannerlord-1.5.x && git -C E:/repos/TAOM diff --stat 31cc629f..origin/bannerlord-1.5.x -- Main/Features/SupplyLines/SupplyOrderService.cs Main/Features/Refuge/RefugeService.cs Main/Features/Refuge/WardenService.cs Main/Features/FieldCamp/CampService.cs TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs TAOM.Tests/Features/Refuge/RefugeServiceTests.cs TAOM.Tests/Features/Refuge/WardenServiceTests.cs TAOM.Tests/Features/FieldCamp/CampServiceTests.cs
> ```
>
> This compares the planned commit `31cc629f` with the remote branch tip. Use `origin/`: the local
> `bannerlord-1.5.x` in `E:/repos/TAOM` sat at `31cc629f` itself when this plan was written, so a
> diff against it compares the commit with itself. (The worktree's own `e452b0c7` is a
> fast-forward of `31cc629f` that touched none of these paths; Step 0.2's worktree checks are the
> other guard.) If the fetch fails (no network), say so in your report and run the `diff` alone
> against the last fetched `origin/bannerlord-1.5.x`. Empty output: go on. Any output: compare the
> "Current state" excerpts below against
> `git -C E:/repos/TAOM show origin/bannerlord-1.5.x:<path>` for each listed file;
> on any mismatch, treat it as a STOP condition. `CHANGELOG.md` churns every session and is left out
> of the check on purpose (you only add an entry at its top).

## Status

- **Priority**: P3
- **Effort**: L (9 files, 76 new tests; each step is mechanical)
- **Risk**: LOW to MED (behaviour-preserving refactor; the moved logic is fully unit-tested, but
  the re-shaped seam bodies run only in game)
- **Depends on**: none to execute. The rule this plan satisfies is ADR-007 "Protected-Virtual
  Boundary Seams", on trunk since plan 021 merged (`b177ecbc`): in the worktree it is
  `docs/adrs/007-adapter-pattern.md:584-600`, and its text is quoted below.
- **Category**: tech-debt (testability)
- **Planned at**: commit `31cc629f`, 2026-09-25 (branch `bannerlord-1.5.x`); extended the same day
  for decision 57. The worktree was then fast-forwarded to `e452b0c7` (plan 021 merged; no code
  changed: `git diff --name-only 31cc629f e452b0c7 -- Main TAOM.Tests` prints nothing). The drift
  check still compares against `31cc629f`.
- **Issue**: create before implementation lands (orchestrator). If the orchestrator gives you an
  issue number, put `Refs #<number>` in the commit body; otherwise leave it out.
- **Maintainer decisions** (numbered answers from the 2026-09-23 audit sprint):
  - Decision 49: the looser seam wording (quoted in "The rule" below).
  - Decision 55 (2026-09-25): "a private helper that only seams call (an id lookup such as
    `RefugeService.FindParty`, `FindHero`, `FindTroop`) counts as part of the seam body;
    condition 1 applies to the calling seam's signature."
  - Decision 56 (2026-09-25): the four seams that break condition 2: "Refactor them now".
  - Decision 57 (2026-09-25): plan 026 also refactors the three seams the plan 021 convergence pass
    found carrying filter or routing logic: `SupplyOrderService.RefundConsumption` (with its helper
    `RestoreVolunteerSlots`), `RefugeService.ReleasePeacePrisoners`, and the fortification-distance
    search, which exists twice (`CampService.DistanceToNearestFortification`, and in
    `RefugeService` `DistanceToNearestFortification` and `DistanceToNearestFortificationFrom` with
    their helper `DistanceToNearestFortificationFromPosition`).

## Why this matters

`SupplyOrderService`, `RefugeService`, `WardenService` and `CampService` keep their engine access in
`protected virtual` members ("seams") that the unit tests override on a test subclass. Nine of those
seams also hold TAOM decisions: who gets paid for a supply order and what happens when the payee is
gone; where a failed order's goods, volunteers and troops go back to and which counts move; which
hostile party raids a refuge; which refuge-held prisoners a peace frees; which heroes count as
warden candidates; how a promoted soldier's companion template and age are chosen; and which
settlements count as the "nearest town" that keeps camps and refuges away. Because every test
subclass overrides the seam, **no unit test runs any of that logic today**; a regression there ships
silently. After this plan each decision lives in the service where tests run it, each seam is one
engine operation, the two identical fortification searches share one pure rule, the behaviour in
game is unchanged, and 76 new tests pin the moved logic.

## Current state

The excerpts for seams 1 to 4 were read at `31cc629f`; those for seams 5 to 7 (decision 57) were
read in the worktree at `e452b0c7`, whose `Main` and `TAOM.Tests` are identical to `31cc629f`. Line
numbers are the same at both commits and describe the files before any step's edit.

### The rule (ADR-007 "Protected-Virtual Boundary Seams", `docs/adrs/007-adapter-pattern.md:584-600`, on trunk since plan 021 merged at `b177ecbc`)

> A service may keep its engine access in `protected virtual` members of its own class, instead of
> behind an adapter, when all four conditions hold:
>
> 1. **Seam signatures use ids, value types and TAOM types only**: string ids, primitives, enums,
>    TAOM data classes, engine structs such as `Vec2` and `CampaignTime`, and the value-like types
>    under "Value Types Don't Need Adapters"; never a sealed TaleWorlds class.
> 2. **Each seam body is one engine operation**: the engine calls or queries for one action, with
>    their null guards and id lookups, and no TAOM decision logic.
> 3. **The service's interface stays engine-free**: callers still pass ids or adapters.
> 4. **A test subclass overrides every seam the tests reach**, so every decision path runs in unit
>    tests without the game.
>
> A private helper that only seams call, such as an id lookup (`FindParty`, `FindHero`), is part of
> the seam body: condition 1 applies to the seam that calls it, not to the helper's own signature
> (decision 55).

An opaque `object` handle carried in a TAOM data class (for example `RaidThreat.EngineParty`) is the
accepted way to hand an engine object from one seam to another without putting a sealed type in a
signature.

The ADR's "Why" paragraph (same file, lines 602-612) names the first four seams and says "plan 026
moves that logic into the services (decision 56). A seam that breaks condition 2 is a finding
wherever it is found, not only in this list." Decision 57 adds seams 5 to 7 on that basis. Do not
edit the ADR: `docs/adrs/` is out of scope.

### Files and roles

| File | Lines | Role | Action |
|---|---|---|---|
| `Main/Features/SupplyLines/SupplyOrderService.cs` | 654 | Supply order book; seam `ChargePlayer` at 469-514 (doc comment 469-476, method 477-514); seam `RefundConsumption` at 574-624 (doc comment 574-578) and its helper `RestoreVolunteerSlots` at 626-647 | split `ChargePlayer` into service logic plus three gold seams; split `RefundConsumption` into service logic plus six refund seams |
| `Main/Features/Refuge/RefugeService.cs` | 1226 | Refuge book and lifecycle; seam `FindNearestHostile` at 1166-1204; fortification seams at 830-854; seam `ReleasePeacePrisoners` at 1019-1042 | split each into service logic plus seams; add three data classes |
| `Main/Features/Refuge/WardenService.cs` | 298 | Warden candidates and promotion; seams `CompanionsInMainParty` at 128-151 and `MintCompanionFromTroop` at 193-235 | split both; add two data classes |
| `Main/Features/FieldCamp/CampService.cs` | 955 | Field camps; seam `DistanceToNearestFortification` at 745-761 | split into service logic plus one scan seam; add the shared `SettlementSite` struct and `FortificationSearch` rule |
| `TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs` | 822 | 45 tests, class tag `RequiresGame`; subclass `TestableSupplyOrderService` (line 22) | re-shape the subclass, change 3 tests' assertions, add 23 tests |
| `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs` | 1797 | 112 tests, class tag `RequiresGame`; subclasses `TestableRefugeService` (line 27) and `HeroMergeProbeService` (line 1713) | re-shape `TestableRefugeService`, add 27 tests; `HeroMergeProbeService` unchanged |
| `TAOM.Tests/Features/Refuge/WardenServiceTests.cs` | 314 | 20 tests, **no** `RequiresGame` tag (runs on hosted CI against reference assemblies); subclass `TestableWardenService` (line 19) | re-shape the subclass, add 16 tests |
| `TAOM.Tests/Features/FieldCamp/CampServiceTests.cs` | 1257 | 81 tests, class tag `RequiresGame`; subclass `TestableCampService` (line 27), the only subclass of `CampService` | re-shape the subclass, add 10 tests |
| `CHANGELOG.md` | n/a | Project changelog | add 1 entry at the top |

Every reference to the first four seams in the repo (from
`git grep -n "ChargePlayer\|FindNearestHostile\|CompanionsInMainParty\|MintCompanionFromTroop" 31cc629f -- "*.cs" "*.md"`,
excluding `plans/`): the definitions above, their single callers
(`SupplyOrderService.cs:185`, `RefugeService.cs:414`, `WardenService.cs:48`, `WardenService.cs:85`),
the test overrides (`SupplyOrderServiceTests.cs:61`, `RefugeServiceTests.cs:175`,
`WardenServiceTests.cs:36`, `WardenServiceTests.cs:45`), and the unrelated one-call seams
`CampService.ChargePlayer(int)` and `RefugeService.ChargePlayer(int)` (out of scope). At
`31cc629f` no doc under `docs/` names any of the four. At the worktree `HEAD` (`e452b0c7`) four
docs do: `docs/adrs/007-adapter-pattern.md`,
`docs/reviews/deep-review-021-architecture-rule-amendments-2026-09-24.md`,
`docs/reviews/lessons/misc.md` and `docs/reviews/rca-architecture-rule-amendments-2026-09-24.md`.
They are the ADR and plan 021's review records, not feature docs, so no doc changes.

Every reference to seams 5 to 7 (from
`git grep -n -E "DistanceToNearestFortification|ReleasePeacePrisoners|RefundConsumption|RestoreVolunteerSlots" -- "*.cs" "*.md" ":!plans"`
in the worktree): the definitions above; the callers `SupplyOrderService.cs:151` and `:175`
(`RefundConsumption`, the two failed-placement paths of `TryPlaceOrder`), `RefugeService.cs:519`
(`OnPeaceMade`), `CampService.cs:192` (`CanEstablish`), `RefugeService.cs:156` (`CanFound`) and
`:259` (`CanUpgrade`, the `From` variant); the test overrides `SupplyOrderServiceTests.cs:86`,
`RefugeServiceTests.cs:102-103` and `:189`, `CampServiceTests.cs:94`. Four more hits are unrelated
and out of scope, all the other method `GetNormalizedDistanceToNearestFortification` (ArmyTargeting):
`Main/Adapters/IMapReachAdapter.cs:31`, `Main/Adapters/MapReachAdapter.cs:52`,
`Main/Features/ArmyTargeting/Hooks/AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:86`
and `docs/features/army-targeting.md:79`. The only other doc hit is
the plan 021 review record `docs/reviews/deep-review-021-architecture-rule-amendments-2026-09-24.md:305-306`,
which found these seams; it is a record, not a feature doc, so no doc changes.

### Seam 1: `Main/Features/SupplyLines/SupplyOrderService.cs:469-519` (`ChargePlayer` 469-514, `FindHero` 516-519)

The only caller is `TryPlaceOrder`, line 185: `ChargePlayer(source, quote);`. The seam region
starts at line 455 with the comment
`    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`.

```csharp
    /// <summary>
    /// Takes the player's gold and credits the source its share (the goods and troops at their
    /// quoted prices), matching vanilla purchases where the town or lord is paid. The port
    /// previously destroyed the whole payment while real stock and soldiers left the source, a
    /// one-way economy sink the #317 town ledger would show as unexplained (review round B).
    /// Transport and guard fees ARE destroyed, deliberately: they pay the carriers, who are not
    /// economy actors, exactly like vanilla mercenary wages.
    /// </summary>
    protected virtual void ChargePlayer(SupplySourceInfo source, SupplyQuote quote)
    {
        int sourceShare = quote.Goods + quote.Troops;
        int fees = quote.Transport + quote.Guard;

        if (sourceShare > 0)
        {
            if (!string.IsNullOrEmpty(source.HeroId))
            {
                var lord = FindHero(source.HeroId);
                if (lord != null)
                {
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, lord, sourceShare, disableNotification: true);
                }
                else
                {
                    _logger.LogWarning($"[SupplyLines] charge: lord '{source.HeroId}' unreachable, his share is destroyed");
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, sourceShare, disableNotification: true);
                }
            }
            else
            {
                var settlement = Settlement.Find(source.SettlementId);
                if (settlement != null)
                {
                    GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, sourceShare, disableNotification: true);
                }
                else
                {
                    _logger.LogWarning($"[SupplyLines] charge: settlement '{source.SettlementId}' unreachable, its share is destroyed");
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, sourceShare, disableNotification: true);
                }
            }
        }

        if (fees > 0)
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, fees, disableNotification: true);
    }

    // Heroes register with CampaignObjectManager only (Hero.cs:1467-1480, verified 1.4.8);
    // MBObjectManager.GetObject<Hero> reads XML type records and misses runtime heroes.
    private static Hero FindHero(string heroId) =>
        string.IsNullOrEmpty(heroId) ? null : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
```

Decision logic to move: the share/fee split, the payee routing (a non-empty `HeroId` pays that lord,
otherwise the settlement), the unreachable-payee fallback (warn, then destroy the share), and "fees
are always destroyed". `FindHero` stays (it is also used by `RefundConsumption`, line 585).

Pure data it uses (`Main/Features/SupplyLines/Domain/SupplyQuote.cs`, whole type):

```csharp
public readonly struct SupplyQuote
{
    public SupplyQuote(int goods, int troops, int transport, int guard) { ... }
    public int Goods { get; }
    public int Troops { get; }
    public int Transport { get; }
    public int Guard { get; }
    public int Total => Goods + Troops + Transport + Guard;
}
```

`SupplySourceInfo` (`Main/Features/SupplyLines/ISupplySourceService.cs:7`) is a public sealed POCO
with public fields `SettlementId`, `HeroId`, `DisplayName`, `RelationText`, `Distance`, `CanOrder`,
`DisabledReason`.

Test subclass today (`SupplyOrderServiceTests.cs:33-34` and `:61-65`):

```csharp
        public readonly List<(SupplySourceInfo Source, SupplyQuote Quote)> Charges =
            new List<(SupplySourceInfo, SupplyQuote)>();
...
        protected override void ChargePlayer(SupplySourceInfo source, SupplyQuote quote)
        {
            CallSequence.Add("charge");
            Charges.Add((source, quote));
        }
```

`Charges` is read by four tests: three assert `Assert.AreEqual(0, _sut.Charges.Count);`
(lines 224, 238, 253; they keep compiling and passing unchanged), and
`TryPlaceOrder_Success_ChargesOnlyAfterConsumeAndSpawn` (lines 257-275) asserts:

```csharp
        CollectionAssert.AreEqual(
            new[] { "consume", "spawn", "charge" }, _sut.CallSequence,
            "the charge must land only after the caravan exists (the source module charged first)");
        Assert.AreEqual(170, _sut.Charges.Single().Quote.Total);
        Assert.AreSame(_townSource, _sut.Charges.Single().Source,
            "the charge carries the source so its share can be credited (round B: payments were a pure sink)");
```

(The test setup quotes `new SupplyQuote(goods: 100, troops: 50, transport: 20, guard: 0)` for every
order and `_townSource = new SupplySourceInfo { SettlementId = "town_G1", CanOrder = true }`.)

### Seam 2: `Main/Features/Refuge/RefugeService.cs:1166-1204`, plus `RaidThreat` at 21-33

The only caller is `HourlyTick`, line 414: `var threat = FindNearestHostile(pair.Key, raidRange);`.
`HourlyTick` has already returned when raids are off (the `EnableRaids` gate, lines 396-397), read
`raidRange` (line 398) and returned on a non-finite or non-positive `raidRange` (lines 399-400). The seam
region starts at line 787 (`    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`);
the "internals" section ends just above it with `SaneBuildHours()` (lines 777-785).

```csharp
/// <summary>
/// One hostile party eligible to raid a refuge. Built at the campaign boundary; the decision
/// logic reads the plain fields and <see cref="EngineParty"/> rides along as an opaque handle so
/// the boundary can start the battle without a second lookup (the AmbushCandidate precedent).
/// </summary>
public sealed class RaidThreat
{
    public string PartyId;
    public string Name;

    /// <summary>The engine <c>MobileParty</c>; null in tests, opaque to the decision logic.</summary>
    public object EngineParty;
}
```

```csharp
    /// <summary>Nearest active hostile party with at least one soldier inside range, or null.
    /// Straight-line distance, like the source; a raid trigger does not need pathfinding.</summary>
    protected virtual RaidThreat FindNearestHostile(string refugePartyId, float range)
    {
        var refuge = FindParty(refugePartyId);
        var refugeFaction = refuge?.MapFaction;
        if (refuge == null || refugeFaction == null)
            return null;

        var position = refuge.GetPosition2D;
        MobileParty best = null;
        float bestDistance = range;
        foreach (var party in MobileParty.All)
        {
            if (party == null || party.IsMainParty || !party.IsActive || party.MapEvent != null)
                continue;
            if (party.PartyComponent is RefugePartyComponent)
                continue;
            var faction = party.MapFaction;
            if (faction == null || !faction.IsAtWarWith(refugeFaction))
                continue;
            if ((party.MemberRoster?.TotalManCount ?? 0) < 1)
                continue;
            float distance = position.Distance(party.GetPosition2D);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = party;
            }
        }
        if (best == null)
            return null;
        return new RaidThreat
        {
            PartyId = best.StringId,
            Name = best.Name?.ToString(),
            EngineParty = best,
        };
    }
```

`StartRaid` (line 1206) casts `threat?.EngineParty is MobileParty enemy`, so the handle must stay the
engine `MobileParty`. `FindParty` (line 1218) is
`Campaign.Current?.CampaignObjectManager?.Find<MobileParty>(partyId)`, a linear scan.

Decision logic to move: the six filters (null; main party, inactive, in a map event; another refuge;
faction missing or not at war; no soldiers) and the nearest pick (strict `<` starting from `range`, so
the first party found wins a tie, a party exactly at `range` is out, and a NaN distance never wins).

**Precedent** for the split: `Main/Features/FieldCamp/CampService.cs` defines `AmbushCandidate`
(lines 18-36: plain fields plus `public object EngineParty`), a seam
`protected virtual IReadOnlyList<AmbushCandidate> EnumerateHostileCandidates(float reach)` (line 797)
that snapshots parties, and the service picks the target in `TrySpringAmbush` (from line 566).

Test subclass today (`RefugeServiceTests.cs:55-56` and `:175-179`):

```csharp
        public RaidThreat Threat;
        public int ThreatSearches;
...
        protected override RaidThreat FindNearestHostile(string refugePartyId, float range)
        {
            ThreatSearches++;
            return Threat;
        }
```

Nine raid tests (in the `// --- Raids ---` section, lines 1040-1222) arrange `_sut.Threat = new RaidThreat { PartyId = "enemy", Name = "Enemy" };`
or `_sut.Threat = null;` and assert on `ThreatSearches`, `RaidsStarted`, `Messages`, `Events` and
`TroopAdds`. Setup (line 219) sets `_settings.RaidRange.Returns(6f)`. They must keep passing with
their bodies unchanged. The second subclass, `HeroMergeProbeService` (lines 1713-1751), overrides only
merge seams and never reaches raids; leave it alone.

### Seam 3: `Main/Features/Refuge/WardenService.cs:128-151`

The only caller is `Candidates()`, line 48: `foreach (var companion in CompanionsInMainParty())`
(it skips null entries, then appends promotable troops when a companion slot is free). The seam region
starts at line 126 (`    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`),
right after `UnwindPromotion` (lines 112-124).

```csharp
    protected virtual IReadOnlyList<WardenCandidate> CompanionsInMainParty()
    {
        var result = new List<WardenCandidate>();
        var roster = MobileParty.MainParty?.MemberRoster;
        var clan = Clan.PlayerClan;
        if (roster == null || clan == null)
            return result;
        for (int i = 0; i < roster.Count; i++)
        {
            var character = roster.GetCharacterAtIndex(i);
            var hero = character?.HeroObject;
            if (hero == null || hero == Hero.MainHero)
                continue;
            if (hero.CompanionOf != clan)
                continue;
            result.Add(new WardenCandidate
            {
                Id = hero.StringId,
                DisplayName = hero.Name?.ToString(),
                IsCompanion = true,
            });
        }
        return result;
    }
```

Decision logic to move: skip the main hero; keep only heroes whose `CompanionOf` is the player clan.
`WardenCandidate` (`Main/Features/Refuge/IWardenService.cs:6-19`) is a public sealed POCO with
`string Id`, `string DisplayName`, `bool IsCompanion`, `int Tier`.

### Seam 4: `Main/Features/Refuge/WardenService.cs:193-235`, constants at 34-36

The only caller is `ResolveWarden`, line 85: `string heroId = MintCompanionFromTroop(candidate.Id);`
(null means the promotion failed and the soldier is kept).

```csharp
    /// <summary>Random spread on a minted companion's age above coming-of-age (source value).</summary>
    private const int PromotedAgeSpreadYears = 14;
    private const int PromotedAgeBaseOffsetYears = 4;
```

```csharp
    /// <summary>
    /// Mints a companion hero from a troop: culture-matched companion template,
    /// HeroCreator.CreateSpecialHero into the player clan, renamed to the troop so "a Rohan
    /// Spearman became Captain-of-sorts" reads on screen, activated, AddCompanionAction, and
    /// placed in the main party so the founding flow can then move him into the refuge.
    /// Returns the hero StringId, or null when any engine step refuses.
    /// </summary>
    protected virtual string MintCompanionFromTroop(string troopId)
    {
        var troop = FindTroop(troopId);
        var clan = Clan.PlayerClan;
        var mainParty = MobileParty.MainParty;
        if (troop == null || troop.IsHero || clan == null || mainParty == null)
            return null;

        var culture = troop.Culture ?? Hero.MainHero?.Culture;
        var template = CharacterHelper.GetRandomCompanionTemplateWithPredicate(
                c => culture == null || c.Culture == culture)
            ?? CharacterHelper.GetRandomCompanionTemplateWithPredicate();
        if (template == null)
            return null;

        int comesOfAge = Campaign.Current?.Models?.AgeModel?.HeroComesOfAge ?? 18;
        int age = comesOfAge + PromotedAgeBaseOffsetYears + MBRandom.RandomInt(PromotedAgeSpreadYears);
        var hero = HeroCreator.CreateSpecialHero(template, bornSettlement: null, faction: clan, supporterOfClan: null, age: age);
        if (hero == null)
            return null;

        try
        {
            // The rename is cosmetic; a template-named hero is still a working warden, so a
            // localization hiccup here must not abort the promotion (source behaviour).
            hero.SetName(troop.Name, troop.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Refuge] promoted-warden rename failed: {ex.Message}");
        }
        hero.ChangeState(Hero.CharacterStates.Active);
        AddCompanionAction.Apply(clan, hero);
        AddHeroToPartyAction.Apply(hero, mainParty, showNotification: false);
        return hero.StringId;
    }
```

Private helpers at the end of the file (lines 293-297), both kept:

```csharp
    private static Hero FindHero(string heroId) =>
        string.IsNullOrEmpty(heroId) ? null : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);

    private static CharacterObject FindTroop(string troopId) =>
        string.IsNullOrEmpty(troopId) ? null : MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
```

Decision logic to move: refuse a hero troop; the culture choice (troop culture, else the main hero's);
the template fallback (culture-matched, else any); the age policy (coming-of-age, default 18, plus 4,
plus one `RandomInt(14)` draw); the rename-failure tolerance (warn and carry on).

Test subclass today (`WardenServiceTests.cs:19-71`): `Companions` (list of `WardenCandidate`) returned
by the `CompanionsInMainParty` override (line 36); `MintResult = "hero_minted"` and `MintCalls`
(lines 25-26) used by the `MintCompanionFromTroop` override (lines 45-49):

```csharp
        protected override string MintCompanionFromTroop(string troopId)
        {
            MintCalls++;
            return MintResult;
        }
```

The ResolveWarden tests (lines 144-222) assert `MintCalls` equals 0 or 1 and set `MintResult = null`
for the "mint refused" case. Setup (line 79) is
`_sut = new TestableWardenService(Substitute.For<IModLogger>());`.

### Seam 5: `Main/Features/SupplyLines/SupplyOrderService.cs:574-647` (`RefundConsumption` 574-624, `RestoreVolunteerSlots` 626-647)

The callers are both in `TryPlaceOrder`: line 151 (the order is unaffordable) and line 175 (the
caravan did not spawn), each `RefundConsumption(source, consumption);`. It sits between
`DeliverCargoToPlayer` (ends at line 572) and `ShowMessage` (line 649).

```csharp
    /// <summary>
    /// Puts a consumption back where it came from after a failed placement: goods to the settlement
    /// roster, volunteers into empty notable slots, lord troops to the lord's roster. Best effort;
    /// a partial refund is logged rather than thrown because the player has not been charged.
    /// </summary>
    protected virtual void RefundConsumption(SupplySourceInfo source, SupplyConsumption consumption)
    {
        try
        {
            if (!string.IsNullOrEmpty(source.HeroId))
            {
                var roster = FindHero(source.HeroId)?.PartyBelongedTo?.MemberRoster;
                if (roster == null)
                {
                    _logger.LogWarning($"[SupplyLines] refund: lord '{source.HeroId}' unreachable, troops not restored");
                    return;
                }
                foreach (var pair in consumption.Troops)
                {
                    var troop = MBObjectManager.Instance.GetObject<CharacterObject>(pair.Key);
                    if (troop != null && pair.Value > 0)
                        roster.AddToCounts(troop, pair.Value);
                }
                return;
            }

            var settlement = Settlement.Find(source.SettlementId);
            if (settlement == null)
            {
                _logger.LogWarning($"[SupplyLines] refund: settlement '{source.SettlementId}' unreachable, stock not restored");
                return;
            }
            foreach (var pair in consumption.Goods)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(pair.Key);
                if (item != null && pair.Value > 0)
                    settlement.ItemRoster?.AddToCounts(item, pair.Value);
            }
            foreach (var pair in consumption.Troops)
            {
                var troop = MBObjectManager.Instance.GetObject<CharacterObject>(pair.Key);
                if (troop == null || pair.Value <= 0)
                    continue;
                RestoreVolunteerSlots(settlement, troop, pair.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SupplyLines] refund after failed order placement threw: {ex.Message}");
        }
    }

    private static void RestoreVolunteerSlots(Settlement settlement, CharacterObject troop, int count)
    {
        if (settlement.Notables == null)
            return;
        int remaining = count;
        foreach (var notable in settlement.Notables)
        {
            var slots = notable?.VolunteerTypes;
            if (slots == null)
                continue;
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = troop;
                    remaining--;
                }
            }
            if (remaining <= 0)
                return;
        }
    }
```

Decision logic to move: the route (a non-empty `HeroId` returns troops to the lord's party roster
and ignores goods; otherwise goods go to the settlement's item roster and troops into empty
volunteer slots); the two unreachable-source warnings (warn, restore nothing); the count filter
(only positive counts move; an unknown id is dropped by its lookup); the slot walk (notables in
order, skipping a null notable or a null slot array, first empty slot first, stopping once the
count is placed, and **silently dropping** recruits beyond the free slots); the catch-all that logs
any exception as an error and swallows it.

`SupplyConsumption` (`Main/Features/SupplyLines/ISupplySourceService.cs:75-81`) is a public sealed
POCO: `Dictionary<string, int> Goods`, `Dictionary<string, int> Troops` (both public fields,
initialised to empty dictionaries), `float GoodsMarketValue`, `int TroopRecruitCost`. Goods and
troops are refunded in the dictionaries' enumeration order (insertion order), as before.

Test subclass today (`SupplyOrderServiceTests.cs:38` and `:86-90`):

```csharp
        public readonly List<SupplyConsumption> Refunds = new List<SupplyConsumption>();
...
        protected override void RefundConsumption(SupplySourceInfo source, SupplyConsumption consumption)
        {
            CallSequence.Add("refund");
            Refunds.Add(consumption);
        }
```

`Refunds` is read by three tests: `TryPlaceOrder_NothingObtained_FailsWithoutRefundOrCharge`
(line 223, `Assert.AreEqual(0, _sut.Refunds.Count, "nothing was taken, so nothing must be refunded");`,
keeps compiling and passing unchanged), and `TryPlaceOrder_UnaffordableTotal_RefundsAndDoesNotCharge`
and `TryPlaceOrder_SpawnFails_RefundsAndDoesNotCharge`, which each assert, on lines 237 and 252,
the identical line `        Assert.AreSame(consumption, _sut.Refunds.Single());` after
`var consumption = ArrangeConsumption();` (grain 5, `troop_a` 2) with `_townSource`. No test
asserts a `"refund"` entry in `CallSequence`.

### Seam 6: `Main/Features/Refuge/RefugeService.cs:1019-1042`

The only caller is `OnPeaceMade`, lines 516-520:
`foreach (var partyId in _refuges.Keys) ReleasePeacePrisoners(partyId);`. It sits between
`RemoveRegularRows` (ends at line 1017) and `DestroyRefugeParty` (line 1044).

```csharp
    /// <summary>Releases the refuge's hero prisoners who are no longer at war with the refuge's
    /// faction, mirroring vanilla PrisonerReleaseCampaignBehavior.ReleasePartyPrisoners (which
    /// only enumerates caravans, war parties, villages and garrisons - never a custom
    /// component). Called after a peace involving the player's faction.</summary>
    protected virtual void ReleasePeacePrisoners(string partyId)
    {
        var refuge = FindParty(partyId);
        var refugeFaction = refuge?.MapFaction;
        if (refuge == null || refugeFaction == null)
            return;
        var roster = refuge.PrisonRoster;
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var hero = roster.GetCharacterAtIndex(i)?.HeroObject;
            if (hero == null || hero == Hero.MainHero)
                continue;
            if (hero.MapFaction != null && hero.MapFaction.IsAtWarWith(refugeFaction))
                continue;
            if (hero.PartyBelongedToAsPrisoner == refuge.Party)
                EndCaptivityAction.ApplyByPeace(hero);
            else
                roster.RemoveTroop(hero.CharacterObject);
        }
    }
```

Decision logic to move: the walk (row count read once, rows read one at a time from the last to the
first, because a release or removal takes a row out and a backwards walk keeps every unread row at
its index); skip a row without a hero and the main hero (before any faction is read); keep a hero
still at war with the refuge's faction; then the branch: a prisoner the refuge itself holds is freed
with `EndCaptivityAction.ApplyByPeace`, a row whose recorded captor is another party is only removed.

Test subclass today (`RefugeServiceTests.cs:187-190`):

```csharp
        public readonly List<string> PeaceReleasedParties = new List<string>();

        protected override void ReleasePeacePrisoners(string partyId) =>
            PeaceReleasedParties.Add(partyId);
```

Its one reader is `OnPeaceMade_ReleasesForEveryRefuge` (lines 1339-1348: seeds `r1` and `r2`, calls
`_sut.OnPeaceMade()`, asserts `CollectionAssert.AreEquivalent(new[] { "r1", "r2" }, _sut.PeaceReleasedParties);`).
It must keep passing with its body unchanged. The section that follows it starts at line 1350 with
`    // --- ResetForNewSession (the singleton-leak fix) ---`.

### Seam 7: the fortification-distance search, two copies

`Main/Features/FieldCamp/CampService.cs:745-761` (caller `CanEstablish`, line 192:
`&& DistanceToNearestFortification() < minDistance)`, only for Field and Fortified camps):

```csharp
    protected virtual float DistanceToNearestFortification()
    {
        var party = MobileParty.MainParty;
        if (party == null)
            return float.MaxValue;
        var position = party.GetPosition2D;
        float nearest = float.MaxValue;
        foreach (var settlement in Settlement.All)
        {
            if (settlement == null || (!settlement.IsTown && !settlement.IsCastle))
                continue;
            float distance = position.Distance(settlement.GetPosition2D);
            if (distance < nearest)
                nearest = distance;
        }
        return nearest;
    }
```

It sits between `CurrentTerrain()` (ends at line 743) and `protected virtual int PlayerGold => Hero.MainHero?.Gold ?? 0;`
(line 763). The CampService seam region starts at line 659 with
`    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`, after
the method that ends at line 657. `AmbushCandidate` is at lines 18-37 (doc comment 18-22, class
23-37); `CampService.cs` line 1 is `using System;` and it imports `System.Collections.Generic`.

`Main/Features/Refuge/RefugeService.cs:830-854` (callers `CanFound`, line 156:
`&& DistanceToNearestFortification() < minTownDistance)`, and `CanUpgrade`, line 259:
`&& DistanceToNearestFortificationFrom(refuge.PartyId) < minDistance)`):

```csharp
    protected virtual float DistanceToNearestFortification()
    {
        var main = MobileParty.MainParty;
        return main == null ? float.MaxValue : DistanceToNearestFortificationFromPosition(main.GetPosition2D);
    }

    protected virtual float DistanceToNearestFortificationFrom(string partyId)
    {
        var party = FindParty(partyId);
        return party == null ? float.MaxValue : DistanceToNearestFortificationFromPosition(party.GetPosition2D);
    }

    private static float DistanceToNearestFortificationFromPosition(Vec2 position)
    {
        float nearest = float.MaxValue;
        foreach (var settlement in Settlement.All)
        {
            if (settlement == null || (!settlement.IsTown && !settlement.IsCastle))
                continue;
            float distance = position.Distance(settlement.GetPosition2D);
            if (distance < nearest)
                nearest = distance;
        }
        return nearest;
    }
```

It sits between `PartyPosition` (ends at line 828) and the doc comment of `SpawnRefugeParty`
(line 856, `    /// <summary>` above `    /// Creates the stationary refuge party at the player's position`).
`RefugeService.cs` already has `using TAOM.Features.FieldCamp;` (line 6), so a type declared in
`CampService.cs` (namespace `TAOM.Features.FieldCamp`) is visible there without a new `using`.

Decision logic to move: which settlements count (towns and castles only, a null entry skipped) and
the nearest pick (strict `<` from `float.MaxValue`: nothing found gives `float.MaxValue`, and a NaN
distance never wins). The two copies are the same rule, character for character. They become one
pure function, `FortificationSearch.NearestDistance`, declared in `CampService.cs` beside
`AmbushCandidate` (RefugeService already depends on the FieldCamp namespace; FieldCamp never
depends on Refuge), fed by a seam in each service that only lists the settlements around a party.
Both services keep their method names and their callers; the public behaviour is identical.

Test subclasses today: `CampServiceTests.cs:36` `public float NearestFortDistance = 100f;` and
`:94` `        protected override float DistanceToNearestFortification() => NearestFortDistance;`
(read by four tests at lines 272, 304, 313, 323); `RefugeServiceTests.cs:39-40`
`public float FortDistance = 100f;` and `public float FortDistanceFromRefuge = 100f;`, and `:102-103`:

```csharp
        protected override float DistanceToNearestFortification() => FortDistance;
        protected override float DistanceToNearestFortificationFrom(string partyId) => FortDistanceFromRefuge;
```

(read by `CanFound_TooCloseToTown_Blocks`, `CanFound_MinTownDistanceZero_DisablesProximityCheck` and
`CanUpgrade_TooCloseForAStronghold_Blocks`). All of them must keep passing with their bodies
unchanged. `CampServiceTests.cs` ends with the class's closing `}` on its last line (1257).

### Conventions that bind this change

- **ADR-007 (adapters) with the seam amendment** quoted in "The rule": services never take sealed
  TaleWorlds types; engine access sits in adapters or in seams that meet the four conditions. Every
  new seam in this plan has a signature of ids, primitives, `int?`, TAOM data classes and BCL lists
  or arrays of those, and a body that is one engine operation.
- **ADR-008 (test coverage)**: services need full coverage of their decision paths; that is the
  point of this plan (`.claude/rules/csharp-architecture.md`, "Test Coverage Requirements").
- **ADR-002 (thin entry points under 150 lines)**: no entry point (patch, model, behavior) changes.
- **`csharp-architecture.md`, "Engine-Float Decision Gates"**: "A moved, extracted or reordered gate
  is a new gate: it gets its NaN test in the same commit". The distance comparison moves, so
  `FindNearestHostile_NaNDistance_NeverWins` is mandatory. The fortification comparison moves too,
  so `NearestDistance_NaNDistance_NeverWins` and `NearestDistance_OnlyNaNDistances_ReturnsMaxValue`
  are mandatory.
- **`.claude/rules/tests.md`**: test names are `MethodName_StateUnderTest_ExpectedBehavior`; a class
  that executes engine code carries `[TestCategory("RequiresGame")]`. `WardenServiceTests` is
  untagged and runs on hosted CI against metadata-only reference assemblies where every engine method
  body throws: its new tests must touch no engine type (no `TextObject`, `Vec2`, `CampaignTime`, no
  seam body). Everything this plan adds to it uses TAOM types only. `SupplyOrderServiceTests`,
  `RefugeServiceTests` and `CampServiceTests` keep their `RequiresGame` tag; their new tests still use
  only TAOM types and BCL collections.
- **Visibility for tests**: `Main/TAOM.csproj:112-119` declares `InternalsVisibleTo("TAOM.Tests")`.
  The repo convention is an `internal` member with a comment "internal for TAOM.Tests
  (InternalsVisibleTo)" (for example `Main/Features/CastleRecruitment/Hooks/CastleRecruitmentBehavior.cs:96`).
  The nine moved methods become `internal` (non-virtual) so tests call them directly, and the shared
  rule `FortificationSearch.NearestDistance` is `internal static`.
- **Data classes beside the service**: `RaidThreat` lives in `RefugeService.cs`, `AmbushCandidate`
  in `CampService.cs`. New seam data classes go in the service file, above the service class. They
  must be `public` (a `protected` member of a public class cannot expose a less accessible type).
  This plan adds `RaidCandidate`, `RaidScan` and `RefugePrisoner` to `RefugeService.cs`,
  `PartyHeroInfo` and `PromotionSource` to `WardenService.cs`, and the `SettlementSite` struct plus
  the `FortificationSearch` static class to `CampService.cs`. `SettlementSite` is a `public readonly
  struct` with a constructor and get-only properties, like `SupplyQuote`: one is built per settlement
  on every keep-out check, so a struct avoids an allocation per settlement. The site seams return
  `IEnumerable<SettlementSite>` and yield lazily (`yield return`), so a check builds no list either
  (orchestrator change after the plan review: the keep-outs feed game-menu option conditions).
- **Seam return values that are not TAOM classes**: the volunteer-slot seam returns
  `IReadOnlyList<bool[]>` (BCL types of primitives, allowed by condition 1).
- New `.cs` files are not needed; if you add one anyway, `Main/TAOM.csproj` globs sources (it has no
  `Compile Include` item), so it needs no csproj edit.

### Engine facts (read from the decompiled v1.5.3 install, `%USERPROFILE%\.taom-src\v1.5.3\`, during planning; do not re-derive)

- `GiveGoldAction` (`TaleWorlds.CampaignSystem.Actions.GiveGoldAction.cs:42,47`):
  `public static void ApplyBetweenCharacters(Hero giverHero, Hero recipientHero, int amount, bool disableNotification = false)`
  and `public static void ApplyForCharacterToSettlement(Hero giverHero, Settlement settlement, int amount, bool disableNotification = false)`.
- `Settlement.Find` (`Settlement.cs:1163`): `public static Settlement Find(string idString) => MBObjectManager.Instance.GetObject<Settlement>(idString);`
- `MobileParty` (`TaleWorlds.CampaignSystem.Party.MobileParty.cs`): `All => Campaign.Current.MobileParties` (257);
  `Party { get; private set; }` set in the constructor, `Party = new PartyBase(this)` (362, 1887);
  `IsActive { get; set; }` (365); `GetPosition2D => Position.ToVec2()` (1096; `CampaignVec2.ToVec2()`
  returns a stored field); `MapEvent => Party.MapEvent` (1102; `PartyBase.MapEvent => _mapEventSide?.MapEvent`);
  `MemberRoster => Party.MemberRoster` (1104); `IsMainParty => this == MainParty` (1110);
  `PartyComponent => _partyComponent` (1264). All of these are safe to read on any non-null party.
- `MobileParty.MapFaction` (1112-1150) is **not** safe on every party: it dereferences
  `Party.Owner.HomeSettlement.MapFaction` for notables and `HomeSettlement.OwnerClan.MapFaction`
  without null checks. The old seam read it only for parties that passed the main-party, active,
  map-event and refuge filters. The new code must keep that: the war check is a separate seam the
  service calls only after those four filters.
- `IFaction.IsAtWarWith(IFaction other)` (`TaleWorlds.CampaignSystem.IFaction.cs:94`).
- `Vec2.Distance(Vec2 v)` (`TaleWorlds.Library.Vec2.cs:315`): `MathF.Sqrt((v.x - x) * (v.x - x) + (v.y - y) * (v.y - y))`.
- `CharacterHelper.GetRandomCompanionTemplateWithPredicate` (`Helpers.CharacterHelper.cs:650-657`):

  ```csharp
  public static CharacterObject GetRandomCompanionTemplateWithPredicate(Func<CharacterObject, bool> predicate = null)
  {
      if (predicate == null)
      {
          return MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().GetRandomElementWithPredicate((CharacterObject x) => x.IsTemplate && x.Occupation == Occupation.Wanderer);
      }
      return MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().GetRandomElementWithPredicate((CharacterObject x) => x.IsTemplate && x.Occupation == Occupation.Wanderer && predicate(x));
  }
  ```

  So the old "culture is null" call (a predicate that is always true) filters exactly like the
  no-predicate call and makes the same random draw. The new code calls the no-predicate form for a
  null culture id.
- `MBObjectManager` keeps one registry per type: every object added to the list that
  `GetObjectTypeList<T>()` returns is added to the `StringId` dictionary that `GetObject<T>(string)`
  reads, in the same statement block (`TaleWorlds.ObjectSystem.MBObjectManager.cs:193-197` and
  `236-239`). So a template drawn from the list is found again by `FindTroop(template.StringId)`, and
  two cultures never share a `StringId`, which makes comparing `c.Culture?.StringId` with an id
  equivalent to the old reference comparison `c.Culture == culture`.
- `HeroCreator.CreateSpecialHero(CharacterObject template, Settlement bornSettlement = null, Clan faction = null, Clan supporterOfClan = null, int age = -1)`
  (`HeroCreator.cs:189`) builds the hero with `new Hero(character.StringId, ...)` (`HeroCreator.cs:294`),
  whose constructor runs `base.StringId = Campaign.Current.CampaignObjectManager.FindNextUniqueStringId<Hero>(stringId)`
  then `Campaign.Current.CampaignObjectManager.AddHero(this)` (`Hero.cs:1556-1565`). `AddHero` puts it
  in the alive list (or the dead list) (`CampaignObjectManager.cs:487-511`), and
  `CampaignObjectManager.Find<T>(string id)` scans both lists (`CampaignObjectManager.cs:712-727`,
  a linear scan). So `FindHero(heroId)` finds a hero the moment `CreateSpecialHero` returns.
- `MBRandom.RandomInt(int maxValue) => Random.Next(maxValue)` (`TaleWorlds.Core.MBRandom.cs:72-75`).
- Seams 5 to 7 (read from the same decompiled install while extending the plan):
  - `Settlement` (`TaleWorlds.CampaignSystem.Settlements.Settlement.cs`): `All => Campaign.Current.Settlements`
    (485); `GetPosition2D => Position.ToVec2()` (199), where `Position` returns the stored
    `_position` field (278-283) and `CampaignVec2.ToVec2()` returns a stored field; `IsTown` and
    `IsCastle` (372-394) are `Town != null && Town.IsTown` and `Town != null && Town.IsCastle`;
    `Notables => _notablesCache` (267), an `MBReadOnlyList<Hero>`, and `MBReadOnlyList<T>` derives
    from `List<T>` (so `Count` and the indexer work). So `IsTown`, `IsCastle` and `GetPosition2D`
    are plain reads, safe on every settlement; the old loops read them only for towns and castles,
    the new seams read them for every settlement, with no other effect.
  - The live `TAOM_Map/ModuleData/settlements.xml` declares 1,002 `<Settlement ` entries (counted
    2026-09-25), so a settlement snapshot holds about a thousand entries.
  - `Hero` (`TaleWorlds.CampaignSystem.Hero.cs`): `public CharacterObject[] VolunteerTypes;` is a
    field (46); `PartyBelongedTo` returns a field (634-645); `PartyBelongedToAsPrisoner { get; private set; }`
    (647); `MapFaction` (569-592) returns the clan's kingdom or clan, else null for a special hero,
    else `HomeSettlement.MapFaction`, else `PartyBelongedTo.MapFaction`, which is the
    `MobileParty.MapFaction` noted above as unsafe on some parties. The old release loop read a
    hero's `MapFaction` only after skipping the main hero; the new code keeps that order by making
    the war check its own seam.
  - `TroopRoster` (`TaleWorlds.CampaignSystem.Roster.TroopRoster.cs`): `Count => _count` (46);
    `GetCharacterAtIndex(int index)` (567-574) returns `data[index].Character` when `index < _count`
    and throws `IndexOutOfRangeException` otherwise; `AddToCounts(CharacterObject character, int count, bool insertAtFront = false, int woundedCount = 0, int xpChange = 0, bool removeDepleted = true, int index = -1)`
    (428); `RemoveTroop(CharacterObject troop, int numberToRemove = 1, UniqueTroopDescriptor troopSeed = default, int xp = 0)`
    (667). `ItemRoster.AddToCounts(ItemObject item, int number)` (`TaleWorlds.CampaignSystem.Roster.ItemRoster.cs:185`).
  - `MBObjectManager.GetObject<T>(string objectName)` (`TaleWorlds.ObjectSystem.MBObjectManager.cs:573-597`)
    walks the type records and returns the matching object or null; it writes nothing.
  - `EndCaptivityAction.ApplyByPeace(Hero)` is not in the decompile cache and was not read. The
    plan moves the call unchanged (same argument, same place in the walk) and keeps the source's
    walk (count read once, each row re-read by index from the last to the first), so whatever the
    action does to the prison roster behaves as before. Do not decompile it to "improve" the walk.
- `AgeModel.HeroComesOfAge` is `public abstract int HeroComesOfAge { get; }` (`AgeModel.cs:13`).
- `Hero.SetName(TextObject fullName, TextObject firstName)` (1319), `Hero.ChangeState(CharacterStates newState)`
  (1846), `Hero.CompanionOf` (256), `Hero.MainHero => CharacterObject.PlayerCharacter.HeroObject` (958);
  `AddCompanionAction.Apply(Clan clan, Hero companion)`; `AddHeroToPartyAction.Apply(Hero hero, MobileParty party, bool showNotification = true)`;
  `Clan.PlayerClan => Campaign.Current.PlayerDefaultFaction` (`Clan.cs:284`);
  `TroopRoster.Count` (46), `TroopRoster.GetCharacterAtIndex(int index)` (567), `TroopRoster.TotalManCount` (61).

## Commands you will need

| Purpose | Command (prefix every one with `cd E:/repos/taom-improve/wt-026 && `; `dotnet` ones also with `TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp `) | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `Failed: 0` |
| Filtered test | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | exit 0, `Failed: 0` |
| Docs | `python tools/lint_docs.py` | exit 0, `Em/en dashes in newly written prose: **0**` |

`-p:DisableModuleCopy=true -p:ModuleId=` is required on every build and test: without it the
post-build target deploys into the game install. Never run `./build.ps1`. No ModuleData changes, so
`python tools/validate_moduledata.py` is not needed.

## Scope

**In scope** (the only files you modify):

- `Main/Features/SupplyLines/SupplyOrderService.cs`
- `Main/Features/Refuge/RefugeService.cs`
- `Main/Features/Refuge/WardenService.cs`
- `Main/Features/FieldCamp/CampService.cs`
- `TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs`
- `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs`
- `TAOM.Tests/Features/Refuge/WardenServiceTests.cs`
- `TAOM.Tests/Features/FieldCamp/CampServiceTests.cs`
- `CHANGELOG.md` (one entry at the top)

**Out of scope** (do NOT touch, even though they look related):

- Every other seam, including ones with similar shapes: `SupplyOrderService.DeliverCargoToPlayer`
  (count filter and unknown-id warnings), `PickCompanionEscortId`, `IsPlayerBlockaded`,
  `RefugeService.ResolveMilitiaTroopId` (player-culture then clan-culture fallback),
  `RefugeService.HeroPrisonersInRefuge`, `CampService.EnumerateHostileCandidates` and its helper
  `CollectHostiles` (visibility, reach and war filters), `SupplySourceService` (its own settlement
  filter at line 55), `CampService.ChargePlayer(int)`, `RefugeService.ChargePlayer(int)`. Record
  them in your report if you think they matter; do not edit.
- `ISupplyOrderService`, `IRefugeService`, `IRefugeBook`, `IWardenService`, `ICampService` and every
  caller of them: no public interface or signature changes.
- `Main/Adapters/IMapReachAdapter.cs` and `MapReachAdapter.cs`
  (`GetNormalizedDistanceToNearestFortification`, a different ArmyTargeting method).
- `docs/adrs/` (plan 021 owns the ADR text), `docs/features/` (no page names these seams).
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`: single-owner;
  no registration or project change is needed. Recommend, don't edit: if you believe one is needed,
  STOP and report the exact line you would add.
- `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs` `HeroMergeProbeService` (lines 1713-1751) and
  every existing test method body except the three named in Steps 1 and 7
  (`TryPlaceOrder_Success_ChargesOnlyAfterConsumeAndSpawn`,
  `TryPlaceOrder_UnaffordableTotal_RefundsAndDoesNotCharge`,
  `TryPlaceOrder_SpawnFails_RefundsAndDoesNotCharge`).
- Any save-format change (no `SyncData` or `[SaveableField]` edits).

## Git workflow

- **Branch**: `improve/026-seam-decision-logic` in the worktree `E:/repos/taom-improve/wt-026`, at
  `e452b0c7` (a fast-forward of the planned `31cc629f`; no code changed). The orchestrator created
  both. If the worktree does not exist, STOP and report.
- **One commit** at the end (Step 17). Subject, exactly this format, 72 characters at most:
  `refactor(seams): <version> - move decision logic out of nine seams`, where `<version>` is the
  value of `<Version value="...">` in `Main/_Module/SubModule.xml` copied as is (at `31cc629f` and
  `e452b0c7` line 6 is `<Version value="v2.0.30" />`, giving
  `refactor(seams): v2.0.30 - move decision logic out of nine seams`, 64 characters).
- Body wrapped at 72 columns, human prose, no em or en dashes, **no `Co-Authored-By` or any AI
  attribution trailer**. Add a `Not-tested:` trailer (text in Step 17).
- **Stage explicit paths only** (`cd E:/repos/taom-improve/wt-026 && git add <path> ...`). Never
  `git add -A`, `git add .` or `git commit -a`. Never stage the plan file.
- Never push, merge, rebase, stash, reset, switch or delete branches. Never pass `--no-verify`. If a
  commit hook denies the commit, read its reason; fix it if the cause is in your change; if the hook
  is confused by the worktree, STOP and report the hook text. Known cause of confusion:
  `.claude/hooks/check-commit-subject-version.sh` and `check-changelog-changed.sh` `cd` into
  `${CLAUDE_PROJECT_DIR}` (the main checkout `E:/repos/TAOM`) before reading `SubModule.xml` and
  the staged set, so a denial may describe the main tree's state rather than your worktree's.

## Steps

### Step 0: Confirm the worktree and record the baseline

1. Run the drift check at the top of this plan. Expected: empty output (otherwise follow its
   instructions).
2. `cd E:/repos/taom-improve/wt-026 && git rev-parse --short HEAD && git branch --show-current && git status --porcelain`
   Expected: `e452b0c7`, `improve/026-seam-decision-logic`, then the status lines: exactly
   `?? plans/026-seam-decision-logic.md`, plus `?? plans/_audit/2026-09-23-opus/plan-review-026.md`
   if the review file is present. Any other status line is a STOP.
   Then confirm the fast-forward changed no code:
   `cd E:/repos/taom-improve/wt-026 && git diff --name-only 31cc629f HEAD -- Main TAOM.Tests`
   Expected: no output. Any output is a STOP (the excerpts may no longer match).
3. `mkdir -p E:/repos/taom-improve/scratch/tmp E:/repos/taom-improve/scratch/plans/026`
4. Full suite baseline:
   `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tee E:/repos/taom-improve/scratch/plans/026/baseline.log | tail -5; echo "exit=$?"`
   Expected: `exit=0` and a totals line `Passed!  - Failed:     0, Passed: 10629, Skipped:     2, Total: 10631` (spacing may differ).
   Call the Passed number **P0** and the Skipped number **S0**. If Passed or Skipped differ, it is not
   a STOP: keep your own P0 and S0 and name them in your report. If anything fails (`exit=1`), STOP
   and report the failing test names: this plan needs a green start.
5. The four class baselines (one command each):
   `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SupplyOrderServiceTests" 2>&1 | tail -3; echo "exit=$?"`
   then the same with `RefugeServiceTests`, then with `WardenServiceTests`, then with `CampServiceTests`.
   Expected: `exit=0` each, with `Passed: 45`, `Passed: 112`, `Passed: 20` and `Passed: 81`,
   `Failed: 0`, `Skipped: 0`. If a count differs, STOP (the files drifted from this plan).

**Verify**: Step 2 printed the expected SHA, branch and status lines and an empty code diff, Step 4
printed `exit=0` with `Failed: 0`, Step 5 printed 45, 112, 20 and 81.

### Step 1: RED, supply orders: re-shape the test subclass and add the ChargePlayer tests

Edit `TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs`.

1. Replace the `Charges` field (lines 33-34) with:

   ```csharp
        // Every gold move a charge makes, in order: "lord:<heroId>:<amount>",
        // "settlement:<settlementId>:<amount>", "destroy:<amount>".
        public readonly List<string> Charges = new List<string>();
        public readonly HashSet<string> UnreachablePayees = new HashSet<string>();
   ```

2. Replace the `ChargePlayer` override (lines 61-65, the whole method) with:

   ```csharp
        protected override bool PayLord(string heroId, int amount)
        {
            CallSequence.Add("charge");
            Charges.Add("lord:" + heroId + ":" + amount);
            return !UnreachablePayees.Contains(heroId);
        }

        protected override bool PaySettlement(string settlementId, int amount)
        {
            CallSequence.Add("charge");
            Charges.Add("settlement:" + settlementId + ":" + amount);
            return !UnreachablePayees.Contains(settlementId);
        }

        protected override void DestroyPlayerGold(int amount)
        {
            CallSequence.Add("charge");
            Charges.Add("destroy:" + amount);
        }
   ```

3. In `TryPlaceOrder_Success_ChargesOnlyAfterConsumeAndSpawn` (line 258), replace exactly these
   lines (267-272 at `31cc629f`):

   ```csharp
        CollectionAssert.AreEqual(
            new[] { "consume", "spawn", "charge" }, _sut.CallSequence,
            "the charge must land only after the caravan exists (the source module charged first)");
        Assert.AreEqual(170, _sut.Charges.Single().Quote.Total);
        Assert.AreSame(_townSource, _sut.Charges.Single().Source,
            "the charge carries the source so its share can be credited (round B: payments were a pure sink)");
   ```

   with:

   ```csharp
        CollectionAssert.AreEqual(
            new[] { "consume", "spawn", "charge", "charge" }, _sut.CallSequence,
            "the charge must land only after the caravan exists (the source module charged first)");
        CollectionAssert.AreEqual(
            new[] { "settlement:town_G1:150", "destroy:20" }, _sut.Charges,
            "the source is credited its share (round B: payments were a pure sink) and the 20 "
            + "transport fee is destroyed: 150 + 20 is the 170 total");
   ```

   Leave the rest of that test (`Assert.AreEqual(170, result.TotalPaid);` and the active-order count)
   unchanged. Leave every other existing test unchanged.

4. Add these 10 tests just before the class's closing brace (the last line of the file, `}`):

   ```csharp

    // --- ChargePlayer (payee routing; the three gold seams only move gold) ---

    [TestMethod]
    public void ChargePlayer_SettlementSource_CreditsSettlementThenDestroysFees()
    {
        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: 100, troops: 50, transport: 20, guard: 5));

        CollectionAssert.AreEqual(new[] { "settlement:town_G1:150", "destroy:25" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_LordSource_CreditsLordThenDestroysFees()
    {
        var lord = new SupplySourceInfo { HeroId = "lord_1" };

        _sut.ChargePlayer(lord, new SupplyQuote(goods: 0, troops: 80, transport: 10, guard: 0));

        CollectionAssert.AreEqual(new[] { "lord:lord_1:80", "destroy:10" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_HeroIdAndSettlementIdBothSet_PaysTheLord()
    {
        var source = new SupplySourceInfo { HeroId = "lord_1", SettlementId = "town_G1" };

        _sut.ChargePlayer(source, new SupplyQuote(goods: 100, troops: 0, transport: 0, guard: 0));

        CollectionAssert.AreEqual(new[] { "lord:lord_1:100" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_EmptyHeroId_PaysTheSettlement()
    {
        var source = new SupplySourceInfo { HeroId = "", SettlementId = "town_G1" };

        _sut.ChargePlayer(source, new SupplyQuote(goods: 100, troops: 0, transport: 0, guard: 0));

        CollectionAssert.AreEqual(new[] { "settlement:town_G1:100" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_LordUnreachable_DestroysHisShareAndWarns()
    {
        _sut.UnreachablePayees.Add("lord_1");

        _sut.ChargePlayer(new SupplySourceInfo { HeroId = "lord_1" },
            new SupplyQuote(goods: 0, troops: 80, transport: 10, guard: 0));

        CollectionAssert.AreEqual(new[] { "lord:lord_1:80", "destroy:80", "destroy:10" }, _sut.Charges);
        _logger.Received(1).LogWarning("[SupplyLines] charge: lord 'lord_1' unreachable, his share is destroyed");
    }

    [TestMethod]
    public void ChargePlayer_SettlementUnreachable_DestroysItsShareAndWarns()
    {
        _sut.UnreachablePayees.Add("town_G1");

        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: 100, troops: 50, transport: 20, guard: 0));

        CollectionAssert.AreEqual(new[] { "settlement:town_G1:150", "destroy:150", "destroy:20" }, _sut.Charges);
        _logger.Received(1).LogWarning("[SupplyLines] charge: settlement 'town_G1' unreachable, its share is destroyed");
    }

    [TestMethod]
    public void ChargePlayer_ReachablePayee_LogsNothing()
    {
        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: 100, troops: 50, transport: 20, guard: 0));

        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
    }

    [TestMethod]
    public void ChargePlayer_NoSourceShare_OnlyDestroysFees()
    {
        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: 0, troops: 0, transport: 20, guard: 5));

        CollectionAssert.AreEqual(new[] { "destroy:25" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_NoFees_DestroysNothing()
    {
        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: 100, troops: 0, transport: 0, guard: 0));

        CollectionAssert.AreEqual(new[] { "settlement:town_G1:100" }, _sut.Charges);
    }

    [TestMethod]
    public void ChargePlayer_NonPositiveAmounts_MoveNoGold()
    {
        _sut.ChargePlayer(_townSource, new SupplyQuote(goods: -5, troops: 0, transport: -1, guard: 0));

        Assert.AreEqual(0, _sut.Charges.Count, "only a positive share or fee moves gold");
    }
   ```

5. Run the filtered test; it must fail to compile:
   `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SupplyOrderServiceTests" 2>&1 | tee E:/repos/taom-improve/scratch/plans/026/red-supply.log | tail -3; echo "exit=$?"`
6. `grep -oE "[A-Za-z]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+" E:/repos/taom-improve/scratch/plans/026/red-supply.log | sed -E 's/\([0-9]+,[0-9]+\)//' | sort -u`

**Verify**: step 5 prints `exit=1`; step 6 prints exactly these two lines:
`SupplyOrderServiceTests.cs: error CS0115` (the three overrides have nothing to override yet) and
`SupplyOrderServiceTests.cs: error CS0122` (`ChargePlayer` is still `protected`). Any other file or
error code: fix your edit to match this step exactly and re-run; if it persists, STOP.

### Step 2: GREEN, supply orders: move the routing into the service

Edit `Main/Features/SupplyLines/SupplyOrderService.cs`.

1. Replace lines 469-514 (the `ChargePlayer` doc comment and method, from `    /// <summary>` on line 469 above
   `    /// Takes the player's gold and credits the source its share` down to the method's closing
   `    }` right before the `// Heroes register with CampaignObjectManager only` comment) with the three
   seams:

   ```csharp
    /// <summary>Credits a lord his share of an order, main hero to lord. False when the lord
    /// cannot be found; then no gold moves.</summary>
    protected virtual bool PayLord(string heroId, int amount)
    {
        var lord = FindHero(heroId);
        if (lord == null)
            return false;
        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, lord, amount, disableNotification: true);
        return true;
    }

    /// <summary>Credits a settlement its share of an order. False when the settlement cannot be
    /// found; then no gold moves.</summary>
    protected virtual bool PaySettlement(string settlementId, int amount)
    {
        var settlement = Settlement.Find(settlementId);
        if (settlement == null)
            return false;
        GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, amount, disableNotification: true);
        return true;
    }

    /// <summary>Takes gold from the main hero and gives it to nobody (a null recipient).</summary>
    protected virtual void DestroyPlayerGold(int amount) =>
        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, disableNotification: true);
   ```

   Keep `FindHero` and its two-line comment exactly as they are.

2. Insert the service method directly above the line
   `    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`
   (line 455), leaving one blank line before that comment:

   ```csharp
    /// <summary>
    /// Takes the player's gold and credits the source its share (the goods and troops at their
    /// quoted prices), matching vanilla purchases where the town or lord is paid. The port
    /// previously destroyed the whole payment while real stock and soldiers left the source, a
    /// one-way economy sink the #317 town ledger would show as unexplained (review round B).
    /// Transport and guard fees ARE destroyed, deliberately: they pay the carriers, who are not
    /// economy actors, exactly like vanilla mercenary wages. An unreachable payee's share is
    /// destroyed with a warning. internal for TAOM.Tests (InternalsVisibleTo); the three gold
    /// seams below only move gold.
    /// </summary>
    internal void ChargePlayer(SupplySourceInfo source, SupplyQuote quote)
    {
        int sourceShare = quote.Goods + quote.Troops;
        int fees = quote.Transport + quote.Guard;

        if (sourceShare > 0)
        {
            if (!string.IsNullOrEmpty(source.HeroId))
            {
                if (!PayLord(source.HeroId, sourceShare))
                {
                    _logger.LogWarning($"[SupplyLines] charge: lord '{source.HeroId}' unreachable, his share is destroyed");
                    DestroyPlayerGold(sourceShare);
                }
            }
            else if (!PaySettlement(source.SettlementId, sourceShare))
            {
                _logger.LogWarning($"[SupplyLines] charge: settlement '{source.SettlementId}' unreachable, its share is destroyed");
                DestroyPlayerGold(sourceShare);
            }
        }

        if (fees > 0)
            DestroyPlayerGold(fees);
    }
   ```

   The call at line 185 (`ChargePlayer(source, quote);`) stays as it is.

3. Build and test:
   `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -3; echo "exit=$?"`
   then
   `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SupplyOrderServiceTests" 2>&1 | tail -3; echo "exit=$?"`

**Verify**: build `exit=0` with `0 Error(s)`; tests `exit=0` with `Failed: 0, Passed: 55` (45 + 10).
A failing existing test means the behaviour changed: compare your code with the "Current state"
excerpt, fix, re-run; if it fails twice, STOP.

### Step 3: RED, refuge raids: re-shape the test subclass and add the FindNearestHostile tests

Edit `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs` (keep its BOM).

1. Directly below the two fields `public RaidThreat Threat;` and `public int ThreatSearches;`
   (lines 55-56), add:

   ```csharp
        // The raid tests above the FindNearestHostile section arrange one eligible hostile through
        // Threat; the FindNearestHostile tests arrange the whole map scan through RaidCandidates.
        public List<RaidCandidate> RaidCandidates;
        public bool RaidScanUnavailable;
        public readonly HashSet<string> NotAtWar = new HashSet<string>();
        public readonly List<string> WarChecks = new List<string>();
   ```

2. Replace the `FindNearestHostile` override (lines 175-179, the whole method) with:

   ```csharp
        protected override RaidScan ScanForRaiders(string refugePartyId)
        {
            ThreatSearches++;
            if (RaidScanUnavailable)
                return null;
            var candidates = RaidCandidates;
            if (candidates == null)
            {
                candidates = new List<RaidCandidate>();
                if (Threat != null)
                {
                    candidates.Add(new RaidCandidate
                    {
                        PartyId = Threat.PartyId,
                        IsActive = true,
                        TotalManCount = 1,
                        StraightLineDistance = 0f,
                    });
                }
            }
            return new RaidScan { Candidates = candidates };
        }

        protected override bool IsAtWarWithRefuge(RaidScan scan, RaidCandidate candidate)
        {
            WarChecks.Add(candidate.PartyId);
            return !NotAtWar.Contains(candidate.PartyId);
        }

        protected override string PartyDisplayName(RaidCandidate candidate) =>
            Threat != null && candidate.PartyId == Threat.PartyId ? Threat.Name : "name:" + candidate.PartyId;
   ```

3. Add these 16 tests directly above the line `    // --- Map-event gating (manage/dismantle/enter) ---`
   (line 1224), keeping one blank line before that comment:

   ```csharp
    // --- FindNearestHostile (the raid-target pick; the scan seams only read the map) ---

    private static RaidCandidate Hostile(string id, float distance) => new RaidCandidate
    {
        PartyId = id,
        IsActive = true,
        TotalManCount = 5,
        StraightLineDistance = distance,
    };

    [TestMethod]
    public void FindNearestHostile_RefugeOrItsFactionMissing_ReturnsNull()
    {
        _sut.RaidScanUnavailable = true;

        Assert.IsNull(_sut.FindNearestHostile("r1", 6f));
    }

    [TestMethod]
    public void FindNearestHostile_NoParties_ReturnsNull()
    {
        _sut.RaidCandidates = new List<RaidCandidate>();

        Assert.IsNull(_sut.FindNearestHostile("r1", 6f));
    }

    [TestMethod]
    public void FindNearestHostile_NullEntry_Skipped()
    {
        _sut.RaidCandidates = new List<RaidCandidate> { null, Hostile("enemy", 2f) };

        Assert.AreEqual("enemy", _sut.FindNearestHostile("r1", 6f)?.PartyId);
    }

    [TestMethod]
    public void FindNearestHostile_MainParty_Skipped()
    {
        var main = Hostile("main_party", 1f);
        main.IsMainParty = true;
        _sut.RaidCandidates = new List<RaidCandidate> { main };

        Assert.IsNull(_sut.FindNearestHostile("r1", 6f));
    }

    [TestMethod]
    public void FindNearestHostile_InactiveParty_Skipped()
    {
        var idle = Hostile("idle", 1f);
        idle.IsActive = false;
        _sut.RaidCandidates = new List<RaidCandidate> { idle };

        Assert.IsNull(_sut.FindNearestHostile("r1", 6f));
    }

    [TestMethod]
    public void FindNearestHostile_PartyInMapEvent_Skipped()
    {
        var fighting = Hostile("fighting", 1f);
        fighting.InMapEvent = true;
        _sut.RaidCandidates = new List<RaidCandidate> { fighting };

        Assert.IsNull(_sut.FindNearestHostile("r1", 6f));
    }

    [TestMethod]
    public void FindNearestHostile_OtherRefuge_Skipped()
    {
        var refuge = Hostile("refuge_2", 1f);
        refuge.IsRefuge = true;
        _sut.RaidCandidates = new List<RaidCandidate> { refuge };

        Assert.IsNull(_sut.FindNearestHostile("r1", 6f));
    }

    [TestMethod]
    public void FindNearestHostile_PartyNotAtWar_Skipped()
    {
        _sut.NotAtWar.Add("neighbour");
        _sut.RaidCandidates = new List<RaidCandidate> { Hostile("neighbour", 1f) };

        Assert.IsNull(_sut.FindNearestHostile("r1", 6f));
    }

    [TestMethod]
    public void FindNearestHostile_PartyWithNoSoldiers_Skipped()
    {
        var empty = Hostile("empty", 1f);
        empty.TotalManCount = 0;
        _sut.RaidCandidates = new List<RaidCandidate> { empty };

        Assert.IsNull(_sut.FindNearestHostile("r1", 6f));
    }

    [TestMethod]
    public void FindNearestHostile_WarCheck_RunsOnlyAfterTheFourCheapFilters()
    {
        var main = Hostile("main", 1f);
        main.IsMainParty = true;
        var idle = Hostile("idle", 1f);
        idle.IsActive = false;
        var fighting = Hostile("fighting", 1f);
        fighting.InMapEvent = true;
        var refuge = Hostile("refuge_2", 1f);
        refuge.IsRefuge = true;
        var empty = Hostile("empty", 1f);
        empty.TotalManCount = 0;
        _sut.RaidCandidates = new List<RaidCandidate> { main, idle, fighting, refuge, empty, Hostile("enemy", 2f) };

        var threat = _sut.FindNearestHostile("r1", 6f);

        Assert.AreEqual("enemy", threat?.PartyId);
        CollectionAssert.AreEqual(new[] { "empty", "enemy" }, _sut.WarChecks,
            "the war check reads MobileParty.MapFaction, which can throw on odd parties; the source "
            + "read it only after the main-party, active, map-event and refuge filters, and before "
            + "the soldier count");
    }

    [TestMethod]
    public void FindNearestHostile_SeveralInRange_PicksTheNearest()
    {
        _sut.RaidCandidates = new List<RaidCandidate> { Hostile("a", 3f), Hostile("b", 1f), Hostile("c", 2f) };

        Assert.AreEqual("b", _sut.FindNearestHostile("r1", 6f)?.PartyId);
    }

    [TestMethod]
    public void FindNearestHostile_EqualDistances_FirstFoundWins()
    {
        _sut.RaidCandidates = new List<RaidCandidate> { Hostile("first", 2f), Hostile("second", 2f) };

        Assert.AreEqual("first", _sut.FindNearestHostile("r1", 6f)?.PartyId);
    }

    [TestMethod]
    public void FindNearestHostile_PartyExactlyAtRange_Excluded()
    {
        _sut.RaidCandidates = new List<RaidCandidate> { Hostile("edge", 6f) };

        Assert.IsNull(_sut.FindNearestHostile("r1", 6f), "the range comparison is strict, as in the source");
    }

    [TestMethod]
    public void FindNearestHostile_PartyJustInsideRange_Picked()
    {
        _sut.RaidCandidates = new List<RaidCandidate> { Hostile("inside", 5.9f) };

        Assert.AreEqual("inside", _sut.FindNearestHostile("r1", 6f)?.PartyId);
    }

    [TestMethod]
    public void FindNearestHostile_NaNDistance_NeverWins()
    {
        _sut.RaidCandidates = new List<RaidCandidate> { Hostile("corrupt", float.NaN), Hostile("b", 5f) };

        Assert.AreEqual("b", _sut.FindNearestHostile("r1", 6f)?.PartyId,
            "NaN < bestDistance is false, so a corrupt position can never become the raider");
    }

    [TestMethod]
    public void FindNearestHostile_Result_CarriesIdNameAndEngineHandle()
    {
        var handle = new object();
        var enemy = Hostile("enemy", 2f);
        enemy.EngineParty = handle;
        _sut.RaidCandidates = new List<RaidCandidate> { enemy };

        var threat = _sut.FindNearestHostile("r1", 6f);

        Assert.AreEqual("enemy", threat.PartyId);
        Assert.AreEqual("name:enemy", threat.Name, "the name is rendered only for the winner");
        Assert.AreSame(handle, threat.EngineParty, "StartRaid casts this handle back to the MobileParty");
    }

   ```

4. Run: `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~RefugeServiceTests" 2>&1 | tee E:/repos/taom-improve/scratch/plans/026/red-refuge.log | tail -3; echo "exit=$?"`
5. `grep -oE "[A-Za-z]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+" E:/repos/taom-improve/scratch/plans/026/red-refuge.log | sed -E 's/\([0-9]+,[0-9]+\)//' | sort -u`

**Verify**: step 4 prints `exit=1`; step 5 prints only lines that start with
`RefugeServiceTests.cs: error ` and whose codes are among `CS0115`, `CS0122` and `CS0246`
(`CS0246` must be present: `RaidCandidate` and `RaidScan` do not exist yet). Any other file or code:
fix your edit and re-run; if it persists, STOP.

### Step 4: GREEN, refuge raids: move the filters and the pick into the service

Edit `Main/Features/Refuge/RefugeService.cs` (keep its BOM).

1. Directly after the closing `}` of `RaidThreat` (line 33), add a blank line and:

   ```csharp
/// <summary>
/// One map party the raid scan is weighing, read at the campaign boundary. The raid-target
/// decision in <see cref="RefugeService"/> reads only the plain fields; <see cref="EngineParty"/>
/// rides along as an opaque handle (the <see cref="RaidThreat"/> and AmbushCandidate precedent).
/// </summary>
public sealed class RaidCandidate
{
    public string PartyId;
    public bool IsMainParty;
    public bool IsActive;
    public bool InMapEvent;

    /// <summary>True when the party is itself a refuge (its component is RefugePartyComponent).</summary>
    public bool IsRefuge;

    public int TotalManCount;

    /// <summary>Straight-line distance from the refuge; a raid trigger needs no pathfinding.</summary>
    public float StraightLineDistance;

    /// <summary>The engine <c>MobileParty</c>; null in tests, opaque to the decision logic.</summary>
    public object EngineParty;
}

/// <summary>One raid scan around a refuge: every map party as a <see cref="RaidCandidate"/>, and
/// the refuge's faction as an opaque handle for the war check.</summary>
public sealed class RaidScan
{
    /// <summary>The refuge's engine <c>IFaction</c>; null in tests, opaque to the decision logic.</summary>
    public object RefugeFaction;

    public IReadOnlyList<RaidCandidate> Candidates;
}
   ```

2. Replace lines 1166-1204 (the `FindNearestHostile` doc comment and method, from
   `    /// <summary>Nearest active hostile party with at least one soldier inside range, or null.`
   to the method's closing `    }` just above `    protected virtual void StartRaid`) with the three
   seams:

   ```csharp
    /// <summary>Snapshots every map party for the raid-target pick around a refuge. Null when the
    /// refuge or its faction is missing. Reads only members that are safe on any party; the
    /// faction read waits for <see cref="IsAtWarWithRefuge"/>, which the service calls only
    /// for parties that passed the cheap filters.</summary>
    protected virtual RaidScan ScanForRaiders(string refugePartyId)
    {
        var refuge = FindParty(refugePartyId);
        var refugeFaction = refuge?.MapFaction;
        if (refuge == null || refugeFaction == null)
            return null;

        var position = refuge.GetPosition2D;
        var candidates = new List<RaidCandidate>();
        foreach (var party in MobileParty.All)
        {
            if (party == null)
                continue;
            candidates.Add(new RaidCandidate
            {
                PartyId = party.StringId,
                IsMainParty = party.IsMainParty,
                IsActive = party.IsActive,
                InMapEvent = party.MapEvent != null,
                IsRefuge = party.PartyComponent is RefugePartyComponent,
                TotalManCount = party.MemberRoster?.TotalManCount ?? 0,
                StraightLineDistance = position.Distance(party.GetPosition2D),
                EngineParty = party,
            });
        }
        return new RaidScan { RefugeFaction = refugeFaction, Candidates = candidates };
    }

    /// <summary>True when the candidate's map faction is at war with the refuge's; false when
    /// either faction is missing.</summary>
    protected virtual bool IsAtWarWithRefuge(RaidScan scan, RaidCandidate candidate)
    {
        if (!(scan?.RefugeFaction is IFaction refugeFaction) || !(candidate?.EngineParty is MobileParty party))
            return false;
        var faction = party.MapFaction;
        return faction != null && faction.IsAtWarWith(refugeFaction);
    }

    /// <summary>The party's display name; rendered only for the chosen raider, never per scanned party.</summary>
    protected virtual string PartyDisplayName(RaidCandidate candidate) =>
        (candidate?.EngineParty as MobileParty)?.Name?.ToString();
   ```

3. Insert the service method directly above the line
   `    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`
   (line 787 at `31cc629f`; it follows `SaneBuildHours()`), leaving one blank line before that comment:

   ```csharp
    /// <summary>Nearest active hostile party with at least one soldier inside range, or null.
    /// Straight-line distance, like the source; a raid trigger does not need pathfinding. The
    /// filters run in the source's order, and the war check (which reads the engine's
    /// MapFaction) runs only for parties that passed the first four. Strict less-than from
    /// <paramref name="range"/>: the first party found wins a tie, a party exactly at range is
    /// out, and a NaN distance never wins. internal for TAOM.Tests (InternalsVisibleTo).</summary>
    internal RaidThreat FindNearestHostile(string refugePartyId, float range)
    {
        var scan = ScanForRaiders(refugePartyId);
        if (scan?.Candidates == null)
            return null;

        RaidCandidate best = null;
        float bestDistance = range;
        foreach (var candidate in scan.Candidates)
        {
            if (candidate == null || candidate.IsMainParty || !candidate.IsActive || candidate.InMapEvent)
                continue;
            if (candidate.IsRefuge)
                continue;
            if (!IsAtWarWithRefuge(scan, candidate))
                continue;
            if (candidate.TotalManCount < 1)
                continue;
            if (candidate.StraightLineDistance < bestDistance)
            {
                bestDistance = candidate.StraightLineDistance;
                best = candidate;
            }
        }
        if (best == null)
            return null;
        return new RaidThreat
        {
            PartyId = best.PartyId,
            Name = PartyDisplayName(best),
            EngineParty = best.EngineParty,
        };
    }
   ```

   The call in `HourlyTick` (line 414) stays as it is. No new `using` is needed (`IFaction` is in
   `TaleWorlds.CampaignSystem`, already imported at line 10).

4. Build and test (same two commands as Step 2, with `--filter "FullyQualifiedName~RefugeServiceTests"`).

**Verify**: build `exit=0`, `0 Error(s)`; tests `exit=0`, `Failed: 0, Passed: 128` (112 + 16). The 9
existing raid tests must pass with their bodies unchanged.

### Step 5: RED, wardens: re-shape the test subclass and add the companion and mint tests

Edit `TAOM.Tests/Features/Refuge/WardenServiceTests.cs`.

1. Replace the `CompanionsInMainParty` override (line 36) with:

   ```csharp
        // Existing tests list eligible companions in Companions; the CompanionsInMainParty tests
        // arrange the raw roster heroes in PartyHeroes.
        public List<PartyHeroInfo> PartyHeroes;

        protected override IReadOnlyList<PartyHeroInfo> HeroesInMainParty() =>
            PartyHeroes ?? Companions.ConvertAll(c => c == null ? null : new PartyHeroInfo
            {
                HeroId = c.Id,
                DisplayName = c.DisplayName,
                IsPlayerClanCompanion = true,
            });
   ```

2. Replace the `MintCompanionFromTroop` override (lines 45-49, the whole method) with the mint seam
   overrides below. Keep the existing fields `MintResult` and `MintCalls` (lines 25-26) as they are;
   they keep their meaning (`MintCalls` counts mint attempts, `MintResult` is what the engine
   creation returns):

   ```csharp
        public PromotionSource Source = new PromotionSource
        {
            TroopCultureId = "culture_troop",
            PlayerCultureId = "culture_player",
        };
        public string CultureTemplate = "template_culture"; // returned for any non-null culture id
        public string AnyTemplate = "template_any";         // returned for a null culture id
        public int? ComesOfAge = 18;
        public int RandomIntResult = 7;
        public bool RenameThrows;
        public readonly List<string> MintSteps = new List<string>();

        protected override PromotionSource ReadPromotionSource(string troopId)
        {
            MintCalls++;
            MintSteps.Add("source:" + troopId);
            return Source;
        }

        protected override string RandomCompanionTemplateId(string cultureId)
        {
            MintSteps.Add("template:" + (cultureId ?? "<any>"));
            return cultureId == null ? AnyTemplate : CultureTemplate;
        }

        protected override int? HeroComesOfAge()
        {
            MintSteps.Add("comesOfAge");
            return ComesOfAge;
        }

        protected override int NextRandomInt(int maxExclusive)
        {
            MintSteps.Add("rng:" + maxExclusive);
            return RandomIntResult;
        }

        protected override string CreatePromotedHero(string templateId, int age)
        {
            MintSteps.Add("create:" + templateId + ":" + age);
            return MintResult;
        }

        protected override void RenamePromotedHero(string heroId, string troopId)
        {
            MintSteps.Add("rename:" + heroId + ":" + troopId);
            if (RenameThrows)
                throw new System.InvalidOperationException("text manager missing");
        }

        protected override void EnrolPromotedHero(string heroId) => MintSteps.Add("enrol:" + heroId);
   ```

3. Replace `    private TestableWardenService _sut;` (line 74) with:

   ```csharp
    private TestableWardenService _sut;
    private IModLogger _logger;
   ```

   and in `Setup()` replace `        _sut = new TestableWardenService(Substitute.For<IModLogger>());`
   (line 79) with:

   ```csharp
        _logger = Substitute.For<IModLogger>();
        _sut = new TestableWardenService(_logger);
   ```

4. Add these 16 tests just before the class's closing brace (the last line of the file, `}`):

   ```csharp

    // --- CompanionsInMainParty (the eligibility filter; the roster seam only reads) ---

    private static PartyHeroInfo PartyHero(string id, bool companion = true, bool mainHero = false) =>
        new PartyHeroInfo
        {
            HeroId = id,
            DisplayName = "Name of " + id,
            IsPlayerClanCompanion = companion,
            IsMainHero = mainHero,
        };

    [TestMethod]
    public void CompanionsInMainParty_MainHero_Excluded()
    {
        _sut.PartyHeroes = new List<PartyHeroInfo> { PartyHero("main_hero", companion: true, mainHero: true) };

        Assert.AreEqual(0, _sut.CompanionsInMainParty().Count);
    }

    [TestMethod]
    public void CompanionsInMainParty_HeroNotAPlayerClanCompanion_Excluded()
    {
        _sut.PartyHeroes = new List<PartyHeroInfo> { PartyHero("visiting_lord", companion: false) };

        Assert.AreEqual(0, _sut.CompanionsInMainParty().Count,
            "a visiting noble or quest hero must never be strandable in a refuge");
    }

    [TestMethod]
    public void CompanionsInMainParty_NullEntry_Skipped()
    {
        _sut.PartyHeroes = new List<PartyHeroInfo> { null, PartyHero("companion_1") };

        var companions = _sut.CompanionsInMainParty();

        Assert.AreEqual(1, companions.Count);
        Assert.AreEqual("companion_1", companions[0].Id);
    }

    [TestMethod]
    public void CompanionsInMainParty_MapsEachCompanionInRosterOrder()
    {
        _sut.PartyHeroes = new List<PartyHeroInfo> { PartyHero("companion_2"), PartyHero("companion_1") };

        var companions = _sut.CompanionsInMainParty();

        Assert.AreEqual(2, companions.Count);
        Assert.AreEqual("companion_2", companions[0].Id);
        Assert.AreEqual("Name of companion_2", companions[0].DisplayName);
        Assert.IsTrue(companions[0].IsCompanion);
        Assert.AreEqual(0, companions[0].Tier);
        Assert.AreEqual("companion_1", companions[1].Id);
    }

    [TestMethod]
    public void Candidates_ListsOnlyEligibleCompanionsFromTheRoster()
    {
        _sut.SlotFree = false;
        _sut.PartyHeroes = new List<PartyHeroInfo>
        {
            PartyHero("main_hero", mainHero: true),
            PartyHero("visiting_lord", companion: false),
            PartyHero("companion_1"),
        };

        var candidates = _sut.Candidates();

        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual("companion_1", candidates[0].Id);
    }

    // --- MintCompanionFromTroop (culture, template, age and rename policy; the seams do one engine step each) ---

    [TestMethod]
    public void MintCompanionFromTroop_Default_RunsTheEngineStepsInSourceOrder()
    {
        var heroId = _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual("hero_minted", heroId);
        CollectionAssert.AreEqual(
            new[]
            {
                "source:troop_1",
                "template:culture_troop",
                "comesOfAge",
                "rng:14",
                "create:template_culture:29",
                "rename:hero_minted:troop_1",
                "enrol:hero_minted",
            },
            _sut.MintSteps,
            "the template draw and the age draw use the campaign RNG in the source's order");
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
    }

    [TestMethod]
    public void MintCompanionFromTroop_NoPromotionSource_MintsNothing()
    {
        _sut.Source = null;

        Assert.IsNull(_sut.MintCompanionFromTroop("troop_1"));
        CollectionAssert.AreEqual(new[] { "source:troop_1" }, _sut.MintSteps);
    }

    [TestMethod]
    public void MintCompanionFromTroop_TroopIsAHero_MintsNothing()
    {
        _sut.Source.TroopIsHero = true;

        Assert.IsNull(_sut.MintCompanionFromTroop("troop_1"));
        CollectionAssert.AreEqual(new[] { "source:troop_1" }, _sut.MintSteps);
    }

    [TestMethod]
    public void MintCompanionFromTroop_TroopWithoutCulture_UsesThePlayerCulture()
    {
        _sut.Source.TroopCultureId = null;

        _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual("template:culture_player", _sut.MintSteps[1]);
        Assert.AreEqual("create:template_culture:29", _sut.MintSteps[4]);
    }

    [TestMethod]
    public void MintCompanionFromTroop_NoCultureAnywhere_AsksForAnyTemplateOnce()
    {
        _sut.Source.TroopCultureId = null;
        _sut.Source.PlayerCultureId = null;

        _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual("template:<any>", _sut.MintSteps[1]);
        Assert.AreEqual("comesOfAge", _sut.MintSteps[2]);
        Assert.AreEqual("create:template_any:29", _sut.MintSteps[4]);
    }

    [TestMethod]
    public void MintCompanionFromTroop_NoTemplateForTheCulture_FallsBackToAnyTemplate()
    {
        _sut.CultureTemplate = null;

        _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual("template:culture_troop", _sut.MintSteps[1]);
        Assert.AreEqual("template:<any>", _sut.MintSteps[2]);
        Assert.AreEqual("create:template_any:29", _sut.MintSteps[5]);
    }

    [TestMethod]
    public void MintCompanionFromTroop_NoTemplateAtAll_StopsBeforeTheRandomDraw()
    {
        _sut.CultureTemplate = null;
        _sut.AnyTemplate = null;

        Assert.IsNull(_sut.MintCompanionFromTroop("troop_1"));
        CollectionAssert.AreEqual(
            new[] { "source:troop_1", "template:culture_troop", "template:<any>" }, _sut.MintSteps);
    }

    [TestMethod]
    public void MintCompanionFromTroop_Age_IsComesOfAgePlusFourPlusOneDrawBelowFourteen()
    {
        _sut.ComesOfAge = 16;
        _sut.RandomIntResult = 13;

        _sut.MintCompanionFromTroop("troop_1");

        CollectionAssert.Contains(_sut.MintSteps, "create:template_culture:33");
        Assert.AreEqual(1, _sut.MintSteps.FindAll(s => s.StartsWith("rng:", System.StringComparison.Ordinal)).Count,
            "exactly one RandomInt draw per mint");
        CollectionAssert.Contains(_sut.MintSteps, "rng:14");
    }

    [TestMethod]
    public void MintCompanionFromTroop_NoAgeModel_ComesOfAgeDefaultsTo18()
    {
        _sut.ComesOfAge = null;
        _sut.RandomIntResult = 0;

        _sut.MintCompanionFromTroop("troop_1");

        CollectionAssert.Contains(_sut.MintSteps, "create:template_culture:22");
    }

    [TestMethod]
    public void MintCompanionFromTroop_CreationRefused_NoRenameNoEnrol()
    {
        _sut.MintResult = null;

        Assert.IsNull(_sut.MintCompanionFromTroop("troop_1"));
        Assert.AreEqual(5, _sut.MintSteps.Count);
        Assert.AreEqual("create:template_culture:29", _sut.MintSteps[4]);
    }

    [TestMethod]
    public void MintCompanionFromTroop_RenameThrows_WarnsAndStillEnrols()
    {
        _sut.RenameThrows = true;

        var heroId = _sut.MintCompanionFromTroop("troop_1");

        Assert.AreEqual("hero_minted", heroId, "the rename is cosmetic; the promotion goes on");
        Assert.AreEqual("enrol:hero_minted", _sut.MintSteps[_sut.MintSteps.Count - 1]);
        _logger.Received(1).LogWarning("[Refuge] promoted-warden rename failed: text manager missing");
    }
   ```

5. Run: `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~WardenServiceTests" 2>&1 | tee E:/repos/taom-improve/scratch/plans/026/red-warden.log | tail -3; echo "exit=$?"`
6. `grep -oE "[A-Za-z]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+" E:/repos/taom-improve/scratch/plans/026/red-warden.log | sed -E 's/\([0-9]+,[0-9]+\)//' | sort -u`

**Verify**: step 5 prints `exit=1`; step 6 prints only lines that start with
`WardenServiceTests.cs: error ` and whose codes are among `CS0115`, `CS0122` and `CS0246`
(`CS0246` must be present: `PartyHeroInfo` and `PromotionSource` do not exist yet). Any other file or
code: fix your edit and re-run; if it persists, STOP.

### Step 6: GREEN, wardens: move the filter and the promotion policy into the service

Edit `Main/Features/Refuge/WardenService.cs`.

1. Directly above the class's doc comment (the `/// <summary>` line before
   `/// Warden lifecycle (port of the Refuge module's SoldierPromotion`, line 14), insert:

   ```csharp
/// <summary>One hero in the main party's member roster, read at the campaign boundary. Pure data:
/// the warden eligibility filter in <see cref="WardenService"/> reads only these fields.</summary>
public sealed class PartyHeroInfo
{
    public string HeroId;
    public string DisplayName;

    /// <summary>True for <c>Hero.MainHero</c>.</summary>
    public bool IsMainHero;

    /// <summary>True when the hero's <c>CompanionOf</c> is the player clan; false when there is
    /// no player clan.</summary>
    public bool IsPlayerClanCompanion;
}

/// <summary>What a promotion needs to know about the troop and the player, read at the campaign
/// boundary. The seam returns null instead when the troop, the player clan or the main party is
/// missing.</summary>
public sealed class PromotionSource
{
    public bool TroopIsHero;

    /// <summary>The troop's culture StringId, or null.</summary>
    public string TroopCultureId;

    /// <summary>The main hero's culture StringId, or null.</summary>
    public string PlayerCultureId;
}

   ```

2. Below `    private const int PromotedAgeBaseOffsetYears = 4;` (line 36) add:

   ```csharp

    /// <summary>Coming-of-age when the campaign has no AgeModel (source value).</summary>
    private const int DefaultComesOfAgeYears = 18;
   ```

3. Insert the two service methods directly above the line
   `    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`
   (line 126 at `31cc629f`; it follows `UnwindPromotion`), leaving one blank line before that comment:

   ```csharp
    /// <summary>The player clan's companions riding in the main party, in roster order: every
    /// hero except the main hero whose CompanionOf is the player clan. A visiting noble or quest
    /// hero is never a candidate. internal for TAOM.Tests (InternalsVisibleTo).</summary>
    internal IReadOnlyList<WardenCandidate> CompanionsInMainParty()
    {
        var result = new List<WardenCandidate>();
        foreach (var hero in HeroesInMainParty())
        {
            if (hero == null || hero.IsMainHero)
                continue;
            if (!hero.IsPlayerClanCompanion)
                continue;
            result.Add(new WardenCandidate
            {
                Id = hero.HeroId,
                DisplayName = hero.DisplayName,
                IsCompanion = true,
            });
        }
        return result;
    }

    /// <summary>
    /// Mints a companion hero from a troop: culture-matched companion template,
    /// HeroCreator.CreateSpecialHero into the player clan, renamed to the troop so "a Rohan
    /// Spearman became Captain-of-sorts" reads on screen, activated, AddCompanionAction, and
    /// placed in the main party so the founding flow can then move him into the refuge.
    /// Returns the hero StringId, or null when any engine step refuses. The step order is the
    /// source's (template draw, then the age draw), so the campaign RNG is consumed as before.
    /// internal for TAOM.Tests (InternalsVisibleTo).
    /// </summary>
    internal string MintCompanionFromTroop(string troopId)
    {
        var source = ReadPromotionSource(troopId);
        if (source == null || source.TroopIsHero)
            return null;

        // Culture-matched template first; any companion template when the culture has none.
        string cultureId = source.TroopCultureId ?? source.PlayerCultureId;
        string templateId = RandomCompanionTemplateId(cultureId) ?? RandomCompanionTemplateId(null);
        if (templateId == null)
            return null;

        int age = (HeroComesOfAge() ?? DefaultComesOfAgeYears)
            + PromotedAgeBaseOffsetYears
            + NextRandomInt(PromotedAgeSpreadYears);
        string heroId = CreatePromotedHero(templateId, age);
        if (heroId == null)
            return null;

        try
        {
            // The rename is cosmetic; a template-named hero is still a working warden, so a
            // localization hiccup here must not abort the promotion (source behaviour).
            RenamePromotedHero(heroId, troopId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Refuge] promoted-warden rename failed: {ex.Message}");
        }
        EnrolPromotedHero(heroId);
        return heroId;
    }

   ```

4. Replace the old `CompanionsInMainParty` seam (lines 128-151, the whole method) with:

   ```csharp
    /// <summary>Every hero in the main party's member roster, in roster order, with the two facts
    /// the companion filter reads. Empty when there is no main party roster.</summary>
    protected virtual IReadOnlyList<PartyHeroInfo> HeroesInMainParty()
    {
        var result = new List<PartyHeroInfo>();
        var roster = MobileParty.MainParty?.MemberRoster;
        var clan = Clan.PlayerClan;
        if (roster == null)
            return result;
        for (int i = 0; i < roster.Count; i++)
        {
            var hero = roster.GetCharacterAtIndex(i)?.HeroObject;
            if (hero == null)
                continue;
            result.Add(new PartyHeroInfo
            {
                HeroId = hero.StringId,
                DisplayName = hero.Name?.ToString(),
                IsMainHero = hero == Hero.MainHero,
                IsPlayerClanCompanion = clan != null && hero.CompanionOf == clan,
            });
        }
        return result;
    }
   ```

5. Replace the old `MintCompanionFromTroop` seam (its doc comment and method, lines 193-235, from
   `    /// <summary>` above `    /// Mints a companion hero from a troop` to the closing `    }` after
   `        return hero.StringId;`) with the seven mint seams:

   ```csharp
    /// <summary>Reads the promotion inputs: the troop's hero flag and culture, and the player's
    /// culture. Null when the troop, the player clan or the main party is missing.</summary>
    protected virtual PromotionSource ReadPromotionSource(string troopId)
    {
        var troop = FindTroop(troopId);
        if (troop == null || Clan.PlayerClan == null || MobileParty.MainParty == null)
            return null;
        return new PromotionSource
        {
            TroopIsHero = troop.IsHero,
            TroopCultureId = troop.Culture?.StringId,
            PlayerCultureId = Hero.MainHero?.Culture?.StringId,
        };
    }

    /// <summary>One random wanderer companion template: of the given culture, or of any culture
    /// when <paramref name="cultureId"/> is null. Its StringId, or null when none matches. Draws
    /// from the campaign RNG.</summary>
    protected virtual string RandomCompanionTemplateId(string cultureId)
    {
        var template = cultureId == null
            ? CharacterHelper.GetRandomCompanionTemplateWithPredicate()
            : CharacterHelper.GetRandomCompanionTemplateWithPredicate(
                c => string.Equals(c.Culture?.StringId, cultureId, StringComparison.Ordinal));
        return template?.StringId;
    }

    /// <summary>The campaign AgeModel's coming-of-age, or null when there is no model.</summary>
    protected virtual int? HeroComesOfAge() => Campaign.Current?.Models?.AgeModel?.HeroComesOfAge;

    /// <summary>One campaign RNG draw in [0, <paramref name="maxExclusive"/>).</summary>
    protected virtual int NextRandomInt(int maxExclusive) => MBRandom.RandomInt(maxExclusive);

    /// <summary>Creates a special hero from the template into the player clan at the given age.
    /// The hero's StringId, or null when the template or the clan is missing or the engine
    /// refuses.</summary>
    protected virtual string CreatePromotedHero(string templateId, int age)
    {
        var template = FindTroop(templateId);
        var clan = Clan.PlayerClan;
        if (template == null || clan == null)
            return null;
        var hero = HeroCreator.CreateSpecialHero(template, bornSettlement: null, faction: clan, supporterOfClan: null, age: age);
        return hero?.StringId;
    }

    /// <summary>Renames the hero after the troop he was. May throw; the caller tolerates it.</summary>
    protected virtual void RenamePromotedHero(string heroId, string troopId)
    {
        var hero = FindHero(heroId);
        var troop = FindTroop(troopId);
        if (hero == null || troop == null)
            return;
        hero.SetName(troop.Name, troop.Name);
    }

    /// <summary>Activates the hero, makes him a player-clan companion and puts him in the main
    /// party (the source's three engine calls, in order).</summary>
    protected virtual void EnrolPromotedHero(string heroId)
    {
        var hero = FindHero(heroId);
        var clan = Clan.PlayerClan;
        var mainParty = MobileParty.MainParty;
        if (hero == null || clan == null || mainParty == null)
            return;
        hero.ChangeState(Hero.CharacterStates.Active);
        AddCompanionAction.Apply(clan, hero);
        AddHeroToPartyAction.Apply(hero, mainParty, showNotification: false);
    }
   ```

   Callers at line 48 (`Candidates()`) and line 85 (`ResolveWarden`) stay as they are. No new `using`
   is needed (`System`, `Helpers`, `TaleWorlds.CampaignSystem`, `TaleWorlds.CampaignSystem.Actions`,
   `TaleWorlds.CampaignSystem.Party`, `TaleWorlds.Core` and `TaleWorlds.ObjectSystem` are already
   imported at lines 1-10).

6. Build and test (same two commands as Step 2, with `--filter "FullyQualifiedName~WardenServiceTests"`).

**Verify**: build `exit=0`, `0 Error(s)`; tests `exit=0`, `Failed: 0, Passed: 36` (20 + 16). The 20
existing tests must pass with their bodies unchanged.

### Step 7: RED, supply refunds: re-shape the test subclass and add the RefundConsumption tests

Edit `TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs` again (Step 1 already changed it).

1. Replace the `Refunds` field (line 38 at `31cc629f`, unique in the file):

   ```csharp
        public readonly List<SupplyConsumption> Refunds = new List<SupplyConsumption>();
   ```

   with:

   ```csharp
        // Every refund step, in order: "source:<id>" (the reachability check),
        // "lord:<heroId>:<troopId>:<count>", "goods:<settlementId>:<itemId>:<count>",
        // "volunteer:<settlementId>:<notable>:<slot>:<troopId>".
        public readonly List<string> Refunds = new List<string>();
        public readonly HashSet<string> UnreachableSources = new HashSet<string>();

        // Per notable, true for an empty volunteer slot; a null entry is a notable without a slot
        // array, a null list a settlement without notables. A fill writes back here, like the engine.
        public List<bool[]> VolunteerSlots = new List<bool[]> { new[] { true, true, true } };
        public int VolunteerSlotReads;
        public bool GoodsRefundThrows;
   ```

2. Replace the `RefundConsumption` override (lines 86-90 at `31cc629f`, the whole method):

   ```csharp
        protected override void RefundConsumption(SupplySourceInfo source, SupplyConsumption consumption)
        {
            CallSequence.Add("refund");
            Refunds.Add(consumption);
        }
   ```

   with the six refund seam overrides:

   ```csharp
        protected override bool CanReturnTroopsToLord(string heroId)
        {
            Refunds.Add("source:" + heroId);
            return !UnreachableSources.Contains(heroId);
        }

        protected override void ReturnTroopsToLord(string heroId, string troopId, int count) =>
            Refunds.Add("lord:" + heroId + ":" + troopId + ":" + count);

        protected override bool CanReturnStockToSettlement(string settlementId)
        {
            Refunds.Add("source:" + settlementId);
            return !UnreachableSources.Contains(settlementId);
        }

        protected override void ReturnGoodsToSettlement(string settlementId, string itemId, int count)
        {
            if (GoodsRefundThrows)
                throw new System.InvalidOperationException("roster locked");
            Refunds.Add("goods:" + settlementId + ":" + itemId + ":" + count);
        }

        protected override IReadOnlyList<bool[]> ReadEmptyVolunteerSlots(string settlementId)
        {
            VolunteerSlotReads++;
            // A fresh copy per read, like the engine snapshot: the service sees its own earlier
            // fills only by reading again.
            return VolunteerSlots?.ConvertAll(slots => slots == null ? null : (bool[])slots.Clone());
        }

        protected override void FillVolunteerSlot(string settlementId, int notableIndex, int slotIndex, string troopId)
        {
            VolunteerSlots[notableIndex][slotIndex] = false;
            Refunds.Add("volunteer:" + settlementId + ":" + notableIndex + ":" + slotIndex + ":" + troopId);
        }
   ```

3. In `TryPlaceOrder_UnaffordableTotal_RefundsAndDoesNotCharge` and
   `TryPlaceOrder_SpawnFails_RefundsAndDoesNotCharge`, replace the identical line (lines 237 and 252
   at `31cc629f`; use the Edit tool with `replace_all: true`, and confirm it replaced exactly 2
   occurrences):

   ```csharp
        Assert.AreSame(consumption, _sut.Refunds.Single());
   ```

   with:

   ```csharp
        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "goods:town_G1:grain:5", "volunteer:town_G1:0:0:troop_a", "volunteer:town_G1:0:1:troop_a" },
            _sut.Refunds, "the consumed grain and recruits go back to the source");
   ```

   Leave the rest of both tests unchanged (their `var consumption = ArrangeConsumption();` line stays;
   an unused local assigned from a call is not a compiler warning). The third reader,
   `TryPlaceOrder_NothingObtained_FailsWithoutRefundOrCharge` (`Assert.AreEqual(0, _sut.Refunds.Count, ...)`),
   stays unchanged: any refund attempt would now record a `"source:"` entry, so it still proves no
   refund ran.

4. Add these 13 tests just before the class's closing brace (the last line of the file, `}`, below
   the tests Step 1 added):

   ```csharp

    // --- RefundConsumption (route, count filter and the volunteer-slot walk; the six refund seams each read or write once) ---

    private static SupplyConsumption Consumed(
        Dictionary<string, int> goods = null, Dictionary<string, int> troops = null) =>
        new SupplyConsumption
        {
            Goods = goods ?? new Dictionary<string, int>(),
            Troops = troops ?? new Dictionary<string, int>(),
        };

    [TestMethod]
    public void RefundConsumption_SettlementSource_ReturnsGoodsThenVolunteers()
    {
        _sut.RefundConsumption(_townSource, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 5, ["wine"] = 2 },
            troops: new Dictionary<string, int> { ["troop_a"] = 1 }));

        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "goods:town_G1:grain:5", "goods:town_G1:wine:2", "volunteer:town_G1:0:0:troop_a" },
            _sut.Refunds);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
        _logger.DidNotReceiveWithAnyArgs().LogError(default);
    }

    [TestMethod]
    public void RefundConsumption_EmptyHeroId_UsesTheSettlement()
    {
        var source = new SupplySourceInfo { HeroId = "", SettlementId = "town_G1" };

        _sut.RefundConsumption(source, Consumed(goods: new Dictionary<string, int> { ["grain"] = 1 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1", "goods:town_G1:grain:1" }, _sut.Refunds);
    }

    [TestMethod]
    public void RefundConsumption_LordSource_ReturnsOnlyTroopsToHisParty()
    {
        var source = new SupplySourceInfo { HeroId = "lord_1", SettlementId = "town_G1" };

        _sut.RefundConsumption(source, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 5 },
            troops: new Dictionary<string, int> { ["troop_a"] = 3, ["troop_b"] = 1 }));

        CollectionAssert.AreEqual(
            new[] { "source:lord_1", "lord:lord_1:troop_a:3", "lord:lord_1:troop_b:1" }, _sut.Refunds,
            "a lord source takes back troops only; its settlement id and any goods are ignored");
        Assert.AreEqual(0, _sut.VolunteerSlotReads);
    }

    [TestMethod]
    public void RefundConsumption_LordUnreachable_WarnsAndReturnsNothing()
    {
        _sut.UnreachableSources.Add("lord_1");

        _sut.RefundConsumption(new SupplySourceInfo { HeroId = "lord_1" },
            Consumed(troops: new Dictionary<string, int> { ["troop_a"] = 3 }));

        CollectionAssert.AreEqual(new[] { "source:lord_1" }, _sut.Refunds);
        _logger.Received(1).LogWarning("[SupplyLines] refund: lord 'lord_1' unreachable, troops not restored");
    }

    [TestMethod]
    public void RefundConsumption_SettlementUnreachable_WarnsAndReturnsNothing()
    {
        _sut.UnreachableSources.Add("town_G1");

        _sut.RefundConsumption(_townSource, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 5 },
            troops: new Dictionary<string, int> { ["troop_a"] = 1 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1" }, _sut.Refunds);
        Assert.AreEqual(0, _sut.VolunteerSlotReads);
        _logger.Received(1).LogWarning("[SupplyLines] refund: settlement 'town_G1' unreachable, stock not restored");
    }

    [TestMethod]
    public void RefundConsumption_SettlementNonPositiveCounts_Skipped()
    {
        _sut.RefundConsumption(_townSource, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 0, ["wine"] = -1 },
            troops: new Dictionary<string, int> { ["troop_a"] = 0, ["troop_b"] = -2 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1" }, _sut.Refunds);
        Assert.AreEqual(0, _sut.VolunteerSlotReads, "a non-positive recruit count never walks the slots");
    }

    [TestMethod]
    public void RefundConsumption_LordNonPositiveCounts_Skipped()
    {
        _sut.RefundConsumption(new SupplySourceInfo { HeroId = "lord_1" }, Consumed(
            troops: new Dictionary<string, int> { ["troop_a"] = 0, ["troop_b"] = -1, ["troop_c"] = 2 }));

        CollectionAssert.AreEqual(new[] { "source:lord_1", "lord:lord_1:troop_c:2" }, _sut.Refunds);
    }

    [TestMethod]
    public void RefundConsumption_Volunteers_FillTheFirstEmptySlotsInNotableOrder()
    {
        _sut.VolunteerSlots = new List<bool[]> { new[] { false, true, false, true }, new[] { true, true } };

        _sut.RefundConsumption(_townSource, Consumed(troops: new Dictionary<string, int> { ["troop_a"] = 3 }));

        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "volunteer:town_G1:0:1:troop_a", "volunteer:town_G1:0:3:troop_a", "volunteer:town_G1:1:0:troop_a" },
            _sut.Refunds);
    }

    [TestMethod]
    public void RefundConsumption_NotableWithoutSlots_Skipped()
    {
        _sut.VolunteerSlots = new List<bool[]> { null, new[] { true } };

        _sut.RefundConsumption(_townSource, Consumed(troops: new Dictionary<string, int> { ["troop_a"] = 1 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1", "volunteer:town_G1:1:0:troop_a" }, _sut.Refunds);
    }

    [TestMethod]
    public void RefundConsumption_MoreRecruitsThanFreeSlots_ExtraDroppedSilently()
    {
        _sut.VolunteerSlots = new List<bool[]> { new[] { true, false }, new[] { true } };

        _sut.RefundConsumption(_townSource, Consumed(troops: new Dictionary<string, int> { ["troop_a"] = 5 }));

        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "volunteer:town_G1:0:0:troop_a", "volunteer:town_G1:1:0:troop_a" },
            _sut.Refunds, "the source drops recruits beyond the free slots without a word");
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
        _logger.DidNotReceiveWithAnyArgs().LogError(default);
    }

    [TestMethod]
    public void RefundConsumption_SecondTroopType_ReadsTheSlotsAgain()
    {
        _sut.VolunteerSlots = new List<bool[]> { new[] { true, true, true } };

        _sut.RefundConsumption(_townSource, Consumed(
            troops: new Dictionary<string, int> { ["troop_a"] = 2, ["troop_b"] = 2 }));

        CollectionAssert.AreEqual(
            new[] { "source:town_G1", "volunteer:town_G1:0:0:troop_a", "volunteer:town_G1:0:1:troop_a", "volunteer:town_G1:0:2:troop_b" },
            _sut.Refunds, "troop_b never lands on a slot troop_a just filled");
        Assert.AreEqual(2, _sut.VolunteerSlotReads);
    }

    [TestMethod]
    public void RefundConsumption_NoNotables_ReturnsGoodsOnly()
    {
        _sut.VolunteerSlots = null;

        _sut.RefundConsumption(_townSource, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 1 },
            troops: new Dictionary<string, int> { ["troop_a"] = 2 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1", "goods:town_G1:grain:1" }, _sut.Refunds);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default);
    }

    [TestMethod]
    public void RefundConsumption_SeamThrows_LogsErrorAndStops()
    {
        _sut.GoodsRefundThrows = true;

        _sut.RefundConsumption(_townSource, Consumed(
            goods: new Dictionary<string, int> { ["grain"] = 1 },
            troops: new Dictionary<string, int> { ["troop_a"] = 1 }));

        CollectionAssert.AreEqual(new[] { "source:town_G1" }, _sut.Refunds, "nothing after the throw runs");
        Assert.AreEqual(0, _sut.VolunteerSlotReads);
        _logger.Received(1).LogError("[SupplyLines] refund after failed order placement threw: roster locked");
    }
   ```

5. Run the filtered test; it must fail to compile:
   `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SupplyOrderServiceTests" 2>&1 | tee E:/repos/taom-improve/scratch/plans/026/red-refund.log | tail -3; echo "exit=$?"`
6. `grep -oE "[A-Za-z]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+" E:/repos/taom-improve/scratch/plans/026/red-refund.log | sed -E 's/\([0-9]+,[0-9]+\)//' | sort -u`

**Verify**: step 5 prints `exit=1`; step 6 prints exactly these two lines:
`SupplyOrderServiceTests.cs: error CS0115` (the six overrides have nothing to override yet) and
`SupplyOrderServiceTests.cs: error CS0122` (`RefundConsumption` is still `protected`). Any other file
or error code: fix your edit to match this step exactly and re-run; if it persists, STOP.

### Step 8: GREEN, supply refunds: move the route, the filters and the slot walk into the service

Edit `Main/Features/SupplyLines/SupplyOrderService.cs` again (Step 2 already changed it).

1. Replace the `RefundConsumption` seam and its helper (lines 574-647 at `31cc629f`: from the
   `    /// <summary>` line above `    /// Puts a consumption back where it came from after a failed placement`
   down to the closing `    }` of `private static void RestoreVolunteerSlots(Settlement settlement, CharacterObject troop, int count)`,
   the line just above the blank line before `    protected virtual void ShowMessage(TextObject text, bool error)`)
   with the six refund seams:

   ```csharp
    /// <summary>True when the lord can be found and rides in a party with a member roster, the
    /// place a refund returns his troops to.</summary>
    protected virtual bool CanReturnTroopsToLord(string heroId) =>
        FindHero(heroId)?.PartyBelongedTo?.MemberRoster != null;

    /// <summary>Adds troops back to the lord's party roster. No-op when the lord, his roster or
    /// the troop id cannot be found.</summary>
    protected virtual void ReturnTroopsToLord(string heroId, string troopId, int count)
    {
        var roster = FindHero(heroId)?.PartyBelongedTo?.MemberRoster;
        var troop = MBObjectManager.Instance.GetObject<CharacterObject>(troopId);
        if (roster == null || troop == null)
            return;
        roster.AddToCounts(troop, count);
    }

    /// <summary>True when the settlement can be found.</summary>
    protected virtual bool CanReturnStockToSettlement(string settlementId) =>
        Settlement.Find(settlementId) != null;

    /// <summary>Adds goods back to the settlement's item roster. No-op when the settlement, its
    /// roster or the item id cannot be found.</summary>
    protected virtual void ReturnGoodsToSettlement(string settlementId, string itemId, int count)
    {
        var settlement = Settlement.Find(settlementId);
        var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
        if (settlement == null || item == null)
            return;
        settlement.ItemRoster?.AddToCounts(item, count);
    }

    /// <summary>The settlement's volunteer slots, one entry per notable in notable order, true for
    /// an empty slot. An entry is null for a null notable or one without a slot array; the list is
    /// null when the settlement or its notables list is missing. A fresh snapshot per call.</summary>
    protected virtual IReadOnlyList<bool[]> ReadEmptyVolunteerSlots(string settlementId)
    {
        var notables = Settlement.Find(settlementId)?.Notables;
        if (notables == null)
            return null;
        var result = new List<bool[]>(notables.Count);
        foreach (var notable in notables)
        {
            var slots = notable?.VolunteerTypes;
            if (slots == null)
            {
                result.Add(null);
                continue;
            }
            var empty = new bool[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                empty[i] = slots[i] == null;
            result.Add(empty);
        }
        return result;
    }

    /// <summary>Puts the troop into one volunteer slot of the settlement's notable at
    /// <paramref name="notableIndex"/>. No-op when the settlement, the notable, the slot or the
    /// troop id cannot be found.</summary>
    protected virtual void FillVolunteerSlot(string settlementId, int notableIndex, int slotIndex, string troopId)
    {
        var notables = Settlement.Find(settlementId)?.Notables;
        var troop = MBObjectManager.Instance.GetObject<CharacterObject>(troopId);
        if (notables == null || troop == null || notableIndex < 0 || notableIndex >= notables.Count)
            return;
        var slots = notables[notableIndex]?.VolunteerTypes;
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
            return;
        slots[slotIndex] = troop;
    }
   ```

2. Insert the service method and its private helper directly above the line
   `    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`
   (below the `ChargePlayer` method Step 2 put there), leaving one blank line before that comment:

   ```csharp
    /// <summary>
    /// Puts a consumption back where it came from after a failed placement: lord troops to the
    /// lord's party roster; otherwise goods to the settlement roster and volunteers into empty
    /// notable slots. Best effort; a partial refund is logged rather than thrown because the
    /// player has not been charged. Only positive counts move. Recruits beyond the free volunteer
    /// slots are dropped silently (source behaviour). internal for TAOM.Tests
    /// (InternalsVisibleTo); the six refund seams below each do one engine read or write.
    /// </summary>
    internal void RefundConsumption(SupplySourceInfo source, SupplyConsumption consumption)
    {
        try
        {
            if (!string.IsNullOrEmpty(source.HeroId))
            {
                if (!CanReturnTroopsToLord(source.HeroId))
                {
                    _logger.LogWarning($"[SupplyLines] refund: lord '{source.HeroId}' unreachable, troops not restored");
                    return;
                }
                foreach (var pair in consumption.Troops)
                {
                    if (pair.Value > 0)
                        ReturnTroopsToLord(source.HeroId, pair.Key, pair.Value);
                }
                return;
            }

            if (!CanReturnStockToSettlement(source.SettlementId))
            {
                _logger.LogWarning($"[SupplyLines] refund: settlement '{source.SettlementId}' unreachable, stock not restored");
                return;
            }
            foreach (var pair in consumption.Goods)
            {
                if (pair.Value > 0)
                    ReturnGoodsToSettlement(source.SettlementId, pair.Key, pair.Value);
            }
            foreach (var pair in consumption.Troops)
            {
                if (pair.Value <= 0)
                    continue;
                RestoreVolunteerSlots(source.SettlementId, pair.Key, pair.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[SupplyLines] refund after failed order placement threw: {ex.Message}");
        }
    }

    /// <summary>Fills the first empty volunteer slots, notable by notable, until
    /// <paramref name="count"/> recruits are placed; recruits beyond the free slots are dropped
    /// (source behaviour). Reads the slots afresh for every troop type, so one type never lands
    /// on a slot another type just filled.</summary>
    private void RestoreVolunteerSlots(string settlementId, string troopId, int count)
    {
        var notables = ReadEmptyVolunteerSlots(settlementId);
        if (notables == null)
            return;
        int remaining = count;
        for (int n = 0; n < notables.Count && remaining > 0; n++)
        {
            var empty = notables[n];
            if (empty == null)
                continue;
            for (int i = 0; i < empty.Length && remaining > 0; i++)
            {
                if (empty[i])
                {
                    FillVolunteerSlot(settlementId, n, i, troopId);
                    remaining--;
                }
            }
        }
    }
   ```

   The two calls in `TryPlaceOrder` (lines 151 and 175, `RefundConsumption(source, consumption);`)
   stay as they are. No new `using` is needed (`System`, `System.Collections.Generic`,
   `TaleWorlds.CampaignSystem`, `TaleWorlds.CampaignSystem.Settlements`, `TaleWorlds.Core` and
   `TaleWorlds.ObjectSystem` are already imported at lines 1-15).

3. Build and test (same two commands as Step 2, with `--filter "FullyQualifiedName~SupplyOrderServiceTests"`).

**Verify**: build `exit=0`, `0 Error(s)`; tests `exit=0`, `Failed: 0, Passed: 68` (55 + 13). A failing
existing test means the behaviour changed: compare your code with the "Seam 5" excerpt, fix, re-run;
if it fails twice, STOP.

### Step 9: RED, refuge prisoners: re-shape the test subclass and add the ReleasePeacePrisoners tests

Edit `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs` again (keep its BOM).

1. Replace these four lines (187-190 at `31cc629f`):

   ```csharp
        public readonly List<string> PeaceReleasedParties = new List<string>();

        protected override void ReleasePeacePrisoners(string partyId) =>
            PeaceReleasedParties.Add(partyId);
   ```

   with:

   ```csharp
        public readonly List<string> PeaceReleasedParties = new List<string>();

        // Prison rosters for the ReleasePeacePrisoners tests, by refuge party id; a null row holds
        // no hero. A release or a removal takes the row out, like the engine, so a forward walk
        // would skip rows or run past the end.
        public readonly Dictionary<string, List<RefugePrisoner>> PrisonRows =
            new Dictionary<string, List<RefugePrisoner>>();
        public readonly HashSet<string> PrisonersAtWar = new HashSet<string>();
        public readonly List<string> PrisonerEvents = new List<string>();

        protected override int RefugePrisonRosterCount(string partyId)
        {
            PeaceReleasedParties.Add(partyId);
            return PrisonRows.TryGetValue(partyId, out var rows) ? rows.Count : 0;
        }

        protected override RefugePrisoner ReadRefugePrisonerAt(string partyId, int index)
        {
            PrisonerEvents.Add("read:" + index);
            return PrisonRows[partyId][index];
        }

        protected override bool IsPrisonerAtWarWithRefuge(string partyId, RefugePrisoner prisoner)
        {
            PrisonerEvents.Add("war:" + prisoner.HeroId);
            return PrisonersAtWar.Contains(prisoner.HeroId);
        }

        protected override void ReleasePrisonerByPeace(RefugePrisoner prisoner)
        {
            PrisonerEvents.Add("release:" + prisoner.HeroId);
            foreach (var rows in PrisonRows.Values)
                rows.Remove(prisoner);
        }

        protected override void RemoveFromRefugePrisonRoster(string partyId, RefugePrisoner prisoner)
        {
            PrisonerEvents.Add("remove:" + prisoner.HeroId);
            PrisonRows[partyId].Remove(prisoner);
        }
   ```

   `OnPeaceMade_ReleasesForEveryRefuge` keeps passing unchanged: the row-count seam records each
   party id in `PeaceReleasedParties`.

2. Add these 8 tests directly above the line `    // --- ResetForNewSession (the singleton-leak fix) ---`
   (line 1350 at `31cc629f`), keeping one blank line before that comment:

   ```csharp
    // --- ReleasePeacePrisoners (the release decisions; each prison seam reads or acts on one row) ---

    private static RefugePrisoner Prisoner(string heroId, bool heldByRefuge = true, bool mainHero = false) =>
        new RefugePrisoner { HeroId = heroId, HeldByRefuge = heldByRefuge, IsMainHero = mainHero };

    [TestMethod]
    public void ReleasePeacePrisoners_NoRosterRows_ReadsNothing()
    {
        _sut.ReleasePeacePrisoners("r1");

        CollectionAssert.AreEqual(new[] { "r1" }, _sut.PeaceReleasedParties);
        Assert.AreEqual(0, _sut.PrisonerEvents.Count,
            "a missing refuge or faction reports no rows, and the release does nothing (source behaviour)");
    }

    [TestMethod]
    public void ReleasePeacePrisoners_HeldByTheRefuge_EndsCaptivityByPeace()
    {
        _sut.PrisonRows["r1"] = new List<RefugePrisoner> { Prisoner("lord_a") };

        _sut.ReleasePeacePrisoners("r1");

        CollectionAssert.AreEqual(new[] { "read:0", "war:lord_a", "release:lord_a" }, _sut.PrisonerEvents);
    }

    [TestMethod]
    public void ReleasePeacePrisoners_HeldByAnotherCaptor_OnlyDropsTheRow()
    {
        _sut.PrisonRows["r1"] = new List<RefugePrisoner> { Prisoner("lord_a", heldByRefuge: false) };

        _sut.ReleasePeacePrisoners("r1");

        CollectionAssert.AreEqual(new[] { "read:0", "war:lord_a", "remove:lord_a" }, _sut.PrisonerEvents);
    }

    [TestMethod]
    public void ReleasePeacePrisoners_StillAtWar_Kept()
    {
        _sut.PrisonRows["r1"] = new List<RefugePrisoner> { Prisoner("lord_a") };
        _sut.PrisonersAtWar.Add("lord_a");

        _sut.ReleasePeacePrisoners("r1");

        CollectionAssert.AreEqual(new[] { "read:0", "war:lord_a" }, _sut.PrisonerEvents);
        Assert.AreEqual(1, _sut.PrisonRows["r1"].Count);
    }

    [TestMethod]
    public void ReleasePeacePrisoners_MainHero_NeverReleasedAndNeverWarChecked()
    {
        _sut.PrisonRows["r1"] = new List<RefugePrisoner> { Prisoner("main_hero", mainHero: true) };

        _sut.ReleasePeacePrisoners("r1");

        CollectionAssert.AreEqual(new[] { "read:0" }, _sut.PrisonerEvents,
            "the source skipped the main hero before it read any faction");
    }

    [TestMethod]
    public void ReleasePeacePrisoners_RowWithoutAHero_Skipped()
    {
        _sut.PrisonRows["r1"] = new List<RefugePrisoner> { null };

        _sut.ReleasePeacePrisoners("r1");

        CollectionAssert.AreEqual(new[] { "read:0" }, _sut.PrisonerEvents);
    }

    [TestMethod]
    public void ReleasePeacePrisoners_WalksFromTheLastRowToTheFirst()
    {
        _sut.PrisonRows["r1"] = new List<RefugePrisoner> { Prisoner("a"), Prisoner("b"), Prisoner("c") };

        _sut.ReleasePeacePrisoners("r1");

        CollectionAssert.AreEqual(
            new[] { "read:2", "war:c", "release:c", "read:1", "war:b", "release:b", "read:0", "war:a", "release:a" },
            _sut.PrisonerEvents,
            "a release removes its row; walking backwards keeps every unread row at its index");
        Assert.AreEqual(0, _sut.PrisonRows["r1"].Count);
    }

    [TestMethod]
    public void ReleasePeacePrisoners_MixedRoster_EachRowGetsItsOwnVerdict()
    {
        _sut.PrisonRows["r1"] = new List<RefugePrisoner>
        {
            Prisoner("a"), null, Prisoner("main_hero", mainHero: true), Prisoner("b"), Prisoner("c", heldByRefuge: false),
        };
        _sut.PrisonersAtWar.Add("b");

        _sut.ReleasePeacePrisoners("r1");

        CollectionAssert.AreEqual(
            new[] { "read:4", "war:c", "remove:c", "read:3", "war:b", "read:2", "read:1", "read:0", "war:a", "release:a" },
            _sut.PrisonerEvents);
        Assert.AreEqual(3, _sut.PrisonRows["r1"].Count, "the empty row, the main hero and the enemy stay");
    }

   ```

3. Run: `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~RefugeServiceTests" 2>&1 | tee E:/repos/taom-improve/scratch/plans/026/red-prisoners.log | tail -3; echo "exit=$?"`
4. `grep -oE "[A-Za-z]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+" E:/repos/taom-improve/scratch/plans/026/red-prisoners.log | sed -E 's/\([0-9]+,[0-9]+\)//' | sort -u`

**Verify**: step 3 prints `exit=1`; step 4 prints only lines that start with
`RefugeServiceTests.cs: error ` and whose codes are among `CS0115`, `CS0122` and `CS0246`
(`CS0246` must be present: `RefugePrisoner` does not exist yet). Any other file or code: fix your
edit and re-run; if it persists, STOP.

### Step 10: GREEN, refuge prisoners: move the walk and the release decisions into the service

Edit `Main/Features/Refuge/RefugeService.cs` again (keep its BOM).

1. Directly after the closing `}` of `RaidScan` (the class Step 4 added), add a blank line and:

   ```csharp
/// <summary>
/// One hero row of a refuge's prison roster, read at the campaign boundary for the peace
/// release. The release decision in <see cref="RefugeService"/> reads only the plain fields;
/// <see cref="EngineHero"/> rides along as an opaque handle (the <see cref="RaidThreat"/>
/// precedent) so the release seams act on this exact hero without a second lookup.
/// </summary>
public sealed class RefugePrisoner
{
    public string HeroId;

    /// <summary>True for <c>Hero.MainHero</c>.</summary>
    public bool IsMainHero;

    /// <summary>True when the hero's <c>PartyBelongedToAsPrisoner</c> is the refuge's own party.</summary>
    public bool HeldByRefuge;

    /// <summary>The engine <c>Hero</c>; null in tests, opaque to the decision logic.</summary>
    public object EngineHero;
}
   ```

2. Replace the `ReleasePeacePrisoners` seam (lines 1019-1042 at `31cc629f`: from
   `    /// <summary>Releases the refuge's hero prisoners who are no longer at war with the refuge's`
   to the method's closing `    }` just above the blank line before
   `    protected virtual void DestroyRefugeParty(string partyId)`) with the five prison seams:

   ```csharp
    /// <summary>The number of rows in the refuge's prison roster; 0 when the refuge or its faction
    /// is missing, so the release then does nothing, as before.</summary>
    protected virtual int RefugePrisonRosterCount(string partyId)
    {
        var refuge = FindParty(partyId);
        if (refuge == null || refuge.MapFaction == null)
            return 0;
        return refuge.PrisonRoster.Count;
    }

    /// <summary>The hero on row <paramref name="index"/> of the refuge's prison roster; null for a
    /// row without a hero or when the refuge is gone. Past the roster's end it throws, exactly as
    /// <c>TroopRoster.GetCharacterAtIndex</c> does.</summary>
    protected virtual RefugePrisoner ReadRefugePrisonerAt(string partyId, int index)
    {
        var refuge = FindParty(partyId);
        var hero = refuge?.PrisonRoster.GetCharacterAtIndex(index)?.HeroObject;
        if (hero == null)
            return null;
        return new RefugePrisoner
        {
            HeroId = hero.StringId,
            IsMainHero = hero == Hero.MainHero,
            HeldByRefuge = hero.PartyBelongedToAsPrisoner == refuge.Party,
            EngineHero = hero,
        };
    }

    /// <summary>True when the prisoner's map faction is at war with the refuge's; false when
    /// either faction is missing. <c>Hero.MapFaction</c> can reach the unsafe
    /// <c>MobileParty.MapFaction</c>, so the service calls this only after the main-hero filter,
    /// as the source did.</summary>
    protected virtual bool IsPrisonerAtWarWithRefuge(string partyId, RefugePrisoner prisoner)
    {
        var refugeFaction = FindParty(partyId)?.MapFaction;
        if (refugeFaction == null || !(prisoner?.EngineHero is Hero hero))
            return false;
        return hero.MapFaction != null && hero.MapFaction.IsAtWarWith(refugeFaction);
    }

    /// <summary>Ends the hero's captivity by peace (the engine action frees him from the refuge).</summary>
    protected virtual void ReleasePrisonerByPeace(RefugePrisoner prisoner)
    {
        if (prisoner?.EngineHero is Hero hero)
            EndCaptivityAction.ApplyByPeace(hero);
    }

    /// <summary>Drops the hero's row from the refuge's prison roster: a stale row whose recorded
    /// captor is another party.</summary>
    protected virtual void RemoveFromRefugePrisonRoster(string partyId, RefugePrisoner prisoner)
    {
        var roster = FindParty(partyId)?.PrisonRoster;
        if (roster != null && prisoner?.EngineHero is Hero hero)
            roster.RemoveTroop(hero.CharacterObject);
    }
   ```

3. Insert the service method directly above the line
   `    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`
   (below the `FindNearestHostile` method Step 4 put there), leaving one blank line before that
   comment:

   ```csharp
    /// <summary>Releases the refuge's hero prisoners who are no longer at war with the refuge's
    /// faction, mirroring vanilla PrisonerReleaseCampaignBehavior.ReleasePartyPrisoners (which
    /// only enumerates caravans, war parties, villages and garrisons - never a custom
    /// component). Called after a peace involving the player's faction. The source's walk: the
    /// row count is read once, then each row is read afresh from the last to the first, because
    /// a release takes its row out. The main hero is never released and never faction-checked;
    /// a prisoner the refuge itself holds is freed by the engine's peace action, and a row whose
    /// captor is another party is only dropped. internal for TAOM.Tests (InternalsVisibleTo).</summary>
    internal void ReleasePeacePrisoners(string partyId)
    {
        for (int i = RefugePrisonRosterCount(partyId) - 1; i >= 0; i--)
        {
            var prisoner = ReadRefugePrisonerAt(partyId, i);
            if (prisoner == null || prisoner.IsMainHero)
                continue;
            if (IsPrisonerAtWarWithRefuge(partyId, prisoner))
                continue;
            if (prisoner.HeldByRefuge)
                ReleasePrisonerByPeace(prisoner);
            else
                RemoveFromRefugePrisonRoster(partyId, prisoner);
        }
    }
   ```

   The call in `OnPeaceMade` (line 519) stays as it is. No new `using` is needed
   (`TaleWorlds.CampaignSystem` and `TaleWorlds.CampaignSystem.Actions` are imported at lines 10-11).

4. Build and test (same two commands as Step 2, with `--filter "FullyQualifiedName~RefugeServiceTests"`).

**Verify**: build `exit=0`, `0 Error(s)`; tests `exit=0`, `Failed: 0, Passed: 136` (128 + 8).
`OnPeaceMade_ReleasesForEveryRefuge` must pass with its body unchanged.

### Step 11: RED, camp keep-out: re-shape the camp test subclass and add the fortification tests

Edit `TAOM.Tests/Features/FieldCamp/CampServiceTests.cs` (keep its BOM).

1. Replace this line (94 at `31cc629f`, unique in the file):

   ```csharp
        protected override float DistanceToNearestFortification() => NearestFortDistance;
   ```

   with (the block starts and ends with a blank line, to set it off from the one-line overrides
   around it):

   ```csharp

        // Existing tests set NearestFortDistance (one town at that distance); the fortification
        // tests arrange every settlement through SettlementSites.
        public List<SettlementSite> SettlementSites;
        public int SiteReads;

        protected override IEnumerable<SettlementSite> SettlementSitesFromMainParty()
        {
            SiteReads++;
            return SettlementSites
                ?? new List<SettlementSite> { new SettlementSite(isTown: true, isCastle: false, distance: NearestFortDistance) };
        }

   ```

2. Add these 10 tests just before the class's closing brace (the last line of the file, `}`):

   ```csharp

    // --- The fortification search (FortificationSearch.NearestDistance, shared with RefugeService) ---

    private static SettlementSite TownSite(float distance) =>
        new SettlementSite(isTown: true, isCastle: false, distance: distance);

    private static SettlementSite CastleSite(float distance) =>
        new SettlementSite(isTown: false, isCastle: true, distance: distance);

    private static SettlementSite VillageSite(float distance) =>
        new SettlementSite(isTown: false, isCastle: false, distance: distance);

    [TestMethod]
    public void NearestDistance_NullSites_ReturnsMaxValue()
    {
        Assert.AreEqual(float.MaxValue, FortificationSearch.NearestDistance(null));
    }

    [TestMethod]
    public void NearestDistance_NoSites_ReturnsMaxValue()
    {
        Assert.AreEqual(float.MaxValue, FortificationSearch.NearestDistance(new List<SettlementSite>()));
    }

    [TestMethod]
    public void NearestDistance_OnlyVillages_ReturnsMaxValue()
    {
        Assert.AreEqual(float.MaxValue,
            FortificationSearch.NearestDistance(new List<SettlementSite> { VillageSite(1f), VillageSite(2f) }),
            "a settlement that is neither a town nor a castle is no fortification");
    }

    [TestMethod]
    public void NearestDistance_Town_Counts()
    {
        Assert.AreEqual(7f, FortificationSearch.NearestDistance(new List<SettlementSite> { TownSite(7f) }));
    }

    [TestMethod]
    public void NearestDistance_Castle_Counts()
    {
        Assert.AreEqual(9f, FortificationSearch.NearestDistance(new List<SettlementSite> { CastleSite(9f) }));
    }

    [TestMethod]
    public void NearestDistance_SeveralSettlements_PicksTheNearestFortification()
    {
        var sites = new List<SettlementSite> { TownSite(5f), VillageSite(1f), CastleSite(3f), TownSite(4f) };

        Assert.AreEqual(3f, FortificationSearch.NearestDistance(sites));
    }

    [TestMethod]
    public void NearestDistance_NaNDistance_NeverWins()
    {
        var sites = new List<SettlementSite> { TownSite(float.NaN), CastleSite(7f) };

        Assert.AreEqual(7f, FortificationSearch.NearestDistance(sites),
            "NaN < nearest is false, so a corrupt position never becomes the nearest fortification");
    }

    [TestMethod]
    public void NearestDistance_OnlyNaNDistances_ReturnsMaxValue()
    {
        Assert.AreEqual(float.MaxValue,
            FortificationSearch.NearestDistance(new List<SettlementSite> { TownSite(float.NaN) }),
            "the keep-out gates compare with <, so MaxValue never blocks");
    }

    [TestMethod]
    public void DistanceToNearestFortification_ReadsTheMainPartySitesOnce()
    {
        _sut.SettlementSites = new List<SettlementSite> { VillageSite(1f), TownSite(4f) };

        Assert.AreEqual(4f, _sut.DistanceToNearestFortification());
        Assert.AreEqual(1, _sut.SiteReads);
    }

    [TestMethod]
    public void CanEstablish_OnlyAVillageInsideMinDistance_DoesNotBlock()
    {
        _sut.SettlementSites = new List<SettlementSite> { VillageSite(1f), TownSite(50f) };

        Assert.AreEqual(CampBlockReason.None, _sut.CanEstablish(CampType.Field));
    }
   ```

3. Run: `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CampServiceTests" 2>&1 | tee E:/repos/taom-improve/scratch/plans/026/red-camp.log | tail -3; echo "exit=$?"`
4. `grep -oE "[A-Za-z]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+" E:/repos/taom-improve/scratch/plans/026/red-camp.log | sed -E 's/\([0-9]+,[0-9]+\)//' | sort -u`

**Verify**: step 3 prints `exit=1`; step 4 prints only lines that start with
`CampServiceTests.cs: error ` and whose codes are among `CS0103`, `CS0115`, `CS0122` and `CS0246`
(`CS0246` must be present: `SettlementSite` does not exist yet; `CS0103` is `FortificationSearch`).
Any other file or code: fix your edit and re-run; if it persists, STOP.

### Step 12: GREEN, camp keep-out: add the shared rule and split the camp seam

Edit `Main/Features/FieldCamp/CampService.cs` (keep its BOM).

1. Directly after the closing `}` of `AmbushCandidate` (line 37 at `31cc629f`), add a blank line and:

   ```csharp
/// <summary>
/// One settlement as the fortification search sees it, read at the campaign boundary: its kind
/// and its straight-line distance from the searching party. Pure data, so
/// <see cref="FortificationSearch"/> runs in unit tests.
/// </summary>
public readonly struct SettlementSite
{
    public SettlementSite(bool isTown, bool isCastle, float distance)
    {
        IsTown = isTown;
        IsCastle = isCastle;
        Distance = distance;
    }

    public bool IsTown { get; }
    public bool IsCastle { get; }

    /// <summary>Straight-line distance from the searching party.</summary>
    public float Distance { get; }
}

/// <summary>
/// The nearest-fortification rule shared by <see cref="CampService"/> (the camp keep-out) and
/// RefugeService (the refuge and stronghold keep-outs): only towns and castles count, and the
/// nearest wins. Pure, so both services' tests run it; each service's seam only lists the
/// settlements around a party.
/// </summary>
internal static class FortificationSearch
{
    /// <summary>Straight-line distance to the nearest town or castle among
    /// <paramref name="sites"/>; float.MaxValue when there is none or <paramref name="sites"/> is
    /// null. Strict less-than from float.MaxValue, as in the source loops, so a NaN distance
    /// never wins.</summary>
    internal static float NearestDistance(IEnumerable<SettlementSite> sites)
    {
        float nearest = float.MaxValue;
        if (sites == null)
            return nearest;
        foreach (var site in sites)
        {
            if (!site.IsTown && !site.IsCastle)
                continue;
            if (site.Distance < nearest)
                nearest = site.Distance;
        }
        return nearest;
    }
}
   ```

2. Replace the `DistanceToNearestFortification` seam (lines 745-761 at `31cc629f`: from
   `    protected virtual float DistanceToNearestFortification()` to its closing `    }` just above
   the blank line before `    protected virtual int PlayerGold => Hero.MainHero?.Gold ?? 0;`) with:

   ```csharp
    /// <summary>Every settlement with its kind and its straight-line distance from the main
    /// party, in the campaign's settlement-list order; empty when there is no main party.
    /// Yielded lazily: the keep-out check runs as a menu-option condition, so it builds no list
    /// of the campaign's settlements per call.</summary>
    protected virtual IEnumerable<SettlementSite> SettlementSitesFromMainParty()
    {
        var party = MobileParty.MainParty;
        if (party == null)
            yield break;
        var position = party.GetPosition2D;
        foreach (var settlement in Settlement.All)
        {
            if (settlement == null)
                continue;
            yield return new SettlementSite(settlement.IsTown, settlement.IsCastle, position.Distance(settlement.GetPosition2D));
        }
    }
   ```

3. Insert the service method directly above the line
   `    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`
   (line 659 at `31cc629f`), leaving one blank line before that comment:

   ```csharp
    /// <summary>Straight-line distance from the main party to the nearest town or castle;
    /// float.MaxValue when there is none or no main party. The rule is
    /// <see cref="FortificationSearch.NearestDistance"/>, shared with RefugeService.
    /// internal for TAOM.Tests (InternalsVisibleTo).</summary>
    internal float DistanceToNearestFortification() =>
        FortificationSearch.NearestDistance(SettlementSitesFromMainParty());

   ```

   The call in `CanEstablish` (line 192) stays as it is. No new `using` is needed.

4. Build and test (same two commands as Step 2, with `--filter "FullyQualifiedName~CampServiceTests"`).

**Verify**: build `exit=0`, `0 Error(s)`; tests `exit=0`, `Failed: 0, Passed: 91` (81 + 10). The
four existing tests that set `NearestFortDistance` must pass with their bodies unchanged.

### Step 13: RED, refuge keep-outs: re-shape the refuge test subclass and add its fortification tests

Edit `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs` again (keep its BOM). Its `using
TAOM.Features.FieldCamp;` (line 6) already makes `SettlementSite` visible.

1. Replace these two lines (102-103 at `31cc629f`):

   ```csharp
        protected override float DistanceToNearestFortification() => FortDistance;
        protected override float DistanceToNearestFortificationFrom(string partyId) => FortDistanceFromRefuge;
   ```

   with:

   ```csharp

        // Existing tests set FortDistance and FortDistanceFromRefuge (one town at that distance);
        // the fortification tests arrange every settlement through the two site lists.
        public List<SettlementSite> SitesFromMain;
        public List<SettlementSite> SitesFromParty;
        public readonly List<string> SiteReads = new List<string>();

        protected override IEnumerable<SettlementSite> SettlementSitesFromMainParty()
        {
            SiteReads.Add("main");
            return SitesFromMain
                ?? new List<SettlementSite> { new SettlementSite(isTown: true, isCastle: false, distance: FortDistance) };
        }

        protected override IEnumerable<SettlementSite> SettlementSitesFrom(string partyId)
        {
            SiteReads.Add("party:" + partyId);
            return SitesFromParty
                ?? new List<SettlementSite> { new SettlementSite(isTown: true, isCastle: false, distance: FortDistanceFromRefuge) };
        }
   ```

   (The replacement starts with a blank line; the blank line that already follows line 103 stays,
   so the block is set off on both sides.)

2. Add these 3 tests directly above the line `    // --- ResetForNewSession (the singleton-leak fix) ---`
   (so below the tests Step 9 added), keeping one blank line before that comment:

   ```csharp
    // --- The keep-out distances (FortificationSearch.NearestDistance, shared with CampService) ---

    private static SettlementSite TownSite(float distance) =>
        new SettlementSite(isTown: true, isCastle: false, distance: distance);

    private static SettlementSite VillageSite(float distance) =>
        new SettlementSite(isTown: false, isCastle: false, distance: distance);

    [TestMethod]
    public void DistanceToNearestFortification_MeasuresFromTheMainParty()
    {
        _sut.SitesFromMain = new List<SettlementSite> { VillageSite(1f), TownSite(12f) };

        Assert.AreEqual(12f, _sut.DistanceToNearestFortification());
        CollectionAssert.AreEqual(new[] { "main" }, _sut.SiteReads);
    }

    [TestMethod]
    public void DistanceToNearestFortificationFrom_MeasuresFromThatParty()
    {
        _sut.SitesFromParty = new List<SettlementSite> { TownSite(30f), VillageSite(2f) };

        Assert.AreEqual(30f, _sut.DistanceToNearestFortificationFrom("r1"));
        CollectionAssert.AreEqual(new[] { "party:r1" }, _sut.SiteReads);
    }

    [TestMethod]
    public void CanFound_OnlyAVillageInsideMinTownDistance_DoesNotBlock()
    {
        _sut.SitesFromMain = new List<SettlementSite> { VillageSite(1f) };

        Assert.AreEqual(RefugeBlockReason.None, _sut.CanFound());
    }

   ```

3. Run: `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~RefugeServiceTests" 2>&1 | tee E:/repos/taom-improve/scratch/plans/026/red-refuge-forts.log | tail -3; echo "exit=$?"`
4. `grep -oE "[A-Za-z]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+" E:/repos/taom-improve/scratch/plans/026/red-refuge-forts.log | sed -E 's/\([0-9]+,[0-9]+\)//' | sort -u`

**Verify**: step 3 prints `exit=1`; step 4 prints exactly these two lines:
`RefugeServiceTests.cs: error CS0115` (the two site overrides have nothing to override yet) and
`RefugeServiceTests.cs: error CS0122` (the two distance methods are still `protected`). Any other
file or error code: fix your edit to match this step exactly and re-run; if it persists, STOP.

### Step 14: GREEN, refuge keep-outs: use the shared rule in RefugeService

Edit `Main/Features/Refuge/RefugeService.cs` again (keep its BOM).

1. Replace the two fortification seams and their helper (lines 830-854 at `31cc629f`: from
   `    protected virtual float DistanceToNearestFortification()` down to the closing `    }` of
   `private static float DistanceToNearestFortificationFromPosition(Vec2 position)`, the line just
   above the blank line before the `    /// <summary>` of `SpawnRefugeParty`) with:

   ```csharp
    /// <summary>Every settlement with its kind and its straight-line distance from the main
    /// party; empty when there is no main party.</summary>
    protected virtual IEnumerable<SettlementSite> SettlementSitesFromMainParty()
    {
        var main = MobileParty.MainParty;
        return main == null ? Array.Empty<SettlementSite>() : SettlementSitesAround(main.GetPosition2D);
    }

    /// <summary>Every settlement with its kind and its straight-line distance from the party;
    /// empty when the party cannot be found.</summary>
    protected virtual IEnumerable<SettlementSite> SettlementSitesFrom(string partyId)
    {
        var party = FindParty(partyId);
        return party == null ? Array.Empty<SettlementSite>() : SettlementSitesAround(party.GetPosition2D);
    }

    // Part of the two seams above (decision 55): one lazy read of the campaign's settlement list,
    // no filter, and no list built per call (the keep-outs run as menu-option conditions).
    private static IEnumerable<SettlementSite> SettlementSitesAround(Vec2 position)
    {
        foreach (var settlement in Settlement.All)
        {
            if (settlement == null)
                continue;
            yield return new SettlementSite(settlement.IsTown, settlement.IsCastle, position.Distance(settlement.GetPosition2D));
        }
    }
   ```

2. Insert the two service methods directly above the line
   `    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---`
   (below the `ReleasePeacePrisoners` method Step 10 put there), leaving one blank line before
   that comment:

   ```csharp
    /// <summary>Straight-line distance from the main party to the nearest town or castle;
    /// float.MaxValue when there is none or no main party. The rule is
    /// <see cref="FortificationSearch.NearestDistance"/>, shared with CampService.
    /// internal for TAOM.Tests (InternalsVisibleTo).</summary>
    internal float DistanceToNearestFortification() =>
        FortificationSearch.NearestDistance(SettlementSitesFromMainParty());

    /// <summary>The same distance measured from the given party (a refuge weighing its
    /// stronghold upgrade); float.MaxValue when the party cannot be found. internal for
    /// TAOM.Tests (InternalsVisibleTo).</summary>
    internal float DistanceToNearestFortificationFrom(string partyId) =>
        FortificationSearch.NearestDistance(SettlementSitesFrom(partyId));

   ```

   The calls in `CanFound` (line 156) and `CanUpgrade` (line 259) stay as they are. No new `using`
   is needed (`TAOM.Features.FieldCamp` at line 6 brings `SettlementSite` and `FortificationSearch`;
   `TaleWorlds.Library` brings `Vec2`).

3. Build and test (same two commands as Step 2, with `--filter "FullyQualifiedName~RefugeServiceTests"`).

**Verify**: build `exit=0`, `0 Error(s)`; tests `exit=0`, `Failed: 0, Passed: 139` (136 + 3). The
three existing tests that set `FortDistance` or `FortDistanceFromRefuge` must pass with their bodies
unchanged.

### Step 15: Machine-check the seam shape

Run each command and compare exactly.

1. The old virtual seams are gone:
   `cd E:/repos/taom-improve/wt-026 && git grep -n -E "protected virtual (void ChargePlayer\(SupplySourceInfo|RaidThreat FindNearestHostile|IReadOnlyList<WardenCandidate> CompanionsInMainParty|string MintCompanionFromTroop|void RefundConsumption\(|void ReleasePeacePrisoners\(|float DistanceToNearestFortification)" -- Main TAOM.Tests`
   Expected: no output. (The last alternative also matches `DistanceToNearestFortificationFrom`.)
2. The logic methods exist once each, as `internal`:
   `cd E:/repos/taom-improve/wt-026 && git grep -c -E "internal (void ChargePlayer\(SupplySourceInfo|RaidThreat FindNearestHostile\(|IReadOnlyList<WardenCandidate> CompanionsInMainParty\(\)|string MintCompanionFromTroop\(|void RefundConsumption\(|void ReleasePeacePrisoners\(|float DistanceToNearestFortification(From)?\()" -- Main`
   Expected, exactly these four lines: `Main/Features/FieldCamp/CampService.cs:1`,
   `Main/Features/Refuge/RefugeService.cs:4`, `Main/Features/Refuge/WardenService.cs:2`,
   `Main/Features/SupplyLines/SupplyOrderService.cs:2`.
3. Each log text exists exactly once in `Main`:
   `cd E:/repos/taom-improve/wt-026 && git grep -c -e "promoted-warden rename failed" -e "unreachable, his share is destroyed" -e "unreachable, its share is destroyed" -e "unreachable, troops not restored" -e "unreachable, stock not restored" -e "refund after failed order placement threw" -- Main`
   Expected, exactly: `Main/Features/Refuge/WardenService.cs:1` and
   `Main/Features/SupplyLines/SupplyOrderService.cs:5`.
4. Engine calls sit only in the seams (one line each):
   `cd E:/repos/taom-improve/wt-026 && git grep -n -E "GiveGoldAction\.|MBRandom\.RandomInt|CharacterHelper\.|HeroCreator\.CreateSpecialHero\(|IsAtWarWith\(refugeFaction\)|MobileParty\.All|Settlement\.All|EndCaptivityAction\.|VolunteerTypes|\.RemoveTroop\(" -- Main/Features/SupplyLines/SupplyOrderService.cs Main/Features/Refuge/WardenService.cs Main/Features/Refuge/RefugeService.cs Main/Features/FieldCamp/CampService.cs`
   Expected: exactly 20 lines, 5 + 4 + 9 + 2 per file. Line numbers vary; ignoring the `path:line:`
   prefix, the code text of the hits must be exactly this multiset (leading spaces as shown; the
   block is indented 3 extra spaces because it sits in this list):

   ```text
   SupplyOrderService.cs (5: PayLord, PaySettlement, DestroyPlayerGold, ReadEmptyVolunteerSlots, FillVolunteerSlot)
           GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, lord, amount, disableNotification: true);
           GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, amount, disableNotification: true);
           GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, disableNotification: true);
               var slots = notable?.VolunteerTypes;
           var slots = notables[notableIndex]?.VolunteerTypes;
   WardenService.cs (4: RandomCompanionTemplateId twice, NextRandomInt, CreatePromotedHero)
               ? CharacterHelper.GetRandomCompanionTemplateWithPredicate()
               : CharacterHelper.GetRandomCompanionTemplateWithPredicate(
       protected virtual int NextRandomInt(int maxExclusive) => MBRandom.RandomInt(maxExclusive);
           var hero = HeroCreator.CreateSpecialHero(template, bornSettlement: null, faction: clan, supporterOfClan: null, age: age);
   RefugeService.cs (9)
           GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, disableNotification: true);
           GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount, disableNotification: true);
           foreach (var party in MobileParty.All)
           foreach (var party in MobileParty.All)
           return faction != null && faction.IsAtWarWith(refugeFaction);
           return hero.MapFaction != null && hero.MapFaction.IsAtWarWith(refugeFaction);
               EndCaptivityAction.ApplyByPeace(hero);
               roster.RemoveTroop(hero.CharacterObject);
           foreach (var settlement in Settlement.All)
   CampService.cs (2)
           foreach (var settlement in Settlement.All)
           GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, disableNotification: true);
   ```

   In `RefugeService.cs`, the two `GiveGoldAction` hits and the first `MobileParty.All` are
   untouched out-of-scope code (at `31cc629f` lines 794, 799 and 1093: `ChargePlayer(int)`,
   `RefundPlayer` and `AllRefugePartyIds`); the other `MobileParty.All` and the `faction.IsAtWarWith`
   line are `ScanForRaiders` and `IsAtWarWithRefuge`; the `hero.MapFaction` line is
   `IsPrisonerAtWarWithRefuge`; `EndCaptivityAction` and `RemoveTroop` are `ReleasePrisonerByPeace`
   and `RemoveFromRefugePrisonRoster`; `Settlement.All` is `SettlementSitesAround`. In
   `CampService.cs`, `Settlement.All` is `SettlementSitesFromMainParty` and the `GiveGoldAction` hit
   is the untouched `ChargePlayer(int)` (line 766 at `31cc629f`). Any other hit is a STOP: for
   example the old `if (faction == null || !faction.IsAtWarWith(refugeFaction))` line, the old
   `if (hero.MapFaction != null && hero.MapFaction.IsAtWarWith(refugeFaction))` line, a third
   `foreach (var settlement in Settlement.All)` line, or a `GiveGoldAction` line with `sourceShare`
   or `fees`. (The new site seams keep the old loops' `foreach (var settlement in Settlement.All)`
   text, one per file; item 6 below is what proves the old town-or-castle filter is gone.)
5. No public interface changed:
   `cd E:/repos/taom-improve/wt-026 && git diff --stat -- Main/Features/SupplyLines/ISupplyOrderService.cs Main/Features/Refuge/IRefugeService.cs Main/Features/Refuge/IWardenService.cs Main/Features/FieldCamp/ICampService.cs`
   Expected: no output.
6. The town-or-castle filter exists once, in the shared rule:
   `cd E:/repos/taom-improve/wt-026 && git grep -n -E "!(settlement|site)\.IsTown" -- Main/Features/FieldCamp/CampService.cs Main/Features/Refuge/RefugeService.cs`
   Expected: exactly one line, `Main/Features/FieldCamp/CampService.cs:<n>:            if (!site.IsTown && !site.IsCastle)`
   (inside `FortificationSearch.NearestDistance`; `<n>` varies).

**Verify**: all six outputs match.

### Step 16: CHANGELOG entry

`CHANGELOG.md` starts with a UTF-8 BOM, CRLF line endings, a title, an archive note, then dated
sections (`## 2026-09-24` is the first, at both `31cc629f` and `e452b0c7`), each holding
`### <commit subject>` entries.

1. Get today's date: `date +%F`.
2. The entry always goes at the top. If the first `## ` heading (line 5 at `31cc629f` and
   `e452b0c7`) is today's date, put the entry directly under it, above any entry already there.
   Otherwise insert `## <today>` and a blank line above that first `## ` heading and put the entry
   under the new heading. Do this even when a heading with today's date already exists lower in the
   file (a stray, out-of-order `## 2026-09-25` sits at line 184 at `31cc629f`, line 219 at
   `e452b0c7`): leave that one alone; the duplicate heading is accepted. Replace `76` with your
   actual number of new tests if it differs. The block below is indented 3 spaces only because it
   sits in this list: remove that indentation, so the `###` line and every prose line start in
   column 1.

   ```markdown
   ### refactor(seams): v2.0.30 - move decision logic out of nine seams

   Nine protected-virtual seams held TAOM decisions that no unit test could reach, because every
   test subclass overrides its seams: the supply-order payee routing and the refund after a failed
   placement (`SupplyOrderService.ChargePlayer`, `RefundConsumption`), the refuge raid-target pick and
   the peace release of refuge-held prisoners (`RefugeService.FindNearestHostile`,
   `ReleasePeacePrisoners`), the warden companion filter and companion minting
   (`WardenService.CompanionsInMainParty`, `MintCompanionFromTroop`), and the nearest town-or-castle
   search behind the camp and refuge keep-out distances (`CampService.DistanceToNearestFortification`,
   `RefugeService.DistanceToNearestFortification` and `DistanceToNearestFortificationFrom`), whose two
   identical copies now share one rule, `FortificationSearch.NearestDistance`. Each decision now lives
   in its service, and each seam is one engine operation: pay a lord, pay a settlement or destroy gold;
   check a refund source, return troops or goods, read or fill volunteer slots; scan the map, check a
   war or render a name; count, read, war-check, release or drop one prisoner row; read the roster;
   read the troop, draw a template, read the coming-of-age, draw the age, create, rename or enrol the
   hero; list the settlements around a party. That is the ADR-007 seam rule (plan 021, decisions 49,
   55, 56 and 57). Behaviour is unchanged: amounts, notification flags, log lines, filter order, the
   strict comparisons, the first-found tie-break, the RNG draws, the silent drop of recruits beyond the
   free volunteer slots and the backwards prisoner walk are as before, and the war checks still read
   the engine's factions only where the old code did. 76 new tests pin the moved logic. Owed in game: a
   supply order from a town and from a lord, and one that fails and refunds; a soldier promoted to
   warden; a raid with raids enabled; a peace that frees a refuge-held lord; the town keep-out when
   pitching a camp, founding a refuge and upgrading it to a stronghold.
   ```

   Use the version from `Main/_Module/SubModule.xml` if it is not `v2.0.30`.
3. Dash check on your added lines:
   `cd E:/repos/taom-improve/wt-026 && python -c "import subprocess; d = subprocess.run(['git', 'diff', '-U0', '--', 'CHANGELOG.md'], capture_output=True).stdout.decode('utf-8'); print(sum(1 for l in d.splitlines() if l.startswith('+') and ('\u2014' in l or '\u2013' in l)))"`
   Expected: `0`. (Python reads the `\u2014` and `\u2013` escapes itself, so the result does not
   depend on the shell's locale.) Also confirm the entry is not indented:
   `cd E:/repos/taom-improve/wt-026 && git diff -U0 -- CHANGELOG.md | grep -c '^+   '`
   Expected: `0`.

**Verify**: `cd E:/repos/taom-improve/wt-026 && git diff --stat -- CHANGELOG.md` shows 1 file changed
with only insertions, and step 3 printed `0`.

### Step 17: Full verification, then commit

1. Build: `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -3; echo "exit=$?"`
   Expected: `exit=0`, `0 Error(s)`.
2. Full suite: `cd E:/repos/taom-improve/wt-026 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tee E:/repos/taom-improve/scratch/plans/026/final.log | tail -5; echo "exit=$?"`
   Expected: `exit=0`, `Failed: 0`, `Skipped:` equal to S0, `Passed:` equal to P0 + 76 (10705 when P0 is
   10629; Total 10707 when S0 is 2).
3. Docs: `cd E:/repos/taom-improve/wt-026 && python tools/lint_docs.py > E:/repos/taom-improve/scratch/plans/026/lint.log 2>&1; echo "exit=$?"; grep -F "Em/en dashes in newly written prose" E:/repos/taom-improve/scratch/plans/026/lint.log`
   Expected: `exit=0` and the line `- Em/en dashes in newly written prose: **0**`. (At `31cc629f` and
   `e452b0c7` the log also reports 1 missing feature doc, `TrollBruteForce`, and 7 context-budget
   warnings; both are pre-existing and not yours.)
4. Scope: `cd E:/repos/taom-improve/wt-026 && git status --porcelain`
   Expected, exactly these lines (any order), plus
   `?? plans/_audit/2026-09-23-opus/plan-review-026.md` if the review file is present:
   ```text
    M CHANGELOG.md
    M Main/Features/FieldCamp/CampService.cs
    M Main/Features/Refuge/RefugeService.cs
    M Main/Features/Refuge/WardenService.cs
    M Main/Features/SupplyLines/SupplyOrderService.cs
    M TAOM.Tests/Features/FieldCamp/CampServiceTests.cs
    M TAOM.Tests/Features/Refuge/RefugeServiceTests.cs
    M TAOM.Tests/Features/Refuge/WardenServiceTests.cs
    M TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs
   ?? plans/026-seam-decision-logic.md
   ```
5. Write the commit message to `E:/repos/taom-improve/scratch/plans/026/commit-msg.txt` with the
   Write tool (not a heredoc), body wrapped at 72 columns. The block below is indented 3 spaces
   only because it sits in this list: remove that indentation, so the subject and every body line
   start in column 1.

   ```text
   refactor(seams): v2.0.30 - move decision logic out of nine seams

   Nine protected-virtual seams in SupplyOrderService, RefugeService,
   WardenService and CampService carried TAOM decisions that no unit
   test ran, because the test subclasses override every seam: the supply
   payee routing and its unreachable-payee fallback; the refund route,
   count filter and volunteer-slot walk; the refuge raid-target filters
   and nearest pick; the peace release of refuge-held prisoners; the
   warden companion filter; the promotion policy (culture, template
   fallback, age and rename tolerance); and the nearest town-or-castle
   search behind the camp and refuge keep-outs.

   Each decision moves into its service as an internal method, and each
   seam is now one engine operation with ids, primitives and TAOM data
   classes in its signature, per the ADR-007 seam rule (plan 021,
   decisions 49, 55, 56 and 57). The two identical fortification
   searches now share FortificationSearch.NearestDistance, declared in
   CampService.cs. Behaviour is unchanged: amounts, flags, log texts,
   filter order, the strict comparisons, the tie-break, the RNG draws,
   the silent drop of surplus recruits and the backwards prisoner walk
   are as before. The war checks stay separate seams, so the engine's
   MapFaction is read only where it was before. 76 new tests pin the
   moved logic.

   Not-tested: the seam bodies (GiveGoldAction, the roster, item and
   volunteer-slot writes, the MobileParty.All and Settlement.All scans,
   EndCaptivityAction, HeroCreator and the enrol actions) run only in
   game.
   ```

   Add `Refs #<number>` as the last body line if the orchestrator gave you an issue number, and
   replace `76` if your count differs.
6. Stage and commit:
   `cd E:/repos/taom-improve/wt-026 && git add CHANGELOG.md Main/Features/FieldCamp/CampService.cs Main/Features/Refuge/RefugeService.cs Main/Features/Refuge/WardenService.cs Main/Features/SupplyLines/SupplyOrderService.cs TAOM.Tests/Features/FieldCamp/CampServiceTests.cs TAOM.Tests/Features/Refuge/RefugeServiceTests.cs TAOM.Tests/Features/Refuge/WardenServiceTests.cs TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs && git commit -F E:/repos/taom-improve/scratch/plans/026/commit-msg.txt`
7. `cd E:/repos/taom-improve/wt-026 && git log -1 --format="%h %s" && git status --porcelain`

**Verify**: the log line shows the subject exactly (no leading spaces); status shows only
`?? plans/026-seam-decision-logic.md`, plus `?? plans/_audit/2026-09-23-opus/plan-review-026.md`
if the review file is present.

## Test plan

- **New tests (76)**, each named `MethodName_StateUnderTest_ExpectedBehavior`:
  - `SupplyOrderServiceTests` (23, class tag `RequiresGame` kept). `ChargePlayer` (10, Step 1):
    settlement and lord routing, the lord-wins precedence, empty `HeroId`, both unreachable
    fallbacks with their exact warning text, no warning when reachable, zero share, zero fees,
    non-positive amounts. `RefundConsumption` (13, Step 7): settlement route (goods, then
    volunteers), empty `HeroId`, lord route (troops only, goods ignored), both unreachable
    warnings with their exact text, non-positive counts on each route, first empty slot in notable
    order, a notable without slots, the silent truncation beyond the free slots, the fresh slot
    read per troop type, no notables, and the catch-all error log.
  - `RefugeServiceTests` (27, tag `RequiresGame` kept). `FindNearestHostile` (16, Step 3): no scan,
    empty scan, one test per filter (null, main party, inactive, in map event, other refuge, not
    at war, no soldiers), the war-check evaluation order, nearest pick, first-found tie-break,
    exactly at range (excluded), just inside range, NaN distance (the mandatory moved-gate NaN
    test), and the result's id, name and handle. `ReleasePeacePrisoners` (8, Step 9): no rows,
    held by the refuge (peace release), held by another captor (row dropped), still at war (kept),
    main hero (no war check), a row without a hero, the backwards walk with removals, and a mixed
    roster. Keep-out wiring (3, Step 13): main-party sites, the named party's sites, and a
    village-only neighbourhood not blocking `CanFound`.
  - `WardenServiceTests` (16, Step 5, untagged, must stay engine-free): main hero excluded,
    non-companion excluded, null skipped, field mapping and roster order, `Candidates()` end to
    end; mint: no source, hero troop, troop culture, player-culture fallback, no culture at all,
    culture-template fallback, no template (stops before the RNG), age arithmetic with one
    `RandomInt(14)` draw, the 18 default, creation refused, rename throws (warning and enrol still
    happen), and the full step order.
  - `CampServiceTests` (10, Step 11, tag `RequiresGame` kept): the shared rule
    `FortificationSearch.NearestDistance` (null, empty, villages only, a town counts, a castle
    counts, nearest fortification rather than nearest settlement, NaN never wins, only NaN gives
    `float.MaxValue`: the two mandatory moved-gate NaN tests), the camp wiring, and a village-only
    neighbourhood not blocking `CanEstablish`.
- **Existing tests**: all stay green. Only three change their assertions, because the seams they
  observed were re-shaped: `TryPlaceOrder_Success_ChargesOnlyAfterConsumeAndSpawn` (Step 1; charge
  after consume and spawn, source credited, 170 total, all kept) and
  `TryPlaceOrder_UnaffordableTotal_RefundsAndDoesNotCharge` and
  `TryPlaceOrder_SpawnFails_RefundsAndDoesNotCharge` (Step 7; the consumed grain and recruits now
  visibly go back to the source instead of the consumption object being handed to an override).
- **Structural pattern**: the existing subclass-override style in each file (for example
  `TestableRefugeService` in `RefugeServiceTests.cs`).
- **Structurally untestable** (name it in the `Not-tested:` trailer): the seam bodies, which call
  `GiveGoldAction`, write rosters, item rosters and volunteer slots, scan `MobileParty.All` and
  `Settlement.All`, read `MapFaction`, call `EndCaptivityAction.ApplyByPeace`, `TroopRoster.RemoveTroop`,
  `CharacterHelper`, `HeroCreator.CreateSpecialHero`, `Hero.SetName`, `AddCompanionAction` and
  `AddHeroToPartyAction`.
- **Verification**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` passes with
  P0 + 76 tests (10705 when P0 is 10629).

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with `Failed: 0`,
      `Skipped:` = S0 and `Passed:` = P0 + 76.
- [ ] Filtered runs: `SupplyOrderServiceTests` 68 passed, `RefugeServiceTests` 139 passed,
      `WardenServiceTests` 36 passed, `CampServiceTests` 91 passed, 0 failed each.
- [ ] Step 15's six commands print exactly their expected output.
- [ ] `python tools/lint_docs.py` prints `Em/en dashes in newly written prose: **0**`.
- [ ] `git status --porcelain` after the commit shows only `?? plans/026-seam-decision-logic.md`
      (plus `?? plans/_audit/2026-09-23-opus/plan-review-026.md` if the review file is present);
      `git show --stat HEAD` lists exactly the 9 in-scope files.
- [ ] The commit subject matches `refactor(seams): v2.0.30 - move decision logic out of nine seams`
      (at most 72 characters) and the body has no `Co-Authored-By` line.

## STOP conditions

Stop and report back (do not improvise) if:

- The drift check prints anything and the live code differs from a "Current state" excerpt, Step 0
  shows a HEAD other than `e452b0c7` or a non-empty `git diff --name-only 31cc629f HEAD -- Main TAOM.Tests`,
  or Step 0 shows class counts other than 45, 112, 20 and 81.
- The baseline suite (Step 0) has a failure.
- A RED step fails with an error in a file other than its own test file, or with an error code
  outside the ones listed, after you re-checked your edit against the plan.
- An **existing** test fails after a GREEN step and a re-read of the excerpt does not show a
  transcription mistake: the refactor changed behaviour. Do not edit the existing test to make it pass.
- Making the change seems to need a new or changed member on `ISupplyOrderService`, `IRefugeService`,
  `IRefugeBook`, `IWardenService` or `ICampService`, a change to any caller outside the four service
  files, or a change to `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` or
  `Directory.Build.props`.
- The compiler reports that a seam is overridden somewhere else (a `CS0115` or `CS0506` in a file
  this plan does not list): another subclass exists that this plan did not find.
- `FindHero` in `WardenService.cs` is not `Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId)`
  or `FindTroop` is not `MBObjectManager.Instance?.GetObject<CharacterObject>(troopId)`: the mint
  seams rely on exactly these lookups finding the just-created hero and the drawn template (see
  "Engine facts").
- A TaleWorlds signature you call differs from "Engine facts" (a compile error naming an engine
  member): report it, do not decompile and improvise.
- **Volunteer-slot truncation**: `RefundConsumption_MoreRecruitsThanFreeSlots_ExtraDroppedSilently`
  fails, or you find yourself adding a log line, a counter or a return value for recruits that do
  not fit. The source drops them silently; surfacing them is a behaviour change for a separate
  decision.
- **Backwards prisoner walk**: `ReleasePeacePrisoners_WalksFromTheLastRowToTheFirst` or
  `ReleasePeacePrisoners_MixedRoster_EachRowGetsItsOwnVerdict` fails and you are tempted to
  snapshot the roster into a list and walk that, to walk forwards, to add an index range guard in
  `ReadRefugePrisonerAt`, or to decompile `EndCaptivityAction`. The service must read the row count
  once and each row afresh by index, from the last row to the first, exactly as the source did. If
  that shape cannot pass, STOP.
- **Two fortification copies**: the pre-change loops in `CampService.cs:745-761` and
  `RefugeService.cs:842-854` differ from the "Seam 7" excerpts (a different filter, comparison or
  start value): the shared `FortificationSearch` assumes they are the same rule. Also STOP if
  `RefugeService.cs` cannot see `SettlementSite` or `FortificationSearch` (its `using
  TAOM.Features.FieldCamp;` at line 6 is missing), or if anything outside the nine in-scope files
  would need them.
- You are tempted to also refactor an out-of-scope seam (`DeliverCargoToPlayer`,
  `ResolveMilitiaTroopId`, `PickCompanionEscortId`, `CampService.EnumerateHostileCandidates` and
  `CollectHostiles`): note it in your report instead.
- A commit hook denies the commit for a reason you cannot fix inside the 9 in-scope files.

## Maintenance notes

- **What a reviewer should probe** (the orchestrator runs `/deep-review` before merge; it spans 8 C#
  files):
  - Evaluation order in `ScanForRaiders`: it now reads `IsMainParty`, `IsActive`, `MapEvent`,
    `PartyComponent`, `MemberRoster` and `GetPosition2D` for every party (all safe getters, see
    "Engine facts"), but `MapFaction` still only in `IsAtWarWithRefuge`, after the four cheap
    filters. `FindNearestHostile_WarCheck_RunsOnlyAfterTheFourCheapFilters` pins that. The same
    shape holds for prisoners: `Hero.MapFaction` is read only in `IsPrisonerAtWarWithRefuge`, after
    the main-hero filter (`ReleasePeacePrisoners_MainHero_NeverReleasedAndNeverWarChecked`).
  - Cost: `ScanForRaiders` allocates one `RaidCandidate` per map party. It runs only when a ready,
    idle refuge wins its 5 percent hourly raid roll with raids enabled (raids ship off), so it is
    rare; names are still rendered only for the winner.
  - Cost: each keep-out check enumerates the settlements lazily through an iterator (one small
    iterator object per check; no list of the 1,002 settlements in the live `TAOM_Map`), where the
    old loop allocated nothing. The checks run from `CanEstablish` and `CanFound`/`CanUpgrade`,
    which feed game-menu option conditions (`FieldCampMenuController.EstablishCondition`,
    `RefugeMenuController.FoundCondition` on the camp's wait menu, `UpgradeCondition`). Do not turn
    the seams back into list builders; if a profiler ever shows even the iterator matters, cache the
    distance per campaign hour in the service, not in the seam.
  - The refund seams re-resolve the lord (`FindHero`, a linear scan) or the settlement
    (`Settlement.Find`, a dictionary lookup) once per entry instead of once per refund, and the
    volunteer snapshot is re-read once per troop type. A refund happens only after a failed order
    placement, so the extra lookups cost nothing noticeable.
  - The refund now skips the item or troop lookup for a non-positive count (the old code looked the
    id up first, then tested the count). `MBObjectManager.GetObject` only reads, so the one
    observable difference is when `MBObjectManager.Instance` is null and every entry is
    non-positive: the old code logged the catch-all error, the new code logs nothing. That cannot
    happen inside a running campaign. The reverse also holds: an unknown item or troop id with a
    positive count, which the old code skipped before doing anything, now reaches
    `ReturnGoodsToSettlement`, `ReturnTroopsToLord` or (after a slot-snapshot walk)
    `FillVolunteerSlot`, each of which finds no object and does nothing. The engine outcome is the
    same.
  - The prison seams re-find the refuge (`FindParty`, a linear scan) per row read and per war
    check; prison rosters are small and a peace is rare. `IsPrisonerAtWarWithRefuge` re-reads the
    refuge's faction each time instead of capturing it once; if the faction vanished mid-walk the
    check returns false (release), where the old code kept its captured faction. Likewise
    `ReadRefugePrisonerAt` re-finds the refuge's roster per row where the old loop held one roster
    reference: if the refuge vanished mid-walk, every remaining row reads null and is skipped.
    Nothing in the walk removes the refuge or changes its own faction.
  - The mint seams re-find the new hero by id (`FindHero`, a linear scan of the alive and dead hero
    lists) and the template by id (`FindTroop`, a dictionary lookup). Both are verified to resolve
    (see "Engine facts"); a promotion is a rare player action, so the scans cost nothing noticeable.
    If an engine bump changes `Hero`'s constructor registration or `MBObjectManager`'s registry,
    `EnrolPromotedHero` would silently no-op and a soldier would be consumed for an unenrolled hero:
    the in-game promotion smoke is the check.
  - `HeroesInMainParty` now renders a display name for every hero in the roster (including the main
    hero), not only for companions: a handful of `TextObject.ToString()` calls per `Candidates()`.
  - `RandomCompanionTemplateId` compares culture `StringId`s instead of `CultureObject` references;
    equivalent because `MBObjectManager` keeps one object per id.
  - `FortificationSearch` lives in `CampService.cs` because RefugeService already depends on the
    FieldCamp namespace and FieldCamp never depends on Refuge. The two settlement scans (one per
    service) stay separate seam bodies; only the decision is shared.
- **In-game checks owed** (label `triage-needs-ingame` on the issue at close): place a supply order
  from a town and from a lord and watch both golds move; make one fail (too little gold) and see the
  town's goods and volunteers, or the lord's troops, come back; promote a soldier to warden when
  founding a refuge (named after his troop, in the party, a clan companion); enable raids and let a
  hostile party raid a refuge; make peace with a faction whose lord a refuge holds and see him
  freed; try to pitch a field camp, found a refuge and upgrade it to a stronghold near a town and
  near only a village.
- **Interactions**: plan 021 (the ADR text) is merged; this plan satisfies its condition 2 for the
  four seams its deep review named and the three decision 57 added. Plan 020 (CHANGELOG generated
  at release, in progress) may change how CHANGELOG entries are written; if it merges first, the
  orchestrator moves this entry's text into the commit body instead.
- **Deferred**: other seams with branching (`SupplyOrderService.DeliverCargoToPlayer`,
  `RefugeService.ResolveMilitiaTroopId`, `CampService.EnumerateHostileCandidates` with
  `CollectHostiles`) were not named by the 021 review or decision 57 and are left for a separate
  decision.
