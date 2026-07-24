Imports System.Runtime.InteropServices

Public Class ConsoleWindowSender

    <StructLayout(LayoutKind.Sequential)>
    Private Structure InputRecord
        <MarshalAs(UnmanagedType.U2)> Public eventType As UInt16
        <MarshalAs(UnmanagedType.U2)> Public padd0 As UInt16
        <MarshalAs(UnmanagedType.U4)> Public bKeyDown As UInt32
        <MarshalAs(UnmanagedType.U2)> Public padd1 As UInt16
        <MarshalAs(UnmanagedType.U2)> Public wVirtualKeyCode As UInt16
        <MarshalAs(UnmanagedType.U2)> Public padd2 As UInt16
        <MarshalAs(UnmanagedType.U1)> Public asciiChar As Byte
        <MarshalAs(UnmanagedType.U1)> Public padd3 As Byte
        <MarshalAs(UnmanagedType.U4)> Public padd4 As UInt32
    End Structure

    Private Declare Function GetStdHandle Lib "kernel32" Alias "GetStdHandle" (handle As UInt32) As UInt32
    Private Declare Function WriteConsoleInputA Lib "kernel32" Alias "WriteConsoleInputA" (handle As UInt32, ByRef inprec As InputRecord, size As UInt32, ByRef numberOfEventsWritten As UInt32) As UInt32
    Private Declare Function FindWindowA Lib "user32" Alias "FindWindowA" (lpsz1 As String, lpsz2 As String) As UInt32
    Private Declare Function GetWindowThreadProcessId Lib "user32" Alias "GetWindowThreadProcessId" (hwnd As UInt32, ByRef pid As UInt32) As UInt32
    Private Declare Function FreeConsole Lib "kernel32" Alias "FreeConsole" () As UInt32
    Private Declare Function AttachConsole Lib "kernel32" Alias "AttachConsole" (pid As UInt32) As UInt32
    Private Declare Function SendMessageA Lib "user32" Alias "SendMessageA" (hwnd As UInt32, msg As UInt32, wparam As UInt32, lparam As UInt32) As UInt32

    Private Const STD_INPUT_HANDLE As UInt32 = &HFFFFFFF6& '-10
    Private Const VK_RETURN = &HD
    Private Const KEY_EVENT = 1

    Private Const WM_CHAR = &H102


    Private hwnd As UInt32 = 0
    Private pid As UInt32 = 0
    Private stdin As UInt32 = 0
    Private attached As Boolean = False

    Private runsOnWine As Boolean = False
    Private platformChecked As Boolean = False

    Private Function CheckPlatform() As Boolean
        If Not platformChecked Then

            Dim techlandHwnd = FindWindowA("techland_game_class", vbNullString)
            If techlandHwnd = 0 Then
                Return False
            End If

            Dim wineHwnd = FindWindowA("WineConsoleClass", vbNullString)
            If wineHwnd > 0 Then
                runsOnWine = True
            End If

            platformChecked = True
            Return True
        End If
        Return True
    End Function

    Public Function FindWindow() As Boolean

        If Not CheckPlatform() Then
            Return False
        End If

        If runsOnWine Then
            Dim oldHwnd As UInt32 = hwnd
            hwnd = FindWindowA("techland_game_class", vbNullString)
            If hwnd <> oldHwnd Then
                attached = False
            End If
        Else
            hwnd = FindWindowA("ConsoleWindowClass", vbNullString)
        End If

        If hwnd > 0 Then
            Return True
        End If

        Return False

    End Function

    Private Sub AttachToServerConsole()
        GetWindowThreadProcessId(hwnd, pid)
        FreeConsole()
        AttachConsole(pid)
        stdin = GetStdHandle(STD_INPUT_HANDLE)
    End Sub

    Private Sub SendMessageToConsoleWine(text As String)
        If Not attached Then
            AttachToServerConsole()
            attached = True
        End If

        Dim numberOfEventsWritten As UInt32
        Dim inprec As InputRecord
        inprec.eventType = KEY_EVENT
        inprec.padd0 = 0
        inprec.bKeyDown = 1
        inprec.padd1 = 0
        inprec.wVirtualKeyCode = 0
        inprec.padd2 = 0
        inprec.padd3 = 0
        inprec.padd4 = 0

        For i As Integer = 1 To Len(text)
            inprec.asciiChar = Asc(Mid(text, i, 1))
            WriteConsoleInputA(stdin, inprec, 1, numberOfEventsWritten)
        Next i

        inprec.asciiChar = 0
        inprec.wVirtualKeyCode = VK_RETURN
        WriteConsoleInputA(stdin, inprec, 1, numberOfEventsWritten)
    End Sub

    Private Sub SendMessageToConsoleWindows(text As String)
        For i As Integer = 1 To Len(text)
            SendMessageA(hwnd, WM_CHAR, Asc(Mid(text, i, 1)), 0)
        Next i

        SendMessageA(hwnd, WM_CHAR, Asc(vbCr), 0)
        SendMessageA(hwnd, WM_CHAR, Asc(vbLf), 0)
    End Sub

    Public Sub SendMessageToConsole(text As String)
        If hwnd = 0 Then
            Exit Sub
        End If
        If runsOnWine Then
            SendMessageToConsoleWine(text)
        Else
            SendMessageToConsoleWindows(text)
        End If
    End Sub

End Class
