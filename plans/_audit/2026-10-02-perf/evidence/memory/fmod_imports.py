"""List TaleWorlds.Native.dll's imported DLLs, and any audio-related imports with IAT addresses (read-only).

Usage: fmod_imports.py <the game's bin/Win64_Shipping_Client/TaleWorlds.Native.dll>
On v1.5.3 no FMOD DLL is imported: FMOD is linked into the engine DLL.
"""
import sys

import pefile

DLL = sys.argv[1]
pe = pefile.PE(DLL, fast_load=True)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"],
                                       pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT"]])
base = pe.OPTIONAL_HEADER.ImageBase
entries = list(getattr(pe, "DIRECTORY_ENTRY_IMPORT", [])) + list(getattr(pe, "DIRECTORY_ENTRY_DELAY_IMPORT", []))
for entry in entries:
    name = entry.dll.decode(errors="replace")
    print(f"== {name}: {len(entry.imports)} imports")
    low = name.lower()
    if any(k in low for k in ("fmod", "audio", "sound", "wwise", "xaudio")):
        for imp in entry.imports:
            nm = imp.name.decode(errors="replace") if imp.name else f"ordinal {imp.ordinal}"
            print(f"  IAT RVA 0x{imp.address - base:X}  {nm}")
