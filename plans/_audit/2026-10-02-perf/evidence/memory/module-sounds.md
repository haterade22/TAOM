# Module sounds: when the engine loads them and what a play holds

2026-10-03, v1.5.3 `TaleWorlds.Native.dll`, read with `tools/native_decompile.py` (Ghidra) and the scripts in
this folder. It answers REPORT M8's open question: do TAOM's 437 `ModuleSounds` files (166 MiB) sit in
memory from the main menu on? **No.** The engine registers them at startup and loads each file only when an
event plays it.

## Native facts [TAOM-verified, Ghidra, 2026-10-03]

| Piece | Where | What it does |
|---|---|---|
| FMOD | the whole engine DLL | No FMOD DLL is imported (`fmod_imports.py`): FMOD Core and Studio are linked into `TaleWorlds.Native.dll`. No `System::createStream` name exists in the binary, so every file sound goes through `System::createSound` |
| `System::createSound` | `0x80DEC0` (the only function using the API name `System::createSound`, at `0xA401F8`) | FMOD's public wrapper: `(system, name or data, mode, exinfo, sound out)` |
| Module sound parser | `0x232C90`, with `0x22CA10` (the two users of `module_sounds`, at `0xAFA6B0`) | `0x232C90` reads each `module_sound`: category, `is_2d`, pitch and volume ranges, variations with `path` (prefixed `ModuleSounds/`) and `weight`. It files a record through `0x23AF10` (by its use, an insert into a name-keyed table) and copies the fields in. **Neither function calls `createSound`** |
| Programmer-sound callback | `0x2378E0` (`native/module-sounds/programmer-sound-callback-0x2378E0.txt`) | An FMOD Studio event callback. Type `0x80` (create programmer sound): for a record of kind 1 it calls `createSound(path, mode)` with `mode` = `FMOD_2D` (`0x8`) or `FMOD_3D` (`0x10`), plus `FMOD_NONBLOCKING` (`0x10000`) unless the record's byte `+0x48` is set; kind 2 is raw 11,025 Hz PCM from memory (`0x1800`). Type `0x100` (destroy programmer sound) releases the sound (`0x8138B0`, the function using the API name `Sound::release`). Types `0x8` and `0x20` (started, stopped) notify a callback the engine registered. The type and mode values are FMOD's documented constants |
| Callback registration | `0x235EE0`, `0x236240` (mask `0xFFFFFFFF`), `0x239490` (mask `0x28`) | `EventDescription::setCallback` (`0x921500`) with `LAB_180234e90`, which dispatches to the callback above |

The mode never carries `FMOD_CREATESTREAM` (`0x80`) or `FMOD_CREATECOMPRESSEDSAMPLE` (`0x200`), so FMOD's
default applies: `createSound` decodes the **whole file into memory** for that play, and the destroy callback
frees it. The other four `createSound` callers pass no module path: two engine user-callback sounds with a
null name (`0x239F30` with mode `0x482`, `0x23A1F0` with `0x402`), and two inside the linked FMOD code past
its Studio API wrappers (`0x98AD90`, `0x9BB3E0`), which take name and mode from FMOD's own structures (bank
data by their place; not traced further).

Inference, not traced: which Studio events carry module sounds (the template event that the name lookup
builds a programmer instance from). The parser, the path prefix and the only file-path `createSound` agree.

## What it costs TAOM

From `module-sounds-decoded.txt` (`module_sound_pcm.py`; an MP3's decoded size is estimated from its first
frame's bitrate, CBR assumed) and `module-sounds-unregistered.txt` (`module_sound_unref.py`):

- **At the main menu: nothing.** The 166 MiB on disk are not part of the 7.6 GB floor (M8).
- **Per play:** the decoded size, for as long as the event holds it. By file header: 197 PCM WAVs decode to
  their own size (102 MiB in all); 145 IMA ADPCM WAVs (format tag 17, 5.0 MiB) to about 20 MiB; 93 MP3s
  (59 MiB) to about 317 MiB, about 5.4 times their size (two not estimated); two OGG files (0.1 MiB).
  Single sounds are small: the largest registered sound outside `LOTR/OST` decodes to 2.5 MiB.
- **Three music tracks are registered but nothing plays them:** `LOTR/OST/Isengard Walking.mp3` (46 MiB
  decoded), `Charge Theoden.mp3` (40 MiB) and `Flaming Red Hair.wav` (27 MiB), category
  `mission_ambient_3d_medium`. No TAOM code, no `TAOM_Map` or `LOTRLOME_Armory` scene or XML names them
  (grep of the installed modules, 2026-10-03). If something ever plays them, each play holds that much.
- **21 files are not registered at all** (53.3 MiB of every download): eight `Native/OST` MP3s, five
  `LOTR/Dwarf/D1_*` WAVs, `horn_elf.wav`, and seven small files (two dwarf MP3s, four troll and warg
  footsteps, one Theoden OGG).
- **One registered path has no file:** `LOTR/Elves/Alert/elf_horn.wav` (`module_sounds.xml:5`); the file on
  disk is `horn_elf.wav`. Nothing plays it today; a play would fail in FMOD and log the error.

## Levers (content, the maintainer's)

1. Delete the 21 unregistered files: 53.3 MiB off the download, no runtime effect.
2. Drop or keep the three unplayed music tracks (43 MiB of download). If a feature is meant to play them,
   long music is the costly case for a module sound, since the format has no streaming attribute.
3. Fix `elf_horn.wav` (rename the entry or the file) or drop the entry.
4. A gate: `validate_moduledata.py` does not check `module_sounds.xml` paths against `ModuleSounds/`; one
   check would have caught item 3 and lists item 1.
