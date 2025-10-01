using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LessArcApppp.Models;
using Microsoft.Maui.Controls;
using Microcharts;
using Newtonsoft.Json;
using SkiaSharp;
using Microsoft.Maui.Devices;

namespace LessArcApppp
{
    // 🔹 API'den gelecek günlük ilerleme DTO'su
    public class GunlukIlerlemeDto
    {
        public DateTime Tarih { get; set; }
        public double OrtalamaYuzde { get; set; }
    }

    public partial class AdminCalisanRaporPage : ContentPage
    {
        private readonly string token;
        private readonly HttpClient _http;

        private DateTime _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        private CalisanRaporDto? _aktifCalisan;

        private const float EPS = 0.0001f;
        private const double MobileSideEdge = 16;
        private bool _centerHandlersAttached = false;

        private static readonly JsonSerializerSettings JsonSafeSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            DateParseHandling = DateParseHandling.DateTime
        };

        // ================== 🔥 ÖLÇEKLEME (Aynı mantık) ==================
        public double BaseWidth { get; set; } = 430.0;
        public double MaxDesktopScale { get; set; } = 1.85;
        public double MaxMobileScale { get; set; } = 1.30;
        public double? UserZoomFactor { get; set; } = null;
        public double DesktopEffectiveWidthCap { get; set; } = 2200;

        // (genişlik_DIP, hedef_scale)
        private readonly (double w, double s)[] _desktopScaleCurve =
        {
            (  800, 1.00),
            ( 1000, 1.06),
            ( 1200, 1.12),  // 15"
            ( 1366, 1.18),  // 15.6"
            ( 1500, 1.28),  // 17"
            ( 1680, 1.38),  // 20-21"
            ( 1920, 1.52),  // 24"
            ( 2200, 1.66),  // 27"
            ( 2560, 1.80),  // 27"+/2K
        };

        // Sayfa-geneli ölçekli değerler
        public double ScaledFontSize { get; private set; } = 26;
        public double ScaledFontSize2 { get; private set; } = 20;
        public double ScaledFontSize3 { get; private set; } = 14;
        public double ScaledFontSize4 { get; private set; } = 12;

        public double ScaledPadding { get; private set; } = 15;
        public double ScaledSmallPadding { get; private set; } = 10;
        public double ScaledSpacing { get; private set; } = 10;
        public double ScaledSmallSpacing { get; private set; } = 6;

        public double ChartWidth { get; private set; } = 300;
        public double ChartHeight { get; private set; } = 200;

        public double NavBtnSize { get; private set; } = 36;
        public double NavChevronSize { get; private set; } = 35;

        public AdminCalisanRaporPage(HttpClient httpClient, string kullaniciToken)
        {
            InitializeComponent();

            _http = httpClient;
            token = kullaniciToken ?? string.Empty;

            // BindingContext
            BindingContext = this;

            var auth = _http.DefaultRequestHeaders.Authorization;
            if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                if (!string.IsNullOrWhiteSpace(token))
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            _ = CalisanRaporlariniGetir();

            UpdateMonthLabels();
            ResetCharts();

            SizeChanged += (_, __) => RecomputeScale();
            RecomputeScale();
        }

        // ========= ÖLÇEK HESAPLAMA =========
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private double AutoScaleFromCurve(double widthDip)
        {
            var c = _desktopScaleCurve;
            if (widthDip <= c[0].w) return c[0].s;
            if (widthDip >= c[^1].w) return c[^1].s;

            for (int i = 0; i < c.Length - 1; i++)
            {
                var (w1, s1) = c[i];
                var (w2, s2) = c[i + 1];
                if (widthDip >= w1 && widthDip <= w2)
                {
                    double t = (widthDip - w1) / (w2 - w1);
                    return Lerp(s1, s2, t);
                }
            }
            return c[^1].s;
        }

