' =================================================================================
' WesternTheme.vb - Shared western/leather-and-parchment UI theming helpers.
' Date: 2026
' Created by: Roozbeh Ghazavi (fork additions).
'
'    Copyright (C) 2026 Roozbeh Ghazavi.
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
' =================================================================================

Imports System.Drawing
Imports System.Windows.Forms

' Applies a consistent "old western" look (aged wood, tooled leather, parchment
' and brass/gold accents) to any WinForms control tree at runtime. Recoloring only:
' control fonts are left alone so existing (dense) layouts are not reflowed/clipped.
Public Module WesternTheme

    ' ---- Palette -------------------------------------------------------------
    Public ReadOnly Wood As Color = Color.FromArgb(40, 26, 18)           ' dark aged wood (frame)
    Public ReadOnly Leather As Color = Color.FromArgb(74, 52, 38)        ' tooled leather (buttons, headers)
    Public ReadOnly LeatherLight As Color = Color.FromArgb(104, 74, 52)  ' leather hover
    Public ReadOnly Parchment As Color = Color.FromArgb(232, 216, 181)   ' page background
    Public ReadOnly ParchmentInput As Color = Color.FromArgb(245, 238, 218) ' inputs / lists
    Public ReadOnly Gold As Color = Color.FromArgb(196, 152, 74)         ' brass accent / borders
    Public ReadOnly InkDark As Color = Color.FromArgb(54, 36, 22)        ' text on parchment
    Public ReadOnly Cream As Color = Color.FromArgb(238, 224, 194)       ' text on wood/leather

    ' Returns a slab-serif "western" display face, falling back to a generic serif
    ' on machines without it (used only for buttons and tab captions).
    Public Function DisplayFont(ByVal size As Single, ByVal style As FontStyle) As Font
        For Each name As String In New String() {"Rockwell", "Playbill", "Georgia"}
            Try
                Dim f As New Font(name, size, style)
                If String.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase) Then Return f
                f.Dispose()
            Catch
            End Try
        Next
        Return New Font(FontFamily.GenericSerif, size, style)
    End Function

    ' Themes a form (or any container) and everything inside it.
    Public Sub Apply(ByVal root As Control)
        If root Is Nothing Then Return
        root.BackColor = Wood
        For Each c As Control In root.Controls
            StyleControl(c)
        Next
    End Sub

    Private Sub StyleControl(ByVal c As Control)
        If TypeOf c Is Button Then
            StyleButton(DirectCast(c, Button))
            Return
        ElseIf TypeOf c Is CheckBox Then
            Dim cb = DirectCast(c, CheckBox)
            cb.FlatStyle = FlatStyle.Flat
            cb.FlatAppearance.BorderSize = 0
            cb.ForeColor = InkDark
            cb.BackColor = Color.Transparent
            Return
        ElseIf TypeOf c Is RadioButton Then
            Dim rb = DirectCast(c, RadioButton)
            rb.FlatStyle = FlatStyle.Flat
            rb.FlatAppearance.BorderSize = 0
            rb.ForeColor = InkDark
            rb.BackColor = Color.Transparent
            Return
        ElseIf TypeOf c Is Label Then
            c.ForeColor = InkDark
            c.BackColor = Color.Transparent
            Return
        ElseIf TypeOf c Is LinkLabel Then
            Dim ll = DirectCast(c, LinkLabel)
            ll.LinkColor = Leather
            ll.ActiveLinkColor = Gold
            ll.BackColor = Color.Transparent
            Return
        ElseIf TypeOf c Is TextBox Then
            c.BackColor = ParchmentInput
            c.ForeColor = InkDark
            DirectCast(c, TextBox).BorderStyle = BorderStyle.FixedSingle
            Return
        ElseIf TypeOf c Is ListBox Then
            Dim lb = DirectCast(c, ListBox)
            lb.BackColor = ParchmentInput
            lb.ForeColor = InkDark
            lb.BorderStyle = BorderStyle.FixedSingle
            Return
        ElseIf TypeOf c Is ComboBox Then
            Dim cbo = DirectCast(c, ComboBox)
            cbo.FlatStyle = FlatStyle.Flat
            cbo.BackColor = ParchmentInput
            cbo.ForeColor = InkDark
            Return
        ElseIf TypeOf c Is DataGridView Then
            StyleGrid(DirectCast(c, DataGridView))
            Return
        ElseIf TypeOf c Is TabControl Then
            StyleTabs(DirectCast(c, TabControl))
            ' recurse into tab pages below
        ElseIf TypeOf c Is TabPage Then
            c.BackColor = Parchment
        ElseIf TypeOf c Is GroupBox Then
            c.ForeColor = InkDark
            c.BackColor = Color.Transparent
        ElseIf TypeOf c Is Panel Then
            c.BackColor = Parchment
        End If

        ' Recurse into containers.
        For Each child As Control In c.Controls
            StyleControl(child)
        Next
    End Sub

    Public Sub StyleButton(ByVal b As Button)
        b.FlatStyle = FlatStyle.Flat
        b.FlatAppearance.BorderColor = Gold
        b.FlatAppearance.BorderSize = 1
        b.FlatAppearance.MouseOverBackColor = LeatherLight
        b.FlatAppearance.MouseDownBackColor = Wood
        b.BackColor = Leather
        b.ForeColor = Cream
        b.UseVisualStyleBackColor = False
    End Sub

    Private Sub StyleGrid(ByVal dg As DataGridView)
        dg.EnableHeadersVisualStyles = False
        dg.BorderStyle = BorderStyle.FixedSingle
        dg.BackgroundColor = Parchment
        dg.GridColor = Leather
        dg.ColumnHeadersDefaultCellStyle.BackColor = Leather
        dg.ColumnHeadersDefaultCellStyle.ForeColor = Cream
        dg.ColumnHeadersDefaultCellStyle.SelectionBackColor = Leather
        dg.RowHeadersDefaultCellStyle.BackColor = Leather
        dg.RowHeadersDefaultCellStyle.ForeColor = Cream
        dg.DefaultCellStyle.BackColor = ParchmentInput
        dg.DefaultCellStyle.ForeColor = InkDark
        dg.DefaultCellStyle.SelectionBackColor = Gold
        dg.DefaultCellStyle.SelectionForeColor = Wood
    End Sub

    ' Owner-draws the tab strip so tabs look like branded leather tags with a
    ' brass underline on the active tab.
    Private Sub StyleTabs(ByVal tc As TabControl)
        tc.DrawMode = TabDrawMode.OwnerDrawFixed
        tc.SizeMode = TabSizeMode.Fixed
        ' Sized so all seven tabs end before the top-right Help button (x=400) and
        ' fit the strip without scroll buttons (7 x 56 = 392). Height kept modest so
        ' the strip does not steal room from the (already tall) tab pages.
        tc.ItemSize = New Size(56, 22)
        tc.BackColor = Wood
        RemoveHandler tc.DrawItem, AddressOf DrawTab   ' avoid double-hook if re-applied
        AddHandler tc.DrawItem, AddressOf DrawTab
    End Sub

    Private Sub DrawTab(ByVal sender As Object, ByVal e As DrawItemEventArgs)
        Dim tc As TabControl = DirectCast(sender, TabControl)
        Dim page As TabPage = tc.TabPages(e.Index)
        Dim r As Rectangle = tc.GetTabRect(e.Index)
        Dim selected As Boolean = (e.Index = tc.SelectedIndex)

        Using bg As New SolidBrush(If(selected, Leather, Wood))
            e.Graphics.FillRectangle(bg, r)
        End Using
        If selected Then
            Using p As New Pen(Gold, 2)
                e.Graphics.DrawLine(p, r.Left + 1, r.Bottom - 2, r.Right - 1, r.Bottom - 2)
            End Using
        End If

        Dim sf As New StringFormat()
        sf.Alignment = StringAlignment.Center
        sf.LineAlignment = StringAlignment.Center
        Using f As Font = DisplayFont(8.5F, If(selected, FontStyle.Bold, FontStyle.Regular))
            Using tb As New SolidBrush(Cream)
                e.Graphics.DrawString(page.Text, f, tb, New RectangleF(r.X, r.Y, r.Width, r.Height), sf)
            End Using
        End Using
        sf.Dispose()
    End Sub

End Module
