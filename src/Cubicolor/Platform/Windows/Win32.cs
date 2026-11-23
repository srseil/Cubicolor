using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Cubicolor.Platform.Windows;

// The parts of user32, kernel32 and shell32 the game uses, and the process-wide setup a windowed run needs.
internal static unsafe partial class Win32
{
    public const uint WM_DESTROY = 0x0002, WM_SIZE = 0x0005, WM_KILLFOCUS = 0x0008, WM_CLOSE = 0x0010, WM_QUIT = 0x0012,
        WM_ERASEBKGND = 0x0014, WM_GETMINMAXINFO = 0x0024, WM_SETICON = 0x0080, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101,
        WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105, WM_SYSCOMMAND = 0x0112, WM_MENUCHAR = 0x0120,
        WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205,
        WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208, WM_MOUSEWHEEL = 0x020A, WM_DPICHANGED = 0x02E0;

    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000, WS_POPUP = 0x80000000, WS_VISIBLE = 0x10000000;
    public const int GWL_STYLE = -16;
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20,
        SWP_NOOWNERZORDER = 0x200;

    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct WindowClass
    {
        public uint Size, Style;
        public delegate* unmanaged[Stdcall]<IntPtr, uint, nuint, nint, nint> WindowProc;
        public int ClassExtra, WindowExtra;
        public IntPtr Instance, Icon, Cursor, Background;
        public char* MenuName, ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Message
    {
        public IntPtr Window;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor, Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPlacement
    {
        public uint Length, Flags, ShowCommand;
        public Point MinPosition, MaxPosition;
        public Rect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }

    private const string User32 = "user32.dll", Kernel32 = "kernel32.dll";

    [LibraryImport(User32)] public static partial ushort RegisterClassExW(WindowClass* windowClass);
    [LibraryImport(User32)] public static partial IntPtr CreateWindowExW(uint exStyle, char* className, char* title, uint style, int x, int y,
        int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [LibraryImport(User32)] public static partial nint DefWindowProcW(IntPtr window, uint message, nuint wParam, nint lParam);
    [LibraryImport(User32)] public static partial int DestroyWindow(IntPtr window);
    [LibraryImport(User32)] public static partial int ShowWindow(IntPtr window, int command);
    [LibraryImport(User32)] public static partial int SetForegroundWindow(IntPtr window);
    [LibraryImport(User32)] public static partial IntPtr GetForegroundWindow();
    [LibraryImport(User32)] public static partial int PeekMessageW(Message* message, IntPtr window, uint min, uint max, uint remove);
    [LibraryImport(User32)] public static partial nint DispatchMessageW(Message* message);
    [LibraryImport(User32)] public static partial uint MsgWaitForMultipleObjectsEx(uint count, IntPtr* handles, uint milliseconds, uint wakeMask, uint flags);
    [LibraryImport(User32)] public static partial int GetClientRect(IntPtr window, Rect* rect);
    [LibraryImport(User32)] public static partial int AdjustWindowRectExForDpi(Rect* rect, uint style, int menu, uint exStyle, uint dpi);
    [LibraryImport(User32)] public static partial uint GetDpiForWindow(IntPtr window);
    [LibraryImport(User32)] public static partial int SetProcessDpiAwarenessContext(IntPtr context);
    [LibraryImport(User32)] public static partial int SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [LibraryImport(User32)] public static partial nint GetWindowLongPtrW(IntPtr window, int index);
    [LibraryImport(User32)] public static partial nint SetWindowLongPtrW(IntPtr window, int index, nint value);
    [LibraryImport(User32)] public static partial int GetWindowPlacement(IntPtr window, WindowPlacement* placement);
    [LibraryImport(User32)] public static partial int SetWindowPlacement(IntPtr window, WindowPlacement* placement);
    [LibraryImport(User32)] public static partial IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [LibraryImport(User32)] public static partial int GetMonitorInfoW(IntPtr monitor, MonitorInfo* info);
    [LibraryImport(User32)] public static partial int IsIconic(IntPtr window);
    [LibraryImport(User32)] public static partial int GetCursorPos(Point* point);
    [LibraryImport(User32)] public static partial int ScreenToClient(IntPtr window, Point* point);
    [LibraryImport(User32)] public static partial IntPtr SetCapture(IntPtr window);
    [LibraryImport(User32)] public static partial int ReleaseCapture();
    [LibraryImport(User32)] public static partial IntPtr LoadCursorW(IntPtr instance, nint name);
    [LibraryImport(User32)] public static partial nint SendMessageW(IntPtr window, uint message, nuint wParam, nint lParam);
    [LibraryImport(User32)] public static partial int GetSystemMetricsForDpi(int index, uint dpi);
    [LibraryImport(User32)] public static partial IntPtr CreateIconFromResourceEx(byte* data, uint size, int icon, uint version, int width, int height, uint flags);
    [LibraryImport(User32)] public static partial int DestroyIcon(IntPtr icon);
    [LibraryImport(User32)] public static partial uint MapVirtualKeyExW(uint code, uint mapType, IntPtr layout);
    [LibraryImport(User32)] public static partial IntPtr GetKeyboardLayout(uint thread);

    [LibraryImport(Kernel32)] public static partial IntPtr GetModuleHandleW(char* name);
    [LibraryImport(Kernel32)] public static partial int GetUserPreferredUILanguages(uint flags, uint* count, char* buffer, uint* length);
    [LibraryImport(Kernel32)] public static partial int AttachConsole(uint process);
    [LibraryImport(Kernel32)] public static partial IntPtr GetStdHandle(int which);

    [LibraryImport("shell32.dll")] private static partial IntPtr ShellExecuteW(IntPtr window, char* operation, char* file, char* parameters, char* directory, int show);
    [LibraryImport("winmm.dll")] private static partial uint timeBeginPeriod(uint milliseconds);

    // Pixels, not a scaled bitmap, on every monitor whatever its scale, and messages when a window moves to
    // one with another (WM_DPICHANGED). Must come before the first window.
    public static void StartWindowed()
    {
        SetProcessDpiAwarenessContext(-4);   // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        // Sleep(1) and timed waits otherwise round up to the 15.6 ms system tick, a whole frame.
        timeBeginPeriod(1);
    }

    // A program built for the Windows subsystem has no console, so a run from a terminal would print nothing:
    // it writes to the terminal it was started from, unless its output is already going somewhere (a file, a
    // pipe), which it then keeps.
    public static void AttachParentConsole()
    {
        if (GetStdHandle(-12 /* STD_ERROR_HANDLE */) != IntPtr.Zero) return;
        if (AttachConsole(unchecked((uint) -1) /* ATTACH_PARENT_PROCESS */) == 0) return;
        var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        var stderr = new System.IO.StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
        Console.SetOut(stdout);
        Console.SetError(stderr);
    }

    // The languages the user chose in Windows' settings, most preferred first, as BCP 47 tags ("de-DE").
    public static string[] PreferredLanguages()
    {
        uint count = 0, length = 0;
        if (GetUserPreferredUILanguages(0x8 /* MUI_LANGUAGE_NAME */, &count, null, &length) == 0 || length == 0) return [];
        char* buffer = stackalloc char[(int) length];
        if (GetUserPreferredUILanguages(0x8, &count, buffer, &length) == 0) return [];
        var tags = new List<string>();
        // A list of strings each ending in a zero, the whole ending in another.
        for (int start = 0, i = 0; i < length; i++)
        {
            if (buffer[i] != 0) continue;
            if (i > start) tags.Add(new string(buffer, start, i - start));
            start = i + 1;
        }
        return [.. tags];
    }

    // A web page in the default browser.
    public static void OpenUrl(string url)
    {
        fixed (char* open = "open", file = url)
            ShellExecuteW(IntPtr.Zero, open, file, null, null, 1 /* SW_SHOWNORMAL */);
    }
}
