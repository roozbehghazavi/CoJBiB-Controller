
Public Class GameLogReader

    Private Declare Function CreateFileA Lib "kernel32" Alias "CreateFileA" (lpFileName As String, dwDesiredAccess As UInt32, dwShareMode As UInt32, lpSecurityAttributes As String, dwCreationDisposition As UInt32, dwFlagsAndAttributes As UInt32, hTemplateFile As String) As UInt32
    Private Declare Function ReadFile Lib "kernel32" Alias "ReadFile" (handle As UInt32, buff As Byte(), size As UInt32, ByRef rd As UInt32, overlapped As String) As UInt32
    Private Declare Function CloseHandle Lib "kernel32" Alias "CloseHandle" (handle As UInt32) As UInt32

    Private Const GENERIC_READ = &H80000000&
    Private Const OPEN_EXISTING = 3
    Private Const FILE_ATTRIBUTE_NORMAL = &H80
    Private Const FILE_SHARE_READ = 1
    Private Const FILE_SHARE_WRITE = 2

    Private Const CR = &HD
    Private Const LF = &HA

    Private handle As UInt32
    Private isOpened As Boolean = False
    Private readedInitialContents As Boolean = False
    Private buff(4095) As Byte
    Private lineBuff As String = ""
    Private isBufferEmpty As Boolean = True
    Private bufferPos As UInt32 = 0
    Private dataCount As UInt32 = 0

    Public Sub OpenFile(path As String)
        If isOpened Then
            Exit Sub
        End If

        handle = CreateFileA(path, GENERIC_READ, FILE_SHARE_READ Or FILE_SHARE_WRITE, vbNullString, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, vbNullString)
        isOpened = True
        readedInitialContents = False
        lineBuff = ""
        isBufferEmpty = True
        bufferPos = 0
        dataCount = 0
    End Sub

    Public Sub Close()
        If isOpened Then
            CloseHandle(handle)
            isOpened = False
        End If
    End Sub

    Public Function ReadLine(ByRef isNotInitial As Boolean) As String

        While True
            If isBufferEmpty Then
                Dim result = ReadFile(handle, buff, buff.Length, dataCount, vbNullString)
                If result = 0 Then
                    Exit While
                End If
                If dataCount = 0 Then
                    readedInitialContents = True
                    Exit While
                End If
                isBufferEmpty = False
                bufferPos = 0
            End If

            While bufferPos < dataCount
                Dim chr = buff(bufferPos)
                bufferPos += 1
                If chr = CR Or chr = LF Then
                    If lineBuff.Length > 0 Then
                        Dim line2 = lineBuff
                        lineBuff = ""
                        isNotInitial = readedInitialContents
                        Return line2
                    End If
                Else
                    lineBuff &= ChrW(chr)
                End If
            End While
            isBufferEmpty = True

        End While

        Return Nothing
    End Function

End Class
