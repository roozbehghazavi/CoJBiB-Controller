' =================================================================================
' AssetsBootstrap.vb - Self-extracting asset payload for single-exe distribution.
' Date: 2026
' Created by: Roozbeh Ghazavi (fork additions).
'
'    Copyright (C) 2026 Roozbeh Ghazavi.
'
'    This program Is free software: you can redistribute it And/Or modify
'    it under the terms Of the GNU General Public License As published by
'    the Free Software Foundation, either version 3 Of the License, Or
'    (at your option) any later version. See <https://www.gnu.org/licenses/>.
' =================================================================================

Imports System.IO
Imports System.Reflection

' The app loads its character/weapon art and the modding class files from loose
' folders next to the exe (images\ and bin\). To ship as a single .exe, those
' files are bundled into a payload embedded in the exe at build time; on first
' run (or after a version change) we extract them beside the exe. If no payload
' is embedded (e.g. a plain dev build) we silently rely on the on-disk folders
' that the normal build already copies to the output directory.
'
' The payload is a simple uncompressed container read with only System.IO, so the
' app stays runnable on plain .NET Framework 4.0 (e.g. under Wine with dotnet40),
' which does NOT ship System.IO.Compression (ZipArchive). Format per entry,
' little-endian, repeated to end of stream:
'   Int32 pathLen, UTF-8 path bytes, Int64 dataLen, data bytes.
Public Module AssetsBootstrap

    Private Const ResourceName As String = "CoJ2Controller.assets.pack"

    Public Sub EnsureAssets()
        Dim baseDir As String = Application.StartupPath
        Dim attemptedExtract As Boolean = False
        Try
            Dim ver As String = Application.ProductVersion
            Dim marker As String = Path.Combine(baseDir, ".assets_version")

            Dim haveImages As Boolean = Directory.Exists(Path.Combine(baseDir, "images"))
            Dim haveBin As Boolean = Directory.Exists(Path.Combine(baseDir, "bin"))
            Dim haveChm As Boolean = File.Exists(Path.Combine(baseDir, "CoJ Mods.chm"))
            Dim haveVdll As Boolean = File.Exists(Path.Combine(baseDir, "mod", "version.dll"))
            Dim markerOk As Boolean = File.Exists(marker) AndAlso File.ReadAllText(marker).Trim() = ver

            ' Everything already present and current -> nothing to do.
            If haveImages AndAlso haveBin AndAlso haveChm AndAlso haveVdll AndAlso markerOk Then Return

            Using s As Stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                If s Is Nothing OrElse s.Length = 0 Then Return   ' no embedded payload; use on-disk folders
                attemptedExtract = True       ' from here on, a failure means we could not write
                Using br As New BinaryReader(s)
                    While br.BaseStream.Position < br.BaseStream.Length
                        Dim pathLen As Integer = br.ReadInt32()
                        Dim rel As String = System.Text.Encoding.UTF8.GetString(br.ReadBytes(pathLen))
                        Dim dataLen As Long = br.ReadInt64()
                        Dim data() As Byte = br.ReadBytes(CInt(dataLen))   ' bundled files are well under 2 GB
                        rel = rel.Replace("/"c, Path.DirectorySeparatorChar).Replace("\"c, Path.DirectorySeparatorChar)
                        Dim target As String = Path.Combine(baseDir, rel)
                        Directory.CreateDirectory(Path.GetDirectoryName(target))
                        File.WriteAllBytes(target, data)
                    End While
                End Using
            End Using

            Try
                File.WriteAllText(marker, ver)
            Catch
                ' Marker is only an optimisation; ignore if the folder is read-only.
            End Try
        Catch ex As Exception
            ' The only realistic failure is that baseDir is read-only / protected
            ' (e.g. the exe was placed in Program Files). If we actually tried to
            ' unpack, tell the user why the art/mod files are missing so they can fix it.
            If attemptedExtract Then WarnReadOnly(baseDir, ex)
        End Try
    End Sub

    Private Sub WarnReadOnly(ByVal baseDir As String, ByVal ex As Exception)
        Try
            Dim msg As String =
                "CoJ2 Controller could not unpack its files into this folder:" & vbCrLf &
                vbCrLf &
                "    " & baseDir & vbCrLf &
                vbCrLf &
                "The folder appears to be read-only or write-protected, so the game art " &
                "and modding files can't be created. The program will still open, but " &
                "some pictures and the mod pages may not work." & vbCrLf &
                vbCrLf &
                "Fix: move CoJ2Controller.exe to a folder you can write to (for example your " &
                "Desktop or a games/tools folder) and run it again. Avoid running it directly " &
                "from Program Files or a protected location." & vbCrLf &
                vbCrLf &
                "(" & ex.GetType().Name & ": " & ex.Message & ")"
            MessageBox.Show(msg, "CoJ2 Controller - cannot unpack files",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch
            ' If even the warning fails, don't crash startup.
        End Try
    End Sub

End Module
