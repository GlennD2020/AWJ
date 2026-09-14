#!/usr/bin/env python3
"""
K9 DMS Control v3 - Bug Fix Patcher
Applies Bug #1 (Disconnect Cleanup) and Bug #2 (Thread Safety) fixes.
"""

import sys
import os

# Read the original file from stdin or argument
if len(sys.argv) > 1:
    with open(sys.argv[1], 'r', encoding='utf-8-sig') as f:
        code = f.read()
else:
    code = sys.stdin.read()

patches_applied = 0

def apply_patch(code, old, new, description):
    global patches_applied
    if old not in code:
        print(f"  WARNING: Could not find target for: {description}", file=sys.stderr)
        return code
    count = code.count(old)
    if count > 1:
        print(f"  WARNING: Found {count} matches for: {description} (applying to first)", file=sys.stderr)
        # Replace only first occurrence
        code = code.replace(old, new, 1)
    else:
        code = code.replace(old, new)
    patches_applied += 1
    print(f"  ✓ {description}", file=sys.stderr)
    return code

print("═══ K9 DMS Control v3 - Applying Bug Fixes ═══", file=sys.stderr)
print("", file=sys.stderr)

# ══════════════════════════════════════════════════════════════
# BUG #2 FIX 1: Add ltTxLock field declaration
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '        /// <summary> Список передаваемых команд. </summary>\n        List<Command> ltTxCommand;',
    '        /// <summary> Список передаваемых команд. </summary>\n        List<Command> ltTxCommand;\n        /// <summary> Lock object for thread-safe ltTxCommand access. </summary>\n        private readonly object ltTxLock = new object();',
    "BUG#2: Add ltTxLock field declaration"
)

# ══════════════════════════════════════════════════════════════
# BUG #2 FIX 2: Suppress timer1_Tick during jamming
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '''        private void timer1_Tick(object sender, EventArgs e)
        {
            if (usbConnection != null)''',
    '''        private void timer1_Tick(object sender, EventArgs e)
        {
            // BUG#2 FIX: Don't queue polling commands while jamming thread is active
            if (isJamming) return;

            if (usbConnection != null)''',
    "BUG#2: Suppress timer1_Tick during jamming"
)

# ══════════════════════════════════════════════════════════════
# BUG #2 FIX 3: Lock ltTxCommand in FuncusbConnectionThread
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '''                            if (ltTxCommand.Count > 0)
                            {
                                writeEndpoint.Write(ltTxCommand[0].body, 3000, out bytesWritten);
                                ThreadSafeDebugMessage(String.Format("[USB send]: {0:d} bytes\\n", bytesWritten));
                                ltTxCommand.RemoveAt(0);
                            }''',
    '''                            // BUG#2 FIX: Lock ltTxCommand for thread-safe access
                            Command cmdToSend = null;
                            lock (ltTxLock)
                            {
                                if (ltTxCommand.Count > 0)
                                {
                                    cmdToSend = ltTxCommand[0];
                                    ltTxCommand.RemoveAt(0);
                                }
                            }
                            if (cmdToSend != null)
                            {
                                writeEndpoint.Write(cmdToSend.body, 3000, out bytesWritten);
                                ThreadSafeDebugMessage(String.Format("[USB send]: {0:d} bytes\\n", bytesWritten));
                            }''',
    "BUG#2: Lock ltTxCommand in FuncusbConnectionThread"
)

# ══════════════════════════════════════════════════════════════
# BUG #2 FIX 4: Lock WaitForCommandQueue ltTxCommand.Count check
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '''            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                bool txEmpty = (ltTxCommand.Count == 0);''',
    '''            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                // BUG#2 FIX: Lock ltTxCommand for thread-safe Count check
                bool txEmpty;
                lock (ltTxLock) { txEmpty = (ltTxCommand.Count == 0); }''',
    "BUG#2: Lock WaitForCommandQueue ltTxCommand.Count"
)

