' Application-level events. The Startup handler runs before the main form is
' created, so it is the right place to provision the loose asset folders needed
' by the single-exe build (see AssetsBootstrap).

Namespace My

    Partial Friend Class MyApplication

        Private Sub MyApplication_Startup(ByVal sender As Object, ByVal e As Microsoft.VisualBasic.ApplicationServices.StartupEventArgs) Handles Me.Startup
            AssetsBootstrap.EnsureAssets()
        End Sub

    End Class

End Namespace
