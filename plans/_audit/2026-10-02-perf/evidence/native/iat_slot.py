"""Print the IAT slot RVA of selected imports in a PE file (scratch helper, read-only)."""
import sys
import pefile

path = sys.argv[1]
wanted = set(sys.argv[2:])
pe = pefile.PE(path, fast_load=True)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"]])
base = pe.OPTIONAL_HEADER.ImageBase
for entry in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []):
    dll = entry.dll.decode(errors="replace")
    for imp in entry.imports:
        name = imp.name.decode(errors="replace") if imp.name else f"ord{imp.ordinal}"
        if not wanted or name in wanted:
            print(f"{dll}!{name}  iat_rva=0x{imp.address - base:X}")
