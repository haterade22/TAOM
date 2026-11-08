# Codex pre-review: Fords of War fades every riderless mount (2026-11-07)

Your role is reviewer ([.ai/roles/reviewer.md](../../.ai/roles/reviewer.md)). This is a review only:

- Do not edit, create or delete any file. Do not stage, commit or stash.
- Do not run `dotnet build`, `dotnet test` or `build.ps1`. A Bannerlord match runs on this machine and needs the CPU. The orchestrator ran the Fords, gate, architecture and warg tests after the change: 2,584 passed, 0 failed, 5 skipped; `python -m unittest tools.tests.test_fords_match_review`: 40 passed.
- Read files, `git diff`, `git status`, and the installed engine's decompiled sources: `pwsh tools/taom-src.ps1 path <Type>` (cache `C:\Users\mikew\.taom-src\v1.5.6-202611041234`). Look up one type at a time. Native: `python tools/native_decompile.py` only if you must.

## The request (Mike, 2026-11-07)

He saw a war elephant with no rider running around a Fords match and asked whether it is removed. It was not: the sweep faded only horses and wargs. His word: "it needs to include all creatures, dire wolves, spiders, elephants, mumakil, horses, wargs, fell wargs, etc".

## The change (worktree `E:/repos/taom-fords-creatures`, branch `fix/fords-riderless-creatures`, base `6791dbc75`, uncommitted)

- `Main/Features/FordsOfWar/Domain/RiderlessMountRule.cs`: `IsSwept`, `Horse`, `Wargs` and `IsWarg` are deleted; `WaitSeconds` (20 s) and `MayFade` stay.
- `Main/Adapters/FordsBattlefieldAdapter.cs` `RemoveRiderlessMounts`: every entry of `Mission.MountsWithoutRiders` that is active, has no rider and stood empty 20 s is faded with `Agent.FadeOut(false, false)` unless a human agent holds it (`MountAgent`) or has chosen it (`GetSelectedMountIndex`). The `[Housekeep]` line now counts `mountsRemoved` and `mountsSpared` per Monster id.
- `tools/fords_match_review.py` reads the new `mountsRemoved` field as `mounts_faded` and still reads the old line.
- Tests and docs: `RiderlessMountRuleTests`, `tools/tests/test_fords_match_review.py`, `docs/features/fords-of-war.md` (Housekeeping), `docs/features/dire-wolf.md`, `docs/reference/lotrlome-dire-wolf-changes.md`.

## Questions (answer each with file:line evidence; missing engine evidence is UNVERIFIED)

1. **Crewed beasts.** A war elephant with a howdah (`Main/Features/Elephant/TaomHowdahMachine.cs`, `HowdahCrewSpawner.cs`) and a mumakil with its platform (`Main/Features/Mumakil/TaomMumakilPlatform.cs`, `MumakilCrewSpawner.cs`) carry crew agents that are not riders. When such a beast is faded instead of killed: do the platform and howdah release the seats (their "gone" path tests `IsActive()` and `AgentSlotIdentity.IsCurrentOccupant`), and when, given that `FadeOut` first routs the agent? Can a crew agent be left on a deck that no longer follows anything, or inside the beast's capsule? Is a crew agent ever counted as holding the beast (`MountAgent`) or choosing it?
2. **Other TAOM code that holds a creature mount.** Search for mission behaviours, behaviour trees and services that keep a reference to a mount agent (elephant-like attacks, spiders, wargs, trolls if mounted, creature bandits, Fords' own creature and beast code, the riderless rage of wargs). Does any of them break, throw or act on a recycled slot when the mount is faded rather than killed (no `OnAgentRemoved` with `Killed`, state `Routed` then removal)?
3. **The native crash at RVA `0x6B716E`** (see `docs/features/fords-of-war.md` Known limitations): the doc says a fade routs first and the rout resets every soldier's target and flag. Does fading many more mounts, large ones included, change that reasoning?
4. **Fords' own accounting.** Does anything in Fords (bounty, kill credit, beast offers and their alive count, Harad's beast-call timer, the creature call cooldown, the HUD, the run report, `FordsAgentRegistry`) expect a beast to die and not fade, so that a faded beast is still counted as alive or never frees its slot?
5. **The guard.** Does `GetSelectedMountIndex` and `MountAgent` on every active human cover a soldier who is about to mount a riderless elephant or mumakil? Is there any other way an agent links to a mount the fade would leave dangling?
6. **The tool.** Is the parse in `tools/fords_match_review.py` right for both line formats?
7. Anything else in the diff that is wrong.

## Output

Per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), location, failure scenario, evidence. Then a one-line verdict per question.
