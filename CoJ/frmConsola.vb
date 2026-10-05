' =================================================================================
' frmConsole.vb - Server console form
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

Imports System.Environment
Imports System.IO

Public Class frmConsola
    ' Constante para los mensajes

    ' Recupera en un buffer el texto de la linea
    Const EM_GETLINE As Integer = &HC4
    ' Recupera la cantidad de lineas
    Const EM_GETLINECOUNT As Integer = &HBA

    ' Hwnd de la ventana
    Dim FormCargado As Boolean = False
    Dim LoadingMap As Boolean = True
    Dim commandActivAdLin1 As String = ""
    Dim commandActivAdLin2 As String = ""
    Dim commandActivAdLin3 As String = ""
    Dim commandActivGeneral As String = ""
    Dim GameUnbalanced As Boolean = False
    Dim btnApagarPulsado As Boolean = False

    Dim windowExist As Boolean = False
    Dim gameLogReader As New GameLogReader()

    'Auto-kick: nombre de jugador (en minúsculas) -> momento en que caduca el bloqueo.
    Private tempKicked As New Dictionary(Of String, Date)
    Private autoKickMinutes As Integer = 5

    ' Authenticated-admin detection (anti name-spoofing).
    ' The in-process mod (version.dll) validates /adminlogin against ServerAdmins.ini
    ' and publishes the set of currently-authenticated admins to "authed_admins.txt"
    ' next to the server exe. Each line is:  <name><TAB><rights>  (rights 0 or 1).
    ' The file is rewritten on every login and cleared on map change (auth resets
    ' per map), so we simply mirror whatever it currently says. When the mod is not
    ' loaded the file is absent and we fall back to the manual name list (lbAdmin).
    Private authedAdmins As New HashSet(Of String)
    Private authedStamp As Date = Date.MinValue

    ' Re-read authed_admins.txt only when it actually changed (keyed on write time),
    ' so this can be called freely on every console line without disk churn.
    Private Sub RefreshAuthedAdmins()
        Try
            Dim f As String = ruta & "\authed_admins.txt"
            If Not System.IO.File.Exists(f) Then
                If authedAdmins.Count > 0 Then authedAdmins.Clear()
                authedStamp = Date.MinValue
                Return
            End If
            Dim stamp As Date = System.IO.File.GetLastWriteTimeUtc(f)
            If stamp = authedStamp Then Return
            authedStamp = stamp
            authedAdmins.Clear()
            For Each ln As String In System.IO.File.ReadAllLines(f)
                Dim nm As String = ln.Split(ControlChars.Tab)(0).Trim()
                If nm <> "" Then authedAdmins.Add(nm)
            Next
        Catch
            ' Mid-write / locked file: keep the previous snapshot, retry next call.
        End Try
    End Sub

    ' True if version.dll reports this exact in-game name as /adminlogin-authenticated.
    Private Function IsAuthedAdmin(ByVal playerName As String) As Boolean
        RefreshAuthedAdmins()
        Return authedAdmins.Contains(playerName)
    End Function

    ' The single admin predicate used to gate !commands: authenticated (spoof-proof)
    ' OR present in the manual name list (fallback when the mod isn't loaded).
    Private Function IsAdmin(ByVal playerName As String) As Boolean
        Return IsAuthedAdmin(playerName) OrElse lbAdmin.Items.Contains(playerName)
    End Function

    ' Revoke an authenticated admin when they leave / are kicked. The mod (version.dll)
    ' only adds names on /adminlogin; the controller removes them here so a new player
    ' who reuses the name can't inherit the departed admin's rights before the next
    ' map change (when the mod clears the file). We drop the entry both in memory and
    ' from the file, rewriting it atomically so a concurrent read never sees it torn.
    Private Sub RemoveAuthedAdmin(ByVal playerName As String)
        Try
            authedAdmins.Remove(playerName)
            Dim f As String = ruta & "\authed_admins.txt"
            If Not System.IO.File.Exists(f) Then Return
            Dim kept As New List(Of String)
            For Each ln As String In System.IO.File.ReadAllLines(f)
                Dim nm As String = ln.Split(ControlChars.Tab)(0).Trim()
                If nm <> "" AndAlso nm <> playerName Then kept.Add(ln)
            Next
            Dim tmp As String = f & ".tmp"
            System.IO.File.WriteAllLines(tmp, kept.ToArray(), New System.Text.UTF8Encoding(False))
            System.IO.File.Replace(tmp, f, Nothing)
            ' Our own write bumps the mtime; sync the cache stamp so RefreshAuthedAdmins
            ' doesn't needlessly re-read the file we just wrote.
            authedStamp = System.IO.File.GetLastWriteTimeUtc(f)
        Catch
            ' Mod holds the file open / folder read-only: keep the in-memory removal;
            ' the next login or map change reconciles the file.
        End Try
    End Sub

    ' Rotates dgMaps so the map the server actually just loaded becomes row 0 (the
    ' current map). The controller otherwise only advances its cycle by one on
    ' "Game ended!", so a bare-console /mapset (or the in-game dll hook) that jumps to
    ' an arbitrary map would leave the tracked rotation one-or-more steps off - which
    ' is the bug where the controller showed the next map as one behind the real one.
    ' Driven by the authoritative "Loading map <X> mode <Y>" console line, this
    ' reconciles the grid to reality for every source of a change (!mapgo, /mapset, the
    ' dll hook, or "remember last map" on startup). It only reorders the controller's
    ' own display rows - it never writes server memory - so it can't conflict with
    ' !mapgo or the dll. A no-op when already in sync or the map isn't in the rotation.
    Private Sub ResyncMapCycle(ByVal loadedMap As String, ByVal loadedMode As String)
        Try
            If dgMaps.Rows.Count = 0 Then Return

            Dim rows As New List(Of String())
            For fila As Integer = 0 To dgMaps.Rows.Count - 1
                Dim c0 = dgMaps.Rows(fila).Cells(0).Value
                Dim c1 = dgMaps.Rows(fila).Cells(1).Value
                rows.Add(New String() {If(c0 Is Nothing, "", c0.ToString()), If(c1 Is Nothing, "", c1.ToString())})
            Next

            Dim idx As Integer = -1
            If Not MapGo.FindRowForLoadedMap(rows, loadedMap, loadedMode, idx) Then Return
            If idx <= 0 Then Return   ' already the current row, nothing to rotate

            ' Move the idx maps ahead of it to the bottom, preserving cyclic order, so
            ' the loaded map lands at row 0 (same rotation the "Game ended!" path uses).
            For j As Integer = 1 To idx
                Dim mapa As String = dgMaps.Rows(0).Cells(0).Value.ToString()
                Dim modo As String = dgMaps.Rows(0).Cells(1).Value.ToString()
                dgMaps.Rows.Remove(dgMaps.Rows(0))
                dgMaps.Rows.Add()
                dgMaps.Rows(dgMaps.Rows.Count - 1).Cells(0).Value = mapa
                dgMaps.Rows(dgMaps.Rows.Count - 1).Cells(1).Value = modo
            Next
        Catch
            ' Never let a rotation hiccup interrupt console processing.
        End Try
    End Sub

    ' Snapshots the live rotation exactly as MapGo expects it: row 0 = current map,
    ' each element {mapDisplay, modeDisplay}. Shared by !mapgo and !votemapgo.
    Private Function BuildMapRows() As List(Of String())
        Dim rows As New List(Of String())
        For fila As Integer = 0 To dgMaps.Rows.Count - 1
            Dim c0 = dgMaps.Rows(fila).Cells(0).Value
            Dim c1 = dgMaps.Rows(fila).Cells(1).Value
            rows.Add(New String() {If(c0 Is Nothing, "", c0.ToString()), If(c1 Is Nothing, "", c1.ToString())})
        Next
        Return rows
    End Function

    ' Performs a map jump exactly like the !mapgo command: resolves the target against
    ' the live rotation, sets the server's map index (via MapGo.TryMapGo), rotates
    ' dgMaps so the "Game ended!" from /mapnext lands on the target, then issues it.
    ' Shared so !mapgo and a passed !votemapgo behave identically. The technical diag
    ' stays in the local console; only the clean status is said to players.
    Private Sub EjecutarMapGo(ByVal mapaArg As String, ByVal modoArg As String)
        Dim rows As List(Of String()) = BuildMapRows()
        Dim mensaje As String = ""
        Dim diag As String = ""
        Dim pasos As Integer = 0
        Dim ok As Boolean = MapGo.TryMapGo(rows, mapaArg, modoArg, pasos, mensaje, diag)
        If diag <> "" Then lbConsola.Items.Add(Mid(Date.Now, InStr(Date.Now, " ") + 1, 8) & " " & diag)
        If ok Then
            For k As Integer = 1 To pasos - 1
                Dim mp = dgMaps.Rows(0).Cells(0).Value
                Dim md = dgMaps.Rows(0).Cells(1).Value
                dgMaps.Rows.Remove(dgMaps.Rows(0))
                dgMaps.Rows.Add()
                dgMaps.Rows(dgMaps.Rows.Count - 1).Cells(0).Value = mp
                dgMaps.Rows(dgMaps.Rows.Count - 1).Cells(1).Value = md
            Next
            SendToConsole("/adminsay " & mensaje)
            SendToConsole("/mapnext")
        Else
            SendToConsole("/adminsay " & mensaje)
            lbConsola.Items.Add(Mid(Date.Now, InStr(Date.Now, " ") + 1, 8) & " " & mensaje)
        End If
    End Sub

    Public Sub CloseLogReader()
        gameLogReader.Close()
    End Sub

    Private Sub btnEnviar_Click() Handles btnEnviar.Click
        SendToConsole(cboxMess.Text.Trim & " " & txtEnviaTexto.Text.Trim)
        txtEnviaTexto.Text = ""
        TimerReadConsoleInst.Enabled = True
    End Sub

    Private Sub frmConsola_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        Me.Hide()
        windowExist = Module1.windowSender.FindWindow()
        If windowExist Then
            If btnApagarPulsado = False Then
                If vApagado <> "restart" And vRestart = False Then
                    frmClosingServer.Show()
                End If
                If CerrarServidor = False Then
                    e.Cancel = True
                    Me.Show()
                ElseIf CerrarServidor = True Then
                    gameLogReader.Close()
                    vRestart = False
                    Me.Dispose()
                    FrmMain.Show()
                Else 'Colocado este bucle porque hay veces que la aplicación se queda colgada y oculta, a ver si deja de pasar
                    gameLogReader.Close()
                    Application.ExitThread()
                End If

            ElseIf btnApagarPulsado = True Then
                gameLogReader.Close()
                Me.Dispose()
                FrmMain.Show()
            End If
            'SendToConsole("/servershutdown")
        ElseIf btnApagarPulsado = True Then
            gameLogReader.Close()
            Me.Dispose()
            FrmMain.Show()
        Else 'Colocado este bucle porque hay veces que la aplicación se queda colgada y oculta, a ver si deja de pasar
            gameLogReader.Close()
            Application.ExitThread()
        End If

    End Sub


    Private Sub frmConsola_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.dgPlayersWarned.ColumnHeadersDefaultCellStyle.Font = New System.Drawing.Font("Segoe UI", 6.886957!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        My.Computer.FileSystem.CreateDirectory(Application.StartupPath & "\Logs")

        Me.TopLevel = True
        lbConsola.Items.Add(Mid(Date.Now, InStr(Date.Now, " ") + 1, 8) & " ********* STARTING SERVER *********")
        Call ReadFileMsg()
        Call ReadFilePlayer()
        Call ReadFileAdmin()
        Call ReadMaps()
        Call ReadProtectState()
        Call LoadListLogs()
        Call ReadFileCommand()
        Call CreaListaComandosActivos()
        Call ReadCheckMaps()
        Call ReadFileTabRules()

        If chkMess1.Checked = True Then
            Timer1.Enabled = True
        End If
        If chkMess2.Checked = True Then
            Timer2.Enabled = True
        End If
        If chkMess3.Checked = True Then
            Timer3.Enabled = True
        End If

        'Apply the western (leather/parchment/brass) theme to match FrmMain.
        WesternTheme.Apply(Me)

    End Sub

    Private Sub frmConsola_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        If TimerLoad.Enabled = False Then TimerLoad.Enabled = True
        Me.SendToBack()
        Me.Focus()
        Me.BringToFront()
        lblQuantityLogs.Text = (lbLogFilesList.SelectedIndex + 1).ToString.Trim & "/" & (lbLogFilesList.Items.Count).ToString.Trim
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        If txtAdMess1.Text.Trim = "" Then
            Exit Sub
        End If

        If Val(txtAdMessTime1.Text.Trim) = 0 Then
            Timer1.Enabled = False
            Exit Sub
        Else
            Timer1.Interval = Val(txtAdMessTime1.Text.Trim) * 1000
            'Dim vHwnd As Integer
            Dim vTexto As String = "/adminsay " & txtAdMess1.Text.Trim

            If LoadingMap = False Then SendToConsole(vTexto)
        End If
    End Sub


    Private Sub Timer2_Tick(sender As Object, e As EventArgs) Handles Timer2.Tick
        If txtAdMess2.Text.Trim = "" Then
            Exit Sub
        End If

        If Val(txtAdMessTime2.Text.Trim) = 0 Then
            Timer2.Enabled = False
            Exit Sub
        Else
            Timer2.Interval = Val(txtAdMessTime2.Text.Trim) * 1000
            'Dim vHwnd As Integer
            Dim vTexto As String = "/adminsay " & txtAdMess2.Text.Trim
            If LoadingMap = False Then SendToConsole(vTexto)
        End If
    End Sub

    Private Sub Timer3_Tick(sender As Object, e As EventArgs) Handles Timer3.Tick
        If txtAdMess3.Text.Trim = "" Then
            Exit Sub
        End If

        If Val(txtAdMessTime3.Text.Trim) = 0 Then
            Timer3.Enabled = False
            Exit Sub
        Else
            Timer3.Interval = Val(txtAdMessTime3.Text.Trim) * 1000
            'Dim vHwnd As Integer
            Dim vTexto As String = ""

            If cboxCabMess3.Text = "/mapnext" Or cboxCabMess3.Text = "/playerlist" Then
                vTexto = cboxCabMess3.Text
            Else
                vTexto = cboxCabMess3.Text & " " & txtAdMess3.Text.Trim
            End If

            If LoadingMap = False Then SendToConsole(vTexto)
        End If

    End Sub


    Dim TimeClose As Integer = 0
    Dim vApagado As String = ""
    Private Sub btnShutdown_Click(sender As Object, e As EventArgs) Handles btnShutdown.Click
        btnApagarPulsado = True
        vApagado = "shutdown"
        TimerCountDown.Enabled = True
        Timer1.Enabled = False
        Timer2.Enabled = False
        Timer3.Enabled = False
        TimerLoad.Enabled = False
        TimerReadConsole.Enabled = False
        TimerReadConsoleInst.Enabled = False
        'TimerReadPlayer.Enabled = False
        TimeClose = 15
    End Sub


    Private Sub chkMess1_CheckedChanged(sender As Object, e As EventArgs) Handles chkMess1.CheckedChanged
        If chkMess1.Checked = True Then
            If txtAdMessTime1.Text.Trim = "0" Or txtAdMessTime1.Text = "" Then
                txtAdMessTime1.Text = "30"
            End If
            Timer1.Interval = Val(txtAdMessTime1.Text.Trim) * 1000
            Timer1.Enabled = True
        Else
            Timer1.Enabled = False
        End If
    End Sub

    Private Sub chkMess2_CheckedChanged(sender As Object, e As EventArgs) Handles chkMess2.CheckedChanged
        If chkMess2.Checked = True Then
            If txtAdMessTime2.Text.Trim = "0" Or txtAdMessTime2.Text = "" Then
                txtAdMessTime2.Text = "30"
            End If
            Timer2.Interval = Val(txtAdMessTime2.Text) * 1000
            Timer2.Enabled = True
        Else
            Timer2.Enabled = False
        End If
    End Sub

    Private Sub chkMess3_CheckedChanged(sender As Object, e As EventArgs) Handles chkMess3.CheckedChanged
        If chkMess3.Checked = True Then
            If txtAdMessTime3.Text.Trim = "0" Or txtAdMessTime3.Text = "" Then
                txtAdMessTime3.Text = "30"
            End If
            Timer3.Interval = Val(txtAdMessTime3.Text.Trim) * 1000
            Timer3.Enabled = True
        Else
            Timer3.Enabled = False
        End If

    End Sub

    'Private Sub TimerShutDown_Tick(sender As Object, e As EventArgs) Handles TimerShutDown.Tick
    '    TimerShutDown.Enabled = False
    '    Me.Close()
    'End Sub

    Dim warningShutdown As Integer = 5
    Private Sub TimerCountDown_Tick(sender As Object, e As EventArgs) Handles TimerCountDown.Tick
        warningShutdown = warningShutdown - 1
        TimeClose = TimeClose - 1
        If TimeClose = -1 Then
            TimerCountDown.Enabled = False
            Me.Close()
        End If
        If TimeClose = 10 Then
            SendToConsole("/adminsay Starting to " & vApagado)
            SendToConsole("/servershutdown")
            Call writeLog()
        End If
        If TimeClose <= 10 And vApagado = "shutdown" Then
            vRestart = False
            btnShutdown.Text = "Shutdown (" & TimeClose.ToString.Trim & "s.)"
            If TimeClose = 9 Or TimeClose = 7 Or TimeClose = 5 Then
                Exit Sub
            End If
            lbConsola.Items.Add(Mid(Date.Now, InStr(Date.Now, " ") + 1, 8) & " " & "*****Shutting down in " & TimeClose & " seconds")
            lbConsola.SelectedIndex = (lbConsola.Items.Count - 1)
        ElseIf TimeClose <= 10 And vApagado = "restart" Then
            vRestart = True
            If TimeClose = 9 Or TimeClose = 7 Or TimeClose = 5 Then
                Exit Sub
            End If
            lbConsola.Items.Add(Mid(Date.Now, InStr(Date.Now, " ") + 1, 8) & " " & "*****Restarting in " & TimeClose & " seconds")
            lbConsola.SelectedIndex = (lbConsola.Items.Count - 1)
        ElseIf TimeClose > 10 Then
            SendToConsole("/adminsay Server is going to " & vApagado & " in " & warningShutdown & " seconds")
        End If

    End Sub

    Dim rutaLog As String = ""
    Private Sub writeLog()
        Dim Mes As String = Date.Now.Month.ToString
        Dim Dia As String = Date.Now.Day.ToString
        Dim Hora As String = Date.Now.Hour.ToString
        Dim Minuto As String = Date.Now.Minute.ToString
        Dim Segundo As String = Date.Now.Second.ToString

        If Len(Mes) = 1 Then Mes = "0" & Mes
        If Len(Dia) = 1 Then Dia = "0" & Dia
        If Len(Hora) = 1 Then Hora = "0" & Hora
        If Len(Minuto) = 1 Then Minuto = "0" & Minuto
        If Len(Segundo) = 1 Then Segundo = "0" & Segundo

        Dim FileLog As String = Date.Now.Year.ToString & "-" & Mes & "-" & Dia & "-" & Hora & "-" & Minuto & "-" & Segundo & "_console.log"
        rutaLog = Application.StartupPath & "\Logs"
        My.Computer.FileSystem.CreateDirectory(rutaLog)
        Call DeleLog()

        writer = File.AppendText(rutaLog & "\" & FileLog)

        For i = 0 To lbConsola.Items.Count - 1
            writer.WriteLine(lbConsola.Items(i))
        Next

        writer.Close()
    End Sub

    Private Sub DeleLog()
        Dim UltimoBackupExistente As String = ""

        Dim CantidadLogS As Integer = 0

        For Each foundFile As String In My.Computer.FileSystem.GetFiles(rutaLog)

            If foundFile.EndsWith(".log") Then
                CantidadLogS = CantidadLogS + 1
            End If

        Next

        'Mantendremos los ultimos 40 ficheros Console Logs
        Dim CantidadDeArchivosABorrar = CantidadLogS - 49
        If CantidadDeArchivosABorrar < 0 Then CantidadDeArchivosABorrar = 0

        For Each foundFile As String In My.Computer.FileSystem.GetFiles(rutaLog)
            Dim lengthRuta As Integer = rutaLog.Length
            Dim nameFile As String = foundFile.Substring(lengthRuta + 1).ToString()

            Dim Extension As String = ""
            If nameFile.Length > 5 Then Extension = nameFile.Substring(nameFile.Length - 4, 4)

            If CantidadDeArchivosABorrar > 0 Then
                My.Computer.FileSystem.DeleteFile(rutaLog & "\" & nameFile)
                CantidadDeArchivosABorrar = CantidadDeArchivosABorrar - 1
            End If
        Next
    End Sub


    Private Sub btnRestart_Click(sender As Object, e As EventArgs) Handles btnRestart.Click
        vApagado = "restart"
        'Usamos la variable "vRestart" (booleana) pública declarada en Module1
        'para desde frmArmas controlar que hay que volver a iniciar el server
        vRestart = True
        TimerCountDown.Enabled = True
        Timer1.Enabled = False
        Timer2.Enabled = False
        Timer3.Enabled = False
        TimerLoad.Enabled = False
        TimerReadConsole.Enabled = False
        TimerReadConsoleInst.Enabled = False
        TimeClose = 15
    End Sub

    Dim copiaCrash As String = ""
    Dim rutaCrashLog As String = ""
    'Dim NombreVentana = ruta & "\" & "CoJ2Game_x86_ds.exe"
    'Dim NombreVentana = Application.StartupPath & "\" & "CoJMods.Vshost.exe"
    'Dim HwndEscritura As String
    Private Sub TimerLoad_Tick(sender As Object, e As EventArgs) Handles TimerLoad.Tick
        'Buscar la ventana del servidor
        windowExist = Module1.windowSender.FindWindow()

        If Not windowExist Then
            ' MsgBox("Error. El Hwnd es 0 ", vbCritical)
        Else
            TimerLoad.Enabled = False
            btnEnviar.Enabled = True
            btnShutdown.Enabled = True
            btnRestart.Enabled = True
            btnMapNext.Enabled = True
            btnPlayerList.Enabled = True
            lbConsola.Enabled = True
            txtEnviaTexto.Enabled = True
            PanelAdminMess.Enabled = True
            txtAdMess1.Enabled = True
            txtAdMess2.Enabled = True
            txtAdMess3.Enabled = True
            txtAdMessTime1.Enabled = True
            txtAdMessTime2.Enabled = True
            txtAdMessTime3.Enabled = True
            chkMess1.Enabled = True
            chkMess2.Enabled = True
            chkMess3.Enabled = True
            cboxCabMess3.Enabled = True
            PanelAdmins.Enabled = True
            PanelPlayer.Enabled = True
            FormCargado = True
        End If

        '************Buscamos fichero salida consola*************
        rutaTemp = GetFolderPath(SpecialFolder.ApplicationData)
        rutaCrashLog = Mid(rutaTemp, 1, (Len(rutaTemp) - 8)) & "\Local\VirtualStore\Program Files (x86)\techland\out\crash.log"
        copiaCrash = Mid(rutaTemp, 1, (Len(rutaTemp) - 8)) & "\Local\VirtualStore\Program Files (x86)\techland\out\copiacrash.log"
        'MsgBox(rutaCrashLog)
        If My.Computer.FileSystem.FileExists(rutaCrashLog) Then
            TimerReadConsole.Enabled = True
        Else
            rutaCrashLog = Mid(rutaTemp, 1, (Len(rutaTemp) - 8)) & "\Local\VirtualStore\Program Files\techland\out\crash.log"
            copiaCrash = Mid(rutaTemp, 1, (Len(rutaTemp) - 8)) & "\Local\VirtualStore\Program Files\techland\out\copiacrash.log"
            If My.Computer.FileSystem.FileExists(rutaCrashLog) Then
                TimerReadConsole.Enabled = True
            Else
                rutaCrashLog = ruta & "\CoJ2\out\logs\crash.log"
                copiaCrash = ruta & "\CoJ2\out\logs\copiacrash.log"
                If My.Computer.FileSystem.FileExists(rutaCrashLog) Then
                    TimerReadConsole.Enabled = True
                Else
                    MsgBox("File not found. You have to reinstall Coj Dedicated Server")
                    SendToConsole("/servershutdown")
                    Me.Close()
                End If
            End If
        End If
        If My.Computer.FileSystem.FileExists(copiaCrash) Then
            My.Computer.FileSystem.DeleteFile(copiaCrash)
        End If
    End Sub

    ' Relaunch the dedicated server in place after an unexpected crash and re-attach
    ' the console (used by CheckProcess when Auto-restart is enabled). Because it sets
    ' FormCargado = False, CheckProcess will not fire again until TimerLoad has fully
    ' re-attached to the new server, so we never launch duplicate servers.
    Private Sub RestartCrashedServer()
        Try
            Dim exePath As String = ruta & "\CoJ2Game_x86_ds.exe"
            Dim ts As String = Mid(Date.Now, InStr(Date.Now, " ") + 1, 8)

            ' Stop reading/attaching to the dead server immediately.
            FormCargado = False
            windowExist = False
            TimerReadConsole.Enabled = False
            TimerReadConsoleInst.Enabled = False

            If Not System.IO.File.Exists(exePath) Then
                lbConsola.Items.Add(ts & " *** Server crashed and cannot be auto-restarted: game exe not found ***")
                Return
            End If

            lbConsola.Items.Add(ts & " *** Dedicated server crashed - auto-restarting... ***")

            ' Detach the reader from the dead server's log, relaunch, then let TimerLoad
            ' re-find the new server window and re-enable the UI + console reading.
            gameLogReader.Close()
            Shell(exePath, vbHide)
            TimerLoad.Enabled = True
        Catch
        End Try
    End Sub


    Dim tiempoMapaActivado As Boolean = False
    Dim readerConfig As StreamReader
    Dim lineaLeida As String = ""
    Dim minutes As Integer = 0
    'Dim totalMinutes As Integer = 0
    Dim seconds As Integer = 59 'Empezamos con 11 segundos de sobra mientras se incorporan jugadores
    Dim ciclosSinContar = 4
    Dim reactivaHwnd As Integer = 0
    Dim refreshTickCount As Integer = 0
    Private Sub TimerReadConsole_Tick(sender As Object, e As EventArgs) Handles TimerReadConsole.Tick

        'Cada vez que llegamos a 100 (5m.) reactivamos el Hwnd
        If reactivaHwnd = 100 * 6 Then  '(100 x 3 = 300 segundos)
            windowExist = Module1.windowSender.FindWindow()
            reactivaHwnd = 0
        Else
            reactivaHwnd = reactivaHwnd + 1
        End If


        'BORRAR: He movido la lectura a una rutina aparte porque se tendrá que leer desde más sitios
        LeerConsola()

        If refreshTickCount = 6 Then
            CheckMap()
            CheckProcess()
            refreshTickCount = 0
        Else
            refreshTickCount += 1
        End If


    End Sub

    Private Sub CheckMap()
        If tiempoMapaActivado Then
            Dim ceros As String = ""
            'Control del tiempo que resta
            If minutes > 0 Then
                'timeRemain = timeRemain - 3
                If seconds.ToString.Trim.Length = 1 Then
                    ceros = "0"
                Else
                    ceros = ""
                End If

                'Empezamos a contar hacia atrás 
                If ciclosSinContar <= 0 Then
                    If ciclosSinContar = 0 Then
                        SendToConsole("/adminsay Starting - " & lblCurrentMap.Tag & "  (" & bountyOrPointsToShow & " / Time: " & (Int(timeRemain / 60)).ToString & "m.)")
                        SendToConsole("/adminsay Starting - " & lblCurrentMap.Tag & "  (" & bountyOrPointsToShow & " / Time: " & (Int(timeRemain / 60)).ToString & "m.)")
                    End If
                    lblTimeMap.Text = "Time Map : " & (Int(timeRemain / 60)).ToString & ":00 m."
                    lblRemainTime.Text = "Rem. time : " & (minutes - 1).ToString & ":" & ceros & seconds & " m."
                    seconds = seconds - 3
                    If seconds = -1 Then
                        seconds = 59
                        minutes = minutes - 1
                    End If
                End If

                'Esta variable volverá a tomar valor 4 (x3sec. = 12sec.) en la misma rutina
                'que toma la variable "minutes" el tiempo al cargar un nuevo mapa
                ciclosSinContar = ciclosSinContar - 1



            ElseIf minutes = 0 Then
                lblRemainTime.Text = "0:00 m."
                tiempoMapaActivado = False
            End If
        End If
    End Sub

    Private Sub CheckProcess()
        'Solo comprobamos cuando la consola está totalmente enganchada al server.
        'Tras un auto-reinicio, FormCargado=False hasta que TimerLoad reengancha, lo
        'que evita relanzar servidores duplicados mientras arranca el nuevo.
        If Not FormCargado Then Exit Sub

        'Controlamos si sigue abierto el Server
        Dim myProcesses() As Process
        Dim myProcess As Process
        Dim ExistProcess As Boolean = False
        myProcesses = Process.GetProcesses()
        ' Iterate through the process array.
        For Each myProcess In myProcesses
            '  Console.WriteLine(myProcess.ProcessName)
            If myProcess.ProcessName = "CoJ2Game_x86_ds" Then
                ExistProcess = True
                Exit For
            End If
        Next

        'Si no existe el proceso mientras la consola está abierta:
        If ExistProcess = False Then
            'No interferir con un apagado/reinicio intencionado del admin.
            If btnApagarPulsado OrElse vRestart OrElse TimerCountDown.Enabled Then Exit Sub

            If chkAutoRestart.Checked Then
                'Crash inesperado -> relanzar en el sitio y reenganchar.
                RestartCrashedServer()
            Else
                'Comportamiento original: cerrar la consola.
                vRestart = True
                Me.Close()
            End If
        End If
    End Sub

    'Variables para el control de cambio de mapa  mNextVotesYes mNextPlayersVoted mNextNeededVotes mNextVoteIsActive
    Dim mNextVotesYes As Integer = 0
    Dim mNextVotesNo As Integer = 0
    Dim mNextPlayersVoted As String = ""
    ' Voters are tracked by UNIQUE player id, not by name: /nick changes a player's
    ' name silently (no console output), so a name-only dedup let a renamed player vote
    ' again and again. mNextIdsVoted holds the ids that already voted, each wrapped in
    ' spaces (" 3 ") so id "3" never matches inside "13". See ResolvePlayerId.
    Dim mNextIdsVoted As String = ""
    Dim mNextNeededVotes As Integer = 0
    Dim mNextVoteIsActive As Boolean = False
    ' --- Music stream commands (admin-only) -> voicechat relay's watched command file.
    ' The relay reads this file and acts on the exact command line (play <f>.mp3 / loop <f>.mp3
    ' / folder / skip / stop), so we write ONLY that line - no extra tokens. music_cmd.txt is
    ' mounted into the controller's OWN folder, so the path is just next to the .exe. Change
    ' this one line if you mount it elsewhere. See WriteMusicCommand for the atomic write.
    Dim musicCmdPath As String = Application.StartupPath & "\music_cmd.txt"
    ' --- !protect (admin-only): name whitelist. While ON, any player who joins with a name
    ' that is NOT in whitelist.txt (one name per line, next to the .exe, '#' = comment) is
    ' kicked at once. For a cheater who keeps changing IP and name, so IP bans can't hold.
    ' The file is re-read on every check, so it can be edited live without a restart. The
    ' ON/OFF state is persisted so protection survives a controller restart.
    Dim protectOn As Boolean = False
    Dim whitelistPath As String = Application.StartupPath & "\whitelist.txt"
    Dim changedTeam As Boolean = False
    Dim playerInVote As String = ""

    Private Sub LeerConsola()
        TimerReadConsole.Enabled = False

        gameLogReader.OpenFile(rutaCrashLog)
        ReadConsole()

        TimerReadConsole.Enabled = True
    End Sub

    Private Sub ReadConsole()
        Dim addConsole As Boolean = False
        'changedTeam = False
        Dim lineaPlayer As String = ""
        Dim PlayerLeido As String = ""
        Dim PlayerJoinOrLeave As String = ""
        Dim commandsEnabled As Boolean = False

        While True
            lineaLeida = gameLogReader.ReadLine(commandsEnabled)
            If lineaLeida Is Nothing Then
                Exit While
            End If

            'Seleccionamos las líneas que añadiremos al listbox
            addConsole = False

            'If creado para que acepte los corchetes en los nombres de los jugadores
            If InStr(lineaLeida, "<") > 0 And InStr(lineaLeida, ">") > 0 Then

                If InStr(lineaLeida, "[") > 0 Or InStr(lineaLeida, "]") > 0 Then

                    If InStr(lineaLeida, "<") < InStr(lineaLeida, "[") And InStr(lineaLeida, ">") > InStr(lineaLeida, "[") Then


                        PlayerLeido = Mid(lineaLeida, InStr(lineaLeida, "<") + 1, InStr(lineaLeida, ">") - (InStr(lineaLeida, "<") + 1))
                        addConsole = True
                    End If

                    If InStr(lineaLeida, "<") < InStr(lineaLeida, "]") And InStr(lineaLeida, ">") > InStr(lineaLeida, "]") Then


                        PlayerLeido = Mid(lineaLeida, InStr(lineaLeida, "<") + 1, InStr(lineaLeida, ">") - (InStr(lineaLeida, "<") + 1))
                        addConsole = True
                    End If

                End If
            End If

            If InStr(lineaLeida, "<") > 0 And InStr(lineaLeida, ">") > 0 And InStr(lineaLeida, "::") = 0 And InStr(lineaLeida, "[") = 0 _
                                          And InStr(lineaLeida, "]") = 0 And InStr(lineaLeida, "Undeclared variable <") = 0 _
                                          And Mid(lineaLeida, 1, 8) <> "WARNING!" Then
                ' If InStr(lineaLeida, "!") > 0 Then
                PlayerLeido = Mid(lineaLeida, InStr(lineaLeida, "<") + 1, InStr(lineaLeida, ">") - (InStr(lineaLeida, "<") + 1))
                'End If
                addConsole = True
                '**************Control palabras prohibidas********************
                Dim RowPlayer As Integer = -1
                If Not IsAdmin(PlayerLeido) Then

                    For i = 0 To lbWords.Items.Count - 1
                        If InStr(LCase(lineaLeida), lbWords.Items(i).ToString) > 0 Then
                            For u = 0 To dgPlayersWarned.Rows.Count - 1
                                If dgPlayersWarned.Rows(u).Cells(1).Value.ToString.Trim = PlayerLeido Then
                                    RowPlayer = u
                                    Exit For
                                End If
                            Next

                            'Si el jugador no estaba en la lista de avisados
                            If RowPlayer = -1 Then
                                dgPlayersWarned.Rows.Add()
                                RowPlayer = dgPlayersWarned.Rows.Count - 1
                                dgPlayersWarned.Rows(RowPlayer).Cells(0).Value = 1
                                dgPlayersWarned.Rows(RowPlayer).Cells(1).Value = PlayerLeido
                                lblWarnedPlayers.Text = "(" & (Val(Mid(lblWarnedPlayers.Text.Trim, 2, Len(lblWarnedPlayers.Text) - 2)) + 1).ToString.Trim & ")"
                                lblWarnings.Text = (Val(lblWarnings.Text) + 1).ToString.Trim
                                Call CheckRules(PlayerLeido, Val(dgPlayersWarned.Rows(dgPlayersWarned.Rows.Count - 1).Cells(0).Value))
                                Exit For
                            Else 'Si ya estaba advertido el jugador le sumamos un aviso y salimos para que no busque mas palabras en 1 misma frase
                                dgPlayersWarned.Rows(RowPlayer).Cells(0).Value = Val(dgPlayersWarned.Rows(RowPlayer).Cells(0).Value) + 1
                                lblWarnings.Text = (Val(lblWarnings.Text) + 1).ToString.Trim
                                Call CheckRules(PlayerLeido, Val(dgPlayersWarned.Rows(RowPlayer).Cells(0).Value))
                                Exit For
                            End If

                        End If
                    Next

                End If
                '************Fin control Palabras Prohibidas******************



            End If
            If InStr(lineaLeida, "Loaded map file") > 0 Or InStr(lineaLeida, "Running the game") > 0 _
                                                            Or InStr(lineaLeida, "profile signed in") > 0 _
                                                            Or InStr(lineaLeida, "Loading ") > 0 Or InStr(lineaLeida, "Dedicated server") > 0 _
                                                            Or InStr(lineaLeida, "left the game") > 0 Or InStr(lineaLeida, "has joined the game") > 0 _
                                                            Or InStr(lineaLeida, "has joined the LAWMEN (blue) team") > 0 _
                                                            Or InStr(lineaLeida, "has joined the OUTLAWS (red) team") > 0 _
                                                            Or InStr(lineaLeida, "has changed team to OUTLAWS (red)") > 0 _
                                                            Or InStr(lineaLeida, "has changed team to LAWMEN (blue)") > 0 _
                                                            Or InStr(lineaLeida, "has been kicked") > 0 Then

                If InStr(lineaLeida, "left the game") > 0 Or InStr(lineaLeida, "has joined the game") > 0 _
                                                            Or InStr(lineaLeida, "has joined the LAWMEN (blue) team") > 0 _
                                                            Or InStr(lineaLeida, "has joined the OUTLAWS (red) team") > 0 _
                                                            Or InStr(lineaLeida, "has changed team to OUTLAWS (red)") > 0 _
                                                            Or InStr(lineaLeida, "has changed team to LAWMEN (blue)") > 0 _
                                                            Or InStr(lineaLeida, "has been kicked") > 0 Then

                    'Tomamos aqui el nombre del jugador que usaremos si el juego es por equipos para añadirlo a los listbox red o blue.
                    If InStr(lineaLeida, """ has") - (InStr(lineaLeida, """") + 1) > 0 Then
                        PlayerJoinOrLeave = Mid(lineaLeida, InStr(lineaLeida, """") + 1, InStr(lineaLeida, """ has") - (InStr(lineaLeida, """") + 1))
                    End If

                    'Auto-kick: mantiene fuera 5 min a quien haya sido expulsado (por /kick
                    'de un admin en el juego o por !votekick). Detecta la expulsión en la
                    'línea "has been kicked" y vuelve a expulsarle si reaparece.
                    If chkAutoKick.Checked AndAlso PlayerJoinOrLeave <> "" Then
                        Dim keyName As String = PlayerJoinOrLeave.ToLower()
                        If InStr(lineaLeida, "has been kicked") > 0 Then
                            'No aplicamos el auto-kick temporal (5 min) a un jugador baneado: su
                            'reingreso lo gestiona la lista de baneados (expulsión permanente), no
                            'los 5 min, así no se le muestra "kicked recently" en lugar de "banned".
                            'No refrescamos si ya está: los 5 min cuentan desde la primera expulsión.
                            If Not tempKicked.ContainsKey(keyName) AndAlso FindInListbox(lbBanned, PlayerJoinOrLeave) = False Then
                                tempKicked(keyName) = Date.Now.AddMinutes(autoKickMinutes)
                            End If
                        ElseIf InStr(lineaLeida, "has joined the game") > 0 _
                            Or InStr(lineaLeida, "has joined the LAWMEN (blue) team") > 0 _
                            Or InStr(lineaLeida, "has joined the OUTLAWS (red) team") > 0 Then
                            'Un baneado que reaparece lo expulsa el bloque de baneados (más abajo)
                            'con mensaje de "banned"; aquí lo ignoramos para no pisarlo con "kicked recently".
                            If tempKicked.ContainsKey(keyName) AndAlso FindInListbox(lbBanned, PlayerJoinOrLeave) = True Then
                                tempKicked.Remove(keyName)
                            ElseIf tempKicked.ContainsKey(keyName) Then
                                If Date.Now < tempKicked(keyName) Then
                                    Dim minsLeft As Integer = CInt(Math.Ceiling((tempKicked(keyName) - Date.Now).TotalMinutes))
                                    SendToConsole("/kick " & PlayerJoinOrLeave & " auto-kick (recently kicked)")
                                    SendToConsole("/adminsay " & PlayerJoinOrLeave & " was auto-kicked (kicked recently). Try again in " & minsLeft.ToString.Trim & " min.")
                                Else
                                    tempKicked.Remove(keyName)
                                End If
                            End If
                        End If
                    End If

                    ' Authenticated admin left or was kicked -> revoke their /adminlogin
                    ' authorization at once (mirrors removing them from the old table).
                    If PlayerJoinOrLeave <> "" AndAlso _
                       (InStr(lineaLeida, "left the game") > 0 OrElse InStr(lineaLeida, "has been kicked") > 0) Then
                        RemoveAuthedAdmin(PlayerJoinOrLeave)
                    End If

                End If

                'Player "playerX" has joined the LAWMEN (blue) team
                '"playerX" has joined the OUTLAWS (red) team
                '"playerX" has changed team to OUTLAWS (red)
                '"playerX" has changed team to LAWMEN (blue)
                'has left the game
                'has joined the game
                'has been kicked
                'No players in game

                If InStr(lineaLeida, "::") = 0 And InStr(lineaLeida, "[") = 0 And InStr(lineaLeida, "]") = 0 _
                                               And InStr(lineaLeida, "Undeclared variable <") = 0 _
                                               And Mid(lineaLeida, 1, 8) <> "WARNING!" Then
                    addConsole = True
                End If
            End If

            'Si el mapa acaba de ser cargado, daremos un tiempo mayor antes de leer los players
            'para esperar a que entren
            If InStr(lineaLeida, "Loaded map file") > 0 Then
                LoadingMap = False
                lineaLeida = "******* " & lineaLeida & " *******"
                lblTeamRed.Text = "0"
                lblTeamBlue.Text = "0"
                LbPlayers.Items.Clear()
                lbPlayers2.Items.Clear()
                lbBlueTeam.Items.Clear()
                lbRedTeam.Items.Clear()
                lblNumPlayers.Text = "Players     : 0"
            End If

            If InStr(lineaLeida, "Game ended") > 0 Then
                If InStr(lineaLeida, "Game ended!") > 0 Then
                    LoadingMap = True
                    tiempoMapaActivado = False
                    If chkCleanPlayersWarned.Checked = True Then
                        dgPlayersWarned.Rows.Clear()
                    End If

                    Dim Mapa As String = dgMaps.Rows(0).Cells(0).Value.ToString
                    Dim Modo As String = dgMaps.Rows(0).Cells(1).Value.ToString
                    dgMaps.Rows.Remove(dgMaps.Rows(0))
                    dgMaps.Rows.Add()
                    dgMaps.Rows(dgMaps.Rows.Count - 1).Cells(0).Value = Mapa
                    dgMaps.Rows(dgMaps.Rows.Count - 1).Cells(1).Value = Modo

                    If chkRememberMaps.Checked = True Then
                        Call ReadAndWriteMapsInDedicatedServerSetting()
                    End If
                End If
                addConsole = True
            End If

            If InStr(lineaLeida, "Loading map <") > 0 And InStr(lineaLeida, "[") = 0 Then
                lblCurrentMap.Text = "Current map:  "
                Dim currMap As String = lineaLeida
                currMap = Mid(currMap, InStr(currMap, "<") + 1, Len(currMap) - InStr(currMap, "<"))
                lblCurrentMap.Text = lblCurrentMap.Text & UCase(Mid(currMap, 1, InStr(currMap, ">") - 1))
                'En la tag guardamos justamente solo el mapa con su gamemode
                lblCurrentMap.Tag = UCase(Mid(currMap, 1, InStr(currMap, ">") - 1))
                currMap = Mid(currMap, InStr(currMap, "<") + 1, Len(currMap) - InStr(currMap, "<"))
                lblCurrentMap.Text = lblCurrentMap.Text & "  <" & Mid(currMap, 1, InStr(currMap, ">") - 1) & ">"
                lblCurrentMap.Tag = lblCurrentMap.Tag & "  <" & Mid(currMap, 1, InStr(currMap, ">") - 1) & ">"
                If InStr(lineaLeida, "Posse") > 0 Then
                    lblTimeMap.Text = "Time Map : " & (Int(timePosse / 60)).ToString & ":00 m."
                    lblRemainTime.Text = "Rem. time : " & (Int(timePosse / 60)).ToString & ":00 m."
                    timeRemain = timePosse
                    bountyOrPointsToShow = bountyPosse
                End If
                If InStr(lineaLeida, "Wanted") > 0 Then
                    lblTimeMap.Text = "Time Map : " & (Int(timeWanted / 60)).ToString & ":00 m."
                    lblRemainTime.Text = "Rem. time : " & (Int(timeWanted / 60)).ToString & ":00 m."
                    timeRemain = timeWanted
                    bountyOrPointsToShow = bountyWanted
                End If
                If InStr(lineaLeida, "Shootout") > 0 Then
                    lblTimeMap.Text = "Time Map : " & (Int(timeShootout / 60)).ToString & ":00 m."
                    lblRemainTime.Text = "Rem. time : " & (Int(timeShootout / 60)).ToString & ":00 m."
                    timeRemain = timeShootout
                    bountyOrPointsToShow = bountyShootout
                End If
                If InStr(lineaLeida, "Manhunt") > 0 Then
                    lblTimeMap.Text = "Time Map : " & (Int(timeManhunt / 60)).ToString & ":00 m."
                    lblRemainTime.Text = "Rem. time : " & (Int(timeManhunt / 60)).ToString & ":00 m."
                    timeRemain = timeManhunt
                    bountyOrPointsToShow = pointsManhunt
                End If

                If InStr(lineaLeida, "Posse") > 0 Or InStr(lineaLeida, "Manhunt") > 0 Or InStr(lineaLeida, "W.W.Legends") > 0 Then
                    PanelTeams.Enabled = True
                    PanelTabTeams.Enabled = True

                Else
                    PanelTeams.Enabled = False
                    PanelTabTeams.Enabled = False
                End If

                lblBountyOrPoints.Text = bountyOrPointsToShow
                'Variable utilizada para ir descontando el tiempo de cada mapa
                'dentro de la rutina TimerReadConsole
                minutes = (Int(timeRemain / 60))
                seconds = 59

                'Variable para dar tiempo de espera desde que se activa el tiempo al unirse un jugador al juego
                'hasta que se une a algún equipo, toma valor 4 (ciclos de 3sec.), total 12 sec.
                ciclosSinContar = 4

                ' Resync the tracked rotation to whatever actually loaded (fixes the
                ' desync when a map is changed by /mapset or the dll hook, not !mapgo).
                Dim loadedMapName As String = ""
                Dim loadedModeName As String = ""
                Try
                    Dim afterMap As String = Mid(lineaLeida, InStr(lineaLeida, "<") + 1)   ' "Frisco> mode <Manhunt>"
                    loadedMapName = Trim(Mid(afterMap, 1, InStr(afterMap, ">") - 1))       ' "Frisco"
                    Dim afterMode As String = Mid(afterMap, InStr(afterMap, "<") + 1)      ' "Manhunt>"
                    loadedModeName = Trim(Mid(afterMode, 1, InStr(afterMode, ">") - 1))    ' "Manhunt"
                Catch
                End Try
                If loadedMapName <> "" AndAlso loadedModeName <> "" Then
                    ResyncMapCycle(loadedMapName, loadedModeName)
                End If
            End If

            'Loading map <Adobes> mode <Shootout>

            If InStr(lineaLeida, "Dedicated server") > 0 Then
                Me.Text = Me.Text & " - " & Mid(lineaLeida, InStr(lineaLeida, "<") + 1, InStr(lineaLeida, ">") - (InStr(lineaLeida, "<") + 1))
            End If


            '||||||||||||||||FRASES DE CONTROL DE PLAYERS Y EQUIPOS|||||||||||||||||||||
            'Player "playerX" has joined the LAWMEN (blue) team
            '"playerX" has joined the OUTLAWS (red) team
            '"playerX" has changed team to OUTLAWS (red)
            '"playerX" has changed team to LAWMEN (blue)
            'has left the game
            'has joined the game
            'has been kicked
            'No players in game

            If InStr(lineaLeida, "has been kicked") > 0 Then
                'SendToConsole("/playerlist")
                TimerGetPlayerList.Enabled = True
                LbPlayers.Items.Clear()
                lbPlayers2.Items.Clear()
                addConsole = True
                changedTeam = True
                If PanelTeams.Enabled = True Then
                    If lbRedTeam.Items.Contains(PlayerJoinOrLeave) Then lbRedTeam.Items.Remove(PlayerJoinOrLeave)
                    If lbBlueTeam.Items.Contains(PlayerJoinOrLeave) Then lbBlueTeam.Items.Remove(PlayerJoinOrLeave)
                    lblTeamBlue.Text = (Val(lbBlueTeam.Items.Count)).ToString
                    lblTeamRed.Text = (Val(lbRedTeam.Items.Count)).ToString
                End If
            End If

            If InStr(lineaLeida, "has joined the LAWMEN (blue) team") > 0 And PanelTeams.Enabled = True Then

                If Not lbBlueTeam.Items.Contains(PlayerJoinOrLeave) And Not lbRedTeam.Items.Contains(PlayerJoinOrLeave) Then
                    lbBlueTeam.Items.Add(PlayerJoinOrLeave)
                    lblTeamBlue.Text = (Val(lbBlueTeam.Items.Count)).ToString
                End If

                changedTeam = True

            End If
            If InStr(lineaLeida, "has joined the OUTLAWS (red) team") > 0 And PanelTeams.Enabled = True Then

                If Not lbRedTeam.Items.Contains(PlayerJoinOrLeave) And Not lbBlueTeam.Items.Contains(PlayerJoinOrLeave) Then
                    lbRedTeam.Items.Add(PlayerJoinOrLeave)
                    lblTeamRed.Text = (Val(lbRedTeam.Items.Count)).ToString
                End If

                changedTeam = True

            End If
            If InStr(lineaLeida, "has changed team to OUTLAWS (red)") > 0 And PanelTeams.Enabled = True Then
                If lbBlueTeam.Items.Contains(PlayerJoinOrLeave) Then lbBlueTeam.Items.Remove(PlayerJoinOrLeave)
                If Not lbRedTeam.Items.Contains(PlayerJoinOrLeave) Then lbRedTeam.Items.Add(PlayerJoinOrLeave)
                lblTeamBlue.Text = (Val(lbBlueTeam.Items.Count)).ToString
                lblTeamRed.Text = (Val(lbRedTeam.Items.Count)).ToString
                changedTeam = True

            End If
            If InStr(lineaLeida, "has changed team to LAWMEN (blue)") > 0 And PanelTeams.Enabled = True Then

                If lbRedTeam.Items.Contains(PlayerJoinOrLeave) Then lbRedTeam.Items.Remove(PlayerJoinOrLeave)
                If Not lbBlueTeam.Items.Contains(PlayerJoinOrLeave) Then lbBlueTeam.Items.Add(PlayerJoinOrLeave)
                lblTeamBlue.Text = (Val(lbBlueTeam.Items.Count)).ToString
                lblTeamRed.Text = (Val(lbRedTeam.Items.Count)).ToString
                changedTeam = True

            End If

            If InStr(lineaLeida, "No players in game") > 0 Then

                If PanelTeams.Enabled = True Then
                    lbBlueTeam.Items.Clear()
                    lbRedTeam.Items.Clear()
                End If

                lblTeamRed.Text = "0"
                lblTeamBlue.Text = "0"
                LbPlayers.Items.Clear()
                lbPlayers2.Items.Clear()
                lblNumPlayers.Text = "Players     : 0"
                addConsole = True
            End If
            If InStr(lineaLeida, "has left the game") > 0 Or InStr(lineaLeida, "has joined the game") > 0 Then

                'Comprobamos en que equipo estaba el jugador y actualizamos los jugadores del equipo que corresponda
                If InStr(lineaLeida, "has left the game") > 0 And PanelTeams.Enabled = True Then
                    If lbRedTeam.Items.Contains(PlayerJoinOrLeave) Then
                        lbRedTeam.Items.Remove(PlayerJoinOrLeave)
                        lblTeamRed.Text = (Val(lbRedTeam.Items.Count)).ToString
                        changedTeam = True
                    End If

                    If lbBlueTeam.Items.Contains(PlayerJoinOrLeave) Then
                        lbBlueTeam.Items.Remove(PlayerJoinOrLeave)
                        lblTeamBlue.Text = (Val(lbBlueTeam.Items.Count)).ToString
                    End If

                End If

                'Comprobamos primero si está en la lista de baneados
                'el jugador que se unió, no añade nada a la consola.
                If InStr(lineaLeida, "has joined the game") > 0 Then
                    tiempoMapaActivado = True
                    Dim Player As String = lineaLeida
                    Player = Mid(Player, InStr(Player, """") + 1, Len(Player) - InStr(Player, """"))
                    If InStr(Player, """") > 0 Then
                        Player = Mid(Player, 1, InStr(Player, """") - 1)
                        If FindInListbox(lbBanned, Player) = True Then
                            'Baneado permanente (solo lo conoce el controlador): lo re-expulsamos
                            'con /kick cada vez que reaparece, con un mensaje claro de baneo.
                            SendToConsole("/kick " & Player & " Permanently banned - appeal at IranCoJ")
                            SendToConsole("/adminsay " & Player & ": permanently banned. Appeal at IranCoJ.")
                        ElseIf protectOn Then
                            ' !protect: kick anyone whose name isn't whitelisted (admins exempt).
                            KickIfNotWhitelisted(Player)
                        End If
                    End If
                End If




                'Leeremos la lista de jugadores aquí y al iniciar cada mapa
                lineaLeida = "// " & lineaLeida
                addConsole = True
                LbPlayers.Items.Clear()
                lbPlayers2.Items.Clear()
                SendToConsole("/playerlist")
            End If

            If Val(Replace(((Val(lblTeamBlue.Text) - Val(lblTeamRed.Text)).ToString), "-", " ")) >= 2 And chkBalance.Checked = True And changedTeam = True Then
                GameUnbalanced = True
                If TimerKillBalance.Enabled = False And LoadingMap = False Then
                    SendToConsole("/adminsay Activated automatic team BALANCE in the Server. Please, BOTH teams have to be BALANCED NOW.")
                    SendToConsole("/adminsay Last players who joined to the larger team will be 'killed' by the server  every 5 seconds.")
                    SendToConsole("/adminsay Last players who joined to the larger team will be 'killed' by the server  every 5 seconds.")
                End If
                TimerKillBalance.Enabled = True
                changedTeam = False
            ElseIf Val(Replace(((Val(lblTeamBlue.Text) - Val(lblTeamRed.Text)).ToString), "-", " ")) < 2 And chkBalance.Checked = True And changedTeam = True Then
                GameUnbalanced = False
                If TimerKillBalance.Enabled = True And LoadingMap = False Then
                    SendToConsole("/adminsay Teams balance is OK now.")
                End If
                CiclosBalance = 0
                TimerKillBalance.Enabled = False
                changedTeam = False
            End If
            '||||||||||||||||FINAL FRASES DE CONTROL DE PLAYERS Y EQUIPOS|||||||||||||||||||||



            If InStr(lineaLeida, "accepted") > 0 Then
                addConsole = True
            End If



            If InStr(lineaLeida, "lobby") > 0 And InStr(lineaLeida, "disconnected") > 0 Then
                'Se ha apagado el servidor solo, aquí lo reiniciaremos
                vApagado = "restart"
                'Usamos la variable "vRestart" (booleana) pública declarada en Module1
                'para desde frmArmas controlar que hay que volver a iniciar el server
                vRestart = True
                TimerCountDown.Enabled = True
                Timer1.Enabled = False
                Timer2.Enabled = False
                Timer3.Enabled = False
                TimerLoad.Enabled = False
                TimerReadConsole.Enabled = False
                TimerReadConsoleInst.Enabled = False
                TimeClose = 10
                Call writeLog()
            End If

            If InStr(lineaLeida, "will be shutdown") > 0 And TimerCountDown.Enabled = False Then
                TimeClose = 9
                TimerCountDown.Enabled = True
                'TimerShutDown.Enabled = True
            End If





            If InStr(lineaLeida, "host id:") > 0 Then
                'Aqui pasaremos los jugadores a un ListBox antes de mandar la linea a la consola general
                lineaPlayer = Space(5 - Len(Mid(lineaLeida, InStr(lineaLeida, "host id: ") + 9, 5).Trim)) & Mid(lineaLeida, InStr(lineaLeida, "host id: ") + 9, 5) & "    -  " & Mid(lineaLeida, 1, InStr(lineaLeida, ", ") - 1)
                If FindInListbox(LbPlayers, lineaPlayer) = False Then
                    LbPlayers.Items.Add(lineaPlayer & " ")
                    lbPlayers2.Items.Add(lineaPlayer & " ")
                End If
                'Jugador añadido
                lineaLeida = ">>>> " & lineaLeida & " <<<<"
                addConsole = True
                lblNumPlayers.Text = "Players     : " & LbPlayers.Items.Count.ToString.Trim
            End If

            '********************************************************************
            '*******************COMMANDS Admin***********************************
            If IsAdmin(PlayerLeido) Then
                Dim comando As String = ""

                ' --- Usage hints: an admin command typed with no arguments replies with its
                ' argument hint (the admin !commandlist shows names only, to fit CoJ chat's 3
                ' lines). Only for commands whose toggle is on. The hints deliberately omit the
                ' "!": the controller's /adminsay output echoes back into the console, and an
                ' echo that still contained e.g. "!protect on/off" could re-trigger the command.
                If commandsEnabled Then
                    If chkKick.Checked AndAlso BareCommand(lineaLeida, "!kick") Then SendToConsole("/adminsay Usage: kick <name/id>")
                    If chkBan.Checked AndAlso BareCommand(lineaLeida, "!ban") Then SendToConsole("/adminsay Usage: ban <name/id>  (permanent)")
                    If chkUnban.Checked AndAlso BareCommand(lineaLeida, "!unban") Then SendToConsole("/adminsay Usage: unban <name>")
                    If chkKill.Checked AndAlso BareCommand(lineaLeida, "!kill") Then SendToConsole("/adminsay Usage: kill <name/id>")
                    If chkNewadmin.Checked AndAlso BareCommand(lineaLeida, "!newadmin") Then SendToConsole("/adminsay Usage: newadmin <name>")
                    If chkAdminsay.Checked AndAlso BareCommand(lineaLeida, "!adminsay") Then SendToConsole("/adminsay Usage: adminsay <message>")
                    If BareCommand(lineaLeida, "!addword") Then SendToConsole("/adminsay Usage: addword <word>")
                    If BareCommand(lineaLeida, "!balance") Then SendToConsole("/adminsay Usage: balance yes/no")
                End If

                If InStr(lineaLeida, "!addword") > 0 AndAlso Not BareCommand(lineaLeida, "!addword") Then
                    Dim Palabra As String = LCase(Mid(lineaLeida, (InStr(lineaLeida, "!addword") + 9), Len(lineaLeida) - (InStr(lineaLeida, "!addword") + 8)))
                    If Not lbWords.Items.Contains(Palabra) Then
                        AddItemsToListBoxOrdered(lbWords, Palabra)
                        Call WriteFileTabRules()
                    End If
                End If

                If commandsEnabled And (InStr(lineaLeida, "!mapnext") > 0 And chkMapnext.Checked = True) Then
                    SendToConsole("/mapnext")
                End If

                ' --- Bots (admin-only, no toggle). "!deploybot N" -> /deploybot N (N must be a
                ' whole number); "!removebot" -> /removebot. Replies avoid the trigger words.
                If commandsEnabled And InStr(lineaLeida, "!deploybot") > 0 Then
                    Dim botArg As String = Trim(Mid(lineaLeida, InStr(lineaLeida, "!deploybot") + Len("!deploybot")))
                    Dim botTok() As String = botArg.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                    Dim botCount As Integer
                    If botTok.Length >= 1 AndAlso Integer.TryParse(botTok(0), botCount) AndAlso botCount > 0 Then
                        SendToConsole("/deploybot " & botCount.ToString())
                    Else
                        SendToConsole("/adminsay Usage: deploybot <number>")
                    End If
                ElseIf commandsEnabled And InStr(lineaLeida, "!removebot") > 0 Then
                    SendToConsole("/removebot")
                End If

                ' --- !protect (admin-only): whitelist-only joins. "!protect on" / "!protect off";
                ' a bare "!protect" (or anything else) just reports the state and the usage. Refuses to turn on while
                ' whitelist.txt is missing or empty, so a bad file can never kick everyone.
                ' Turning it on also sweeps players already in the server.
                If commandsEnabled And InStr(lineaLeida, "!protect") > 0 Then
                    Dim protArg As String = LCase(Trim(Mid(lineaLeida, InStr(lineaLeida, "!protect") + Len("!protect"))))
                    If protArg <> "on" AndAlso protArg <> "off" Then
                        SendToConsole("/adminsay Protect is " & If(protectOn, "ON", "OFF") & ".  Usage: protect on/off  (whitelist.txt next to the controller)")
                    ElseIf protArg = "on" Then
                        Dim wl As HashSet(Of String) = LoadWhitelist()
                        If wl Is Nothing OrElse wl.Count = 0 Then
                            SendToConsole("/adminsay Protect: NOT enabled - whitelist.txt is missing or empty.")
                        Else
                            protectOn = True
                            WriteProtectState()
                            SendToConsole("/adminsay Protect: ON - only whitelisted names may join (" & wl.Count.ToString() & " names).")
                            ' Sweep everyone already connected (LbPlayers: "   <id>    -  <name> ").
                            For i As Integer = LbPlayers.Items.Count - 1 To 0 Step -1
                                Dim item As String = LbPlayers.Items(i).ToString()
                                Dim dashPos As Integer = InStr(item, "-")
                                If dashPos > 0 Then KickIfNotWhitelisted(Trim(Mid(item, dashPos + 1)), wl)
                            Next
                        End If
                    Else
                        protectOn = False
                        WriteProtectState()
                        SendToConsole("/adminsay Protect: OFF - anyone may join.")
                    End If
                End If

                If commandsEnabled And (InStr(lineaLeida, "!mapgo") > 0 And chkMapgo.Checked = True) Then
                    ' Matches "!mapgo" even with no arguments, so a bare/partial call gets the hint below.
                    Dim mapgoArgs As String = Trim(Mid(lineaLeida, InStr(lineaLeida, "!mapgo") + Len("!mapgo")))
                    Dim partes() As String = mapgoArgs.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                    If partes.Length >= 2 Then
                        Dim mapaArg As String = partes(0)
                        Dim modoArg As String = String.Join(" ", partes, 1, partes.Length - 1)
                        EjecutarMapGo(mapaArg, modoArg)
                    Else
                        SendToConsole("/adminsay Usage: mapgo <map> <mode>")
                    End If
                End If

                ' --- Music stream (admin-only, no toggle box). Writes a command to the
                ' voicechat relay's mounted command file via WriteMusicCommand. The relay
                ' parses "<verb> <file>" (verb + rest-of-line as filename), so we match its
                ' exact vocabulary and append ".mp3" to the song name the admin typed:
                '   !playmusic <song>          -> play <song>.mp3      (play once)
                '   !playmusic <song> loop     -> loop <song>.mp3      (repeat that track)
                '   !playmusic                 -> folder               (whole folder, looping)
                '   !stopmusic                 -> stop
                '   !skipmusic                 -> skip
                '   !shufflemusic              -> shuffle              (shuffle the folder)
                ' Order matters (stop/skip/shuffle before play) and the /adminsay confirmations
                ' avoid the '!...music' trigger words so their console echo can't re-fire the handler.
                If commandsEnabled And InStr(lineaLeida, "!stopmusic") > 0 Then
                    WriteMusicCommand("stop")
                    SendToConsole("/adminsay Music: stopped.")
                ElseIf commandsEnabled And InStr(lineaLeida, "!skipmusic") > 0 Then
                    WriteMusicCommand("skip")
                    SendToConsole("/adminsay Music: skipped to next track.")
                ElseIf commandsEnabled And InStr(lineaLeida, "!shufflemusic") > 0 Then
                    WriteMusicCommand("shuffle")
                    SendToConsole("/adminsay Music: shuffled the folder.")
                ElseIf commandsEnabled And InStr(lineaLeida, "!playmusic") > 0 Then
                    Dim musicArgs As String = Trim(Mid(lineaLeida, InStr(lineaLeida, "!playmusic") + Len("!playmusic")))
                    If musicArgs = "" Then
                        WriteMusicCommand("folder")
                        SendToConsole("/adminsay Music: playing the whole folder.")
                    Else
                        Dim musicTok() As String = musicArgs.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                        Dim song As String = musicTok(0)
                        If LCase(song).EndsWith(".mp3") = False Then song = song & ".mp3"
                        Dim loopMode As Boolean = (musicTok.Length >= 2 AndAlso LCase(musicTok(1)) = "loop")
                        If loopMode Then
                            WriteMusicCommand("loop " & song)
                            SendToConsole("/adminsay Music: now playing " & song & " (loop).")
                        Else
                            WriteMusicCommand("play " & song)
                            SendToConsole("/adminsay Music: now playing " & song & ".")
                        End If
                    End If
                End If

                If InStr(lineaLeida, "!balance yes") > 0 Then
                    chkBalance.Checked = True
                End If

                If InStr(lineaLeida, "!balance no") > 0 Then
                    chkBalance.Checked = False
                End If

                If commandsEnabled And (InStr(lineaLeida, "!kill ") > 0 And chkKill.Checked = True And FindInListbox(lbBanned, Trim(Mid(lineaLeida, InStr(lineaLeida, "kick ") + 4, (Len(lineaLeida) - (InStr(lineaLeida, "kick ") + 3))))) = False) Then
                    comando = "/kill " & Trim(Mid(lineaLeida, InStr(lineaLeida, "kill ") + 4, (Len(lineaLeida) - (InStr(lineaLeida, "kill ") + 3))))
                    SendToConsole(comando)
                End If

                If commandsEnabled And (InStr(lineaLeida, "!kick ") > 0 And chkKick.Checked = True And FindInListbox(lbBanned, Trim(Mid(lineaLeida, InStr(lineaLeida, "kick ") + 4, (Len(lineaLeida) - (InStr(lineaLeida, "kick ") + 3))))) = False) Then
                    comando = "/kick " & Trim(Mid(lineaLeida, InStr(lineaLeida, "kick ") + 4, (Len(lineaLeida) - (InStr(lineaLeida, "kick ") + 3))))
                    SendToConsole(comando)
                End If

                If commandsEnabled And (InStr(lineaLeida, "!adminsay ") > 0 And chkAdminsay.Checked = True) Then
                    comando = "/adminsay " & Trim(Mid(lineaLeida, InStr(lineaLeida, "adminsay ") + 8, (Len(lineaLeida) - (InStr(lineaLeida, "adminsay ") + 7))))
                    Dim lin As String = "-------------------------------------------------------------------------------------------------------------------------------------------------------"

                    SendToConsole("/adminsay " & Mid(lin, 1, Len(comando) - 10 + ((Len(comando) - 10) / 2)))
                    SendToConsole(comando)
                    SendToConsole("/adminsay " & Mid(lin, 1, Len(comando) - 10 + ((Len(comando) - 10) / 2)))
                End If

                If commandsEnabled And (InStr(lineaLeida, "!playerlist") > 0 And chkPlayerlistAd.Checked = True And PlayerLeido <> "SERVER") Then
                    Dim Line1 As String = ""
                    Dim Line2 As String = ""
                    Dim Line3 As String = ""
                    For i = 0 To LbPlayers.Items.Count - 1
                        If Line1.Length < 75 Then
                            Line1 = Line1 & " ;  " & Replace(LbPlayers.Items(i).ToString.Trim, "    -  ", "-")
                        ElseIf Line2.Length < 75 Then
                            Line2 = Line2 & " ;  " & Replace(LbPlayers.Items(i).ToString.Trim, "    -  ", "-")
                        ElseIf Line3.Length < 75 Then
                            Line3 = Line3 & " ;  " & Replace(LbPlayers.Items(i).ToString.Trim, "    -  ", "-")
                        End If
                    Next

                    If Line1.Length > 0 Then
                        SendToConsole("/adminsay" & Line1)
                    End If

                    If Line2.Length > 0 Then
                        SendToConsole("/adminsay" & Line2)
                    End If

                    If Line3.Length > 0 Then
                        SendToConsole("/adminsay" & Line3)
                    End If

                End If

                If commandsEnabled And (InStr(lineaLeida, "!banlist") > 0 And chkBanlist.Checked = True) Then
                    Dim Line1 As String = "/adminsay"
                    Dim Line2 As String = "/adminsay"
                    Dim Line3 As String = "/adminsay"
                    For i = 0 To lbBanned.Items.Count - 1
                        If Line1.Length < 85 Then
                            Line1 = Line1 & " ;  " & lbBanned.Items(i).ToString.Trim
                        ElseIf Line2.Length < 85 Then
                            Line2 = Line2 & " ;  " & lbBanned.Items(i).ToString.Trim
                        ElseIf Line3.Length < 85 Then
                            Line3 = Line3 & " ;  " & lbBanned.Items(i).ToString.Trim
                        End If
                    Next
                    If Line1.Trim = "/adminsay " And chkAdminsay.Checked = True Then
                        Line1 = "/adminsay No players in banned list"
                    End If
                    SendToConsole(Line1)
                    SendToConsole(Line2)
                    SendToConsole(Line3)
                End If

                If commandsEnabled And (InStr(lineaLeida, "!commandlist") > 0 And chkCommandlistAd.Checked = True And PlayerLeido <> "SERVER") Then
                    'Dim Line1 As String = "/adminsay (!commandlist) ; (!mapnext) ; (!maplist) ; (!votemapnext) ; (!newadmin <name>) ; (!kill <name/id>) ; (!kick <name/id>) "
                    'Dim Line2 As String = "/adminsay (!ban <name>) ; (!unban <name>) ; (!banlist) ; (!playerlist) ; (!adminsay) ; (!servershutdown) ; (!restart) "
                    'Dim Line3 As String = "/adminsay"

                    SendToConsole("/adminsay " & commandActivAdLin1)
                    SendToConsole("/adminsay " & commandActivAdLin2)
                    SendToConsole("/adminsay " & commandActivAdLin3)
                End If

                '!ban <name/id>: expulsión PERMANENTE. Igual que !kick manda /kick, pero
                'además añade al jugador a la lista de baneados para que el controlador lo
                'vuelva a expulsar cada vez que reaparezca (bloque "has joined the game"), sin
                'límite de tiempo (a diferencia del auto-kick de 5 min de !kick). Acepta id o
                'nombre como !kick; si se da un id se resuelve al nombre ACTUAL, porque la
                'reexpulsión al reingresar se compara por NOMBRE (el id cambia entre sesiones).
                If commandsEnabled And (InStr(lineaLeida, "!ban ") > 0 And chkBan.Checked = True) Then
                    Dim banArg As String = Trim(Mid(lineaLeida, InStr(lineaLeida, "!ban ") + Len("!ban ")))
                    If banArg <> "" Then
                        Dim banName As String = ResolvePlayerName(banArg)
                        If FindInListbox(lbBanned, banName) = False Then
                            lbBanned.Items.Add(Mid(Date.Now.ToString, 1, 10) & "   " & banName & " ")
                            Call WriteFileBanned()
                        End If
                        'Tanto !ban como !kick mandan /kick (el juego no tiene baneo propio; el
                        'baneo solo existe en el controlador). La diferencia: al estar ya en la
                        'lista de baneados, el auto-kick temporal de 5 min lo ignora (ver bloque
                        '"has been kicked") y su reingreso se bloquea SIEMPRE, no 5 min.
                        SendToConsole("/kick " & banArg & " Permanently banned - appeal at IranCoJ")
                    End If
                End If

                If commandsEnabled And (InStr(lineaLeida, "!unban ") > 0 And chkUnban.Checked = True) Then
                    comando = Trim(Mid(lineaLeida, InStr(lineaLeida, "unban ") + 5, (Len(lineaLeida) - (InStr(lineaLeida, "unban ") + 4))))
                    If FindInListbox(lbBanned, comando) Then
                        For i = 0 To lbBanned.Items.Count - 1
                            If InStr(lbBanned.Items(i), comando) > 0 Then
                                lbBanned.Items.Remove(lbBanned.Items(i))
                            End If
                        Next
                    End If

                    Call WriteFileBanned()
                End If

                If commandsEnabled And (InStr(lineaLeida, "!newadmin ") > 0 And chkNewadmin.Checked = True) Then
                    comando = Trim(Mid(lineaLeida, InStr(lineaLeida, "newadmin ") + 8, (Len(lineaLeida) - (InStr(lineaLeida, "newadmin ") + 7))))
                    If Not lbAdmin.Items.Contains(comando) Then lbAdmin.Items.Add(comando)
                    Call WriteFileAdmins()
                    SendToConsole("/adminsay " & comando & " has been added to Admins List.")
                End If

                If commandsEnabled And (InStr(lineaLeida, "!servershutdown") > 0 And chkServershutdown.Checked = True) Then
                    vApagado = "shutdown"
                    TimerCountDown.Enabled = True
                    Timer1.Enabled = False
                    Timer2.Enabled = False
                    Timer3.Enabled = False
                    TimerLoad.Enabled = False
                    TimerReadConsole.Enabled = False
                    TimerReadConsoleInst.Enabled = False
                    'TimerReadPlayer.Enabled = False
                    TimeClose = 15
                    Exit Sub
                End If

                If commandsEnabled And (InStr(lineaLeida, "!restart") > 0 And chkRestart.Checked = True And chkRestart.Checked = True) Then
                    vApagado = "restart"
                    'Usamos la variable "vRestart" (booleana) pública declarada en Module1
                    'para desde frmArmas controlar que hay que volver a iniciar el server
                    vRestart = True
                    TimerCountDown.Enabled = True
                    Timer1.Enabled = False
                    Timer2.Enabled = False
                    Timer3.Enabled = False
                    TimerLoad.Enabled = False
                    TimerReadConsole.Enabled = False
                    TimerReadConsoleInst.Enabled = False
                    TimeClose = 15
                End If

                If commandsEnabled And (InStr(lineaLeida, "!maplist") > 0 And TimerGetMapList.Enabled = False And PlayerLeido <> "SERVER" And chkMaplistAd.Checked = True) Then
                    'Mostramos hasta los 3 siguientes mapas (fila 0 es el mapa actual).
                    Dim nextCount As Integer = dgMaps.Rows.Count - 1
                    If nextCount > 3 Then nextCount = 3
                    Dim nextMapsList As String = "" & nextCount & " next maps are: "
                    For i = 1 To nextCount
                        nextMapsList = nextMapsList & "(" & i & "- " & dgMaps.Rows(i).Cells(0).Value.ToString.Trim & " " & dgMaps.Rows(i).Cells(1).Value.ToString.Trim & ")  "
                    Next
                    TimerGetMapList.Enabled = True
                    SendToConsole("/adminsay " & nextMapsList)
                End If


            ElseIf Not IsAdmin(PlayerLeido) Then
                '********************************************************************
                '*******************COMMANDS Normal Players***************************

                If commandsEnabled And (InStr(lineaLeida, "!commandlist") > 0 And PlayerLeido <> "SERVER" And chkCommandlist.Checked = True) Then
                    SendToConsole("/adminsay " & commandActivGeneral)
                End If

                If commandsEnabled And (InStr(lineaLeida, "!maplist") > 0 And TimerGetMapList.Enabled = False And PlayerLeido <> "SERVER" And chkMaplist.Checked = True) Then
                    'Mostramos hasta los 3 siguientes mapas (fila 0 es el mapa actual).
                    Dim nextCount As Integer = dgMaps.Rows.Count - 1
                    If nextCount > 3 Then nextCount = 3
                    Dim nextMapsList As String = "" & nextCount & " next maps are: "
                    For i = 1 To nextCount
                        nextMapsList = nextMapsList & "(" & i & "- " & dgMaps.Rows(i).Cells(0).Value.ToString.Trim & " " & dgMaps.Rows(i).Cells(1).Value.ToString.Trim & ")  "
                    Next
                    TimerGetMapList.Enabled = True
                    SendToConsole("/adminsay " & nextMapsList)
                End If

                If commandsEnabled And (InStr(lineaLeida, "!playerlist") > 0 And chkPlayerlistAd.Checked = True And PlayerLeido <> "SERVER") Then
                    Dim Line1 As String = ""
                    Dim Line2 As String = ""
                    Dim Line3 As String = ""
                    For i = 0 To LbPlayers.Items.Count - 1
                        If Line1.Length < 75 Then
                            Line1 = Line1 & " ;  " & Replace(LbPlayers.Items(i).ToString.Trim, "    -  ", "-")
                        ElseIf Line2.Length < 75 Then
                            Line2 = Line2 & " ;  " & Replace(LbPlayers.Items(i).ToString.Trim, "    -  ", "-")
                        ElseIf Line3.Length < 75 Then
                            Line3 = Line3 & " ;  " & Replace(LbPlayers.Items(i).ToString.Trim, "    -  ", "-")
                        End If
                    Next

                    If Line1.Length > 0 Then
                        SendToConsole("/adminsay" & Line1)
                    End If

                    If Line2.Length > 0 Then
                        SendToConsole("/adminsay" & Line2)
                    End If

                    If Line3.Length > 0 Then
                        SendToConsole("/adminsay" & Line3)
                    End If

                End If

            End If


            ' The vote-start runs for EVERY player (unlike admin commands, which the
            ' IsAdmin(PlayerLeido) gate shields from the controller's own console echoes). Without
            ' a guard, the !commandlist output - which prints "!votemapgo <map> <mode>", "!votekick",
            ' "!votemapnext" as text - echoes back and re-fires this block (e.g. parsing the literal
            ' "<mode>" and replying "unknown mode"). Two guards prevent that: the initiator must be a
            ' currently-connected player (an echo's scraped name is a placeholder like "map", never a
            ' real player), and the line must not carry the command-list separator " - " (a genuine
            ' single command never contains it).
            If commandsEnabled And InStr(lineaLeida, " - ") = 0 And FindInListbox(LbPlayers, PlayerLeido) = True _
               And (InStr(lineaLeida, "!votemapnext") > 0 And mNextVoteIsActive = False And chkVotemapnext.Checked = True _
               Or InStr(lineaLeida, "!votemapgo") > 0 And mNextVoteIsActive = False And chkVotemapgo.Checked = True _
               Or InStr(lineaLeida, "!votekick") > 0 And mNextVoteIsActive = False And chkVotekick.Checked = True) Then
                playerInVote = ""
                mapgoVoteMap = ""
                mapgoVoteMode = ""
                Dim line1 As String = ""
                Dim line2 As String = ""
                Dim voteReady As Boolean = False
                mNextNeededVotes = 0
                mNextVotesYes = 0
                mNextVotesNo = 0


                If PlayerLeido <> "SERVER" Then


                    If InStr(lineaLeida, "!votemapnext") > 0 Then
                        CommandInVote = "!votemapnext"
                        'Necesita más de la mitad de los jugadores del servidor (como votekick).
                        mNextNeededVotes = (LbPlayers.Items.Count \ 2) + 1
                        line1 = "/adminsay Starting vote to change next map.  " & dgMaps.Rows(1).Cells(0).Value.ToString.Trim _
                          & dgMaps.Rows(1).Cells(1).Value.ToString.Trim & ".  45 seconds to vote."
                        line2 = "/adminsay Type '!vote yes' or '!vote no'.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes)"
                        voteReady = True

                    ElseIf InStr(lineaLeida, "!votemapgo") > 0 Then
                        'Igual que !votemapnext pero saltando a un mapa concreto: "!votemapgo <map> <mode>".
                        'El objetivo se resuelve AHORA (sin tocar la memoria del servidor) para no arrancar
                        'un voto imposible; el salto real (MapGo.TryMapGo) se hace solo si el voto se aprueba.
                        Dim vgArgs As String = Trim(Mid(lineaLeida, InStr(lineaLeida, "!votemapgo") + Len("!votemapgo")))
                        Dim vgPartes() As String = vgArgs.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                        If vgPartes.Length >= 2 Then
                            Dim vgMap As String = vgPartes(0)
                            Dim vgMode As String = String.Join(" ", vgPartes, 1, vgPartes.Length - 1)
                            Dim vgRow As Integer = 0
                            Dim vgMsg As String = ""
                            If MapGo.ResolveTargetRow(BuildMapRows(), vgMap, vgMode, vgRow, vgMsg) Then
                                CommandInVote = "!votemapgo"
                                mapgoVoteMap = vgMap
                                mapgoVoteMode = vgMode
                                'Necesita más de la mitad de los jugadores del servidor (como votemapnext).
                                mNextNeededVotes = (LbPlayers.Items.Count \ 2) + 1
                                line1 = "/adminsay Starting vote to change map to " & vgMap & " " & vgMode & ".  45 seconds to vote."
                                line2 = "/adminsay Type '!vote yes' or '!vote no'.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes)"
                                voteReady = True
                            Else
                                'El objetivo no vale: informamos directamente (el voto no se inicia).
                                SendToConsole("/adminsay Vote map: " & vgMsg)
                            End If
                        Else
                            SendToConsole("/adminsay Usage: !votemapgo <map> <mode>")
                        End If

                    ElseIf InStr(lineaLeida, "!votekick") > 0 Then
                        'El nombre es todo lo que sigue a "!votekick". Usamos Mid de dos
                        'argumentos: si no se escribió nombre devuelve "" en lugar de
                        'lanzar una excepción (el bug original usaba longitud negativa).
                        Dim kickPos As Integer = InStr(lineaLeida, "!votekick") + Len("!votekick")
                        If kickPos <= Len(lineaLeida) Then
                            playerInVote = Trim(Mid(lineaLeida, kickPos))
                        Else
                            playerInVote = ""
                        End If
                        If playerInVote <> "" AndAlso FindInListbox(LbPlayers, playerInVote) = True And Not IsAdmin(playerInVote) Then
                            CommandInVote = "!votekick"
                            'Necesita más de la mitad de los jugadores del servidor.
                            mNextNeededVotes = (LbPlayers.Items.Count \ 2) + 1
                            line1 = "/adminsay Starting vote to kick " & playerInVote & ".  45 seconds to vote."
                            line2 = "/adminsay Type '!vote yes' or '!vote no'.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes)"
                            voteReady = True
                        ElseIf playerInVote = "" Then
                            SendToConsole("/adminsay Usage: votekick <name>")
                        Else
                            SendToConsole("/adminsay Vote kick: player '" & playerInVote & "' not found.")
                        End If
                    End If

                    'Solo arrancamos el voto cuando una rama lo validó (voteReady). Antes se usaba
                    'FindInListbox(LbPlayers, playerInVote) con playerInVote="" para votemapnext/mapgo,
                    'que casaba con el espacio de cualquier fila y arrancaba votos vacíos (0/0), dejando
                    'mNextVoteIsActive colgado y corrompiendo el siguiente !votemapnext.
                    If voteReady Then
                        mNextVotesYes = 0
                        mNextVotesNo = 0
                        mNextPlayersVoted = ""
                        mNextIdsVoted = ""

                        If LbPlayers.Items.Count = 2 Then mNextNeededVotes = 2
                        mNextVoteIsActive = True
                        CountDownVotes = 48
                        TimerVote.Enabled = True

                        ' Refresh the roster so name->id lookups (ResolvePlayerId) see current
                        ' nicks during this vote. A /nick change prints nothing to the console,
                        ' so without this the id behind a renamed player would go stale.
                        SendToConsole("/playerlist")

                        SendToConsole(line1)
                        SendToConsole(line2)
                    End If
                End If
            End If


            If commandsEnabled And (InStr(lineaLeida, "!vote yes") > 0 And mNextVoteIsActive = True And InStr(lineaLeida, "Type '!vote yes'") = 0) Then
                ' Dedup by unique player id, not name: a /nick rename would otherwise let the
                ' same player vote repeatedly. If the name can't be resolved to an id (not in
                ' the current roster) the vote is ignored - which also blocks the rename exploit.
                Dim voterId As String = ResolvePlayerId(PlayerLeido)
                If voterId <> "" AndAlso InStr(mNextIdsVoted, " " & voterId & " ") = 0 Then
                    mNextVotesYes = mNextVotesYes + 1
                    mNextIdsVoted = mNextIdsVoted & " " & voterId & " "
                    Dim line2 As String = ""

                    If CommandInVote = "!votemapnext" Then
                        line2 = "/adminsay Type '!vote yes' or '!vote no'.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes).  " & CountDownVotes.ToString.Trim & " seconds remaining" _
                                          & " to change to next map.  " & dgMaps.Rows(1).Cells(0).Value.ToString.Trim _
                                          & " " & dgMaps.Rows(1).Cells(1).Value.ToString.Trim & "."
                        SendToConsole(line2)

                    ElseIf CommandInVote = "!votemapgo" Then
                        line2 = "/adminsay Type '!vote yes' or '!vote no'.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes).  " & CountDownVotes.ToString.Trim & " seconds remaining" _
                                          & " to change map to " & mapgoVoteMap & " " & mapgoVoteMode & "."
                        SendToConsole(line2)

                    ElseIf CommandInVote = "!votekick" Then
                        line2 = "/adminsay Type '!vote yes' or '!vote no'.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes).  " & CountDownVotes.ToString.Trim & " seconds remaining" _
                                          & " to KICK to the player '" & playerInVote & "'."
                        SendToConsole(line2)
                    End If


                    If mNextVotesYes >= mNextNeededVotes Then

                        If CommandInVote = "!votemapnext" Then
                            SendToConsole("/mapnext")
                            SendToConsole("/adminsay Vote Passed.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes)")
                        ElseIf CommandInVote = "!votemapgo" Then
                            SendToConsole("/adminsay Vote Passed.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes)")
                            'Ejecuta el salto igual que el comando !mapgo (fija el índice del server y rota dgMaps).
                            EjecutarMapGo(mapgoVoteMap, mapgoVoteMode)
                        ElseIf CommandInVote = "!votekick" Then
                            If FindInListbox(lbBanned, playerInVote) = False And Not IsAdmin(playerInVote) Then
                                SendToConsole("/kick " & playerInVote & " kick by players' votes")
                                SendToConsole("/adminsay Vote Passed.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes). " & playerInVote & " was kicked out!!!")
                            End If
                        End If

                        TimerVote.Enabled = False
                        mNextVoteIsActive = False
                    End If
                End If

            End If

            '!vote no: cuenta el voto en contra (un voto por jugador, no puede votar sí y no).
            'Filtramos el eco del recordatorio del controlador ("Type '!vote yes' or '!vote no'")
            'con la misma guarda que el bloque de "yes" para no contarlo como voto. Si la mayoría
            'vota no, el voto ya no puede pasar y lo cerramos en el momento.
            If commandsEnabled And (InStr(lineaLeida, "!vote no") > 0 And mNextVoteIsActive = True And InStr(lineaLeida, "Type '!vote yes'") = 0) Then
                ' Same id-based dedup as the "yes" branch (a player can't vote both yes and no,
                ' and can't re-vote by renaming). Unresolvable name => ignored.
                Dim voterId As String = ResolvePlayerId(PlayerLeido)
                If voterId <> "" AndAlso InStr(mNextIdsVoted, " " & voterId & " ") = 0 Then
                    mNextVotesNo = mNextVotesNo + 1
                    mNextIdsVoted = mNextIdsVoted & " " & voterId & " "
                    SendToConsole("/adminsay Vote: " & mNextVotesYes.ToString.Trim & " yes / " & mNextVotesNo.ToString.Trim & " no  (need " & mNextNeededVotes.ToString.Trim & " yes).  " & CountDownVotes.ToString.Trim & "s left.")
                    If mNextVotesNo >= mNextNeededVotes Then
                        SendToConsole("/adminsay Vote not Passed.  (" & mNextVotesYes.ToString.Trim & " yes / " & mNextVotesNo.ToString.Trim & " no).  Majority voted no.")
                        TimerVote.Enabled = False
                        mNextVoteIsActive = False
                    End If
                End If
            End If

            'mNextVoteIsActive = False
            If commandsEnabled And (InStr(lineaLeida, "Vote finished") > 0) Then
                SendToConsole("/adminsay Vote not Passed.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes)")
            End If

            If addConsole = True Then
                Dim LineToAdd1 As String = Mid(Date.Now, InStr(Date.Now, " ") + 1, 8) & " " & lineaLeida
                Dim LineToAdd2 As String = ""

                'Si la línea a enviar a la consola es mayor de 80 la enviamos a la funcion RecortaLineToAdd que la envia
                'como 2 lineas separadas al listbox de la consola
                If LineToAdd1.Length > 85 Then
                    RecortaLineToAdd(LineToAdd1)
                    'lbConsola.Items.Add(LineToAdd1)
                    'lbConsola.Items.Add(LineToAdd2)
                Else
                    lbConsola.Items.Add(LineToAdd1)
                End If

                ' lbConsola.Items.Add(Mid(Date.Now, InStr(Date.Now, " ") + 1, 8) & " " & lineaLeida)
                lbConsola.SelectedIndex = (lbConsola.Items.Count - 1)
            End If
        End While
    End Sub

    'Dim LbPlayersSinId As New ListBox
    'Private Sub LeerLbPlayersSinId()
    '    LbPlayers.Items.Clear()
    '    For i = 0 To LbPlayers.Items.Count - 1
    '        LbPlayersSinId.Items.Add(Mid(LbPlayers.Items(i), InStr(LbPlayers.Items(i), "-") + 2, LbPlayers.Items(i).ToString.Length - (InStr(LbPlayers.Items(i), "-") + 2)))
    '    Next
    'End Sub



    'Contendrá lo que estamos votando (!votemapnext, !voteban player, !votekick player/id)
    Dim CommandInVote As String = ""
    'Mapa/modo objetivo de un !votemapgo en curso (se resuelve al iniciar el voto y se
    'ejecuta con EjecutarMapGo si el voto se aprueba). Vacío = no hay votemapgo activo.
    Dim mapgoVoteMap As String = ""
    Dim mapgoVoteMode As String = ""
    Dim CountDownVotes As Integer = 48
    Private Sub TimerVote_Tick(sender As Object, e As EventArgs) Handles TimerVote.Tick
        If CountDownVotes <= 0 Then
            TimerVote.Enabled = False
            SendToConsole("/adminsay Vote finished!")
            mNextVoteIsActive = False
        End If
        CountDownVotes = CountDownVotes - 1

        If CountDownVotes = 23 Then
            If CommandInVote = "!votemapnext" Then
                SendToConsole("/adminsay (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes). 20 seconds remaining" _
                         & " to change to next map.  " & dgMaps.Rows(1).Cells(0).Value.ToString.Trim & " " & dgMaps.Rows(1).Cells(1).Value.ToString.Trim & ".")
            ElseIf CommandInVote = "!votemapgo" Then
                SendToConsole("/adminsay (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes). 20 seconds remaining" _
                         & " to change map to " & mapgoVoteMap & " " & mapgoVoteMode & ".")
            ElseIf CommandInVote = "!votekick" Then
                SendToConsole("/adminsay Type '!vote yes' or '!vote no'.  (" & mNextVotesYes.ToString.Trim & "/" & mNextNeededVotes.ToString.Trim & " votes).  " & CountDownVotes.ToString.Trim & " seconds remaining" _
                                          & " to KICK to the player '" & playerInVote & "'.")
            End If

        End If
    End Sub

    'La sacamos fuera para que no cambien de color las lineas demasiado largas al cortarse
    Dim myBrushlbConsola As Brush
    Private Sub lbConsola_DrawItem(sender As Object, e As DrawItemEventArgs) Handles lbConsola.DrawItem
        ' e.DrawBackground()
        ' Define the default color of the brush as black.

        ' Determine the color of the brush to draw each item based on the index of the item to draw.

        'SELECCION DEL COLOR DEPENDIENDO DEL TIPO DE MENSAJE
        'CON lbConsola.Items(e.Index) LEEMOS EL ITEM QUE SE ESTÁ AÑADIENDO
        'PARA COMPROBAR EL TIPO DE MENSAJE QUE SE VA A ESCRIBIR
        'MsgBox(lbConsola.Items(e.Index))

        If Mid(lbConsola.Items(e.Index), InStr(lbConsola.Items(e.Index), ":") + 7, 1) = "/" Then
            myBrushlbConsola = Brushes.Gray
            'ElseIf lbConsola.Items(e.Index) = ... Then
        ElseIf Mid(lbConsola.Items(e.Index), InStr(lbConsola.Items(e.Index), ":") + 7, 8) = "<SERVER>" Then
            myBrushlbConsola = Brushes.Red
        ElseIf Mid(lbConsola.Items(e.Index), InStr(lbConsola.Items(e.Index), ":") + 7, 8) <> "<SERVER>" And Mid(lbConsola.Items(e.Index), InStr(lbConsola.Items(e.Index), ":") + 7, 1) = "<" Then
            myBrushlbConsola = Brushes.Blue
        ElseIf Mid(lbConsola.Items(e.Index), InStr(lbConsola.Items(e.Index), ":") + 7, 1) = "/" Then
            myBrushlbConsola = Brushes.OrangeRed
        ElseIf Mid(lbConsola.Items(e.Index), 1, 8) = "        " Then
            'Este caso se da al partir una línea, no eliminar.
            'Solo hay que detectar este caso para que use el ultimo brush
        Else
            myBrushlbConsola = Brushes.Green
        End If

        ' Draw the current item text based on the current Font and the custom brush settings.
        e.Graphics.DrawString(lbConsola.Items(e.Index), e.Font, myBrushlbConsola, New RectangleF(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height))

        ' If the ListBox has focus, draw a focus rectangle around the selected item.
        e.DrawFocusRectangle()
    End Sub

    'La sacamos fuera para que no cambien de color las lineas demasiado largas al cortarse
    Dim myBrushlbOldChats As Brush
    Private Sub lbOldChats_DrawItem(sender As Object, e As DrawItemEventArgs) Handles lbOldChats.DrawItem
        e.DrawBackground()
        ' Define the default color of the brush as black.


        ' Determine the color of the brush to draw each item based on the index of the item to draw.

        'SELECCION DEL COLOR DEPENDIENDO DEL TIPO DE MENSAJE
        'CON lbOldChats.Items(e.Index) LEEMOS EL ITEM QUE SE ESTÁ AÑADIENDO
        'PARA COMPROBAR EL TIPO DE MENSAJE QUE SE VA A ESCRIBIR
        'MsgBox(lbOldChats.Items(e.Index))

        If Mid(lbOldChats.Items(e.Index), InStr(lbOldChats.Items(e.Index), ":") + 7, 1) = "/" Then
            myBrushlbOldChats = Brushes.Gray
            'ElseIf lbOldChats.Items(e.Index) = ... Then
        ElseIf Mid(lbOldChats.Items(e.Index), InStr(lbOldChats.Items(e.Index), ":") + 7, 8) = "<SERVER>" Then
            myBrushlbOldChats = Brushes.Red
        ElseIf Mid(lbOldChats.Items(e.Index), InStr(lbOldChats.Items(e.Index), ":") + 7, 8) <> "<SERVER>" And Mid(lbOldChats.Items(e.Index), InStr(lbOldChats.Items(e.Index), ":") + 7, 1) = "<" Then
            myBrushlbOldChats = Brushes.Blue
        ElseIf Mid(lbOldChats.Items(e.Index), InStr(lbOldChats.Items(e.Index), ":") + 7, 1) = "/" Then
            myBrushlbOldChats = Brushes.OrangeRed
        ElseIf Mid(lbOldChats.Items(e.Index), 1, 8) = "        " Then
            'Este caso se da al partir una línea, no eliminar.
            'Solo hay que detectar este caso para que use el ultimo brush
        Else
            myBrushlbOldChats = Brushes.Green
        End If

        ' Draw the current item text based on the current Font and the custom brush settings.
        e.Graphics.DrawString(lbOldChats.Items(e.Index), e.Font, myBrushlbOldChats, New RectangleF(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height))

        ' If the ListBox has focus, draw a focus rectangle around the selected item.
        e.DrawFocusRectangle()
    End Sub


    Private Sub TimerReadConsoleInst_Tick(sender As Object, e As EventArgs) Handles TimerReadConsoleInst.Tick
        TimerReadConsoleInst.Enabled = False
        LeerConsola()
    End Sub

    Sub SendToConsole(vText As String)

        Dim LineToAdd1 As String = Mid(Date.Now, InStr(Date.Now, " ") + 1, 8) & " " & vText
        Dim LineToAdd2 As String = ""

        'Si la linea es mayor de 85 la enviamos partida desde la Subs Recortalinetoadd
        If LineToAdd1.Length > 85 Then
            RecortaLineToAdd(LineToAdd1)
            'lbConsola.Items.Add(LineToAdd1)
            'lbConsola.Items.Add(LineToAdd2)
        Else
            lbConsola.Items.Add(LineToAdd1)
        End If

        lbConsola.SelectedIndex = (lbConsola.Items.Count - 1)
        Module1.windowSender.SendMessageToConsole(vText)
    End Sub

    Private Sub RecortaLineToAdd(line1 As String)
        Dim longitud As Integer = 75
        Dim lenCadena1 As Integer = Len(line1)
        Dim line2 As String = ""

        If line1.Length > longitud Then
            Do While Mid(line1, longitud, 1) <> " "
                longitud = longitud - 1
            Loop

            line2 = Mid(line1, longitud, (lenCadena1 - longitud))
            line1 = (Mid(line1, 1, longitud)).Trim
            lbConsola.Items.Add(line1)
            lbConsola.Items.Add("             " & line2)
        End If
    End Sub


    Private Sub btnPlayerList_Click(sender As Object, e As EventArgs) Handles btnPlayerList.Click
        Dim vTexto As String = "/playerlist"
        LbPlayers.Items.Clear()
        lbPlayers2.Items.Clear()
        SendToConsole(vTexto)
    End Sub

    Private Sub btnMapNext_Click(sender As Object, e As EventArgs) Handles btnMapNext.Click
        Dim vTexto As String = "/mapnext"
        SendToConsole(vTexto)
    End Sub

    Private Sub WriteFileMsg()
        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Config")
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\msg.ini") Then
            My.Computer.FileSystem.DeleteFile(rutaAppData & "\Config\msg.ini")
        End If
        writer = File.AppendText(rutaAppData & "\Config\msg.ini")
        writer.WriteLine(txtAdMess1.Text)
        writer.WriteLine(txtAdMess2.Text)
        writer.WriteLine(txtAdMess3.Text)
        writer.WriteLine(txtAdMessTime1.Text)
        writer.WriteLine(txtAdMessTime2.Text)
        writer.WriteLine(txtAdMessTime3.Text)
        writer.WriteLine(cboxCabMess3.Text)
        writer.Close()
    End Sub

    Private Sub ReadFileMsg()
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\msg.ini") Then
            ' MsgBox(Application.StartupPath & "\Config\msg.ini")
            readerConfig = File.OpenText(rutaAppData & "\Config\msg.ini")
            txtAdMess1.Text = readerConfig.ReadLine()
            txtAdMess2.Text = readerConfig.ReadLine()
            txtAdMess3.Text = readerConfig.ReadLine()
            txtAdMessTime1.Text = readerConfig.ReadLine()
            txtAdMessTime2.Text = readerConfig.ReadLine()
            txtAdMessTime3.Text = readerConfig.ReadLine()
            cboxCabMess3.Text = readerConfig.ReadLine()
            readerConfig.Close()
        End If
    End Sub

    Private Sub txtAdMessTime1_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txtAdMessTime1.KeyPress, txtAdMessTime2.KeyPress, txtAdMessTime3.KeyPress
        If Not IsNumeric(e.KeyChar) And Not Char.IsControl(e.KeyChar) Then
            e.Handled = True
        End If
    End Sub


    Private Sub txtAdMess1_TextChanged(sender As Object, e As EventArgs) Handles txtAdMess1.TextChanged, txtAdMess2.TextChanged, txtAdMess3.TextChanged
        If FormCargado = True Then
            Call WriteFileMsg()
        End If
    End Sub

    Private Sub txtAdMessTime1_TextChanged(sender As Object, e As EventArgs) Handles txtAdMessTime1.TextChanged, txtAdMessTime2.TextChanged, txtAdMessTime3.TextChanged
        If FormCargado = True Then
            Call WriteFileMsg()
        End If
    End Sub


    Private Sub txtAdMessTime1_Validating(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles txtAdMessTime1.Validating
        If Val(txtAdMessTime1.Text) < 3 And Val(txtAdMessTime1.Text) <> 0 Then
            txtAdMessTime1.Text = "3"
        End If
    End Sub


    Private Sub txtAdMessTime2_Validating(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles txtAdMessTime2.Validating
        If Val(txtAdMessTime2.Text) < 3 And Val(txtAdMessTime2.Text) <> 0 Then
            txtAdMessTime2.Text = "3"
        End If
    End Sub

    Private Sub txtAdMessTime3_Validating(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles txtAdMessTime3.Validating
        If Val(txtAdMessTime3.Text) < 3 And Val(txtAdMessTime3.Text) <> 0 Then
            txtAdMessTime3.Text = "3"
        End If
    End Sub

    Private Sub cboxCabMess3_KeyDown(sender As Object, e As KeyEventArgs) Handles cboxCabMess3.KeyDown
        e.SuppressKeyPress = True
    End Sub


    Private Sub cboxCabMess3_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboxCabMess3.SelectedIndexChanged
        If FormCargado = True Then
            If cboxCabMess3.Text = "/playerlist" Or cboxCabMess3.Text = "/mapnext" Then
                txtAdMess3.Enabled = False
            Else
                txtAdMess3.Enabled = True
            End If
            Call WriteFileMsg()
        End If
    End Sub

    'Variables que se cargan con los valores de configuración del DedicatedServerSettings.ini
    Dim timeManhunt As Integer = 0
    Dim timeWanted As Integer = 0
    Dim timeShootout As Integer = 0
    Dim timePosse As Integer = 0
    Dim pointsManhunt As String = ""
    Dim bountyWanted As String = ""
    Dim bountyShootout As String = ""
    Dim bountyPosse As String = ""
    Dim timeRemain As Integer = 0
    Dim bountyOrPointsToShow As String = ""
    Private Sub ReadMaps()
        If My.Computer.FileSystem.FileExists(ruta & "\CoJ2\DedicatedServerSettings.ini") Then
            Dim linealeida As String = ""
            readerConfig = File.OpenText(ruta & "\CoJ2\DedicatedServerSettings.ini")
            'Map("Adobes", "deathmatch")
            Do While readerConfig.EndOfStream = False
                linealeida = readerConfig.ReadLine()
                If Mid(linealeida, 1, 4) = "Map(" Then
                    Dim Mapa As String = Mid(linealeida, 6, InStr(linealeida, ",") - 7)
                    Dim Modo As String = Mid(linealeida, InStr(linealeida, ", ") + 3, (InStr(linealeida, ")") - (InStr(linealeida, ", ") + 4)))
                    ' Self-heal a file that was previously saved with a bracketed mode
                    ' (the old writer wrote "<wanted>" instead of "wanted"): strip any stray
                    ' <> here so the mode is clean before we re-wrap it for display below.
                    Modo = Modo.Trim()
                    If Modo.StartsWith("<") AndAlso Modo.EndsWith(">") AndAlso Modo.Length >= 2 Then
                        Modo = Modo.Substring(1, Modo.Length - 2)
                    End If
                    If Mapa = "StinkingSprings2" Then Mapa = "Stinking Springs"
                    If Mapa = "PrisonBreak2" Then Mapa = "Nogales"
                    If Mapa = "Adobes" Then Mapa = "Taos Pueblo"
                    If Mapa = "Civil" Then Mapa = "Burnside's Bridge"
                    If Modo = "teamdeathmatch" Then Modo = "Posse"
                    If Modo = "deathmatch" Then Modo = "Shootout"
                    If Modo = "teamwanted" Then Modo = "Manhunt"
                    If Modo = "historical" Then Modo = "W.W.Legends"

                    dgMaps.Rows.Add()
                    dgMaps.Rows(dgMaps.Rows.Count - 1).Cells(0).Value = Mapa

                    Mapa = Mapa & "  <" & Modo & ">"
                    dgMaps.Rows(dgMaps.Rows.Count - 1).Cells(1).Value = "<" & Modo & ">"
                    'lbMapas.Items.Add(Mapa)

                End If


                Dim valorLeido As String = ""
                If InStr(linealeida, "TimeLimit(") Then
                    valorLeido = Val(Replace(Mid(linealeida, InStr(linealeida, ",") + 1, 7), ")", " "))
                    If InStr(linealeida, "deathmatch") > 0 And InStr(linealeida, "teamdeathmatch") = 0 Then
                        timeShootout = valorLeido
                    End If
                    If InStr(linealeida, "wanted") > 0 And InStr(linealeida, "teamwanted") = 0 Then
                        timeWanted = valorLeido
                    End If

                    If InStr(linealeida, "teamdeathmatch") > 0 Then timePosse = valorLeido
                    If InStr(linealeida, "teamwanted") > 0 Then timeManhunt = valorLeido
                End If


                If InStr(linealeida, "BountyLimit(") Then
                    valorLeido = (Val(Replace(Mid(linealeida, InStr(linealeida, ",") + 1, 7), ")", " "))).ToString.Trim
                    If InStr(linealeida, "deathmatch") > 0 And InStr(linealeida, "teamdeathmatch") = 0 Then
                        If valorLeido = "0" Then
                            bountyShootout = "Bounty = No Limits"
                        Else
                            bountyShootout = "Bounty = " & valorLeido
                        End If
                    End If
                    If InStr(linealeida, "wanted") > 0 And InStr(linealeida, "teamwanted") = 0 Then
                        If valorLeido = "0" Then
                            bountyWanted = "Bounty = No Limits"
                        Else
                            bountyWanted = "Bounty = " & valorLeido
                        End If
                    End If

                    If InStr(linealeida, "teamdeathmatch") > 0 Then
                        If valorLeido = "0" Then
                            bountyPosse = "Bounty = No Limits"
                        Else
                            bountyPosse = "Bounty = " & valorLeido
                        End If
                    End If
                End If

                If InStr(linealeida, "PointsLimit(") Then
                    valorLeido = (Val(Replace(Mid(linealeida, InStr(linealeida, ",") + 1, 7), ")", " "))).ToString.Trim
                    If InStr(linealeida, "teamwanted") > 0 Then
                        If valorLeido = "0" Then
                            pointsManhunt = "Points = No Limits"
                        Else
                            pointsManhunt = "Points = " & valorLeido
                        End If
                    End If
                End If



            Loop
            readerConfig.Close()
        End If
    End Sub


    '***************************************************************************************
    '***************************************************************************************
    '******************Tab Players Subs*****************************************************
    '***************************************************************************************
    '***************************************************************************************
    Dim writer As StreamWriter

    Private Sub btnBan_Click(sender As Object, e As EventArgs) Handles btnBan.Click
        Dim Player As String = ""
        For i = 0 To LbPlayers.SelectedItems.Count - 1
            Player = LbPlayers.SelectedItems.Item(i)
            Player = RTrim(LTrim(Mid(Player, InStr(Player, "-") + 1, Len(Player) - InStr(Player, "-"))))
            If FindInListbox(lbBanned, Player) = False Then
                lbBanned.Items.Add(Mid(Date.Now.ToString, 1, 10) & "   " & Player & " ")

                'ACTIVAR LA SIGUIENTE LINEA CUANDO SE QUIERA EMPEZAR A BANEAR
                SendToConsole("/kick " & Player & " Banned by Mod Coj Controller")
            End If
        Next
        Call WriteFileBanned()
    End Sub

    Private Sub btnClearBan_Click(sender As Object, e As EventArgs) Handles btnClearBan.Click
        Do While lbBanned.SelectedItems.Count > 0
            lbBanned.Items.Remove(lbBanned.SelectedItems.Item(0))
        Loop
        Call WriteFileBanned()
    End Sub

    Private Sub WriteFileBanned()
        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Config")
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\Banned.ini") Then
            My.Computer.FileSystem.DeleteFile(rutaAppData & "\Config\Banned.ini")
        End If
        writer = File.AppendText(rutaAppData & "\Config\Banned.ini")
        For i = 0 To lbBanned.Items.Count - 1
            writer.WriteLine(lbBanned.Items(i))
        Next
        writer.Close()

    End Sub

    Private Sub btnKick_Click(sender As Object, e As EventArgs) Handles btnKick.Click
        Dim Player As String = ""
        For i = 0 To LbPlayers.SelectedItems.Count - 1
            Player = (Mid(LbPlayers.SelectedItems.Item(i), 1, 7)).Trim
            If FindInListbox(lbAdmin, Player) = False Then
                SendToConsole("/kick " & Player & " " & cboxReason.Text.ToString & " (by Mods Coj Controller)")
            End If
        Next
    End Sub

    Private Sub btnKill_Click(sender As Object, e As EventArgs) Handles btnKill.Click
        Dim Player As String = ""
        For i = 0 To LbPlayers.SelectedItems.Count - 1
            Player = (Mid(LbPlayers.SelectedItems.Item(i), 1, 7)).Trim
            SendToConsole("/kill " & Player)
        Next
    End Sub

    Private Sub ReadFilePlayer()
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\Banned.ini") Then
            ' MsgBox(Application.StartupPath & "\Config\msg.ini")
            readerConfig = File.OpenText(rutaAppData & "\Config\Banned.ini")
            Do While readerConfig.EndOfStream = False
                lbBanned.Items.Add(readerConfig.ReadLine())
            Loop
            readerConfig.Close()
        End If
    End Sub


    Private Sub Button1_Click(sender As Object, e As EventArgs)
        Call writeLog()
    End Sub

    Private Sub TimerGetPlayerList_Tick(sender As Object, e As EventArgs) Handles TimerGetPlayerList.Tick
        TimerGetPlayerList.Enabled = False
        ' LbPlayers.Items.Clear()
        SendToConsole("/playerlist")
    End Sub

    Private Sub btnAddBannedTBox_Click(sender As Object, e As EventArgs) Handles btnAddBannedTBox.Click
        If FindInListbox(lbBanned, txtNewBanned.Text.Trim) = False And txtNewBanned.Text.Trim <> "" Then
            lbBanned.Items.Add(Mid(Date.Now.ToString, 1, 10) & "   " & txtNewBanned.Text.Trim & " ")
            SendToConsole("/kick " & txtNewBanned.Text.Trim)
            txtNewBanned.Text = ""
            Call WriteFileBanned()
        End If
    End Sub



    '***************************************************************************************
    '***************************************************************************************
    '******************Tab Players Admins*****************************************************
    '***************************************************************************************
    '***************************************************************************************

    'Chk para permitir o No, comandos de consola
    Private Sub chkCommandlistAd_CheckedChanged(sender As Object, e As EventArgs) Handles chkMapnext.CheckedChanged, chkMapgo.CheckedChanged, chkCommandlistAd.CheckedChanged, chkNewadmin.CheckedChanged, chkKick.CheckedChanged, chkKill.CheckedChanged, chkBan.CheckedChanged, chkUnban.CheckedChanged, chkBanlist.CheckedChanged, chkPlayerlistAd.CheckedChanged, chkServershutdown.CheckedChanged, chkRestart.CheckedChanged, chkAdminsay.CheckedChanged, chkCommandlist.CheckedChanged, chkPlayerlist.CheckedChanged, chkVotemapnext.CheckedChanged, chkVotemapgo.CheckedChanged, chkVoteBan.CheckedChanged, chkVotekick.CheckedChanged, chkMaplist.CheckedChanged, chkBalanceAd.CheckedChanged
        If FormCargado = False Then
            Exit Sub
        End If
        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Config")
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\Commands.ini") Then
            My.Computer.FileSystem.DeleteFile(rutaAppData & "\Config\Commands.ini")
        End If
        writer = File.AppendText(rutaAppData & "\Config\Commands.ini")
        'Guardamos en un fichero el estado de cada checkBox de comandos
        writer.WriteLine(chkCommandlistAd.Checked)
        writer.WriteLine(chkNewadmin.Checked)
        writer.WriteLine(chkKill.Checked)
        writer.WriteLine(chkKick.Checked)
        writer.WriteLine(chkBan.Checked)
        writer.WriteLine(chkUnban.Checked)
        writer.WriteLine(chkBanlist.Checked)
        writer.WriteLine(chkPlayerlistAd.Checked)
        writer.WriteLine(chkServershutdown.Checked)
        writer.WriteLine(chkRestart.Checked)
        writer.WriteLine(chkMapnext.Checked)
        writer.WriteLine(chkAdminsay.Checked)
        writer.WriteLine(chkMaplistAd.Checked)
        writer.WriteLine(chkBalanceAd.Checked)
        writer.WriteLine(chkAddWord.Checked)
        writer.WriteLine(chkCommandlist.Checked)
        writer.WriteLine(chkPlayerlist.Checked)
        writer.WriteLine(chkVotemapnext.Checked)
        writer.WriteLine(chkMaplist.Checked)
        writer.WriteLine(chkVotekick.Checked)
        writer.WriteLine(chkVoteBan.Checked)
        writer.WriteLine(chkMapgo.Checked)
        writer.WriteLine(chkVotemapgo.Checked)
        writer.Close()

        Call CreaListaComandosActivos()
    End Sub

    'Lectura fichero commands.ini
    Private Sub ReadFileCommand()
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\Commands.ini") Then
            readerConfig = File.OpenText(rutaAppData & "\Config\Commands.ini")

            Try
                chkCommandlistAd.Checked = readerConfig.ReadLine()
                chkNewadmin.Checked = readerConfig.ReadLine()
                chkKill.Checked = readerConfig.ReadLine()
                chkKick.Checked = readerConfig.ReadLine()
                chkBan.Checked = readerConfig.ReadLine()
                chkUnban.Checked = readerConfig.ReadLine()
                chkBanlist.Checked = readerConfig.ReadLine()
                chkPlayerlistAd.Checked = readerConfig.ReadLine()
                chkServershutdown.Checked = readerConfig.ReadLine()
                chkRestart.Checked = readerConfig.ReadLine()
                chkMapnext.Checked = readerConfig.ReadLine()
                chkAdminsay.Checked = readerConfig.ReadLine()
                chkMaplistAd.Checked = readerConfig.ReadLine()
                chkBalanceAd.Checked = readerConfig.ReadLine()
                chkAddWord.Checked = readerConfig.ReadLine()
                chkCommandlist.Checked = readerConfig.ReadLine()
                chkPlayerlist.Checked = readerConfig.ReadLine()
                chkVotemapnext.Checked = readerConfig.ReadLine()
                chkMaplist.Checked = readerConfig.ReadLine()
                chkVotekick.Checked = readerConfig.ReadLine()
                chkVoteBan.Checked = readerConfig.ReadLine()
                'Nuevo: !mapgo. Se lee al final para no romper los Commands.ini existentes;
                'si la línea no existe (config antigua) se conserva el valor por defecto.
                Dim mapgoLine As String = readerConfig.ReadLine()
                If mapgoLine IsNot Nothing Then chkMapgo.Checked = CBool(mapgoLine)
                'Nuevo: !votemapgo. También al final por la misma razón.
                Dim votemapgoLine As String = readerConfig.ReadLine()
                If votemapgoLine IsNot Nothing Then chkVotemapgo.Checked = CBool(votemapgoLine)
            Catch ex As Exception

            End Try
            readerConfig.Close()
        End If
    End Sub

    Private Sub CreaListaComandosActivos()
        commandActivAdLin1 = ""
        commandActivAdLin2 = ""
        commandActivAdLin3 = ""
        commandActivGeneral = ""
        ' !commandlist is not listed in its own admin output (whoever reads it just typed it),
        ' which keeps the full admin list inside CoJ chat's 3-line limit.
        If chkNewadmin.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!newadmin - "
        End If
        If chkKill.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!kill - "
        End If
        If chkKick.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!kick - "
        End If
        If chkBan.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!ban - "
        End If
        If chkUnban.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!unban - "
        End If
        If chkBanlist.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!banlist - "
        End If
        If chkPlayerlistAd.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!playerlist - "
        End If
        If chkServershutdown.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!servershutdown - "
        End If
        If chkRestart.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!restart - "
        End If
        If chkMapnext.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!mapnext - "
        End If
        If chkMapgo.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!mapgo - "
        End If
        If chkAdminsay.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!adminsay - "
        End If
        If chkMaplistAd.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!maplist - "
        End If
        If chkCommandlist.Checked = True Then
            commandActivGeneral = commandActivGeneral & "!commandlist  -  "
        End If
        If chkPlayerlist.Checked = True Then
            commandActivGeneral = commandActivGeneral & "!playerlist  -  "
        End If
        If chkVotemapnext.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!votemapnext - "
            commandActivGeneral = commandActivGeneral & "!votemapnext  -  "
        End If
        '!votemapgo tiene su propio toggle (Panel2, comandos para todos los jugadores).
        If chkVotemapgo.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!votemapgo - "
            commandActivGeneral = commandActivGeneral & "!votemapgo <map> <mode>  -  "
        End If
        If chkMaplist.Checked = True Then
            commandActivGeneral = commandActivGeneral & "!maplist  -  "
        End If
        If chkVotekick.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!votekick - "
            commandActivGeneral = commandActivGeneral & "!votekick  -  "
        End If
        If chkBalanceAd.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!balance - "
        End If
        If chkAddWord.Checked = True Then
            commandActivAdLin1 = commandActivAdLin1 & "!addword - "
        End If
        ' Always-on admin commands (no toggle). CoJ chat shows at most 3 lines, so the
        ' admin list carries names only - no "<arg>" hints - and the four music
        ' commands are folded into one compact entry.
        commandActivAdLin1 = commandActivAdLin1 & "!play/stop/skip/shufflemusic - !deploybot - !removebot - !protect - "

        Dim letra As String = ""
        Dim positionLetra As Integer = 100
        Do While Len(commandActivAdLin1) > 100 Or Len(commandActivAdLin2) > 100
            Do While letra <> "-"
                letra = Mid(commandActivAdLin1, positionLetra, 1)
                positionLetra = positionLetra - 1
            Loop
            If commandActivAdLin1.Length <= (positionLetra + 3) Then
                Exit Do
            Else
                commandActivAdLin2 = Trim(Mid(commandActivAdLin1, positionLetra + 3, Len(commandActivAdLin1) - (positionLetra + 3)))
                commandActivAdLin1 = Trim(Mid(commandActivAdLin1, 1, positionLetra - 1))
            End If

            letra = ""
            positionLetra = 100
            Do While letra <> "-"
                letra = Mid(commandActivAdLin2, positionLetra, 1)
                positionLetra = positionLetra - 1
            Loop

            If commandActivAdLin2.Length <= (positionLetra + 3) Then
                Exit Do
            Else
                commandActivAdLin3 = Trim(Mid(commandActivAdLin2, positionLetra + 3, Len(commandActivAdLin2) - (positionLetra + 3)))
                commandActivAdLin2 = Trim(Mid(commandActivAdLin2, 1, positionLetra - 1))
            End If
        Loop


    End Sub


    Private Sub btnDeleAdmin_Click(sender As Object, e As EventArgs) Handles btnDeleAdmin.Click
        Do While lbAdmin.SelectedItems.Count > 0
            SendToConsole("/adminsay " & lbAdmin.SelectedItems.Item(0) & " has been removed from Admins List.")
            lbAdmin.Items.Remove(lbAdmin.SelectedItems.Item(0))
        Loop
        Call WriteFileAdmins()
    End Sub

    Private Sub WriteFileAdmins()
        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Config")
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\Admins.ini") Then
            My.Computer.FileSystem.DeleteFile(rutaAppData & "\Config\Admins.ini")
        End If
        writer = File.AppendText(rutaAppData & "\Config\Admins.ini")
        For i = 0 To lbAdmin.Items.Count - 1
            writer.WriteLine(lbAdmin.Items(i))
        Next
        writer.Close()

    End Sub

    Private Sub btnAddAdmin_Click(sender As Object, e As EventArgs) Handles btnAddAdmin.Click
        Dim Player As String = ""
        For i = 0 To lbPlayers2.SelectedItems.Count - 1
            Player = lbPlayers2.SelectedItems.Item(i)
            Player = RTrim(LTrim(Mid(Player, InStr(Player, "-") + 1, Len(Player) - InStr(Player, "-"))))
            If lbAdmin.Items.Contains(Player) = False Then
                lbAdmin.Items.Add(Player)
                SendToConsole("/adminsay " & Player & " has been added to Admins List.")
            End If
        Next
        Call WriteFileAdmins()
    End Sub

    Private Sub lbPlayers2_DoubleClick(sender As Object, e As EventArgs) Handles lbPlayers2.DoubleClick
        If lbPlayers2.SelectedItems.Count > 0 Then
            Dim Player As String = lbPlayers2.SelectedItems.Item(0)
            Player = RTrim(LTrim(Mid(Player, InStr(Player, "-") + 1, Len(Player) - InStr(Player, "-"))))
            lbAdmin.Items.Add(Player)
            SendToConsole("/adminsay " & Player & " has been added to Admins List.")
            Call WriteFileAdmins()
        End If
    End Sub

    Private Sub lbAdmin_DoubleClick(sender As Object, e As EventArgs) Handles lbAdmin.DoubleClick
        If lbAdmin.SelectedItems.Count > 0 Then
            SendToConsole("/adminsay " & lbAdmin.SelectedItems.Item(0) & " has been removed from Admins List.")
            lbAdmin.Items.Remove(lbAdmin.SelectedItems.Item(0))
            Call WriteFileAdmins()
        End If
    End Sub

    Private Sub ReadFileAdmin()
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\Admins.ini") Then
            ' MsgBox(Application.StartupPath & "\Config\msg.ini")
            readerConfig = File.OpenText(rutaAppData & "\Config\Admins.ini")
            Do While readerConfig.EndOfStream = False
                lbAdmin.Items.Add(readerConfig.ReadLine())
            Loop
            readerConfig.Close()
        End If
    End Sub

    Private Sub btnAddAdminTbox_Click(sender As Object, e As EventArgs) Handles btnAddAdminTbox.Click
        If Not lbAdmin.Items.Contains(txtNewAdmin.Text.Trim) And txtNewAdmin.Text.Trim <> "" Then
            lbAdmin.Items.Add(txtNewAdmin.Text.Trim)
            txtNewAdmin.Text = ""
            Call WriteFileAdmins()
        End If
    End Sub


    '***************************************************************************************
    '***************************************************************************************
    '****************************Tab Old Console Logs***************************************
    '***************************************************************************************
    '***************************************************************************************

    Private Sub ReadFileLogs(FileLog As String)
        If My.Computer.FileSystem.FileExists(Application.StartupPath & "\Logs\" & FileLog) Then
            Dim linealeida As String = ""
            Dim FirstLine As String = ""
            readerConfig = File.OpenText(Application.StartupPath & "\Logs\" & FileLog)
            lbOldChats.Items.Clear()
            Do While readerConfig.EndOfStream = False
                linealeida = readerConfig.ReadLine()
                If FirstLine.Trim = "" Then FirstLine = Trim(Mid(linealeida, 1, 8))
                lbOldChats.Items.Add(linealeida)
            Loop
            readerConfig.Close()
            FirstLine = Mid(FirstLine, 1, Len(FirstLine) - 2)
            Dim dateChat As String = lbLogFilesList.SelectedItems.Item(0).ToString
            dateChat = Mid(dateChat, 9, 2) & "/" & Mid(dateChat, 6, 2) & "/" & Mid(dateChat, 1, 4) & " from " & FirstLine & " to " & Mid(dateChat, 12, 2) & ":" & Mid(dateChat, 15, 2)
            Label12.Text = "Chat from day  " & dateChat
        End If
    End Sub

    Private Sub lbLogFilesList_DoubleClick(sender As Object, e As EventArgs) Handles lbLogFilesList.DoubleClick, btnLoadLog.Click
        If lbLogFilesList.SelectedItems.Count > 0 Then
            Call ReadFileLogs(lbLogFilesList.SelectedItems.Item(0).ToString)
        End If
    End Sub


    'Busqueda en el list box
    Private Sub btnBuscaEnOldChat_Click() Handles btnBuscaEnOldChat.Click
        Static startIndex As Integer
        Static index As Integer

        ' Obtenemos la lista de elementos coincidentes
        Dim items As List(Of String) = ObtenerLista(txtFindInChat.Text)

        If ((items Is Nothing) OrElse (items.Count = 0)) Then
            ' No hay elementos coincidentes.
            lbOldChats.SelectedIndex = ListBox.NoMatches
            startIndex = 0
            index = 0
            Return
        End If

        Try
            startIndex = lbOldChats.FindString(items(index), startIndex)
        Catch ex As Exception

        End Try

        lbOldChats.SelectedIndex = startIndex

        index += 1
        If (index >= items.Count) Then index = 0
    End Sub
    'Funcion para busqueda en list box
    Private Function ObtenerLista(valor As String) As List(Of String)
        Dim query = From item As String In lbOldChats.Items.OfType(Of String)() _
                    Where item.ToUpperInvariant().Contains(valor.ToUpperInvariant()) _
                    Select item
        Return query.ToList()
    End Function


    Private Sub txtFindInChat_TextChanged(sender As Object, e As EventArgs) Handles txtFindInChat.TextChanged
        If txtFindInChat.Text.Length > 0 Then
            lblFindInChat.Visible = False
        Else
            lblFindInChat.Visible = True
        End If
    End Sub

    Private Sub lblFindInChat_Click(sender As Object, e As EventArgs) Handles lblFindInChat.Click
        txtFindInChat.Focus()
    End Sub

    Private Sub LoadListLogs()
        'Cargamos al ListBox todos los ficheros Logs
        Dim rutaLogs As String = Application.StartupPath & "\Logs"
        Dim Fich As String = ""
        lbLogFilesList.Items.AddRange(Directory.GetFiles(Application.StartupPath & "\Logs"))

        'Array donde pasaremos todos los ficheros para ordenarlos descendente y devolverlos al listbox
        Dim arrayList(lbLogFilesList.Items.Count - 1)
        For i = 0 To lbLogFilesList.Items.Count - 1
            Fich = lbLogFilesList.Items(i)
            arrayList(i) = Mid(Fich, InStr(Fich, "Logs\") + 5, Len(Fich) - InStr(Fich, "Logs\") + 5)
        Next

        'Ordenamos el array en sentido descendente, (ficheros mas nuevos arriba)
        Array.Sort(arrayList)
        Array.Reverse(arrayList)

        'vaciamos el listbox para llenarlo ordenado
        lbLogFilesList.Items.Clear()
        For i = 0 To arrayList.Count - 1
            lbLogFilesList.Items.Add(arrayList(i))
        Next

    End Sub

    Private Sub lbLogFilesList_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lbLogFilesList.SelectedIndexChanged
        lblQuantityLogs.Text = (lbLogFilesList.SelectedIndex + 1).ToString.Trim & "/" & (lbLogFilesList.Items.Count).ToString.Trim
    End Sub


    'Lectura fichero commands.ini
    Private Sub ReadAndWriteMapsInDedicatedServerSetting()
        If My.Computer.FileSystem.FileExists(ruta & "\CoJ2\DedicatedServerSettings.ini") Then

            '*****************Leemos el fichero DedicatedServerSetting para modificar orden de mapas*****************
            readerConfig = File.OpenText(ruta & "\CoJ2\DedicatedServerSettings.ini")

            Dim NumLineas As Integer = 0
            Do While readerConfig.EndOfStream = False
                readerConfig.ReadLine()
                NumLineas = NumLineas + 1
            Loop
            readerConfig.Close()

            readerConfig = File.OpenText(ruta & "\CoJ2\DedicatedServerSettings.ini")
            Dim arrLineaLeida(NumLineas) As String
            Dim LineaDgridMap As Integer = 0
            Dim mapa As String = ""
            Dim modo As String = ""
            For i = 0 To NumLineas
                arrLineaLeida(i) = readerConfig.ReadLine()
                If Mid(arrLineaLeida(i), 1, 4) = "Map(" Then
                    Try
                        If LineaDgridMap < dgMaps.Rows.Count Then
                            mapa = dgMaps.Rows(LineaDgridMap).Cells(0).Value.ToString.Trim
                            If mapa = "Taos Pueblo" Then mapa = "Adobes"
                            If mapa = "Burnside's Bridge" Then mapa = "Civil"
                            If mapa = "Nogales" Then mapa = "PrisonBreak2"
                            If mapa = "Stinking Springs" Then mapa = "StinkingSprings2"

                            modo = dgMaps.Rows(LineaDgridMap).Cells(1).Value.ToString.Trim
                            ' The grid stores the mode wrapped in <> for display; strip the
                            ' brackets before writing so we never save "<wanted>" to the file.
                            If modo.StartsWith("<") AndAlso modo.EndsWith(">") AndAlso modo.Length >= 2 Then
                                modo = modo.Substring(1, modo.Length - 2)
                            End If
                            ' Reverse the display renames (unbracketed, case-insensitive). Any
                            ' other mode - including plain "wanted" - is already the file value.
                            Select Case LCase(modo)
                                Case "manhunt" : modo = "teamwanted"
                                Case "posse" : modo = "teamdeathmatch"
                                Case "shootout" : modo = "deathmatch"
                                Case "w.w.legends" : modo = "historical"
                            End Select

                            'Map("Adobes", "deathmatch")

                            arrLineaLeida(i) = "Map(""" & mapa & """, """ & modo & """)"
                            LineaDgridMap = LineaDgridMap + 1
                        Else
                            ' More Map() lines in the file than rows in the rotation grid:
                            ' drop the stale extra instead of leaving the old line behind.
                            arrLineaLeida(i) = ""
                        End If
                    Catch ex As Exception

                    End Try

                End If
            Next
            readerConfig.Close()

            '**************Volvemos a escribir DedicatedServerSetting con otro orden de mapas***************
            writer = File.CreateText(ruta & "\CoJ2\DedicatedServerSettings.ini")

            For i = 0 To NumLineas
                writer.WriteLine(arrLineaLeida(i))
            Next
            writer.Close()
        End If
    End Sub

    '***************************************************************************************
    '***************************************************************************************
    '********************************** Tab Mapas  *****************************************
    '***************************************************************************************
    '***************************************************************************************

    Private Sub chkRememberMaps_CheckedChanged(sender As Object, e As EventArgs) Handles chkRememberMaps.CheckedChanged
        If FormCargado = False Then
            Exit Sub
        End If

        'Escribimos el fichero de configuración para recordar el estado del checkbox al iniciar
        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Config")
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\Maps.ini") Then
            My.Computer.FileSystem.DeleteFile(rutaAppData & "\Config\Maps.ini")
        End If
        writer = File.AppendText(rutaAppData & "\Config\Maps.ini")
        'Guardamos en un fichero el estado de cada checkBox de comandos
        writer.WriteLine(chkRememberMaps.Checked)
        writer.Close()
    End Sub

    'Lectura fichero commands.ini
    Private Sub ReadCheckMaps()
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\Maps.ini") Then
            readerConfig = File.OpenText(rutaAppData & "\Config\Maps.ini")

            Try
                chkRememberMaps.Checked = readerConfig.ReadLine()
            Catch ex As Exception

            End Try
            readerConfig.Close()
        End If
    End Sub


    '***************************************************************************************
    '***************************************************************************************
    '********************************** Tab Rules  *****************************************
    '***************************************************************************************
    '***************************************************************************************
    Private Sub btnAddWord_Click() Handles btnAddWord.Click
        If txtAddWords.Text.Trim <> "" And lbWords.Items.Contains(txtAddWords.Text.Trim) = False Then
            AddItemsToListBoxOrdered(lbWords, txtAddWords.Text.Trim)
            Call WriteFileTabRules()
        End If
        txtAddWords.Text = ""
    End Sub

    Private Sub chkCleanPlayersWarned_CheckedChanged(sender As Object, e As EventArgs) Handles chkCleanPlayersWarned.CheckedChanged
        If FormCargado = True And TabPanel1.SelectedTab.Name = TabRules.Name Then Call WriteFileTabRules()
    End Sub

    Private Sub rbWordsKill_CheckedChanged(sender As Object, e As EventArgs) Handles rbWordsKill.CheckedChanged
        cboxWordKillEachTimes.Enabled = True
        cboxWordBanEachTimes.Enabled = False
        cboxWordKickEachTimes.Enabled = False
        If FormCargado = True And TabPanel1.SelectedTab.Name = TabRules.Name Then Call WriteFileTabRules()
    End Sub

    Private Sub rbWordsKick_CheckedChanged(sender As Object, e As EventArgs) Handles rbWordsKick.CheckedChanged
        cboxWordKillEachTimes.Enabled = False
        cboxWordBanEachTimes.Enabled = False
        cboxWordKickEachTimes.Enabled = True
        If FormCargado = True And TabPanel1.SelectedTab.Name = TabRules.Name Then Call WriteFileTabRules()
    End Sub

    Private Sub rbWordsBan_CheckedChanged(sender As Object, e As EventArgs) Handles rbWordsBan.CheckedChanged
        cboxWordKillEachTimes.Enabled = False
        cboxWordBanEachTimes.Enabled = True
        cboxWordKickEachTimes.Enabled = False
        If FormCargado = True And TabPanel1.SelectedTab.Name = TabRules.Name Then Call WriteFileTabRules()
    End Sub

    Private Sub rbWordsSendWarning_CheckedChanged(sender As Object, e As EventArgs) Handles rbWordsSendWarning.CheckedChanged
        cboxWordKillEachTimes.Enabled = False
        cboxWordBanEachTimes.Enabled = False
        cboxWordKickEachTimes.Enabled = False
        If FormCargado = True And TabPanel1.SelectedTab.Name = TabRules.Name Then Call WriteFileTabRules()
    End Sub

    Private Sub btnRemoveWords_Click(sender As Object, e As EventArgs) Handles btnRemoveWords.Click
        Do While lbWords.SelectedItems.Count > 0
            lbWords.Items.Remove(lbWords.SelectedItems.Item(0))
        Loop
        Call WriteFileTabRules()
    End Sub


    Private Sub txtWarningWords_Validated(sender As Object, e As EventArgs) Handles txtWarningWords.Validated
        Call WriteFileTabRules()
    End Sub

    Private Sub cboxWordKillEachTimes_TextChanged(sender As Object, e As EventArgs) Handles cboxWordKillEachTimes.TextChanged, cboxWordKickEachTimes.TextChanged, cboxWordBanEachTimes.TextChanged
        If FormCargado = True And TabPanel1.SelectedTab.Name = TabRules.Name Then Call WriteFileTabRules()
    End Sub


    'Escritura del fichero rules.ini
    Private Sub WriteFileTabRules()
        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Config")
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\rules.ini") Then
            My.Computer.FileSystem.DeleteFile(rutaAppData & "\Config\rules.ini")
        End If
        Try
            writer = File.AppendText(rutaAppData & "\Config\rules.ini")
            writer.WriteLine(chkCleanPlayersWarned.Checked)
            writer.WriteLine(rbWordsSendWarning.Checked)
            writer.WriteLine(rbWordsKill.Checked)
            writer.WriteLine(rbWordsKick.Checked)
            writer.WriteLine(rbWordsBan.Checked)
            writer.WriteLine(cboxWordKillEachTimes.Text)
            writer.WriteLine(cboxWordKickEachTimes.Text)
            writer.WriteLine(cboxWordBanEachTimes.Text)
            writer.WriteLine(txtWarningWords.Text)
            writer.WriteLine("-Words-")
            For i = 0 To lbWords.Items.Count - 1
                writer.WriteLine(lbWords.Items(i))
            Next
        Catch ex As Exception

        End Try

        writer.Close()
    End Sub

    'Lectura fichero rules.ini
    Private Sub ReadFileTabRules()
        If My.Computer.FileSystem.FileExists(rutaAppData & "\Config\rules.ini") Then
            readerConfig = File.OpenText(rutaAppData & "\Config\rules.ini")

            Try
                'Leemos la configuración de las reglas
                chkCleanPlayersWarned.Checked = readerConfig.ReadLine()
                rbWordsSendWarning.Checked = readerConfig.ReadLine()
                rbWordsKill.Checked = readerConfig.ReadLine()
                rbWordsKick.Checked = readerConfig.ReadLine()
                rbWordsBan.Checked = readerConfig.ReadLine()
                cboxWordKillEachTimes.Text = readerConfig.ReadLine()
                cboxWordKickEachTimes.Text = readerConfig.ReadLine()
                cboxWordBanEachTimes.Text = readerConfig.ReadLine()
                txtWarningWords.Text = readerConfig.ReadLine()
                readerConfig.ReadLine()

                'A partir de la siguiente línea leemos todas las palabras prohibidas de la lista
                Do While readerConfig.EndOfStream = False
                    lbWords.Items.Add(readerConfig.ReadLine())
                Loop
            Catch ex As Exception

            End Try
            readerConfig.Close()
        End If
    End Sub

    Private Sub CheckRules(Player As String, Warnings As Integer)
        If rbWordsSendWarning.Checked = True Then
            SendToConsole("/adminsay " & txtWarningWords.Text & " (" & Player & ").")
        ElseIf rbWordsKill.Checked = True Then
            If Val(cboxWordKillEachTimes.Text) <= Warnings Then
                SendToConsole("/kill " & Player)
                SendToConsole("/adminsay " & Player & " was killed by Mods Coj2 Controller. Limit reached warnings. (" & Warnings & "/" & cboxWordKillEachTimes.Text.Trim & ").")
            Else
                SendToConsole("/adminsay " & txtWarningWords.Text & " " & Player & " will be killed from " & cboxWordKillEachTimes.Text.Trim & " warnings" & " (" & Player & ").")
            End If

        ElseIf rbWordsKick.Checked = True Then
            If Val(cboxWordKickEachTimes.Text) <= Warnings And FindInListbox(lbBanned, Player) = False Then
                SendToConsole("/kick " & Player & " Limit reached warnings. (" & Warnings & "/" & cboxWordKickEachTimes.Text.Trim & ")")
                SendToConsole("/adminsay " & Player & " was kicked by Mods Coj2 Controller. Limit reached warnings. (" & Warnings & "/" & cboxWordKickEachTimes.Text.Trim & ").")
            Else
                SendToConsole("/adminsay " & txtWarningWords.Text & " " & Player & " will be kicked out from " & cboxWordKickEachTimes.Text.Trim & " warnings" & " (" & Player & ").")
            End If

        ElseIf rbWordsBan.Checked = True Then
            If Val(cboxWordBanEachTimes.Text) <= Warnings And FindInListbox(lbBanned, Player) = False Then
                '                AddItemsToListBoxOrdered(lbBanned, Player)
                lbBanned.Items.Add(Mid(Date.Now.ToString, 1, 10) & "   " & Player & " ")
                SendToConsole("/kick " & Player & " Limit reached warnings. (" & Warnings & "/" & cboxWordBanEachTimes.Text.Trim & ")")
                SendToConsole("/adminsay " & Player & " was banned by Mods Coj2 Controller. Limit reached warnings. (" & Warnings & "/" & cboxWordBanEachTimes.Text.Trim & ").")
            Else
                SendToConsole("/adminsay " & txtWarningWords.Text & " " & Player & " will be banned from " & cboxWordBanEachTimes.Text.Trim & " warnings" & " (" & Player & ").")
            End If

        End If
    End Sub


    '***************************************************************************************
    '***************************************************************************************
    '**************************** Control de teclas  ***************************************
    '***************************************************************************************
    '***************************************************************************************


    Private Sub frmConsola_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        If e.KeyCode = Keys.F12 Then
            windowExist = Module1.windowSender.FindWindow()
            'MsgBox(vHwnd.ToString)
        End If

        If e.KeyCode = Keys.F1 Then
            Call btnHelp_Click()
        End If

        If e.KeyCode = Keys.F3 Then
            If TabPanel1.SelectedTab.Name = TabLogs.Name Then
                Call btnBuscaEnOldChat_Click()
            End If
        End If

        'TECLAS TAB CHAT
        If TabPanel1.SelectedTab.Name = TabChat.Name Then
            If e.KeyCode = Keys.Enter Then
                e.Handled = True
                btnEnviar_Click()
            End If
        End If

        'TECLAS TAB RULES
        If TabPanel1.SelectedTab.Name = TabRules.Name Then
            If e.KeyCode = Keys.Enter Then
                e.Handled = True
                btnAddWord_Click()
            End If
        End If

    End Sub


    Private Sub btnDeleteLog_Click(sender As Object, e As EventArgs) Handles btnDeleteLog.Click
        Do While lbLogFilesList.SelectedItems.Count > 0
            If My.Computer.FileSystem.FileExists(Application.StartupPath & "\Logs\" & lbLogFilesList.SelectedItems.Item(0).trim) Then
                My.Computer.FileSystem.DeleteFile(Application.StartupPath & "\Logs\" & lbLogFilesList.SelectedItems.Item(0).trim)
            End If
            lbLogFilesList.Items.Remove(lbLogFilesList.SelectedItems.Item(0))
        Loop
    End Sub

    Private Sub TimerGetMapList_Tick(sender As Object, e As EventArgs) Handles TimerGetMapList.Tick
        TimerGetMapList.Enabled = False
    End Sub


    Private Sub chkBalance2_CheckedChanged(sender As Object, e As EventArgs) Handles chkBalance2.CheckedChanged
        If chkBalance2.Checked = True And chkBalance.Checked = False Then
            chkBalance.Checked = True
            Exit Sub
        ElseIf chkBalance2.Checked = False And chkBalance.Checked = True Then
            chkBalance.Checked = False
            Exit Sub
        End If
        If chkBalance2.Checked = True And LoadingMap = False Then
            SendToConsole("/adminsay Activated automatic team BALANCE in the Server. Please, BOTH teams have to be BALANCED NOW.")
            SendToConsole("/adminsay Last players who joined to the larger team will be 'killed' by the server  every 5 seconds.")
            If Val(Replace(((Val(lblTeamBlue.Text) - Val(lblTeamRed.Text)).ToString), "-", " ")) >= 2 Then changedTeam = True
        ElseIf chkBalance2.Checked = False And LoadingMap = False Then
            SendToConsole("/adminsay Deactivated automatic balance team.")
            TimerKillBalance.Enabled = False
        End If
    End Sub

    Private Sub chkBalance_CheckedChanged(sender As Object, e As EventArgs) Handles chkBalance.CheckedChanged
        If chkBalance.Checked = True And chkBalance2.Checked = False Then
            chkBalance2.Checked = True
            Exit Sub
        ElseIf chkBalance.Checked = False And chkBalance2.Checked = True Then
            chkBalance2.Checked = False
            Exit Sub
        End If

        If chkBalance.Checked = True And LoadingMap = False Then
            SendToConsole("/adminsay Activated automatic team BALANCE in the Server. Please, BOTH teams have to be BALANCED NOW.")
            SendToConsole("/adminsay Last players who joined to the larger team will be 'killed' by the server  every 5 seconds.")
            If Val(Replace(((Val(lblTeamBlue.Text) - Val(lblTeamRed.Text)).ToString), "-", " ")) >= 2 Then changedTeam = True
        ElseIf chkBalance.Checked = False And LoadingMap = False Then
            SendToConsole("/adminsay Deactivated automatic balance team.")
            TimerKillBalance.Enabled = False
        End If
    End Sub


    'Private Sub lblTeamRed_TextChanged(sender As Object, e As EventArgs) Handles lblTeamRed.TextChanged, lblTeamBlue.TextChanged
    '    ' MsgBox(((Val(lblTeamBlue.Text) - Val(lblTeamRed.Text)).ToString))
    '    Dim variableprueba As Integer = Val(Replace(((Val(lblTeamBlue.Text) - Val(lblTeamRed.Text)).ToString), "-", " "))

    'End Sub
    Dim CiclosBalance As Integer = 0
    Private Sub TimerKillBalance_Tick(sender As Object, e As EventArgs) Handles TimerKillBalance.Tick
        If LoadingMap = True Then
            Exit Sub
        End If
        CiclosBalance = CiclosBalance + 1
        If CiclosBalance >= 5 Then
            If lbBlueTeam.Items.Count > lbRedTeam.Items.Count Then ' MÁS EN EL AZUL
                SendToConsole("/kill " & lbBlueTeam.Items(lbBlueTeam.Items.Count - 1))
                SendToConsole("/adminsay Killing will stop when teams are balanced (" & lbBlueTeam.Items(lbBlueTeam.Items.Count - 1) & ").")
            ElseIf lbBlueTeam.Items.Count < lbRedTeam.Items.Count Then ' MÁS EN EL ROJO
                SendToConsole("/kill " & lbRedTeam.Items(lbRedTeam.Items.Count - 1))
                SendToConsole("/adminsay Killing will stop when teams are balanced (" & lbRedTeam.Items(lbRedTeam.Items.Count - 1) & ").")
            End If
        ElseIf CiclosBalance = 3 Then
            If lbBlueTeam.Items.Count > lbRedTeam.Items.Count Then ' MÁS EN EL AZUL
                SendToConsole("/adminsay Starting to kill in 10 seconds if teams keep unbalanced (" & lbBlueTeam.Items(lbBlueTeam.Items.Count - 1) & ").")
            ElseIf lbBlueTeam.Items.Count < lbRedTeam.Items.Count Then ' MÁS EN EL ROJO
                SendToConsole("/adminsay Starting to kill in 10 seconds if teams keep unbalanced (" & lbRedTeam.Items(lbRedTeam.Items.Count - 1) & ").")
            End If
        End If


    End Sub


    Private Sub btnKickRed_Click(sender As Object, e As EventArgs) Handles btnKickRed.Click
        Dim Player As String = ""
        For i = 0 To lbRedTeam.SelectedItems.Count - 1
            Player = lbRedTeam.SelectedItems.Item(i).Trim
            SendToConsole("/kick " & Player & " " & cboxReasonRed.Text.ToString & " (by Mods Coj Controller)")
        Next
    End Sub

    Private Sub btnKickBlue_Click(sender As Object, e As EventArgs) Handles btnKickBlue.Click
        Dim Player As String = ""
        For i = 0 To lbBlueTeam.SelectedItems.Count - 1
            Player = lbBlueTeam.SelectedItems.Item(i).Trim
            SendToConsole("/kick " & Player & " " & cboxReasonBlue.Text.ToString & " (by Mods Coj Controller)")
        Next
    End Sub

    Private Sub btnKillRed_Click(sender As Object, e As EventArgs) Handles btnKillRed.Click
        Dim Player As String = ""
        For i = 0 To lbRedTeam.SelectedItems.Count - 1
            Player = lbRedTeam.SelectedItems.Item(i).Trim
            SendToConsole("/kill " & Player)
        Next
    End Sub

    Private Sub btnKillBlue_Click(sender As Object, e As EventArgs) Handles btnKillBlue.Click
        Dim Player As String = ""
        For i = 0 To lbBlueTeam.SelectedItems.Count - 1
            Player = lbBlueTeam.SelectedItems.Item(i).Trim
            SendToConsole("/kill " & Player)
        Next
    End Sub

    Private Function FindInListbox(lb As ListBox, findWord As String)
        findWord = findWord & " "
        For i = 0 To lb.Items.Count - 1
            If InStr(lb.Items(i), findWord) > 0 Then
                Return True
            End If
        Next
        Return False
    End Function

    ' Resolves a !kick/!ban argument to a player NAME. If the argument is a numeric id
    ' that matches a currently-connected player in LbPlayers ("<id>    -  <name>"), the
    ' current name is returned; otherwise the argument is returned unchanged (already a
    ' name, or an id with no connected player). Used so !ban stores a name - the banlist
    ' re-kick on rejoin matches by name, since server ids are per-session.
    Private Function ResolvePlayerName(ByVal arg As String) As String
        arg = Trim(arg)
        Dim id As Integer
        If arg <> "" AndAlso Integer.TryParse(arg, id) Then
            For i As Integer = 0 To LbPlayers.Items.Count - 1
                Dim item As String = LbPlayers.Items(i).ToString()
                Dim dashPos As Integer = InStr(item, "-")
                If dashPos > 0 Then
                    If Trim(Mid(item, 1, dashPos - 1)) = arg Then Return Trim(Mid(item, dashPos + 1))
                End If
            Next
        End If
        Return arg
    End Function

    ' True when cmd appears in the line with nothing after it (e.g. "<admin> !kick"), i.e.
    ' the command was typed without its arguments. Whole-token: "!ban" in "!banlist" does
    ' not count, because "list" follows it.
    Private Function BareCommand(ByVal line As String, ByVal cmd As String) As Boolean
        Dim p As Integer = InStr(line, cmd)
        If p = 0 Then Return False
        Return Trim(Mid(line, p + Len(cmd))) = ""
    End Function

    ' Reverse of ResolvePlayerName: given a player NAME (as scraped from chat), returns the
    ' unique player id from LbPlayers ("   <id>    -  <name> "), or "" if the name is not in
    ' the current roster. Used to dedup votes by id instead of by name, so a /nick rename
    ' can't buy extra votes. Names are not guaranteed unique, so on a name collision this
    ' returns the FIRST matching id - an inherent limit of chat lines (they carry no id), but
    ' still strictly better than name-dedup, which a single renamer could beat outright.
    Private Function ResolvePlayerId(ByVal name As String) As String
        name = Trim(name)
        If name = "" Then Return ""
        For i As Integer = 0 To LbPlayers.Items.Count - 1
            Dim item As String = LbPlayers.Items(i).ToString()
            Dim dashPos As Integer = InStr(item, "-")
            If dashPos > 0 Then
                If Trim(Mid(item, dashPos + 1)) = name Then Return Trim(Mid(item, 1, dashPos - 1))
            End If
        Next
        Return ""
    End Function

    ' Reads whitelist.txt (one name per line, '#' starts a comment line, blanks ignored).
    ' Returns Nothing if the file is missing/unreadable. Case-SENSITIVE exact match, so a
    ' cheater can't slip through with a different capitalisation of a whitelisted name.
    Private Function LoadWhitelist() As HashSet(Of String)
        Try
            If Not File.Exists(whitelistPath) Then Return Nothing
            Dim wl As New HashSet(Of String)(StringComparer.Ordinal)
            For Each ln As String In File.ReadAllLines(whitelistPath)
                Dim nm As String = ln.Trim()
                If nm <> "" AndAlso Not nm.StartsWith("#") Then wl.Add(nm)
            Next
            Return wl
        Catch
            Return Nothing
        End Try
    End Function

    ' Kicks playerName unless it is whitelisted or an admin. A missing/empty whitelist
    ' never kicks (fail-open), so a deleted file can't empty the server. Pass wl to reuse
    ' an already-loaded list (the !protect sweep); otherwise the file is re-read.
    Private Sub KickIfNotWhitelisted(ByVal playerName As String, Optional ByVal wl As HashSet(Of String) = Nothing)
        playerName = Trim(playerName)
        If playerName = "" OrElse IsAdmin(playerName) Then Return
        If wl Is Nothing Then wl = LoadWhitelist()
        If wl Is Nothing OrElse wl.Count = 0 Then
            lbConsola.Items.Add(Mid(Date.Now, InStr(Date.Now, " ") + 1, 8) & " protect: whitelist.txt missing/empty - not kicking " & playerName)
            Return
        End If
        If Not wl.Contains(playerName) Then
            SendToConsole("/kick " & playerName & " Not on the whitelist")
            SendToConsole("/adminsay " & playerName & " was kicked: server is whitelist-only right now.")
        End If
    End Sub

    Private Sub ReadProtectState()
        Try
            Dim f As String = rutaAppData & "\Config\Protect.ini"
            If File.Exists(f) Then protectOn = CBool(File.ReadAllText(f).Trim())
        Catch
            protectOn = False
        End Try
    End Sub

    Private Sub WriteProtectState()
        Try
            My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Config")
            File.WriteAllText(rutaAppData & "\Config\Protect.ini", protectOn.ToString())
        Catch
        End Try
    End Sub

    ' Writes ONE command line to the voicechat relay's command file (musicCmdPath). Overwrites
    ' (never appends) so the relay always reads the intended command, and writes IN PLACE
    ' (truncate + rewrite the same file), NOT via a temp file + rename. This matters because
    ' music_cmd.txt is a SINGLE-FILE Docker bind mount shared with the relay container: a
    ' rename would swap in a new inode that the container's mount can't see, leaving the relay
    ' stuck on the old content. An in-place truncate keeps the same inode, so the relay sees
    ' the change. The payload is one tiny line the relay parses verbatim (no extra tokens); a
    ' torn read is a non-issue at the relay's 1s poll for a few bytes.
    Private Sub WriteMusicCommand(ByVal cmd As String)
        Try
            File.WriteAllText(musicCmdPath, cmd & vbLf)
        Catch ex As Exception
            lbConsola.Items.Add(Mid(Date.Now, InStr(Date.Now, " ") + 1, 8) & " music cmd write failed: " & ex.Message)
        End Try
    End Sub


    'Subrutina para Ordenar los ListBox cuando añadimos un item
    Private Sub AddItemsToListBoxOrdered(lb As ListBox, itemToAdd As String)
        lb.Items.Add(itemToAdd)
        'Array donde pasaremos todos los ficheros para ordenarlos descendente y devolverlos al listbox
        Dim arrayToOrder(lb.Items.Count - 1)
        For i = 0 To lb.Items.Count - 1
            arrayToOrder(i) = lb.Items(i)
        Next

        'Ordenamos el array 
        Array.Sort(arrayToOrder)

        'vaciamos el listbox para llenarlo ordenado
        lb.Items.Clear()

        lb.Items.AddRange(arrayToOrder)
    End Sub


    Private Sub btnHelp_Click() Handles btnHelp.Click
        Cursor = Cursors.WaitCursor
        Help.ShowHelp(Me, Application.StartupPath & "\CoJ Mods.chm")
        Cursor = Cursors.Default
    End Sub
End Class