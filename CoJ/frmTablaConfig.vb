' =================================================================================
' frmTablaConfig.vb - Mod summary table form
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

Public Class frmTablaConfig



    ' Column names of the table's cells: panel<col>, panel<col>Dynamite / LeftWeapon / RightWeapon / BothHands and the
    ' ammo labels lbl<Pistol|Rifle|Shotgun>Ammo<col> / lblArrows<col>. The Tribe Warrior's column is "TW".
    Private Function ColumnOf(ByVal key As String) As String
        Return If(key = "TribeWarrior", "TW", key)
    End Function

    Private Function Cell(Of T As Control)(ByVal name As String) As T
        Dim found() As Control = Me.Controls.Find(name, True)
        Return If(found.Length > 0, TryCast(found(0), T), Nothing)
    End Function

    ' Double-click on a class's picture: edit that class in the mod form.
    Private Sub panelClass_DoubleClick(sender As Object, e As EventArgs) Handles panelAristocrat.DoubleClick, panelBandit.DoubleClick, panelBarnsby.DoubleClick, panelDoc.DoubleClick, panelGunslinger.DoubleClick, panelGunsmith.DoubleClick, panelJuarez.DoubleClick, panelMiner.DoubleClick, panelRifleman.DoubleClick, panelSniper.DoubleClick, panelThief.DoubleClick, panelTrapper.DoubleClick, panelTW.DoubleClick
        Dim col As String = DirectCast(sender, Control).Name.Substring("panel".Length)
        Dim i As Integer = Array.IndexOf(ClassKeys, If(col = "TW", "TribeWarrior", col))
        If i < 0 Then Exit Sub
        frmArmas.Focus()
        frmArmas.TabControlMods.SelectedTab = frmArmas.tabNewMod
        frmArmas.lbNewChar.SelectedItem = ClassNames(i)
        Me.Hide()
    End Sub

    Private Sub ArmasClase(modIniTabla As String, ClaseEditando As String)
        Dim left As PictureBox = Cell(Of PictureBox)("panel" & ClaseEditando & "LeftWeapon")
        Dim right As PictureBox = Cell(Of PictureBox)("panel" & ClaseEditando & "RightWeapon")
        Dim both As PictureBox = Cell(Of PictureBox)("panel" & ClaseEditando & "BothHands")
        Dim tnt As PictureBox = Cell(Of PictureBox)("panel" & ClaseEditando & "Dynamite")
        If left Is Nothing Or right Is Nothing Or both Is Nothing Or tnt Is Nothing Then Exit Sub
        Dim thumbs As String = Application.StartupPath & "\images\weaps\miniaturas\"

        'Armas predeterminadas (primera posicion) en verde.
        Dim def As Integer = IniIndex(modIniTabla, 0)
        left.BackColor = If(def = 1, Color.SeaGreen, SystemColors.Control)
        right.BackColor = If(def = 1 Or def = 2, Color.SeaGreen, SystemColors.Control)
        both.BackColor = If(def = 0, Color.SeaGreen, SystemColors.Control)
        tnt.BackColor = If(def = 2, Color.SeaGreen, SystemColors.Control)

        If IniIndex(modIniTabla, 1) = 0 Then
            tnt.ImageLocation = thumbs & "no_TNT.png"
        Else
            tnt.ImageLocation = thumbs & (IniIndex(modIniTabla, 9) + 1) & "_TNT.png"
        End If

        Dim l As String = OneHandThumbs(IniIndex(modIniTabla, 2))
        Dim r As String = OneHandThumbs(IniIndex(modIniTabla, 3))
        Dim b As String = BothHandsThumbs(IniIndex(modIniTabla, 4))
        left.ImageLocation = If(l = "", "", thumbs & l & ".png")
        right.ImageLocation = If(r = "", "", thumbs & r & ".png")
        both.ImageLocation = If(b = "", "", thumbs & b & ".png")

        Dim ammo() As String = {"lblPistolAmmo", "lblRifleAmmo", "lblShotgunAmmo", "lblArrows"}
        For k = 0 To 3
            Dim lbl As Label = Cell(Of Label)(ammo(k) & ClaseEditando)
            If lbl IsNot Nothing Then lbl.Text = AmmoAmounts(IniIndex(modIniTabla, 5 + k)).ToString()
        Next
    End Sub

    ' The designer's table has the 13 stock classes. The Rancher and Redneck (IranCoJ New Classes) get their columns
    ' here: each row's last cell (the Tribe Warrior's) is copied twice, right after it, and the table grows to fit.
    Private columnsAdded As Boolean = False

    Private Sub AddNewClassColumns()
        If columnsAdded Then Exit Sub
        columnsAdded = True
        Dim lastCells() As Control = {panelTW, Panel29, panelTWDynamite, panelTWLeftWeapon, panelTWRightWeapon, panelTWBothHands, Panel100, Panel114, Panel128, Panel142}
        Dim extra As Integer = 0
        FlowLayoutPanel1.SuspendLayout()
        For Each last As Control In lastCells
            Dim at As Integer = FlowLayoutPanel1.Controls.GetChildIndex(last)
            Dim n As Integer = 0
            For Each key As String In {"Rancher", "Redneck"}
                Dim c As Control = CopyCell(last, key)
                FlowLayoutPanel1.Controls.Add(c)
                n += 1
                FlowLayoutPanel1.Controls.SetChildIndex(c, at + n)
            Next
            extra = 2 * last.Width
        Next
        ' the two full-width strips (title and separator) and the table itself
        Panel1.Width += extra
        Panel157.Width += extra
        FlowLayoutPanel1.Width += extra
        FlowLayoutPanel1.ResumeLayout(True)
        Me.Width += extra
        btnSalir.Left += extra
        Me.AutoScroll = True
        Me.CenterToScreen()
    End Sub

    Private Function CopyCell(ByVal src As Control, ByVal key As String) As Control
        Dim c As Control
        If TypeOf src Is PictureBox Then
            Dim p As New PictureBox With {.SizeMode = DirectCast(src, PictureBox).SizeMode}
            c = p
        ElseIf TypeOf src Is Label Then
            c = New Label With {.AutoSize = DirectCast(src, Label).AutoSize, .TextAlign = DirectCast(src, Label).TextAlign}
        Else
            Dim p As New Panel With {.BorderStyle = DirectCast(src, Panel).BorderStyle}
            c = p
        End If
        If TypeOf src Is PictureBox Then DirectCast(c, PictureBox).BorderStyle = DirectCast(src, PictureBox).BorderStyle
        If TypeOf src Is Label Then DirectCast(c, Label).BorderStyle = DirectCast(src, Label).BorderStyle
        c.Name = If(src.Name.Contains("TW"), src.Name.Replace("TW", key), src.Name & key)
        c.Size = src.Size
        c.Margin = src.Margin
        c.Font = src.Font
        c.ForeColor = src.ForeColor
        c.BackColor = src.BackColor
        c.Cursor = src.Cursor
        c.BackgroundImageLayout = src.BackgroundImageLayout
        If src Is panelTW Then
            ' the class's picture; double-click edits it like the others
            c.BackgroundImage = Image.FromFile(Application.StartupPath & "\images\characters\" & key & ".jpg")
            AddHandler c.DoubleClick, AddressOf panelClass_DoubleClick
        End If
        If TypeOf src Is Label Then c.Text = src.Text
        For Each child As Control In src.Controls
            Dim cc As Control = CopyCell(child, key)
            cc.Location = child.Location
            If cc.Text = "Tribe Warrior" Then cc.Text = key   ' the class name row
            c.Controls.Add(cc)
        Next
        Return c
    End Function

    Private Sub frmTablaConfig_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        Me.Dispose()
    End Sub

    Private Sub frmTablaConfig_Load(sender As Object, e As EventArgs) Handles MyBase.Load, Me.Activated
        Me.Text = "Modding CoJ2 for Dedicated Servers - Setting Mod " & UCase(modName)
        AddNewClassColumns()

        For Each key As String In ClassKeys
            Dim ini As String = GetModIni(key)
            If ini <> "" Then ArmasClase(ini, ColumnOf(key))
        Next
    End Sub

    Private Sub btnSalir_Click(sender As Object, e As EventArgs) Handles btnSalir.Click
        Me.Close()
    End Sub

End Class