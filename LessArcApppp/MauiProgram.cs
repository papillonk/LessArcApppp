using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Handlers.Items;   // CollectionViewHandler
using Microsoft.Maui.Handlers;                   // EntryHandler, EditorHandler, PickerHandler, ButtonHandler
using Microcharts.Maui;
using Microsoft.Maui.LifecycleEvents;

#if WINDOWS
using Microsoft.Maui.Platform;           // MauiWinUIWindow
using Microsoft.UI.Windowing;            // AppWindow, OverlappedPresenter, AppWindowTitleBar
using WinRT.Interop;                     // WindowNative
using WinColor = Windows.UI.Color;
#endif

#if IOS
using UIKit;
using CoreGraphics;
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

            // ---- iOS görünüm düzeltmeleri (Android/Windows etkilenmez) ----
            builder.ConfigureMauiHandlers(handlers =>
            {
#if IOS
                // ENTRY
                EntryHandler.Mapper.AppendToMapping("iOSColors", (handler, view) =>
                {
                    if (handler.PlatformView is UITextField tf)
                    {
                        tf.BackgroundColor = UIColor.Clear;
                        tf.BorderStyle = UITextBorderStyle.None;
                        tf.TintColor = UIColor.FromRGB(0x2d, 0x2e, 0x32);     // cursor
                        tf.TextColor = UIColor.FromRGB(0x2d, 0x2e, 0x32);
                        tf.AttributedPlaceholder = new Foundation.NSAttributedString(
                            view.Placeholder ?? string.Empty,
                            foregroundColor: UIColor.FromRGB(0x2d, 0x2e, 0x32).ColorWithAlpha(0.65f)
                        );
                    }
                });

                // EDITOR
                EditorHandler.Mapper.AppendToMapping("iOSColors", (handler, view) =>
                {
                    handler.PlatformView.BackgroundColor = UIColor.Clear;
                    handler.PlatformView.TintColor = UIColor.FromRGB(0x2d, 0x2e, 0x32);
                    handler.PlatformView.TextColor = UIColor.FromRGB(0x2d, 0x2e, 0x32);
                });

                // PICKER
                PickerHandler.Mapper.AppendToMapping("iOSColors", (handler, view) =>
                {
                    handler.PlatformView.BackgroundColor = UIColor.Clear;
                    handler.PlatformView.TintColor = UIColor.FromRGB(0x2d, 0x2e, 0x32);
                    handler.PlatformView.TextColor = UIColor.FromRGB(0x2d, 0x2e, 0x32);
                });

                // BUTTON (native parlamayı azalt)
                ButtonHandler.Mapper.AppendToMapping("iOSColors", (handler, view) =>
                {
                    handler.PlatformView.BackgroundColor = UIColor.Clear;
                    handler.PlatformView.TintColor = UIColor.FromRGB(0x2d, 0x2e, 0x32);
                });

                // COLLECTIONVIEW arkaplanı şeffaf
                CollectionViewHandler.Mapper.AppendToMapping("iOSBg", (handler, view) =>
                {
                    handler.PlatformView.BackgroundColor = UIColor.Clear;
                });

                // iOS CollectionView FlowLayout crash sigortası
                CollectionViewHandler.Mapper.AppendToMapping("iOSFlowFix", (handler, view) =>
                {
                    if (handler.PlatformView is UICollectionView uiView &&
                        uiView.CollectionViewLayout is UICollectionViewFlowLayout flow)
                    {
                        flow.EstimatedItemSize = CGSize.Empty; // 0x0 frame crash'ini engeller
                    }
                });
#endif
            });

            // ---- OS lifecycle & Windows başlık çubuğu ----
            builder.ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(w =>
                {
                    w.OnWindowCreated(win =>
                    {
                        win.ExtendsContentIntoTitleBar = false;

                        if (win.AppWindow?.Presenter is OverlappedPresenter p)
                        {
                            p.SetBorderAndTitleBar(true, true);
                            p.IsResizable = true;
                            p.IsMaximizable = true;
                            p.IsMinimizable = true;
                        }

                        var mw = win as MauiWinUIWindow;
                        var appWindow = mw?.AppWindow;
                        if (appWindow is null) return;

                        if (AppWindowTitleBar.IsCustomizationSupported())
                        {
                            var tb = appWindow.TitleBar;

                            var bg = WinColor.FromArgb(255, 0x1C, 0x1C, 0x1C);
                            var fg = WinColor.FromArgb(255, 255, 255, 255);
                            var hover = WinColor.FromArgb(255, 60, 60, 60);
                            var press = WinColor.FromArgb(255, 45, 45, 45);

                            tb.ExtendsContentIntoTitleBar = false;
                            tb.BackgroundColor = bg;
                            tb.InactiveBackgroundColor = bg;
                            tb.ForegroundColor = fg;
                            tb.InactiveForegroundColor = fg;
                            tb.ButtonBackgroundColor = bg;
                            tb.ButtonInactiveBackgroundColor = bg;
                            tb.ButtonForegroundColor = fg;
                            tb.ButtonInactiveForegroundColor = fg;
                            tb.ButtonHoverBackgroundColor = hover;
                            tb.ButtonPressedBackgroundColor = press;
                        }
                    });
                });
#endif
            });

            return builder.Build();
        }
    }
}
