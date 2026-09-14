@echo off
echo ══════════════════════════════════════════════════════
echo   K9 Electronics - TDM Standalone Buttons Patch
echo ══════════════════════════════════════════════════════
echo.
echo This will add the following to FmMain.cs:
echo   - UPLOAD TO BOARD button  (sends bands to STM32)
echo   - SAVE TO EEPROM button   (persists for standalone)
echo   - LOAD EEPROM button      (reload saved config)
echo   - START TDM button        (start standalone hopping)
echo   - STOP TDM button         (stop standalone hopping)
echo   - Dwell time control      (ms per band hop)
echo   - Status display
echo.
echo Place this file and apply_tdm_buttons.py in the same
echo folder as your FmMain.cs, then run this batch file.
echo.
pause
python apply_tdm_buttons.py
echo.
pause
