# RCA: review of PR #1340, the wrong game version warning

PR #1340 (REDACTEDSTUFF, head 98cbc971f) warns the player when the game version differs from the version TAOM pins.
A new `TAOM.Dependencies` constructor reads the pin and the game version. On a difference it patches
`Module.CheckIfSubmoduleCanBeLoadable` so the submodules of TAOM and of its dependents do not load, and the game
reaches the main menu, where a popup names both versions. The review ran at tier 1 (a Harmony patch on module loading,
engine calls at startup): lens 5 (data flow) and lens 2 (engine compatibility), each at the agent definition's `max`.
Codex did not run: it is out of usage (Mike, 2026-11-15).

Lens 2 found all 17 engine members present on v1.5.7 with matching shapes, and the target's only caller is the
submodule loop. Lens 5 traced the pin, the version source, the skip set and the popup, and found them connected.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | On the skip path TAOM's XML is registered and its code is not, and the game's save check sees TAOM in the module list at its own version. A TAOM save would load with no warning and no TAOM code, and a save made then would be damaged. The popup's Ok only closed the popup (lens 2 finding A; the contributor's RCA row F7 left it to the owner) | Unsafe state left open | The popup text told the player to close the game, so the state looked handled | Fixed: the contributor made the same change in parallel (873e04f98, d671c33c8): the only button is Native's "Exit Game" and calls `TaleWorlds.Engine.Utilities.QuitGame()` in a NoInlining helper with a catch. The review kept that version and dropped its own duplicate; MCM's restart prompt quits the same way from an inquiry button (session read MCM v1.4.5 line 1632). The popup text now says "Press Exit Game". Lesson appended to `state-lifecycle-save.md` |
| 2 | MEDIUM | The second remedy told the player to install "the TAOM made for your game version" from the launcher; after a game hotfix no such TAOM may exist yet (lens 5 trace 5) | Promise the code cannot keep | The text was written for the case where a matching build exists | Fixed: "if a TAOM made for your game version is out, install it". The compare stays major.minor.revision: a revision is the engine change TAOM is re-verified for |
| 3 | LOW | The guard read `MBDebug.TestModeEnabled`, which `/runTest` sets only after the submodule loop (`Module.cs:315` against `:307`), so it was always false there (both lenses) | Dead check | It copied the engine's own guard, which has the same limit | Fixed: the read is gone (parity: it never fired); the comment, the doc and the test say why |
| 4 | LOW | A test comment said `ApplicationVersion.FromParametersFile` reads the on-disk `Version.xml`; it reads text compiled into `TaleWorlds.Library` (lens 2 finding D, lens 5 trace 2) | Wrong engine claim in a comment | The contributor's RCA F4 fixed the doc, not the test | Fixed: the comment names both sources. FOLLOW-UP no issue: the same claim in the `VersionProbe.cs` header is outside this PR and changes no behaviour |
| 5 | LOW | `reflection-sites.md` Category A did not name the hand-attached patch (lens 2 finding C) | Catalogue gap | The patch sits in `TAOM.Dependencies`, which the catalogue rarely covers | Fixed: one paragraph names it and its binding test |
| 6 | LOW | An optional dependent is skipped too; a module that names TAOM only in its metadata is not; the walk is not transitive (lens 5 trace 4) | Edge cases with no producer | No module or launcher on this desktop produces them | Not applied: a guard for a case nothing produces needs Mike's word (`simplicity-criterion.md`). FOLLOW-UP no issue: revisit if a third-party module depends on TAOM |

## Why each lens caught or missed these

- Lens 2 found row 1 by reading the game's save check (`SandBoxSaveHelper.CheckMetaDataCompatibilityErrors`) against the module list the skip leaves.
- Lens 5 found row 2 by comparing the three rules that read the same `version` attribute.
- Both lenses found row 3 by finding the flag's only writer.

## In-game checks owed

`startup-alerts.md` check 5: on a mismatched pin, the popup names both versions and Exit Game closes the game.
