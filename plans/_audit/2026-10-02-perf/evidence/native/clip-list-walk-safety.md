# Is `IsAnyAnimationLoadingFromDisk` safe to call every frame in a battle? 2026-10-03

Written for: Mike, deciding whether plan 041's hitch probe ships on by default (plan 036's clip memory probe
calls the same function once a second). Plan 041's review listed it as UNVERIFIED by every lens: the
function walks the engine's clip record list with no lock while the agent tick may be loading clips, and a
native access violation cannot be caught. Read with `tools/native_decompile.py` (v1.5.3,
`TaleWorlds.Native.dll`), `rip_target_scan.py` beside this file, and the v1.5.3 managed decompile.

## What the function reads

`IMBAnimation.IsAnyAnimationLoadingFromDisk` (`0x6EAAE0`, size `0x46`) takes the list's begin and end
pointers (`0xDB00C8`, `0xDB00D0`), walks the pointers between them and returns true at the first record
whose state (`+0xE0`) is 1, loading. It writes nothing and takes no lock. A clip loading concurrently only
changes a record's state field, an aligned 4-byte value, so the walk reads either the old or the new state.

## The list's lifecycle

- `0x55A3A0`, a static initializer for a block of engine globals, zeroes the list (`begin = end = 0`, a
  container tag of 3 at `0xDB00C0`).
- `0x563730` frees its storage, called from `0x55B160`, which logs "Start Game Final Cleanup." at exit.
- No other instruction in `.text` writes `0xDB00C0`, `0xDB00C8` or `0xDB00D0` directly. The brute-force
  disp32 scan finds 96 references to the begin pointer `0xDB00C8`: the initializer's write, two
  address-takes (the initializer and the teardown) and 93 reads. Disassembled from their function starts
  (`disasm_at.py` beside this file), the sampled reads are 64-bit loads of the begin pointer followed
  by `[rax + index*8]`: lookups of a record by index (`0x63D0B7`, `0x5E5938` with a bounds check against
  the end pointer, and the probe's own `0x6EAAE0`). The list is a member of a larger engine object, so it
  is filled through that object's methods; that fill path was not traced.
- So the engine itself indexes this list without a lock from about 90 places (which of them run during
  a battle, and on which threads, was not traced). If the list's storage could move while a battle runs,
  those lookups would be exposed to the same fault as the probe's walk.

## What vanilla does with it

`MissionScreen`'s `IMissionSystemHandler.BeforeMissionTick` (v1.5.3 decompile, `MissionScreen.cs:4077`)
calls `MBAnimation.IsAnyAnimationLoadingFromDisk()` on every frame of a mission's loading screen
(`:4129`), and keeps the loading screen up until it returns false. So the game itself runs this walk every
frame during the phase when clips load from disk most heavily.

## Verdict

Not proven, but the remaining risk is narrow. The walk only reads, and concurrent clip loading only
changes the state it reads. A crash would need the list itself to grow (and its storage to move) while a
battle runs; vanilla's own per-frame polling during mission loading would face the same race at the time
it is most likely, and so would the engine's own lock-free record lookups. What stays an inference: that no clip record is appended to the list after the mission
has loaded. The cheap way to settle it in game: the first measurement session's battles, with plan 041's
probe on, ending without a native crash in this function.
