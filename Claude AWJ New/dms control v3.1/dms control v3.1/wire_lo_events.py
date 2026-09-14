"""
K9 Electronics - Wire Up LO TextBox Events
Inserts Tag assignments + KeyPress event wiring into FmMain.cs
"""
import sys, os, re

# Find FmMain.cs
target = None
if len(sys.argv) > 1:
    target = sys.argv[1]
else:
    # Look in same folder as this script
    script_dir = os.path.dirname(os.path.abspath(__file__))
    candidate = os.path.join(script_dir, "FmMain.cs")
    if os.path.exists(candidate):
        target = candidate

if not target or not os.path.exists(target):
    print("ERROR: Cannot find FmMain.cs")
    print("Put this script in the same folder as FmMain.cs, or run:")
    print("  python wire_lo_events.py path\\to\\FmMain.cs")
    input("\nPress Enter to exit...")
    sys.exit(1)

print(f"Found: {target}")
print()

# Read file
with open(target, 'r', encoding='utf-8-sig') as f:
    content = f.read()

# Check if already applied
if "K9 FIX: Wire up LO textbox events" in content:
    print("FIX ALREADY APPLIED - no changes needed.")
    input("\nPress Enter to exit...")
    sys.exit(0)

# Find the anchor line
anchor = "grbxLO.Width = Math.Min(grbxLO.Width, 200);"
if anchor not in content:
    print("ERROR: Cannot find anchor line:")
    print(f"  {anchor}")
    print("\nSearch your FmMain.cs for 'grbxLO.Width' and add the code after that block.")
    input("\nPress Enter to exit...")
    sys.exit(1)

# Find the closing brace after the anchor line
# We need to find: "grbxLO.Width = Math.Min(grbxLO.Width, 200);\n            }"
# and insert our code after that closing brace
anchor_pos = content.index(anchor)
# Find the next "}" after the anchor
brace_pos = content.index("}", anchor_pos + len(anchor))

# The code to insert
insert_code = """

            // K9 FIX: Wire up LO textbox events (missing from Designer)
            // Without these, pressing Enter in Base Frequency boxes does nothing
            if (txbxLoStart != null)
            {
                txbxLoStart.Tag = "LoSweepStart";
                txbxLoStart.KeyPress += LoParameters_KeyPress;
            }
            if (txbxLoStep != null)
            {
                txbxLoStep.Tag = "LoSweepStep";
                txbxLoStep.KeyPress += LoParameters_KeyPress;
            }
            if (txbxLoPoints != null)
            {
                txbxLoPoints.Tag = "LoSweepPoint";
                txbxLoPoints.KeyPress += LoParameters_KeyPress;
            }
            if (txbxLoDelays != null)
            {
                txbxLoDelays.Tag = "LoSweepDelay";
                txbxLoDelays.KeyPress += LoParameters_KeyPress;
            }"""

# Insert after the closing brace
new_content = content[:brace_pos + 1] + insert_code + content[brace_pos + 1:]

# Create backup
backup = target + ".bak"
with open(backup, 'w', encoding='utf-8') as f:
    f.write(content)
print(f"Backup created: {backup}")

# Write fixed file
with open(target, 'w', encoding='utf-8') as f:
    f.write(new_content)

print()
print("SUCCESS! LO textbox events wired up.")
print()
print("What was added after grbxLO.Width line:")
print("  txbxLoStart.Tag  = LoSweepStart  + KeyPress event")
print("  txbxLoStep.Tag   = LoSweepStep   + KeyPress event")
print("  txbxLoPoints.Tag = LoSweepPoint  + KeyPress event")
print("  txbxLoDelays.Tag = LoSweepDelay  + KeyPress event")
print()
print("NEXT: Rebuild in Visual Studio (Ctrl+Shift+B) then test!")
print("  Type 2100 in Center Freq box and press Enter")
print("  It should reformat to 2.100 GHz and progress bar should fill")

input("\nPress Enter to exit...")
