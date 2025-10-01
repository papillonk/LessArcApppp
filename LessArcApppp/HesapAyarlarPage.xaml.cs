using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices; // DeviceInfo için

namespace LessArcApppp
{
    public partial class HesapAyarlarPage : ContentPage
    {
        // 🔐 DI’den gelenler
        private readonly string token;
        private readonly HttpClient _http;

        // =========================
        // 📏 RESPONSIVE ÖLÇEKLEME
        // =========================
        public double BaseWidth { get; set; } = 430.0;
        public double DesktopEffectiveWidthCap { get; set; } = 2200.0;
        public double? UserZoomFactor { get; set; } = null;

        private readonly (double w, double s)[] _desktopScaleCurve =
        {
            (  800, 1.00),
            ( 1000, 1.06),
            ( 1200, 1.12),
            ( 1366, 1.18),
            ( 1500, 1.28),
            ( 1680, 1.38),
            ( 1920, 1.52),
            ( 2200, 1.66),
            ( 2560, 1.80),
        };

        public double ScaledPadding { get; private set; } = 18;
        public double ScaledSpacing { get; private set; } = 16;
        public double ScaledSmallSpacing { get; private set; } = 10;

        public double ScaledCardPadding { get; private set; } = 16;
        public double ScaledItemPadding { get; private set; } = 12;

        public double ScaledCornerRadiusLarge { get; private set; } = 20;
        public double ScaledCornerRadiusMedium { get; private set; } = 18;

        public double ScaledSectionFont { get; private set; } = 20;
        public double ScaledFont { get; private set; } = 16;
        public double ScaledFontSmall { get; private set; } = 12;

        public double ScaledIconSmall { get; private set; } = 28;

        public ObservableCollection<KullaniciListItem> Users { get; } = new();
        public List<string> Roller { get; } = new() { "admin", "calisan" };

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private double AutoScaleFromCurve(double widthDip)
        {
            if (_desktopScaleCurve.Length == 0) return 1.0;
            if (widthDip <= _desktopScaleCurve[0].w) return _desktopScaleCurve[0].s;
            if (widthDip >= _desktopScaleCurve[^1].w) return _desktopScaleCurve[^1].s;

            for (int i = 0; i < _desktopScaleCurve.Length - 1; i++)
            {
                var (w1, s1) = _desktopScaleCurve[i];
                var (w2, s2) = _desktopScaleCurve[i + 1];
                if (widthDip >= w1 && widthDip <= w2)
                {
                    var t = (widthDip - w1) / (w2 - w1);
                    return Lerp(s1, s2, t);
                }
            }
            return 1.0;
        }

        private void RecomputeScale()
        {
            var w = Width;
            if (double.IsNaN(w) || w <= 0) return;

            bool isDesktop = DeviceInfo.Idiom == DeviceIdiom.Desktop;
            double widthDip = isDesktop ? Math.Min(w, DesktopEffectiveWidthCap) : w;

            double scale;
            if (isDesktop)
            {
                scale = AutoScaleFromCurve(widthDip);
                if (UserZoomFactor is > 0) scale *= UserZoomFactor.Value;
                scale = Math.Clamp(scale, 1.0, 1.85);
            }
            else
            {
                scale = Math.Clamp(widthDip / BaseWidth, 1.0, 1.30);
            }

            ScaledPadding = 18 * scale;
            ScaledSpacing = 16 * scale;
            ScaledSmallSpacing = 10 * scale;

            ScaledCardPadding = 16 * scale;
            ScaledItemPadding = 12 * scale;

            ScaledCornerRadiusLarge = 20 * scale;
            ScaledCornerRadiusMedium = 18 * scale;

            ScaledSectionFont = 20 * scale;
            ScaledFont = 16 * scale;
            ScaledFontSmall = 12 * scale;

            ScaledIconSmall = 28 * scale;

            OnPropertyChanged(nameof(ScaledPadding));
            OnPropertyChanged(nameof(ScaledSpacing));
            OnPropertyChanged(nameof(ScaledSmallSpacing));
            OnPropertyChanged(nameof(ScaledCardPadding));
            OnPropertyChanged(nameof(ScaledItemPadding));
            OnPropertyChanged(nameof(ScaledCornerRadiusLarge));
            OnPropertyChanged(nameof(ScaledCornerRadiusMedium));
            OnPropertyChanged(nameof(ScaledSectionFont));
            OnPropertyChanged(nameof(ScaledFont));
            OnPropertyChanged(nameof(ScaledFontSmall));
            OnPropertyChanged(nameof(ScaledIconSmall));
        }

