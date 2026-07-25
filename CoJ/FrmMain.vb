' =================================================================================
' frmMain.vb - Main form
' Date: 2013
' Created by: S!lv3r and Tr!tu.

'    Copyright (C) 2020 S!lv3r and Tr!tu.
'
'    This program Is free software: you can redistribute it And/Or modify
'    it under the terms Of the GNU General Public License As published by
'    the Free Software Foundation, either version 3 Of the License, Or
'    (at your option) any later version.
'
'    This program Is distributed In the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty Of
'    MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License For more details.
'
'   You should have received a copy Of the GNU General Public License
'   along with this program. If Not, see <https://www.gnu.org/licenses/>.
'
'   Email contact: Tr!tu <victormvy@gmail.com>
' =================================================================================

Imports System.IO
Imports System.Environment

Public Class FrmMain
    Dim writeini As StreamWriter
    Dim readini As StreamReader
    Dim descargando As Boolean = False

    'Western plank button art (loaded once from images\ui\; shared by all buttons).
    Private plankNormal As Image
    Private plankHover As Image

    Private Sub FrmMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        lblVersion.Text = "Ver. " & Application.ProductVersion
        Call ApplyWesternTheme()
        If My.Computer.FileSystem.FileExists(Application.StartupPath & "\ver.txt") Then
            My.Computer.FileSystem.DeleteFile(Application.StartupPath & "\ver.txt")
        End If

        If My.Computer.FileSystem.FileExists(Application.StartupPath & "\setup_ModsCoj.exe") Then
            My.Computer.FileSystem.DeleteFile(Application.StartupPath & "\setup_ModsCoj.exe")
        End If

        If My.Computer.FileSystem.FileExists(Application.StartupPath & "\motd.txt") Then
            My.Computer.FileSystem.DeleteFile(Application.StartupPath & "\motd.txt")
        End If


        Try
            Dim ultimaVersion As String = ""
            If My.Computer.Network.IsAvailable Then
                My.Computer.Network.DownloadFile("https://dl.dropboxusercontent.com/u/89811799/ver.txt", Application.StartupPath & "\ver.txt")
                read = File.OpenText(Application.StartupPath & "\ver.txt")
                ultimaVersion = read.ReadLine
                read.Close()

                If My.Computer.FileSystem.FileExists(Application.StartupPath & "\ver.txt") Then
                    My.Computer.FileSystem.DeleteFile(Application.StartupPath & "\ver.txt")
                End If


                If Application.ProductVersion < ultimaVersion Then
                    If MessageBox.Show("The " & ultimaVersion & " version is ready to download. Do you want to download it now?", "Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1) = vbYes Then

                        Me.Hide()
                        frmUpdate.ShowDialog()


                        'My.Computer.Network.DownloadFile("https://dl.dropboxusercontent.com/u/89811799/setup_ModsCoJ.exe", Application.StartupPath & "\setup_ModsCoj.exe")

                        Process.Start("""" & Application.StartupPath & "\setup_ModsCoj.exe""")
                        Application.Exit()
                    End If
                End If

            End If


        Catch ex As Exception
            'MsgBox("Unable to check for updates. Check your internet connection.")
        End Try

        Try
            Dim MOTD As String = ""

            If My.Computer.Network.IsAvailable Then
                My.Computer.Network.DownloadFile("https://dl.dropboxusercontent.com/u/89811799/motd.txt", Application.StartupPath & "\motd.txt")

                read = File.OpenText(Application.StartupPath & "\motd.txt")
                MOTD = read.ReadToEnd
                read.Close()

                If My.Computer.FileSystem.FileExists(Application.StartupPath & "\motd.txt") Then
                    My.Computer.FileSystem.DeleteFile(Application.StartupPath & "\motd.txt")
                End If

                If MOTD <> "" Then
                    MessageBox.Show(MOTD, "", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If

            End If
       
        Catch ex As Exception

        End Try



        'Comprobamos si existe una ruta en el archivo .ini y que sea correcta
        If System.IO.File.Exists(Application.StartupPath & "\cojmods.ini") Then
            readini = File.OpenText(Application.StartupPath & "\cojmods.ini")
            ruta = RTrim(readini.ReadLine())
            readini.Close()

        End If
        'Solicitamos la ruta de la carpeta del juego / servidor
        '  dialRuta.ShowDialog()
        ' ruta = dialRuta.SelectedPath()



        While System.IO.File.Exists(ruta & "\CoJ2Game_x86_ds.exe") = False And System.IO.File.Exists(ruta & "\CoJ2_x86_ds.dll") = False

            If ruta <> "" And System.IO.File.Exists(ruta & "\CoJ2Game_x86_ds.exe") And System.IO.File.Exists(ruta & "\CoJ2_x86_ds.dll") Then

            ElseIf Trim(ruta) = "" Then
                Dim msgerror As Integer
                msgerror = MessageBox.Show("You have to choose the same path where CoJ Dedicated Server was installed. Click Ok to select the correct path or click Cancel to exit.", "Incorrect patch", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation)
                If msgerror = vbOK Then
                    If System.IO.File.Exists(Application.StartupPath & "\cojmods.ini") Then

                        My.Computer.FileSystem.DeleteFile(Application.StartupPath & "\cojmods.ini")

                    End If
                    dialRuta.ShowDialog()
                    ruta = dialRuta.SelectedPath()
                    'Escribimos la ruta en el archivo .ini

                    writeini = File.AppendText(Application.StartupPath & "\cojmods.ini")
                    writeini.WriteLine(ruta)
                    writeini.Close()

                ElseIf msgerror = vbCancel Then

                    Application.ExitThread()
                    End ' Si hacemos click en cancelar, se cierra el programa entero
                End If
            Else
                Dim msgerror As Integer
                msgerror = MessageBox.Show("You have to choose the same path where CoJ Dedicated Server was installed. Click Ok to select the correct path or click Cancel to exit.", "Incorrect patch", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation)
                If msgerror = vbOK Then
                    If System.IO.File.Exists(Application.StartupPath & "\cojmods.ini") Then

                        My.Computer.FileSystem.DeleteFile(Application.StartupPath & "\cojmods.ini")

                    End If
                    dialRuta.ShowDialog()
                    ruta = dialRuta.SelectedPath()
                    'Escribimos la ruta en el archivo .ini

                    writeini = File.AppendText(Application.StartupPath & "\cojmods.ini")
                    writeini.WriteLine(ruta)
                    writeini.Close()

                ElseIf msgerror = vbCancel Then
                    End ' Si hacemos click en cancelar, se cierra el programa entero
                End If
            End If
        End While


    End Sub

    Private Sub btnLaunchServer_Click(sender As Object, e As EventArgs) Handles btnLaunchServer.Click
        Me.Hide()
        Call StartServer()
        frmConsola.Show()
        Do While vRestart = True
            Call StartServer()
            vRestart = False
            frmConsola.Show()
        Loop
        'Me.Show()
    End Sub

    Private Sub StartServer()
        'Borramos antes de arrancar los posibles ficheros LOG de consola que pudiesen existir de partidas anteriores
        '************Buscamos fichero salida consola*************
        Dim rutaAppdata As String = GetFolderPath(SpecialFolder.ApplicationData)
        Dim rutaCrashLog As String = Mid(rutaAppdata, 1, (Len(rutaAppdata) - 8)) & "\Local\VirtualStore\Program Files (x86)\techland\out\crash.log"
        Dim copiaCrash As String = Mid(rutaAppdata, 1, (Len(rutaAppdata) - 8)) & "\Local\VirtualStore\Program Files (x86)\techland\out\copiacrash.log"

        If My.Computer.FileSystem.FileExists(rutaCrashLog) Then
            My.Computer.FileSystem.DeleteFile(rutaCrashLog)
        Else
            rutaCrashLog = Mid(rutaAppdata, 1, (Len(rutaAppdata) - 8)) & "\Local\VirtualStore\Program Files\techland\out\crash.log"
            If My.Computer.FileSystem.FileExists(rutaCrashLog) Then
                My.Computer.FileSystem.DeleteFile(rutaCrashLog)
            Else
                rutaCrashLog = ruta & "\CoJ2\out\logs\crash.log"
                If My.Computer.FileSystem.FileExists(rutaCrashLog) Then
                    Try
                        My.Computer.FileSystem.DeleteFile(rutaCrashLog)
                    Catch

                    End Try
                End If
            End If
        End If

        If My.Computer.FileSystem.FileExists(copiaCrash) Then
            My.Computer.FileSystem.DeleteFile(copiaCrash)
        Else
            copiaCrash = Mid(rutaAppdata, 1, (Len(rutaAppdata) - 8)) & "\Local\VirtualStore\Program Files\techland\out\copiacrash.log"
            If My.Computer.FileSystem.FileExists(copiaCrash) Then
                My.Computer.FileSystem.DeleteFile(copiaCrash)
            Else
                copiaCrash = ruta & "\CoJ2\out\logs\copiacrash.log"
                If My.Computer.FileSystem.FileExists(copiaCrash) Then
                    My.Computer.FileSystem.DeleteFile(copiaCrash)
                End If
            End If
        End If

        '*************************************
        'Process.Start("""" & ruta & "\CoJ2Game_x86_ds.exe""")

        Dim myProcesses() As Process
        Dim myProcess As Process
        myProcesses = Process.GetProcesses()
        ' Iterate through the process array.
        For Each myProcess In myProcesses
            '  Console.WriteLine(myProcess.ProcessName)
            If myProcess.ProcessName = "CoJ2Game_x86_ds" Then
                If MessageBox.Show("Coj2 Dedicated Server is already running. Do you want to restart it?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) = vbYes Then
                    myProcess.Kill()
                    Exit For
                Else
                    Exit Sub
                End If
            End If
        Next

        Shell(ruta & "\CoJ2Game_x86_ds.exe", vbHide) ' vbNormalNoFocus) 'vbHide) ' 
    End Sub

    Private Sub btnConfigServer_Click(sender As Object, e As EventArgs) Handles btnConfigServer.Click
        Me.Hide()
        frmServerConfig.Show()
    End Sub

    Private Sub btnModding_Click(sender As Object, e As EventArgs) Handles btnModding.Click
        Me.Hide()
        frmArmas.Show()
    End Sub

    Private Sub FrmMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        Me.Dispose()
        Application.ExitThread()
        Application.Exit()
    End Sub

    Private Sub AboutModCojControllerToolStripMenuItem1_Click(sender As Object, e As EventArgs) Handles ArchiveToolStripMenuItem1.Click
        Me.Close()
    End Sub


    Private Sub HelpToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles HelpToolStripMenuItem.Click
        Cursor = Cursors.WaitCursor
        Help.ShowHelp(Me, Application.StartupPath & "\CoJ Mods.chm")
        Cursor = Cursors.Default
    End Sub

    Private Sub AboutModCojControllerToolStripMenuItem2_Click(sender As Object, e As EventArgs) Handles ArchiveToolStripMenuItem2.Click
        frmAcercade.Show()
    End Sub

    Private Sub CustomMapsToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles CustomMapsToolStripMenuItem.Click
        'The old cojmodding.com download flow is dead; open the current IranCoJ launcher/maps page instead.
        Process.Start("https://www.moddb.com/mods/irancoj-multiplayer-launcher/downloads/irancoj-launcher")
    End Sub

    ' ---------------------------------------------------------------------------
    ' Western UI theming for the launcher buttons.
    ' Loads plank art from <StartupPath>\images\ui\ (plank.png / plank_hover.png).
    ' If the art is missing, the buttons fall back to a leather-brown flat style so
    ' the launcher still looks themed (and works) before the art is added.
    ' ---------------------------------------------------------------------------
    Private Sub ApplyWesternTheme()
        Dim uiDir As String = System.IO.Path.Combine(Application.StartupPath, "images", "ui")
        plankNormal = TryLoadImage(System.IO.Path.Combine(uiDir, "plank.png"))
        plankHover = TryLoadImage(System.IO.Path.Combine(uiDir, "plank_hover.png"))
        If plankHover Is Nothing Then plankHover = plankNormal

        ' Load the main background from images\ui\bg.png (swappable without a rebuild).
        ' Falls back to the embedded designer BackgroundImage if the file is missing.
        Dim bg As Image = TryLoadImage(System.IO.Path.Combine(uiDir, "bg.png"))
        If bg IsNot Nothing Then PictureBox2.BackgroundImage = bg

        ' For the plank's transparent areas to show the cowboy background (instead of
        ' white), the buttons are reparented onto PictureBox2 and painted transparent.
        ' Clear any foreground Image so what composites behind them is the Zoom-fitted
        ' BackgroundImage (a foreground Image is drawn stretched and also isn't picked
        ' up by transparent children).
        PictureBox2.Image = Nothing

        StyleWesternButton(btnLaunchServer)
        StyleWesternButton(btnConfigServer)
        StyleWesternButton(btnModding)
    End Sub

    ' Loads an image without keeping the file locked (so the art can be swapped
    ' while the app is closed without file-in-use errors). Returns Nothing on failure.
    Private Function TryLoadImage(ByVal path As String) As Image
        Try
            If System.IO.File.Exists(path) Then
                Dim bytes() As Byte = System.IO.File.ReadAllBytes(path)
                Return Image.FromStream(New System.IO.MemoryStream(bytes))
            End If
        Catch
        End Try
        Return Nothing
    End Function

    Private Sub StyleWesternButton(ByVal b As Button)
        ' Reparent onto PictureBox2 so a transparent BackColor composites against the
        ' cowboy background directly behind the button. A Button parented to the Form
        ' cannot see through to a sibling PictureBox, so its transparent (plank) pixels
        ' render white; parenting it to the background control fixes that.
        If b.Parent IsNot PictureBox2 Then
            Dim onForm As Point = b.Location
            b.Parent = PictureBox2
            b.Location = New Point(onForm.X - PictureBox2.Left, onForm.Y - PictureBox2.Top)
            b.BringToFront()
        End If

        b.FlatStyle = FlatStyle.Flat
        b.FlatAppearance.BorderSize = 0
        b.FlatAppearance.MouseOverBackColor = Color.Transparent
        b.FlatAppearance.MouseDownBackColor = Color.Transparent
        b.UseVisualStyleBackColor = False
        b.BackColor = Color.Transparent
        b.ForeColor = Color.FromArgb(245, 235, 210)                 ' parchment / cream text
        b.Font = New Font("Rockwell", 13.0!, FontStyle.Bold)        ' slab-serif western feel (falls back if not installed)
        b.TextAlign = ContentAlignment.MiddleCenter
        b.Cursor = Cursors.Hand

        If plankNormal IsNot Nothing Then
            ' Image-based plank button with a hover swap.
            b.BackgroundImage = plankNormal
            b.BackgroundImageLayout = ImageLayout.Stretch
            AddHandler b.MouseEnter, Sub() b.BackgroundImage = plankHover
            AddHandler b.MouseLeave, Sub() b.BackgroundImage = plankNormal
        Else
            ' No art yet: leather-brown flat fallback so it still reads as western.
            b.BackColor = Color.FromArgb(74, 52, 38)                ' dark leather brown
            b.FlatAppearance.BorderColor = Color.FromArgb(150, 110, 60)  ' brass/tan border
            b.FlatAppearance.BorderSize = 2
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(105, 74, 48)
        End If
    End Sub
End Class