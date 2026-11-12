# RCA: Hero Auras Slice 1 (#1127, deep review 2026-11-12)

## Top-line

The tier 1 deep review of Hero Auras Slice 1 (worktree `feat/hero-auras`, base 8fe13342c, uncommitted) ran two
waves: lenses 5 and 2 at max effort, then lenses 1, 3, 4 and 6 at high effort after wave 1 reported a HIGH. Codex did
not run (out of usage, Mike 2026-11-12). The review confirmed 1 HIGH, 5 MEDIUM and 10 LOW findings, all fixed or
documented before the first commit. The two MEDIUM engine findings repeat a trap TAOM had already recorded: a
nearby-agent query returns its own centre, and its "ally" set is wider than one team.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `HeroAurasMissionLogic` had no co-op role: unlisted means `Local`, so each co-op peer would stamp the other peers' puppets with local stat and damage changes | Co-op authority | The builder added a mission behavior without reading the role table. `CoopMissionRolePolicyTests` caught it, but only in the full /verify run after the build | None new: the gate worked. Role `AgentOwner`, an `IsOwnedHere` filter in the pulse, the doc row |
| 2 | MED | The hero received his own aura and counted as a reached soldier, so Dáin alone wrote a `[Check]` pass | Engine API semantics | The builder copied the Dread Aura's source side and did not read the lesson "Check whether a collection-returning API includes the caller", which names `GetNearbyAllyAgents` | Repeat offender: a new item 4 in `.claude/rules/csharp-architecture.md` (loads on every C# edit), plus the recurrence in the lesson |
| 3 | MED | `ally.Team != team` dropped the troops of an allied team (the player beside a lord outside an army), against "units around them" | Engine API semantics | The builder assumed "ally" meant "same team". The managed call passes `GetNearbyAgentsAuxType.Friend`; native admits any team whose enemy bit is clear (`GetNearbyAgentsAux`, RVA 0x6F91A0) | The same rule item 4 |
| 4 | MED | The four MCM strings had no `{=KEY}` | Localization | /localize was skipped; `UnregisteredLocalizationKeyBaselineTests` sees only keyed literals | None new: the CLAUDE.md skills table already says /localize "always" |
| 5 | MED | `HeroAuraSettingsProvider` had no test, and `Strength > 0` is what makes 0 mean off (`Scale` reads 0 as 1) | Test coverage | The rule lived in a comment | `HeroAuraSettingsProviderTests` |
| 6 | MED | The gate (`SignatureMissionGate`) admitted every combat mission, hideouts and the deployment phase included, wider than the issue's field battle, siege, sally-out and Custom Battle | Spec drift | The builder reused the nearest gate without comparing it to the issue text | Allowlist plus `Mode == Battle` each tick; the doc states where auras run |
| 7 | LOW | `lingerSeconds: 0` passed validation but expired every stamp in its own tick | Config validation | The range was copied without tracing the pulse order | Minimum 0.25, `GetConfig_ZeroLinger_RevertsAndWarns` |
| 8 | LOW | After a throw the auras stayed on to the end of the battle | Failure path | The stand-down path was not traced past the log line | `StandDown` expires every stamp |
| 9 | LOW | `OnAgentBuild` called the pulse unguarded inside `Mission.SpawnAgent` | Failure path (lesson #595) | Same as 8 | try/catch and stand-down, as the Dread Aura |
| 10 | LOW | Race names were checked only at run time | Test coverage | The shipped-config test copied the hero id check only | `ShippedConfig_RaceNamesAreSpelledAsTheGameSpellsThem` |
| 11 | LOW | Saruman's aura missed the `berserker` race (lens 5: 6 of 164 Isengard rows; not re-counted here) | Data | The race list was written from memory | Race added |
| 12 | LOW | The telemetry text, five model comments, five registry rows, `long-defeat.md`, `race-abilities.md` and the toml comment no longer matched the seam | Docs drift | The seam change was reviewed for code, not for every text that describes it | Text corrected; `HasLiveAbility` renamed `HasLiveEffects` |
| 13 | LOW | Overlapping auras linger only as a set | Design limit | Accepted by design | Documented as a known limitation |
| 14 | LOW | A cache comment credited `Stamp`'s reference check with a saving that `TryExtend` gives | Comment accuracy | Written before `TryExtend` existed | Comment corrected; the check removed (lens 6 proposal 1) |
| 15 | LOW | The Théoden and Éomer auras reach foot soldiers and have no horse-charge channel, unlike the issue's draft | Spec drift | No rider filter or charge channel exists | Documented; Théoden gained dismount resistance; rider filter and charge are Slice 2 candidates on #1127 |
| 16 | LOW | Two auras with one id, or ids holding the key separators `+` and `@`, could share an aura key, so a soldier moving between them kept stale effects (convergence pass) | Config validation | The loader rejected repeated hero ids but not repeated aura ids; the key's dependence on unique ids was implicit | The loader skips both with a warning; two config provider tests |

## Root-cause pattern

Findings 2, 3 and 6 share one cause: the builder took a sibling's code (Dread Aura, Signature Strikes) as the
specification instead of the issue and the engine. The sibling's choices were right for the sibling: the Dread Aura
pulses enemies, so self-inclusion and allied teams never mattered to it. Reuse of code carries the reused code's
assumptions; the check is to read the API's semantics and the issue's text before keeping them.

## Why each agent missed these

- **Lens 5 (data flow)** found 1, 2, 7, 8, 10, 11, 12, 13 and 14. It did not raise 3: it read the doc line "of the
  hero's team" as the intent. It did not raise 4 and 6: localization and spec scope are lens 4's domain.
- **Lens 2 (engine)** found 2, 3 and 8 from the native decompile. Localization, tests and spec scope are outside it.
- **Lens 1 (standards)** found 4, 5 and comment items in wave 2. **Lens 4 (completeness)** found 4, 5, 6 and 15.
  **Lens 3 and 6** made no defect findings: their proposals (aura key, refresh decision, `EffectsOf`, rename) were
  applied as improvements.

## Feedback memories to codify

None. The durable rule is item 4 of the mission-scope section in `.claude/rules/csharp-architecture.md`, and the
lesson entry in `docs/reviews/lessons/adapters-taleworlds-api.md` records the recurrence.
