using System.Runtime.InteropServices;

public static class AppUserModelHelper
{
    [DllImport("shell32.dll")]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appID);

    public static void SetAppUserModelID(string appID)
    {
        SetCurrentProcessExplicitAppUserModelID(appID);
    }
}