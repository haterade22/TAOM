"""Set the Status cell of plan rows in plans/README.md. Usage: python set_status.py <num> "<status text>" [...]"""
import re
import sys

p = r"E:\repos\TAOM\plans\README.md"
s = open(p, encoding="utf-8").read()
args = sys.argv[1:]
for num, status in zip(args[0::2], args[1::2]):
    pat = re.compile(r"^(\| \[" + num + r"\]\([^)]*\) \|(?:[^|]*\|){5}) [^\n]*\|$", re.M)
    s, n = pat.subn(lambda m: m.group(1) + " " + status.replace("\\", "\\\\").replace("\\\\", "\\") + " |", s)
    print(num, "updated" if n == 1 else f"NOT FOUND ({n})")
open(p, "w", encoding="utf-8", newline="\n").write(s)
