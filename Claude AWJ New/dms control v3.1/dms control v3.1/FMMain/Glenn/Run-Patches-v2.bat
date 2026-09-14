@echo off
title K9 Electronics - DMS Patch Installer v2
color 0A
echo.
echo  ============================================
echo   K9 ELECTRONICS - DMS PATCH INSTALLER v2
echo   Direct Python patching (no PowerShell)
echo  ============================================
echo.

REM ── Check FmMain.cs exists ──
if not exist "FmMain.cs" (
    color 0C
    echo  ERROR: FmMain.cs not found in this folder!
    echo.
    echo  Put this .bat file and apply_patches.py in the
    echo  SAME folder as your FmMain.cs
    echo.
    pause
    exit /b 1
)

if not exist "apply_patches.py" (
    color 0C
    echo  ERROR: apply_patches.py not found!
    echo  Download it from Claude and put it here.
    pause
    exit /b 1
)

REM ── Backup original ──
if not exist "FmMain_BACKUP.cs" (
    echo  Backing up FmMain.cs ...
    copy "FmMain.cs" "FmMain_BACKUP.cs" >nul
    echo  Backup saved as FmMain_BACKUP.cs
    echo.
)

REM ── Try Python ──
echo  Running patches...
echo  ─────────────────────────────────────────────
echo.

python apply_patches.py "FmMain.cs" "FmMain_patched_final.cs" 2>nul
if %ERRORLEVEL% EQU 0 goto :success

python3 apply_patches.py "FmMain.cs" "FmMain_patched_final.cs" 2>nul
if %ERRORLEVEL% EQU 0 goto :success

py apply_patches.py "FmMain.cs" "FmMain_patched_final.cs" 2>nul
if %ERRORLEVEL% EQU 0 goto :success

REM ── Python not found ──
color 0C
echo.
echo  ERROR: Python not found!
echo.
echo  Install Python from: https://www.python.org/downloads/
echo  During install, CHECK "Add Python to PATH"
echo  Then run this batch file again.
echo.
pause
exit /b 1

:success
echo.
if not exist "FmMain_patched_final.cs" (
    color 0C
    echo  ERROR: Patched file was not created!
    pause
    exit /b 1
)

REM ── Replace original ──
echo  Replacing FmMain.cs with patched version...
copy /Y "FmMain_patched_final.cs" "FmMain.cs" >nul
echo.

color 0A
echo  ============================================
echo   ALL PATCHES APPLIED SUCCESSFULLY
echo  ============================================
echo.
echo   Files:
echo     FmMain_BACKUP.cs          (your original)
echo     FmMain_patched_final.cs   (patched version)
echo     FmMain.cs                 (ready for VS)
echo.
echo   Copy FmMain.cs into your Visual Studio
echo   project folder and rebuild.
echo.
echo  ============================================
pause
