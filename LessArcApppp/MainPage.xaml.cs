using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;          // Preferences & SecureStorage
using LessArcApppp.Models;

namespace LessArcApppp
{
    public partial class MainPage : ContentPage
    {
        private readonly HttpClient _client;

        // ====== ViewBox benzeri ölçekleme sabitleri ======
        const double DesignW = 1100;   // gridCanvas.WidthRequest
        const double DesignH = 650;    // gridCanvas.HeightRequest
        const double MinScale = 0.65;
        const double MaxScale = 1.20;

        // 📍 Mobil kart konum hedefi (ekran yüksekliğinin oranı)
        // 0.60 = ekranın %60’ında merkez; daha da aşağı için 0.62–0.68 deneyin
        const double DesiredCenterYRatioMobile = 0.67;

        // ---- Anahtarlar (Preferences / SecureStorage) ----
        private const string PREF_USER = "login_saved_user";
        private const string PREF_REMEMBER = "login_remember_me";
        private const string SEC_PASS = "login_saved_pass";
        private const string SEC_TOKEN = "jwt_token";

        // İstersen test için override verilebilir (emülatörde "http://10.0.2.2:7013" gibi)
        public MainPage(HttpClient httpClient, string? baseUrlOverride = null)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            // Arka plan (opsiyonel)
            BackgroundImageSource = "arkaplann.png";

            // Masaüstü/Mobil görünümü ayır
            ToggleLayoutsByDevice();

            // XAML'de de bağlı ama güvence olsun:
            SizeChanged += ContentPage_SizeChanged;

            // 🔗 DI’dan gelen HttpClient
            _client = httpClient;

            // DI’da BaseAddress yoksa: override → yoksa bulut
            if (_client.BaseAddress is null)
            {
                var effective = string.IsNullOrWhiteSpace(baseUrlOverride)
                    ? "https://lessarc.com.tr"
                    : baseUrlOverride.Trim();
                _client.BaseAddress = new Uri(effective, UriKind.Absolute);
            }

            // JSON kabul başlığı (gerekirse)
            if (!_client.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // ✅ Sayfa yüklendiğinde kayıtlı bilgileri doldur
            Loaded += async (_, __) => await LoadSavedCredentialsAsync();
        }

        // ====== Layout seçimi ======
        void ToggleLayoutsByDevice()
        {
            var idiom = DeviceInfo.Idiom;
            bool isPhone = idiom == DeviceIdiom.Phone;
            mobileLayout.IsVisible = isPhone;
            desktopLayout.IsVisible = !isPhone;
        }

        // ====== ViewBox ölçekleme (MOBİL: yok, DESKTOP: var) ======
        void ContentPage_SizeChanged(object sender, EventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;

            bool isPhone = DeviceInfo.Idiom == DeviceIdiom.Phone;

            if (isPhone)
            {
                // 📱 Mobilde ViewBox ölçekleme YOK → 1:1 kalsın
                gridCanvas.Scale = 1;
                gridCanvas.WidthRequest = Width;
                gridCanvas.HeightRequest = Height;

                // Mobile giriş kartı: ekranın ~%90’ı, makul sınırlar içinde
                if (this.FindByName<Frame>("mobileCard") is Frame mcard)
                    mcard.WidthRequest = Math.Clamp(Width * 0.90, 320, 420);

                // ➜ Kartı ekranda aşağı konumlandır
                PositionMobileCard();
                return;
            }

            // 💻 Masaüstü/laptop: ViewBox ölçekleme aktif
            double scaleW = Width / DesignW;
            double scaleH = Height / DesignH;
            double scale = Math.Clamp(Math.Min(scaleW, scaleH), MinScale, MaxScale);

            gridCanvas.Scale = scale;

            // Desktop giriş kartı makul aralıkta kalsın
            if (this.FindByName<Frame>("loginCard") is Frame card)
                card.WidthRequest = Math.Clamp(Width * 0.32, 420, 520);
        }

