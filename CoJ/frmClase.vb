' =================================================================================
' frmClase.vb - Class selection form
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

Public Class frmClase

    Dim ClaseEnCreacion As Integer = 0

    ' One picture and one label per class, in the order of ClassNames (= frmArmas.cbClass). The 13 stock classes come
    ' from the designer; the Rancher and Redneck (IranCoJ New Classes) are added in the free spots of the third row.
    Dim pictures As New List(Of PictureBox)
    Dim labels As New List(Of Label)

    Private Sub frmClase_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        pictures.AddRange({pictureAristocrat, pictureBandit, pictureBarnsby, pictureDoc, pictureGunslinger, pictureGunsmith, pictureJuarez, pictureMiner, pictureRifleman, pictureSniper, pictureThief, pictureTrapper, pictureTribeWarrior})
        labels.AddRange({lblAristocrat, lblBandit, lblBarnsby, lblDoc, lblGunslinger, lblGunsmith, lblJuarez, lblMiner, lblRifleman, lblSniper, lblThief, lblTrapper, lblTribeWarrior})

        ' The third row's right side held the buttons: they move down a row to make room.
        Dim shift As Integer = 90
        checkLoadDefault.Top += shift
        btnAdd.Top += shift
        btnCancel.Top += shift
        Me.ClientSize = New Size(Me.ClientSize.Width, Me.ClientSize.Height + shift)

        Dim newNames() As String = {"Rancher", "Redneck"}
        Dim newX() As Integer = {pictureRifleman.Left, pictureSniper.Left}   ' the 4th and 5th columns
        For n = 0 To newNames.Length - 1
            Dim slot = New With {.Name = newNames(n), .X = newX(n)}
            Dim pic As New PictureBox With {
                .BorderStyle = pictureTribeWarrior.BorderStyle, .Cursor = Cursors.Hand, .SizeMode = PictureBoxSizeMode.Zoom,
                .Size = pictureTribeWarrior.Size, .Location = New Point(slot.X, pictureTribeWarrior.Top), .Name = "picture" & slot.Name,
                .ImageLocation = Application.StartupPath & "\images\characters\" & slot.Name & ".jpg"}
            Dim lbl As New Label With {
                .AutoSize = True, .Cursor = Cursors.Hand, .Font = lblTribeWarrior.Font, .Text = slot.Name, .Name = "lbl" & slot.Name}
            Controls.Add(pic)
            Controls.Add(lbl)
            lbl.Location = New Point(slot.X + (pic.Width - lbl.PreferredWidth) \ 2, lblTribeWarrior.Top)
            pictures.Add(pic)
            labels.Add(lbl)
        Next

        For i = 0 To pictures.Count - 1
            AddHandler pictures(i).Click, AddressOf Class_Click
            AddHandler labels(i).Click, AddressOf Class_Click
            AddHandler pictures(i).DoubleClick, AddressOf btnAdd_Click
        Next
    End Sub

    ' Click on a class: it shows its "selected" picture, the others their normal one.
    Private Sub Class_Click(sender As Object, e As EventArgs)
        Dim i As Integer = pictures.IndexOf(TryCast(sender, PictureBox))
        If i < 0 Then i = labels.IndexOf(TryCast(sender, Label))
        If i < 0 Then Exit Sub
        ClaseEnCreacion = i
        For k = 0 To pictures.Count - 1
            pictures(k).ImageLocation = Application.StartupPath & "\images\characters\" & If(k = i, "Selecc_", "") & ClassNames(k) & ".jpg"
        Next
    End Sub

    Private Sub btnAdd_Click(sender As Object, e As EventArgs) Handles btnAdd.Click
        frmArmas.cbClass.SelectedIndex = ClaseEnCreacion

        If checkLoadDefault.Checked = True Then
            frmArmas.lbNewChar.Items.Add(ClassNames(ClaseEnCreacion))
            frmArmas.lbNewChar.SelectedIndex = frmArmas.lbNewChar.Items.Count - 1
            frmArmas.LoadDefaultWeapons(ClaseEnCreacion)
        End If

        frmArmas.TabControlMods.SelectedTab = frmArmas.tabNewMod


        Me.Close()
    End Sub

    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click

        Me.Close()
    End Sub


    Private Sub frmClase_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        Me.Dispose()
    End Sub
End Class
