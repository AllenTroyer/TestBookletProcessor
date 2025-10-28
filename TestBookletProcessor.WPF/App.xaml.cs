using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace TestBookletProcessor.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("shell32.dll")]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        protected override void OnStartup(StartupEventArgs e)
        {
            // Set a unique AppUserModelID for toast notifications (proper format)
            SetCurrentProcessExplicitAppUserModelID("Catforms.TestBookletProcessor.WPF");
            base.OnStartup(e);
        }
    }
}