        // 📍 Mobil kartı ekranda daha aşağı hizala
        private void PositionMobileCard()
        {
            if (mobileLayout == null || !mobileLayout.IsVisible) return;

            // Ölçü kaynağı: gridCanvas varsa onu, yoksa sayfanın toplam yüksekliğini kullan
            double containerHeight = gridCanvas?.Height > 0 ? gridCanvas.Height : this.Height;
            if (containerHeight <= 0) return;

            // iOS çentik/safe-area için küçük bir pay bırak (Android’de genelde gerek olmuyor)
            double safeTopPad = DeviceInfo.Platform == DevicePlatform.iOS ? 12 : 0;

            // Kart yüksekliği: ilk ölçüm gelene kadar yaklaşık
            double cardHeight = mobileCard?.Height > 0 ? mobileCard.Height : 360;

            // Kartın merkezi ekranın DesiredCenterYRatioMobile oranına gelsin:
            // top boşluk piksel = hedefMerkezY - kartYarısı
            double targetCenterY = containerHeight * DesiredCenterYRatioMobile;
            double topPixels = Math.Max(0, targetCenterY - (cardHeight / 2) + safeTopPad);

            // mobileLayout: RowDefinitions = "*,Auto,*" olmalı
            var rows = mobileLayout.RowDefinitions;
            if (rows.Count == 3)
            {
                rows[0].Height = new GridLength(topPixels, GridUnitType.Absolute);
                rows[1].Height = GridLength.Auto;
                rows[2].Height = GridLength.Star;
            }

            // Kartın altına küçük boşluk
            if (mobileCard != null)
            {
                var m = mobileCard.Margin;
                mobileCard.Margin = new Thickness(m.Left, m.Top, m.Right, 24);
            }

            // Karartma / şifre paneli tüm alanı kaplasın
            if (arkaplanMobile != null)
            {
                Grid.SetRow(arkaplanMobile, 0);
                Grid.SetRowSpan(arkaplanMobile, 3);
            }
            if (sifreUnuttumPanelMobile != null)
            {
                Grid.SetRow(sifreUnuttumPanelMobile, 0);
                Grid.SetRowSpan(sifreUnuttumPanelMobile, 3);
            }
        }

