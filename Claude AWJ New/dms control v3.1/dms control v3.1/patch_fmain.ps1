# ═══════════════════════════════════════════════════════════════
#  K9 DMS FmMain.cs Bug Fix Patcher v2
#  Applies 12 fixes: disconnect cleanup + threading safety
#  
#  USAGE: Right-click this file > Run with PowerShell
#         Or: powershell -ExecutionPolicy Bypass -File patch_fmain.ps1
#
#  Place this file in the SAME FOLDER as FmMain.cs
# ═══════════════════════════════════════════════════════════════

$ErrorActionPreference = "Stop"

# Find FmMain.cs in current directory or script directory
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Get-Location }
$inputFile = Join-Path $scriptDir "FmMain.cs"

if (-not (Test-Path $inputFile)) {
    Write-Host ""
    Write-Host "ERROR: FmMain.cs not found!" -ForegroundColor Red
    Write-Host "Place this script in the same folder as FmMain.cs" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

# Read file preserving encoding (UTF-8 with BOM)
$content = [System.IO.File]::ReadAllText($inputFile)
$originalLength = $content.Length
$patchCount = 0
$failCount = 0

function Apply-Patch {
    param([string]$Find, [string]$Replace, [string]$Label)
    
    if ($script:content.Contains($Find)) {
        # Check it's unique
        $idx1 = $script:content.IndexOf($Find)
        $idx2 = $script:content.IndexOf($Find, $idx1 + 1)
        if ($idx2 -ge 0) {
            Write-Host "  WARN: $Label - multiple matches, patching first" -ForegroundColor Yellow
        }
        $script:content = $script:content.Remove($idx1, $Find.Length).Insert($idx1, $Replace)
        $script:patchCount++
        Write-Host "  OK: $Label" -ForegroundColor Green
        return $true
    } else {
        # Check if already patched
        if ($script:content.Contains($Replace)) {
            Write-Host "  SKIP: $Label (already applied)" -ForegroundColor Cyan
            return $true
        }
        Write-Host "  FAIL: $Label - target not found!" -ForegroundColor Red
        $script:failCount++
        return $false
    }
}

# Regex-based patch for anchors containing Cyrillic text
function Apply-RegexPatch {
    param([string]$Pattern, [string]$ReplaceCallback, [string]$Label)
    
    $regex = [regex]::new($Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $m = $regex.Match($script:content)
    if ($m.Success) {
        # Build replacement using the matched text
        $replacement = $ReplaceCallback -replace '\$0', $m.Value
        $script:content = $script:content.Remove($m.Index, $m.Length).Insert($m.Index, $replacement)
        $script:patchCount++
        Write-Host "  OK: $Label" -ForegroundColor Green
        return $true
    } else {
        Write-Host "  FAIL: $Label - pattern not found!" -ForegroundColor Red
        $script:failCount++
        return $false
    }
}

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  K9 DMS FmMain.cs Patcher v2 - 12 Bug Fixes" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Input: $inputFile"
Write-Host ""

# Detect line ending style
$nl = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }

# ─── EDIT 1: Add ltTxLock field ───
# Use regex to match across the Cyrillic comment between ltTxCommand and ltRxCommand
$edit1pattern = '(        List<Command> ltTxCommand;\r?\n)(        /// <summary>.*?</summary>\r?\n        List<Command> ltRxCommand;)'
$edit1check = "readonly object ltTxLock = new object();"
if ($content.Contains($edit1check)) {
    Write-Host "  SKIP: EDIT 1: Add ltTxLock field (already applied)" -ForegroundColor Cyan
} else {
    $regex1 = [regex]::new($edit1pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $m1 = $regex1.Match($content)
    if ($m1.Success) {
        $insertText = "        /// <summary> Lock object for thread-safe ltTxCommand access. </summary>${nl}        readonly object ltTxLock = new object();${nl}"
        $replacement1 = $m1.Groups[1].Value + $insertText + $m1.Groups[2].Value
        $content = $content.Remove($m1.Index, $m1.Length).Insert($m1.Index, $replacement1)
        $patchCount++
        Write-Host "  OK: EDIT 1: Add ltTxLock field" -ForegroundColor Green
    } else {
        Write-Host "  FAIL: EDIT 1: Add ltTxLock field - pattern not found!" -ForegroundColor Red
        $failCount++
    }
}

# ─── EDIT 2: Suppress timer1_Tick during jamming ───
$find2 = "        private void timer1_Tick(object sender, EventArgs e)${nl}        {${nl}            if (usbConnection != null)"
$repl2 = "        private void timer1_Tick(object sender, EventArgs e)${nl}        {${nl}            if (isJamming) return;${nl}${nl}            if (usbConnection != null)"
Apply-Patch $find2 $repl2 "EDIT 2: Suppress timer during jamming"

# ─── EDIT 3: Lock ltTxCommand in FuncusbConnectionThread ───
$find3 = "                            if (ltTxCommand.Count > 0)${nl}                            {${nl}                                writeEndpoint.Write(ltTxCommand[0].body, 3000, out bytesWritten);${nl}                                ThreadSafeDebugMessage(String.Format(`"[USB send]: {0:d} bytes\n`", bytesWritten));${nl}                                ltTxCommand.RemoveAt(0);${nl}                            }"
$repl3 = "                            lock (ltTxLock)${nl}                            {${nl}                                if (ltTxCommand.Count > 0)${nl}                                {${nl}                                    writeEndpoint.Write(ltTxCommand[0].body, 3000, out bytesWritten);${nl}                                    ThreadSafeDebugMessage(String.Format(`"[USB send]: {0:d} bytes\n`", bytesWritten));${nl}                                    ltTxCommand.RemoveAt(0);${nl}                                }${nl}                            }"
Apply-Patch $find3 $repl3 "EDIT 3: Lock USB send loop"

