using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices; // DeviceInfo
using LessArcApppp.Models;

namespace LessArcApppp
{
    public partial class ProjelerimPage : ContentPage
    {
        // ====== DI ve HTTP ======
        private readonly HttpClient _http;
        private readonly string _token;
        private const string CloudFallbackBaseUrl = "https://lessarc.com.tr";

        // ====== Koleksiyonlar ======
        private readonly ObservableCollection<Proje> _projeler = new();
        private readonly ObservableCollection<ProjeAdimi> _adimlar = new();
        private readonly ObservableCollection<YorumDto> _yorumlar = new(); // sadece görüntüleme

        private Proje? _seciliProje;

        // ====== Responsive ölçek ======
        public double BaseWidth { get; set; } = 430.0;          // mobil referans
        public double MaxDesktopScale { get; set; } = 1.85;
        public double MaxMobileScale { get; set; } = 1.30;
        public double DesktopEffectiveWidthCap { get; set; } = 2400;

        private readonly (double w, double s)[] _curve =
        {
            (1000, 1.00), (1280, 1.12), (1440, 1.22),
            (1680, 1.35), (1920, 1.48), (2200, 1.62), (2560, 1.75)
        };

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private double AutoScale(double w)
        {
            if (w <= _curve[0].w) return _curve[0].s;
            if (w >= _curve[^1].w) return _curve[^1].s;
            for (int i = 0; i < _curve.Length - 1; i++)
            {
                var (w1, s1) = _curve[i]; var (w2, s2) = _curve[i + 1];
                if (w >= w1 && w <= w2) return Lerp(s1, s2, (w - w1) / (w2 - w1));
            }
            return 1.0;
        }

        // ---- Binding’e açtığımız property’ler ----
        public double F_Title { get; private set; } = 20;
        public double F_Body { get; private set; } = 16;
        public double F_Small { get; private set; } = 13;

        public double H_Entry { get; private set; } = 46;
        public double H_Button { get; private set; } = 44;
        public double H_CardList { get; private set; } = 260;

        public double C_Radius { get; private set; } = 24;
        public double C_SmallRadius { get; private set; } = 12;
        public double IconSquare { get; private set; } = 28;
        public double IconCorner { get; private set; } = 14;

        public Thickness P_Page { get; private set; } = new(16);
        public Thickness P_CardPad { get; private set; } = new(18);
        public Thickness P_FieldPad { get; private set; } = new(14, 10);
        public Thickness P_ItemPad { get; private set; } = new(12, 10);
        public Thickness P_ButtonPad { get; private set; } = new(18, 12);
        public Thickness P_RowPad { get; private set; } = new(4);

        public double S_L { get; private set; } = 16; // large spacing
        public double S_M { get; private set; } = 10; // medium
        public double S_S { get; private set; } = 8;  // small

        public double W_PopupDesktop { get; private set; } = 500;

        // 🔹 Mobil adım item’lerini büyütmek için yeni property’ler
        public double M_Dot { get; private set; } = 22;// mobil yuvarlak göstergeler (çap)
        public double M_Delete { get; private set; } = 24;  // mobil sil butonu (genişlik/yükseklik)

        // ölçek hesapla
        private void RecomputeScale()
        {
            var w = this.Width;
            if (double.IsNaN(w) || w <= 0) return;

            bool isDesktop = DeviceInfo.Idiom == DeviceIdiom.Desktop;
            double widthDip = isDesktop ? Math.Min(w, DesktopEffectiveWidthCap) : w;

            double scale = isDesktop
                ? Math.Clamp(AutoScale(widthDip), 1.0, MaxDesktopScale)
                : Math.Clamp(widthDip / BaseWidth, 1.0, MaxMobileScale);

            // fontlar
            F_Title = 20 * scale;
            F_Body = 16 * scale;
            F_Small = 13 * scale;

            // boylar
            H_Entry = 46 * scale;
            H_Button = 44 * scale;
            H_CardList = Math.Clamp(260 * scale, 200, 520);

            // köşe/ikon
            C_Radius = Math.Clamp(24 * scale, 18, 36);
            C_SmallRadius = Math.Clamp(12 * scale, 8, 20);
            IconSquare = Math.Clamp(28 * scale, 22, 44);
            IconCorner = IconSquare / 2.0;

            // padding/spacing
            P_Page = new Thickness(16 * scale);
            P_CardPad = new Thickness(18 * scale);
            P_FieldPad = new Thickness(14 * scale, 10 * scale);
            P_ItemPad = new Thickness(12 * scale, 10 * scale);
            P_ButtonPad = new Thickness(18 * scale, 12 * scale);
            P_RowPad = new Thickness(4 * scale);

            // 📱 Mobilde sayfa kenar boşluğu güvenli fren (6–12dp)
            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                var basePad = 16 * scale;
                var clamped = Math.Clamp(basePad, 6, 12);
                var bottom = Math.Min(16, clamped + 2); // gesture bar için minik pay
                P_Page = new Thickness(clamped, clamped, clamped, bottom);

                // 🔸 Adım item’lerini belirgin büyüt
                M_Dot = Math.Clamp(22 * scale * 1.15, 22, 28);      // noktalar
                M_Delete = Math.Clamp(24 * scale * 1.20, 24, 32);   // sil butonu
                P_ItemPad = new Thickness(14 * scale, 12 * scale);  // kart içi dikey padding
                S_S = Math.Max(S_S, 10 * scale);                    // item spacing hafif artış
            }
            else
            {
                // masaüstü/Tablet için varsayılanlar
                M_Dot = Math.Clamp(20 * scale, 18, 26);
                M_Delete = Math.Clamp(24 * scale, 22, 30);
            }