# ══════════════════════════════════════════════════════════════
# BUG #2 FIX 5: Lock direct ltTxCommand.Add() in btnSaveToDevice_Click (STOP packet)
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '''                stopPacket[0] = (byte)(CMD_TDM_STOP & 0xFF);
                stopPacket[1] = (byte)((CMD_TDM_STOP >> 8) & 0xFF);
                ltTxCommand.Add(new Command(stopPacket, cmdSize));
                Thread.Sleep(200);''',
    '''                stopPacket[0] = (byte)(CMD_TDM_STOP & 0xFF);
                stopPacket[1] = (byte)((CMD_TDM_STOP >> 8) & 0xFF);
                // BUG#2 FIX: Lock direct Add() to prevent thread conflicts
                lock (ltTxLock) { ltTxCommand.Add(new Command(stopPacket, cmdSize)); }
                Thread.Sleep(200);''',
    "BUG#2: Lock ltTxCommand.Add in btnSaveToDevice_Click"
)

# ══════════════════════════════════════════════════════════════
# BUG #2 FIX 6: Lock direct ltTxCommand.Add() in btnTdmStart_Click
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '''            packet[0] = (byte)(CMD_TDM_START & 0xFF);
            packet[1] = (byte)((CMD_TDM_START >> 8) & 0xFF);
            ltTxCommand.Add(new Command(packet, cmdSize));

            if (btnTdmStart != null) btnTdmStart.Enabled = false;''',
    '''            packet[0] = (byte)(CMD_TDM_START & 0xFF);
            packet[1] = (byte)((CMD_TDM_START >> 8) & 0xFF);
            // BUG#2 FIX: Lock direct Add() to prevent thread conflicts
            lock (ltTxLock) { ltTxCommand.Add(new Command(packet, cmdSize)); }

            if (btnTdmStart != null) btnTdmStart.Enabled = false;''',
    "BUG#2: Lock ltTxCommand.Add in btnTdmStart_Click"
)

# ══════════════════════════════════════════════════════════════
# BUG #2 FIX 7: Lock direct ltTxCommand.Add() in btnTdmStop_Click
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '''            packet[0] = (byte)(CMD_TDM_STOP & 0xFF);
            packet[1] = (byte)((CMD_TDM_STOP >> 8) & 0xFF);
            ltTxCommand.Add(new Command(packet, cmdSize));

            if (btnTdmStart != null) btnTdmStart.Enabled = true;''',
    '''            packet[0] = (byte)(CMD_TDM_STOP & 0xFF);
            packet[1] = (byte)((CMD_TDM_STOP >> 8) & 0xFF);
            // BUG#2 FIX: Lock direct Add() to prevent thread conflicts
            lock (ltTxLock) { ltTxCommand.Add(new Command(packet, cmdSize)); }

            if (btnTdmStart != null) btnTdmStart.Enabled = true;''',
    "BUG#2: Lock ltTxCommand.Add in btnTdmStop_Click"
)

# ══════════════════════════════════════════════════════════════
# BUG #1 FIX 1: Add CleanupOnDisconnect method before FmMain_FormClosing
# ══════════════════════════════════════════════════════════════
cleanup_method = '''        // ═══════════════════════════════════════════════════════════════
        // BUG#1 FIX: Cleanup method called on both manual disconnect
        // and cable-pull detection. Stops all PC-side activity.
        // ═══════════════════════════════════════════════════════════════
        private void CleanupOnDisconnect()
        {
            // 1. Stop jamming thread loop
            isJamming = false;

            // 2. Mark device as inactive (stops timer from queuing commands)
            guiActive = false;

            // 3. Flush command queues (thread-safe)
            lock (ltTxLock) { ltTxCommand.Clear(); }
            lock (ltServiceSequences) { ltServiceSequences.Clear(); }

            // 4. Restore UI from jamming mode (if we were mid-jam)
            try { SetControlsJammingMode(false); } catch { }

            // 5. Update connection indicators
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (ledConnection != null) ledConnection.BackColor = Color.Red;
                        if (lblConnectionStatus != null)
                        {
                            lblConnectionStatus.Text = "OFFLINE";
                            lblConnectionStatus.ForeColor = Color.FromArgb(255, 80, 80);
                        }
                        if (btnGuardStart != null) btnGuardStart.Enabled = false;
                        if (btnGuardStop != null) btnGuardStop.Enabled = false;
                    });
                }
                else
                {
                    if (ledConnection != null) ledConnection.BackColor = Color.Red;
                    if (lblConnectionStatus != null)
                    {
                        lblConnectionStatus.Text = "OFFLINE";
                        lblConnectionStatus.ForeColor = Color.FromArgb(255, 80, 80);
                    }
                    if (btnGuardStart != null) btnGuardStart.Enabled = false;
                    if (btnGuardStop != null) btnGuardStop.Enabled = false;
                }
            }
            catch { }

            Debug.WriteLine("CleanupOnDisconnect: All PC-side activity stopped");
        }

'''

