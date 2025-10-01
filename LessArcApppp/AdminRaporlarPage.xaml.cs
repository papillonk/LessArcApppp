using System;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

namespace LessArcApppp
{
    public partial class AdminRaporlarPage : ContentPage
    {
        private readonly string token;
        private readonly HttpClient _http;

        // 🔹 Responsive UI state (XAML’de F_*/S_*/C_* bağlayarak kullanabilirsin)
        private readonly UiScale _ui = new();

        // DI: HttpClient (MauiProgram.cs'de singleton) + token
        public AdminRaporlarPage(HttpClient httpClient, string kullaniciToken)
        {
            InitializeComponent();
            _http = httpClient;
            token = kullaniciToken ?? string.Empty;

            BindingContext = _ui;                      // XAML font/spacing bind’leri için
            SizeChanged += ContentPage_SizeChanged;    // ekran döndüğünde/resize’da ölçekle
            ApplyResponsiveSizing(isInitial: true);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ApplyResponsiveSizing(); // görünürken de bir kez daha uygula
        }

        // 🔘 Rapor sayfaları
        private async void BtnCalisanRapor_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new AdminCalisanRaporPage(_http, token));

        private async void BtnZamanRapor_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new AdminZamanRaporPage(_http, token));

        // ===== Responsive Ölçekleme =====
        private void ContentPage_SizeChanged(object? sender, EventArgs e) => ApplyResponsiveSizing();

        private void ApplyResponsiveSizing(bool isInitial = false)
        {
            double w = Width > 0
                ? Width
                : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;

            bool isPhone = DeviceInfo.Current.Idiom == DeviceIdiom.Phone || w < 800;

            // XAML: x:Name="MobileLayoutView" / "DesktopLayoutView"
            if (MobileLayoutView != null) MobileLayoutView.IsVisible = isPhone;
            if (DesktopLayoutView != null) DesktopLayoutView.IsVisible = !isPhone;

            // Font, spacing, padding vb.
            if (isPhone)
            {
                // 📱 Mobil: butonları daha uzun & yazıyı daha büyük yaptık
                _ui.F_Title = 22;
                _ui.F_Body = 17;   // 15 -> 17
                _ui.F_Small = 13;

                _ui.S_L = 18;
                _ui.S_M = 14;
                _ui.S_S = 10;

                _ui.C_Radius = 18;
                _ui.C_SmallRadius = 12;

                _ui.P_Page = new Thickness(16);
                _ui.P_CardPad = new Thickness(16);
                _ui.P_ItemPad = new Thickness(18, 14); // iç dolgu arttı

                _ui.H_Button = 56;   // 44 -> 56  ✅ yazılar kesilmesin
            }
            else
            {
                // 💻 Masaüstü: butonlar daha tok
                _ui.F_Title = 26;
                _ui.F_Body = 18;   // 16 -> 18
                _ui.F_Small = 13;

                _ui.S_L = 22;
                _ui.S_M = 16;
                _ui.S_S = 12;

                _ui.C_Radius = 22;
                _ui.C_SmallRadius = 14;

                _ui.P_Page = new Thickness(24);
                _ui.P_CardPad = new Thickness(18);
                _ui.P_ItemPad = new Thickness(20, 16); // iç dolgu arttı

                _ui.H_Button = 60;   // 46 -> 60  ✅ daha yüksek
            }
        }

        // ===== UI Scale ViewModel =====
        private sealed class UiScale : INotifyPropertyChanged
        {
            // Fontlar
            double fTitle = 26, fBody = 18, fSmall = 13;
            public double F_Title { get => fTitle; set => Set(ref fTitle, value); }
            public double F_Body { get => fBody; set => Set(ref fBody, value); }
            public double F_Small { get => fSmall; set => Set(ref fSmall, value); }

            // Spacing
            double sL = 22, sM = 16, sS = 12;
            public double S_L { get => sL; set => Set(ref sL, value); }
            public double S_M { get => sM; set => Set(ref sM, value); }
            public double S_S { get => sS; set => Set(ref sS, value); }

            // Corner radii
            double cRadius = 22, cSmall = 14;
            public double C_Radius { get => cRadius; set => Set(ref cRadius, value); }
            public double C_SmallRadius { get => cSmall; set => Set(ref cSmall, value); }

            // Paddings
            Thickness pPage = new(24), pCard = new(18), pItem = new(20, 16);
            public Thickness P_Page { get => pPage; set { pPage = value; OnPropertyChanged(); } }
            public Thickness P_CardPad { get => pCard; set { pCard = value; OnPropertyChanged(); } }
            public Thickness P_ItemPad { get => pItem; set { pItem = value; OnPropertyChanged(); } }

            // Button height
            double hBtn = 60;
            public double H_Button { get => hBtn; set => Set(ref hBtn, value); }

            public event PropertyChangedEventHandler? PropertyChanged;
            void OnPropertyChanged([CallerMemberName] string? n = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            bool Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
            {
                if (Equals(field, value)) return false;
                field = value; OnPropertyChanged(n); return true;
            }
        }
    }
}
