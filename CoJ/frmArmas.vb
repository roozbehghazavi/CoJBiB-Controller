' =================================================================================
' frmArmas.vb - Weapon mods form
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

Public Class frmArmas
    'Dim write As StreamWriter
    Dim readini As StreamReader
    Dim writeini As StreamWriter
    Dim clase As String
    'Dim weap1 As String
    'Dim weap2 As String
    'Dim weap3 As String
    'Dim weap4 As String = """WeaponDynamiteStickNet"""
    Dim predeterminadaL As String
    Dim predeterminadaR As String
    Dim estabaActivadoHC As Integer = 0
    Dim currentFile As String
    Dim numeroClases As Integer
    'Dim descargando As Boolean = False


    'Variable para dar valor 0 si es false el check de la dinamita o 1 si es true
    Dim TntSiNo As String = ""

    Private Sub frmArmas_Leave(sender As Object, e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing

        Dim IniFinal As String = ""

        If ModIniCabecera = "" Then ModIniCabecera = "0"
        If ModIniCabecera <> "" Then IniFinal = IniFinal & ModIniCabecera
        If Trim(ModIniAristocrat) <> "" Then IniFinal = IniFinal & "Aristocrat" & ModIniAristocrat
        If Trim(ModIniBandit) <> "" Then IniFinal = IniFinal & "Bandit" & ModIniBandit
        If Trim(ModIniBarnsby) <> "" Then IniFinal = IniFinal & "Barnsby" & ModIniBarnsby
        If Trim(ModIniDoc) <> "" Then IniFinal = IniFinal & "Doc" & ModIniDoc
        If Trim(ModIniGunslinger) <> "" Then IniFinal = IniFinal & "Gunslinger" & ModIniGunslinger
        If Trim(ModIniGunsmith) <> "" Then IniFinal = IniFinal & "Gunsmith" & ModIniGunsmith
        If Trim(ModIniJuarez) <> "" Then IniFinal = IniFinal & "Juarez" & ModIniJuarez
        If Trim(ModIniMiner) <> "" Then IniFinal = IniFinal & "Miner" & ModIniMiner
        If Trim(ModIniRifleman) <> "" Then IniFinal = IniFinal & "Rifleman" & ModIniRifleman
        If Trim(ModIniSniper) <> "" Then IniFinal = IniFinal & "Sniper" & ModIniSniper
        If Trim(ModIniThief) <> "" Then IniFinal = IniFinal & "Thief" & ModIniThief
        If Trim(ModIniTrapper) <> "" Then IniFinal = IniFinal & "Trapper" & ModIniTrapper
        If Trim(ModIniTribeWarrior) <> "" Then IniFinal = IniFinal & "TribeWarrior" & ModIniTribeWarrior
        If Trim(ModIniRancher) <> "" Then IniFinal = IniFinal & "Rancher" & ModIniRancher
        If Trim(ModIniRedneck) <> "" Then IniFinal = IniFinal & "Redneck" & ModIniRedneck



        If Trim(ConfiguracionModInicial) <> Trim(IniFinal) And Trim(IniFinal) <> "0" Then
            Dim guardarmsg = MessageBox.Show("The current changes to this mod haven't been saved. Do you want to save now?", "", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button3)
            If guardarmsg = vbYes Then
                btnSave_Click()
            ElseIf guardarmsg = vbCancel Then
                e.Cancel = True
                Exit Sub
            End If
        End If


        If System.IO.Directory.Exists(ruta & rutaTemp) Then
            My.Computer.FileSystem.DeleteDirectory(ruta & rutaTemp, FileIO.DeleteDirectoryOption.DeleteAllContents)
        End If

        FrmMain.Show()
        Me.Dispose()
        'Application.Exit()

    End Sub




    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load


        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Mods\")

        RecargarMisMods()

        'Activamos o desactivamos el botón de activar mod.
        If lbNewChar.Items.Count = 0 Then
            btnSetMod.Enabled = False
        Else
            btnSetMod.Enabled = True
        End If

        'Cambiamos el color de la caja de predeterminado
        cbDefault.BackColor = Color.SeaGreen

        'Desactivamos por defecto las dinamitas.
        lblAmmo4.Enabled = False
        cbAmmo4.Enabled = False
        cbAmmo4.Text = 0
        pictureWeap4.ImageLocation = Application.StartupPath & "\images\weaps\nodynamite.png"


        'Asignamos los item que estarán seleccionados inicialmente.
        cbClass.SelectedIndex = 0
        cbWeap1.SelectedIndex = 8
        cbWeap2.SelectedIndex = 3
        cbWeap3.SelectedIndex = 5
        cbAmmo1.SelectedIndex = 1
        cbAmmo2.SelectedIndex = 3
        cbAmmo3.SelectedIndex = 3
        cbAmmo5.SelectedIndex = 0
        cbDefault.SelectedIndex = 0

        'Asignamos las imagenes de armas y clase iniciales.
        pictureWeap1.ImageLocation = Application.StartupPath & "\images\weaps\pistol" & cbWeap1.SelectedIndex.ToString & ".png"
        pictureWeap2.ImageLocation = Application.StartupPath & "\images\weaps\pistol" & cbWeap2.SelectedIndex.ToString & ".png"
        pictureWeap3.ImageLocation = Application.StartupPath & "\images\weaps\rifle" & cbWeap3.SelectedIndex.ToString & ".png"
        pictureClass.ImageLocation = Application.StartupPath & "\images\characters\" & cbClass.Text & ".jpg"

        'Si no hay ningun mod seleccionado, desactivamos el tab y el panel de modificacion de armas.
        TabControlMods.Enabled = False
        panelModificarArmas.Enabled = False
        checkHC.Enabled = False
        panelAmmo.Enabled = False
        panelLeftHand.Enabled = False
        panelRightHand.Enabled = False
        panelBothHands.Enabled = False



    End Sub

    Private Sub cbWeap1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbWeap1.SelectedIndexChanged
        'Cambiamos la imagen del arma de mano izquierda para mostrar la imagen del arma seleccionada.
        pictureWeap1.ImageLocation = Application.StartupPath & "\images\weaps\pistol" & cbWeap1.SelectedIndex.ToString & ".png"

        ''Asignamos a la variable weap1 el nombre de arma que se utiliza en el archivo NetWeapon
        'If cbWeap1.SelectedIndex = 0 Then
        '    weap1 = """WeaponPistolCogswellNet"""
        'ElseIf cbWeap1.SelectedIndex = 1 Then
        '    weap1 = """WeaponPistolDerringerNet"""
        'ElseIf cbWeap1.SelectedIndex = 2 Then
        '    weap1 = """WeaponPistolLemantNet"""
        'ElseIf cbWeap1.SelectedIndex = 3 Then
        '    weap1 = """WeaponPistolPeacemakerNet"""
        'ElseIf cbWeap1.SelectedIndex = 4 Then
        '    weap1 = """WeaponRifleRemingtonSawOffNet"""
        'ElseIf cbWeap1.SelectedIndex = 5 Then
        '    weap1 = """WeaponPistolSchofieldNet"""
        'ElseIf cbWeap1.SelectedIndex = 6 Then
        '    weap1 = """WeaponPistolVolcanicNet"""
        'ElseIf cbWeap1.SelectedIndex = 7 Then
        '    weap1 = """WeaponPistolWalkerNet"""
        'ElseIf cbWeap1.SelectedIndex = 8 Then
        '    weap1 = ""
        'End If

        If cbWeap1.SelectedIndex = 8 Then
            pictureWeap1.BackColor = Color.Transparent
        Else
            If cbDefault.SelectedIndex = 1 Then
                pictureWeap1.BackColor = Color.SeaGreen
            End If
        End If

        CogeValorInis()

    End Sub

    Private Sub cbWeap2_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbWeap2.SelectedIndexChanged
        'Cambiamos la imagen del arma de mano derecha para mostrar la imagen del arma seleccionada.
        pictureWeap2.ImageLocation = Application.StartupPath & "\images\weaps\pistol" & cbWeap2.SelectedIndex.ToString & ".png"

        'Asignamos a la variable weap2 el nombre de arma que se utiliza en el archivo NetWeapon
        'If cbWeap2.SelectedIndex = 0 Then
        '    weap2 = """WeaponPistolCogswellNet"""
        'ElseIf cbWeap2.SelectedIndex = 1 Then
        '    weap2 = """WeaponPistolDerringerNet"""
        'ElseIf cbWeap2.SelectedIndex = 2 Then
        '    weap2 = """WeaponPistolLemantNet"""
        'ElseIf cbWeap2.SelectedIndex = 3 Then
        '    weap2 = """WeaponPistolPeacemakerNet"""
        'ElseIf cbWeap2.SelectedIndex = 4 Then
        '    weap2 = """WeaponRifleRemingtonSawOffNet"""
        'ElseIf cbWeap2.SelectedIndex = 5 Then
        '    weap2 = """WeaponPistolSchofieldNet"""
        'ElseIf cbWeap2.SelectedIndex = 6 Then
        '    weap2 = """WeaponPistolVolcanicNet"""
        'ElseIf cbWeap2.SelectedIndex = 7 Then
        '    weap2 = """WeaponPistolWalkerNet"""
        'ElseIf cbWeap2.SelectedIndex = 8 Then
        '    weap2 = ""
        'End If

        'Cambiamos el color del fondo dependiendo de las armas por defecto
        If cbWeap2.SelectedIndex = 8 Then
            pictureWeap2.BackColor = Color.Transparent
        Else

            If cbDefault.SelectedIndex = 1 Or cbDefault.SelectedIndex = 2 Then
                pictureWeap2.BackColor = Color.SeaGreen
            End If

        End If
        CogeValorInis()

    End Sub

    Private Sub cbWeap3_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbWeap3.SelectedIndexChanged
        'Cambiamos la imagen del arma de dos manos para mostrar la imagen del arma seleccionada.
        pictureWeap3.ImageLocation = Application.StartupPath & "\images\weaps\rifle" & cbWeap3.SelectedIndex.ToString & ".png"

        'Asignamos a la variable weap3 el nombre de arma que se utiliza en el archivo NetWeapon
        'If cbWeap3.SelectedIndex = 0 Then
        '    weap3 = """WeaponBowNet"""
        'ElseIf cbWeap3.SelectedIndex = 1 Then
        '    weap3 = """WeaponRifleRemingtonNet"""
        'ElseIf cbWeap3.SelectedIndex = 2 Then
        '    weap3 = """WeaponRifleSharpsNet"""
        'ElseIf cbWeap3.SelectedIndex = 3 Then
        '    weap3 = """WeaponRifleSharpsScopeNet"""
        'ElseIf cbWeap3.SelectedIndex = 4 Then
        '    weap3 = """WeaponRifleWinchesterNet"""
        'ElseIf cbWeap3.SelectedIndex = 5 Then
        '    weap3 = """WeaponRifleWinchesterScopeNet"""
        'ElseIf cbWeap3.SelectedIndex = 6 Then
        '    weap3 = ""
        'End If
        CogeValorInis()

    End Sub

    Private Sub checkWeap4_CheckedChanged(sender As Object, e As EventArgs) Handles checkWeap4.CheckedChanged
        If checkWeap4.Checked = True Then 'Si marcamos la casilla de TNT, activamos su combobox de cantidad y ponemos por defecto la cantidad de 2 TNTs.
            lblAmmo4.Enabled = True
            cbAmmo4.Enabled = True
            cbAmmo4.SelectedIndex = 1
            pictureWeap4.ImageLocation = Application.StartupPath & "\images\weaps\dynamite.png"

        Else 'Cuando desmarcamos el checkbox se desactiva el combobox y se pone el valor de 0.
            lblAmmo4.Enabled = False
            cbAmmo4.Enabled = False
            cbAmmo4.Text = 0
            pictureWeap4.ImageLocation = Application.StartupPath & "\images\weaps\nodynamite.png"
        End If
        CogeValorInis()



    End Sub

    Private Sub cbClass_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbClass.SelectedIndexChanged

        'Cambiamos el nombre del boton de armas por defecto
        btnDefault.Text = "Load default " & cbClass.Text & " weapons"

        'Cuando seleccionamos una clase, asigna un valor a las variables de los nombres de archivo.
        If cbClass.SelectedIndex = 0 Then
            clase = "_NetWeapon_Aristocrat_Properties.scr"

        ElseIf cbClass.SelectedIndex = 1 Then
            clase = "_NetWeapon_Bandit_Properties.scr"

        ElseIf cbClass.SelectedIndex = 2 Then
            clase = "_NetWeapon_Barnsby_Properties.scr"

        ElseIf cbClass.SelectedIndex = 3 Then
            clase = "_NetWeapon_Doc_Properties.scr"

        ElseIf cbClass.SelectedIndex = 4 Then
            clase = "_NetWeapon_Gunslinger_Properties.scr"

        ElseIf cbClass.SelectedIndex = 5 Then
            clase = "_NetWeapon_Gunsmith_Properties.scr"

        ElseIf cbClass.SelectedIndex = 6 Then
            clase = "_NetWeapon_Juarez_Properties.scr"

        ElseIf cbClass.SelectedIndex = 7 Then
            clase = "_NetWeapon_Miner_Properties.scr"

        ElseIf cbClass.SelectedIndex = 8 Then
            clase = "_NetWeapon_Rifleman_Properties.scr"

        ElseIf cbClass.SelectedIndex = 9 Then
            clase = "_NetWeapon_Sniper_Properties.scr"

        ElseIf cbClass.SelectedIndex = 10 Then
            clase = "_NetWeapon_Thief_Properties.scr"

        ElseIf cbClass.SelectedIndex = 11 Then
            clase = "_NetWeapon_Trapper_Properties.scr"

        ElseIf cbClass.SelectedIndex = 12 Then
            clase = "_NetWeapon_TribeWarrior_Properties.scr"

        ElseIf cbClass.SelectedIndex = 13 Then
            clase = "_NetWeapon_Rancher_Properties.scr"

        ElseIf cbClass.SelectedIndex = 14 Then
            clase = "_NetWeapon_Redneck_Properties.scr"

        End If

        'Mostramos la imagen del personaje seleccionado.
        pictureClass.ImageLocation = Application.StartupPath & "\images\characters\" & cbClass.Text & ".jpg"

        'Modificamos la label que muestra la clase que estamos creando.
        lblCreatingEditing.Text = "Creating class: " & cbClass.SelectedItem

    End Sub


    Private Sub cbDefault_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbDefault.SelectedIndexChanged

        'Ajustamos los colores de fondo de las armas dependiendo de la seleccion de armas por defecto.
        If cbDefault.SelectedIndex = 0 Then
            pictureWeap1.BackColor = Color.Transparent
            pictureWeap2.BackColor = Color.Transparent
            pictureWeap3.BackColor = Color.SeaGreen
            pictureWeap4.BackColor = Color.Transparent
        ElseIf cbDefault.SelectedIndex = 1 Then
            If cbWeap1.SelectedIndex = 8 Then
                pictureWeap1.BackColor = Color.Transparent
            Else
                pictureWeap1.BackColor = Color.SeaGreen
            End If

            If cbWeap2.SelectedIndex = 8 Then
                pictureWeap2.BackColor = Color.Transparent
            Else
                pictureWeap2.BackColor = Color.SeaGreen
            End If

            pictureWeap3.BackColor = Color.Transparent
            pictureWeap4.BackColor = Color.Transparent
        ElseIf cbDefault.SelectedIndex = 2 Then
            pictureWeap1.BackColor = Color.Transparent
            pictureWeap2.BackColor = Color.SeaGreen
            pictureWeap3.BackColor = Color.Transparent
            pictureWeap4.BackColor = Color.SeaGreen
            checkWeap4.Checked = True
        End If
        CogeValorInis()
    End Sub

    Private Sub btnRestore_Click(sender As Object, e As EventArgs) Handles btnRestore.Click
        Dim restoreclass As Integer
        restoreclass = MessageBox.Show("WARNING! All classes in dedicated server are going to be restored like original game's classes. Do you want to continue?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)

        If restoreclass = vbYes Then
            checkHC.Checked = False
            ' Only the class weapon files and the hardcore weapons file go: the Player folder can hold the server's
            ' own files too (and on Docker it is a mounted volume that cannot be deleted).
            Try
                ClearPlayerClassFiles(ruta & rutaPlayer)
                If File.Exists(ruta & rutaPlayer & "WeaponsParams.scr") Then File.Delete(ruta & rutaPlayer & "WeaponsParams.scr")
            Catch ex As Exception
                MessageBox.Show("The classes could not be restored: " & ex.Message, "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            End Try

        End If


    End Sub


    Private Sub cbAmmo1_KeyPress(sender As Object, e As KeyPressEventArgs) Handles cbAmmo1.KeyPress, cbAmmo2.KeyPress, cbAmmo3.KeyPress, cbAmmo4.KeyPress, cbAmmo5.KeyPress, cbClass.KeyPress, cbWeap1.KeyPress, cbWeap2.KeyPress, cbWeap3.KeyPress, cbDefault.KeyPress
        e.KeyChar = "" 'Evitar que se escriba en los combobox
    End Sub

    Private Sub btnDefault_Click(sender As Object, e As EventArgs) Handles btnDefault.Click
        If cbClass.SelectedIndex = 0 Then 'Explorador
            cbWeap1.SelectedIndex = 8
            cbWeap2.SelectedIndex = 3
            cbWeap3.SelectedIndex = 5
            checkWeap4.Checked = False
            cbDefault.SelectedIndex = 0
        ElseIf cbClass.SelectedIndex = 1 Then 'Hombre
            cbWeap1.SelectedIndex = 4
            cbWeap2.SelectedIndex = 4
            cbWeap3.SelectedIndex = 6
            checkWeap4.Checked = False
            cbDefault.SelectedIndex = 1
        ElseIf cbClass.SelectedIndex = 2 Then 'Veterano
            cbWeap1.SelectedIndex = 8
            cbWeap2.SelectedIndex = 8
            cbWeap3.SelectedIndex = 2
            checkWeap4.Checked = False
            cbDefault.SelectedIndex = 0
        ElseIf cbClass.SelectedIndex = 3 Then 'Duelista
            cbWeap1.SelectedIndex = 8
            cbWeap2.SelectedIndex = 6
            cbWeap3.SelectedIndex = 6
            checkWeap4.Checked = False
            cbDefault.SelectedIndex = 1
        ElseIf cbClass.SelectedIndex = 4 Then 'Artillero
            cbWeap1.SelectedIndex = 5
            cbWeap2.SelectedIndex = 5
            cbWeap3.SelectedIndex = 6
            checkWeap4.Checked = True
            cbAmmo4.SelectedIndex = 1
            cbDefault.SelectedIndex = 1
        ElseIf cbClass.SelectedIndex = 5 Then 'Armero
            cbWeap1.SelectedIndex = 2
            cbWeap2.SelectedIndex = 0
            cbWeap3.SelectedIndex = 6
            checkWeap4.Checked = False
            cbDefault.SelectedIndex = 1
        ElseIf cbClass.SelectedIndex = 6 Then 'Oficial
            cbWeap1.SelectedIndex = 7
            cbWeap2.SelectedIndex = 7
            cbWeap3.SelectedIndex = 6
            checkWeap4.Checked = False
            cbDefault.SelectedIndex = 1
        ElseIf cbClass.SelectedIndex = 7 Then 'Miner
            cbWeap1.SelectedIndex = 7
            cbWeap2.SelectedIndex = 4
            cbWeap3.SelectedIndex = 6
            checkWeap4.Checked = True
            cbAmmo4.SelectedIndex = 4
            cbDefault.SelectedIndex = 2
        ElseIf cbClass.SelectedIndex = 8 Then 'Fusilero
            cbWeap1.SelectedIndex = 3
            cbWeap2.SelectedIndex = 3
            cbWeap3.SelectedIndex = 4
            checkWeap4.Checked = False
            cbDefault.SelectedIndex = 0
        ElseIf cbClass.SelectedIndex = 9 Then 'Tirador
            cbWeap1.SelectedIndex = 8
            cbWeap2.SelectedIndex = 7
            cbWeap3.SelectedIndex = 3
            checkWeap4.Checked = False
            cbDefault.SelectedIndex = 0
        ElseIf cbClass.SelectedIndex = 10 Then 'Spy
            cbWeap1.SelectedIndex = 8
            cbWeap2.SelectedIndex = 1
            cbWeap3.SelectedIndex = 6
            checkWeap4.Checked = True
            cbAmmo4.SelectedIndex = 2
            cbDefault.SelectedIndex = 2
        ElseIf cbClass.SelectedIndex = 11 Then 'Trampero
            cbWeap1.SelectedIndex = 8
            cbWeap2.SelectedIndex = 7
            cbWeap3.SelectedIndex = 1
            checkWeap4.Checked = False
            cbDefault.SelectedIndex = 0
        ElseIf cbClass.SelectedIndex = 12 Then 'Piel Roja
            cbWeap1.SelectedIndex = 8
            cbWeap2.SelectedIndex = 8
            cbWeap3.SelectedIndex = 0
            checkWeap4.Checked = False
            cbDefault.SelectedIndex = 0
        ElseIf cbClass.SelectedIndex >= 13 Then 'Rancher / Redneck (IranCoJ New Classes)
            LoadDefaultWeapons(cbClass.SelectedIndex)
        End If
    End Sub

    ' A class's own weapons (ClassDefaults), as the Load default weapons button and the class picker set them:
    ' default weapon, left / right / both-hands weapon and dynamite. The ammo stays as it is.
    Public Sub LoadDefaultWeapons(ByVal classIndex As Integer)
        Dim ini As String = ClassDefaults(classIndex)
        cbWeap1.SelectedIndex = IniIndex(ini, 2)
        cbWeap2.SelectedIndex = IniIndex(ini, 3)
        cbWeap3.SelectedIndex = IniIndex(ini, 4)
        checkWeap4.Checked = IniIndex(ini, 1) = 1
        If checkWeap4.Checked Then cbAmmo4.SelectedIndex = IniIndex(ini, 9)
        cbDefault.SelectedIndex = IniIndex(ini, 0)
    End Sub

    Private Sub checkHC_CheckedChanged(sender As Object, e As EventArgs) Handles checkHC.CheckedChanged

        If checkHC.Checked = True And estabaActivadoHC = 0 Then
            Dim HCmsgbox As Integer
            HCmsgbox = MessageBox.Show("All classes could die with only one shot in this gamemode. Do you want to continue?", "Hardcore Mode", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

            If HCmsgbox = vbYes Then
                ModIniCabecera = "1"
            Else
                checkHC.Checked = False
            End If

        Else
            ModIniCabecera = "0"

        End If

    End Sub


    Private Sub frmArmas_Shown(sender As Object, e As EventArgs) Handles Me.Shown

        'Comprobamos si está activado el HC mode
        If System.IO.File.Exists(ruta & rutaTemp & "WeaponsParams.scr") Then
            estabaActivadoHC = 1
            checkHC.Checked = True
            estabaActivadoHC = 0
        End If

        'Vaciamos la label de crear/editar
        lblCreatingEditing.Text = "Weapon Configuration Class: ..."


        pictureClass.ImageLocation = Application.StartupPath & "\images\fondo.jpg"


    End Sub

    Private Sub PictureBox1_Click(sender As Object, e As EventArgs) Handles PictureBox1.Click
        frmAcercade.Show()
    End Sub

    Private Sub lbNewChar_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lbNewChar.SelectedIndexChanged
        Dim iniActual As String = ""

        Dim key As String = ClassKeyOf(lbNewChar.SelectedItem)
        If key <> "" Then
            currentFile = "_NetWeapon_" & key & "_Properties.scr"
            iniActual = GetModIni(key)
        End If

        If Len(iniActual) > 0 Then
            cbDefault.SelectedIndex = IniIndex(iniActual, 0)
            checkWeap4.Checked = IniIndex(iniActual, 1) <> 0
            cbWeap1.SelectedIndex = IniIndex(iniActual, 2)
            cbWeap2.SelectedIndex = IniIndex(iniActual, 3)
            cbWeap3.SelectedIndex = IniIndex(iniActual, 4)
            cbAmmo1.SelectedIndex = IniIndex(iniActual, 5)
            cbAmmo2.SelectedIndex = IniIndex(iniActual, 6)
            cbAmmo5.SelectedIndex = IniIndex(iniActual, 7)
            cbAmmo3.SelectedIndex = IniIndex(iniActual, 8)
            cbAmmo4.SelectedIndex = IniIndex(iniActual, 9)
        End If

        pictureClass.ImageLocation = Application.StartupPath & "\images\characters\" & lbNewChar.Text & ".jpg"

        'Modificamos la label que muestra la clase que se está editando
        lblCreatingEditing.Text = "Weapon Configuration Class: " & UCase(lbNewChar.SelectedItem)
    End Sub

    Private Sub btnSave_Click() Handles btnSave.Click 'sender As Object, e As EventArgs
        If modName = "" Then

            cancelModName = False
            frmModName.ShowDialog()

        End If

        If modName = "" Or cancelModName = True Then
            cancelModName = False
            Exit Sub
        End If

        If System.IO.Directory.Exists(rutaAppData & "\Mods\" & modName) And ModNameComprobacionGuardar <> modName Then

            Dim reemplazarMod As Integer
            reemplazarMod = MessageBox.Show("Existing Mod. Do you want to overwrite the gamemode " & modName.ToUpper & " ?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation)

            If reemplazarMod = vbYes Then

                If System.IO.Directory.Exists(rutaAppData & "\Mods\" & modName) Then
                    My.Computer.FileSystem.DeleteDirectory(rutaAppData & "\Mods\" & modName, FileIO.DeleteDirectoryOption.DeleteAllContents)
                End If
            Else
                If tabNewMod.Text <> "New Mod" Then
                    modName = tabNewMod.Text
                Else
                    modName = ""
                End If
                Exit Sub
            End If
        End If

        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Mods")
        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Mods\" & modName)



        tabNewMod.Text = modName
        GuardarClases()
        RecargarMisMods()
        EscribeFicheroModIni()

        lbLoadMod.Text = modName

        ModNameComprobacionGuardar = modName


    End Sub

    Private Sub btnSaveAs_Click(sender As Object, e As EventArgs) Handles btnSaveAs.Click

        cancelModName = False
        frmModName.ShowDialog()

        If modName = "" Or cancelModName = True Then
            cancelModName = False
            Exit Sub
        End If

        If System.IO.Directory.Exists(rutaAppData & "\Mods\" & modName) Then

            Dim reemplazarMod As Integer
            reemplazarMod = MessageBox.Show("Existing Mod. Do you want to overwrite the gamemode " & modName.ToUpper & " ?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation)

            If reemplazarMod = vbYes Then

                If System.IO.Directory.Exists(rutaAppData & "\Mods\" & modName) Then
                    My.Computer.FileSystem.DeleteDirectory(rutaAppData & "\Mods\" & modName, FileIO.DeleteDirectoryOption.DeleteAllContents)
                End If
            Else
                If tabNewMod.Text <> "New Mod" Then
                    modName = tabNewMod.Text
                Else
                    modName = ""
                End If

                Exit Sub
            End If
        End If

        modName = frmModName.txtModName.Text
        tabNewMod.Text = modName
        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Mods")
        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Mods\" & modName)


        GuardarClases()
        EscribeFicheroModIni()
        RecargarMisMods()

        lbLoadMod.Text = modName

    End Sub

    Private Sub btnDeleteMod_Click(sender As Object, e As EventArgs) Handles btnDeleteMod.Click

        frmDeleteMods.ShowDialog()

        RecargarMisMods()

        'Si no hay ningun mod seleccionado, desactivamos el tab y el panel de modificacion de armas.
        If lbLoadMod.SelectedItem = "" Then
            TabControlMods.Enabled = False
            panelModificarArmas.Enabled = False
            checkHC.Enabled = False
            btnLookMod.Text = "Look Mod ..."
            panelAmmo.Enabled = False
            panelLeftHand.Enabled = False
            panelRightHand.Enabled = False
            panelBothHands.Enabled = False
        Else
            TabControlMods.Enabled = True
            panelModificarArmas.Enabled = True
            checkHC.Enabled = True
            panelAmmo.Enabled = True
            panelLeftHand.Enabled = True
            panelRightHand.Enabled = True
            panelBothHands.Enabled = True
        End If
    End Sub


    Private Sub lbLoadMod_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lbLoadMod.SelectedIndexChanged

        lbNewChar.Items.Clear()

        If System.IO.Directory.Exists(ruta & rutaTemp) Then
            My.Computer.FileSystem.DeleteDirectory(ruta & rutaTemp, FileIO.DeleteDirectoryOption.DeleteAllContents)
        End If

        If System.IO.Directory.Exists(rutaAppData & "\mods\" & lbLoadMod.SelectedItem & "\") Then
            My.Computer.FileSystem.CopyDirectory(rutaAppData & "\mods\" & lbLoadMod.SelectedItem & "\", ruta & "\temp")
            OcultarCarpeta(ruta & rutaTemp)
        End If


        For Each foundFile As String In My.Computer.FileSystem.GetFiles(rutaAppData & "\mods\" & lbLoadMod.SelectedItem & "\")

            Dim lengthRuta As Integer = foundFile.Length
            Dim FirstCharacter As Integer = foundFile.IndexOf("_NetWeapon")
            Dim nameFile As String = foundFile.Substring(FirstCharacter).ToString()

            ObtenerNombreClase(nameFile)
            lbNewChar.Items.Add(nombreClaseObtenido)

        Next

        TabControlMods.SelectedTab = tabNewMod
        tabNewMod.Text = lbLoadMod.SelectedItem
        modName = lbLoadMod.SelectedItem

        If lbNewChar.Items.Count = 0 Then
            btnSetMod.Enabled = False
        Else
            btnSetMod.Enabled = True
        End If

        LeeFicheroModIni()


        If ModIniCabecera = "1" Then
            estabaActivadoHC = 1
            checkHC.Checked = True
            estabaActivadoHC = 0
        Else
            checkHC.Checked = False
        End If

        'Si no hay ningun mod seleccionado, desactivamos el tab y el panel de modificacion de armas.
        If lbLoadMod.SelectedItem = "" Then
            TabControlMods.Enabled = False
            panelModificarArmas.Enabled = False
            checkHC.Enabled = False
            panelAmmo.Enabled = False
            panelLeftHand.Enabled = False
            panelRightHand.Enabled = False
            panelBothHands.Enabled = False
        Else
            TabControlMods.Enabled = True
            panelModificarArmas.Enabled = True
            checkHC.Enabled = True
            panelAmmo.Enabled = True
            panelLeftHand.Enabled = True
            panelRightHand.Enabled = True
            panelBothHands.Enabled = True
        End If

        'Cambiamos el nombre al boton que abre el formulario de la tabla.
        If modName <> "" Then
            btnLookMod.Text = "Look Mod " & UCase(modName)
        Else
            btnLookMod.Text = "Look Mod ..."
        End If

        'Seleccionamos la primera clase de listbox de clases.
        lbNewChar.SelectedIndex = 0

        ModNameComprobacionGuardar = lbLoadMod.Text

    End Sub

    Private Sub btnSetMod_Click(sender As Object, e As EventArgs) Handles btnSetMod.Click


        'Comprobamos que la ruta especificada es correcta y es de un servidor dedicado.
        If System.IO.File.Exists(ruta & "\CoJ2Game_x86_ds.exe") And System.IO.File.Exists(ruta & "\CoJ2_x86_ds.dll") Then

            Dim myProcesses() As Process
            Dim myProcess As Process
            myProcesses = Process.GetProcesses()
            ' Iterate through the process array.
            For Each myProcess In myProcesses
                '  Console.WriteLine(myProcess.ProcessName)
                If myProcess.ProcessName = "CoJ2Game_x86_ds" Then

                    MessageBox.Show("You must close CoJ2 Dedicated Server before you make some changes. Close the server and try again. The mod has not been set.", "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                    Exit Sub
                End If
            Next

            Dim msgActiveMod As Integer
            msgActiveMod = MessageBox.Show("Your current server game mod will be changed for this one. Do you want to continue?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

            If msgActiveMod = vbYes Then

                ' The Player folder is never deleted (it can hold the server's own files, and on Docker it is a
                ' mounted volume): the old class weapon files are taken out and the mod's files copied over.
                Try
                    ClearPlayerClassFiles(ruta & rutaPlayer)
                    If System.IO.Directory.Exists(ruta & rutaTemp) Then CopyModFiles(ruta & rutaTemp, ruta & rutaPlayer)
                Catch ex As Exception
                    MessageBox.Show("The mod could not be set: " & ex.Message, "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                    Exit Sub
                End Try

            End If


            If System.IO.File.Exists(ruta & rutaPlayer & "WeaponsParams.scr") And estabaActivadoHC = 0 Then
                My.Computer.FileSystem.DeleteFile(ruta & rutaPlayer & "WeaponsParams.scr")
            End If

            If checkHC.Checked = True Then

                DecryptFile(Application.StartupPath & "\bin\modscoj.dll", ruta & rutaPlayer & "WeaponsParams.scr", sSecretKey)

            Else
                If My.Computer.FileSystem.FileExists(ruta & rutaPlayer & "WeaponsParams.scr") Then
                    My.Computer.FileSystem.DeleteFile(ruta & rutaPlayer & "WeaponsParams.scr")
                End If

            End If


        Else

                'Solicitamos la ruta de la carpeta del juego / servidor
            MessageBox.Show("You have to choose the same path where CoJ Dedicated Server was installed.", "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
                dialRuta.ShowDialog()
                ruta = dialRuta.SelectedPath()

                'Si la ruta especificada durante la creacion de las clases es correcta, la añadimos al archivo .ini
                If System.IO.File.Exists(ruta & "\CoJ2Game_x86_ds.exe") And System.IO.File.Exists(ruta & "\CoJ2_x86_ds.dll") Then
                    writeini = File.AppendText(Application.StartupPath & "\cojmods.ini")
                    writeini.WriteLine(ruta)
                    writeini.Close()
                End If
        End If

    End Sub


    'Rutina para que tomen valor los inis cada vez que cambiamos un list box (armas, ammo, class)
    Private Sub CogeValorInis()
        Dim IndexSelecEnCbammo4 As String = ""
        If checkWeap4.Checked = True Then
            IndexSelecEnCbammo4 = cbAmmo4.SelectedIndex.ToString
            TntSiNo = 1
        Else
            IndexSelecEnCbammo4 = 0
            TntSiNo = 0
        End If

        Dim key As String = ClassKeyOf(lbNewChar.Text)
        If key <> "" Then
            SetModIni(key, IniDigit(cbDefault.SelectedIndex) & TntSiNo & IniDigit(cbWeap1.SelectedIndex) & IniDigit(cbWeap2.SelectedIndex) & IniDigit(cbWeap3.SelectedIndex) & IniDigit(cbAmmo1.SelectedIndex) & IniDigit(cbAmmo2.SelectedIndex) & IniDigit(cbAmmo5.SelectedIndex) & IniDigit(cbAmmo3.SelectedIndex) & IndexSelecEnCbammo4)
        End If
    End Sub

    Private Sub cbAmmo1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbAmmo1.SelectedIndexChanged
        CogeValorInis()
    End Sub

    Private Sub cbAmmo2_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbAmmo2.SelectedIndexChanged
        CogeValorInis()
    End Sub

    Private Sub cbAmmo5_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbAmmo5.SelectedIndexChanged
        CogeValorInis()
    End Sub

    Private Sub cbAmmo3_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbAmmo3.SelectedIndexChanged
        CogeValorInis()
    End Sub

    Private Sub cbAmmo4_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbAmmo4.SelectedIndexChanged
        CogeValorInis()
    End Sub

    Private Sub btnLookMod_Click(sender As Object, e As EventArgs) Handles btnLookMod.Click
        frmTablaConfig.Show()
    End Sub

    Private Sub btnNewMod_Click(sender As Object, e As EventArgs) Handles btnNewMod.Click
        lbNewChar.Items.Clear()
        TabControlMods.SelectedTab = tabNewMod
        btnSetMod.Enabled = False
        modName = ""
        tabNewMod.Text = "New Mod"
        checkHC.Checked = False
        'Todas estas variables cogen los valores de las armas por defecto.
        ModIniAristocrat = "0083513030"
        ModIniBandit = "1044613030"
        ModIniBarnsby = "0088213030"
        ModIniCabecera = "0"
        ModIniDoc = "1086613030"
        ModIniGunslinger = "1155613031"
        ModIniGunsmith = "1020613030"
        ModIniJuarez = "1077613030"
        ModIniMiner = "2174613034"
        ModIniRifleman = "0033413030"
        ModIniSniper = "0087313030"
        ModIniThief = "2181613032"
        ModIniTrapper = "0087113030"
        ModIniTribeWarrior = "0088013030"
        ModIniRancher = ClassDefaults(13)
        ModIniRedneck = ClassDefaults(14)

        lbNewChar.Items.Add("Aristocrat")
        lbNewChar.Items.Add("Bandit")
        lbNewChar.Items.Add("Barnsby")
        lbNewChar.Items.Add("Doc")
        lbNewChar.Items.Add("Gunslinger")
        lbNewChar.Items.Add("Gunsmith")
        lbNewChar.Items.Add("Juarez")
        lbNewChar.Items.Add("Miner")
        lbNewChar.Items.Add("Rifleman")
        lbNewChar.Items.Add("Sniper")
        lbNewChar.Items.Add("Thief")
        lbNewChar.Items.Add("Trapper")
        lbNewChar.Items.Add("Tribe Warrior")
        lbNewChar.Items.Add("Rancher")
        lbNewChar.Items.Add("Redneck")

        TabControlMods.Enabled = True
        panelModificarArmas.Enabled = True
        checkHC.Enabled = True
        panelAmmo.Enabled = True
        panelLeftHand.Enabled = True
        panelRightHand.Enabled = True
        panelBothHands.Enabled = True
        lbLoadMod.Text = ""
        btnLookMod.Text = "Look Mod ..."

        'Seleccionamos la primera clase de listbox de clases.
        lbNewChar.SelectedIndex = 0

        ModNameComprobacionGuardar = ""


    End Sub

    Sub GuardarClases()

        'Comprobamos que la ruta especificada es correcta y es de un servidor dedicado.
        If System.IO.File.Exists(ruta & "\CoJ2Game_x86_ds.exe") And System.IO.File.Exists(ruta & "\CoJ2_x86_ds.dll") Then

            'Antes de llamar a GuardarClases() debe estar creada la carpeta destino del mod
            For i = 0 To ClassKeys.Length - 1

                If Not lbNewChar.Items.Contains(ClassNames(i)) Then Continue For
                clase = ClassKeys(i)
                Dim ModIniALeer As String = GetModIni(clase)
                If ModIniALeer = "" Then Continue For

                Dim fichero As String = rutaAppData & "\Mods\" & modName & "\_NetWeapon_" & clase & "_Properties.scr"

                'Borramos el archivo NetWeapon de la clase seleccionada, si existe.
                If System.IO.File.Exists(fichero) Then My.Computer.FileSystem.DeleteFile(fichero)

                'Armas: mano izquierda (3a posicion), mano derecha (4a), dos manos (5a). "" = ninguna.
                Dim WeaponLeft As String = Quoted(OneHandWeapons(IniIndex(ModIniALeer, 2)))
                Dim WeaponRight As String = Quoted(OneHandWeapons(IniIndex(ModIniALeer, 3)))
                Dim WeaponBoth As String = Quoted(BothHandsWeapons(IniIndex(ModIniALeer, 4)))

                'Municion: pistola (6a), rifle (7a), escopeta (8a), flechas (9a); TNT si/no (2a) y cantidad (10a).
                Dim AmmoPistol As Integer = AmmoAmounts(IniIndex(ModIniALeer, 5))
                Dim AmmoRifle As Integer = AmmoAmounts(IniIndex(ModIniALeer, 6))
                Dim AmmoShotgun As Integer = AmmoAmounts(IniIndex(ModIniALeer, 7))
                Dim AmmoArrows As Integer = AmmoAmounts(IniIndex(ModIniALeer, 8))
                Dim TieneTNT As Boolean = IniIndex(ModIniALeer, 1) <> 0
                Dim AmmoTNT As Integer = IniIndex(ModIniALeer, 9) + 1

                'Armas predeterminadas, primera posicion.
                Dim PredeterminadaLeft As String = ""
                Dim PredeterminadaRight As String = ""
                Select Case IniIndex(ModIniALeer, 0)
                    Case 0 'Arma de dos manos
                        PredeterminadaRight = WeaponBoth
                    Case 1 'Dos pistolas
                        PredeterminadaLeft = WeaponLeft
                        PredeterminadaRight = WeaponRight
                    Case 2 'PistolaR + TNT
                        PredeterminadaLeft = """WeaponDynamiteStickNet"""
                        PredeterminadaRight = WeaponRight
                End Select

                'Escritura de todas las armas y municiones en el archivo NetWeapon
                write = File.AppendText(fichero)
                write.WriteLine("!NetAmmo(s, i)")
                write.WriteLine("!NetWeapon(s)")
                write.WriteLine("!NetDefaultWeaponL(s)")
                write.WriteLine("!NetDefaultWeaponR(s)")
                If WeaponLeft <> "" Then write.WriteLine("NetWeapon(" & WeaponLeft & ")")
                If WeaponRight <> "" Then write.WriteLine("NetWeapon(" & WeaponRight & ")")
                If WeaponBoth <> "" Then write.WriteLine("NetWeapon(" & WeaponBoth & ")")
                If TieneTNT Then
                    For n = 1 To AmmoTNT
                        write.WriteLine("NetWeapon(""WeaponDynamiteStickNet"")")
                    Next
                End If
                write.WriteLine("NetAmmo(""AmmoPistol"", " & AmmoPistol & ")")
                write.WriteLine("NetAmmo(""AmmoRifle"", " & AmmoRifle & ")")
                write.WriteLine("NetAmmo(""AmmoShotgun"", " & AmmoShotgun & ")")
                write.WriteLine("NetAmmo(""AmmoArrows"", " & AmmoArrows & ")")

                If PredeterminadaLeft <> "" Then write.WriteLine("NetDefaultWeaponL(" & PredeterminadaLeft & ")")
                If PredeterminadaRight <> "" Then write.WriteLine("NetDefaultWeaponR(" & PredeterminadaRight & ")")

                write.Close() 'Terminamos la escritura del archivo NetWeapon y lo CERRAMOS.
            Next

        End If


    End Sub

    Private Shared Function Quoted(ByVal weapon As String) As String
        Return If(weapon = "", "", """" & weapon & """")
    End Function

    Private Sub btnHelp_Click(sender As Object, e As EventArgs) Handles btnHelp.Click
        Cursor = Cursors.WaitCursor
        Help.ShowHelp(Me, Application.StartupPath & "\CoJ Mods.chm")
        Cursor = Cursors.Default
    End Sub


End Class
