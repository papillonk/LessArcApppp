using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;

namespace LessArcApppp
{
    public partial class AyarlarPage : ContentPage
    {
        // ============ AĞ ============
        private readonly HttpClient _http;
        private readonly string token;

        private class KullaniciDto
        {
            public string? Ad { get; set; }
            public string? Soyad { get; set; }
            public string? Eposta { get; set; }
            public string? KullaniciAdi { get; set; }
        }
        private class KullaniciGuncelleDto
        {
            public string? Ad { get; set; }
            public string? Soyad { get; set; }
            public string? KullaniciAdi { get; set; }
            public string? Eposta { get; set; }
            public string? Sifre { get; set; }
        }

        // ============ BINDING ÖLÇÜLER ============
        public Thickness PagePadding { get; private set; } = new(30, 40);
        public double PageSpacing { get; private set; } = 20;
        public double FormSpacing { get; private set; } = 16;
        public double SmallSpacing { get; private set; } = 10;

        public double FormWidth { get; private set; } = 520;

        public double IconSize { get; private set; } = 30;
        public double TitleFontSize { get; private set; } = 28;
        public double SectionTitleFontSize { get; private set; } = 18;
        public double LabelFontSize { get; private set; } = 16;
        public double EntryFontSize { get; private set; } = 16;

        public double EntryHeight { get; private set; } = 44;
        public Thickness EntryMargin { get; private set; } = new(0, 4);

        public double ButtonWidth { get; private set; } = 180;
        public double ButtonHeight { get; private set; } = 46;
        public int ButtonCorner { get; private set; } = 20;
        public double ButtonFontSize { get; private set; } = 15;
        public Thickness ButtonPadding { get; private set; } = new(12, 8);

        // ======== ÖLÇEK PARAMETRELERİ (GELİŞMİŞ) ========
        private const double BasePhoneWidth = 430.0;
        private const double MaxPhoneScale = 1.10;

        // 💡 Masaüstünde daha büyük görünüm için ölçek bandını genişlettim:
        private const double MinDeskScale = 0.85;
        private const double MaxDeskScale = 1.35;

        // Masaüstü için büyüyen ölçek eğrisi (geniş ekranlarda daha büyük UI)
        private readonly (double w, double s)[] deskCurve =
        {
            ( 800, 0.95), (1000, 1.02), (1280, 1.08), (1366, 1.12),
            (1600, 1.18), (1920, 1.25), (2560, 1.32), (3840, 1.35)
        };

        public AyarlarPage(HttpClient httpClient, string kullaniciToken)
        {
            InitializeComponent();

            _http = httpClient;
            if (_http.BaseAddress is null)
            {
#if ANDROID
                _http.BaseAddress = new Uri("http://10.0.2.2:5218");
#else
                _http.BaseAddress = new Uri("http://192.168.1.105:5218");
#endif
            }

            token = !string.IsNullOrWhiteSpace(kullaniciToken)
                ? kullaniciToken
                : (SecureStorage.GetAsync("token").GetAwaiter().GetResult() ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(token))
            {
                var auth = _http.DefaultRequestHeaders.Authorization;
                if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            BindingContext = this;
        }

        // ======== LIFECYCLE ========
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var startWidth = Width > 0
                ? Width
                : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;

            RecomputeScale(startWidth);

            await YukleKullaniciBilgileriAsync();
            try { TemaYonetici.TemayiYukle(); } catch { }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            RecomputeScale(width);
        }

        // ======== SCALE HESABI ========
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private double AutoScaleFromCurve(double width)
        {
            if (width <= deskCurve[0].w) return deskCurve[0].s;
            if (width >= deskCurve[^1].w) return deskCurve[^1].s;

            for (int i = 0; i < deskCurve.Length - 1; i++)
            {
                var (w1, s1) = deskCurve[i];
                var (w2, s2) = deskCurve[i + 1];
                if (width >= w1 && width <= w2)
                    return Lerp(s1, s2, (width - w1) / (w2 - w1));
            }
            return 1.0;
        }

