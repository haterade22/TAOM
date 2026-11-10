# RCA: emissary party limit and co-op upgrade charge (#1108, #967 C2, `/deep-review`, 2026-11-09)

## Top-line

The change caps a gold-only emissary sale at the party's room for that troop (#1108) and adds Patch144, which charges `upgrade_cost` on the co-op server for a guest's party-screen upgrades (#967 C2). Before the review, my own tests found one runtime defect: the first party cap counted every troop as one slot, while Troop Weight counts a heavy troop as its weight (row 1). The tier 1 review then ran lenses 5 Data flow, 2 Engine compatibility and 7 XML, and one Codex `ultra` pass. No reviewer found a CRITICAL, a HIGH or an engine incompatibility. Lens 2 found one MEDIUM, a missing binding gate for the Coop target (row 2). Codex confirmed no new finding and disputed all seven Known Suspects. Every confirmed finding is fixed in the same change.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The first party cap was `PartySizeLimit - NumberOfAllMembers`, one troop per slot. Under Troop Weight the main party's limit is already deflated by its weight surplus (`TroopWeightService.ApplyPartySizeWeightPenalty`), so a free slot is a weighted slot. With 10 free slots the emissary offered 10 weight-3 elites, and the party then stood 20 slots over its limit. | Missing gate (a TAOM writer of the limit) | I read the engine's two numbers and not the TAOM model that writes the limit. The weights live in another feature's XML. | Self-found before the review: four tests were RED first, then `GetPartySlotsPerTroop` and `RoomFor` (room = free slots / weight, rounded down). Lesson in `gamemodels-services.md`. |
| 2 | MEDIUM | Lens 2: no test pinned the Coop target's shape, and the postfix reads `__0` and `__1` by position. `HarmonyPatchBindingTests` passes a `ThirdPartyTargetPatches` class whenever `Prepare` returns false, which is always in a test host without Coop. A Coop change to the parameters that kept the name would pass every test. | Missing binding gate | I took the `ThirdPartyTargetPatches` row for a shape gate. It only proves that `Prepare` fails closed. | New `CoopMissionBindingTests` row (LiveInstall): it loads the installed `GameInterface.dll`, compares the target's parameter types with the postfix's, and checks that the patch's own reflection finds the same method. Lesson in `harmony-il.md`. |
| 3 | LOW | Lens 2 and lens 5: `harmony-patch-registry.md` said the Coop handler requires `IsSenderControllingHero`. Only Coop source from f3a075369 has it; the installed `GameInterface.dll` (file date 2026-10-29) does not. | Claim without reading the producer | I read the Coop source checkout, not the installed build. AGENTS.md already says signatures come from installed DLLs only. | Registry text now names both builds and what each means for the payer. Covered by the `harmony-il.md` lesson for row 2. |
| 4 | LOW | Lens 2: the registry and `special-resources.md` said the server "applies the upgrades" in `ApplyUpgradedTroopHistory`. `TryApplyTroopRosterDeltas` changes the roster; the method only grants the leader's upgrade XP. The registry also said "prisoner effects" for prisoner recruitment effects. | Unchecked generalisation | I named the method after its role in Coop's flow, not after its body. | Corrected in both docs and in the patch's class doc. One-off. |
| 5 | LOW | Lens 2, UNVERIFIED: `SpecialResourceEventAdapter.Describe` compared with `Hero.MainHero`, which reads `Game.Current.PlayerTroop` unguarded. Coop's source says `Hero.MainHero` is null on a dedicated server, where Patch144 calls `DescribeHero`. If so, every server upgrade logged a read failure and charged nothing. | Missing null guard (a computed campaign static) | `Describe` was written for earning paths that run where a main hero exists. I reused it on a new path without re-reading its reads. `adapters.md` already names this getter. | `Describe` reads `(Game.Current?.PlayerTroop as CharacterObject)?.HeroObject`. One-off: the rule exists. |
| 6 | LOW | Lens 7: the party-full text said "no room for that many troops". Players see it mostly as the hint on a greyed offer, before they pick a quantity. | Convention inconsistency (text for one of its two uses) | I wrote it for the `PartyFull` purchase result, which the picker makes rare. | Now "Your party has no room for these troops." in the C# literal and all 13 rows, while the 12 seeds still equal the English. One-off. |
| 7 | LOW | Lens 7, UNVERIFIED until Codex S1: `967.toml` read only `taom-ds` logs. Codex quoted an in-process route in the installed Coop: without Steam, the game process starts as the server, and that process writes the `taom` log. | Incomplete sweep | I traced the dedicated-server route only. | `logs = ["taom-ds", "taom"]`. One-off. |
| 8 | LOW | Lens 7: the `967.toml` header asked for `--ds-data`, which adds only the `ds` and `coop-server` kinds, and this manifest reads neither. | Convention inconsistency | I copied the header from `981.toml`, which reads `ds`. | Header names `--ds-engine` only. One-off. |

## Why each agent missed these

- **Row 1** was found before the review, by a test I wrote for a weight-3 troop. No reviewer saw the first version.
- **Lens 2 Engine compatibility** found rows 2 to 5. Rows 6 to 8 are outside its rule set.
- **Lens 5 Data flow** found row 3, traced the weighted room as exact for every shipped weight, and proposed the room in the log line (applied). Row 2 is a test gap, outside its value traces.
- **Lens 7 XML** found rows 6 to 8. Rows 2 to 5 are outside its files.
- **Codex** confirmed no new finding. Its S1 evidence settled row 7. For row 2 it noted that the patch test runs on a fake handler and does not verify the reflection target, then checked the target itself in the installed DLL; it did not report the missing gate. It named the installed-versus-source difference of row 3 as a qualification, not a finding.

## Improvements applied

Lenses 3 and 6 did not run (tier 1, first wave only, and no HIGH), so Step 4 is N/A. The optional proposals:

- **Applied (lens 5):** the `PartyFull` log line names the room, so the 1108 human checks can be read from the log.
- **Not applied (lens 2 and lens 5): `DescribeHero(Hero)`.** `ServiceSignatureGateTests` refuses a new adapter member that names `Hero`. The id lookup runs once per party screen.
- **Not applied (lens 2): a `Skip` check line when upgrades charge nothing.** A zero charge is the normal result for every troop without an `upgrade_cost` and for gold-only, so the line would fire on most screens. After row 5, `DescribeHero` returns null only after a logged read failure.

## Not fixed

- **The client's gate and the server's charge read different state.** The guest's balance is stale until #967 C4 and the career discount is client-side until #967 C5, so the server can charge more than the client's Patch26 clamp allowed, and the balance floors at zero. `special-resources.md` records it, and Codex S4 confirmed it as a known limitation.
- **A fractional custom weight gives a conservative cap.** Every shipped weight is a whole number (lens 5, Codex S6). The room is never above the true room.
- FOLLOW-UP: `CoopMissionBindingTests` has no shape row for Patch113's target `TroopRosterInterface.HandleOnRecruitmentDone` (lens 2). Older than this change. #967
- FOLLOW-UP: `SpecialResourceEventAdapter.DescribeLocalHero` and `SnapshotDaily` still read `Hero.MainHero`, which may throw on a dedicated server (UNVERIFIED). Older than this change. #967
- FOLLOW-UP: the Enable Troop Weight MCM hint says "mumakil 10x" (`TaomSettings.cs:46`); the data says 20 (lens 5). A fix changes translated English (Case E). no issue: creating a public issue needs the maintainer's word.

## Not run

- **In game:** `tools/ingame_checks/967.toml` needs a co-op session with a guest upgrade, and the 1108 human rows need a person at the emissary with Troop Weight on. No session has run a build of this change.
- **A second review wave or Codex pass on the fixes.** Row 2 is a test, and rows 3 to 8 are docs, text, a null-safe read and a manifest. The tests and `/verify` prove them.
