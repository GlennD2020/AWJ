' ============================================================
'  K9 AWJ — PLAIN-ENGLISH DIAGNOSTICS INTEGRATION
'  Double-click this file to run it.
'  Must be in the SAME folder as FmMain.cs and
'  DiagnosticsReplacement_English.cs
' ============================================================

Option Explicit

Dim fso, shell
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

Dim scriptDir
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
shell.CurrentDirectory = scriptDir

Dim fmPath, repPath
fmPath = fso.BuildPath(scriptDir, "FmMain.cs")
repPath = fso.BuildPath(scriptDir, "DiagnosticsReplacement_English.cs")

' ── Check files exist ──
If Not fso.FileExists(fmPath) Then
    MsgBox "Cannot find FmMain.cs in:" & vbCrLf & scriptDir & vbCrLf & vbCrLf & _
           "Put this script in the SAME folder as FmMain.cs", _
           vbCritical, "K9 Diagnostics"
    WScript.Quit 1
End If

If Not fso.FileExists(repPath) Then
    MsgBox "Cannot find DiagnosticsReplacement_English.cs in:" & vbCrLf & scriptDir & vbCrLf & vbCrLf & _
           "Download it from Claude and put it here.", _
           vbCritical, "K9 Diagnostics"
    WScript.Quit 1
End If

' ── Create backup ──
Dim backupPath
backupPath = fso.BuildPath(scriptDir, "FmMain_BACKUP.cs")
fso.CopyFile fmPath, backupPath, True

' ── Read original file (ASCII/UTF-8 mode) ──
Dim origFile, origText
Set origFile = fso.OpenTextFile(fmPath, 1, False, 0)
origText = origFile.ReadAll()
origFile.Close

' ── Read replacement file (ASCII/UTF-8 mode) ──
Dim repFile, repText
Set repFile = fso.OpenTextFile(repPath, 1, False, 0)
repText = repFile.ReadAll()
repFile.Close

' ── Find the cut point ──
Dim marker, cutPos
marker = "#region DIAGNOSTICS_PANEL_CREATION"
cutPos = InStr(origText, marker)

' If not found, try searching for partial matches
If cutPos = 0 Then
    ' Maybe it has slightly different spacing or casing
    cutPos = InStr(LCase(origText), LCase(marker))
End If

' Still not found - check if file has ANY diagnostics regions
If cutPos = 0 Then
    Dim hasAnyDiag
    hasAnyDiag = InStr(LCase(origText), "diagnostics")
    
    Dim fileSize
    fileSize = fso.GetFile(fmPath).Size
    
    Dim lineCount
    lineCount = UBound(Split(origText, vbLf)) + 1
    
    ' Show helpful debug info
    Dim debugMsg
    debugMsg = "Cannot find '#region DIAGNOSTICS_PANEL_CREATION'" & vbCrLf & vbCrLf
    debugMsg = debugMsg & "File info:" & vbCrLf
    debugMsg = debugMsg & "  Size: " & fileSize & " bytes" & vbCrLf
    debugMsg = debugMsg & "  Lines: " & lineCount & vbCrLf
    
    If hasAnyDiag > 0 Then
        debugMsg = debugMsg & "  Contains word 'diagnostics': YES" & vbCrLf
        debugMsg = debugMsg & vbCrLf
        debugMsg = debugMsg & "The marker exists but may have extra spaces." & vbCrLf
        debugMsg = debugMsg & "Try the MANUAL method instead (see below)."
    Else
        debugMsg = debugMsg & "  Contains word 'diagnostics': NO" & vbCrLf
        debugMsg = debugMsg & vbCrLf
        debugMsg = debugMsg & "This file has NO diagnostics code yet." & vbCrLf
        debugMsg = debugMsg & "You need the version that already has the" & vbCrLf
        debugMsg = debugMsg & "old diagnostics integrated."
    End If
    
    debugMsg = debugMsg & vbCrLf & vbCrLf
    debugMsg = debugMsg & "MANUAL METHOD:" & vbCrLf
    debugMsg = debugMsg & "1. Open FmMain.cs in Visual Studio" & vbCrLf
    debugMsg = debugMsg & "2. Press Ctrl+End to go to the very bottom" & vbCrLf
    debugMsg = debugMsg & "3. Find the last two closing braces:  }  }" & vbCrLf
    debugMsg = debugMsg & "4. Put your cursor BEFORE those two braces" & vbCrLf
    debugMsg = debugMsg & "5. Paste the contents of" & vbCrLf
    debugMsg = debugMsg & "   DiagnosticsReplacement_English.cs there" & vbCrLf
    debugMsg = debugMsg & "6. Delete the extra  }  }  at the very end" & vbCrLf
    debugMsg = debugMsg & "   (so you only have one pair of closing braces)"
    
    MsgBox debugMsg, vbExclamation, "K9 Diagnostics"
    WScript.Quit 1
End If

' ── Find the start of that line ──
Dim lineStart
lineStart = cutPos
Do While lineStart > 1
    Dim ch
    ch = Mid(origText, lineStart - 1, 1)
    If ch = vbLf Or ch = vbCr Then
        Exit Do
    End If
    lineStart = lineStart - 1
Loop

' ── Keep everything before the marker line ──
Dim keepPart
keepPart = Left(origText, lineStart - 1)

' Make sure it ends with a newline
If Right(keepPart, 1) <> vbLf And Len(keepPart) > 0 Then
    keepPart = keepPart & vbCrLf
End If

' ── Combine ──
Dim newText
newText = keepPart & repText

' ── Write new file ──
Dim outFile
Set outFile = fso.OpenTextFile(fmPath, 2, True, 0)
outFile.Write newText
outFile.Close

' ── Result ──
Dim origLines, newLines
origLines = UBound(Split(origText, vbLf)) + 1
newLines = UBound(Split(newText, vbLf)) + 1

MsgBox "INTEGRATION COMPLETE!" & vbCrLf & vbCrLf & _
       "Original: " & origLines & " lines" & vbCrLf & _
       "New file: " & newLines & " lines" & vbCrLf & _
       "Backup: FmMain_BACKUP.cs" & vbCrLf & vbCrLf & _
       "NOW DO THIS:" & vbCrLf & _
       "1. Open Visual Studio" & vbCrLf & _
       "2. Click YES to reload FmMain.cs" & vbCrLf & _
       "3. In Solution Explorer - if you see" & vbCrLf & _
       "   DiagnosticsReplacement_English.cs" & vbCrLf & _
       "   RIGHT-CLICK it > Exclude From Project" & vbCrLf & _
       "4. Press Ctrl+Shift+B to Build" & vbCrLf & vbCrLf & _
       "If it breaks: rename FmMain_BACKUP.cs to FmMain.cs", _
       vbInformation, "K9 Diagnostics - Done"
