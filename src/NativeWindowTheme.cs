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

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string subAppName, string subIdList);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);

    public static void Apply(Form form)
    {
        if (form == null) return;
        form.HandleCreated += delegate { Apply(form.Handle); };
        if (form.IsHandleCreated) Apply(form.Handle);
    }

    public static void Apply(Control control)
    {
        if (control == null) return;
        control.HandleCreated += delegate { ApplyControl(control.Handle); };
        if (control.IsHandleCreated) ApplyControl(control.Handle);
    }

    public static void ApplyTree(Control control)
    {
        if (control == null) return;
        Apply(control);
        foreach (Control child in control.Controls) ApplyTree(child);
        control.ControlAdded += delegate(object sender, ControlEventArgs e) { ApplyTree(e.Control); };
    }

    public static void SetCueBanner(TextBox textBox, string text)
    {
        if (textBox == null) return;
        textBox.HandleCreated += delegate { SendMessage(textBox.Handle, 0x1501, new IntPtr(1), text ?? string.Empty); };
        if (textBox.IsHandleCreated) SendMessage(textBox.Handle, 0x1501, new IntPtr(1), text ?? string.Empty);
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

    private static void ApplyControl(IntPtr handle)
    {
        if (handle == IntPtr.Zero || Environment.OSVersion.Version.Major < 10) return;
        try { SetWindowTheme(handle, "DarkMode_Explorer", null); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private static int ColorRef(int red, int green, int blue)
    {
        return red | (green << 8) | (blue << 16);
    }
}
