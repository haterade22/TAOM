# RCA — SpecialResources cheat command (`taom.add_special_resources`), 2026-07-30

Five deep-review agents over a 6-file changeset (new console command + `ISpecialResourceService.GrantAmount` + tests). Standards PASS, compatibility 9 verified / 0 incompatible, completeness COMPLETE bar the issue. Two defects confirmed and fixed in-session; two items accepted as documented trade-offs; one engine question unresolved by design.

Neither defect was a crash or a data-corruption bug. Both were **reporting** defects — the command would have told the player something false about what it did. For a debug tool whose whole purpose is showing you the state, that is the failure mode that matters.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | Clamp report keyed on the *sign of the request* (`After >= Cap && amount > 0f`), not on whether a clamp fired. A save whose balance predates a lowered cap clamps on a NEGATIVE grant too (550 − 10 → 500, not 540) and reported "(cap 500)" as if nothing was clamped. The floor-at-0 was never reported at all. | Boundary reporting | The tests asserted the *balance*, never the *message*. Every test case had `Before <= Cap`, so the over-cap-legacy state — the only state where sign and clamp disagree — was never constructed. The message was also stranded in a `Campaign.Current`-dependent static, and "untestable" was accepted rather than fixed. | Fixed: report from the unclamped natural result (`Before + amount`) with a distinct floor-at-0 branch, and extracted `SpecialResourceCheats.FormatResult` as `internal static` so all six branches are covered by `SpecialResourceCheatsFormatTests` without a running campaign. Lesson below. |
| 2 | LOW | New `LogInfo` used bare `{before}`/`{after}`/`{Cap}` where all six sibling earn-path logs use `:F0`, so cheat lines would print `120.00001` beside `120` from a battle. | Convention drift | Wrote the log line from scratch instead of reading a sibling first. No linter covers format-specifier consistency. | Fixed: matched the `[SpecRes] EVENT: ±N Resource \| before→after` shape used by the six earn paths. |
| 3 | GAP | No test exercised `GrantAmount` while a party-screen session was open (`_inSession = true`, `_pendingSpend > 0`) — the highest-risk interaction in the feature, since the cheat writes storage directly and the session debit is deferred. | Test coverage | The new tests were written against the *method*, not against the *feature's state machine*. Session state is a service-level concern the method silently participates in. | Fixed: added `GrantAmount_DuringOpenPartyScreenSession_FoldsIntoBalanceAndCommitDebitsOnce`. Behavior was already correct — `CommitSession` debits with a relative `Add(-pending)`, never an absolute `Set` — but nothing pinned it. |
| 4 | LOW | `Delegate.CreateDelegate` inside the engine's discovery loop is unguarded, so a TAOM method with the attribute and a wrong signature aborts discovery for *every* command in the pass, including vanilla's `campaign.*` cheats. | Engine binding | Not a defect in the shipped code — surfaced by the compatibility agent reading the engine loop. Nothing pinned the shape against a future refactor. | Added `ConsoleCommandBindingTests` — asserts every attributed TAOM method is static, returns `string`, takes exactly `List<string>`, and survives the engine's own `CreateDelegate` call. |

### Accepted, not fixed

- **Cap-timing during an open session.** `AddCapped` clamps against the raw stored balance, which is temporarily inflated by `_pendingSpend` while a party-screen session is open. This is pre-existing behavior shared by all six `EarnFrom*` paths, not introduced here; changing it would change the earn economy, not just the cheat.
- **`ResourceGrantResult.ResourceId` is not read by the console formatter** (only `DisplayName` is shown). Kept deliberately: it is the identity of the thing acted on, it is asserted in tests as a guard against resolve regressions, and a result type that cannot say *which* resource it touched is ambiguous for any second caller. This is a judgment call against `simplicity-criterion.md`'s dead-field bias, recorded here so it can be revisited.
- **`Hero.MainHero` can NRE inside its own getter** (`(Game.Current.PlayerTroop as CharacterObject).HeroObject`, no `?.` in the chain), which makes the `if (hero == null)` check partly dead code. `CampaignCheats.CheckCheatUsage` runs first and proves both `Game.Current` and `Campaign.Current` are non-null, so the window is narrow. Left as-is; noted for anyone touching the file.

### Unresolved by design

**When does `CollectCommandLineFunctions` run relative to module assembly load?** The compatibility agent verified the discovery contract fully — assembly filter, method shape, delegate construction — but the call *site* is in `TaleWorlds.Native.dll` and outside ILSpy's reach. Verdict is [Likely] after module load, by analogy to sibling `[EngineCallback]` methods in the same class (`GetScriptComponentClassNames`, `CreateScriptComponentInstance`) whose entire purpose is discovering mod-added types. Not proven.

**The settling check is in-game and cheap:** open the console at the main menu, before loading a campaign, and type `taom.add_special_resources`. Discovery is proven by *which* error comes back — `"Campaign was not started."` means the command was found and `CheckCheatUsage` rejected it; `"Could not find the command taom.add_special_resources"` means discovery never saw the TAOM assembly. Until that is run, this feature is unverified in the only way that counts.

## Guards verified RED before acceptance

Every guard here was authored *after* the defect it covers, so each passed on first run and proved nothing (`lessons/testing-qa.md`: "a guard never seen failing is not a guard"). All four were verified by injecting their defect simultaneously and confirming each named the right failure:

| Injected defect | Test that fired |
|---|---|
| Reverted `FormatResult` to the `amount > 0f` clamp test | `FormatResult_NegativeGrantOnLegacyOverCapBalance_ReportsClamp`, `FormatResult_NegativeGrantBelowZero_ReportsFloor` |
| Added an attributed command with a `string` parameter | `ConsoleCommands_AllAttributedMethods_MatchEngineDelegateShape` |
| `GrantAmount` early-returns while `_inSession` | `GrantAmount_DuringOpenPartyScreenSession_FoldsIntoBalanceAndCommitDebitsOnce` |

Injection reverted; suite back to 4505 passed / 0 failed.

## Root-cause pattern

Findings 1 and 3 share one shape: **the tests asserted the state change and ignored everything else the method produces.** Finding 1's message and finding 3's session interaction were both outside the frame of "did the number change correctly." The balance arithmetic was right in every case — what was wrong, or untested, was what the code *said* about it and what else was in flight while it said so.

## Why each agent missed what it missed

- **Standards (Agent 1)** — correctly out of scope. Message formatting and session state are not ADR concerns.
- **Compatibility (Agent 2)** — found #4 by reading the engine's discovery loop rather than just confirming the attribute exists. This is the agent working as intended: it went one level past "does the API exist" to "what does the engine do with it."
- **Efficiency (Agent 3)** — found #2. Also correctly refused to rate the `IoC.Resolve` in a once-per-invocation console command as a hot-path problem, and verified the custom numeric format string was valid rather than flagging it on suspicion.
- **Completeness (Agent 4)** — passed the test file as comprehensive. It checked that edge cases *existed* (cap, floor, NaN, unresolved) without asking whether the assertions covered the method's whole output. Coverage breadth was measured by case count, not by output surface.
- **Data Flow (Agent 5)** — found #1 and #3, and correctly disproved a premise in its own brief (AlignmentDesertion is not a consumer of this service). This remains the highest-value agent.

## Lesson to codify

**When a method returns a report as well as a state change, test the report.** A test that asserts only the mutation passes while the message lies. Every branch of a user-facing result string needs a case that constructs the state it describes — including states the happy path cannot reach (here: a stored balance above a cap that was lowered after the save was written).

Appended to `docs/reviews/lessons/testing-qa.md`.
