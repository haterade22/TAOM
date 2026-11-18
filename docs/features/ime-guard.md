# IME Guard

## Overview

For players who type with a Chinese, Japanese or Korean input method (IME), the IME opened its candidate window when
they pressed hotkeys on the campaign map and in battle, and the keys went to the IME. The IME guard pauses the input
method while the game window is in front and no text field has focus, and gives it back as soon as a text field takes
focus, the game leaves the foreground, or the switch is turned off. Ported from Silas He's v1.5.6 fix package (#1447).

## Why This Exists

- **Vanilla behavior:** the game window keeps one Windows input context for its whole life, so an active CJK IME
  reads every key, also outside text fields.
- **TAOM requirement:** hotkeys reach the game; text fields (search, rename, the clan and kingdom name popups) still
  take CJK text.
- **Without this feature:** CJK players have to switch their IME off by hand before each campaign or battle session,
  and on again for each name they type.

## Architecture

### Design Challenge

Windows re-attaches a saved input context, and wakes the IME for it, only when the call comes from the thread that
owns the window. The engine creates and pumps the game window (class `TWApplication`) on its own "Message Pump"
thread, not the thread that ticks the modules. From the tick thread Windows ignores the re-attach and the window keeps
no context; attaching its default context (`IACE_DEFAULT`) works from any thread, but the IME may not wake for it.

### Solution Approach

- `Domain/ImeGuardRules.ShouldDetach`: detach only when the switch is on, the game window is in front and no text field
  has focus.
- `ImeGuardService`: a small state machine ticked from `ClientSubModule.OnApplicationTick`. It detaches once, puts the
  context back on any other state or when another game window comes in front, and stops for the session after one
  logged failure.
- `IImeWindowAdapter`: core registers `NullImeWindowAdapter` (never in front), so core resolves without the client
  (ADR-013). A dedicated server never loads `TAOM.Client`, so it never ticks the guard. The client's `ImeWindowAdapter` reads `GetForegroundWindow` (counted only for the game's own `TWApplication` window) and the Gauntlet focus (`ScreenManager.FocusedLayer`, and
  global layers in `ScreenManager.SortedLayers`, where native text popups sit), and runs the `imm32` calls on the
  window thread through a thread message hook (`WH_GETMESSAGE`) and an empty `WM_NULL` message.
- The context is per window, so other programs keep their own IME state whatever the game does.

### Component Diagram

```
ClientSubModule.OnApplicationTick
        |
  ImeGuardService (state machine) -- IImeGuardSettingsProvider (MCM switch)
        |
  IImeWindowAdapter
     /        \
NullImeWindowAdapter   ImeWindowAdapter (client: user32, imm32, ScreenManager)
(core)                      |
                     WindowThreadDispatcher -> window thread -> ImeContextLease
```

## Configuration

MCM, TAOM page, **Map UI / Text Input**: **Pause the input method outside text fields** (`ImeGuardEnabled`, default
on, applies at once). Co-op does not compare it: it is a local window setting (`CoopSettingsRelevance`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ImeGuard/Domain/ImeGuardRules.cs` | When to detach |
| `Main/Features/ImeGuard/ImeGuardService.cs` | The state machine |
| `Main/Features/ImeGuard/IImeWindowAdapter.cs` | The adapter seam, its state struct and the null adapter |
| `Main/Features/ImeGuard/IImeGuardSettingsProvider.cs` | The MCM read |
| `Main/Features/ImeGuard/ImeGuardIoC.cs` | Core registrations |
| `Client/Features/ImeGuard/ImeWindowAdapter.cs` | The Windows calls, the window thread hook and the context lease |
| `Client/ClientSubModule.cs` | Ticks the service |
| `Client/TaomClientComposition.cs` | Replaces the null adapter |

## Dependencies

- `user32.dll` and `imm32.dll` (Windows only, which is every Bannerlord client).
- Client only: a dedicated server never loads `TAOM.Client`.

## Tests

- `TAOM.Tests/Features/ImeGuard/ImeGuardServiceTests.cs`: the rule, a single detach, the re-attach on text focus,
  foreground loss and the switch, the move to another game window, no re-attach when nothing was detached, and the
  stop after a failure with one warning.
- The client adapter's Windows calls have no unit test; they are checked in game.

## How to verify in game

**Preconditions:** a Windows Chinese, Japanese or Korean IME, switched on, and a TAOM build with #1447. Needs a CJK
tester.

1. On the campaign map, press hotkeys (for example `I`, `C`, `P`, space): each opens its screen and no IME candidate
   window opens.
2. In a battle, press order keys (`F1`, `F2`) and `M`: no IME candidate window opens.
3. Open a rename or search field and type: the IME composes CJK text.
4. Alt-tab to another program: its IME works as before.
5. The TAOM log has `[ImeGuard] input method paused` once, and `[ImeGuard] input method restored: original` after the
   first text field. `default` or `FAILED` there is the evidence to report.
6. Turn the switch off in MCM: the IME takes hotkeys again, as before the fix.
7. Open the Steam overlay (Shift+Tab) and type in its chat: the IME composes CJK text there too. Text focus is read
   from Gauntlet only, so a failure here is the evidence to report.
8. Quit to the desktop several times, from the main menu and from a campaign, moving the mouse while the game closes:
   no crash, and no 0xC0000005 entry for Bannerlord in the Windows Application log.

Open question for the tester: text focus counts the focused layer and the global layers, as Silas's version did. A
top-screen layer that the engine refused focus still receives typed text; if a text field ever composes no CJK text,
report which screen it was on.

## Performance

Each frame with the switch on: one `GetForegroundWindow`, one `GetWindowThreadProcessId` and a walk of the screen
layers, which are few. With the switch off, no window call runs. The context calls run only when the state changes.
After the first detach, the window thread hook runs a little managed code for every message the pump thread
retrieves, until the game exits, also with the switch off; it then only passes each message on. It is removed in
`ClientSubModule.OnSubModuleUnloaded`, before the engine's last GC, which would otherwise collect its delegate while
the hook still calls it.

## Changelog

- 2026-11-18: feature created, ported from Silas He's v1.5.6 fix package (`TAOM.BattleExperience`, `ImeGuard.cs`),
  #1447. Not ported: his focus-change Harmony patch, his `ScreenManager.FocusGained` tick and his restore at
  unload. The context belongs to the game window only, so a detached
  context while the game is behind another program changes nothing for that program, and the next tick in front
  reads the state again.

## GitHub Issue

- **Issue:** #1447: [feat: stop the CJK IME from taking hotkeys outside text fields](https://github.com/haterade22/TAOM/issues/1447)
- **Status:** Open
