# Plan 050: The messenger arrival verdict is one tested classification in MessengerService

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given (a branch made for
> this plan, to be merged later; never the trunk). The orchestrator keeps `plans/README.md`; do not
> edit it.
>
> **Drift check (run first)**:
> `git diff --stat 7087fa633..HEAD -- Main/Features/Messengers/MessengerCampaignBehavior.cs Main/Features/Messengers/MessengerService.cs Main/Features/Messengers/IMessengerService.cs Main/Features/Messengers/MessengerTargetReader.cs Main/Features/Messengers/Domain TAOM.Tests/Features/Messengers TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs Client/Features/Messengers/UI/MessengerEncyclopediaMixin.cs docs/features/messengers.md`
> If any in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition.

## Status

- **Priority**: P3
- **Effort**: M
- **Risk**: MED
- **Depends on**: none
- **Category**: tech-debt
- **Planned at**: commit `7087fa633`, 2026-11-12
- **Baseline at that commit**: dotnet `Passed!  - Failed:     0, Passed: 39250, Skipped:     9, Total: 39259`
  (game-backed desktop run), failing: none. Build: exit 0, `11 Warning(s)`, `0 Error(s)`. Python suite:
  `Ran 8578 tests`, `FAILED (failures=7, errors=2, skipped=13)`, all nine heraldry and kingdom-generator
  data checks that fail on the trunk (this plan touches no `tools/` file, so the Python suite is not
  rerun). Hook suite: `1267 passed, 0 failed` (not touched by this plan).
- **Issue**: filed by the orchestrator before execution

## Why this matters

`MessengerCampaignBehavior` is the largest TAOM-owned entry point in the ADR-002 shrink-only baseline
(`TAOM.Features.Messengers.MessengerCampaignBehavior 668`). Inside it, `HandleArrivedMessenger` decides on
raw `Hero` objects whether an arrived messenger is lost, confused, waiting or ready. That rule is the only
copy of "is the target still reachable" with no unit test, and it already differs from the tested
send-time copy (`MessengerService.CanSendMessenger`). This plan moves the decision into
`MessengerService.ClassifyArrival` as a pure function over the existing `HeroSnapshot` plus three
booleans, pins every input combination with tests, and leaves the behavior with only the engine reads and
the inquiry calls. **There is no behaviour change.** The classification encodes the rule as it EXECUTES
today, which is not what its comments say: a prisoner is LOST on arrival (not deferred), and a wanderer that
is `Released`, `Traveling` or `Disabled` is lost although the send check let it through. Both are known bug
leads the orchestrator raises separately; changing either here is a defect.

## Current state

### Files and roles

- `Main/Features/Messengers/MessengerCampaignBehavior.cs` (695 lines; class body lines 28 to 695 = 668,
  the baseline number): the boundary. Hourly tick, arrival handling, conversation, dialog tree.
- `Main/Features/Messengers/MessengerService.cs` (154 lines): pure logic, `CanSendMessenger` (send rule),
  quote, accident roll, position math. Implements `IMessengerService`.
- `Main/Features/Messengers/IMessengerService.cs` (21 lines): the service contract the behavior holds as
  `_service`. The only implementer in the repo is `MessengerService` (`git grep -n "class .*: *IMessengerService"`
  gives only `Main/Features/Messengers/MessengerService.cs:7`); tests use `Substitute.For<IMessengerService>()`
  (`CoopAuthorityGateTests.cs:199`, `MessengerCampaignBehaviorTests.cs:112`), which tolerates a new member.
- `Main/Features/Messengers/MessengerTargetReader.cs` (59 lines): the one engine read of a target; builds
  `HeroSnapshot`. Engine-bound, not unit-tested.
- `Main/Features/Messengers/Domain/HeroSnapshot.cs` (42 lines): the plain-value snapshot (11 properties).

### The hourly loop (`MessengerCampaignBehavior.cs:214-225`)

```csharp
        foreach (var messenger in _store.GetAll())
        {
            if (messenger.Arrived)
            {
                if (!_processingArrivedMessenger)
                {
                    var outcome = HandleArrivedMessenger(messenger);
                    if (outcome == ArrivedHandleOutcome.RemoveFromList)
                        _toRemoveScratch.Add(messenger.TargetHeroId);
                }
                continue;
            }
```

`_processingArrivedMessenger` is set only on the ready path (`:309`), so later arrived messengers in the
same pass are skipped once one inquiry is up. This plan does not touch the loop.

### The arrival handler today (`MessengerCampaignBehavior.cs:265-327`)

```csharp
    private enum ArrivedHandleOutcome
    {
        WaitForNextTick,
        AwaitingUserInput,
        RemoveFromList,
    }

    private ArrivedHandleOutcome HandleArrivedMessenger(PendingMessenger messenger)
    {
        var target = ResolveHero(messenger.TargetHeroId);
        if (target == null)
            return ArrivedHandleOutcome.RemoveFromList;

        // Permanent unavailability classes — drop with a "messenger lost" notification.
        // Codex review #34 caught: send-time validation rejects fugitive / dead / disabled targets,
        // but the original arrival path only screened dead/disabled. A target that became fugitive
        // mid-flight could pass through the temporary-wait path and trigger StartMessengerConversation
        // with no settlement / no party, falling back to MainParty as the conversation partner.
        if (!target.IsAlive
            || target.HeroState == Hero.CharacterStates.Disabled
            || target.IsFugitive
            || (!target.IsActive && !target.IsWanderer)
            || (!target.IsActive && target.IsWanderer && target.HeroState != Hero.CharacterStates.NotSpawned))
        {
            NotifyMessengerLost(messenger, target);
            return ArrivedHandleOutcome.RemoveFromList;
        }

        if (target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo)
        {
            var text = new TextObject("{=taom_messenger_confused}The messenger seems confused — they were trying to reach someone in your own party!");
            text.SetTextVariable("HERO_NAME", target.Name);
            ShowInquiry(
                new TextObject("{=taom_messenger_error}Messenger Error").ToString(),
                text.ToString(),
                affirmative: GameTexts.FindText("str_ok").ToString());
            return ArrivedHandleOutcome.RemoveFromList;
        }

        // Temporary unavailability — defer to next tick. IsTargetAvailableNow gates on
        // prisoner/child/MapEvent (transient), all of which the player may resolve.
        if (!IsPlayerAvailable() || !IsTargetAvailableNow(target))
            return ArrivedHandleOutcome.WaitForNextTick;

        _processingArrivedMessenger = true;

        var arrivalText = new TextObject("{=taom_messenger_arrived}A messenger from {HERO_NAME} has arrived. Do you wish to speak with them?");
        // ... the Speak / Dismiss inquiry, unchanged by this plan ...
        return ArrivedHandleOutcome.AwaitingUserInput;
    }
```

The comment at `:304-305` is wrong about prisoners (see the semantics table). The confused string keeps
its existing em dash: it is a player-facing localized string and must stay byte-identical.

### Lost notice (`MessengerCampaignBehavior.cs:348-358`), unchanged

```csharp
    private void NotifyMessengerLost(PendingMessenger messenger, Hero deadTarget)
    {
        var targetName = deadTarget?.Name ?? new TextObject(messenger.TargetHeroId);

        var text = new TextObject("{=taom_messenger_recipient_gone}Your messenger to {HERO_NAME} returned without delivering — the recipient is no longer reachable.");
        text.SetTextVariable("HERO_NAME", targetName);
        ShowInquiry(
            new TextObject("{=taom_messenger_lost_title}Messenger Lost").ToString(),
            text.ToString(),
            affirmative: GameTexts.FindText("str_ok").ToString());
    }
```

