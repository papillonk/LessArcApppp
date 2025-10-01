using System.Linq;
using System.Net.Http;
using Microsoft.Maui.Controls;

#if WINDOWS
using Microsoft.Maui.Platform;       // MauiWinUIWindow
using Microsoft.UI.Windowing;        // AppWindow, DisplayArea, AppWindowChangedEventArgs
using Windows.Graphics;              // SizeInt32, PointInt32
using WinColor = Windows.UI.Color;   // Windows renk tipi alias
#endif

namespace LessArcApppp
{
    public partial class App : Application
    {
        public static int KullaniciId { get; set; }
        private readonly HttpClient _http; // DI'dan gelen tekil HttpClient

        public App(HttpClient http)
        {
            InitializeComponent();
            _http = http;

            // Uygulama açılır açılmaz her zaman Giriş ekranı
            MainPage = new NavigationPage(new MainPage(_http));

#if WINDOWS
            Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
            {
                TryResizeAndCenterToPercent(0.80);
                ConfigureWindowsTitleBar();

                var mw = Microsoft.Maui.Controls.Application.Current?.Windows?
                           .FirstOrDefault()?.Handler?.PlatformView as MauiWinUIWindow;
                var appWindow = mw?.AppWindow;
                if (appWindow != null)
                {
                    appWindow.Changed -= AppWindow_Changed;
                    appWindow.Changed += AppWindow_Changed;
                }
            });
#endif
        }

        // Eski akışta OnStart/OnResume’a gerek yok; kaldırıldı

#if WINDOWS
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            TryResizeAndCenterToPercent(0.80);
            ConfigureWindowsTitleBar();
        }

        // 🔸 Tüm sayfalar için tek noktadan başlık ayarı
        private static void ConfigureWindowsTitleBar()
        {
            var mw = Microsoft.Maui.Controls.Application.Current?.Windows?
                        .FirstOrDefault()?.Handler?.PlatformView as MauiWinUIWindow;
            var appWindow = mw?.AppWindow;
            if (appWindow is null) return;

            var tb = appWindow.TitleBar;

            tb.ExtendsContentIntoTitleBar = true;

            // Şeffaf arkaplan
            var transparent = WinColor.FromArgb(0, 0, 0, 0);
            tb.BackgroundColor         = transparent;
            tb.InactiveBackgroundColor = transparent;

            // Caption butonları
            tb.ButtonBackgroundColor         = transparent;
            tb.ButtonInactiveBackgroundColor = transparent;

            var white = WinColor.FromArgb(255, 255, 255, 255);
            tb.ButtonForegroundColor         = white;
            tb.ButtonInactiveForegroundColor = white;

            tb.ButtonHoverBackgroundColor    = WinColor.FromArgb(30, 255, 255, 255);
            tb.ButtonPressedBackgroundColor  = WinColor.FromArgb(48, 255, 255, 255);

            // Not: TitleView/CustomTitleBar'da WinUI için sağ padding bırak:
            // Padding="{OnPlatform WinUI='16,8,140,8', Default='16,8'}"
        }

        private static void TryResizeAndCenterToPercent(double percent)
        {
            percent = Math.Clamp(percent, 0.2, 1.0);

            var mw = Microsoft.Maui.Controls.Application.Current?.Windows?
                        .FirstOrDefault()?.Handler?.PlatformView as MauiWinUIWindow;
            var appWindow = mw?.AppWindow;
            if (appWindow is null) return;

            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            var work = displayArea.WorkArea;

            int targetW = (int)(work.Width * percent);
            int targetH = (int)(work.Height * percent);
            if (targetW <= 0 || targetH <= 0) return;

            appWindow.Resize(new SizeInt32(targetW, targetH));

            int x = work.X + (work.Width - targetW) / 2;
            int y = work.Y + (work.Height - targetH) / 2;
            appWindow.Move(new PointInt32(x, y));
        }
#endif
    }
}
