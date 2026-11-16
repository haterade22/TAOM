# RCA: review of PR #1308, the battle shader notice

PR #1308 (REDACTEDSTUFF, head 082ef924b) shows the startup shader window and a loading-screen line when a mission
load has 50 or more shaders queued. It adds a postfix on `MissionState.OnTick` and a `[ClientOnly]` postfix on
`LoadingWindowViewModel.Update`, and gives the shared shader window a Startup and a Battle mode. The review ran at
tier 1 (a mission tick, a native call, a new thread): lens 5 (data flow) and lens 2 (engine compatibility), each at
the agent definition's `max`. Codex did not run: an outside PR needs Mike's word for each paid pass.

Lens 2 found nothing incompatible: both targets bind, and they run on the frames the PR describes. Seven engine items
stay UNVERIFIED, all native or OS behaviour: where the frame blocks, what the counter counts, whether the line draws
before the stall, and how a window on another thread behaves while the game thread stalls. The in-game check owns them.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The patch category `Patch153_BattleShaderNotice` collided with PR #1306's `Patch153_MountReinsGuard`, written the same day (session) | Number collision | Two PRs from one base each took the next free number | Fixed: renamed to `Patch155` in 11 places. `PatchCategoryNumberTests` catches a duplicate only once both are on one tree |
| 2 | LOW | The 3 new English keys had no row in the 12 language files (session, test run) | Seed skipped | Trunk was already red on that test | Fixed: seeded after the trunk merge. Existing lesson `localization-ui.md`, "Deferring the paid translation never defers the free seed" |
| 3 | LOW (player-facing) | The notice fires on every mission load (town, tavern, conversation), but all three texts say "this battle" (lens 5 F1, lens 2 F1) | Scope from the test case | The measurements came from battles only | Fixed: the texts say "scene", before any language row exists. Lesson appended to `localization-ui.md` |
| 4 | LOW | The hook read the native shader count before the service's dedicated-server check, so a server made one native read per mission load (lens 5 F4, lens 2 F2) | Gate order | The server check sat in the service, after the read the hook makes | Fixed: the hook asks `IsDedicatedServer` (read once, cached) first. Test `IsDedicatedServer_ReadsTheProviderOnce_SoTheHookSkipsTheNativeCountOnAServer` |
| 5 | LOW | The runner stored the run before the thread started, so one failed start blocked the window for the rest of the session (lens 5 F3) | Latch before success | The happy path cannot show it | Fixed: the run is stored after `Start` |
| 6 | LOW | The doc said the engine reports the shader count "only to the game thread"; in native it is one field read with no thread check (lens 2 F3) | Engine claim from intent | The design reason was written as an engine fact | Fixed: the sentence now gives TAOM's reason |
| 7 | LOW | The loading-screen line has one closer, the first mission frame after the wait (lens 5 F2) | Single closer | Lens 5 found no engine path that ends the wait another way | APPLIED after the merge on Mike's word (2026-11-15): the writer also requires the engine's render wait, as `harmony-il.md` (the stall-probe lesson) asks. Documented under "Limits" |
| 8 | LOW | The parked Patch21 writes the same `DescriptionText` (lens 5 F5) | Latent conflict | Patch21 is parked | Documented under "Limits". The re-enable note in `SubModule.cs` is single-owner; FOLLOW-UP no issue: add one line there when Patch21 returns |
| 9 | LOW | The log said "notice shown" when the gate started, before the window's delay; the registry said `OnTick` runs on every frame of every mission (lens 5 doc points) | Wording | Small inaccuracies | Fixed: "notice started"; the registry names the active state and `OnIdleTick` |

## Why each lens caught or missed these

- Both lenses found findings 3 and 4 on their own, which makes them the most certain.
- Lens 2 alone found finding 6: it needs the native decompile.
- Lens 5 alone found findings 5, 7, 8 and 9 by tracing each latch and writer.
- The session found findings 1 and 2 when it reviewed the four PRs side by side and ran the tests.

## In-game checks owed

`battle-shader-notice.md` "How to verify in game": a cold-cache battle load and a cold-cache town load.
