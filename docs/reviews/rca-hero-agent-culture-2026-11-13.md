# RCA: Hero agents read their hero's culture, review (2026-11-13)

**Issue:** #1157. **Branch:** `fix/1157-hero-agent-culture` from trunk `ef959062f`.
**Review:** `/deep-review` at risk tier 1 (the readers run inside GameModel overrides and mission
logic): Agent 5 Data flow and Agent 2 Engine compatibility (`deep-reviewer`, effort max), and Codex
(`gpt-6-astra`) at `xhigh`, below the tier's `ultra` (prompt
`docs/reviews/codex-prereview-hero-agent-culture-2026-11-13.prompt.md`).

## Top-line

An agent's `Character` is a `BasicCharacterObject`, so `agent.Character.Culture` binds to the base
property and reads the raw `_culture` field (v1.5.7 `BasicCharacterObject.cs:124-134`).
`CharacterObject.Culture` is a `new` property that returns `HeroObject.Culture` for a hero
(`CharacterObject.cs:264-278`). For the player's hero the raw field holds `main_hero`'s
`Culture.battania` (SandBox `lords.xml:11`), because `CharacterCreationContent.ApplyCulture` sets only
`Hero.MainHero.Culture` and `Clan.PlayerClan.Culture` (`CharacterCreationContent.cs:137-138`). Nine
mission readers in eight files used the raw field, so the player fought as Khand for Culture
Doctrine, mount charges, race abilities, banner heraldry and formation routing. The change adds
`CharacterCulture.Of` and `IdOf` and moves the readers to them. All three reviewers found no runtime
gap. They found two LOW defects in the new gate, one LOW weakness in an older IL test, and one wrong
method name in the review briefs.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW (Codex, Agent 5) | The source gate's regex `\.Character\s*\??\.Culture\b` banned a spelling, not the binding. Codex reverted `FormationRoutingSubscriber.Route` in memory to `character.Culture?.StringId`, the read on a `BasicCharacterObject` parameter that this change fixed, and the gate passed. It also missed `agent.Origin.Troop.Culture` and `AgentBuildData.AgentCharacter.Culture`, and failed on the same text inside a string. | Gate checks syntax, not the defect | I wrote the gate from the agent route that the issue named. I fixed the parameter route by reading the file, and did not ask whether the gate would have found it. | `CharacterCultureGateTests` scans the IL of `TAOM.dll` and `TAOM.Client.dll` for calls to `BasicCharacterObject.get_Culture` (the `MusicPauseGateTests` shape). The raw readers are listed by type or namespace, each with its reason. A canary proves the scan sees the agent route and the parameter route. Lesson bullet added to `lessons/adapters-taleworlds-api.md`. |
| 2 | LOW (Agent 5, Agent 2, Codex) | The gate sat in `CharacterCultureTests`, whose class carries `RequiresGame`, so the no-game step (`.ai/verification.md`, filter `TestCategory!=RequiresGame`) never ran it. The sibling source gates carry no tag. | Test category inherited from a class | I added the gate to the helper's test class, which needs the game for its `CharacterObject` and `Hero` fakes. | The gate has its own untagged class and goes Inconclusive when the game assemblies do not load, as `MusicPauseGateTests` does. |
| 3 | LOW (Codex) | `MountChargeDamageBindingTests.RiderCultureOf_HopsThroughRiderAgent` accepts `_ = mount.RiderAgent; return CharacterCulture.IdOf(mount.Character);`. Codex compiled that in memory and the scanner gave `get_IsMount,get_RiderAgent,get_Character,IdOf` with the order check true. The production expression is correct. The test had the same gap before this change, with `get_Culture` in place of `IdOf`. | Older test weakness | The test checks call names and their order. It cannot see which value a call receives. | Not applied (see Fixes). |
| 4 | WRONG claim (Agent 2, Agent 5, Codex) | The Codex prompt and the lens briefs named `CharacterCreationContent.SetSelectedCulture` for lines 137-138. Those lines are in `ApplyCulture` (135-141); `SetSelectedCulture` (126-133) writes no culture. The code cited only the line numbers, so no code was wrong. | Name written from memory | I wrote the method name from an earlier read and did not re-read the method header before the brief. | The helper's remarks now name `ApplyCulture`. The prompt file stays as the record of what was sent. |
| 5 | Text (Codex) | `race-abilities.md` did not say that a hero whose `Hero.Culture` is null gets the raw field, and said the raw field "holds" the ModuleData culture where repair code and Coop's null-culture postfix can fill it from the hero. | Unqualified doc claim | I described the player's case and wrote it as the rule. | The doc and the remarks name the null fallback; the doc says "can hold". |

**Not entered as a finding:**
- Agent 5's old-save note. An XML lord whose ModuleData culture changed after a save now uses the
  culture saved with the hero (`Hero.Culture` is saved, `_culture` is not). On a save from before
  the move of `lord_1_48_1`, `_2` and `_3` from Dol Guldur to Mordor, those lords now get Dol Guldur's
  `neverRout: true`. That matches the recorded rule that heroes keep the culture they were created
  with (`alignment-aware-execution.md`, `minor-factions.md`). The commit body states it.
- Agent 5's note that `EquipmentSnapshotAdapter` writes `<noculture>` for every Custom Battle agent.
  It is crash-report text only, older than this change, and outside its scope.

## Disagreement

None. Codex and Agent 5 found findings 1 and 2 on their own and agree on each. All three named the
wrong method in finding 4.

## Why each reviewer missed these

- **Agent 2** found findings 2 and 4. Its lens checks engine claims, not gate coverage.
- **Agent 5** found findings 1, 2 and 4, and traced 18 flows with no gap.
- **Codex** found all five. Only Codex proved finding 1 and finding 3 with compiled counterexamples.

## Fixes

- `TAOM.Tests/Adapters/CharacterCultureGateTests.cs` (new) replaces the regex gate. The red run
  listed 8 calling methods in `TAOM.dll` and 4 in `TAOM.Client.dll`; they hold the call sites Agent 5
  listed from the IL. A mutation run put `character.Culture?.StringId` back in `FormationRoutingSubscriber.Route`;
  the gate failed and named that method.
- `BannerBearerLogic_SpawnBannerBearer_Patch.Prefix` writes the troop's culture into a warning text.
  It now reads `CharacterCulture.IdOf(troop)`, the same answer for a troop, so it needs no place in
  the raw reader list.
- `Main/Adapters/CharacterCulture.cs` remarks and `docs/features/race-abilities.md:70-74`: findings
  4 and 5.
- **Not applied, finding 3 (no issue):** a receiver check needs a data-flow pass over the IL that
  `IlCallScanner` does not have. Win: a guard against one more spelling of a regression the order
  check already narrows. Cost: a new IL helper. The simplicity criterion rejects it.
- **Not run:** the new gate in the no-game RefAsm configuration of `.ai/verification.md`.

## Feedback memories to codify

None. The lesson bullet carries the prevention.
