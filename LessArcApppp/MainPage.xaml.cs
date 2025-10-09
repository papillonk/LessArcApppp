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

        // Constructor
        public MainPage(HttpClient httpClient, string? baseUrlOverride = null)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            BackgroundImageSource = "arkaplann.png";

            _client = httpClient ?? new HttpClient();

            if (_client.BaseAddress is null)
            {
                var effective = string.IsNullOrWhiteSpace(baseUrlOverride)
                    ? "https://lessarc.com.tr"
                    : baseUrlOverride.Trim();
                _client.BaseAddress = new Uri(effective, UriKind.Absolute);
            }

            if (!_client.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            ToggleLayoutsByDevice();

            // XAML'de de bağlı ama güvence olsun:
            SizeChanged += ContentPage_SizeChanged;

            // Sayfa yüklendiğinde kayıtlı bilgileri doldur
            Loaded += async (_, __) => await LoadSavedCredentialsAsync();
        }

        // ====== Layout seçimi ======
        void ToggleLayoutsByDevice()
        {
            var idiom = DeviceInfo.Idiom;
            bool isPhone = idiom == DeviceIdiom.Phone;

            // XAML tarafında mobileLayout / desktopLayout adları korunmalı
            if (mobileLayout != null) mobileLayout.IsVisible = isPhone;
            if (desktopLayout != null) desktopLayout.IsVisible = !isPhone;
        }

        // ====== SizeChanged handler ======
        void ContentPage_SizeChanged(object sender, EventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;

            bool isPhone = DeviceInfo.Idiom == DeviceIdiom.Phone;

            if (isPhone)
            {
                // Mobil: gridCanvas tüm ekranı kapsasın; kartı ortala
                gridCanvas.Scale = 1;
                gridCanvas.WidthRequest = Width;
                gridCanvas.HeightRequest = Height;

                if (mobileCard != null)
                    mobileCard.WidthRequest = Math.Clamp(Width * 0.90, 320, 420);

                // kesinlikle ortala (XAML ayarıyla birlikte)
                if (mobileCard != null)
                {
                    mobileCard.HorizontalOptions = LayoutOptions.Center;
                    mobileCard.VerticalOptions = LayoutOptions.Center;
                }

                // karartma ve panelin tüm alanı kaplamasını sağla
                if (arkaplanMobile != null)
                {
                    arkaplanMobile.HorizontalOptions = LayoutOptions.Fill;
                    arkaplanMobile.VerticalOptions = LayoutOptions.Fill;
                }
                if (sifreUnuttumPanelMobile != null)
                {
                    sifreUnuttumPanelMobile.HorizontalOptions = LayoutOptions.Center;
                    sifreUnuttumPanelMobile.VerticalOptions = LayoutOptions.Center;
                }

                return;
            }

            // Desktop: eski davranış (ViewBox benzeri)
            double scaleW = Width / DesignW;
            double scaleH = Height / DesignH;
            double scale = Math.Clamp(Math.Min(scaleW, scaleH), MinScale, MaxScale);

            gridCanvas.Scale = scale;

            if (this.FindByName<Frame>("loginCard") is Frame card)
                card.WidthRequest = Math.Clamp(Width * 0.32, 420, 520);
        }

        // ======================================================
        // ORTAK: Giriş isteği + yanıtı işleme (masaüstü/mobil)
        // ======================================================
        private async Task HandleLoginAsync(string kullaniciAdi, string sifre, Label? hataLabel = null)
        {
            var loginModel = new Models.LoginModel
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
                var tokenResponse = JsonSerializer.Deserialize<Models.TokenResponse>(
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
                kullaniciAdi: txtKullaniciAdi?.Text,
                sifre: txtSifre?.Text,
                hataLabel: lblHata
            );
        }

        // ==========================
        // MASAÜSTÜ: Şifre Sıfırlama
        // ==========================
        // Bu method XAML içinde: Clicked="BtnSifreUnuttum_Toggle"
        private void BtnSifreUnuttum_Toggle(object sender, EventArgs e)
        {
            bool yeniDurum = !(sifreUnuttumPanel?.IsVisible ?? false);
            if (sifreUnuttumPanel != null) sifreUnuttumPanel.IsVisible = yeniDurum;
            if (arkaplan != null) arkaplan.IsVisible = yeniDurum;
        }

        // Bu method XAML içinde: Clicked="BtnKodGonder_Clicked"
        private async void BtnKodGonder_Clicked(object sender, EventArgs e)
        {
            string eposta = txtSifreResetEposta?.Text?.Trim();
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

        // Bu method XAML içinde: Clicked="BtnSifreSifirla_Clicked"
        private async void BtnSifreSifirla_Clicked(object sender, EventArgs e)
        {
            string eposta = txtSifreResetEposta?.Text?.Trim();
            string kod = txtKod?.Text?.Trim();
            string yeniSifre = txtYeniSifreReset?.Text;

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
                if (sifreUnuttumPanel != null) sifreUnuttumPanel.IsVisible = false;
                if (arkaplan != null) arkaplan.IsVisible = false;
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
                kullaniciAdi: txtKullaniciAdiMobile?.Text,
                sifre: txtSifreMobile?.Text,
                hataLabel: lblHataMobile
            );
        }

        // ==========================
        // MOBİL: Şifre Sıfırlama toggle
        // ==========================
        // XAML: Clicked="BtnSifreUnuttum_ToggleMobile"
        private void BtnSifreUnuttum_ToggleMobile(object sender, EventArgs e)
        {
            bool yeniDurum = !(sifreUnuttumPanelMobile?.IsVisible ?? false);
            if (sifreUnuttumPanelMobile != null) sifreUnuttumPanelMobile.IsVisible = yeniDurum;
            if (arkaplanMobile != null) arkaplanMobile.IsVisible = yeniDurum;
        }

        // XAML: Clicked="BtnKodGonderMobile_Clicked"
        private async void BtnKodGonderMobile_Clicked(object sender, EventArgs e)
        {
            string eposta = txtSifreResetEpostaMobile?.Text?.Trim();
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

        // XAML: Clicked="BtnSifreSifirlaMobile_Clicked"
        private async void BtnSifreSifirlaMobile_Clicked(object sender, EventArgs e)
        {
            string eposta = txtSifreResetEpostaMobile?.Text?.Trim();
            string kod = txtKodMobile?.Text?.Trim();
            string yeniSifre = txtYeniSifreResetMobile?.Text;

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
                if (sifreUnuttumPanelMobile != null) sifreUnuttumPanelMobile.IsVisible = false;
                if (arkaplanMobile != null) arkaplanMobile.IsVisible = false;
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
