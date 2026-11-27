"""Builds out/xinput9_1_0.dll, the Dinen start-up proxy for the Modding Kit (#1629), from dinen_proxy.c, .def and .rc.
Needs the MSVC x64 build tools (found through vswhere, or VCVARS64 set to a vcvars64.bat).

Usage:
  python build.py              build only
  python build.py --install    build, then copy the DLL into <game>\\bin\\Win64_Shipping_wEditor

The game folder is BANNERLORD_GAME_DIR, else the Steam path of the desktop. The install refuses to replace an
xinput9_1_0.dll that is not a Dinen build (another mod's proxy), and it never touches bin\\Win64_Shipping_Client.
The build steps follow Native/ShaderCacheKeeper/build.py (MIT, (c) 2026 yotthani)."""
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
VSWHERE = os.path.expandvars(r"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe")
NAME = "xinput9_1_0"
MARKER = "TAOM Dinen".encode("utf-16-le")
DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"


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
    raise SystemExit("no MSVC x64 tools found: install them or set VCVARS64 to vcvars64.bat")


def run(vcvars, command):
    return subprocess.run('cmd /c ""%s" >nul 2>&1 && %s"' % (vcvars, command), capture_output=True, text=True,
                          shell=True)


def build():
    vcvars = find_vcvars()
    out = os.path.join(HERE, "out")
    os.makedirs(out, exist_ok=True)
    resource = os.path.join(out, "dinen_proxy.res")
    rc = run(vcvars, 'rc /nologo /fo "%s" "%s"' % (resource, os.path.join(HERE, "dinen_proxy.rc")))
    if rc.returncode != 0:
        print((rc.stdout + rc.stderr).strip()[-1500:])
        raise SystemExit("version resource not built")
    dll = os.path.join(out, NAME + ".dll")
    r = run(vcvars, 'cl /nologo /O2 /MT /W4 /WX /LD "%s" /Fo"%s" /Fe"%s" /link /DEF:"%s" /NOIMPLIB /NOEXP "%s" kernel32.lib user32.lib'
            % (os.path.join(HERE, "dinen_proxy.c"), os.path.join(out, "dinen_proxy.obj"), dll,
               os.path.join(HERE, "dinen_proxy.def"), resource))
    ok = r.returncode == 0 and os.path.exists(dll)
    print((r.stdout + r.stderr).strip()[-2500:])
    print("%s.dll: build %s" % (NAME, "ok" if ok else "FAILED"))
    if not ok:
        raise SystemExit(1)
    return dll


def install(dll):
    game = os.environ.get("BANNERLORD_GAME_DIR") or DEFAULT_GAME
    target_dir = os.path.join(game, "bin", "Win64_Shipping_wEditor")
    if not os.path.isfile(os.path.join(target_dir, "TaleWorlds.Native.dll")):
        raise SystemExit("no Modding Kit bin at %s" % target_dir)
    target = os.path.join(target_dir, NAME + ".dll")
    if os.path.exists(target):
        with open(target, "rb") as existing:
            if MARKER not in existing.read():
                raise SystemExit("%s is not a Dinen build: not replaced" % target)
    try:
        shutil.copyfile(dll, target)
    except PermissionError:
        raise SystemExit("%s is in use: close the Modding Kit, then install again" % target)
    print("installed %s" % target)


if __name__ == "__main__":
    built = build()
    if "--install" in sys.argv[1:]:
        install(built)