### Helpers (`MessengerCampaignBehavior.cs:612-632`)

```csharp
    // FindFirst scans Characters on purpose: Hero.Find misses a dead hero the engine unregistered (UnregisterDeadHero),
    // which would drop the messenger with no "lost" notice.
    private static Hero ResolveHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return null;
        return Hero.FindFirst(h => h != null && h.StringId == heroId);
    }

    private static bool IsTargetAvailableNow(Hero hero)
    {
        if (hero == null || !hero.IsAlive || hero.IsPrisoner || hero.IsChild) return false;
        return hero.PartyBelongedTo?.MapEvent == null;
    }

    private static bool IsPlayerAvailable()
    {
        if (Campaign.Current?.MainParty == null) return false;
        if (PlayerEncounter.Current != null) return false;
        if (GameStateManager.Current?.ActiveState is MapState mapState && !mapState.AtMenu) return true;
        return false;
    }
```

`IsTargetAvailableNow` has one caller (`:306`); `git grep -l IsTargetAvailableNow` at the base lists only
this file and `docs/changelog-archive/CHANGELOG-2026-H1.md` (history, not edited). `ResolveHero` and
`IsPlayerAvailable` stay exactly as they are (`IsPlayerAvailable` is pinned, see Pins).

`GetReason` (`:634-680`) is a `public TextObject GetReason(MessengerOffer offer, Hero target)` used by the
client mixin. **This plan leaves it alone** (see Out of scope).

### The send-time rule (`MessengerService.cs:26-68`), unchanged

```csharp
    public MessengerValidationResult CanSendMessenger(HeroSnapshot? target, int playerGold, int quotedCost, bool inPerson)
    {
        if (target == null)
            return MessengerValidationResult.NullTarget;
        if (target.IsHumanPlayerCharacter)
            return MessengerValidationResult.HumanPlayerCharacter;
        if (!target.IsAlive)
            return MessengerValidationResult.HeroDead;
        if (target.IsPrisoner)
            return MessengerValidationResult.HeroPrisoner;
        if (target.IsChild)
            return MessengerValidationResult.HeroChild;
        if (target.IsFugitive)
            return MessengerValidationResult.HeroFugitive;

        var canBeReached = target.IsActive
                           || (target.IsWanderer && !target.IsActive);
        if (!canBeReached)
            return MessengerValidationResult.TargetUnavailable;

        if (target.IsInPlayerParty)
            return MessengerValidationResult.TargetInPlayerParty;
        // ... SameSettlement, SameArmy, gold, AlreadyPending, Ok ...
    }
```

(Blank lines between the guards are elided in this excerpt.) Note `IsWanderer && !IsActive` accepts a
wanderer in `Released`, `Traveling` or `Disabled`; arrival does not. Do not align them.

### The reader (`MessengerTargetReader.cs:15-38`)

```csharp
    internal static HeroSnapshot? ReadSnapshot(Hero? hero)
    {
        if (hero == null) return null;
        var mainParty = Campaign.Current?.MainParty;
        var inPlayerParty = hero.PartyBelongedTo != null
                            && Hero.MainHero?.PartyBelongedTo != null
                            && hero.PartyBelongedTo == Hero.MainHero.PartyBelongedTo;
        var here = mainParty?.CurrentSettlement;
        var army = mainParty?.Army;
        return new HeroSnapshot(
            heroId: hero.StringId,
            isAlive: hero.IsAlive,
            isPrisoner: hero.IsPrisoner,
            isChild: hero.IsChild,
            isFugitive: hero.IsFugitive,
            isActive: hero.IsActive,
            isWanderer: hero.IsWanderer,
            isHumanPlayerCharacter: hero.IsHumanPlayerCharacter,
            isInPlayerParty: inPlayerParty,
            sameSettlement: here != null && hero.CurrentSettlement == here,
            sameArmy: army != null && mainParty != null && hero.PartyBelongedTo != null
                      && army.DoesLeaderPartyAndAttachedPartiesContain(mainParty)
                      && army.DoesLeaderPartyAndAttachedPartiesContain(hero.PartyBelongedTo));
    }
```

`IsInPlayerParty` is false when either party is null. The arrival test
`target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo` is TRUE when both are null (a partyless target
while the main hero has no party). They are different facts: do not reuse `IsInPlayerParty` for arrival.

### The snapshot (`Domain/HeroSnapshot.cs:17-28`, constructor)

```csharp
    public HeroSnapshot(
        string heroId,
        bool isAlive,
        bool isPrisoner,
        bool isChild,
        bool isFugitive,
        bool isActive,
        bool isWanderer,
        bool isHumanPlayerCharacter,
        bool isInPlayerParty,
        bool sameSettlement = false,
        bool sameArmy = false)
```

`TAOM.Tests/Features/Messengers/MessengerServiceTests.cs` builds `HeroSnapshot` 13 times (lines 39, 56, 69,
82, 95, 108, 121, 134, 155, 166, 177, 200, 221). The new fact is an OPTIONAL TRAILING parameter so none of
those 13 sites change. History: a `HeroSnapshot.IsInMapEvent` field was removed as unread in commit
`464cbd37` ("arrival still waits for a battle to end by reading the engine directly"); so the map-event fact
stays a behavior read passed as a bool, not a snapshot field.

### Engine facts (`pwsh tools/taom-src.ps1 path Hero`, v1.5.7 decompile, `TaleWorlds.CampaignSystem.Hero.cs`)

```csharp
	public enum CharacterStates            // :28-38
	{
		NotSpawned,
		Active,
		Fugitive,
		Prisoner,
		Released,
		Dead,
		Disabled,
		Traveling
	}

	public CharacterStates HeroState { get { return _heroState; } private set { ChangeState(value); } }  // :233-243
	public bool IsDead => HeroState == CharacterStates.Dead;          // :303
	public bool IsFugitive => HeroState == CharacterStates.Fugitive;  // :305
	public bool IsPrisoner => HeroState == CharacterStates.Prisoner;  // :307
	public bool IsActive => HeroState == CharacterStates.Active;      // :311
	public bool IsDisabled => HeroState == CharacterStates.Disabled;  // :315
	public bool IsAlive => !IsDead;                                    // :319
	public bool IsWanderer => Occupation == Occupation.Wanderer;      // :330
	public bool IsChild => Age < (float)Campaign.Current.Models.AgeModel.HeroComesOfAge;  // :490
	public bool IsHumanPlayerCharacter => this == MainHero;           // :670
	public static Hero MainHero => CharacterObject.PlayerCharacter.HeroObject;            // :958
```

Other getters the new code reads (all pure, no writes): `MobileParty.MapEvent => Party.MapEvent;`
(`TaleWorlds.CampaignSystem.Party.MobileParty.cs:1102`), `PartyBase.MapEvent => _mapEventSide?.MapEvent;`
(`TaleWorlds.CampaignSystem.Party.PartyBase.cs:288`), `Army.DoesLeaderPartyAndAttachedPartiesContain`
(`TaleWorlds.CampaignSystem.Army.cs:267-274`, a comparison and an `IndexOf`), `Hero.Age` (`Hero.cs:474-488`,
reads birth and death days).

### Binding semantics (re-derived from the code and the engine above; this table is the spec)

Reducing the lost test with the engine identities: not lost iff `HeroState == Active`, or
`IsWanderer && HeroState == NotSpawned`. Then `IsTargetAvailableNow`'s `!IsAlive` and `IsPrisoner` are
always false on that path (Active and NotSpawned are neither Dead nor Prisoner), so the wait test reduces
to `!playerAvailable || IsChild || targetPartyInMapEvent`.