            S_L = 16 * scale;
            S_M = 10 * scale;
            // S_S yukarıda mobilde artırılmış olabilir

            // popup
            W_PopupDesktop = Math.Clamp(500 * scale, 420, 820);

            // notify
            OnPropertyChanged(nameof(F_Title));
            OnPropertyChanged(nameof(F_Body));
            OnPropertyChanged(nameof(F_Small));
            OnPropertyChanged(nameof(H_Entry));
            OnPropertyChanged(nameof(H_Button));
            OnPropertyChanged(nameof(H_CardList));
            OnPropertyChanged(nameof(C_Radius));
            OnPropertyChanged(nameof(C_SmallRadius));
            OnPropertyChanged(nameof(IconSquare));
            OnPropertyChanged(nameof(IconCorner));
            OnPropertyChanged(nameof(P_Page));
            OnPropertyChanged(nameof(P_CardPad));
            OnPropertyChanged(nameof(P_FieldPad));
            OnPropertyChanged(nameof(P_ItemPad));
            OnPropertyChanged(nameof(P_ButtonPad));
            OnPropertyChanged(nameof(P_RowPad));
            OnPropertyChanged(nameof(S_L));
            OnPropertyChanged(nameof(S_M));
            OnPropertyChanged(nameof(S_S));
            OnPropertyChanged(nameof(W_PopupDesktop));

            // 🔔 yeni property’ler
            OnPropertyChanged(nameof(M_Dot));
            OnPropertyChanged(nameof(M_Delete));
        }

        public ProjelerimPage(HttpClient httpClient, string kullaniciToken, string? baseUrlOverride = null)
        {
            // TR yerelleştirme
            CultureInfo.DefaultThreadCurrentCulture = new("tr-TR");
            CultureInfo.DefaultThreadCurrentUICulture = new("tr-TR");

            InitializeComponent();

            // BindingContext: Responsive props için gerekli
            BindingContext = this;

            _token = kullaniciToken ?? string.Empty;
            _http = httpClient;

            // BaseAddress DI’dan geldiyse dokunma; yoksa override → yoksa bulut fallback
            if (_http.BaseAddress is null)
            {
                var effectiveBase = string.IsNullOrWhiteSpace(baseUrlOverride) ? CloudFallbackBaseUrl : baseUrlOverride.Trim();
                _http.BaseAddress = new Uri(effectiveBase, UriKind.Absolute);
            }

            // Authorization header yoksa/boşsa ekle
            var auth = _http.DefaultRequestHeaders.Authorization;
            if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                if (!string.IsNullOrWhiteSpace(_token))
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            }

            // Liste bağlamaları
            lstProjeler.ItemsSource = _projeler;
            lstAdimlar.ItemsSource = _adimlar;

            // Yorum listelerini güvenli bağla
            if (this.FindByName<CollectionView>("lstYorumlarDesktop") is CollectionView yorumlarDesktop)
                yorumlarDesktop.ItemsSource = _yorumlar;

            if (this.FindByName<CollectionView>("lstYorumlarMobile") is CollectionView yorumlarMobile)
                yorumlarMobile.ItemsSource = _yorumlar;

            // İlk ölçek ve görünüm
            RecomputeScale();
            ContentPage_SizeChanged(this, EventArgs.Empty);

