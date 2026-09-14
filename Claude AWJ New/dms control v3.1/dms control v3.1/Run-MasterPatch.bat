@echo off
title K9 Electronics - DMS Master Patch
color 0A
echo.
echo  ============================================
echo   K9 DMS - MASTER PATCH (all fixes)
echo  ============================================
echo.
echo  Applies in order:
echo    1. Queue sync + UI fixes
echo    2. Speed + stop fix
echo    3. Hits/second display
echo.

if not exist "FmMain.cs" (
    echo  ERROR: FmMain.cs not found!
    pause
    exit /b 1
)

:: Find Python
set PYTHON=
where python >nul 2>&1 && set PYTHON=python
if "%PYTHON%"=="" where python3 >nul 2>&1 && set PYTHON=python3
if "%PYTHON%"=="" where py >nul 2>&1 && set PYTHON=py
if "%PYTHON%"=="" (
    echo  ERROR: Python not found!
    echo  Install from https://www.python.org/downloads/
    pause
    exit /b 1
)

:: Backup original
if not exist "FmMain_ORIGINAL.cs" copy "FmMain.cs" "FmMain_ORIGINAL.cs" >nul

:: Step 1
echo.
echo  ── Step 1/3: Queue Sync + UI Fixes ──
echo.
if not exist "apply_all_fixes.py" ( echo  SKIP: apply_all_fixes.py not found & goto :step2 )
%PYTHON% apply_all_fixes.py
if %errorlevel% neq 0 ( echo  FAILED at step 1 & pause & exit /b 1 )

:step2
echo.
echo  ── Step 2/3: Speed + Stop Fix ──
echo.
if not exist "apply_speed_fix.py" ( echo  SKIP: apply_speed_fix.py not found & goto :step3 )
%PYTHON% apply_speed_fix.py
if %errorlevel% neq 0 ( echo  FAILED at step 2 & pause & exit /b 1 )

:step3
echo.
echo  ── Step 3/3: Hits/Second Display ──
echo.
if not exist "apply_hits_display.py" ( echo  SKIP: apply_hits_display.py not found & goto :done )
%PYTHON% apply_hits_display.py
if %errorlevel% neq 0 ( echo  FAILED at step 3 & pause & exit /b 1 )

:done
echo.
echo  ============================================
echo   ALL PATCHES APPLIED SUCCESSFULLY
echo  ============================================
echo.
echo   Files:
echo     FmMain_ORIGINAL.cs   (your untouched backup)
echo     FmMain.cs            (fully patched - copy to VS)
echo.
echo   Copy FmMain.cs to your Visual Studio
echo   project folder and rebuild.
echo.
echo  ============================================
pause