        private void RecomputeScale()
        {
            double w = Width;
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

            // font/padding/spacing
            ScaledFontSize = 26 * scale;
            ScaledFontSize2 = 20 * scale;
            ScaledFontSize3 = 14 * scale;
            ScaledFontSize4 = 12 * scale;

            ScaledPadding = 15 * scale;
            ScaledSmallPadding = 10 * scale;
            ScaledSpacing = 10 * scale;
            ScaledSmallSpacing = 6 * scale;

            // grafik ve navigasyon butonları
            ChartWidth = 300 * scale;
            ChartHeight = 200 * scale;
            NavBtnSize = Math.Max(30, 36 * scale);
            NavChevronSize = Math.Max(24, 32 * scale);

            try
            {
                if (chartView != null)
                {
                    chartView.WidthRequest = ChartWidth;
                    chartView.HeightRequest = ChartHeight;
                }
                if (lineChartView != null)
                {
                    lineChartView.HeightRequest = Math.Max(180, ChartHeight);
                }

                if (chartViewMobile != null)
                {
                    chartViewMobile.WidthRequest = Math.Max(260, ChartWidth * 0.9);
                    chartViewMobile.HeightRequest = Math.Max(180, ChartHeight);
                }
                if (lineChartViewMobile != null)
                {
                    lineChartViewMobile.HeightRequest = Math.Max(200, ChartHeight * 1.1);
                }

                if (lblAyBaslik != null) lblAyBaslik.FontSize = ScaledFontSize3;
                if (lblAyBaslikMobile != null) lblAyBaslikMobile.FontSize = ScaledFontSize3;

                if (btnAyOnceki != null)
                {
                    btnAyOnceki.WidthRequest = NavBtnSize;
                    btnAyOnceki.HeightRequest = NavBtnSize;
                    btnAyOnceki.FontSize = NavChevronSize;
                }
                if (btnAySonraki != null)
                {
                    btnAySonraki.WidthRequest = NavBtnSize;
                    btnAySonraki.HeightRequest = NavBtnSize;
                    btnAySonraki.FontSize = NavChevronSize;
                }
                if (btnAyOncekiMobile != null)
                {
                    btnAyOncekiMobile.WidthRequest = Math.Max(32, NavBtnSize * 0.9);
                    btnAyOncekiMobile.HeightRequest = Math.Max(32, NavBtnSize * 0.9);
                    btnAyOncekiMobile.FontSize = Math.Max(24, NavChevronSize * 0.9);
                }
                if (btnAySonrakiMobile != null)
                {
                    btnAySonrakiMobile.WidthRequest = Math.Max(32, NavBtnSize * 0.9);
                    btnAySonrakiMobile.HeightRequest = Math.Max(32, NavBtnSize * 0.9);
                    btnAySonrakiMobile.FontSize = Math.Max(24, NavChevronSize * 0.9);
                }

                if (lblAdSoyad != null) lblAdSoyad.FontSize = ScaledFontSize3;
                if (lblToplam != null) lblToplam.FontSize = ScaledFontSize3;
                if (lblDevam != null) lblDevam.FontSize = ScaledFontSize3;
                if (lblTamam != null) lblTamam.FontSize = ScaledFontSize3;
                if (lblBaslamayan != null) lblBaslamayan.FontSize = ScaledFontSize3;

                if (lblAdSoyadMobile != null) lblAdSoyadMobile.FontSize = ScaledFontSize3;
                if (lblToplamMobile != null) lblToplamMobile.FontSize = ScaledFontSize3;
                if (lblDevamMobile != null) lblDevamMobile.FontSize = ScaledFontSize3;
                if (lblTamamMobile != null) lblTamamMobile.FontSize = ScaledFontSize3;
                if (lblBaslamayanMobile != null) lblBaslamayanMobile.FontSize = ScaledFontSize3;
            }
            catch { }

            if (_aktifCalisan != null)
            {
                BuildAndSetStatusCharts(_aktifCalisan);
                _ = BuildAndSetMonthlyLineChartAsync(_aktifCalisan);
            }

            ApplyMobileEdgeSizing();
        }