# ─── EDIT 4: Lock ltTxCommand.Count in WaitForCommandQueue ───
$find4 = "                bool txEmpty = (ltTxCommand.Count == 0);${nl}                bool seqEmpty;"
$repl4 = "                bool txEmpty;${nl}                lock (ltTxLock) { txEmpty = (ltTxCommand.Count == 0); }${nl}                bool seqEmpty;"
Apply-Patch $find4 $repl4 "EDIT 4: Lock WaitForCommandQueue"

# ─── EDIT 5: Lock ltTxCommand.Add in btnSaveToDevice_Click ───
$find5 = "                stopPacket[1] = (byte)((CMD_TDM_STOP >> 8) & 0xFF);${nl}                ltTxCommand.Add(new Command(stopPacket, cmdSize));${nl}                Thread.Sleep(200);"
$repl5 = "                stopPacket[1] = (byte)((CMD_TDM_STOP >> 8) & 0xFF);${nl}                lock (ltTxLock) { ltTxCommand.Add(new Command(stopPacket, cmdSize)); }${nl}                Thread.Sleep(200);"
Apply-Patch $find5 $repl5 "EDIT 5: Lock Add in btnSaveToDevice_Click"

# ─── EDIT 6: Lock ltTxCommand.Add in btnTdmStart_Click ───
$find6 = "            packet[1] = (byte)((CMD_TDM_START >> 8) & 0xFF);${nl}            ltTxCommand.Add(new Command(packet, cmdSize));${nl}${nl}            if (btnTdmStart != null) btnTdmStart.Enabled = false;"
$repl6 = "            packet[1] = (byte)((CMD_TDM_START >> 8) & 0xFF);${nl}            lock (ltTxLock) { ltTxCommand.Add(new Command(packet, cmdSize)); }${nl}${nl}            if (btnTdmStart != null) btnTdmStart.Enabled = false;"
Apply-Patch $find6 $repl6 "EDIT 6: Lock Add in btnTdmStart_Click"

# ─── EDIT 7: Lock ltTxCommand.Add in btnTdmStop_Click ───
$find7 = "            packet[1] = (byte)((CMD_TDM_STOP >> 8) & 0xFF);${nl}            ltTxCommand.Add(new Command(packet, cmdSize));${nl}${nl}            if (btnTdmStart != null) btnTdmStart.Enabled = true;"
$repl7 = "            packet[1] = (byte)((CMD_TDM_STOP >> 8) & 0xFF);${nl}            lock (ltTxLock) { ltTxCommand.Add(new Command(packet, cmdSize)); }${nl}${nl}            if (btnTdmStart != null) btnTdmStart.Enabled = true;"
Apply-Patch $find7 $repl7 "EDIT 7: Lock Add in btnTdmStop_Click"

# ─── EDIT 8: Add CleanupOnDisconnect() method ───
# Insert before FmMain_FormClosing - use regex to skip Cyrillic comment
$cleanupBody = @"
        /// <summary>
        /// Cleans up jamming state, command queues, and UI on disconnect.
        /// Called from manual disconnect, cable-pull, and guiActive loss paths.
        /// </summary>
        private void CleanupOnDisconnect()
        {
            // 1. Stop jamming immediately
            if (isJamming)
            {
                isJamming = false;
                Debug.WriteLine("CleanupOnDisconnect: isJamming cleared");
            }

            // 2. Mark GUI inactive to prevent new commands
            guiActive = false;

            // 3. Flush the TX command queue (prevent stale commands on reconnect)
            lock (ltTxLock)
            {
                ltTxCommand.Clear();
            }

            // 4. Restore UI to disconnected state (must run on UI thread)
            try
            {
                MethodInvoker uiCleanup = delegate
                {
                    SetControlsJammingMode(false);

                    if (btnGuardStart != null) btnGuardStart.Enabled = true;
                    if (btnGuardStop != null) btnGuardStop.Enabled = false;

                    if (ledConnection != null) ledConnection.BackColor = Color.Red;
                    if (lblConnectionStatus != null)
                    {
                        lblConnectionStatus.Text = "OFFLINE";
                        lblConnectionStatus.ForeColor = Color.FromArgb(255, 80, 80);
                    }

                    UpdateGuardStatus();
                };

                if (this.InvokeRequired)
                    this.Invoke(uiCleanup);
                else
                    uiCleanup();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CleanupOnDisconnect UI error: " + ex.Message);
            }
        }

