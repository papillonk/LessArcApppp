using LessArcApppp.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace LessArcApppp
{
    public partial class DevamEdenProjelerPage : ContentPage
    {
        // ---------- SCALE ----------
        public double BaseWidth { get; set; } = 430.0;
        public double MaxDesktopScale { get; set; } = 1.85;
        public double MaxMobileScale { get; set; } = 1.30;
        public double? UserZoomFactor { get; set; } = null;
        public double DesktopEffectiveWidthCap { get; set; } = 2200;

        private readonly (double w, double s)[] _desktopScaleCurve =
        {
            (800,1.00),(1000,1.06),(1200,1.12),(1366,1.18),
            (1500,1.28),(1680,1.38),(1920,1.52),(2200,1.66),(2560,1.80),
        };

        public double ScaledTitleFont { get; private set; } = 28;
        public double ScaledSectionFont { get; private set; } = 18;
        public double ScaledFont { get; private set; } = 15;
        public double ScaledFontSmall { get; private set; } = 13;
        public double ScaledInputHeight { get; private set; } = 46;
        public double ScaledIconSmall { get; private set; } = 18;
        public double ScaledCornerRadiusLarge { get; private set; } = 18;
        public double ScaledCornerRadiusMedium { get; private set; } = 12;
        public Thickness ScaledPadding { get; private set; } = new(20);
        public Thickness ScaledSmallPadding { get; private set; } = new(10, 8);
        public Thickness ScaledCardPadding { get; private set; } = new(20);
        public double ScaledSpacing { get; private set; } = 20;
        public double ScaledSmallSpacing { get; private set; } = 10;
        public double ProjectListWidthDesktop { get; private set; } = 700;
        public double SearchPanelWidthDesktop { get; private set; } = 520;

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private double AutoScaleFromCurve(double w)
        {
            if (w <= _desktopScaleCurve[0].w) return _desktopScaleCurve[0].s;
            if (w >= _desktopScaleCurve[^1].w) return _desktopScaleCurve[^1].s;
            for (int i = 0; i < _desktopScaleCurve.Length - 1; i++)
            {
                var (w1, s1) = _desktopScaleCurve[i];
                var (w2, s2) = _desktopScaleCurve[i + 1];
                if (w >= w1 && w <= w2)
                {
                    var t = (w - w1) / (w2 - w1);
                    return Lerp(s1, s2, t);
                }
            }
            return 1.0;
        }

        private void UpdateScale()
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
                scale = Math.Clamp(scale, 1.0, MaxDesktopScale);
            }
            else
            {
                scale = Math.Clamp(widthDip / BaseWidth, 1.0, MaxMobileScale);
            }

            ScaledTitleFont = (isDesktop ? 28 : 22) * scale;
            ScaledSectionFont = 18 * scale;
            ScaledFont = (isDesktop ? 15 : 14) * scale;
            ScaledFontSmall = 13 * scale;
            ScaledInputHeight = (isDesktop ? 46 : 44) * scale;
            ScaledIconSmall = 18 * scale;
            ScaledCornerRadiusLarge = 18 * scale;
            ScaledCornerRadiusMedium = 12 * scale;
            ScaledPadding = new Thickness(20 * scale);
            ScaledSmallPadding = new Thickness(10 * scale, 8 * scale);
            ScaledCardPadding = new Thickness(20 * scale);
            ScaledSpacing = 20 * scale;
            ScaledSmallSpacing = 10 * scale;
            ProjectListWidthDesktop = Math.Clamp(700 * scale, 560, 980);
            SearchPanelWidthDesktop = Math.Clamp(520 * scale, 420, 760);

            bool showDesktop = DeviceInfo.Idiom == DeviceIdiom.Desktop;
            if (desktopLayout != null) desktopLayout.IsVisible = showDesktop;
            if (mobileLayout != null) mobileLayout.IsVisible = !showDesktop;
        }

        // ---------- DATA ----------
        private readonly string _token;
        private readonly HttpClient _http;

        private ObservableCollection<ProjeViewModel> _tum = new();
        public ObservableCollection<ProjeViewModel> FiltreliProjeler { get; } = new();

        private string _secilenYil = "Tüm Yıllar";
        private string _secilenDurum = "Tüm Durumlar";

        public DevamEdenProjelerPage(HttpClient httpClient, string kullaniciToken)
        {
            InitializeComponent();

            _http = httpClient;
            _token = kullaniciToken ?? string.Empty;

            // BaseAddress garantile
            if (_http.BaseAddress == null)
            {
#if ANDROID
                _http.BaseAddress = new Uri("http://10.0.2.2:7013");
#else
                _http.BaseAddress = new Uri("https://localhost:7013");
#endif
            }

            if (!string.IsNullOrWhiteSpace(_token))
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _token);

            BindingContext = this;

            if (entryAra != null) entryAra.TextChanged += entryAra_TextChanged;
            if (entryAraMobile != null) entryAraMobile.TextChanged += entryAra_TextChanged;

            SizeChanged += (_, __) => UpdateScale();

            // picker içerikleri
            var durumlar = new List<string> { "Tüm Durumlar", "Devam Eden", "Başlamamış" };
            pickerDurum.ItemsSource = durumlar;
            pickerDurumMobile.ItemsSource = durumlar;
            pickerDurum.SelectedIndex = 0;
            pickerDurumMobile.SelectedIndex = 0;

            var yılList = BuildYearList();
            pickerYil.ItemsSource = yılList;
            pickerYilMobile.ItemsSource = yılList;
            pickerYil.SelectedIndex = 0;
            pickerYilMobile.SelectedIndex = 0;
        }

        private static List<string> BuildYearList()
        {
            int minYil = DateTime.Now.Year - 5;
            int maxYil = DateTime.Now.Year + 5;
            var yillar = new List<string> { "Tüm Yıllar" };
            yillar.AddRange(Enumerable.Range(minYil, maxYil - minYil + 1).Select(y => y.ToString()));
            return yillar;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            UpdateScale();
            await EnsureAuthAsync();
            await ProjeleriYukleAsync();
        }

        private async Task EnsureAuthAsync()
        {
            var h = _http.DefaultRequestHeaders.Authorization;
            if (h is { Scheme: "Bearer" } && !string.IsNullOrWhiteSpace(h.Parameter)) return;

            var tok = string.IsNullOrWhiteSpace(_token) ? await SecureStorage.GetAsync("auth_token") : _token;
            if (!string.IsNullOrWhiteSpace(tok))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok);
        }

        public class ProjeViewModel
        {
            public int Id { get; set; }
            public string Baslik { get; set; } = "";
            public string? KullaniciAdSoyad { get; set; } = "-";
            public DateTime? BaslangicTarihi { get; set; }
            public DateTime? BitisTarihi { get; set; }
            // "devam" | "baslamamis" | "diger"
            public string DurumKategori { get; set; } = "diger";

            public string BaslangicTarihiFormatted =>
                BaslangicTarihi.HasValue
                    ? BaslangicTarihi.Value.ToString("dd MMMM yyyy", new CultureInfo("tr-TR"))
                    : "-";
        }

        private async Task ProjeleriYukleAsync()
        {
            try
            {
                var list = await _http.GetFromJsonAsync<List<AdminProjeListDto>>("api/Projeler/tum-projeler-detayli")
                           ?? new List<AdminProjeListDto>();

                string MapDurum(AdminProjeListDto p)
                {
                    var d = p?.Durum?.Trim().ToLowerInvariant() ?? "";
                    var today = DateTime.Today;

                    if (d.Contains("devam")) return "devam";
                    if (d.Contains("başlam") || d.Contains("baslam")) return "baslamamis";
                    if (d.Contains("tamamlan")) return "diger";

                    if (!p?.BaslangicTarihi.HasValue ?? true) return "baslamamis";
                    if (!p.BitisTarihi.HasValue) return "devam";
                    if (p.BitisTarihi.Value.Date > today) return "devam";
                    return "diger";
                }

                var vm = list.Select(p => new ProjeViewModel
                {
                    Id = p.Id,
                    Baslik = p.Baslik,
                    KullaniciAdSoyad = string.IsNullOrWhiteSpace(p.KullaniciAdSoyad) ? "-" : p.KullaniciAdSoyad,
                    BaslangicTarihi = p.BaslangicTarihi,
                    BitisTarihi = p.BitisTarihi,
                    DurumKategori = MapDurum(p)
                })
                .Where(x => x.DurumKategori is "devam" or "baslamamis")
                .ToList();

                _tum = new ObservableCollection<ProjeViewModel>(vm);

                FiltreliProjeler.Clear();
                foreach (var item in _tum) FiltreliProjeler.Add(item);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Projeler yüklenirken sorun oluştu.\n{ex.Message}", "Tamam");
            }
        }

        // ====== EVENTLAR ======
        private void ContentPage_SizeChanged(object sender, EventArgs e) => UpdateScale();

        private void entryAra_TextChanged(object sender, TextChangedEventArgs e) => Filtrele();

        private void pickerYil_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is Picker p) _secilenYil = p.SelectedItem?.ToString() ?? "Tüm Yıllar";
            Filtrele();
        }

        private void pickerDurum_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is Picker p) _secilenDurum = p.SelectedItem?.ToString() ?? "Tüm Durumlar";
            Filtrele();
        }

        private void Filtrele()
        {
            string kelime = (mobileLayout?.IsVisible == true ? entryAraMobile?.Text : entryAra?.Text) ?? "";
            kelime = kelime.Trim().ToLowerInvariant();

            var filtered = _tum.Where(p =>
            {
                bool textOk =
                    string.IsNullOrWhiteSpace(kelime) ||
                    (p.Baslik?.ToLower().Contains(kelime) ?? false) ||
                    ((p.KullaniciAdSoyad ?? "-").ToLower().Contains(kelime));

                bool yearOk = true;
                if (_secilenYil != "Tüm Yıllar")
                {
                    bool by = p.BaslangicTarihi.HasValue && p.BaslangicTarihi.Value.Year.ToString() == _secilenYil;
                    bool ey = p.BitisTarihi.HasValue && p.BitisTarihi.Value.Year.ToString() == _secilenYil;
                    yearOk = by || ey;
                }

                bool durumOk = true;
                if (_secilenDurum == "Devam Eden") durumOk = p.DurumKategori == "devam";
                else if (_secilenDurum == "Başlamamış") durumOk = p.DurumKategori == "baslamamis";

                return textOk && yearOk && durumOk;
            }).ToList();

            FiltreliProjeler.Clear();
            foreach (var item in filtered) FiltreliProjeler.Add(item);
        }
    }
}
