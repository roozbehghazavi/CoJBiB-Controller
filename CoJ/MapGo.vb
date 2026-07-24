' =================================================================================
' MapGo.vb - Implements the "!mapgo <map> <mode>" admin command.
' Date: 2026
' Added by: Roozbeh Ghazavi (fork of victormvy/CoJ2-Controller)
'
'    Copyright (C) 2026 Roozbeh Ghazavi.
'    Part of CoJ2 Controller, originally (C) 2013-2020 S!lv3r and Tr!tu.
'
'    This program is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    This program is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with this program. If not, see <https://www.gnu.org/licenses/>.
' =================================================================================
'
' Looks up the target map/mode in the DedicatedServerSettings.ini rotation
' (0-based, in file order), writes N-1 (mod count) to the running dedicated
' server's current-map-index counter at CoJ2_x86_ds.dll + 0x9CAF10 using
' WriteProcessMemory, verifies the value with ReadProcessMemory, then lets the
' caller issue /mapnext so the server's own advance+load path lands on entry N.
'
' See mapgo_implementation_reference.md for the reverse-engineering details.
' =================================================================================

Imports System.Runtime.InteropServices

Module MapGo

    ' RVA of the current-map-index counter inside CoJ2_x86_ds.dll (ghidra 0x109caf10).
    Private Const RVA_MAP_INDEX As Integer = &H9CAF10

    ' The dedicated server executable (matches CheckProcess in frmConsola) and the
    ' module that holds the index counter.
    Private Const TARGET_PROCESS As String = "CoJ2Game_x86_ds"
    Private Const TARGET_MODULE As String = "CoJ2_x86_ds.dll"

    Private Const TH32CS_SNAPMODULE As UInt32 = &H8
    Private Const TH32CS_SNAPMODULE32 As UInt32 = &H10
    Private Const PROCESS_VM_OPERATION As UInt32 = &H8
    Private Const PROCESS_VM_READ As UInt32 = &H10
    Private Const PROCESS_VM_WRITE As UInt32 = &H20

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Ansi)>
    Private Structure MODULEENTRY32
        Public dwSize As UInt32
        Public th32ModuleID As UInt32
        Public th32ProcessID As UInt32
        Public GlblcntUsage As UInt32
        Public ProccntUsage As UInt32
        Public modBaseAddr As IntPtr
        Public modBaseSize As UInt32
        Public hModule As IntPtr
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=256)> Public szModule As String
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)> Public szExePath As String
    End Structure

    ' P/Invoke declared in the same style as ConsoleWindowSender.vb (parameters are
    ' ByVal by default). SIZE_T params (nSize / bytes read-written) are IntPtr so the
    ' calls are correct on a 64-bit build talking to the 32-bit server.
    Private Declare Function CreateToolhelp32Snapshot Lib "kernel32" (dwFlags As UInt32, th32ProcessID As UInt32) As IntPtr
    Private Declare Function Module32First Lib "kernel32" Alias "Module32First" (hSnapshot As IntPtr, ByRef lpme As MODULEENTRY32) As Boolean
    Private Declare Function Module32Next Lib "kernel32" Alias "Module32Next" (hSnapshot As IntPtr, ByRef lpme As MODULEENTRY32) As Boolean
    Private Declare Function OpenProcess Lib "kernel32" (dwDesiredAccess As UInt32, bInheritHandle As Boolean, dwProcessId As UInt32) As IntPtr
    Private Declare Function ReadProcessMemory Lib "kernel32" (hProcess As IntPtr, lpBaseAddress As IntPtr, lpBuffer As Byte(), nSize As IntPtr, ByRef lpNumberOfBytesRead As IntPtr) As Boolean
    Private Declare Function WriteProcessMemory Lib "kernel32" (hProcess As IntPtr, lpBaseAddress As IntPtr, lpBuffer As Byte(), nSize As IntPtr, ByRef lpNumberOfBytesWritten As IntPtr) As Boolean
    Private Declare Function CloseHandle Lib "kernel32" (hObject As IntPtr) As Boolean

    ''' <summary>
    ''' Resolves "&lt;map&gt; &lt;mode&gt;" against the live rotation and, on success,
    ''' sets the server's map-index counter so a single /mapnext advances to the target,
    ''' confirming the write with a read-back.
    '''
    ''' <paramref name="rows"/> is the current rotation exactly as the controller tracks
    ''' it in dgMaps: row 0 is the CURRENT map and each element is {mapDisplay,
    ''' modeDisplay} (mode wrapped like "&lt;Manhunt&gt;"). The index counter at
    ''' base+0x9CAF10 is read live (it is authoritative and rotation-proof), the target's
    ''' offset from row 0 gives how many steps a /mapnext must advance, and the counter is
    ''' set to (current + steps - 1) so one advance lands on the target.
    '''
    ''' On success <paramref name="stepsToTarget"/> is the offset (>=1); the caller rotates
    ''' dgMaps by stepsToTarget-1 and issues /mapnext (whose "Game ended!" handler performs
    ''' the final rotation and, if chkRememberMaps is on, rewrites the ini). resultMsg is a
    ''' short player-facing status; diagMsg is a base/before/after line for the local log.
    ''' </summary>
    Public Function TryMapGo(ByVal rows As List(Of String()), ByVal mapArg As String, ByVal modeArg As String,
                             ByRef stepsToTarget As Integer, ByRef resultMsg As String, ByRef diagMsg As String) As Boolean

        mapArg = Trim(mapArg)
        modeArg = Trim(modeArg)
        stepsToTarget = 0
        diagMsg = ""

        If mapArg = "" Or modeArg = "" Then
            resultMsg = "!mapgo usage: !mapgo <map> <mode>"
            Return False
        End If

        Dim wantedMode As String = ResolveMode(modeArg)
        If wantedMode = "" Then
            resultMsg = "!mapgo: unknown mode '" & modeArg & "'"
            Return False
        End If

        Dim count As Integer = rows.Count
        If count = 0 Then
            resultMsg = "!mapgo: map list is empty"
            Return False
        End If

        Dim wantedMap As String = NormalizeMap(mapArg)

        ' Row 0 is the current map; find the next occurrence of the target ahead of it.
        Dim r As Integer = -1
        Dim matchesCurrent As Boolean = False
        For i As Integer = 0 To count - 1
            If NormalizeMap(rows(i)(0)) = wantedMap AndAlso ResolveMode(StripMode(rows(i)(1))) = wantedMode Then
                If i = 0 Then
                    matchesCurrent = True
                Else
                    r = i
                    Exit For
                End If
            End If
        Next

        If r < 0 Then
            If matchesCurrent Then
                resultMsg = "!mapgo: '" & mapArg & " " & modeArg & "' is already the current map"
            Else
                resultMsg = "!mapgo: '" & mapArg & " " & modeArg & "' is not in the map rotation"
            End If
            Return False
        End If

        ' Locate the live process/module, read the current index, and set it so the next
        ' advance ("index = (index + 1) % count; load") lands r steps ahead on the target.
        Dim hProc As IntPtr = IntPtr.Zero
        Dim modBase As IntPtr = IntPtr.Zero
        Dim openError As String = ""
        If Not OpenTarget(hProc, modBase, openError) Then
            resultMsg = "!mapgo: " & openError
            Return False
        End If

        Try
            Dim target As New IntPtr(modBase.ToInt64() + RVA_MAP_INDEX)

            Dim curIdx As Integer = 0
            If Not ReadInt(hProc, target, curIdx) Then
                resultMsg = "!mapgo: could not read the current map index"
                diagMsg = "!mapgo read failed @0x" & modBase.ToInt64().ToString("X") & "+9CAF10 (err " & Marshal.GetLastWin32Error() & ")"
                Return False
            End If

            Dim writeValue As Integer = ((curIdx + r - 1 + count) Mod count)

            If Not WriteInt(hProc, target, writeValue) Then
                resultMsg = "!mapgo: write to server memory failed"
                diagMsg = "!mapgo WPM failed @0x" & modBase.ToInt64().ToString("X") & "+9CAF10 (err " & Marshal.GetLastWin32Error() & ")"
                Return False
            End If

            Dim afterVal As Integer = 0
            Dim afterOk As Boolean = ReadInt(hProc, target, afterVal)

            diagMsg = "!mapgo base=0x" & modBase.ToInt64().ToString("X") & " cur=" & curIdx.ToString() &
                      " steps=" & r.ToString() & " wrote=" & writeValue.ToString() &
                      " readback=" & If(afterOk, afterVal.ToString(), "?")

            If Not afterOk OrElse afterVal <> writeValue Then
                resultMsg = "!mapgo: index verify failed (write did not stick)"
                Return False
            End If

            stepsToTarget = r
            resultMsg = "Next map: " & Trim(rows(r)(0)) & " " & Trim(rows(r)(1))
            Return True
        Finally
            CloseHandle(hProc)
        End Try

    End Function

    ' Strips the "<...>" wrapper dgMaps uses around the display mode name.
    Private Function StripMode(ByVal s As String) As String
        s = Trim(s)
        If s.StartsWith("<") Then s = s.Substring(1)
        If s.EndsWith(">") Then s = s.Substring(0, s.Length - 1)
        Return Trim(s)
    End Function

    ' Accepts either the internal mode name or its in-game display name and returns
    ' the internal name (as stored in the ini). Returns "" for an unknown mode.
    Private Function ResolveMode(ByVal mode As String) As String
        Select Case LCase(Trim(mode))
            Case "historical", "wild west legends", "w.w.legends", "wwlegends", "legends"
                Return "historical"
            Case "teamwanted", "manhunt"
                Return "teamwanted"
            Case "wanted"
                Return "wanted"
            Case "deathmatch", "shootout"
                Return "deathmatch"
            Case "teamdeathmatch", "posse"
                Return "teamdeathmatch"
            Case Else
                Return ""
        End Select
    End Function

    ' Normalizes a map name to its lowercase internal ini token, accepting the
    ' display aliases the UI uses (mirrors the aliases in ReadMaps).
    Private Function NormalizeMap(ByVal m As String) As String
        Select Case LCase(Trim(m))
            Case "stinking springs", "stinkingsprings", "stinkingsprings2"
                Return "stinkingsprings2"
            Case "nogales", "prisonbreak2"
                Return "prisonbreak2"
            Case "taos pueblo", "adobes"
                Return "adobes"
            Case "burnside's bridge", "burnsides bridge", "civil"
                Return "civil"
            Case Else
                Return LCase(Trim(m))
        End Select
    End Function

    ' Finds the running server, resolves the base of CoJ2_x86_ds.dll, and opens a
    ' handle with read/write access. Fills a descriptive error on failure.
    Private Function OpenTarget(ByRef hProc As IntPtr, ByRef modBase As IntPtr, ByRef errorMsg As String) As Boolean

        Dim pid As UInt32 = 0
        For Each p As Process In Process.GetProcesses()
            If p.ProcessName = TARGET_PROCESS Then
                pid = CUInt(p.Id)
                Exit For
            End If
        Next
        If pid = 0 Then
            errorMsg = "server process '" & TARGET_PROCESS & "' not found"
            Return False
        End If

        modBase = GetModuleBase(pid, TARGET_MODULE)
        If modBase = IntPtr.Zero Then
            errorMsg = "module '" & TARGET_MODULE & "' not found in the server process"
            Return False
        End If

        hProc = OpenProcess(PROCESS_VM_OPERATION Or PROCESS_VM_READ Or PROCESS_VM_WRITE, False, pid)
        If hProc = IntPtr.Zero Then
            errorMsg = "OpenProcess failed (last error " & Marshal.GetLastWin32Error() & "); try running the controller as Administrator"
            Return False
        End If

        Return True
    End Function

    Private Function ReadInt(ByVal hProc As IntPtr, ByVal address As IntPtr, ByRef value As Integer) As Boolean
        Dim buffer(3) As Byte
        Dim read As IntPtr
        If ReadProcessMemory(hProc, address, buffer, New IntPtr(4), read) AndAlso read.ToInt64() = 4 Then
            value = BitConverter.ToInt32(buffer, 0)
            Return True
        End If
        Return False
    End Function

    Private Function WriteInt(ByVal hProc As IntPtr, ByVal address As IntPtr, ByVal value As Integer) As Boolean
        Dim buffer() As Byte = BitConverter.GetBytes(value)   ' 4-byte little-endian Int32
        Dim written As IntPtr
        Return WriteProcessMemory(hProc, address, buffer, New IntPtr(buffer.Length), written) AndAlso written.ToInt64() = buffer.Length
    End Function

    ' Enumerates the target's modules (SNAPMODULE32 so a 64-bit build can read a
    ' 32-bit target) and returns the base address of the named module.
    Private Function GetModuleBase(ByVal pid As UInt32, ByVal moduleName As String) As IntPtr

        Dim snapshot As IntPtr = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE Or TH32CS_SNAPMODULE32, pid)
        If snapshot = New IntPtr(-1) Then Return IntPtr.Zero

        Try
            Dim entry As New MODULEENTRY32()
            entry.dwSize = CUInt(Marshal.SizeOf(GetType(MODULEENTRY32)))
            If Module32First(snapshot, entry) Then
                Do
                    If String.Equals(entry.szModule, moduleName, StringComparison.OrdinalIgnoreCase) Then
                        Return entry.modBaseAddr
                    End If
                Loop While Module32Next(snapshot, entry)
            End If
        Finally
            CloseHandle(snapshot)
        End Try

        Return IntPtr.Zero
    End Function

End Module
