@echo off
title K9 Electronics - Final Fix
color 0A
echo.
echo  ============================================
echo   K9 DMS - Final Fix
echo   (Speed + Stop + Hits/sec display)
echo  ============================================
echo.
echo  IMPORTANT: Start from FmMain_ORIGINAL.cs
echo.
echo  Step 1: Copy FmMain_ORIGINAL.cs to FmMain.cs
echo  Step 2: Run this batch file
echo  Step 3: Copy FmMain.cs to Visual Studio
echo.

if not exist "FmMain.cs" (
    echo  ERROR: FmMain.cs not found!
    echo  Copy FmMain_ORIGINAL.cs to FmMain.cs first.
    pause
    exit /b 1
)

if not exist "apply_final_fix.py" (
    echo  ERROR: apply_final_fix.py not found!
    pause
    exit /b 1
)

set PYTHON=
where python >nul 2>&1 && set PYTHON=python
if "%PYTHON%"=="" where python3 >nul 2>&1 && set PYTHON=python3
if "%PYTHON%"=="" where py >nul 2>&1 && set PYTHON=py
if "%PYTHON%"=="" (
    echo  ERROR: Python not found!
    pause
    exit /b 1
)

%PYTHON% apply_final_fix.py
if %errorlevel% neq 0 ( pause & exit /b 1 )

echo.
echo  ============================================
echo   DONE - Copy FmMain.cs to VS and rebuild
echo  ============================================
pause
