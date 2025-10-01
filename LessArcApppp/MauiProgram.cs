using Microcharts.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using System.Net.Http;

#if WINDOWS
using Microsoft.Maui.Platform;           // MauiWinUIWindow
using Microsoft.UI.Windowing;            // AppWindow, OverlappedPresenter, AppWindowTitleBar, DisplayArea
using WinRT.Interop;                     // WindowNative
using WinColor = Windows.UI.Color;
#endif

namespace LessArcApppp
{
    public static class MauiProgram
    {
        public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
        {
            var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseMicrocharts()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("NotoColorEmoji.ttf", "EmojiFont");
                    fonts.AddFont("ArchitectsDaughter.ttf", "ArchitectFont");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // ---- HTTP Client (DI) ----
            const string BaseUrl = "https://lessarc.com.tr";
            builder.Services.AddSingleton(sp => new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            });

            builder.Services.AddSingleton<MainPage>();

            builder.ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(w =>
                {
                    w.OnWindowCreated(win =>
                    {
                        // 1) TAMAMEN NATIVE TITLE BAR
                        win.ExtendsContentIntoTitleBar = false;  // içerik başlığa uzamasın (custom bar YOK)

                        if (win.AppWindow?.Presenter is OverlappedPresenter p)
                        {
                            // Sistem çerçevesi + sistem title bar AÇIK
                            p.SetBorderAndTitleBar(true, true);
                            p.IsResizable   = true;   // tek boyut istersen false yap
                            p.IsMaximizable = true;   // maximize kapatmak istersen false yap
                            p.IsMinimizable = true;
                        }

                        // 2) TITLE BAR RENK/TEMA (opsiyonel ama güzel durur)
                        var mw = win as MauiWinUIWindow;
                        var appWindow = mw?.AppWindow;
                        if (appWindow is null) return;

                        if (AppWindowTitleBar.IsCustomizationSupported())
                        {
                            var tb = appWindow.TitleBar;

                            // Senin paletin:
                            var bg     = WinColor.FromArgb(255, 0x1C, 0x1C, 0x1C); // #1C1C1C
                            var fg     = WinColor.FromArgb(255, 255, 255, 255);    // #FFFFFF
                            var hover  = WinColor.FromArgb(255, 60, 60, 60);
                            var press  = WinColor.FromArgb(255, 45, 45, 45);

                            tb.ExtendsContentIntoTitleBar   = false;  // native bar
                            tb.BackgroundColor              = bg;
                            tb.InactiveBackgroundColor      = bg;

                            tb.ForegroundColor              = fg;
                            tb.InactiveForegroundColor      = fg;

                            tb.ButtonBackgroundColor        = bg;
                            tb.ButtonInactiveBackgroundColor= bg;
                            tb.ButtonForegroundColor        = fg;
                            tb.ButtonInactiveForegroundColor= fg;

                            tb.ButtonHoverBackgroundColor   = hover;
                            tb.ButtonPressedBackgroundColor = press;
                        }

                        // (OPSİYONEL) AÇILIŞTA MERKEZE AL & BOYUT VER
                        // var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
                        // int w0 = 1280, h0 = 800;
                        // appWindow.Resize(new Windows.Graphics.SizeInt32(w0, h0));
                        // appWindow.Move(new Windows.Graphics.PointInt32(area.X + (area.Width - w0)/2, area.Y + (area.Height - h0)/2));
                    });
                });
#endif
            });

            return builder.Build();
        }
    }
}
