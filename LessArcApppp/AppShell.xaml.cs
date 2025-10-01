// AppShell_and_App_NoTitleBar.cs
// Tek dosyada: AppShell (NavBar kapalı) + App (Windows'ta tamamen titlesız pencere)

using System;
using System.Linq;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics; // 🎯 MAUI renkler

#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.Maui.Platform;
using Microsoft.UI.Windowing;
// using Microsoft.UI;  // ❌ KALDIRILDI: Colors çakışması oluyordu
using WinRT.Interop;
#endif

namespace LessArcApppp
{
    // =======================
    // 1) SHELL: NavBar kapalı
    // =======================
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Üst bar renkleri (görünse bile)
            Shell.SetBackgroundColor(this, Color.FromArgb("#686660"));
            Shell.SetTitleColor(this, Colors.White);       // MAUI Colors
            Shell.SetForegroundColor(this, Colors.White);  // MAUI Colors

            // Varsayılan Shell NavBar tamamen gizli
        }
    }

    // =========================================
    // 2) APP: Windows'ta OS Title Bar'ı sökme
    // =========================================
    // Not: partial -> mevcut App.xaml.cs ile birlikte sorunsuz çalışır.
    public partial class App : Application
    {
        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = base.CreateWindow(activationState);

#if WINDOWS
            window.Created += (_, __) =>
            {
                var win = window?.Handler?.PlatformView as MauiWinUIWindow;
                if (win is null) return;

                // 1) Win32 handle
                var hwnd = WindowNative.GetWindowHandle(win);

                // 2) Mevcut stil
                var style = GetWindowLongPtr(hwnd, GWL_STYLE);

                // 3) Başlık + kenarlık + sistem menüsü + min/max KAPAT
                //    (sağ üstteki — ▢ ✕ tamamen yok olur)
                style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
                SetWindowLongPtr(hwnd, GWL_STYLE, style);

                // 4) Çerçeve değişikliğini uygula
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

                // İçeriği başlık alanına uzat (drag rect'i sen belirleyeceksin)
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            };
#endif

            return window;
        }

#if WINDOWS
        // ---- Win32 P/Invoke ve sabitler ----
        const int GWL_STYLE = -16;

        static readonly nint WS_CAPTION = (nint)0x00C00000; // başlık
        static readonly nint WS_THICKFRAME = (nint)0x00040000; // kenarlık (resize)
        static readonly nint WS_SYSMENU = (nint)0x00080000; // sistem menüsü
        static readonly nint WS_MINIMIZEBOX = (nint)0x00020000;
        static readonly nint WS_MAXIMIZEBOX = (nint)0x00010000;

        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
#endif
    }
}
