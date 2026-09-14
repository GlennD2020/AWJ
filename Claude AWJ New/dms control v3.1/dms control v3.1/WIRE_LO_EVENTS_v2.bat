@echo off
echo K9 Electronics - Wire Up LO TextBox Events
echo.
python "%~dp0wire_lo_events.py" %1
if errorlevel 1 (
    echo.
    echo If Python is not installed, open FmMain.cs in Visual Studio
    echo and manually paste the code from LO_COMPLETE_FIX.cs
    echo after the line: grbxLO.Width = Math.Min(grbxLO.Width, 200);
)
pause