code = apply_patch(code,
    '        /// <summary> Закрытие формы. </summary>\n        private void FmMain_FormClosing',
    cleanup_method + '        /// <summary> Закрытие формы. </summary>\n        private void FmMain_FormClosing',
    "BUG#1: Add CleanupOnDisconnect method"
)

# ══════════════════════════════════════════════════════════════
# BUG #1 FIX 2: Wire CleanupOnDisconnect into manual disconnect (tlspbtConnect_Click)
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '''                else
                {
                    if (usbConnection.Close() == false)
                    {
                        throw new Exception("Close connection error.");
                    }
                    ConnectionControlGUI(false, run_cfg);
                }''',
    '''                else
                {
                    // BUG#1 FIX: Clean up all PC-side activity before closing connection
                    CleanupOnDisconnect();
                    if (usbConnection.Close() == false)
                    {
                        throw new Exception("Close connection error.");
                    }
                    ConnectionControlGUI(false, run_cfg);
                }''',
    "BUG#1: Wire CleanupOnDisconnect into manual disconnect"
)

# ══════════════════════════════════════════════════════════════
# BUG #1 FIX 3: Wire CleanupOnDisconnect into cable-pull detection (IsAlive == false)
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '''                            if (usbConnection.UsbRegistryInfo.IsAlive == false)
                            {
                                ConnectionControlGUI(false, run_cfg);
                                usbConnection.Close();''',
    '''                            if (usbConnection.UsbRegistryInfo.IsAlive == false)
                            {
                                // BUG#1 FIX: Clean up all PC-side activity on cable pull
                                CleanupOnDisconnect();
                                ConnectionControlGUI(false, run_cfg);
                                usbConnection.Close();''',
    "BUG#1: Wire CleanupOnDisconnect into cable-pull detection"
)

# ══════════════════════════════════════════════════════════════
# BUG #1 FIX 4: Wire CleanupOnDisconnect into second disconnect path (guiActive but !IsOpen)
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '''                        else if (guiActive == true)
                        {
                            ConnectionControlGUI(false, run_cfg);
                            usbConnection.Close();
                        }''',
    '''                        else if (guiActive == true)
                        {
                            // BUG#1 FIX: Clean up all PC-side activity
                            CleanupOnDisconnect();
                            ConnectionControlGUI(false, run_cfg);
                            usbConnection.Close();
                        }''',
    "BUG#1: Wire CleanupOnDisconnect into guiActive disconnect path"
)

# ══════════════════════════════════════════════════════════════
# BUG #1 FIX 5: Re-enable START button on reconnect
# ══════════════════════════════════════════════════════════════
code = apply_patch(code,
    '''                // Update connection LED
                if (ledConnection != null) { ledConnection.BackColor = Color.Lime; }
                if (lblConnectionStatus != null) { lblConnectionStatus.Text = "ONLINE"; lblConnectionStatus.ForeColor = Color.FromArgb(100, 255, 100); }''',
    '''                // Update connection LED
                if (ledConnection != null) { ledConnection.BackColor = Color.Lime; }
                if (lblConnectionStatus != null) { lblConnectionStatus.Text = "ONLINE"; lblConnectionStatus.ForeColor = Color.FromArgb(100, 255, 100); }
                // BUG#1 FIX: Re-enable START button on reconnect
                if (btnGuardStart != null) btnGuardStart.Enabled = true;
                if (btnGuardStop != null) btnGuardStop.Enabled = false;''',
    "BUG#1: Re-enable START button on reconnect"
)

# ══════════════════════════════════════════════════════════════
# Write output
# ══════════════════════════════════════════════════════════════
print("", file=sys.stderr)
print(f"═══ {patches_applied} patches applied successfully ═══", file=sys.stderr)

outpath = sys.argv[2] if len(sys.argv) > 2 else '/home/claude/work/FmMain_patched.cs'
with open(outpath, 'w', encoding='utf-8') as f:
    f.write(code)
print(f"Output: {outpath}", file=sys.stderr)
