using System;
using System.Linq;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

#if WINDOWS
using Microsoft.Maui.Platform;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Windows.Graphics;
#endif

#if MACCATALYST
using UIKit;
using CoreGraphics;
#endif

namespace LessArcApppp
{
    public static class WindowSizer
    {
        /// <summary>
        /// Ana pencereyi ekran çalışma alanının (taskbar/dock hariç) belirtilen yüzdesine getirir ve ortalar.
        /// </summary>
        public static void ResizeToPercent(double percent = 0.8, bool center = true)
        {
            percent = Math.Clamp(percent, 0.2, 1.0);

#if WINDOWS
            var mw = Application.Current?.Windows?.FirstOrDefault()?.Handler?.PlatformView as Microsoft.Maui.Platform.MauiWinUIWindow;
            if (mw is null) return;

            var hwnd = WindowNative.GetWindowHandle(mw);
            var winId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(winId);
            if (appWindow is null) return;

            // Aktif ekranın çalışma alanını al
            var displayArea = DisplayArea.GetFromWindowId(winId, DisplayAreaFallback.Primary);
            var work = displayArea.WorkArea; // piksel

            int targetW = (int)(work.Width  * percent);
            int targetH = (int)(work.Height * percent);

            appWindow.Resize(new SizeInt32(targetW, targetH));

            if (center)
            {
                int x = work.X + (work.Width  - targetW) / 2;
                int y = work.Y + (work.Height - targetH) / 2;
                appWindow.Move(new PointInt32(x, y));
            }
#elif MACCATALYST
            // MacCatalyst: pencere yönetimi sınırlı. UIWindowScene üstünden tercih edilen boyut belirleyebiliriz.
            // Bu, çoğu durumda pencereyi hedef boyuta getirir/korur.
            var scene = UIApplication.SharedApplication?.ConnectedScenes?
                .OfType<UIWindowScene>()
                .FirstOrDefault();

            var window = UIApplication.SharedApplication?.KeyWindow
                         ?? scene?.Windows?.FirstOrDefault();

            if (scene is null || window is null) return;

            // Ekran boyutu (pt)
            var screen = UIScreen.MainScreen.Bounds; // pt
            nfloat targetW = (nfloat)(screen.Width  * percent);
            nfloat targetH = (nfloat)(screen.Height * percent);

            var size = new CGSize(targetW, targetH);

            // Catalyst’te doğrudan "resize" yok, ama size restrictions ile yönlendirebiliriz
            var restrict = scene.SizeRestrictions;
            if (restrict != null)
            {
                // İzin verilen aralığı hedef boyuta sabitle
                restrict.MinimumSize = size;
                restrict.MaximumSize = size;
            }

            // Ortalamak için çerçeve konumu öner
            // Not: Konumlandırma her sürümde kabul edilmeyebilir; Catalyst pencere yöneticisi karar verebilir.
            var frame = window.Frame;
            var origin = new CGPoint((screen.Width - targetW) / 2f, (screen.Height - targetH) / 2f);
            frame.X = origin.X;
            frame.Y = origin.Y;
            frame.Width = targetW;
            frame.Height = targetH;
            window.Frame = frame;

            // Bazı sürümlerde layout’u tetiklemek gerekebilir
            window.SetNeedsLayout();
            window.LayoutIfNeeded();
#else
            // Mobil platformlarda pencere kavramı yok; %80 pencere boyutu anlamlı değil.
            // Gerekirse UI ölçeğiyle (Styles) oynanabilir.
            return;
#endif
        }
    }
}