        // ========= Orijinal mantık (veri, olaylar, çizimler) =========

        private async Task CalisanRaporlariniGetir()
        {
            try
            {
                var response = await _http.GetAsync("/api/Raporlar/CalisanBazli");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var calisanlar = JsonConvert.DeserializeObject<List<CalisanRaporDto>>(json, JsonSafeSettings) ?? new();

                    foreach (var c in calisanlar)
                        c.Projeler ??= new List<CalisanProjeDto>();

                    await Dispatcher.DispatchAsync(() =>
                    {
                        calisanListesi.ItemsSource = null;
                        pickerCalisanMobile.ItemsSource = null;

                        calisanListesi.ItemsSource = calisanlar;
                        pickerCalisanMobile.ItemsSource = calisanlar;

                        if (calisanlar.Count > 0)
                        {
                            calisanListesi.SelectedItem = calisanlar[0];
                            pickerCalisanMobile.SelectedIndex = 0;
                        }
                        else
                        {
                            projeDetayListesi.ItemsSource = null;
                            projeDetayListesi.IsVisible = false;
                            projeDetayListesiMobile.ItemsSource = null;
                            projeDetayListesiMobile.IsVisible = false;
                        }
                    });
                }
                else
                {
                    await DisplayAlert("Hata", $"Rapor verileri alınamadı. (HTTP {(int)response.StatusCode})", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Bir hata oluştu: {ex.Message}", "Tamam");
            }
        }

        private async void calisanListesi_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection?.FirstOrDefault() is not CalisanRaporDto secilen)
                return;

            _aktifCalisan = secilen;

            ResetCharts();
            UpdateDesktopDetailUI(secilen);

            var projeler = secilen.Projeler ?? new List<CalisanProjeDto>();
            projeDetayListesi.ItemsSource = null;
            projeDetayListesi.ItemsSource = projeler;
            projeDetayListesi.IsVisible = projeler.Count > 0;

            BuildAndSetStatusCharts(secilen);
            await BuildAndSetMonthlyLineChartAsync(secilen);