        // ---- List ve API modelleri ----
        public class KullaniciListItem
        {
            public int Id { get; set; }
            public string Ad { get; set; } = "";
            public string Soyad { get; set; } = "";
            public string Role { get; set; } = "";
            public string AdSoyadRol => $"{Ad} {Soyad} • {Role}";
        }

        private class Kullanici
        {
            public int Id { get; set; }
            public string Ad { get; set; } = "";
            public string Soyad { get; set; } = "";
            public string Eposta { get; set; } = "";
            public string Sifre { get; set; } = "";
            public string? KullaniciAdi { get; set; }
            public string Role { get; set; } = "calisan";
            public string? SifreSifirlamaKodu { get; set; }
            public DateTime? KodGecerlilikSuresi { get; set; }
        }

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // ✅ DI kullanan ctor: HttpClient + token
        public HesapAyarlarPage(HttpClient httpClient, string kullaniciToken)
        {
            InitializeComponent();

            _http = httpClient;
            token = kullaniciToken ?? string.Empty;

            BindingContext = this;
            SizeChanged += (_, __) => RecomputeScale();
            RecomputeScale();

            // Authorization header yoksa tak
            var auth = _http.DefaultRequestHeaders.Authorization;
            if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                if (!string.IsNullOrWhiteSpace(token))
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Sayfa açılınca listeyi getir
            _ = LoadUsersAsync();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            RecomputeScale();
        }

        // ================== Yardımcılar ==================
        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        }

        private async Task ShowServerErrorAsync(HttpResponseMessage resp)
        {
            string body = "";
            try { body = await resp.Content.ReadAsStringAsync(); } catch { }
            await DisplayAlert("Hata",
                $"İstek başarısız. Kod: {(int)resp.StatusCode}\n{body}",
                "Tamam");
        }

        /// <summary>
        /// Hangi düzen aktifse (mobil/masaüstü), o form kontrollerini okur.
        /// </summary>
        private (string ad, string soyad, string eposta, string kullaniciAdi, string sifre, string rol) ReadActiveForm()
        {
            // Masaüstü varsayılan
            string ad = (entryAd?.Text ?? "").Trim();
            string soyad = (entrySoyad?.Text ?? "").Trim();
            string eposta = (entryEposta?.Text ?? "").Trim();
            string kullaniciAdi = (entryKullaniciAdi?.Text ?? "").Trim();
            string sifre = (entrySifre?.Text ?? "").Trim();
            string rol = (pickerRol?.SelectedItem as string ?? "").Trim();

            // Telefon/Tablet ise mobil formu kullan
            bool isMobile = DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceInfo.Idiom == DeviceIdiom.Tablet;
            if (isMobile && entryAdMobile != null)
            {
                ad = (entryAdMobile.Text ?? "").Trim();
                soyad = (entrySoyadMobile.Text ?? "").Trim();
                eposta = (entryEpostaMobile.Text ?? "").Trim();
                kullaniciAdi = (entryKullaniciAdiMobile.Text ?? "").Trim();
                sifre = (entrySifreMobile.Text ?? "").Trim();
                rol = (pickerRolMobile?.SelectedItem as string ?? "").Trim();
            }

            return (ad, soyad, eposta, kullaniciAdi, sifre, rol);
        }

        private void ClearForm()
        {
            // Desktop
            if (entryAd != null) entryAd.Text = "";
            if (entrySoyad != null) entrySoyad.Text = "";
            if (entryEposta != null) entryEposta.Text = "";
            if (entryKullaniciAdi != null) entryKullaniciAdi.Text = "";
            if (entrySifre != null) entrySifre.Text = "";
            if (pickerRol != null) pickerRol.SelectedIndex = -1;

            // Mobile
            if (entryAdMobile != null) entryAdMobile.Text = "";
            if (entrySoyadMobile != null) entrySoyadMobile.Text = "";
            if (entryEpostaMobile != null) entryEpostaMobile.Text = "";
            if (entryKullaniciAdiMobile != null) entryKullaniciAdiMobile.Text = "";
            if (entrySifreMobile != null) entrySifreMobile.Text = "";
            if (pickerRolMobile != null) pickerRolMobile.SelectedIndex = -1;
        }

