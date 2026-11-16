# RCA: timed abilities and killing blows for Aragorn and Gimli (#1359)

Findings from the tier 1 review of #1359, Phase 4 of #1325, on the uncommitted worktree `feat/hero-companions` over 2ef2d647d. Three lenses ran (5 Data flow, 2 Engine compatibility, 7 XML and ModuleData). They found no CRITICAL and no HIGH, so no second wave ran; five MEDIUM and a set of LOW items were fixed and verified with tests. The engine lens verified 41 engine members and found none incompatible. The Codex pass did not run: the maintainer said to skip it while the account has no usage.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | The `fired` check could fail while the heroes behaved correctly. It counted a pass as contact when one enemy stood within 10 m on the ground, and failed a hero with 20 such passes and no cast. The abilities need three enemies within 10 m or 8 m by 3D distance, so a hero on the wall walk above a crowd at the wall's foot, or fed one ladder at a time, would fail the line with no defect | A check judged by another rule than the code's | The pinned plan defined contact as a proxy ("an enemy near") instead of the trigger the kit really has. The engine query takes a 2D point, and the sensor measures in 3D | Contact is the kit's own easiest enemy trigger, counted by an independent 3D count (`HeroAbilityService.TryContactRule`, `HeroAbilityHost.InContact`). Lesson 1 |
| 2 | MED | The kill filter never saw a shrug-off. The strike context is built with `BlowFlags.None` on this path, so the clause that excluded a shrugged-off hit was dead, and a berserker in his berserk window could be killed outright. A test covered a state production never builds | A guard fed by a constant | The plan's own claim list said the flag is always false here and kept the clause | `DecideInstantKill` takes `isShruggingOff`, read from `RaceAbilityHooks.IsShruggingOff(victim)` at the kill; the dead clause and its test are gone |
| 3 | MED | Three texts said the abilities work in the Helm's Deep story mission. The mission heroes load for custom games only, so no hero with a kit can stand in a campaign-hosted mission yet. The gate also read a second marker that can never change its answer, because the story launcher adds the Long Defeat's logic too | A claim written before a decision changed its ground | Phase 2 decided to load the characters for custom games only; the Phase 4 design still described the campaign registration of the original plan | The gate reads one marker; the config comment, the feature doc and the manifest say the story mission gets the kits when a later phase loads the heroes in a campaign |
| 4 | MED | The manifest copied the hero-aura check of #1127, whose line names no aura and which trunk already writes, so this issue could read PASS on a build without the change | A proof any build can give | The check was copied for its id, not written from what this change adds | The check passes only on the mission-end line that shows `aragorn_heir_of_isildur` reached a soldier, and `fixed_in` is the feature commit |
| 5 | MED | An ability's enemy shock and the heroes' strike fear drain morale, but the Long Defeat holds every attacker (`LongDefeatMorale.HoldsAttacker`), and the engine then floors his morale instead of making him run. The drain changes no outcome in the gated missions, while the manifest asked a person to see enemies break | An effect decided without reading who consumes it | The plan fixed "Gimli's fear is his strike fear and his war-cry shock" from the strike feature's code and never read the morale model of the mode it would run in | Texts corrected; the human claim is removed. What fear should do where attackers are held is the maintainer's decision. Lesson 2 |
| 6 | LOW | The stats check computed its expected value through the hooks it tests; `ISignatureStrikeService.ComputeFearDrain` had no caller left; the aim-error floor was in one of two validators; the heroes' war cry ignored the war-cry switch; a config comment said "human" where the filter is the humanoid flag; a doc sentence promised a refresh for a horse the rider left | First-draft gaps | Each is one line | Fixed in place, each with a test or a pin where code changed |

Process notes, not findings of the lenses: the builders wrote some tests after their code, against the brief. A stray `sed -i` left two JSON files with LF endings; a line-ending check over every changed file before the commit caught it.

Not applied:

- `PassiveEffectConsumers.cs` still says "four fear drains". The count is still four; the sentence names no call site.
- A horse its rider left keeps a buffed mount speed until its own next refresh. No shipped kit grants mount speed.

FOLLOW-UP (#1325): what Gimli's fear does in a mode that holds its attackers. Three options are on the issue: a stagger on the war cry, a hold the heroes' shock can break, or the drain as it is.

FOLLOW-UP (#1325): Aragorn's aura finds him by character id in a custom game and would miss him in a campaign-hosted story mission, where the id comes from a `Hero` he does not have. The phase that puts the heroes in the story mission must handle it. Status: #1366 did it (`HeroAuraService.SourceId`, 2026-11-16); the story mission with the heroes has not been opened in a game.

## Proof

Run 20261115-232309 (4 waves) on the committed build 5ee569711. `python tools/ingame_verdict.py --issue 1359` reads PASS for the gate, `fired` (Aragorn 11 casts, Gimli 8), `stats-refreshed` (`ratio=1.100 want=1.100`), `instant-kill` (Gimli, an Uruk-hai warrior, roll 0.108 under 0.35) and the aura line (283 soldiers). No stand-down line. The contact counts were 287 and 206 seconds, so the new contact rule and the casts agree in a real fight. Both melee heroes stood through four waves; in the run before the abilities Aragorn fell twice in three.

## Root-cause pattern

Findings 1, 4 and 5 are the lesson of the shop heroes' review again, one layer up: a check, a copied check and an effect were each defined from the feature's own code and not from what the player would see in the mode. The shrug-off clause (2) and the story claims (3) are the same habit in small: text and guards kept after the fact under them had changed.

## Why each reviewer missed these

- **The architect's pinned plan** wrote findings 1, 2 and 5 into the specification. It read the engine for every call and did not read the Long Defeat's morale hold, which is TAOM code in another feature.
- **The builders** followed the plan. The third builder noted that the contact count ran only on passes in which the hero could decide, and did not question the proxy.
- **Lens 5 and lens 2** each found 1 and 5 independently, from the query's signature and from the morale model. **Lens 7** found 3 and 4 from the manifest and the module's load list.

## Lessons recorded

One entry in `docs/reviews/lessons/testing-qa.md` and one in `docs/reviews/lessons/gamemodels-services.md`, dated 2026-11-15 with this file as their source.
