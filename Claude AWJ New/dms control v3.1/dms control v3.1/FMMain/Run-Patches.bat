@echo off
title K9 Electronics - DMS Patch Installer
color 0A
echo.
echo  ============================================
echo   K9 ELECTRONICS - DMS PARAMETER PATCH
echo   Applying fixes to FmMain.cs
echo  ============================================
echo.

REM ── Check FmMain.cs exists ──
if not exist "FmMain.cs" (
    color 0C
    echo  ERROR: FmMain.cs not found in this folder!
    echo.
    echo  Make sure this .bat file is in the SAME folder as:
    echo    - FmMain.cs
    echo    - Apply-Patches.ps1
    echo    - Apply-DDS-Fix.ps1
    echo.
    pause
    exit /b 1
)

REM ── Check both scripts exist ──
if not exist "Apply-Patches.ps1" (
    color 0C
    echo  ERROR: Apply-Patches.ps1 not found!
    pause
    exit /b 1
)
if not exist "Apply-DDS-Fix.ps1" (
    color 0C
    echo  ERROR: Apply-DDS-Fix.ps1 not found!
    pause
    exit /b 1
)

REM ── Backup original ──
if not exist "FmMain_BACKUP.cs" (
    echo  Backing up original FmMain.cs ...
    copy "FmMain.cs" "FmMain_BACKUP.cs" >nul
    echo  ✓ Backup saved as FmMain_BACKUP.cs
    echo.
)

REM ── Run Script 1: State Management Patches ──
echo  STEP 1/2: Applying state management patches...
echo  ─────────────────────────────────────────────
powershell -ExecutionPolicy Bypass -File "Apply-Patches.ps1" -InputFile "FmMain.cs"

if not exist "FmMain_patched.cs" (
    color 0C
    echo.
    echo  ERROR: Script 1 failed - FmMain_patched.cs was not created!
    echo  Check the error messages above.
    pause
    exit /b 1
)
echo.
echo  ✓ Script 1 complete - FmMain_patched.cs created
echo.

REM ── Run Script 2: DDS Per-Band Fix ──
echo  STEP 2/2: Applying DDS per-band patches...
echo  ─────────────────────────────────────────────
powershell -ExecutionPolicy Bypass -File "Apply-DDS-Fix.ps1" -InputFile "FmMain_patched.cs"

if not exist "FmMain_patched_ddsfix.cs" (
    color 0C
    echo.
    echo  ERROR: Script 2 failed - FmMain_patched_ddsfix.cs was not created!
    echo  Check the error messages above.
    pause
    exit /b 1
)
echo.
echo  ✓ Script 2 complete - FmMain_patched_ddsfix.cs created
echo.

REM ── Replace original with patched version ──
echo  Replacing FmMain.cs with fully patched version...
copy /Y "FmMain_patched_ddsfix.cs" "FmMain.cs" >nul
echo.

color 0A
echo  ============================================
echo   ALL PATCHES APPLIED SUCCESSFULLY
echo  ============================================
echo.
echo   Files in this folder:
echo     FmMain_BACKUP.cs          (your original)
echo     FmMain_patched.cs         (after script 1)
echo     FmMain_patched_ddsfix.cs  (after script 2)
echo     FmMain.cs                 (final patched version)
echo.
echo   Copy FmMain.cs back into your Visual Studio
echo   project folder and rebuild.
echo.
echo  ============================================
pause