| Case | Send (`CanSendMessenger`) | Arrival (must stay exactly this) |
|---|---|---|
| Hero not found | `NullTarget` | `ResolveHero` null: removed silently, no notice (`:274-276`); `ResolveHero` keeps its `Hero.FindFirst` scan |
| Human player character | refused | not checked |
| Dead | `HeroDead` | Lost |
| Prisoner (any occupation) | `HeroPrisoner` | **Lost** (Prisoner is not Active and not NotSpawned); the prisoner wait in `IsTargetAvailableNow` is unreachable and stays unreachable in effect |
| Fugitive | `HeroFugitive` | Lost |
| Child, otherwise reachable | `HeroChild` | **Wait** |
| Non-wanderer in NotSpawned, Released, Traveling or Disabled | `TargetUnavailable` | Lost |
| Wanderer, NotSpawned | passes | reachable |
| Wanderer, Released, Traveling or Disabled | **passes** | **Lost** |
| Shares the main hero's party | `IsInPlayerParty` (false when either party is null) | raw `target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo`, **true when both are null**: Confused, removed |
| Player busy (`IsPlayerAvailable()` false) | n/a | Wait |
| Target's party in a map event | n/a | Wait |

Order: not found, then Lost, then Confused, then Wait, then Ready. Lost before Confused (a dead hero in the
player's party is Lost). Confused before Wait (a child in the player's party, or any shared party while the
player is busy, is Confused). `_processingArrivedMessenger` is set only on Ready.

### One accepted difference in evaluation order (not in outcome)

Today the party, player and map-event reads run only when the earlier branch did not return. After the
move, the behavior reads the snapshot and the three facts up front for every arrived messenger it handles.
Every one of these is a getter without side effects (quoted above), `Hero.MainHero` is already read by
`ReadSnapshot` and by the old confused test, and the hourly tick only runs inside a campaign. The up-front
`ReadSnapshot` also runs, for every arrived messenger and lost ones too, two reads the old arrival code never
made for a lost target: `Army.DoesLeaderPartyAndAttachedPartiesContain` (it reads `LeaderParty.AttachedParties`,
`Army.cs:267-274`) and `IsChild` (it reads `Campaign.Current.Models.AgeModel`, `Hero.cs:490`). Both are
side-effect free and the dialog path already runs them; the review probes this spot. No outcome
changes. If you find any of these reads writes state, STOP.

### Conventions that bind this change

- **ADR-002** (thin entry points): an entry point stays under 150 lines and delegates decisions to a
  service. Gate: `EntryPointLineCountGateTests`. This class is grandfathered in a shrink-only baseline.
- **ADR-007** (adapters): services take TAOM snapshots, plain values and adapters, never sealed TaleWorlds
  types such as `Hero`. Gate: `ServiceSignatureGateTests`. `ClassifyArrival` takes `HeroSnapshot` and bools.
- **ADR-008** (testability): services are unit-testable without the game; no static TaleWorlds calls in a
  service.
- `.claude/rules/csharp-architecture.md`: "Convert at boundary" (the reader converts `Hero.CharacterStates`
  to the TAOM enum), "Every decision path in a service or engine has a test, judged by the two-edit test".
- `.claude/rules/tests.md`: test names `MethodName_StateUnderTest_ExpectedBehavior`; MSTest and NSubstitute
  only; skip-guard exhaustion (one test per state that is skipped); the two-edit test.
- Existing pattern to model tests on: `TAOM.Tests/Features/Messengers/MessengerServiceTests.cs` (service built
  from four substitutes) and `TAOM.Tests/Features/Messengers/MessengerEntryPolicyTests.cs` (`[DataTestMethod]`
  rows).