            // Verileri çek
            ProjeleriYukle();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Yeniden ölçek ve görünüm sync
            RecomputeScale();
            ContentPage_SizeChanged(this, EventArgs.Empty);
        }

        // Boyut/görünüm değişimi
        private void ContentPage_SizeChanged(object sender, EventArgs e)
        {
            // Responsive hesap
            RecomputeScale();

            // Masaüstü / Mobil toggle
            bool isMobile = DeviceInfo.Current.Idiom == DeviceIdiom.Phone;
            if (this.FindByName<StackLayout>("mobileLayout") is StackLayout m) m.IsVisible = isMobile;
            if (this.FindByName<Grid>("desktopLayout") is Grid d) d.IsVisible = !isMobile;
        }

        // Projeleri getir
        private async void ProjeleriYukle()
        {
            try
            {
                var response = await _http.GetFromJsonAsync<List<Proje>>("/api/Projeler");
                _projeler.Clear();
                if (response != null)
                    foreach (var proje in response)
                        _projeler.Add(proje);

                if (this.FindByName<Picker>("pickerProjeler") is Picker picker)
                    picker.ItemsSource = _projeler;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Projeler alınamadı: {ex.Message}", "Tamam");
            }
        }

        // Adımları getir
        private async void AdimlariYukle(int projeId)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<List<ProjeAdimi>>($"/api/ProjeAdimlari/proje/{projeId}");
                _adimlar.Clear();
                if (response != null)
                    foreach (var adim in response)
                        _adimlar.Add(adim);

                // UI refresh – mobil
                lstAdimlar.ItemsSource = null;
                lstAdimlar.ItemsSource = _adimlar;

                // UI refresh – desktop
                if (this.FindByName<CollectionView>("lstAdimlarDesktop") is CollectionView lstAdimlarDesktop)
                {
                    lstAdimlarDesktop.ItemsSource = null;
                    lstAdimlarDesktop.ItemsSource = _adimlar;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Adımlar alınamadı: {ex.Message}", "Tamam");
            }
        }

        // Yorumları getir (sadece görüntüle)
        private async void YorumlariYukle(int projeId)
        {
            try
            {
                var url = $"/api/Yorumlar/proje/{projeId}?page=1&pageSize=100";
                var response = await _http.GetFromJsonAsync<List<YorumDto>>(url);

                _yorumlar.Clear();
                if (response != null)
                    foreach (var y in response)
                        _yorumlar.Add(y);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Yorumlar alınamadı: {ex.Message}", "Tamam");
            }
        }

        // Seçilen projeye göre etiketleri doldur
        private void DetayEtiketleriniGuncelle()
        {
            if (_seciliProje == null) return;

            string baslangicTxt = (_seciliProje.BaslangicTarihi ?? DateTime.Today).ToString("dd / MM / yyyy");
            string bitisTxt = (_seciliProje.BitisTarihi ?? DateTime.Today).ToString("dd / MM / yyyy");
            string durumTxt = string.IsNullOrWhiteSpace(_seciliProje.Aciklama) ? "Başlamadı" : _seciliProje.Aciklama;

            // Masaüstü
            if (this.FindByName<Label>("lblProjeBaslikDesktop") is Label lblPB) lblPB.Text = _seciliProje.Baslik;
            if (this.FindByName<Label>("lblBaslangicDesktop") is Label lblBD) lblBD.Text = baslangicTxt;
            if (this.FindByName<Label>("lblBitisDesktop") is Label lblBiD) lblBiD.Text = bitisTxt;
            if (this.FindByName<Label>("lblDurumDesktop") is Label lblDD) lblDD.Text = durumTxt;

            // Mobil
            if (this.FindByName<Label>("lblProjeBaslik") is Label lblPM) lblPM.Text = _seciliProje.Baslik;
            if (this.FindByName<Label>("lblBaslangic") is Label lblBM) lblBM.Text = baslangicTxt;
            if (this.FindByName<Label>("lblBitis") is Label lblBiM) lblBiM.Text = bitisTxt;
            if (this.FindByName<Label>("lblDurumMobile") is Label lblDM) lblDM.Text = durumTxt;
        }

        // Masaüstü: proje seçimi
        private void lstProjeler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _seciliProje = e.CurrentSelection.FirstOrDefault() as Proje;
            if (_seciliProje != null)
            {
                DetayEtiketleriniGuncelle();
                AdimlariYukle(_seciliProje.Id);
                YorumlariYukle(_seciliProje.Id);
            }
        }

        // Mobil: proje seçimi
        private void pickerProjeler_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.FindByName<Picker>("pickerProjeler") is Picker picker &&
                picker.SelectedItem is Proje secilen)
            {
                _seciliProje = secilen;
                DetayEtiketleriniGuncelle();
                AdimlariYukle(secilen.Id);
                YorumlariYukle(secilen.Id);
            }
        }

        // “Projeyi Güncelle”
        private async void BtnProjeGuncelle_Clicked(object sender, EventArgs e)
        {
            if (_seciliProje == null)
            {
                await DisplayAlert("Uyarı", "Lütfen bir proje seçin.", "Tamam");
                return;
            }

            var guncelProje = new
            {
                _seciliProje.Id,
                _seciliProje.Baslik,
                _seciliProje.BaslangicTarihi,
                _seciliProje.BitisTarihi,
                Aciklama = _seciliProje.Aciklama, // durum
                _seciliProje.KullaniciId,
                _seciliProje.RenkKodu
            };

            try
            {
                var response = await _http.PutAsJsonAsync($"/api/Projeler/{_seciliProje.Id}", guncelProje);
                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Bilgi", "Proje bilgileri güncellendi.", "Tamam");
                    ProjeleriYukle();
                }
                else
                {
                    var hata = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Hata", $"Güncelleme başarısız: {hata}", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"İstek hatası: {ex.Message}", "Tamam");
            }
        }

        // Adım ekle
        private async void BtnAdimEkle_Clicked(object sender, EventArgs e)
        {
            if (_seciliProje == null)
            {
                await DisplayAlert("Uyarı", "Lütfen bir proje seçin.", "Tamam");
                return;
            }

            string adimBasligi = DeviceInfo.Current.Idiom == DeviceIdiom.Phone
                ? (this.FindByName<Entry>("entryAdim")?.Text ?? "")
                : (this.FindByName<Entry>("entryAdimDesktop")?.Text ?? "");

            if (string.IsNullOrWhiteSpace(adimBasligi))
            {
                await DisplayAlert("Uyarı", "Adım başlığı giriniz.", "Tamam");
                return;
            }

            var yeniAdim = new ProjeAdimi
            {
                ProjeId = _seciliProje.Id,
                AdimBasligi = adimBasligi.Trim(),
                TamamlanmaYuzdesi = 0
            };

            var response = await _http.PostAsJsonAsync("/api/ProjeAdimlari", yeniAdim);
            if (response.IsSuccessStatusCode)
            {
                if (this.FindByName<Entry>("entryAdim") is Entry e1) e1.Text = "";
                if (this.FindByName<Entry>("entryAdimDesktop") is Entry e2) e2.Text = "";
                AdimlariYukle(_seciliProje.Id);
            }
            else
            {
                var hata = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Hata", $"Adım eklenemedi: {hata}", "Tamam");
            }
        }

        // Nokta tıklandı → yüzde güncelle
        private async void Dot_Tapped(object sender, TappedEventArgs e)
        {
            if (sender is Border border && border.BindingContext is ProjeAdimi adim)
            {
                if (e.Parameter is string param && int.TryParse(param, out int index))
                {
                    int yeniYuzde = (index + 1) * 20;

                    var response = await _http.PutAsJsonAsync($"/api/ProjeAdimlari/{adim.Id}", yeniYuzde);
                    if (response.IsSuccessStatusCode)
                    {
                        adim.TamamlanmaYuzdesi = yeniYuzde;

                        // Mobil yenile
                        lstAdimlar.ItemsSource = null;
                        lstAdimlar.ItemsSource = _adimlar;

                        // Masaüstü yenile
                        if (this.FindByName<CollectionView>("lstAdimlarDesktop") is CollectionView lstAdimlarDesktop)
                        {
                            lstAdimlarDesktop.ItemsSource = null;
                            lstAdimlarDesktop.ItemsSource = _adimlar;
                        }
                    }
                    else
                    {
                        var hata = await response.Content.ReadAsStringAsync();
                        await DisplayAlert("Hata", $"Güncelleme başarısız: {hata}", "Tamam");
                    }
                }
            }
        }

        // Adım sil
        private async void BtnAdimSil_Clicked(object sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is int adimId)
            {
                bool onay = await DisplayAlert("Adımı Sil", "Bu adımı silmek istiyor musunuz?", "Evet", "Hayır");
                if (!onay) return;

                var response = await _http.DeleteAsync($"/api/ProjeAdimlari/{adimId}");
                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Başarılı", "Adım silindi.", "Tamam");
                    if (_seciliProje != null)
                        AdimlariYukle(_seciliProje.Id);
                }
                else
                {
                    var hata = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Hata", $"Adım silinemedi: {hata}", "Tamam");
                }
            }
        }
    }
}
