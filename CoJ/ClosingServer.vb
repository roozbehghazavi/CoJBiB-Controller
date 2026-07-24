' =================================================================================
' ClosingServer.vb - Closing server progress form
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

Public Class frmClosingServer

    Private returnToMainWindow As Boolean = True

    Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        CerrarServidor = False
        btnApagadoPulsado = False
        returnToMainWindow = False
        Me.Close()
        Me.Dispose()
        frmConsola.Show()
    End Sub


    Dim btnApagadoPulsado As Boolean = False
    Private Sub btnShutdown_Click(sender As Object, e As EventArgs) Handles btnShutdown.Click
        btnShutdown.Enabled = False
        btnCancel.Enabled = False
        SumaBarra = (PanelProgresBar.Width - 2) / 80
        btnApagadoPulsado = True
        Timershutdown.Enabled = True
        Label1.Text = "Coj2 Dedicated Server is shutting down"
        Timershutdown.Enabled = True

        Dim vText As String = "/servershutdown"
        Module1.windowSender.FindWindow()
        Module1.windowSender.SendMessageToConsole(vText)

        CerrarServidor = True
        frmConsola.CloseLogReader()
    End Sub


    Private Sub frmClosingServer_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        If btnApagadoPulsado = False Then
            If returnToMainWindow Then
                frmConsola.CloseLogReader()
                frmConsola.Close()
                frmConsola.Dispose()
                FrmMain.Show()
            End If
            'e.Cancel = True
        Else
            Me.Dispose()
        End If
    End Sub

    Dim countDown As Integer = 85
    Dim SumaBarra As Double = 0
    Dim Rojo As Double = 255
    Dim Verde As Double = 30
    Dim SumaResta As Double = (Rojo - Verde) / (countDown - 30)
    Private Sub Timershutdown_Tick(sender As Object, e As EventArgs) Handles Timershutdown.Tick
        If countDown < (55) Then
            Rojo = Rojo - SumaResta
            Verde = Verde + SumaResta
        End If


        If countDown = -1 Then
            Timershutdown.Enabled = False
            Me.Close()
            Me.Dispose()
            FrmMain.Show()
        Else
            FrmMain.Hide()
            If Verde < 256 Then
                lblProgressBarr.BackColor = System.Drawing.Color.FromArgb(Int(Rojo), Int(Verde), 30)
            End If
            lblProgressBarr.Width = (lblProgressBarr.Width + Int(SumaBarra))

            If Mid(countDown.ToString.Trim, 2, 1) = "0" Then
                Label1.Text = Label1.Text & "."
                btnShutdown.Text = "Shutdown (" & (countDown / 10).ToString.Trim & ")"
            End If
            countDown = countDown - 1
        End If
    End Sub

    Private Sub frmClosingServer_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        lblProgressBarr.Width = 0
    End Sub

    Private Sub frmClosingServer_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        FrmMain.Hide()
        frmConsola.Hide()
        returnToMainWindow = True
        btnApagadoPulsado = False
    End Sub
End Class