        // ================== Listeleme ==================
        private async Task LoadUsersAsync()
        {
            try
            {
                var resp = await _http.GetAsync("/api/KullanicilarApi");
                if (!resp.IsSuccessStatusCode)
                {
                    await ShowServerErrorAsync(resp);
                    return;
                }

                var json = await resp.Content.ReadAsStringAsync();
                var list = JsonSerializer.Deserialize<List<Kullanici>>(json, _jsonOpts) ?? new();

                var view = list.Select(k => new KullaniciListItem
                {
                    Id = k.Id,
                    Ad = k.Ad?.Trim() ?? "",
                    Soyad = k.Soyad?.Trim() ?? "",
                    Role = (k.Role ?? "").Trim()
                })
                .OrderBy(x => x.Ad)
                .ThenBy(x => x.Soyad)
                .ToList();

                // Binding kullanan XAML için:
                Users.Clear();
                foreach (var u in view) Users.Add(u);

                // x:Name ile ItemsSource atayan XAML için de doldur:
                if (cvKullanicilar != null) cvKullanicilar.ItemsSource = view;             // Desktop
                if (cvKullanicilarMobile != null) cvKullanicilarMobile.ItemsSource = view;       // Mobile
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Liste yüklenirken sorun oluştu:\n{ex.Message}", "Tamam");
            }
        }

        // ================== Ekle ==================
        private bool _saving = false;

        private async void BtnKaydet_Clicked(object sender, EventArgs e)
        {
            if (_saving) return; // çift tıklama koruması
            _saving = true;

            try
            {
                var (ad, soyad, eposta, kullaniciAdi, sifre, rol) = ReadActiveForm();

                // Zorunlu alanlar
                if (string.IsNullOrWhiteSpace(ad) ||
                    string.IsNullOrWhiteSpace(soyad) ||
                    string.IsNullOrWhiteSpace(eposta) ||
                    string.IsNullOrWhiteSpace(sifre) ||
                    string.IsNullOrWhiteSpace(rol))
                {
                    await DisplayAlert("Eksik Bilgi", "Ad, Soyad, E-posta, Şifre ve Rol zorunludur.", "Tamam");
                    return;
                }

                if (!IsValidEmail(eposta))
                {
                    await DisplayAlert("Uyarı", "Lütfen geçerli bir e-posta gir.", "Tamam");
                    return;
                }

                var yeni = new Kullanici
                {
                    Ad = ad,
                    Soyad = soyad,
                    Eposta = eposta,
                    Sifre = sifre,
                    KullaniciAdi = string.IsNullOrWhiteSpace(kullaniciAdi) ? null : kullaniciAdi,
                    Role = rol
                };

                var content = new StringContent(JsonSerializer.Serialize(yeni, _jsonOpts), Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync("/api/KullanicilarApi", content);

                if (resp.IsSuccessStatusCode)
                {
                    await DisplayAlert("Başarılı", "Kullanıcı eklendi.", "Tamam");
                    ClearForm();
                    await LoadUsersAsync();
                    return;
                }

                // ❗ Hata durumları
                if (resp.StatusCode == HttpStatusCode.Conflict)
                {
                    var msg = await resp.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(msg))
                        msg = "Kullanıcı adı veya e-posta zaten kayıtlı.";
                    await DisplayAlert("Çakışma", msg, "Tamam");
                    return;
                }

                await ShowServerErrorAsync(resp);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Kayıt sırasında sorun oluştu:\n{ex.Message}", "Tamam");
            }
            finally
            {
                _saving = false;
            }
        }

 
        // ================== Sil ==================
        private async void BtnSil_Clicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton btn || btn.CommandParameter is not int id) return;

            var onay = await DisplayAlert(
                "Sil",
                "Bu kullanıcıyı ve ona ait TÜM yorumları silmek istiyor musun?",
                "Evet", "Hayır");
            if (!onay) return;

            try
            {
                // 1) Yorumları sil (endpoint: DELETE /api/Yorumlar/kullanici/{id})
                //    - 204/200 -> başarı
                //    - 404 -> bu kullanıcıya ait yorum yok, sorun değil
                //    - Diğer durumlar -> hatayı göster ve kullanıcıyı silme
                var yorumResp = await _http.DeleteAsync($"/api/Yorumlar/kullanici/{id}");
                if (!(yorumResp.IsSuccessStatusCode || yorumResp.StatusCode == HttpStatusCode.NotFound))
                {
                    await ShowServerErrorAsync(yorumResp);
                    return;
                }

                // 2) Kullanıcıyı sil (endpoint: DELETE /api/KullanicilarApi/{id})
                var userResp = await _http.DeleteAsync($"/api/KullanicilarApi/{id}");
                if (userResp.IsSuccessStatusCode)
                {
                    await DisplayAlert("Tamam", "Kullanıcı ve yorumları silindi.", "OK");
                    await LoadUsersAsync();
                }
                else
                {
                    // Örn. FK hatası hâlâ gelirse ShowServerError bunu pop-up’ta detaylı gösterir
                    await ShowServerErrorAsync(userResp);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Silme sırasında bir sorun oluştu:\n{ex.Message}", "Tamam");
            }
        }

    }
}
