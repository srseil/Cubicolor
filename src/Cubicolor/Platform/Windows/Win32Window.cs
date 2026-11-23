using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cubicolor.Utilities;
using static Cubicolor.Platform.Windows.Win32;

namespace Cubicolor.Platform.Windows;

// The game's window: a plain Win32 window the swap chain draws into, resizable, and fullscreen as a borderless
// window over the whole monitor (Alt-Tab and a second monitor keep working, and nothing changes the display
// mode). The process is per-monitor DPI aware: the client area is in pixels, and Width and Height are those
// divided by the monitor's scale, the points the UI thinks in.
internal sealed unsafe class Win32Window : IWindow
{
    public IntPtr Handle { get; }

    // The client area in pixels, which is what the renderer draws.
    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }

    public int Width => Math.Max(1, (int) MathF.Round(PixelWidth / PixelDensity));
    public int Height => Math.Max(1, (int) MathF.Round(PixelHeight / PixelDensity));
    public float PixelDensity => GetDpiForWindow(Handle) / 96f;
    public bool CloseRequested { get; private set; }
    public bool IsFocused => GetForegroundWindow() == Handle;
    public bool IsVisible => IsIconic(Handle) == 0;
    public bool IsFullscreen { get; private set; }

    // Sees every keyboard and mouse message the window gets (WindowsInput); set once, after construction.
    public Action<uint, nuint, nint>? Observe { get; set; }

    private static Win32Window? _current;   // the one window, for the window procedure
    private WindowPlacement _windowed;
    private IntPtr _bigIcon, _smallIcon;

    private const int MinWidth = 480, MinHeight = 360;

    public Win32Window(int width, int height, string title, string? iconPath)
    {
        _current = this;
        IntPtr instance = GetModuleHandleW(null);
        fixed (char* className = "Cubicolor", text = title)
        {
            var windowClass = new WindowClass
            {
                Size = (uint) sizeof(WindowClass),
                Style = 0x3,   // CS_HREDRAW | CS_VREDRAW
                WindowProc = &WindowProc,
                Instance = instance,
                Cursor = LoadCursorW(IntPtr.Zero, 32512 /* IDC_ARROW */),
                ClassName = className,
            };
            if (RegisterClassExW(&windowClass) == 0) throw new InvalidOperationException($"RegisterClassEx failed ({Marshal.GetLastPInvokeError()})");
            Handle = CreateWindowExW(0, className, text, WS_OVERLAPPEDWINDOW, unchecked((int) 0x80000000), 0, 640, 480,
                IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        }
        if (Handle == IntPtr.Zero) throw new InvalidOperationException("CreateWindowEx failed");
        if (iconPath != null) SetIcon(iconPath);

        // Sized only now that the window knows its monitor's scale: `width` × `height` points of client area,
        // no more than the monitor's work area, centred on it.
        uint dpi = GetDpiForWindow(Handle);
        var frame = new Rect { Right = width * (int) dpi / 96, Bottom = height * (int) dpi / 96 };
        AdjustWindowRectExForDpi(&frame, WS_OVERLAPPEDWINDOW, 0, 0, dpi);
        var monitor = new MonitorInfo { Size = (uint) sizeof(MonitorInfo) };
        GetMonitorInfoW(MonitorFromWindow(Handle, 2 /* MONITOR_DEFAULTTONEAREST */), &monitor);
        Rect work = monitor.Work;
        int w = Math.Min(frame.Right - frame.Left, work.Right - work.Left);
        int h = Math.Min(frame.Bottom - frame.Top, work.Bottom - work.Top);
        SetWindowPos(Handle, IntPtr.Zero, work.Left + (work.Right - work.Left - w) / 2, work.Top + (work.Bottom - work.Top - h) / 2, w, h,
            SWP_NOZORDER | SWP_NOACTIVATE);
        ShowWindow(Handle, 5 /* SW_SHOW */);
        SetForegroundWindow(Handle);
        RefreshSize();
        Log.Info($"Window created: {Width}x{Height} points, {PixelWidth}x{PixelHeight} pixels");
    }

    // The window's icon, from the game's own PNG (Windows reads PNG icons since Vista): the title bar's and
    // Alt-Tab's. The taskbar shows the executable's icon once the game is packaged.
    private void SetIcon(string path)
    {
        if (!File.Exists(path)) return;
        byte[] png = File.ReadAllBytes(path);
        uint dpi = GetDpiForWindow(Handle);
        fixed (byte* data = png)
        {
            _bigIcon = CreateIconFromResourceEx(data, (uint) png.Length, 1, 0x00030000, GetSystemMetricsForDpi(11 /* SM_CXICON */, dpi),
                GetSystemMetricsForDpi(12, dpi), 0);
            _smallIcon = CreateIconFromResourceEx(data, (uint) png.Length, 1, 0x00030000, GetSystemMetricsForDpi(49 /* SM_CXSMICON */, dpi),
                GetSystemMetricsForDpi(50, dpi), 0);
        }
        if (_bigIcon != IntPtr.Zero) SendMessageW(Handle, WM_SETICON, 1 /* ICON_BIG */, _bigIcon);
        if (_smallIcon != IntPtr.Zero) SendMessageW(Handle, WM_SETICON, 0 /* ICON_SMALL */, _smallIcon);
    }

    // Every message waiting, each to the window procedure. Called once a frame, by the input.
    public void Pump()
    {
        Message message;
        while (PeekMessageW(&message, IntPtr.Zero, 0, 0, 1 /* PM_REMOVE */) != 0)
        {
            if (message.Id == WM_QUIT) CloseRequested = true;
            DispatchMessageW(&message);
        }
        RefreshSize();
    }

    // Returns when a message arrives or after `seconds`, whichever is first.
    public void WaitForEvent(double seconds) =>
        MsgWaitForMultipleObjectsEx(0, null, (uint) Math.Max(0, seconds * 1000), 0x04FF /* QS_ALLINPUT */, 0x4 /* MWMO_INPUTAVAILABLE */);

    private void RefreshSize()
    {
        Rect client;
        GetClientRect(Handle, &client);
        PixelWidth = Math.Max(1, client.Right - client.Left);
        PixelHeight = Math.Max(1, client.Bottom - client.Top);
    }

    // The pointer over the client area, in pixels from its top left (it may lie outside).
    public (float X, float Y) MouseLocation()
    {
        Point point;
        GetCursorPos(&point);
        ScreenToClient(Handle, &point);
        return (point.X, point.Y);
    }

    public void SetFullscreen(bool fullscreen)
    {
        if (fullscreen == IsFullscreen) return;
        IsFullscreen = fullscreen;
        if (fullscreen)
        {
            _windowed = new WindowPlacement { Length = (uint) sizeof(WindowPlacement) };
            fixed (WindowPlacement* placement = &_windowed) GetWindowPlacement(Handle, placement);
            var monitor = new MonitorInfo { Size = (uint) sizeof(MonitorInfo) };
            GetMonitorInfoW(MonitorFromWindow(Handle, 2), &monitor);
            Rect m = monitor.Monitor;
            SetWindowLongPtrW(Handle, GWL_STYLE, unchecked((nint) (WS_POPUP | WS_VISIBLE)));
            SetWindowPos(Handle, IntPtr.Zero, m.Left, m.Top, m.Right - m.Left, m.Bottom - m.Top,
                SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_FRAMECHANGED);
        }
        else
        {
            SetWindowLongPtrW(Handle, GWL_STYLE, (nint) (WS_OVERLAPPEDWINDOW | WS_VISIBLE));
            fixed (WindowPlacement* placement = &_windowed) SetWindowPlacement(Handle, placement);
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_FRAMECHANGED);
        }
        RefreshSize();
    }

    public void Dispose()
    {
        DestroyWindow(Handle);
        if (_bigIcon != IntPtr.Zero) DestroyIcon(_bigIcon);
        if (_smallIcon != IntPtr.Zero) DestroyIcon(_smallIcon);
        _current = null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WindowProc(IntPtr hwnd, uint message, nuint wParam, nint lParam)
    {
        Win32Window? window = _current;
        if (window == null || window.Handle != hwnd && window.Handle != IntPtr.Zero) return DefWindowProcW(hwnd, message, wParam, lParam);
        try
        {
            return window.OnMessage(message, wParam, lParam) ?? DefWindowProcW(hwnd, message, wParam, lParam);
        }
        catch (Exception e)
        {
            // An exception must not unwind through user32.
            Log.Error($"Window message 0x{message:X4} failed: {e}");
            return 0;
        }
    }

    // A message's result, or null to leave it to DefWindowProc.
    private nint? OnMessage(uint message, nuint wParam, nint lParam)
    {
        switch (message)
        {
            case WM_CLOSE:
                // The loop ends the run and disposes the window.
                CloseRequested = true;
                return 0;
            case WM_ERASEBKGND:
                return 1;   // the swap chain covers it all
            case WM_GETMINMAXINFO:
            {
                if (Handle == IntPtr.Zero) return null;
                uint dpi = GetDpiForWindow(Handle);
                var frame = new Rect { Right = MinWidth * (int) dpi / 96, Bottom = MinHeight * (int) dpi / 96 };
                AdjustWindowRectExForDpi(&frame, WS_OVERLAPPEDWINDOW, 0, 0, dpi);
                var info = (MinMaxInfo*) lParam;
                info->MinTrackSize = new Point { X = frame.Right - frame.Left, Y = frame.Bottom - frame.Top };
                return 0;
            }
            case WM_DPICHANGED:
            {
                // Onto a monitor with another scale: the size Windows suggests keeps the same points.
                if (IsFullscreen) return 0;
                var suggested = (Rect*) lParam;
                SetWindowPos(Handle, IntPtr.Zero, suggested->Left, suggested->Top, suggested->Right - suggested->Left,
                    suggested->Bottom - suggested->Top, SWP_NOZORDER | SWP_NOACTIVATE);
                return 0;
            }
            case WM_SYSCOMMAND when (wParam & 0xFFF0) == 0xF100:   // SC_KEYMENU: Alt or F10 alone would open the (absent) menu and stall the loop
                return 0;
            case WM_MENUCHAR:
                return 1 << 16;   // MNC_CLOSE: no beep for Alt with a letter
            case WM_KEYDOWN or WM_KEYUP or WM_KILLFOCUS:
                Observe?.Invoke(message, wParam, lParam);
                return 0;
            case WM_SYSKEYDOWN or WM_SYSKEYUP:
                // Seen as keys, and left to DefWindowProc too, which closes the window on Alt-F4.
                Observe?.Invoke(message, wParam, lParam);
                return null;
            case WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN:
                // Captured, so a release outside the window is still seen.
                SetCapture(Handle);
                Observe?.Invoke(message, wParam, lParam);
                return 0;
            case WM_LBUTTONUP or WM_RBUTTONUP or WM_MBUTTONUP:
                if ((wParam & 0x13) == 0) ReleaseCapture();   // no MK_LBUTTON, MK_RBUTTON or MK_MBUTTON left
                Observe?.Invoke(message, wParam, lParam);
                return 0;
            case WM_MOUSEWHEEL:
                Observe?.Invoke(message, wParam, lParam);
                return 0;
            default:
                return null;
        }
    }
}