"@

$edit8check = "private void CleanupOnDisconnect()"
if ($content.Contains($edit8check)) {
    Write-Host "  SKIP: EDIT 8: Add CleanupOnDisconnect() method (already applied)" -ForegroundColor Cyan
} else {
    # Match the summary comment (any language) before FmMain_FormClosing
    $edit8pattern = '(        /// <summary>.*?</summary>\r?\n        private void FmMain_FormClosing)'
    $regex8 = [regex]::new($edit8pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $m8 = $regex8.Match($content)
    if ($m8.Success) {
        $replacement8 = $cleanupBody + $m8.Value
        $content = $content.Remove($m8.Index, $m8.Length).Insert($m8.Index, $replacement8)
        $patchCount++
        Write-Host "  OK: EDIT 8: Add CleanupOnDisconnect() method" -ForegroundColor Green
    } else {
        Write-Host "  FAIL: EDIT 8 - FmMain_FormClosing not found!" -ForegroundColor Red
        $failCount++
    }
}

# ─── EDIT 9: Wire CleanupOnDisconnect into manual disconnect ───
$find9 = "                else${nl}                {${nl}                    if (usbConnection.Close() == false)${nl}                    {${nl}                        throw new Exception(`"Close connection error.`");${nl}                    }${nl}                    ConnectionControlGUI(false, run_cfg);${nl}                }"
$repl9 = "                else${nl}                {${nl}                    CleanupOnDisconnect();${nl}                    if (usbConnection.Close() == false)${nl}                    {${nl}                        throw new Exception(`"Close connection error.`");${nl}                    }${nl}                    ConnectionControlGUI(false, run_cfg);${nl}                }"
Apply-Patch $find9 $repl9 "EDIT 9: Wire cleanup into manual disconnect"

# ─── EDIT 10: Wire CleanupOnDisconnect into cable-pull ───
$find10 = "                            if (usbConnection.UsbRegistryInfo.IsAlive == false)${nl}                            {${nl}                                ConnectionControlGUI(false, run_cfg);${nl}                                usbConnection.Close();"
$repl10 = "                            if (usbConnection.UsbRegistryInfo.IsAlive == false)${nl}                            {${nl}                                CleanupOnDisconnect();${nl}                                ConnectionControlGUI(false, run_cfg);${nl}                                usbConnection.Close();"
Apply-Patch $find10 $repl10 "EDIT 10: Wire cleanup into cable-pull"

# ─── EDIT 11: Wire CleanupOnDisconnect into guiActive disconnect ───
$find11 = "                        else if (guiActive == true)${nl}                        {${nl}                            ConnectionControlGUI(false, run_cfg);${nl}                            usbConnection.Close();${nl}                        }"
$repl11 = "                        else if (guiActive == true)${nl}                        {${nl}                            CleanupOnDisconnect();${nl}                            ConnectionControlGUI(false, run_cfg);${nl}                            usbConnection.Close();${nl}                        }"
Apply-Patch $find11 $repl11 "EDIT 11: Wire cleanup into guiActive disconnect"

# ─── EDIT 12: Re-enable START button on reconnect ───
$find12 = "                // Update connection LED${nl}                if (ledConnection != null) { ledConnection.BackColor = Color.Lime; }"
$repl12 = "                // Re-enable START button (may have been disabled by previous disconnect)${nl}                if (btnGuardStart != null) btnGuardStart.Enabled = true;${nl}                if (btnGuardStop != null) btnGuardStop.Enabled = false;${nl}${nl}                // Update connection LED${nl}                if (ledConnection != null) { ledConnection.BackColor = Color.Lime; }"
Apply-Patch $find12 $repl12 "EDIT 12: Re-enable START on reconnect"

# ═══ Write output ═══
$backupFile = Join-Path $scriptDir "FmMain_BACKUP.cs"
$outputFile = $inputFile

# Create backup
Copy-Item $inputFile $backupFile -Force
Write-Host ""
Write-Host "Backup saved: $backupFile" -ForegroundColor DarkGray

# Write patched file
[System.IO.File]::WriteAllText($outputFile, $content)

$newLength = $content.Length
Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  RESULTS: $patchCount patches applied, $failCount failed" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host "  Original: $originalLength chars" -ForegroundColor DarkGray
Write-Host "  Patched:  $newLength chars (+$($newLength - $originalLength))" -ForegroundColor DarkGray
Write-Host "  Output:   $outputFile" -ForegroundColor White
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "WARNING: $failCount patch(es) failed! Check output above." -ForegroundColor Red
    Write-Host "The backup file has your original: $backupFile" -ForegroundColor Yellow
} else {
    Write-Host "All patches applied successfully!" -ForegroundColor Green
    Write-Host "Open FmMain.cs in Visual Studio and rebuild." -ForegroundColor White
}

Write-Host ""
Read-Host "Press Enter to exit"
