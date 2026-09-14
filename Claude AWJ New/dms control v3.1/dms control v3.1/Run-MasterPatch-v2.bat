@echo off
title K9 Electronics - DMS Master Patch v2
color 0A
echo.
echo  ============================================
echo   K9 DMS - MASTER PATCH v2
echo  ============================================
echo.
echo  IMPORTANT: Start from FmMain_ORIGINAL.cs
echo  Copy FmMain_ORIGINAL.cs to FmMain.cs first!
echo.

if not exist "FmMain.cs" (
    echo  ERROR: FmMain.cs not found!
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

echo.
echo  -- Step 1/2: Speed + Stop + Dwell + Hits/sec --
echo.
%PYTHON% apply_final_fix.py
if %errorlevel% neq 0 ( echo FAILED at step 1 & pause & exit /b 1 )

echo.
echo  -- Step 2/2: DDS Output + Status Display --
echo.
%PYTHON% apply_output_fix.py
if %errorlevel% neq 0 ( echo FAILED at step 2 & pause & exit /b 1 )

echo.
echo  ============================================
echo   ALL DONE - Copy FmMain.cs to VS + rebuild
echo  ============================================
pause
