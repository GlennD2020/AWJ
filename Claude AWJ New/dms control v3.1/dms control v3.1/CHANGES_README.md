# K9 Electronics — FmMain.cs Conflict Resolution
## DMS Control v3 — LO/DDS/Table Parameter Ownership Fix

**Date:** 2026-02-09  
**Applies to:** FmMain.cs (dms_control_v3)

---

## Problem Summary

Three conflicts between left-side controls and the band table:

| # | Conflict | Root Cause | Impact |
|---|----------|------------|--------|
| 1 | Centre Freq vs Table Start MHz | Both write `device.lo.pll.OutFrequency` | LO stuck on last hop after jamming stops |
| 2 | DDS params vs Table BW/Points | Both write `device.dds.*` | User DDS settings destroyed by jamming |
| 3 | Two BW columns in grid | Designer creates `colBwMhz`, runtime adds `colBandwidthMhz` | Only right column works; left is dead |

---

## Solution Architecture

**During normal operation:** Left-side controls are the master — they control LO and DDS as before.

**When jamming starts:**
1. Current LO + DDS state is saved
2. Left-side controls become read-only (yellow background)
3. Title bar shows `[JAMMING]` indicator
4. Table becomes the band plan authority
5. Left-side fields show LIVE state (current hop freq, DDS config)

**When jamming stops (or errors):**
1. Saved LO + DDS state is restored to hardware
2. Left-side controls become editable again
3. Display refreshes to pre-jamming values

---

## Patches Applied (12 total)

### PATCH 1 — Saved State Fields
**Location:** After `#region DIAGNOSTICS_FIELDS` ... `#endregion`  
**What:** Adds fields to hold pre-jamming LO and DDS values.

```csharp
#region JAMMING_STATE_FIELDS
private double savedLoFrequency = 0;
private float savedLoStep = 0;
private uint savedLoPoints = 0;
private float savedLoHoldTime = 0;
private int savedLoWaitLD = 0;
private int savedLoSweepOn = 0;
private ushort savedDdsSweepOn = 0;
private double savedDdsStart = 0;
private double savedDdsStep = 0;
private ulong savedDdsPoints = 0;
private float savedDdsFreqCtrl = 0;
private ushort savedDdsTwMem = 0;
private bool controlsLockedForJamming = false;
#endregion
```

### PATCH 2 — Hide Duplicate BW Column
**Location:** Top of `EnsureGridColumns()`  
**What:** Hides the Designer's `colBwMhz` and `colStepKhz` columns, keeping only the runtime `colBandwidthMhz` that jamming actually reads.

### PATCH 3 — Save/Restore/Lock/Unlock Methods
**Location:** New `#region JAMMING_STATE_MANAGEMENT` before `GUARD_BAND_JAMMING`  
**What:** Six new methods:

| Method | Purpose |
|--------|---------|
| `SaveHardwareState()` | Snapshots all LO + DDS values |
| `RestoreHardwareState()` | Writes saved values back to device + sends to hardware |
| `SetControlsJammingMode(bool)` | Locks/unlocks left-side controls, sets yellow background |
| `UpdateLiveLoDisplay(double MHz)` | Updates Centre Freq field during hops |
| `UpdateLiveDdsDisplay(bw, pts, fc)` | Updates DDS fields when jamming configures waveform |

### PATCH 4 — START Button Saves State
**Location:** `btnGuardStart_Click`, before thread launch  
**What:** Calls `SaveHardwareState()` and `SetControlsJammingMode(true)`.

### PATCH 5 — STOP Button Restores State
**Location:** `btnGuardStop_Click`  
**What:** Calls `RestoreHardwareState()` and `SetControlsJammingMode(false)`.

### PATCH 6 — Live LO Display During Hops
**Location:** End of `HopToFrequency()`  
**What:** Calls `UpdateLiveLoDisplay(frequencyMHz)` so left-side Centre Freq shows the current band.

### PATCH 7 — Live DDS Display During Jamming
**Location:** End of `ConfigureDDSForJamming()`  
**What:** Calls `UpdateLiveDdsDisplay()` so left-side BW/Points/FreqCtrl show jamming values.

### PATCH 8 — Restore State on Completion/Error
**Location:** `ExecuteGuardBandJamming` cleanup blocks  
**What:** Both normal completion and error paths call `RestoreHardwareState()` + `SetControlsJammingMode(false)`.

### PATCHES 9-12 — Input Guards
**Location:** All left-side input handlers  
**What:** Each handler checks `controlsLockedForJamming` and blocks edits during jamming:

- `LoParameters_KeyPress` (PATCH 9)
- `DdsParameters_KeyPress` (PATCH 10)
- `cmbbxDdsMode_SelectedIndexChanged` (PATCH 11)
- `cmbbxLoPower_SelectedIndexChanged` (PATCH 12)

---

## Usage

Open PowerShell in the folder containing FmMain.cs, then run:

```powershell
.\Apply-Patches.ps1 -InputFile "FmMain.cs"
```

Output: `FmMain_patched.cs` (same folder)

If you get an execution policy error:
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Apply-Patches.ps1 -InputFile "FmMain.cs"
```

---

## Testing Checklist

- [ ] Load bands, START JAMMING — left-side fields go yellow + read-only
- [ ] During jamming — Centre Freq updates to show each hop frequency
- [ ] During jamming — DDS Bandwidth shows jamming BW
- [ ] STOP — all fields return to pre-jamming values
- [ ] STOP — LO returns to original frequency
- [ ] STOP — DDS mode returns to original mode
- [ ] Error during jamming — state still restored
- [ ] Only one BW column visible in grid (right side `colBandwidthMhz`)
- [ ] Cannot type into left-side fields during jamming
- [ ] Cannot change DDS mode dropdown during jamming
- [ ] Title bar shows [JAMMING] during operation
