' =================================================================================
' Module1.vb - Shared functions used in different forms
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

Imports System
Imports System.IO
Imports System.Security
Imports System.Security.Cryptography
Imports System.Text
Imports System.Environment


Module Module1

    Public windowSender As New ConsoleWindowSender()
    Public CerrarServidor As Boolean = False
    Public modName As String
    Public ModNameComprobacionGuardar As String = ""
    Public nombreArchivoObtenido As String
    Public nombreClaseObtenido As String
    Public cancelModName As Boolean
    Public ruta As String
    Public rutaTemp As String = "\temp\"
    Public rutaPlayer As String = "\CoJ2\Data\Player\"
    Public rutaAppData As String = GetFolderPath(SpecialFolder.ApplicationData) & "\ModsCoJ2\"
    Public write As StreamWriter
    Public read As StreamReader
    Public vRestart As Boolean = False

    'Variables donde almacenaremos las 13 clases (0 a 12) que podemos crear antes de guardarlas en el fichero Mod.ini
    'y la cabecera general del fichero, o también cuando las leamos del mismo fichero, las almacenaremos aquí
    Public ModIniCabecera As String = ""
    Public ModIniAristocrat As String = ""
    Public ModIniBandit As String = ""
    Public ModIniBarnsby As String = ""
    Public ModIniDoc As String = ""
    Public ModIniGunslinger As String = ""
    Public ModIniGunsmith As String = ""
    Public ModIniJuarez As String = ""
    Public ModIniMiner As String = ""
    Public ModIniRifleman As String = ""
    Public ModIniSniper As String = ""
    Public ModIniThief As String = ""
    Public ModIniTrapper As String = ""
    Public ModIniTribeWarrior As String = ""
    Public ModIniRancher As String = ""
    Public ModIniRedneck As String = ""
    Public ConfiguracionModInicial As String = ""

    ' The classes, in the order of the class lists: shown name (lists and art), file key (_NetWeapon_<key>_Properties.scr
    ' and mod.ini) and the class's own weapons as a mod.ini string. Rancher and Redneck are the IranCoJ New Classes.
    Public ReadOnly ClassNames() As String = {"Aristocrat", "Bandit", "Barnsby", "Doc", "Gunslinger", "Gunsmith", "Juarez", "Miner", "Rifleman", "Sniper", "Thief", "Trapper", "Tribe Warrior", "Rancher", "Redneck"}
    Public ReadOnly ClassKeys() As String = {"Aristocrat", "Bandit", "Barnsby", "Doc", "Gunslinger", "Gunsmith", "Juarez", "Miner", "Rifleman", "Sniper", "Thief", "Trapper", "TribeWarrior", "Rancher", "Redneck"}
    Public ReadOnly ClassDefaults() As String = {"0083513030", "1044613030", "0088213030", "1086613030", "1155613031", "1020613030", "1077613030", "2174613034", "0033413030", "0087313030", "2181613032", "0087113030", "0088013030", "1099853030", "008B711030"}

    ' Weapon lists (index = position in the combo boxes). One hand: 8 = none; both hands: 6 = none.
    Public ReadOnly OneHandWeapons() As String = {"WeaponPistolCogswellNet", "WeaponPistolDerringerNet", "WeaponPistolLemantNet", "WeaponPistolPeacemakerNet", "WeaponRifleRemingtonSawOffNet", "WeaponPistolSchofieldNet", "WeaponPistolVolcanicNet", "WeaponPistolWalkerNet", "", "WeaponPistolFrontierNet", "WeaponPistolDerringerUpgradeNet", "WeaponPistolLemantUpgradeNet"}
    Public ReadOnly BothHandsWeapons() As String = {"WeaponBowNet", "WeaponRifleRemingtonNet", "WeaponRifleSharpsNet", "WeaponRifleSharpsScopeNet", "WeaponRifleWinchesterNet", "WeaponRifleWinchesterScopeNet", "", "WeaponPistolFrontierUpgradeNet", "WeaponPistolDerringerUpgradeNet", "WeaponPistolLemantUpgradeNet"}
    Public ReadOnly OneHandThumbs() As String = {"Cogswell", "Derringer", "Lemant", "PeaceMaker", "SawOff", "Schofield", "Volcanic", "Walker", "", "BorderGun", "ThrowingKnife", "Tomahawk"}
    Public ReadOnly BothHandsThumbs() As String = {"Bow", "Remington", "Sharp", "SharpScope", "Winchester", "WinchesterScope", "", "Musket", "ThrowingKnife", "Tomahawk"}
    Public ReadOnly AmmoAmounts() As Integer = {35, 45, 55, 65, 75, 95, 140, 190}

    ' mod.ini keeps one character per setting: 0-9, then A, B... for the weapons added after the first nine.
    Private Const IniChars As String = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"

    Public Function IniDigit(ByVal index As Integer) As String
        Return IniChars.Substring(Math.Max(0, index), 1)
    End Function

    Public Function IniIndex(ByVal ini As String, ByVal position As Integer) As Integer
        Return Math.Max(0, IniChars.IndexOf(Char.ToUpperInvariant(ini(position))))
    End Function

    Public Function GetModIni(ByVal key As String) As String
        Select Case key
            Case "Aristocrat" : Return ModIniAristocrat
            Case "Bandit" : Return ModIniBandit
            Case "Barnsby" : Return ModIniBarnsby
            Case "Doc" : Return ModIniDoc
            Case "Gunslinger" : Return ModIniGunslinger
            Case "Gunsmith" : Return ModIniGunsmith
            Case "Juarez" : Return ModIniJuarez
            Case "Miner" : Return ModIniMiner
            Case "Rifleman" : Return ModIniRifleman
            Case "Sniper" : Return ModIniSniper
            Case "Thief" : Return ModIniThief
            Case "Trapper" : Return ModIniTrapper
            Case "TribeWarrior" : Return ModIniTribeWarrior
            Case "Rancher" : Return ModIniRancher
            Case "Redneck" : Return ModIniRedneck
        End Select
        Return ""
    End Function

    Public Sub SetModIni(ByVal key As String, ByVal value As String)
        Select Case key
            Case "Aristocrat" : ModIniAristocrat = value
            Case "Bandit" : ModIniBandit = value
            Case "Barnsby" : ModIniBarnsby = value
            Case "Doc" : ModIniDoc = value
            Case "Gunslinger" : ModIniGunslinger = value
            Case "Gunsmith" : ModIniGunsmith = value
            Case "Juarez" : ModIniJuarez = value
            Case "Miner" : ModIniMiner = value
            Case "Rifleman" : ModIniRifleman = value
            Case "Sniper" : ModIniSniper = value
            Case "Thief" : ModIniThief = value
            Case "Trapper" : ModIniTrapper = value
            Case "TribeWarrior" : ModIniTribeWarrior = value
            Case "Rancher" : ModIniRancher = value
            Case "Redneck" : ModIniRedneck = value
        End Select
    End Sub

    Public Function ClassKeyOf(ByVal shownName As String) As String
        Dim i As Integer = Array.IndexOf(ClassNames, shownName)
        Return If(i >= 0, ClassKeys(i), "")
    End Function

    ' The server's CoJ2\Data\Player folder also holds files that are not the Controller's (a server can keep its own
    ' class and weapon scripts there, and on Docker it is a mounted volume that cannot be deleted). So the folder is
    ' never removed: only the class weapon files (_NetWeapon_<class>_Properties.scr) are taken out, then the mod's
    ' files are copied over, replacing any with the same name.
    ' The Rancher's and Redneck's weapon files are not in the game's own data (their stock versions differ), so after a
    ' clean-up they get their IranCoJ New Classes weapons back unless the mod sets them.
    Public Const RancherWeapons As String =
        "!NetAmmo(s, i)" & vbCrLf & "!NetWeapon(s)" & vbCrLf & "!NetDefaultWeaponL(s)" & vbCrLf & "!NetDefaultWeaponR(s)" & vbCrLf & vbCrLf &
        "NetDefaultWeaponR(""WeaponPistolFrontierNet"")" & vbCrLf & "NetDefaultWeaponL(""WeaponPistolFrontierNet"")" & vbCrLf &
        "NetWeapon(""WeaponPistolFrontierNet"")" & vbCrLf & "NetWeapon(""WeaponPistolFrontierNet"")" & vbCrLf &
        "NetAmmo(""AmmoPistol"", 90)" & vbCrLf & "NetWeapon(""WeaponPistolDerringerUpgradeNet"")" & vbCrLf
    Public Const RedneckWeapons As String =
        "!NetAmmo(s, i)" & vbCrLf & "!NetWeapon(s)" & vbCrLf & "!NetDefaultWeaponL(s)" & vbCrLf & "!NetDefaultWeaponR(s)" & vbCrLf & vbCrLf &
        "NetDefaultWeaponR(""WeaponPistolFrontierUpgradeNet"")" & vbCrLf & "NetWeapon(""WeaponPistolFrontierUpgradeNet"")" & vbCrLf &
        "NetAmmo(""AmmoRifle"", 45)" & vbCrLf & "NetWeapon(""WeaponPistolLemantUpgradeNet"")" & vbCrLf

    Public Sub ClearPlayerClassFiles(ByVal playerDir As String)
        If Not Directory.Exists(playerDir) Then Directory.CreateDirectory(playerDir)
        For Each key As String In ClassKeys
            Dim f As String = Path.Combine(playerDir, "_NetWeapon_" & key & "_Properties.scr")
            If File.Exists(f) Then
                File.SetAttributes(f, FileAttributes.Normal)
                File.Delete(f)
            End If
        Next
        File.WriteAllText(Path.Combine(playerDir, "_NetWeapon_Rancher_Properties.scr"), RancherWeapons)
        File.WriteAllText(Path.Combine(playerDir, "_NetWeapon_Redneck_Properties.scr"), RedneckWeapons)
    End Sub

    Public Sub CopyModFiles(ByVal sourceDir As String, ByVal playerDir As String)
        For Each src As String In Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
            Dim rel As String = src.Substring(sourceDir.TrimEnd("\"c).Length).TrimStart("\"c)
            If rel.StartsWith("ini\", StringComparison.OrdinalIgnoreCase) Then Continue For   ' the Controller's own mod.ini
            Dim dst As String = Path.Combine(playerDir, rel)
            Directory.CreateDirectory(Path.GetDirectoryName(dst))
            If File.Exists(dst) Then File.SetAttributes(dst, FileAttributes.Normal)
            File.Copy(src, dst, True)
        Next
    End Sub



    'Debe ser 64 bits, 8 bytes.
    Public Const sSecretKey As String = "%c0jm0ds"

    Public Sub Main()
        EncryptFile("c:\temp\test.txt", "c:\temp\Encrypted.txt", sSecretKey)
        DecryptFile("c:\temp\Encrypted.txt", "c:\temp\Decrypted.txt", sSecretKey)
    End Sub

    Sub EncryptFile(ByVal sInputFilename As String, ByVal sOutputFilename As String, ByVal sKey As String)

        Dim fsInput As New FileStream(sInputFilename, FileMode.Open, FileAccess.Read)
        Dim fsEncrypted As New FileStream(sOutputFilename, FileMode.Create, FileAccess.Write)

        Dim DES As New DESCryptoServiceProvider()

        'Establecer la clave secreta para el algoritmo DES.
        'Se necesita una clave de 64 bits y IV para este proveedor
        DES.Key = ASCIIEncoding.ASCII.GetBytes(sKey)

        'Establecer el vector de inicialización.
        DES.IV = ASCIIEncoding.ASCII.GetBytes(sKey)

        'crear cifrado DES a partir de esta instancia
        Dim desencrypt As ICryptoTransform = DES.CreateEncryptor()
        'Crear una secuencia de cifrado que transforma la secuencia
        'de archivos mediante cifrado DES
        Dim cryptostream As New CryptoStream(fsEncrypted, desencrypt, CryptoStreamMode.Write)

        'Leer el texto del archivo en la matriz de bytes
        Dim bytearrayinput(fsInput.Length - 1) As Byte
        fsInput.Read(bytearrayinput, 0, bytearrayinput.Length)
        'Escribir el archivo cifrado con DES
        cryptostream.Write(bytearrayinput, 0, bytearrayinput.Length)
        cryptostream.Close()
    End Sub

    Sub DecryptFile(ByVal sInputFilename As String, ByVal sOutputFilename As String, ByVal sKey As String)

        Dim DES As New DESCryptoServiceProvider()
        'Se requiere una clave de 64 bits y IV para este proveedor.
        'Establecer la clave secreta para el algoritmo DES.
        DES.Key() = ASCIIEncoding.ASCII.GetBytes(sKey)
        'Establecer el vector de inicialización.
        DES.IV = ASCIIEncoding.ASCII.GetBytes(sKey)

        'crear la secuencia de archivos para volver a leer el archivo cifrado
        Dim fsread As New FileStream(sInputFilename, FileMode.Open, FileAccess.Read)
        'crear descriptor DES a partir de nuestra instancia de DES
        Dim desdecrypt As ICryptoTransform = DES.CreateDecryptor()
        'crear conjunto de secuencias de cifrado para leer y realizar 
        'una transformación de descifrado DES en los bytes entrantes
        Dim cryptostreamDecr As New CryptoStream(fsread, desdecrypt, CryptoStreamMode.Read)
        'imprimir el contenido de archivo descifrado
        Dim fsDecrypted As New StreamWriter(sOutputFilename)
        fsDecrypted.Write(New StreamReader(cryptostreamDecr).ReadToEnd)
        fsDecrypted.Flush()
        fsDecrypted.Close()
    End Sub
    Sub LeeFicheroModIni()


        Dim BuscaClaseEnModIni As String = ""
        Dim ModIniLeido As String = ""

        'Comprobamos si existe una ruta en el archivo .ini y que sea correcta
        If System.IO.File.Exists(rutaAppData & "\Mods\" & modName & "\ini\" & "mod.ini") Then
            read = File.OpenText(rutaAppData & "\Mods\" & modName & "\ini\" & "mod.ini")
            ModIniLeido = read.ReadToEnd()
            ModIniLeido = ModIniLeido.Substring(0, ModIniLeido.Length - 2)
            read.Close()
        Else
            Exit Sub
        End If


        Dim FirstCharacter As Integer = 0
        For Each key As String In ClassKeys   ' nothing left over from the mod read before
            SetModIni(key, "")
        Next
        ModIniCabecera = ModIniLeido.Substring(0, 1).ToString()
        For i = 1 To 13
            If i = 1 Then
                BuscaClaseEnModIni = "Aristocrat"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniAristocrat = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 2 Then
                BuscaClaseEnModIni = "Bandit"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniBandit = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 3 Then
                BuscaClaseEnModIni = "Barnsby"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniBarnsby = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 4 Then
                BuscaClaseEnModIni = "Doc"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniDoc = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 5 Then
                BuscaClaseEnModIni = "Gunslinger"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniGunslinger = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 6 Then
                BuscaClaseEnModIni = "Gunsmith"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniGunsmith = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 7 Then
                BuscaClaseEnModIni = "Juarez"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniJuarez = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 8 Then
                BuscaClaseEnModIni = "Miner"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniMiner = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 9 Then
                BuscaClaseEnModIni = "Rifleman"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniRifleman = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 10 Then
                BuscaClaseEnModIni = "Sniper"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniSniper = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 11 Then
                BuscaClaseEnModIni = "Thief"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniThief = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 12 Then
                BuscaClaseEnModIni = "Trapper"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniTrapper = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            ElseIf i = 13 Then
                BuscaClaseEnModIni = "TribeWarrior"
                FirstCharacter = ModIniLeido.IndexOf(BuscaClaseEnModIni)
                If FirstCharacter > -1 Then ModIniTribeWarrior = ModIniLeido.Substring(FirstCharacter + Len(BuscaClaseEnModIni), 10).ToString()
            End If

        Next

        For Each extra As String In {"Rancher", "Redneck"}
            FirstCharacter = ModIniLeido.IndexOf(extra)
            If FirstCharacter > -1 Then SetModIni(extra, ModIniLeido.Substring(FirstCharacter + Len(extra), 10).ToString())
        Next

        ConfiguracionModInicial = ModIniLeido

    End Sub


    Sub EscribeFicheroModIni()

        If System.IO.Directory.Exists(rutaAppData & "\Mods\" & modName & "\ini") Then
            My.Computer.FileSystem.DeleteDirectory(rutaAppData & "\Mods\" & modName & "\ini", FileIO.DeleteDirectoryOption.DeleteAllContents)
        End If

        My.Computer.FileSystem.CreateDirectory(rutaAppData & "\Mods\" & modName & "\ini")

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


        write = File.AppendText(rutaAppData & "\Mods\" & modName & "\ini" & "\mod.ini")
        write.WriteLine(IniFinal)
        write.Close()
    End Sub


    Sub ObtenerNombreArchivo(ByVal nombreArchivo As String)

        If nombreArchivo = "Aristocrat" Then
            nombreArchivoObtenido = "_NetWeapon_Aristocrat_Properties.scr"
        ElseIf nombreArchivo = "Bandit" Then
            nombreArchivoObtenido = "_NetWeapon_Bandit_Properties.scr"
        ElseIf nombreArchivo = "Barnsby" Then
            nombreArchivoObtenido = "_NetWeapon_Barnsby_Properties.scr"
        ElseIf nombreArchivo = "Doc" Then
            nombreArchivoObtenido = "_NetWeapon_Doc_Properties.scr"
        ElseIf nombreArchivo = "Gunslinger" Then
            nombreArchivoObtenido = "_NetWeapon_Gunslinger_Properties.scr"
        ElseIf nombreArchivo = "Gunsmith" Then
            nombreArchivoObtenido = "_NetWeapon_Gunsmith_Properties.scr"
        ElseIf nombreArchivo = "Juarez" Then
            nombreArchivoObtenido = "_NetWeapon_Juarez_Properties.scr"
        ElseIf nombreArchivo = "Miner" Then
            nombreArchivoObtenido = "_NetWeapon_Miner_Properties.scr"
        ElseIf nombreArchivo = "Rifleman" Then
            nombreArchivoObtenido = "_NetWeapon_Rifleman_Properties.scr"
        ElseIf nombreArchivo = "Sniper" Then
            nombreArchivoObtenido = "_NetWeapon_Sniper_Properties.scr"
        ElseIf nombreArchivo = "Thief" Then
            nombreArchivoObtenido = "_NetWeapon_Thief_Properties.scr"
        ElseIf nombreArchivo = "Trapper" Then
            nombreArchivoObtenido = "_NetWeapon_Trapper_Properties.scr"
        ElseIf nombreArchivo = "Tribe Warrior" Then
            nombreArchivoObtenido = "_NetWeapon_TribeWarrior_Properties.scr"
        ElseIf nombreArchivo = "Rancher" Then
            nombreArchivoObtenido = "_NetWeapon_Rancher_Properties.scr"
        ElseIf nombreArchivo = "Redneck" Then
            nombreArchivoObtenido = "_NetWeapon_Redneck_Properties.scr"
        End If

    End Sub

    Sub RecargarMisMods()

        frmArmas.lbLoadMod.Items.Clear()

        For Each foundDirectory As String In My.Computer.FileSystem.GetDirectories(rutaAppData & "\mods\")

            Dim lengthRuta As Integer = foundDirectory.Length
            Dim FirstCharacter As Integer = foundDirectory.IndexOf("Mods\")
            Dim nameDirectory As String = foundDirectory.Substring(FirstCharacter + 5)


            frmArmas.lbLoadMod.Items.Add(nameDirectory)


        Next

        frmArmas.lbLoadMod.Text = frmArmas.lbLoadMod.SelectedItem

    End Sub

    Sub ObtenerNombreClase(ByVal nombreClase As String)

        If nombreClase = "_NetWeapon_Aristocrat_Properties.scr" Then
            nombreClaseObtenido = "Aristocrat"
        ElseIf nombreClase = "_NetWeapon_Bandit_Properties.scr" Then
            nombreClaseObtenido = "Bandit"
        ElseIf nombreClase = "_NetWeapon_Barnsby_Properties.scr" Then
            nombreClaseObtenido = "Barnsby"
        ElseIf nombreClase = "_NetWeapon_Doc_Properties.scr" Then
            nombreClaseObtenido = "Doc"
        ElseIf nombreClase = "_NetWeapon_Gunslinger_Properties.scr" Then
            nombreClaseObtenido = "Gunslinger"
        ElseIf nombreClase = "_NetWeapon_Gunsmith_Properties.scr" Then
            nombreClaseObtenido = "Gunsmith"
        ElseIf nombreClase = "_NetWeapon_Juarez_Properties.scr" Then
            nombreClaseObtenido = "Juarez"
        ElseIf nombreClase = "_NetWeapon_Miner_Properties.scr" Then
            nombreClaseObtenido = "Miner"
        ElseIf nombreClase = "_NetWeapon_Rifleman_Properties.scr" Then
            nombreClaseObtenido = "Rifleman"
        ElseIf nombreClase = "_NetWeapon_Sniper_Properties.scr" Then
            nombreClaseObtenido = "Sniper"
        ElseIf nombreClase = "_NetWeapon_Thief_Properties.scr" Then
            nombreClaseObtenido = "Thief"
        ElseIf nombreClase = "_NetWeapon_Trapper_Properties.scr" Then
            nombreClaseObtenido = "Trapper"
        ElseIf nombreClase = "_NetWeapon_TribeWarrior_Properties.scr" Then
            nombreClaseObtenido = "Tribe Warrior"
        ElseIf nombreClase = "_NetWeapon_Rancher_Properties.scr" Then
            nombreClaseObtenido = "Rancher"
        ElseIf nombreClase = "_NetWeapon_Redneck_Properties.scr" Then
            nombreClaseObtenido = "Redneck"
        End If

    End Sub

    Sub OcultarCarpeta(ByVal carpetaName As String)

        If My.Computer.FileSystem.DirectoryExists(ruta & rutaTemp) Then
            Dim checkFile As System.IO.DirectoryInfo
            checkFile = My.Computer.FileSystem.GetDirectoryInfo(carpetaName)
            ' Dim attributeReader As System.IO.FileAttributes
            ' attributeReader = checkFile.Attributes
            checkFile.Attributes = FileAttributes.Hidden
        End If


    End Sub



End Module
