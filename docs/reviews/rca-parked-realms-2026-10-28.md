# RCA: parked realms (six factions not playable at character creation, deep-review, 2026-10-28)

## Top line

Mike asked that the player cannot choose Lindon, Ered Luin, Cardolan, Rhudaur, Umbar or Khand at character creation,
because their troops have no armour yet. The change sets their `playable` flag to `false` in `factionmap/factions.json`,
pins the six with `WorkInProgressFaction_IsNotPlayable`, and relaxes `FactionUIConfigIdsTests` so the faction screen's
tuning rows for the parked realms can stay. `/deep-review` ran at tier 2 with Lens 5 (data flow) and Lens 7 (XML and
ModuleData) in one wave; Codex was not run, because nobody asked for a paid dispatch. Lens 5 traced 17 flows and found
no runtime gap: every path that sets the player's start culture reads the flag or runs after something that does. Both
lenses found only LOW items, and every one in the changed code was fixed before the commit.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | LOW | Removing four realms from `PlayableFaction_HasMinimumContentSections` dropped their card content checks, although the faction map still shows their full cards, and the restore step did not put them back. (Lens 5 and Lens 7) | The content test is keyed on the `playable` state, not on what the player sees; moving a realm between states moved it out of the check. | The content assertions are one helper that both the playable and the parked tests call; the restore step says to move the row. Arthedain, playable and never in the list, gained its row. |
| 2 | LOW | Two docs quoted a hand count of playable factions (24 and 20); the command beside each now prints 18. The second row's side counts were also stale before this change. (Lens 7) | Hand counts with no gate. | Both rows rewritten from the command output, with the date. |
| 3 | LOW | `faction-ui.md` said the relaxed gate accepts a faction "playable or parked"; it accepts any of the 45 factions, and a row for a never-playable faction passes and only logs. (Lens 5) | The doc described the intent, not the helper. | The doc says what the gate accepts and what it lets through. |
| 4 | LOW | `faction-ui.md` still said three of the parked realms show the elite infantry troop on the faction screen. (Lens 5) | The sentence predates the change and was not re-read. | It says only Arthedain is listed today. |
| 5 | LOW | The realm status lines said the player "cannot start" there; the accepted Advanced Starting Options path still starts there. (Lens 5) | Loose wording. | They say "cannot choose it at character creation", and `faction-map.md` names the ASO path. |

## Accepted limits

- The Advanced Starting Options menu still offers the six kingdoms for a King, Vassal or Mercenary start (Mike, 2026-10-28).
- The faction screen logs one warning per tuning file, once per process, for the parked rows (Lens 7 F3). Aligning the
  runtime check with the relaxed gate changes production logging; not done.
- Follow-up closed 2026-11-04 (b7fefdae6, f9a8bdebc) (no issue: filing one is public and waits on Mike's word): `tools/make_new_factions_playable.py --apply`
  writes stale entries straight into `factions.json` (Lindon playable with `game_faction` `rivendell`, Blue Craig with
  `goblin`), and `tools/assemble_faction_map.py` and `tools/process_faction_map.py` overwrite `playable` and
  `game_faction` from old tables. `WorkInProgressFaction_IsNotPlayable` now catches a flag flip; nothing pins a real
  `game_faction`. Pre-existing.
- `FactionUIConfigIdsTests` passes with nothing to check if the FactionUI files are missing (an empty config). Pre-existing.

## Lesson

One entry in `lessons/testing-qa.md`: a content gate follows what the player sees, not the state flag that the change
flips.
