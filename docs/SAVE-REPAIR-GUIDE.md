# Fixing a "A problem occured while trying to load the saved game" save (Windows)

**What happened:** a bug in TAOM v2.0.9's War of the Ring tracker made the game write a broken value into your save once a campaign got large (usually around in-game day ~50). Every save made after that point fails to load with the error dialog. **This is fixed in the current update** — but if you have older saves that already won't load, this guide recovers them.

**Good news:** your campaign is fine. The broken part is only the cosmetic war-progress meter's history. The repair resets that one thing and leaves your heroes, parties, settlements, and everything else untouched. The repaired save loads normally.

There are two ways to run the repair. **PowerShell (Option A) needs nothing installed** — it's built into Windows 10 and 11 — so try that first. Python (Option B) is there if you'd rather.

---

## Where your saves are

Your Bannerlord saves live in one of these folders. Open **File Explorer** and paste the path into the address bar:

```
%USERPROFILE%\Documents\Mount and Blade II Bannerlord\Game Saves
```

If that folder doesn't exist (some PCs redirect Documents to OneDrive), try:

```
%USERPROFILE%\OneDrive\Documents\Mount and Blade II Bannerlord\Game Saves
```

You should see your `.sav` files there (e.g. `Elves209.sav`, `main_campaign.sav`, `save_5.sav`).

---

## Option A — PowerShell (recommended, nothing to install)

1. **Get the script** — download `repair_sav_strings.ps1` (the mod author will provide the file) and **put it in your Game Saves folder**, next to your `.sav` files.

2. **Open PowerShell in that folder** — in File Explorer, hold **Shift** and right-click an empty area of the Game Saves folder, then choose **"Open PowerShell window here"** (on some PCs it's "Open in Terminal"). A blue window opens, already pointed at your saves.

3. **Check the save first** (safe, changes nothing). Type this, replacing `YourSave` with your save's name:
   ```
   powershell -ExecutionPolicy Bypass -File .\repair_sav_strings.ps1 "YourSave.sav"
   ```
   If the save has the bug, you'll see a line like:
   ```
   Found 1 oversized entry ... [MOMENTUM war-tracker string]
   ```
   (If it says *"NOT hit by the momentum bug"*, that save fails for a different reason — send it to the mod author.)

4. **Repair it.** Run the same command with `-Repair` added:
   ```
   powershell -ExecutionPolicy Bypass -File .\repair_sav_strings.ps1 "YourSave.sav" -Repair
   ```
   It may take up to a minute on a large campaign — wait for the `REPAIRED ->` line. It writes a new file next to the original: **`YourSave_fixed.sav`**. Your original is not touched.

5. **Load `YourSave_fixed.sav` in the game** (see *Loading the fixed save* below).

---

## Option B — Python (alternative)

1. **Install Python** (~5 min): go to <https://www.python.org/downloads/>, click **Download Python**, run the installer, and **tick "Add Python to PATH"** on the first screen before clicking Install.
2. **Get the script** `repair_sav_strings.py` and put it in your Game Saves folder.
3. **Open a terminal there:** in File Explorer, click the address bar, type `cmd`, press **Enter**.
4. **Check, then repair:**
   ```
   python repair_sav_strings.py "YourSave.sav"
   python repair_sav_strings.py "YourSave.sav" --repair
   ```
   This writes **`YourSave_fixed.sav`** next to the original.

---

## Loading the fixed save

Start Bannerlord with the **exact same mods enabled** as when the save was made, go to **Load Game**, and pick the `_fixed` save. It should load normally. The War of the Ring meter starts fresh and rebuilds itself as the war continues — nothing else is lost.

---

## Troubleshooting

- **The name has spaces** (e.g. `main campaign.sav`) → keep the quotes around it, exactly as shown.
- **You didn't put the script in the saves folder** → give both full paths instead. PowerShell:
  ```
  powershell -ExecutionPolicy Bypass -File "C:\path\to\repair_sav_strings.ps1" "C:\...\Game Saves\YourSave.sav" -Repair
  ```
- **Keep your original** until you've confirmed the `_fixed` save loads. If anything looks wrong, the original is still there.
- **The `_fixed` save fails to load too** → the mod list at save time must match. If the save was made with extra add-ons (e.g. TAOMTweaks, TAOMFixes), enable those same versions before loading. If it still fails, it's a different issue — send the save to the mod author.
- **(Python only) `'python' is not recognized...`** → Python isn't on PATH. Re-run the Python installer, choose *Modify*, tick "Add Python to PATH" (or reinstall with that box checked), then close and reopen the terminal.

---

*Technical details of the bug and fix: `docs/reviews/rca-momentum-save-corruption-2026-07-07.md`. To prevent it going forward, update to the current TAOM version — new saves stay safe automatically.*
