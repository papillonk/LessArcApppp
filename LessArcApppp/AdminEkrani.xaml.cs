using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace LessArcApppp
{
    public partial class AdminEkrani : ContentPage
    {
        private readonly string token;
        private readonly HttpClient _http;

        // 🔔 Bildirim çerçevesini kırmızı yapan trigger için Binding kaynağı
        private bool _hasUnread;
        public bool HasUnread
        {
            get => _hasUnread;
            set
            {
                if (_hasUnread != value)
                {
                    _hasUnread = value;
                    OnPropertyChanged(nameof(HasUnread));
                }
            }
        }

        private CancellationTokenSource? _pollCts;

        // Masaüstü “viewbox” ölçek referansı (XAML’de x:Name="desktopCanvas")
        private const double DESIGN_W = 1100.0;
        private const double DESIGN_H = 700.0;
        private const double MIN_SCALE = 0.80;
        private const double MAX_SCALE = 1.60;

        public AdminEkrani(HttpClient httpClient, string kullaniciToken)
        {
            InitializeComponent();

            _http = httpClient;
            token = kullaniciToken ?? string.Empty;

            // DataTrigger/Binding’ler için
            BindingContext = this;

            // Üst barları gizle
            NavigationPage.SetHasBackButton(this, false);
            NavigationPage.SetHasNavigationBar(this, false);
            Shell.SetNavBarIsVisible(this, false);
            Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false });
            Shell.SetTitleView(this, new ContentView());

            // Authorization yoksa ekle
            var auth = _http.DefaultRequestHeaders.Authorization;
            if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                if (!string.IsNullOrWhiteSpace(token))
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        // ========= Responsive (XAML: SizeChanged="Admin_SizeChanged") =========
        private void Admin_SizeChanged(object sender, EventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            if (desktopCanvas is null) return; // XAML’de x:Name="desktopCanvas" (sadece Desktop görünümünde var)

            double scaleW = Width / DESIGN_W;
            double scaleH = Height / DESIGN_H;
            double scale = Math.Min(scaleW, scaleH);

            if (scale < MIN_SCALE) scale = MIN_SCALE;
            if (scale > MAX_SCALE) scale = MAX_SCALE;

            desktopCanvas.Scale = scale;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Hoş geldin metni
            var ad = await SecureStorage.GetAsync("ad");
            if (string.IsNullOrWhiteSpace(ad))
                ad = await TryGetKullaniciAdiAsync();

            string greet = string.IsNullOrWhiteSpace(ad) ? "Hoşgeldin 👋" : $"Hoşgeldin {ad} 👋";
            if (lblHosgeldin != null) lblHosgeldin.Text = greet;               // mobil/tablet başlık
            if (lblHosgeldinDesktop != null) lblHosgeldinDesktop.Text = greet; // desktop başlık

            // İlk yüklemede unread çek ve polling başlat
            await RefreshUnreadAsync();
            StartUnreadPolling();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopUnreadPolling();
        }

        private void StartUnreadPolling()
        {
            StopUnreadPolling();
            _pollCts = new CancellationTokenSource();

            Application.Current?.Dispatcher.StartTimer(TimeSpan.FromSeconds(30), () =>
            {
                if (_pollCts?.IsCancellationRequested == true) return false;
                _ = RefreshUnreadAsync();
                return true;
            });
        }

        private void StopUnreadPolling()
        {
            try { _pollCts?.Cancel(); } catch { }
            _pollCts = null;
        }

        // Unread sayısını hem düz sayı, hem JSON dönerse de yönetebilecek hale getirelim (ileride API değişirse kırılmasın)
        private async Task RefreshUnreadAsync()
        {
            try
            {
                var raw = await _http.GetStringAsync("/api/Bildirimler/admin/unread-count");
                if (string.IsNullOrWhiteSpace(raw))
                {
                    HasUnread = false;
                    return;
                }

                if (int.TryParse(raw.Trim(), out var cnt))
                {
                    HasUnread = cnt > 0;
                    return;
                }

                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;
                    int val =
                        root.TryGetProperty("count", out var p1) ? p1.GetInt32() :
                        root.TryGetProperty("unread", out var p2) ? p2.GetInt32() :
                        root.TryGetProperty("unreadCount", out var p3) ? p3.GetInt32() :
                        0;
                    HasUnread = val > 0;
                }
                catch
                {
                    HasUnread = false;
                }
            }
            catch
            {
                // Ağ hatası tetiklemesin yeter
                HasUnread = false;
            }
        }

        private async Task<string> TryGetKullaniciAdiAsync()
        {
            try
            {
                var response = await _http.GetAsync("/api/Kullanicilar/ben");
                if (!response.IsSuccessStatusCode) return "";

                var json = await response.Content.ReadAsStringAsync();
                var kullanici = JsonSerializer.Deserialize<KullaniciDto>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var ad = kullanici?.Ad ?? "";
                if (!string.IsNullOrWhiteSpace(ad))
                    await SecureStorage.SetAsync("ad", ad);

                return ad;
            }
            catch
            {
                return "";
            }
        }

        // 🔘 Butonlar
        private async void BtnBildirimler_Clicked(object sender, EventArgs e)
        {
            var page = new AdminBildirimlerPage(_http, token);

            // Sayfadan çıkınca unread'i tazele (kırmızı çerçeve hemen söner)
            page.Disappearing += async (_, __) =>
            {
                try { await RefreshUnreadAsync(); } catch { }
            };

            await Navigation.PushAsync(page);
        }



        private async void BtnProjeler_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new AdminProjelerPage(_http, token));

        private async void BtnPlanlar_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new AdminPlanlarPage(_http, token));

        private async void BtnRaporlar_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new AdminRaporlarPage(_http, token));

        private async void BtnAyarlar_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new AyarlarPage(_http, token));

        private async void BtnKullanıciEkle_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new HesapAyarlarPage(_http, token));

        private async void BtnProjeAyarlari_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new ProjeAtamaPage(_http, token));

        private class KullaniciDto { public string Ad { get; set; } = ""; }
    }
}
