using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class NativeWindowTheme
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeLegacy = 19;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public static void Apply(Form form)
    {
        if (form == null) return;
        form.HandleCreated += delegate { Apply(form.Handle); };
        if (form.IsHandleCreated) Apply(form.Handle);
    }

    private static void Apply(IntPtr handle)
    {
        if (handle == IntPtr.Zero || Environment.OSVersion.Version.Major < 10) return;
        try
        {
            int enabled = 1;
            if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, UseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
            int caption = ColorRef(10, 14, 20);
            int text = ColorRef(241, 245, 249);
            int border = ColorRef(41, 51, 65);
            DwmSetWindowAttribute(handle, CaptionColor, ref caption, sizeof(int));
            DwmSetWindowAttribute(handle, TextColor, ref text, sizeof(int));
            DwmSetWindowAttribute(handle, BorderColor, ref border, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private static int ColorRef(int red, int green, int blue)
    {
        return red | (green << 8) | (blue << 16);
    }
}