        private void RecomputeScale(double w)
        {
            if (double.IsNaN(w) || w <= 0) return;

            bool isDesktop = DeviceInfo.Current.Idiom == DeviceIdiom.Desktop || DeviceInfo.Current.Idiom == DeviceIdiom.Tablet;

            // 👉 Masaüstünde daha büyük görünüm:
            double scale = isDesktop
                ? Math.Clamp(AutoScaleFromCurve(w), MinDeskScale, MaxDeskScale)
                : Math.Clamp(w / BasePhoneWidth, 0.90, MaxPhoneScale);

            // Baz değerleri ölçekle
            PagePadding = new Thickness(26 * scale, 34 * scale);
            PageSpacing = 16 * scale;
            FormSpacing = 12 * scale;
            SmallSpacing = 8 * scale;

            // 👉 Masaüstünde form genişliğini de artır
            FormWidth = isDesktop
                ? Math.Min(720 * scale, 960)   // önceki 540*scale yerine daha geniş
                : Math.Max(260, Math.Min(w - 32, 480 * scale));

            // Yazılar & ikonlar
            IconSize = 26 * scale;
            TitleFontSize = 24 * scale;
            SectionTitleFontSize = 16 * scale;
            LabelFontSize = 14 * scale;
            EntryFontSize = 14 * scale;

            // Girdiler & butonlar
            EntryHeight = 40 * scale;
            EntryMargin = new Thickness(0, 3 * scale);

            ButtonWidth = 160 * scale;
            ButtonHeight = 40 * scale;
            ButtonCorner = (int)Math.Round(16 * scale);
            ButtonFontSize = 14 * scale;
            ButtonPadding = new Thickness(10 * scale, 7 * scale);

            // Binding refresh
            OnPropertyChanged(nameof(PagePadding));
            OnPropertyChanged(nameof(PageSpacing));
            OnPropertyChanged(nameof(FormSpacing));
            OnPropertyChanged(nameof(SmallSpacing));
            OnPropertyChanged(nameof(FormWidth));
            OnPropertyChanged(nameof(IconSize));
            OnPropertyChanged(nameof(TitleFontSize));
            OnPropertyChanged(nameof(SectionTitleFontSize));
            OnPropertyChanged(nameof(LabelFontSize));
            OnPropertyChanged(nameof(EntryFontSize));
            OnPropertyChanged(nameof(EntryHeight));
            OnPropertyChanged(nameof(EntryMargin));
            OnPropertyChanged(nameof(ButtonWidth));
            OnPropertyChanged(nameof(ButtonHeight));
            OnPropertyChanged(nameof(ButtonCorner));
            OnPropertyChanged(nameof(ButtonFontSize));
            OnPropertyChanged(nameof(ButtonPadding));
        }

        // ======== SECURE STORAGE HELPERS ========
        private async Task TrySetSecureAsync(string key, string? value)
        {
            try { await SecureStorage.SetAsync(key, value ?? string.Empty); }
            catch { Preferences.Set(key, value ?? string.Empty); }
        }
        private async Task<string?> TryGetSecureAsync(string key)
        {
            try
            {
                var v = await SecureStorage.GetAsync(key);
                if (v is null) v = Preferences.Get(key, null);
                return v;
            }
            catch { return Preferences.Get(key, null); }
        }

        // ======== PROFİL YÜKLE ========
        private async Task YukleKullaniciBilgileriAsync()
        {
            try
            {
                var resp = await _http.GetAsync("/api/KullanicilarApi/benim");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var dto = JsonSerializer.Deserialize<KullaniciDto>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (dto != null)
                    {
                        entryAd.Text = dto.Ad;
                        entrySoyad.Text = dto.Soyad;
                        entryEposta.Text = dto.Eposta;
                        entryKullaniciAdi.Text = dto.KullaniciAdi;

                        await TrySetSecureAsync("ad", dto.Ad);
                        await TrySetSecureAsync("soyad", dto.Soyad);
                        await TrySetSecureAsync("eposta", dto.Eposta);
                        await TrySetSecureAsync("kullaniciAdi", dto.KullaniciAdi);
                        return;
                    }
                }
                else if ((int)resp.StatusCode is 401 or 403)
                {
                    await DisplayAlert("Oturum", "Oturumunuz geçersiz. Lütfen tekrar giriş yapın.", "Tamam");
                    return;
                }

                // API başarısızsa cache
                entryAd.Text = await TryGetSecureAsync("ad");
                entrySoyad.Text = await TryGetSecureAsync("soyad");
                entryEposta.Text = await TryGetSecureAsync("eposta");
                entryKullaniciAdi.Text = await TryGetSecureAsync("kullaniciAdi");
            }
            catch
            {
                entryAd.Text = await TryGetSecureAsync("ad");
                entrySoyad.Text = await TryGetSecureAsync("soyad");
                entryEposta.Text = await TryGetSecureAsync("eposta");
                entryKullaniciAdi.Text = await TryGetSecureAsync("kullaniciAdi");
            }
        }

