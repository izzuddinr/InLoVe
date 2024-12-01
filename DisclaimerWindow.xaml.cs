using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using static Qatalyst.NativeApi;

namespace Qatalyst;

public static class NativeApi
{
    //Platform invoke definition for DwmSetWindowAttribute
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    // Define GetDpiForWindow
    [DllImport("user32.dll")]
    public static extern int GetDpiForWindow(IntPtr hwnd);
}

internal static class User32
{
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    public const int SM_CXSCREEN = 0; // Screen width
    public const int SM_CYSCREEN = 1; // Screen height
}


public sealed partial class DisclaimerWindow : Window
{
    public DisclaimerWindow()
    {
        InitializeComponent();

        var presenter = AppWindow.Presenter as OverlappedPresenter;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(true, false);


        SetWindowSize(800, 480);
        CenterWindowOnScreen();

        //This Win32Interop is in the Microsoft.UI namespace.
        var windowHandle = Win32Interop.GetWindowFromWindowId(AppWindow.Id);

        //DWM_WINDOW_CORNER_PREFERENCE documentation gives DWMWCP_ROUND as 2
        var cornerPreference = 2;
        //DWMWA_WINDOW_CORNER_PREFERENCE is documented to have a value of 33
        DwmSetWindowAttribute(windowHandle, 33, ref cornerPreference, Marshal.SizeOf<int>());
    }

    private void SetWindowSize(int width, int height)
    {
        // Get the native window handle
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);

        // Get the DPI for the window
        var dpi = NativeApi.GetDpiForWindow(hwnd);

        // Convert width and height from DIPs (Device Independent Pixels) to physical pixels
        var scale = dpi / 96.0;
        var physicalWidth = (int)(width * scale);
        var physicalHeight = (int)(height * scale);

        // Resize the window
        AppWindow.Resize(new SizeInt32(physicalWidth, physicalHeight));
    }

    private void CenterWindowOnScreen()
    {
        // Get the native window handle
        var hwnd = WindowNative.GetWindowHandle(this);

        // Screen size in physical pixels
        var screenWidth = User32.GetSystemMetrics(User32.SM_CXSCREEN);
        var screenHeight = User32.GetSystemMetrics(User32.SM_CYSCREEN);

        // Window size in physical pixels
        var windowWidth = AppWindow.Size.Width;
        var windowHeight = AppWindow.Size.Height;

        // Calculate the center position
        var left = (screenWidth - windowWidth) / 2;
        var top = (screenHeight - windowHeight) / 2;

        // Move the window to the center of the screen
        AppWindow.Move(new PointInt32(left, top));
    }

    private void GoodButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BadButton_OnClick(object sender, RoutedEventArgs e)
    {
        Environment.Exit(0);
    }
}