        // ======================================================
        // ORTAK: Giriş isteği + yanıtı işleme (masaüstü/mobil)
        // ======================================================
        private async Task HandleLoginAsync(string kullaniciAdi, string sifre, Label? hataLabel = null)
        {
            var loginModel = new LoginModel
            {
                KullaniciAdi = kullaniciAdi?.Trim(),
                Password = sifre
            };

            try
            {
                var json = JsonSerializer.Serialize(loginModel);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("/api/Auth/login", content);

                if (!response.IsSuccessStatusCode)
                {
                    if (hataLabel != null)
                    {
                        hataLabel.Text = "❗ Kullanıcı adı veya şifre hatalı!";
                        hataLabel.IsVisible = true;
                    }
                    else
                    {
                        await DisplayAlert("Hata", "Kullanıcı adı veya şifre hatalı!", "Tamam");
                    }
                    return;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (tokenResponse == null ||
                    string.IsNullOrWhiteSpace(tokenResponse.Token) ||
                    string.IsNullOrWhiteSpace(tokenResponse.Rol))
                {
                    await DisplayAlert("Hata", "Geçersiz oturum yanıtı (token/rol).", "Tamam");
                    return;
                }

                if (hataLabel != null) hataLabel.IsVisible = false;

                _client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", tokenResponse.Token);

                await SecureStorage.SetAsync(SEC_TOKEN, tokenResponse.Token);

                bool remember = DeviceInfo.Idiom == DeviceIdiom.Desktop
                                ? (chkBeniHatirlaDesktop?.IsChecked ?? false)
                                : (chkBeniHatirlaMobile?.IsChecked ?? false);

                if (remember)
                {
                    Preferences.Set(PREF_REMEMBER, true);
                    Preferences.Set(PREF_USER, kullaniciAdi ?? string.Empty);
                    await SecureStorage.SetAsync(SEC_PASS, sifre ?? string.Empty);
                }
                else
                {
                    Preferences.Remove(PREF_REMEMBER);
                    Preferences.Remove(PREF_USER);
                    SecureStorage.Remove(SEC_PASS);
                }

                var rol = tokenResponse.Rol.Trim().ToLowerInvariant();
                if (rol == "admin")
                {
                    await Navigation.PushAsync(new AdminEkrani(_client, tokenResponse.Token));
                }
                else if (rol == "calisan")
                {
                    await Navigation.PushAsync(new CalisanEkrani(_client, tokenResponse.Token));
                }
                else
                {
                    await DisplayAlert("Uyarı", $"Bilinmeyen rol: {tokenResponse.Rol}", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Bağlantı Hatası", $"Sunucuya ulaşılamadı: {ex.Message}", "Tamam");
            }
        }

        // ==========================
        // MASAÜSTÜ: Giriş
        // ==========================
        private async void BtnGirisYap_Clicked(object sender, EventArgs e)
        {
            await HandleLoginAsync(
                kullaniciAdi: txtKullaniciAdi.Text,
                sifre: txtSifre.Text,
                hataLabel: lblHata
            );
        }

        // ==========================
        // MASAÜSTÜ: Şifre Sıfırlama
        // ==========================
        private void BtnSifreUnuttum_Toggle(object sender, EventArgs e)
        {
            bool yeniDurum = !sifreUnuttumPanel.IsVisible;
            sifreUnuttumPanel.IsVisible = yeniDurum;
            arkaplan.IsVisible = yeniDurum;
        }

        private async void BtnKodGonder_Clicked(object sender, EventArgs e)
        {
            string eposta = txtSifreResetEposta.Text?.Trim();
            if (string.IsNullOrWhiteSpace(eposta))
            {
                await DisplayAlert("Hata", "Lütfen e-posta adresinizi girin.", "Tamam");
                return;
            }

            var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
            var json = JsonSerializer.Serialize(new { Eposta = eposta }, options);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/Auth/sifremi-unuttum", content);

            if (response.IsSuccessStatusCode)
                await DisplayAlert("Başarılı", "Doğrulama kodu e-posta adresinize gönderildi.", "Tamam");
            else
                await DisplayAlert("Hata", $"Kod gönderilemedi: {await response.Content.ReadAsStringAsync()}", "Tamam");
        }

        private async void BtnSifreSifirla_Clicked(object sender, EventArgs e)
        {
            string eposta = txtSifreResetEposta.Text?.Trim();
            string kod = txtKod.Text?.Trim();
            string yeniSifre = txtYeniSifreReset.Text;

            if (string.IsNullOrWhiteSpace(eposta) ||
                string.IsNullOrWhiteSpace(kod) ||
                string.IsNullOrWhiteSpace(yeniSifre))
            {
                await DisplayAlert("Hata", "Lütfen tüm alanları doldurun.", "Tamam");
                return;
            }

            var data = new { Eposta = eposta, Kod = kod, YeniSifre = yeniSifre };
            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/Auth/sifre-sifirla", content);

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Başarılı", "Şifreniz başarıyla sıfırlandı. Giriş yapabilirsiniz.", "Tamam");
                sifreUnuttumPanel.IsVisible = false;
                arkaplan.IsVisible = false;
            }
            else
            {
                await DisplayAlert("Hata", "Şifre sıfırlama başarısız. Bilgilerinizi kontrol edin.", "Tamam");
            }
        }

        // ==========================
        // MOBİL: Giriş
        // ==========================
        private async void BtnGirisYapMobile_Clicked(object sender, EventArgs e)
        {
            await HandleLoginAsync(
                kullaniciAdi: txtKullaniciAdiMobile.Text,
                sifre: txtSifreMobile.Text,
                hataLabel: lblHataMobile
            );
        }

        // ==========================
        // MOBİL: Şifre Sıfırlama
        // ==========================
        private void BtnSifreUnuttum_ToggleMobile(object sender, EventArgs e)
        {
            bool yeniDurum = !sifreUnuttumPanelMobile.IsVisible;
            sifreUnuttumPanelMobile.IsVisible = yeniDurum;
            arkaplanMobile.IsVisible = yeniDurum;
        }

        private async void BtnKodGonderMobile_Clicked(object sender, EventArgs e)
        {
            string eposta = txtSifreResetEpostaMobile.Text?.Trim();
            if (string.IsNullOrWhiteSpace(eposta))
            {
                await DisplayAlert("Hata", "Lütfen e-posta adresinizi girin.", "Tamam");
                return;
            }

            var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
            var json = JsonSerializer.Serialize(new { Eposta = eposta }, options);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/Auth/sifremi-unuttum", content);

            if (response.IsSuccessStatusCode)
                await DisplayAlert("Başarılı", "Doğrulama kodu e-posta adresinize gönderildi.", "Tamam");
            else
                await DisplayAlert("Hata", $"Kod gönderilemedi: {await response.Content.ReadAsStringAsync()}", "Tamam");
        }

        private async void BtnSifreSifirlaMobile_Clicked(object sender, EventArgs e)
        {
            string eposta = txtSifreResetEpostaMobile.Text?.Trim();
            string kod = txtKodMobile.Text?.Trim();
            string yeniSifre = txtYeniSifreResetMobile.Text;

            if (string.IsNullOrWhiteSpace(eposta) ||
                string.IsNullOrWhiteSpace(kod) ||
                string.IsNullOrWhiteSpace(yeniSifre))
            {
                await DisplayAlert("Hata", "Lütfen tüm alanları doldurun.", "Tamam");
                return;
            }

            var data = new { Eposta = eposta, Kod = kod, YeniSifre = yeniSifre };
            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/Auth/sifre-sifirla", content);

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Başarılı", "Şifreniz başarıyla sıfırlandı. Giriş yapabilirsiniz.", "Tamam");
                sifreUnuttumPanelMobile.IsVisible = false;
                arkaplanMobile.IsVisible = false;
            }
            else
            {
                await DisplayAlert("Hata", "Şifre sıfırlama başarısız. Bilgilerinizi kontrol edin.", "Tamam");
            }
        }

        // ==========================
        // Kayıtlı bilgileri doldurma
        // ==========================
        private async Task LoadSavedCredentialsAsync()
        {
            try
            {
                var remember = Preferences.Get(PREF_REMEMBER, false);
                if (chkBeniHatirlaDesktop != null) chkBeniHatirlaDesktop.IsChecked = remember;
                if (chkBeniHatirlaMobile != null) chkBeniHatirlaMobile.IsChecked = remember;

                if (remember)
                {
                    var savedUser = Preferences.Get(PREF_USER, string.Empty);
                    var savedPass = await SecureStorage.GetAsync(SEC_PASS) ?? string.Empty;

                    if (txtKullaniciAdi != null) txtKullaniciAdi.Text = savedUser;
                    if (txtSifre != null) txtSifre.Text = savedPass;

                    if (txtKullaniciAdiMobile != null) txtKullaniciAdiMobile.Text = savedUser;
                    if (txtSifreMobile != null) txtSifreMobile.Text = savedPass;
                }
            }
            catch
            {
                // SecureStorage reddedilirse sessiz geç
            }
        }
    }
}

/* --- Kullanılan DTO örnekleri ---
namespace LessArcApppp.Models
{
    public class LoginModel
    {
        public string? KullaniciAdi { get; set; }
        public string? Password { get; set; }
    }

    public class TokenResponse
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; } = "";
        public string Ad { get; set; } = "";
        public string Soyad { get; set; } = "";
        public string Eposta { get; set; } = "";
        public string Rol { get; set; } = "";     // "Admin" | "Calisan"
        public string Token { get; set; } = "";
    }
}
*/
