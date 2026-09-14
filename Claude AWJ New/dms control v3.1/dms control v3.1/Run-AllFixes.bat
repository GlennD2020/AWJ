@echo off
title K9 Electronics - DMS Comprehensive Fix v2
color 0A
echo.
echo  ============================================
echo   K9 DMS - Comprehensive Fix v2
echo  ============================================
echo.
echo  Fixes:
echo    - SA shows no change during jamming (queue sync)
echo    - Grid resizes with window width
echo    - CLEAR TABLE button added
echo    - Active checkbox now filters bands
echo    - Removed unused columns (Step KHz, Loop, duplicate BW)
echo    - Fixed band values going into wrong columns
echo.

if not exist "FmMain.cs" (
    echo  ERROR: FmMain.cs not found!
    echo  Place this file in the same folder as FmMain.cs
    pause
    exit /b 1
)

if not exist "apply_all_fixes.py" (
    echo  ERROR: apply_all_fixes.py not found!
    pause
    exit /b 1
)

echo  Running patch...
echo.

python apply_all_fixes.py
if %errorlevel% equ 0 goto :success

python3 apply_all_fixes.py
if %errorlevel% equ 0 goto :success

py apply_all_fixes.py
if %errorlevel% equ 0 goto :success

echo.
echo  ERROR: Python not found!
echo  Install from https://www.python.org/downloads/
pause
exit /b 1

:success
echo.
echo  ============================================
echo   ALL FIXES APPLIED SUCCESSFULLY
echo  ============================================
echo.
echo   Copy FmMain.cs to your Visual Studio
echo   project folder and rebuild.
echo.
echo  ============================================
pause