- Lesson (`docs/reviews/lessons/testing-qa.md`, "A test that derives its own input set cannot fail on a
  deletion"): the characterization oracle below is a transcription of the OLD behavior code, an independent
  authority, never of the new service code.
- Lesson (`docs/reviews/lessons/testing-qa.md`, "Logic moved out of an untestable host to meet a line limit
  is testable: test it in the same change"): the moved rule is tested in this commit.
- No lessons heading names Messengers (`grep -n '^### ' docs/reviews/lessons/*.md | grep -i messenger`: no hit).

### Pins the change must keep green (re-read at the base)

- `TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorSourceTests.cs` (reads source with comments stripped):
  - `:60-62` the behavior contains `MessengerTargetReader.ReadSnapshot(` and `MessengerTargetReader.ReadQuoteInput(`,
    and must not contain `new HeroSnapshot(`; the mixin must not contain `HeroSnapshot`.
  - `:65` exactly one `GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, offer.Cost, false)`.
  - `:73-75` the three dialog methods call `GetOffer(hero, inPerson: true)` / `SendMessenger(hero, inPerson: true)`.
  - `:83` the reader holds exactly two `DoesLeaderPartyAndAttachedPartiesContain(` calls.
  - `:90-94` the `DialogCondition_CanSendAtQuote` body uses `offer.CanSend` and `offer.Cost`, no `Hero.MainHero?.Gold`.
  - `:102` the mixin calls `behavior.GetReason(offer, hero)` (so `GetReason` stays public on the behavior).
  - `:112-114` the reader keeps `FactionManager.IsAtWarAgainstFaction(hero?.MapFaction, Hero.MainHero?.MapFaction)`,
    `!hero.HasMet` and `IsUnderSiege`.
  - `:131-132` the reader must not contain `inPerson`; the behavior contains `_service.GetOffer(`.
  - `:22-51` the conversation recovery pins and `:140` the sent-notice DAYS line (untouched code).
- `TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs:191-206`: builds the behavior with six arguments
  (`Substitute.For<IMessengerService>(), store, settings, Substitute.For<IModLogger>(), Client(), Substitute.For<IMessengerConversationState>()`)
  and calls `OnHourlyTick()` on a co-op client, asserting `settings.EnableMessengers` and `store.Count` are never
  read. The constructor signature and the authority gate at `MessengerCampaignBehavior.cs:201` stay. This file is NOT edited.
- `TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs` (`[TestCategory("RequiresGame")]`): `:36` and
  `:47` read `Main/IoC.cs` and `Main/SubModule.cs` (neither changes); `:108-120` constructs the behavior with the
  six substitutes; `:123-134` pins `if (Campaign.Current?.MainParty == null) return false;` in the behavior
  (inside `IsPlayerAvailable`, which stays) and forbids `PartyBase.MainParty == null`.
- `TAOM.Tests/Features/Messengers/MessengerConversationStateTests.cs:23-45` and `MessengerEntryPolicyTests.cs:28-34`:
  source pins on the conversation start and settlement branch (untouched code).
- `TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs`: see Step 7.

### Blast radius

The graphify graph is absent in a fresh worktree (`python tools/graphify_taom.py status` at planning:
`graphify code graph: MISSING`), so the radius was taken with `git grep -l -w` over `*.cs *.xml *.xslt *.py *.sh *.json`:

- `MessengerCampaignBehavior`: `Client/Features/Messengers/UI/MessengerEncyclopediaMixin.cs` (`GetCampaignBehavior<MessengerCampaignBehavior>()`
  at `:66`, `:101`; calls `GetOffer`, `GetReason`, `SendMessenger`, all unchanged), `Main/Features/Messengers/MessengerIoC.cs:15`
  (registration), `Main/SubModule.cs:1400` (`AddBehavior`), comments only in `Main/Adapters/EncounterAdapter.cs:444`,
  `Main/Features/CultureConversion/Hooks/CultureConversionBehavior.cs:16`, `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:23`,
  `Main/Features/Messengers/Domain/MapCoord.cs`; tests listed under Pins; JSON audit records under `docs/audits`, `docs/raw`, `plans/_audit` (history).
- `MessengerService`: `MessengerIoC.cs`, `MessengerServiceTests.cs`, `MessengerCampaignBehaviorTests.cs`, one audit JSON.
- `MessengerTargetReader`: the behavior and `MessengerCampaignBehaviorSourceTests.cs` only.
- Messenger `HeroSnapshot` (`TAOM.Features.Messengers.Domain`): `IMessengerService.cs`, `MessengerService.cs`,
  `MessengerTargetReader.cs`, `MessengerServiceTests.cs`. (A different `HeroSnapshot` record lives in
  `Main/Features/CrashReport/Domain/CampaignSnapshot.cs:20`; it is unrelated, do not touch it.)

Optional: if `python tools/graphify_taom.py status` exits 0 in your worktree, also run
`python tools/graphify_taom.py affected "MessengerService" --depth 2` and the same for
`MessengerCampaignBehavior` and `HeroSnapshot`; a consumer outside the list above is a STOP.

## Commands you will need

Prefix every dotnet command with the `TEMP="<tmp>" TMP="<tmp>"` your dispatch rules give (create the
folder first, quote it). Both MSBuild flags go on build AND test. Never `./build.ps1`: it deploys into the game.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0`, `Skipped: 9`, Passed = 39250 plus the new tests |
| New tests | the same, plus `--filter "FullyQualifiedName~MessengerArrivalClassificationTests"` | the named tests run (a filter matching nothing proves nothing) |
| Messenger area | the same, plus `--filter "FullyQualifiedName~TAOM.Tests.Features.Messengers"` | all pass |
| Co-op pin | the same, plus `--filter "FullyQualifiedName~CoopAuthorityGateTests"` | all pass |
| Line gate | the same, plus `--filter "FullyQualifiedName~EntryPointLineCountGateTests"` | all pass (`Baseline_Regenerate_WhenAsked` reports skipped or inconclusive without the env var) |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Data | `python tools/validate_moduledata.py` | not required: no data file changes |
| No-game unit step | see Step 9 | `Failed: 0` |

## Scope

**In scope** (the only files you modify or create):
- `Main/Features/Messengers/Domain/MessengerHeroState.cs` (new)
- `Main/Features/Messengers/Domain/MessengerArrivalVerdict.cs` (new)
- `Main/Features/Messengers/Domain/HeroSnapshot.cs` (one optional trailing parameter and property)
- `Main/Features/Messengers/IMessengerService.cs` (one new member; needed because the behavior calls the
  service through `_service`, typed `IMessengerService`)
- `Main/Features/Messengers/MessengerService.cs` (`ClassifyArrival`)
- `Main/Features/Messengers/MessengerTargetReader.cs` (one argument added to the snapshot constructor call)
- `Main/Features/Messengers/MessengerCampaignBehavior.cs` (`HandleArrivedMessenger` body, delete
  `IsTargetAvailableNow`, one header comment phrase)
- `TAOM.Tests/Features/Messengers/MessengerArrivalClassificationTests.cs` (new)
- `TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorSourceTests.cs` (one new test method appended; no
  existing method edited)
- `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` (the one `MessengerCampaignBehavior` row,
  regenerated by the gate's own test, Step 7)
- `docs/features/messengers.md` (Step 8)

No existing test needs a signature edit: the snapshot parameter is optional and trailing, and the new
interface member is satisfied by the NSubstitute fakes.

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`: single-owner; nothing here needs them (new `.cs`
  files are picked up by the SDK-style project glob; if the build says otherwise, STOP and report).
- `Main/Features/Messengers/MessengerIoC.cs`: no registration changes.
- `MessengerService.CanSendMessenger` and every existing `MessengerServiceTests` method.
- `GetReason` on the behavior: it stays exactly as it is (public instance method, pinned by
  `MessengerCampaignBehaviorSourceTests.cs:102` and called by the client mixin). Moving its text map out is a
  possible follow-up, not this plan.
- The prisoner-on-arrival behaviour and the wanderer send/arrival mismatch: bug leads for the orchestrator.
- `StartMessengerConversation`, `ResolveHero`, `IsPlayerAvailable`, `NotifyMessengerLost`, `NotifyAccident`,
  the hourly loop, `SyncData`, the dialog tree.
- Any localized string. Every `{=taom_...}` literal stays byte-identical, including the existing em dashes
  inside the confused and recipient-gone texts.
- `TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs` (skip-list folder; read it, never edit it).
- `CHANGELOG.md`, `plans/README.md`, protected files (`.claude/settings.json`, `.claude/settings.local.json`,
  `Directory.Build.props`, `docs/adrs/*.md`).
- The gates themselves: never turn a gate green by editing `EntryPointLineCountGateTests.cs`, by
  hand-editing the baseline number, by deleting or `[Ignore]`-ing a test, or by loosening an assertion.

## Git workflow

- Commit on the branch you were given (a new branch for this plan, merged later by the maintainer); never
  push, merge or open a PR.
- One commit. Subject `refactor(messengers): <version> - Classify arrivals in the service`, where
  `<version>` is the `<Version value="...">` in `Main/_Module/SubModule.xml` when you commit (`v2.0.38` at
  planning, which gives 64 characters; at most 72). Stage explicit paths only (never `-A`, `-u`, `.`).
  Write the message to a file in your scratch folder and run `git commit -F "<file>"`. Never `--no-verify`.
- Body (the changelog entry, wrapped at 72, no AI attribution), a draft to check against your own diff:

  ```
  No change in game. The rule that decides what happens when a
  messenger reaches its target (lost, confused, wait for the player
  or the target, or ready to speak) moved from the campaign behavior
  into MessengerService.ClassifyArrival, which decides from the hero
  snapshot and three plain facts. The snapshot now carries the
  hero's engine state, and every combination of state, wanderer,
  child, shared party, battle and player availability is tested
  against the rule as it ran before the move. The behavior keeps
  only the engine reads and the notices.

  Not-tested: the boundary reads in HandleArrivedMessenger and
  MessengerTargetReader (the raw party equality, the map event,
  IsPlayerAvailable, the state cast) need a live campaign; a source
  pin holds their text and the enum parity test holds the cast.
  ```

## Steps

### Step 1: record the base

Run the drift check. Then run the build and the full test suite before any edit, each with its output
captured to a log in your scratch folder (`> "<scratch>/step1-build.log" 2>&1`; Step 5 compares against the
build log). Write the totals and failing tests into your report.

**Verify**: build exit 0 with `0 Error(s)` (note the warning count; base is 11); tests show
`Failed: 0, Passed: 39250, Skipped: 9, Total: 39259`, or the difference is explained (a different
machine or game install is a STOP: this plan's proof needs the game-backed run).

### Step 2: write the failing tests (RED by compile)

Create `TAOM.Tests/Features/Messengers/MessengerArrivalClassificationTests.cs` with this content (CRLF
like its neighbours is fine; the oracle `OldArrivalRule` is a line-for-line transcription of
`MessengerCampaignBehavior.cs:283-307` at `7087fa633`, re-check it against your own read of those lines):

```csharp
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.Messengers;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Tests.Features.Messengers;

/// <summary>
/// The arrival verdict, MessengerService.ClassifyArrival, held to the rule MessengerCampaignBehavior.HandleArrivedMessenger
/// executed before the move (7087fa633). That rule is the executed one, not its old comments: a prisoner is lost, not
/// deferred, and a wanderer that is Released, Traveling or Disabled is lost although the send check accepts it.
/// </summary>
[TestClass]
public class MessengerArrivalClassificationTests
{
    private static readonly MessengerHeroState[] AllStates = (MessengerHeroState[])Enum.GetValues(typeof(MessengerHeroState));
    private static readonly bool[] Both = { false, true };

    private MessengerService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new MessengerService(
            Substitute.For<IMessengerSettingsProvider>(),
            Substitute.For<IMessengerConfigProvider>(),
            Substitute.For<IMessengerStateStore>(),
            Substitute.For<IMessengerRandomSource>());
    }

    // Facts consistent with the engine for one state: IsDead, IsFugitive, IsPrisoner and IsActive read HeroState, and
    // IsAlive is !IsDead (v1.5.7 Hero.cs:303-319).
    private static HeroSnapshot Snap(MessengerHeroState state, bool wanderer = false, bool child = false, bool inPlayerParty = false) =>
        new HeroSnapshot("hero_1",
            isAlive: state != MessengerHeroState.Dead,
            isPrisoner: state == MessengerHeroState.Prisoner,
            isChild: child,
            isFugitive: state == MessengerHeroState.Fugitive,
            isActive: state == MessengerHeroState.Active,
            isWanderer: wanderer,
            isHumanPlayerCharacter: false,
            isInPlayerParty: inPlayerParty,
            heroState: state);

    private MessengerArrivalVerdict Classify(HeroSnapshot target, bool shares = false, bool playerAvailable = true, bool inMapEvent = false) =>
        _sut.ClassifyArrival(target, sharesMainHeroParty: shares, playerAvailable: playerAvailable, targetPartyInMapEvent: inMapEvent);

    // MessengerCampaignBehavior.cs:283-307 at 7087fa633, transcribed over the same engine identities.
    private static MessengerArrivalVerdict OldArrivalRule(
        MessengerHeroState state, bool wanderer, bool child, bool shares, bool playerAvailable, bool inMapEvent)
    {
        var isAlive = state != MessengerHeroState.Dead;
        var isActive = state == MessengerHeroState.Active;
        var isFugitive = state == MessengerHeroState.Fugitive;
        var isPrisoner = state == MessengerHeroState.Prisoner;
        if (!isAlive
            || state == MessengerHeroState.Disabled
            || isFugitive
            || (!isActive && !wanderer)
            || (!isActive && wanderer && state != MessengerHeroState.NotSpawned))
            return MessengerArrivalVerdict.Lost;
        if (shares)
            return MessengerArrivalVerdict.Confused;
        var targetAvailableNow = isAlive && !isPrisoner && !child && !inMapEvent;
        if (!playerAvailable || !targetAvailableNow)
            return MessengerArrivalVerdict.Wait;
        return MessengerArrivalVerdict.Ready;
    }

    [TestMethod]
    public void ClassifyArrival_EveryInputCombination_MatchesTheRuleTheBehaviorExecuted()
    {
        Assert.AreEqual(8, AllStates.Length, "MessengerHeroState must carry all eight engine states");
        var mismatches = new List<string>();
        var cells = 0;
        foreach (var state in AllStates)
        foreach (var wanderer in Both)
        foreach (var child in Both)
        foreach (var shares in Both)
        foreach (var playerAvailable in Both)
        foreach (var inMapEvent in Both)
        {
            cells++;
            var expected = OldArrivalRule(state, wanderer, child, shares, playerAvailable, inMapEvent);
            var actual = Classify(Snap(state, wanderer, child), shares, playerAvailable, inMapEvent);
            if (expected != actual)
                mismatches.Add($"{state} wanderer={wanderer} child={child} shares={shares} player={playerAvailable} battle={inMapEvent}: expected {expected}, got {actual}");
        }

        Assert.AreEqual(256, cells);
        Assert.AreEqual(0, mismatches.Count, string.Join(Environment.NewLine, mismatches));
    }

    [DataTestMethod]
    [DataRow(MessengerHeroState.NotSpawned, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Active, MessengerArrivalVerdict.Ready)]
    [DataRow(MessengerHeroState.Fugitive, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Prisoner, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Released, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Dead, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Disabled, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Traveling, MessengerArrivalVerdict.Lost)]
    public void ClassifyArrival_NonWandererInEachState_OnlyActiveIsReachable(MessengerHeroState state, MessengerArrivalVerdict expected)
        => Assert.AreEqual(expected, Classify(Snap(state)));

    [DataTestMethod]
    [DataRow(MessengerHeroState.NotSpawned, MessengerArrivalVerdict.Ready)]
    [DataRow(MessengerHeroState.Active, MessengerArrivalVerdict.Ready)]
    [DataRow(MessengerHeroState.Fugitive, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Prisoner, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Released, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Dead, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Disabled, MessengerArrivalVerdict.Lost)]
    [DataRow(MessengerHeroState.Traveling, MessengerArrivalVerdict.Lost)]
    public void ClassifyArrival_WandererInEachState_ActiveOrUnspawnedIsReachable(MessengerHeroState state, MessengerArrivalVerdict expected)
        => Assert.AreEqual(expected, Classify(Snap(state, wanderer: true)));

    [TestMethod]
    public void ClassifyArrival_Prisoner_IsLostNotDeferred()
    {
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Prisoner)));
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Prisoner, wanderer: true)));
        Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Prisoner), playerAvailable: false, inMapEvent: true));
    }

    [TestMethod]
    public void ClassifyArrival_DeadTargetSharingThePlayersParty_IsLostBeforeConfused()
        => Assert.AreEqual(MessengerArrivalVerdict.Lost, Classify(Snap(MessengerHeroState.Dead), shares: true));

    [TestMethod]
    public void ClassifyArrival_ReachableTargetSharingTheMainHerosParty_IsConfused()
        => Assert.AreEqual(MessengerArrivalVerdict.Confused, Classify(Snap(MessengerHeroState.Active), shares: true));

    [TestMethod]
    public void ClassifyArrival_SharedPartyWhileAChildThePlayerBusyAndABattle_IsConfusedBeforeWait()
        => Assert.AreEqual(MessengerArrivalVerdict.Confused,
            Classify(Snap(MessengerHeroState.Active, child: true), shares: true, playerAvailable: false, inMapEvent: true));

    [TestMethod]
    public void ClassifyArrival_UsesTheRawPartyEquality_NotTheSnapshotsIsInPlayerParty()
    {
        // Arrival's party test is true when both parties are null; IsInPlayerParty is false then, so it must not decide.
        Assert.AreEqual(MessengerArrivalVerdict.Ready, Classify(Snap(MessengerHeroState.Active, inPlayerParty: true), shares: false));
        Assert.AreEqual(MessengerArrivalVerdict.Confused, Classify(Snap(MessengerHeroState.Active, inPlayerParty: false), shares: true));
    }

    [TestMethod]
    public void ClassifyArrival_ReachableChild_Waits()
    {
        Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.Active, child: true)));
        Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.NotSpawned, wanderer: true, child: true)));
    }

    [TestMethod]
    public void ClassifyArrival_PlayerNotAvailable_Waits()
        => Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.Active), playerAvailable: false));

    [TestMethod]
    public void ClassifyArrival_TargetPartyInAMapEvent_Waits()
        => Assert.AreEqual(MessengerArrivalVerdict.Wait, Classify(Snap(MessengerHeroState.Active), inMapEvent: true));

    [TestMethod]
    public void ClassifyArrival_ActiveAdultPlayerFreeNoBattleNoSharedParty_IsReady()
        => Assert.AreEqual(MessengerArrivalVerdict.Ready, Classify(Snap(MessengerHeroState.Active)));

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void MessengerHeroState_MatchesTheEngineCharacterStates_ByNameAndValue()
    {
        // The reader casts Hero.CharacterStates to MessengerHeroState by value, so the two must match one to one.
        CollectionAssert.AreEqual(Enum.GetNames(typeof(Hero.CharacterStates)), Enum.GetNames(typeof(MessengerHeroState)));
        foreach (Hero.CharacterStates s in Enum.GetValues(typeof(Hero.CharacterStates)))
            Assert.AreEqual(s.ToString(), ((MessengerHeroState)(int)s).ToString());
    }
}
```

Append one test method to `TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorSourceTests.cs`, after
`TheSentNotice_StillPromisesTheTravelDaysCap_NotTheStraightLineEstimate` and before the class's closing
brace (the file already has `using System.Text.RegularExpressions;` and the `Path` constant):

```csharp
    [TestMethod]
    public void TheArrival_AsksTheServiceForItsVerdict_WithTheRawPartyEqualityAndTheEngineState()
    {
        var behavior = RepoPaths.ReadSource(Path, stripComments: true);
        var reader = RepoPaths.ReadSource("Main/Features/Messengers/MessengerTargetReader.cs", stripComments: true);
        var body = Regex.Match(behavior, @"ArrivedHandleOutcome HandleArrivedMessenger\([\s\S]*?\n    \}").Value;

        Assert.IsTrue(body.Length > 0, "HandleArrivedMessenger not found");
        StringAssert.Contains(body, "_service.ClassifyArrival(");
        StringAssert.Contains(body, "sharesMainHeroParty: target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo");
        Assert.IsFalse(body.Contains("IsInPlayerParty"), "arrival's party test is true when both parties are null; IsInPlayerParty is not");
        StringAssert.Contains(reader, "heroState: (MessengerHeroState)(int)hero.HeroState");
    }
```

**Verify**: `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` fails, and its errors include
CS0246 naming `MessengerHeroState` and CS0246 naming `MessengerArrivalVerdict` (CS0103 may also appear where
an enum name is used as a value), and CS1061 naming `ClassifyArrival` on `MessengerService`, all in
`MessengerArrivalClassificationTests.cs`. An error for the named argument `heroState` (CS1739) may be absent:
the compiler does not report overload resolution on a call whose other arguments already have errors, so its
absence is a cascade, not a failure. Any error in another file is a STOP. Quote the error lines (file, id and
symbol) in your report.

### Step 3: add the types and a throwing stub (RED by assertion)

1. Create `Main/Features/Messengers/Domain/MessengerHeroState.cs`:

   ```csharp
   namespace TAOM.Features.Messengers.Domain;

   /// <summary>
   /// The engine's Hero.CharacterStates, one to one, in the same order and with the same values (v1.5.7 Hero.cs:28-38).
   /// MessengerTargetReader casts by value; MessengerArrivalClassificationTests checks the two enums still match.
   /// </summary>
   public enum MessengerHeroState
   {
       NotSpawned,
       Active,
       Fugitive,
       Prisoner,
       Released,
       Dead,
       Disabled,
       Traveling,
   }
   ```

2. Create `Main/Features/Messengers/Domain/MessengerArrivalVerdict.cs`:

   ```csharp
   namespace TAOM.Features.Messengers.Domain;

   /// <summary>What the hourly tick does with a messenger that has reached its target, in the order the rule tests them.</summary>
   public enum MessengerArrivalVerdict
   {
       /// <summary>The target can no longer be reached: the "messenger lost" notice, then removal.</summary>
       Lost,
       /// <summary>The target shares the main hero's party: the "confused" notice, then removal.</summary>
       Confused,
       /// <summary>The player or the target is busy: try again next hour.</summary>
       Wait,
       /// <summary>The "messenger arrived" inquiry.</summary>
       Ready,
   }
   ```

3. `Main/Features/Messengers/Domain/HeroSnapshot.cs`: add the property `public MessengerHeroState HeroState { get; }`
   after `SameArmy`; add the trailing parameter `MessengerHeroState heroState = MessengerHeroState.Active` after
   `bool sameArmy = false`; assign `HeroState = heroState;` last in the constructor. Add a one-line comment on the
   parameter: the default exists only so the send-rule tests that predate it compile; `MessengerTargetReader`
   always passes the engine's state. (Both types share the `TAOM.Features.Messengers.Domain` namespace, so no
   `using` is needed.)

4. `Main/Features/Messengers/IMessengerService.cs`: add after `GetOffer`:

   ```csharp
       /// <summary>
       /// What to do with an arrived messenger. <paramref name="sharesMainHeroParty"/> is the raw party equality, true when
       /// both parties are null, not <see cref="HeroSnapshot.IsInPlayerParty"/>.
       /// </summary>
       MessengerArrivalVerdict ClassifyArrival(HeroSnapshot target, bool sharesMainHeroParty, bool playerAvailable, bool targetPartyInMapEvent);
   ```

5. `Main/Features/Messengers/MessengerService.cs`: add after `GetOffer` a stub
   `public MessengerArrivalVerdict ClassifyArrival(HeroSnapshot target, bool sharesMainHeroParty, bool playerAvailable, bool targetPartyInMapEvent) => throw new NotImplementedException();`
   (`using System;` is already at line 1).

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0. Then
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~MessengerArrivalClassificationTests"`
shows every `ClassifyArrival_*` test (including each data row) failing with `NotImplementedException`, and
`MessengerHeroState_MatchesTheEngineCharacterStates_ByNameAndValue` passing (it guards the enum, which now
exists). Also run `--filter "FullyQualifiedName~MessengerCampaignBehaviorSourceTests"`: only
`TheArrival_AsksTheServiceForItsVerdict_WithTheRawPartyEqualityAndTheEngineState` fails (assertion on
`_service.ClassifyArrival(`); every other pin passes. Record the failing and passing counts.

### Step 4: implement ClassifyArrival (GREEN for the service tests)

Replace the stub in `MessengerService.cs` with:

```csharp
    public MessengerArrivalVerdict ClassifyArrival(HeroSnapshot target, bool sharesMainHeroParty, bool playerAvailable, bool targetPartyInMapEvent)
    {
        // Reachable on arrival only while Active, or as a wanderer the engine has not spawned yet. Every other state is
        // lost, a prisoner included. The send check (CanSendMessenger) also accepts a wanderer that is Released, Traveling
        // or Disabled; arrival does not.
        var reachable = target.HeroState == MessengerHeroState.Active
                        || (target.IsWanderer && target.HeroState == MessengerHeroState.NotSpawned);
        if (!reachable)
            return MessengerArrivalVerdict.Lost;

        if (sharesMainHeroParty)
            return MessengerArrivalVerdict.Confused;

        if (!playerAvailable || target.IsChild || targetPartyInMapEvent)
            return MessengerArrivalVerdict.Wait;

        return MessengerArrivalVerdict.Ready;
    }
```

Re-check each comment line against the semantics table before you keep it.

**Verify**: build exits 0; the `MessengerArrivalClassificationTests` filter shows `Failed: 0` and every test
passed (the exhaustive test reports 256 cells, no mismatch). The source pin from Step 2 still fails (the
behavior is not changed yet); that is expected.

### Step 5: read the state in the reader and switch in the behavior

1. `MessengerTargetReader.cs`: change the last constructor argument line
   `                      && army.DoesLeaderPartyAndAttachedPartiesContain(hero.PartyBelongedTo));`
   to end with `,` instead of `);`, and add one line after it:
   `            heroState: (MessengerHeroState)(int)hero.HeroState);`
   The reader stays a pure field read; the count of `DoesLeaderPartyAndAttachedPartiesContain(` stays two.

2. `MessengerCampaignBehavior.cs`, four surgical Edits (never retype a localized string line):

   a. Replace lines 278 to 291 (from `        // Permanent unavailability classes` through the `}` that closes
      the lost block) with:

   ```csharp
           // The verdict is the service's (MessengerService.ClassifyArrival); these are the engine facts it needs. The
           // party test is the raw equality, which is true when both parties are null, so it is not IsInPlayerParty.
           var verdict = _service.ClassifyArrival(
               MessengerTargetReader.ReadSnapshot(target)!,
               sharesMainHeroParty: target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo,
               playerAvailable: IsPlayerAvailable(),
               targetPartyInMapEvent: target.PartyBelongedTo?.MapEvent != null);

           if (verdict == MessengerArrivalVerdict.Lost)
           {
               NotifyMessengerLost(messenger, target);
               return ArrivedHandleOutcome.RemoveFromList;
           }
   ```

   b. Replace the single line `        if (target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo)` (old `:293`;
      include the leading `if (` so the match is unique) with
      `        if (verdict == MessengerArrivalVerdict.Confused)`. The block body below it stays untouched.

   c. Replace old lines 304 to 307 (the two `// Temporary unavailability` comment lines, the
      `if (!IsPlayerAvailable() || !IsTargetAvailableNow(target))` line and its return) with:

   ```csharp
           if (verdict != MessengerArrivalVerdict.Ready)
               return ArrivedHandleOutcome.WaitForNextTick;
   ```

      (An unknown verdict waits rather than opening a conversation.)

   d. Delete `IsTargetAvailableNow` (old `:620-624`) and the blank line after it.

   e. Optional comment accuracy, header line 23: in `(validation, accident roll,` add `arrival verdict, ` after
      `validation, `. Touch no other header line (line 21 holds an existing em dash; leave it).

   The `!` after `ReadSnapshot(target)` is safe: `ReadSnapshot` returns null only for a null hero, and
   `target` is non-null after the early return at old `:275-276`. The null-forgiving form is already used in
   `Main` (for example `Main/Adapters/LostFootingRescueAdapter.cs:94`).

**Verify**:
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= > "<scratch>/step5-build.log" 2>&1`
  exits 0, and `grep -i "Messengers.*warning" "<scratch>/step5-build.log"` prints no line that the same grep over
  Step 1's build log did not already print (capture Step 1's build output to a log for this; a Messengers warning
  the base already had is not a failure).
- `dotnet test ... --filter "FullyQualifiedName~TAOM.Tests.Features.Messengers"`: `Failed: 0` (the new source
  pin now passes; every existing Messengers pin passes).
- `dotnet test ... --filter "FullyQualifiedName~CoopAuthorityGateTests"`: `Failed: 0`.
- `git grep -n IsTargetAvailableNow -- Main Client TAOM.Tests` prints nothing.
- `git diff 7087fa633 -- Main/Features/Messengers/MessengerCampaignBehavior.cs | grep -E "^[-+].*\{=taom_"`
  prints nothing (no localized line was added or removed).
- `git diff 7087fa633 -- Main/Features/Messengers/MessengerCampaignBehavior.cs` touches only
  `HandleArrivedMessenger`, `IsTargetAvailableNow` and (if done) header line 23.

### Step 6: prove the tests guard the rule (two mutations, restored by hand)

With the Edit tool (never git), make each mutation in `MessengerService.ClassifyArrival`, run the
`MessengerArrivalClassificationTests` filter, record the failing test names, then restore the exact
original text with Edit:

- M1: change `|| (target.IsWanderer && target.HeroState == MessengerHeroState.NotSpawned);` to
  `|| (target.IsWanderer && target.HeroState == MessengerHeroState.NotSpawned) || target.HeroState == MessengerHeroState.Prisoner;`.
  Expected failures include `ClassifyArrival_Prisoner_IsLostNotDeferred`, the exhaustive test and the Prisoner
  data rows.
- M2: move the `if (sharesMainHeroParty)` block below the Wait block. Expected failures include
  `ClassifyArrival_SharedPartyWhileAChildThePlayerBusyAndABattle_IsConfusedBeforeWait` and the exhaustive test.

**Verify**: after restoring, `git diff 7087fa633 -- Main/Features/Messengers/MessengerService.cs` shows exactly
the Step 4 method and no mutation text, and the filter is green again. If either mutation leaves every test
green, STOP: the tests do not guard the rule.

### Step 7: regenerate the one baseline row

`EntryPointLineCountGateTests.Compare` (`TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs:77-78`) fails a
listed class whose live count is below its number ("lines is below its baseline ... lower it (set
TAOM_WRITE_ENTRY_POINT_BASELINE=1 and run Baseline_Regenerate_WhenAsked)"). `Baseline_Regenerate_WhenAsked`
(`:100-125`) rewrites the file from the live counts, taking `Math.Min(live, old)` per listed class, and never adds
a row. Run it with the variable set for that one process only:

`TAOM_WRITE_ENTRY_POINT_BASELINE=1 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EntryPointLineCountGateTests.Baseline_Regenerate_WhenAsked"`

**Verify**:
- `git diff -- TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` shows exactly one removed line
  `TAOM.Features.Messengers.MessengerCampaignBehavior 668` and one added line
  `TAOM.Features.Messengers.MessengerCampaignBehavior 659`, and nothing else (header, `# Entries: 18.` and the
  other 17 rows unchanged). 659 is 668 minus 9: Step 5a replaces 14 lines with 13, 5c replaces 4 with 2, 5d
  deletes 6, and 5e changes no line count. Any other number means an edit lost or duplicated a line: compare
  your diff with Step 5 and report the difference. A whole-file diff (line endings) or any other row changing
  is a STOP.
- Without the variable, `--filter "FullyQualifiedName~EntryPointLineCountGateTests"` shows `Failed: 0`.

### Step 8: update the feature doc

`docs/features/messengers.md` (CRLF; edit with the Edit tool; add no em or en dash; the file's existing dashes
and arrows on lines you do not rewrite stay):

- Line 30, the `MessengerService` bullet: insert right after `(quote, verdict against that quote, days),` (the
  parenthetical that belongs to the `GetOffer` item, so the two stay together) and before `` `QuoteCost ``, add
  `` `ClassifyArrival(HeroSnapshot, sharesMainHeroParty, playerAvailable, targetPartyInMapEvent) → MessengerArrivalVerdict` (lost, confused, wait or ready for an arrived messenger; the hourly tick switches on it), ``
  keeping the line's existing arrow style for consistency.
- Line 121, the `HeroSnapshot.cs` row: replace `Boundary POCO (11 hero properties) for validation` with
  `Boundary POCO (12 hero properties, the engine hero state among them) for the send check and the arrival verdict`
  (count the properties in your final `HeroSnapshot.cs`; 12 at this plan's end).
- After line 121 (the `HeroSnapshot.cs` row) and before line 122 (the `MessengerValidationResult.cs` row), add
  two rows, keeping the alphabetical order of these Domain rows:
  `| [Main/Features/Messengers/Domain/MessengerArrivalVerdict.cs](../../Main/Features/Messengers/Domain/MessengerArrivalVerdict.cs) | 4-value enum: what the hourly tick does with an arrived messenger |`
  and
  `| [Main/Features/Messengers/Domain/MessengerHeroState.cs](../../Main/Features/Messengers/Domain/MessengerHeroState.cs) | The engine's eight hero states, one to one, read by MessengerTargetReader |`.
- After line 210 (the `MessengerServiceTests.cs` bullet), add:
  `- [TAOM.Tests/Features/Messengers/MessengerArrivalClassificationTests.cs](../../TAOM.Tests/Features/Messengers/MessengerArrivalClassificationTests.cs): the arrival verdict over every hero state, wanderer or not, child or not, shared party, battle and player availability, checked against the rule as the behavior ran it; a prisoner is lost on arrival; the enum matches the engine's states.`

**Verify**: `python tools/lint_docs.py --fail-on-drift` exits 0, and
`git diff -- docs/features/messengers.md | grep -P "^\+.*[\x{2013}\x{2014}]"` prints nothing.

### Step 9: full verification

1. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: exit 0, `0 Error(s)`, warning count
   not above the Step 1 count.
2. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: `Failed: 0`, `Skipped: 9`, and Passed equals
   the Step 1 Passed plus the number of results the `MessengerArrivalClassificationTests` filter reports plus 1 (the
   new source pin). Quote the totals line.
3. Last, the no-game unit step from `.ai/verification.md`, limited to the new tests (it rebuilds
   `TAOM.Tests/bin/Debug` against BUTR reference assemblies, so run nothing game-backed after it without first
   running `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --no-incremental`):
   - `dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=`
   - `dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~MessengerArrivalClassificationTests&TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"`

   Expected: `Failed: 0` and a Passed count equal to the game-backed filter's count minus 1 (the parity test is
   `RequiresGame` and is left out). If the RefAsm restore or build fails for an environment reason (network,
   package feed), record "RefAsm step NOT RUN" with the exact error and continue to Step 10; do not fix the
   environment. A test failure here is a STOP.

### Step 10: commit

Stage exactly the in-scope paths that changed (list them with `git status --porcelain` first; every entry must
be in Scope), write the message from "Git workflow" to a file, re-check its claims against your diff, and run
`git commit -F "<file>"`.

**Verify**: `git log -1 --stat` lists only in-scope files; `git status --porcelain` is empty.

## Test plan

- New file `TAOM.Tests/Features/Messengers/MessengerArrivalClassificationTests.cs`:
  - one exhaustive characterization test over all 256 cells (8 states x wanderer x child x shared party x
    battle x player available) against a transcription of the old behavior code;
  - two data-driven tests, one row per state, for non-wanderers and wanderers (16 cells);
  - named branch tests: prisoner lost (three cases), lost before confused, confused, confused before wait,
    raw party equality not `IsInPlayerParty`, reachable child waits, player busy waits, target in a battle
    waits, ready;
  - one `RequiresGame` parity test: `MessengerHeroState` matches `Hero.CharacterStates` by name and value.
- One new source pin in `MessengerCampaignBehaviorSourceTests.cs` for the boundary text no unit test can reach:
  the service call, the raw party equality, no `IsInPlayerParty` in the handler, the reader's state cast.
- Pattern: `MessengerServiceTests.cs` (service from substitutes), `MessengerEntryPolicyTests.cs` (data rows).
- Not testable without a live campaign (for the `Not-tested:` trailer): the engine reads in
  `HandleArrivedMessenger` (`target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo`,
  `target.PartyBelongedTo?.MapEvent != null`, `IsPlayerAvailable()`) and the reader's
  `(MessengerHeroState)(int)hero.HeroState`. Only those lines.
- The deletion of `IsTargetAvailableNow` has no permanent absence test; Step 5's grep is its evidence in the report.
- No in-game check is owed: the decision moved, the engine reads did not change, and the exhaustive test holds
  the outcome for every input.

## Done criteria

ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with `0 Error(s)`
- [ ] The full `dotnet test` shows `Failed: 0`, `Skipped: 9`, Passed = base Passed + the new results
- [ ] The `MessengerArrivalClassificationTests` filter shows every test passed, with RED quoted from Steps 2
      and 3 and the two mutation results from Step 6 in the report
- [ ] `git grep -n IsTargetAvailableNow -- Main Client TAOM.Tests` returns nothing
- [ ] `git grep -n "ClassifyArrival(" -- Main` prints exactly three lines: one in `IMessengerService.cs`, one in
      `MessengerService.cs` and one in `MessengerCampaignBehavior.cs` (the `_service.ClassifyArrival(` call; the
      Step 5a comment names `MessengerService.ClassifyArrival)` with no `(` after the name, so it does not match)
- [ ] `git diff 7087fa633 -- Main/Features/Messengers/MessengerCampaignBehavior.cs | grep -E "^[-+].*\{=taom_"` returns nothing
- [ ] `git diff 7087fa633 -- TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` changes exactly the one
      `MessengerCampaignBehavior` row, from 668 to 659
- [ ] `git diff 7087fa633 -- TAOM.Tests/Features/Messengers/MessengerServiceTests.cs TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj Client`
      is empty
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0
- [ ] The RefAsm unit step for the new tests shows `Failed: 0`, or is recorded NOT RUN with an environment error
- [ ] `git status --porcelain` is empty after the commit, and the commit lists only in-scope files
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it describes

## STOP conditions

Stop and report (do not improvise) if:

- Any "Current state" excerpt does not match the code at your HEAD (line numbers drifting a few lines with the
  same text is fine; different text is not).
- The engine's `Hero.CharacterStates` or the state getters differ from the quoted v1.5.7 lines (re-run
  `pwsh tools/taom-src.ps1 path Hero` only to compare; never decompile and adapt), or the parity test fails.