            Dispatcher.Dispatch(ApplyMobileEdgeSizing);
        }

        private async void pickerCalisanMobile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is not Picker picker || picker.SelectedItem is not CalisanRaporDto secilen)
                return;

            _aktifCalisan = secilen;

            ResetCharts();
            UpdateMobileDetailUI(secilen);

            var projeler = secilen.Projeler ?? new List<CalisanProjeDto>();
            projeDetaylistesiMobile_Items(projeler);

            BuildAndSetStatusCharts(secilen);
            await BuildAndSetMonthlyLineChartAsync(secilen);

            Dispatcher.Dispatch(ApplyMobileEdgeSizing);
        }

        private void projeDetaylistesiMobile_Items(List<CalisanProjeDto> projeler)
        {
            projeDetayListesiMobile.ItemsSource = null;
            projeDetayListesiMobile.ItemsSource = projeler;
            projeDetayListesiMobile.IsVisible = projeler.Count > 0;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            try { TemaYonetici.TemayiYukle(); } catch { }

            Dispatcher.Dispatch(() =>
            {
                RecomputeScale();
                ApplyMobileEdgeSizing();
            });

            if (!_centerHandlersAttached && chartViewMobile != null)
            {
                _centerHandlersAttached = true;
                chartViewMobile.SizeChanged += (_, __) =>
                {
                    NudgeToParentCenter(chartViewMobile);
                    MatchMobileChartWidths();
                };

                var parentFrame = FindAncestor<Frame>(chartViewMobile);
                if (parentFrame is VisualElement ve)
                {
                    ve.SizeChanged += (_, __) =>
                    {
                        NudgeToParentCenter(chartViewMobile);
                        MatchMobileChartWidths();
                    };
                }

                if (lineChartViewMobile != null)
                {
                    lineChartViewMobile.SizeChanged += (_, __) => MatchMobileChartWidths();
                    var progressFrame = FindAncestor<Frame>(lineChartViewMobile);
                    if (progressFrame is VisualElement pve)
                        pve.SizeChanged += (_, __) => MatchMobileChartWidths();
                }
            }
        }

        // ================== Ay Değiştirme ==================
        private async void btnAyOnceki_Clicked(object sender, EventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            UpdateMonthLabels();
            ResetLineCharts();
            if (_aktifCalisan != null) await BuildAndSetMonthlyLineChartAsync(_aktifCalisan);
            Dispatcher.Dispatch(ApplyMobileEdgeSizing);
        }

        private async void btnAySonraki_Clicked(object sender, EventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            UpdateMonthLabels();
            ResetLineCharts();
            if (_aktifCalisan != null) await BuildAndSetMonthlyLineChartAsync(_aktifCalisan);
            Dispatcher.Dispatch(ApplyMobileEdgeSizing);
        }

        private void UpdateMonthLabels()
        {
            var tr = new CultureInfo("tr-TR");
            var text = _currentMonth.ToString("MMMM yyyy", tr).ToUpper(tr);
            if (lblAyBaslik != null) lblAyBaslik.Text = text;
            if (lblAyBaslikMobile != null) lblAyBaslikMobile.Text = text;
        }

        // ================== Reset ==================
        private void ResetCharts()
        {
            ResetStatusCharts();
            ResetLineCharts();
        }

        private void ResetStatusCharts()
        {
            if (chartView != null) { chartView.Chart = null; chartView.InvalidateSurface(); }
            if (chartViewMobile != null) { chartViewMobile.Chart = null; chartViewMobile.InvalidateSurface(); }
        }

        private void ResetLineCharts()
        {
            if (lineChartView != null) { lineChartView.Chart = null; lineChartView.InvalidateSurface(); }
            if (lineChartViewMobile != null) { lineChartViewMobile.Chart = null; lineChartViewMobile.InvalidateSurface(); }
        }

        // ================== Durum Çubuk Grafiği ==================
        private void BuildAndSetStatusCharts(CalisanRaporDto s)
        {
            int toplam, tamam, devam, baslamayan;

            if (s?.Projeler != null && s.Projeler.Count > 0)
            {
                toplam = s.Projeler.Count;
                tamam = s.Projeler.Count(p => IsTamam(p.Aciklama));
                devam = s.Projeler.Count(p => IsDevam(p.Aciklama));
                baslamayan = s.Projeler.Count(p => IsBaslamadi(p.Aciklama));
            }
            else
            {
                tamam = Math.Max(0, s?.TamamlananProjeSayisi ?? 0);
                devam = Math.Max(0, s?.DevamEdenProjeSayisi ?? 0);
                toplam = Math.Max(0, s?.ToplamProjeSayisi ?? 0);
                baslamayan = Math.Max(0, toplam - (tamam + devam));
            }

            float vTamam = tamam == 0 ? EPS : tamam;
            float vDevam = devam == 0 ? EPS : devam;
            float vBaslamayan = baslamayan == 0 ? EPS : baslamayan;

            float labelSizeDesktop = (float)Math.Max(12, ScaledFontSize4);
            bool isMobile = DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceInfo.Idiom == DeviceIdiom.Tablet;
            float labelSizeMobile = isMobile ? (float)Math.Max(16, ScaledFontSize3) : (float)Math.Max(12, ScaledFontSize4);

            var entriesDesktop = new List<ChartEntry>
            {
                new(vTamam)      { Label="Tamamlandı", ValueLabel=tamam.ToString(),      Color=SKColor.Parse("#4CAF50"), ValueLabelColor=SKColors.Black, TextColor=SKColors.Black },
                new(vDevam)      { Label="Devam",      ValueLabel=devam.ToString(),      Color=SKColor.Parse("#FF9800"), ValueLabelColor=SKColors.Black, TextColor=SKColors.Black },
                new(vBaslamayan) { Label="Başlamadı",  ValueLabel=baslamayan.ToString(), Color=SKColor.Parse("#9E9E9E"), ValueLabelColor=SKColors.Black, TextColor=SKColors.Black }
            };

            var entriesMobile = new List<ChartEntry>
            {
                new(vTamam)      { Label="Tamamlandı", ValueLabel=tamam.ToString(),      Color=SKColor.Parse("#4CAF50"), ValueLabelColor=SKColors.Black, TextColor=SKColors.Black },
                new(vDevam)      { Label="Devam",      ValueLabel=devam.ToString(),      Color=SKColor.Parse("#FF9800"), ValueLabelColor=SKColors.Black, TextColor=SKColors.Black },
                new(vBaslamayan) { Label="Başlamadı",  ValueLabel=baslamayan.ToString(), Color=SKColor.Parse("#9E9E9E"), ValueLabelColor=SKColors.Black, TextColor=SKColors.Black }
            };

            var barDesktop = new BarChart
            {
                Entries = entriesDesktop,
                LabelTextSize = labelSizeDesktop,
                ValueLabelOrientation = Orientation.Horizontal,
                LabelOrientation = Orientation.Horizontal,
                Margin = 15,
                BackgroundColor = SKColors.Transparent,
                AnimationDuration = TimeSpan.Zero
            };

            var barMobile = new BarChart
            {
                Entries = entriesMobile,
                LabelTextSize = labelSizeMobile,
                ValueLabelOrientation = Orientation.Horizontal,
                LabelOrientation = Orientation.Horizontal,
                Margin = 15,
                BackgroundColor = SKColors.Transparent,
                AnimationDuration = TimeSpan.Zero
            };

            if (lblBaslamayan != null) lblBaslamayan.Text = $"⏸️ Başlanmayan: {baslamayan}";
            if (lblBaslamayanMobile != null) lblBaslamayanMobile.Text = $"⏸️ Başlanmayan: {baslamayan}";

            Dispatcher.Dispatch(() =>
            {
                if (chartView != null) { chartView.Chart = barDesktop; chartView.InvalidateSurface(); }
                if (chartViewMobile != null) { chartViewMobile.Chart = barMobile; chartViewMobile.InvalidateSurface(); }
            });
        }

        // ================== Aylık Çizgi Grafiği (API + fallback) ==================
        private async Task<List<(DateTime gun, double yuzde)>> GetAylikIlerlemeAsync(int kullaniciId, DateTime ay)
        {
            try
            {
                var url = $"/api/Raporlar/CalisanAylikIlerleme?kullaniciId={kullaniciId}&yil={ay.Year}&ay={ay.Month}";
                var res = await _http.GetAsync(url);
                if (!res.IsSuccessStatusCode) return new();

                var json = await res.Content.ReadAsStringAsync();
                var list = JsonConvert.DeserializeObject<List<GunlukIlerlemeDto>>(json, JsonSafeSettings) ?? new();

                return list
                    .OrderBy(x => x.Tarih)
                    .Select(x => (x.Tarih.Date, x.OrtalamaYuzde))
                    .ToList();
            }
            catch
            {
                return new();
            }
        }

        private async Task BuildAndSetMonthlyLineChartAsync(CalisanRaporDto s)
        {
            var start = _currentMonth;
            int days = DateTime.DaysInMonth(start.Year, start.Month);

            // 1) API
            List<(DateTime gun, double yuzde)> seri = new();
            if (s?.KullaniciId > 0)
                seri = await GetAylikIlerlemeAsync(s.KullaniciId, start);

            var entriesDesktop = new List<ChartEntry>(days);
            var entriesMobile = new List<ChartEntry>(days);

            if (seri.Count > 0)
            {
                var byDay = seri.ToDictionary(x => x.gun.Date, x => Math.Clamp(x.yuzde, 0, 100));
                float last = 0f;

                for (int i = 0; i < days; i++)
                {
                    var day = start.AddDays(i).Date;
                    var val = byDay.TryGetValue(day, out var y) ? (float)y : last;
                    last = val;

                    string label = (i == 0 || i == 7 || i == 14 || i == 21 || i >= 28) ? (i + 1).ToString() : string.Empty;

                    entriesDesktop.Add(new ChartEntry(val) { Label = label, ValueLabel = string.Empty, Color = SKColor.Parse("#1976D2"), TextColor = SKColors.Black, ValueLabelColor = SKColors.Black });
                    entriesMobile.Add(new ChartEntry(val) { Label = label, ValueLabel = string.Empty, Color = SKColor.Parse("#1976D2"), TextColor = SKColors.Black, ValueLabelColor = SKColors.Black });
                }
            }
            else
            {
                // 2) Fallback: lokal hesap
                float last = 0f;
                for (int i = 0; i < days; i++)
                {
                    var day = start.AddDays(i);
                    float avg = (float)Math.Round(ComputeDailyAverageProgressPercent(s, day), 1);
                    if (i > 0 && Math.Abs(avg - last) < 0.5f) avg = last;
                    last = avg;

                    string label = (i == 0 || i == 7 || i == 14 || i == 21 || i == 28) ? (i + 1).ToString() : string.Empty;

                    entriesDesktop.Add(new ChartEntry(avg) { Label = label, ValueLabel = string.Empty, Color = SKColor.Parse("#1976D2"), TextColor = SKColors.Black, ValueLabelColor = SKColors.Black });
                    entriesMobile.Add(new ChartEntry(avg) { Label = label, ValueLabel = string.Empty, Color = SKColor.Parse("#1976D2"), TextColor = SKColors.Black, ValueLabelColor = SKColors.Black });
                }
            }

            var isMobile = DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceInfo.Idiom == DeviceIdiom.Tablet;

            var lineDesktop = new LineChart
            {
                Entries = entriesDesktop,
                MinValue = 0,
                MaxValue = 100,
                LineMode = LineMode.Straight,
                PointMode = PointMode.None,
                LineSize = 3,
                LabelTextSize = (float)Math.Max(12, ScaledFontSize4),
                BackgroundColor = SKColors.Transparent,
                AnimationDuration = TimeSpan.Zero,
                LineAreaAlpha = (byte)0,
                PointAreaAlpha = (byte)0
            };

            var lineMobile = new LineChart
            {
                Entries = entriesMobile,
                MinValue = 0,
                MaxValue = 100,
                LineMode = LineMode.Straight,
                PointMode = PointMode.None,
                LineSize = 3,
                LabelTextSize = isMobile ? (float)Math.Max(16, ScaledFontSize3) : (float)Math.Max(12, ScaledFontSize4),
                BackgroundColor = SKColors.Transparent,
                AnimationDuration = TimeSpan.Zero,
                LineAreaAlpha = (byte)0,
                PointAreaAlpha = (byte)0
            };

            Dispatcher.Dispatch(() =>
            {
                if (lineChartView != null) { lineChartView.Chart = lineDesktop; lineChartView.InvalidateSurface(); }
                if (lineChartViewMobile != null) { lineChartViewMobile.Chart = lineMobile; lineChartViewMobile.InvalidateSurface(); }
            });
        }

        // Günlük ortalama ilerleme (%)
        private double ComputeDailyAverageProgressPercent(CalisanRaporDto s, DateTime day)
        {
            if (s?.Projeler == null || s.Projeler.Count == 0) return 0;

            double sum = 0;
            int count = 0;

            foreach (var p in s.Projeler)
            {
                DateTime? bas = p.BaslangicTarihi;
                DateTime? bit = p.BitisTarihi;
                double prog;

                if (bas.HasValue && day < bas.Value)
                {
                    prog = 0;
                }
                else if (bit.HasValue && day >= bit.Value)
                {
                    prog = 100;
                }
                else if (bas.HasValue && bit.HasValue && bit.Value > bas.Value)
                {
                    var total = (bit.Value - bas.Value).TotalDays;
                    var done = (day - bas.Value).TotalDays;
                    prog = Math.Clamp(100.0 * (done / total), 0, 100);
                }
                else
                {
                    var durum = NormalizeStatus(p.Aciklama);
                    if (durum.Contains("tamam"))
                        prog = 100;
                    else if (durum.Contains("baslamad"))
                        prog = 0;
                    else
                        prog = 50;
                }

                sum += prog;
                count++;
            }

            return count == 0 ? 0 : sum / count;
        }

        // ================== UI ==================
        private void UpdateDesktopDetailUI(CalisanRaporDto s)
        {
            lblAdSoyad.Text = $"👤 Ad Soyad: {s?.AdSoyad ?? "-"}";
            lblToplam.Text = $"📁 Toplam Proje: {s?.ToplamProjeSayisi ?? 0}";
            var (tamam, devam, baslamayan, _) = CountFromProjectsOrDto(s);
            lblDevam.Text = $"🔄 Devam Eden: {devam}";
            lblTamam.Text = $"✅ Tamamlanan: {tamam}";
            if (lblBaslamayan != null) lblBaslamayan.Text = $"⏸️ Başlanmayan: {baslamayan}";
        }

        private void UpdateMobileDetailUI(CalisanRaporDto s)
        {
            lblAdSoyadMobile.Text = $"👤 Ad Soyad: {s?.AdSoyad ?? "-"}";
            lblToplamMobile.Text = $"📁 Toplam Proje: {s?.ToplamProjeSayisi ?? 0}";
            var (tamam, devam, baslamayan, _) = CountFromProjectsOrDto(s);
            lblDevamMobile.Text = $"🔄 Devam Eden: {devam}";
            lblTamamMobile.Text = $"✅ Tamamlanan: {tamam}";
            if (lblBaslamayanMobile != null) lblBaslamayanMobile.Text = $"⏸️ Başlanmayan: {baslamayan}";
        }

        // ================== Yardımcılar ==================
        private static string NormalizeStatus(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var formD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().ToLowerInvariant().Trim();
        }

        private static bool IsBaslamadi(string? acik)
        {
            var n = NormalizeStatus(acik);
            return n.Contains("baslamad");
        }

        private static bool IsDevam(string? acik)
        {
            var n = NormalizeStatus(acik);
            return n.Contains("devam");
        }

        private static bool IsTamam(string? acik)
        {
            var n = NormalizeStatus(acik);
            return n.Contains("tamam");
        }

        private static (int tamam, int devam, int baslamayan, int toplam) CountFromProjectsOrDto(CalisanRaporDto s)
        {
            if (s?.Projeler != null && s.Projeler.Count > 0)
            {
                int toplam = s.Projeler.Count;
                int tamam = s.Projeler.Count(p => IsTamam(p.Aciklama));
                int devam = s.Projeler.Count(p => IsDevam(p.Aciklama));
                int baslamayan = s.Projeler.Count(p => IsBaslamadi(p.Aciklama));
                return (tamam, devam, baslamayan, toplam);
            }
            int t = Math.Max(0, s?.ToplamProjeSayisi ?? 0);
            int tm = Math.Max(0, s?.TamamlananProjeSayisi ?? 0);
            int dv = Math.Max(0, s?.DevamEdenProjeSayisi ?? 0);
            int bs = Math.Max(0, t - (tm + dv));
            return (tm, dv, bs, t);
        }

        private void ApplyMobileEdgeSizing()
        {
            if (Width <= 0) return;

            double side = MobileSideEdge;

            if (chartViewMobile != null)
            {
                chartViewMobile.Margin = new Thickness(side, 0);
                chartViewMobile.HorizontalOptions = LayoutOptions.FillAndExpand;
                chartViewMobile.WidthRequest = -1;
                chartViewMobile.TranslationX = 0;
            }

            if (lineChartViewMobile != null)
            {
                var frame = FindAncestor<Frame>(lineChartViewMobile);
                if (frame != null)
                {
                    frame.Margin = new Thickness(side, 0);
                    frame.HorizontalOptions = LayoutOptions.FillAndExpand;
                    frame.WidthRequest = -1;
                    frame.HasShadow = true;
                    frame.Shadow = new Shadow { Opacity = 0.25f, Radius = 10, Offset = new Point(0, 2) };
                    frame.TranslationX = 0;
                }

                lineChartViewMobile.Margin = Thickness.Zero;
                lineChartViewMobile.HorizontalOptions = LayoutOptions.FillAndExpand;
                lineChartViewMobile.WidthRequest = -1;
                lineChartViewMobile.TranslationX = 0;
            }

            Dispatcher.Dispatch(() =>
            {
                if (chartViewMobile != null) NudgeToPerfectCenter(chartViewMobile);
                if (lineChartViewMobile != null)
                {
                    var frame2 = FindAncestor<Frame>(lineChartViewMobile);
                    if (frame2 != null) NudgeToPerfectCenter(frame2);
                }

                if (chartViewMobile != null) NudgeToParentCenter(chartViewMobile);
                MatchMobileChartWidths();
            });
        }

        private void NudgeToPerfectCenter(VisualElement el)
        {
            if (el == null || Width <= 0 || el.Width <= 0) return;

            double leftSpace = el.X;
            double rightSpace = Width - (el.X + el.Width);
            double delta = rightSpace - leftSpace;

            el.TranslationX = Math.Abs(delta) > 0.5 ? delta / 2.0 : 0;
        }

        private void NudgeToParentCenter(VisualElement el)
        {
            if (el == null || el.Width <= 0) return;
            if (el.Parent is not VisualElement parent || parent.Width <= 0) return;

            double left = el.X;
            double right = parent.Width - (el.X + el.Width);
            double delta = right - left;

            el.TranslationX = Math.Abs(delta) > 0.5 ? delta / 2.0 : 0;
        }

        private void MatchMobileChartWidths()
        {
            if (chartViewMobile == null || lineChartViewMobile == null) return;

            var statusFrame = FindAncestor<Frame>(chartViewMobile) as VisualElement;
            var progressFrame = FindAncestor<Frame>(lineChartViewMobile) as VisualElement;

            if (statusFrame == null || progressFrame == null) return;
            if (statusFrame.Width <= 0 || progressFrame.Width <= 0) return;

            double statusInnerWidth =
                statusFrame.Width
                - (statusFrame is Frame sf
                    ? (sf.Padding.Left + sf.Padding.Right)
                    : 0);

            progressFrame.WidthRequest = statusFrame.Width;

            lineChartViewMobile.HorizontalOptions = LayoutOptions.Center;
            lineChartViewMobile.WidthRequest = statusInnerWidth;
            lineChartViewMobile.TranslationX = 0;

            NudgeToParentCenter(lineChartViewMobile);
        }

        private static TElement? FindAncestor<TElement>(Element? start) where TElement : Element
        {
            var cur = start?.Parent;
            while (cur != null && cur is not TElement)
                cur = cur.Parent;
            return cur as TElement;
        }
    }

    public class DateHasValueConverter : IValueConverter
    {
        private static readonly DateTime MinOkDate = DateTime.MinValue.AddYears(1);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null) return false;
            if (value is DateTime dt) return dt > MinOkDate;
            if (value is string s && DateTime.TryParse(s, out var parsed)) return parsed > MinOkDate;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
