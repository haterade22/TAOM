"""Builds out/xinput9_1_0.dll from keeper.c, keeper.def and keeper.rc. Needs the MSVC x64 build tools (found through
vswhere, or VCVARS64 set to a vcvars64.bat). Usage: python build.py

Upstream: yotthani/bannerlord HoN/ShaderCacheKeeper build.py (MIT). TAOM's version exports stubs through keeper.def
instead of generating forwarders to C:\\Windows\\System32 from dumpbin, and finds the tools through vswhere."""
import os
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
VSWHERE = os.path.expandvars(r"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe")
NAME = "xinput9_1_0"


def find_vcvars():
    """VCVARS64 if set, else the newest Visual Studio with the x64 C++ tools (vswhere)."""
    if os.environ.get("VCVARS64"):
        return os.environ["VCVARS64"]
    if os.path.exists(VSWHERE):
        found = subprocess.run([VSWHERE, "-latest", "-products", "*", "-requires",
                                "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property",
                                "installationPath"], capture_output=True, text=True).stdout.strip()
        if found:
            return os.path.join(found.splitlines()[0], r"VC\Auxiliary\Build\vcvars64.bat")
    raise SystemExit("no MSVC x64 tools found - install them or set VCVARS64 to vcvars64.bat")


VCVARS = find_vcvars()


def run(command):
    return subprocess.run('cmd /c ""%s" >nul 2>&1 && %s"' % (VCVARS, command), capture_output=True, text=True, shell=True)


out = os.path.join(HERE, "out")
os.makedirs(out, exist_ok=True)
resource = os.path.join(out, "keeper.res")
rc = run('rc /nologo /fo "%s" "%s"' % (resource, os.path.join(HERE, "keeper.rc")))
if rc.returncode != 0:
    print((rc.stdout + rc.stderr).strip()[-1500:])
    raise SystemExit("version resource not built")
dll = os.path.join(out, NAME + ".dll")
r = run('cl /nologo /O2 /MT /W4 /LD "%s" /Fo"%s" /Fe"%s" /link /DEF:"%s" /NOIMPLIB /NOEXP "%s" kernel32.lib advapi32.lib'
        % (os.path.join(HERE, "keeper.c"), os.path.join(out, "keeper.obj"), dll, os.path.join(HERE, "keeper.def"),
           resource))
ok = r.returncode == 0 and os.path.exists(dll)
print((r.stdout + r.stderr).strip()[-2500:])
print("%s.dll: build %s" % (NAME, "ok" if ok else "FAILED"))
raise SystemExit(0 if ok else 1)
