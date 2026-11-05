# Gauntlet screen checklist

What a new or changed TAOM screen, overlay or panel must handle before it counts as done. The
mechanics (sprites, brushes, input wiring, view-model binding) are in
[gui-ui.md](../../.claude/rules/gui-ui.md); this page covers what the player sees and does.
Condensed from [pbakaus/impeccable](https://github.com/pbakaus/impeccable) (`critique.md`,
`craft-floor.md`, `harden.md`, Apache-2.0) and calibrated to Gauntlet
(#1007).

Each item is a check on the built screen in game, not on the prefab text. Static review cannot see
a layout; an item not seen in game goes into the commit body's `Not-tested:` trailer, and the issue
keeps `triage-needs-ingame`.

## States

Run the screen with each of these, when the data can reach it:

- **Nothing:** an empty list, no troops, no prisoners, no gold. The screen says why it is empty and
  what the player can do, not a blank panel.
- **One, and many:** a single row, and the largest realistic count (a full garrison, 50 or more
  rows). The list scrolls and keeps its header and actions in view.
- **Long text:** the longest hero, clan, settlement and item names in the data, and the longest
  translation. `python tools/loc_length_report.py --keys <key> ...` names the language to check.
- **Large numbers:** gold, wages and troop counts at their highest realistic value.
- **Disabled:** every control that can be unavailable says why on hover and names what would enable
  it. Gauntlet does not hover a disabled widget, so the reason's hint sits in an always-enabled
  wrapper beside the button, as vanilla's governor button does
  ([messengers](../features/messengers.md) #944).
- **After an action:** the screen shows the result (a row moves, a count changes, a message
  appears). A click with no visible change reads as a broken button.

## Layout and flow

- **One primary action** per screen, placed where the vanilla screen it sits beside places its own.
  Same kind of action, same kind of control, as in vanilla.
- **No wall of options:** a decision offers about as many choices as the same kind of vanilla menu
  or inquiry, ordered so the usual one comes first; group the rest or reveal them on demand.
- **No memory bridge:** the player never has to remember a value from another screen to act here.
  Show the selected hero, party or settlement on the screen that acts on it.
- **A way out:** the screen closes with its own Done or Cancel control and with the same keys that
  close the vanilla screen beside it. A change the player cannot undo asks for confirmation first.
- **Text names the action:** a button says what it does ("Release prisoners"), an error names the
  problem and the recovery.

## Consistency with vanilla

- Reuse vanilla brushes, fonts and sprite families for the same role before adding TAOM ones; a new
  brush needs the declaration check in `gui-ui.md`.
- A TAOM panel injected into a vanilla screen matches the spacing and text sizes of its neighbours.
