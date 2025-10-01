using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LessArcApppp
{
    public partial class CalisanEkrani : ContentPage
    {
        private readonly string token;
        private readonly HttpClient _http;    // DI'dan gelen HttpClient
        private readonly UiScale _ui = new(); // responsive ölçüler

        // DI: HttpClient (MauiProgram.cs'de BaseAddress ayarlı) + token
        public CalisanEkrani(HttpClient httpClient, string kullaniciToken)
        {
            InitializeComponent();

            _http = httpClient;
            token = kullaniciToken ?? string.Empty;

            // Üst bar / geri butonu kapat
            NavigationPage.SetHasBackButton(this, false);
            NavigationPage.SetHasNavigationBar(this, false);
            Shell.SetNavBarIsVisible(this, false);
            Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false });
            Shell.SetTitleView(this, new ContentView());

            // Authorization header yoksa ekle
            var auth = _http.DefaultRequestHeaders.Authorization;
            if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                if (!string.IsNullOrWhiteSpace(token))
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Responsive binding’ler için
            BindingContext = _ui;
            SizeChanged += ContentPage_SizeChanged;
            ApplyResponsiveSizing(isInitial: true);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Ekran görünürken de bir kez daha ölçülendir
            ApplyResponsiveSizing();

            // ✅ Önce local kayıtlı ad (anında)
            var ad = await SecureStorage.GetAsync("ad");
            if (!string.IsNullOrWhiteSpace(ad))
            {
                lblHosgeldin.Text = $"Hoşgeldin {ad} 👋";
            }
            else
            {
                await KullaniciAdiniYukle();
            }
        }

        private async Task KullaniciAdiniYukle()
        {
            try
            {
                var response = await _http.GetAsync("/api/Kullanicilar/ben");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var kullanici = JsonSerializer.Deserialize<KullaniciDto>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (kullanici != null && !string.IsNullOrWhiteSpace(kullanici.Ad))
                    {
                        lblHosgeldin.Text = $"Hoşgeldin {kullanici.Ad} 👋";
                        await SecureStorage.SetAsync("ad", kullanici.Ad);
                    }
                    else
                    {
                        lblHosgeldin.Text = "Hoşgeldin 👋";
                    }
                }
                else
                {
                    lblHosgeldin.Text = "Hoşgeldin 👋";
                }
            }
            catch
            {
                lblHosgeldin.Text = "Hoşgeldin 👋";
            }
        }

        // --- Buton Eventleri ---
        private async void BtnProjelerim_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new ProjelerimPage(_http, token));

        private async void BtnPlanlarim_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new PlanlarimPage(_http, token));

        private async void BtnBildirimGonder_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new BildirimGonderPage(_http, token));

        private async void BtnAyarlar_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new AyarlarPage(_http, token));

        private async void BtnCikisYap_Clicked(object sender, EventArgs e)
        {
            // İstersen çıkışta ad bilgisini temizleyebilirsin
            // await SecureStorage.SetAsync("ad", string.Empty);
            await Navigation.PopToRootAsync();
        }

        // ===== Responsive Ölçekleme =====
        private void ContentPage_SizeChanged(object? sender, EventArgs e) => ApplyResponsiveSizing();

        private void ApplyResponsiveSizing(bool isInitial = false)
        {
            // İçerik genişliği (device density dikkate alınır)
            double w = Width > 0
                ? Width
                : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;

            bool isPhone = DeviceInfo.Current.Idiom == DeviceIdiom.Phone || w < 800;

            if (isPhone)
            {
                _ui.F_Title = 22;   // Hoşgeldin
                _ui.F_Body = 18;   // buton yazıları
                _ui.S_L = 18; _ui.S_M = 12;

                _ui.H_Button = 64;
                _ui.W_Button = Math.Min(360, w - 32); // yanlarda 16’şar px boşluk varsay
                _ui.T_Border = 4;
                _ui.P_Button = new Thickness(12, 6);
                _ui.M_Welcome = new Thickness(0, 0, 0, _ui.S_M);
            }
            else
            {
                _ui.F_Title = 26;
                _ui.F_Body = 20;
                _ui.S_L = 22; _ui.S_M = 14;

                _ui.H_Button = 72;
                _ui.W_Button = 420;
                _ui.T_Border = 4;
                _ui.P_Button = new Thickness(16, 8);
                _ui.M_Welcome = new Thickness(0, 0, 0, _ui.S_M);
            }

            // XAML’de binding kullanmıyorsan minimum uyum:
            if (lblHosgeldin != null)
                lblHosgeldin.FontSize = _ui.F_Title;
        }

        // --- DTO ---
        private class KullaniciDto
        {
            public string Ad { get; set; } = "";
        }

        // ======== UI ölçek view-model ========
        private sealed class UiScale : INotifyPropertyChanged
        {
            // Fontlar
            double fTitle = 26, fBody = 20;
            public double F_Title { get => fTitle; set => Set(ref fTitle, value); }
            public double F_Body { get => fBody; set => Set(ref fBody, value); }

            // Spacing
            double sL = 22, sM = 14;
            public double S_L { get => sL; set => Set(ref sL, value); }
            public double S_M { get => sM; set => Set(ref sM, value); }

            // Buton ölçüleri
            double hBtn = 72, wBtn = 420, tBorder = 4;
            public double H_Button { get => hBtn; set => Set(ref hBtn, value); }
            public double W_Button { get => wBtn; set => Set(ref wBtn, value); }
            public double T_Border { get => tBorder; set => Set(ref tBorder, value); }

            // Buton iç padding
            Thickness pBtn = new(16, 8);
            public Thickness P_Button { get => pBtn; set { pBtn = value; OnPropertyChanged(); } }

            // Hoşgeldin label margin (Thickness olarak tek binding)
            Thickness mWelcome = new(0, 0, 0, 14);
            public Thickness M_Welcome { get => mWelcome; set { mWelcome = value; OnPropertyChanged(); } }

            // INotifyPropertyChanged
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