- Any existing Messenger, co-op or line-gate test fails after your change for any reason (no existing test
  needs a signature edit in this plan).
- The exhaustive test shows a mismatch you can only clear by changing the oracle, or you feel the need to
  reorder the branches, or to make a prisoner wait, or to align the wanderer send and arrival rules: those are
  behaviour changes.
- Any of the getters this plan reads up front turns out to write state.
- A mutation in Step 6 leaves every test green.
- The baseline regeneration changes more than the one row, or the whole file.
- The work seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `MessengerIoC.cs`, a protected
  file, or `CoopAuthorityGateTests.cs`.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (no issue exists for this refactor).
- `/localize`: none; no player-facing text is added or changed.
- `docs/features/messengers.md` is updated by the executor (Step 8); the feature-map row needs no change.
- Raise separately, as bug leads (not for this executor): a messenger that arrives while its target is a prisoner
  is dropped as lost although the old comment intended a wait; an inactive wanderer that is Released, Traveling or
  Disabled can be sent a paid messenger that is then always lost on arrival.
- `/deep-review` of the commit before merge (C# change).

## After merge: the maintainer's actions

None beyond pulling. No save format, settings, hooks or data change.

## Maintenance notes

- A fix for either bug lead now changes one line in `ClassifyArrival` and the matching oracle rows in
  `MessengerArrivalClassificationTests` (the oracle documents the pre-move rule; such a fix replaces it with the
  new intended table, with the reason in its commit body).
- An engine bump that adds or reorders `Hero.CharacterStates` fails the parity test; update `MessengerHeroState`
  and re-derive the reachable states.
- Review probes: the raw party equality stays in the behavior (true when both parties are null); the handler
  never reads `IsInPlayerParty`; `_processingArrivedMessenger` is still set only after the Ready check; the
  localized lines are untouched; the snapshot read runs only after the null check; the up-front `ReadSnapshot`
  now runs the army and `IsChild` reads for every arrived messenger, lost ones included, so check they cannot
  throw in the hourly tick (an army with a null leader party, a campaign without its age model).
- Deferred: moving `GetReason`'s text map into a static mapper (it must stay a public instance method on the
  behavior for the client mixin and `MessengerCampaignBehaviorSourceTests.cs:102`); a further ADR-002 shrink of
  this entry point (the conversation routing is the bulk of what remains).