        // ======== OLAYLAR ========
        private async void BtnGuncelle_Clicked(object sender, EventArgs e)
        {
            try
            {
                string eskiAd = await TryGetSecureAsync("ad") ?? "";
                string eskiSoyad = await TryGetSecureAsync("soyad") ?? "";
                string eskiEposta = await TryGetSecureAsync("eposta") ?? "";
                string eskiKullaniciAdi = await TryGetSecureAsync("kullaniciAdi") ?? "";

                var guncelleDto = new KullaniciGuncelleDto
                {
                    Ad = string.IsNullOrWhiteSpace(entryAd.Text) ? eskiAd : entryAd.Text,
                    Soyad = string.IsNullOrWhiteSpace(entrySoyad.Text) ? eskiSoyad : entrySoyad.Text,
                    KullaniciAdi = string.IsNullOrWhiteSpace(entryKullaniciAdi.Text) ? eskiKullaniciAdi : entryKullaniciAdi.Text,
                    Eposta = string.IsNullOrWhiteSpace(entryEposta.Text) ? eskiEposta : entryEposta.Text,
                    Sifre = string.IsNullOrWhiteSpace(entrySifre.Text) ? null : entrySifre.Text
                };

                var json = JsonSerializer.Serialize(guncelleDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PutAsync("/api/KullanicilarApi/guncelle", content);
                var mesaj = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("✅ Başarılı", "Bilgiler güncellendi.", "Tamam");

                    await TrySetSecureAsync("ad", guncelleDto.Ad);
                    await TrySetSecureAsync("soyad", guncelleDto.Soyad);
                    await TrySetSecureAsync("eposta", guncelleDto.Eposta);
                    await TrySetSecureAsync("kullaniciAdi", guncelleDto.KullaniciAdi);

                    await YukleKullaniciBilgileriAsync();
                    return;
                }

                await DisplayAlert("❌ Hata",
                    $"Güncelleme başarısız.\nKod: {(int)response.StatusCode}\nMesaj: {mesaj}",
                    "Tamam");
            }
            catch (Exception ex)
            {
                await DisplayAlert("💥 Hata", $"Hata oluştu: {ex.Message}", "Tamam");
            }
        }

        private async void BtnCikisYap_Clicked(object sender, EventArgs e)
        {
            bool onay = await DisplayAlert("Çıkış", "Oturumu kapatmak istiyor musunuz?", "Evet", "Hayır");
            if (!onay) return;

            KullaniciBilgileriniTemizle();

            await Navigation.PushAsync(new MainPage(_http));
            Navigation.RemovePage(this);
        }

        private async void BtnHesapSil_Clicked(object sender, EventArgs e)
        {
            bool onay = await DisplayAlert("Dikkat", "Hesabınızı silmek üzeresiniz. Emin misiniz?", "Evet", "İptal");
            if (!onay) return;

            try
            {
                var response = await _http.DeleteAsync("/api/KullanicilarApi/benim");
                var mesaj = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Silindi", "Hesabınız başarıyla silindi.", "Tamam");

                    KullaniciBilgileriniTemizle();
                    await Navigation.PushAsync(new MainPage(_http));
                    Navigation.RemovePage(this);
                }
                else if ((int)response.StatusCode == 401)
                {
                    await DisplayAlert("Oturum Süresi Doldu", "Lütfen tekrar giriş yapın.", "Tamam");
                    await Navigation.PushAsync(new MainPage(_http));
                }
                else
                {
                    await DisplayAlert("Hata", $"Hesap silinemedi.\nKod: {response.StatusCode}\nMesaj: {mesaj}", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Silme sırasında hata oluştu: {ex.Message}", "Tamam");
            }
        }

        private void KullaniciBilgileriniTemizle()
        {
            SecureStorage.Remove("kullaniciId");
            SecureStorage.Remove("token");
            SecureStorage.Remove("rol");
            SecureStorage.Remove("ad");
            SecureStorage.Remove("soyad");
            SecureStorage.Remove("eposta");
            SecureStorage.Remove("kullaniciAdi");

            Preferences.Remove("ad");
            Preferences.Remove("soyad");
            Preferences.Remove("eposta");
            Preferences.Remove("kullaniciAdi");
        }
    }
}
