using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;              // DeviceInfo
using LessArcApppp.Models;

namespace LessArcApppp
{
    public partial class PlanlarimPage : ContentPage
    {
        private readonly HttpClient _http;
        private readonly string _token;

        private ObservableCollection<Plan> planlar = new();
        private List<DateTime> planliGunler = new();
        private DateTime secilenTarih = DateTime.Today;
        private Button? seciliGunButon;

        // ========= ÖLÇEKLENEBİLİR PROPS =========
        public Thickness ScaledPadding { get; private set; } = new(16);
        public double ScaledSpacing { get; private set; } = 16;
        public double ScaledSmallSpacing { get; private set; } = 8;

        public double ScaledFontSize { get; private set; } = 22;
        public double ScaledFontSize2 { get; private set; } = 20;
        public double ScaledFontSize3 { get; private set; } = 14;
        public double ScaledFontSize4 { get; private set; } = 12;

        public double ScaledButtonHeight { get; private set; } = 44;
        public double ScaledButtonWidth { get; private set; } = 140;

        public double CalendarGridSpacing { get; private set; } = 8;
        public double DayCellSize { get; private set; } = 56;

        public Thickness P_BottomSmall { get; private set; } = new(0, 0, 0, 8);
        public Thickness P_TopSmall { get; private set; } = new(0, 8, 0, 0);
        public Thickness M_TopSmall { get; private set; } = new(0, 8, 0, 0);
        public Thickness M_VertSmall { get; private set; } = new(0, 6);

        // ========= Ölçek eğrisi =========
        private const double BasePhoneWidth = 430.0;
        private const double MaxPhoneScale = 1.30;
        private const double MaxDeskScale = 1.85;

        private readonly (double w, double s)[] deskCurve =
        {
            (800,1.00),(1000,1.08),(1200,1.16),(1366,1.24),
            (1600,1.36),(1920,1.52),(2200,1.66),(2560,1.80)
        };

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
            return deskCurve[^1].s;
        }

        private void RecomputeScale()
        {
            var w = Width;
            if (double.IsNaN(w) || w <= 0) return;

            bool isDesktop = DeviceInfo.Idiom == DeviceIdiom.Desktop || DeviceInfo.Idiom == DeviceIdiom.Tablet;
            double scale = isDesktop
                ? Math.Clamp(AutoScaleFromCurve(w), 1.0, MaxDeskScale)
                : Math.Clamp(w / BasePhoneWidth, 1.0, MaxPhoneScale);

            ScaledPadding = new Thickness(20 * scale);
            ScaledSpacing = 16 * scale;
            ScaledSmallSpacing = 8 * scale;

            ScaledFontSize = 22 * scale;
            ScaledFontSize2 = 20 * scale;
            ScaledFontSize3 = 14 * scale;
            ScaledFontSize4 = 12 * scale;

            ScaledButtonHeight = 44 * scale;
            ScaledButtonWidth = 140 * scale;

            CalendarGridSpacing = 10 * scale;

            const double DayCellScale = 0.78;
            double approxCalendarWidth = isDesktop
                ? w * 0.50
                : w - (ScaledPadding.Left + ScaledPadding.Right);

            double cell = (approxCalendarWidth
                           - (CalendarGridSpacing * 6)
                           - (ScaledPadding.Left + ScaledPadding.Right)) / 7.0;

            DayCellSize = Math.Max(32 * scale, Math.Floor(cell * DayCellScale));

            P_BottomSmall = new Thickness(0, 0, 0, 8 * scale);
            P_TopSmall = new Thickness(0, 8 * scale, 0, 0);
            M_TopSmall = new Thickness(0, 8 * scale, 0, 0);
            M_VertSmall = new Thickness(0, 6 * scale);

            OnPropertyChanged(nameof(ScaledPadding));
            OnPropertyChanged(nameof(ScaledSpacing));
            OnPropertyChanged(nameof(ScaledSmallSpacing));
            OnPropertyChanged(nameof(ScaledFontSize));
            OnPropertyChanged(nameof(ScaledFontSize2));
            OnPropertyChanged(nameof(ScaledFontSize3));
            OnPropertyChanged(nameof(ScaledFontSize4));
            OnPropertyChanged(nameof(ScaledButtonHeight));
            OnPropertyChanged(nameof(ScaledButtonWidth));
            OnPropertyChanged(nameof(CalendarGridSpacing));
            OnPropertyChanged(nameof(DayCellSize));
            OnPropertyChanged(nameof(P_BottomSmall));
            OnPropertyChanged(nameof(P_TopSmall));
            OnPropertyChanged(nameof(M_TopSmall));
            OnPropertyChanged(nameof(M_VertSmall));
        }

        // ========= CTOR =========
        public PlanlarimPage(HttpClient httpClient, string kullaniciToken)
        {
            InitializeComponent();

            _http = httpClient;
            _token = kullaniciToken ?? string.Empty;

            if (_http.DefaultRequestHeaders.Authorization is null && !string.IsNullOrWhiteSpace(_token))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            BindingContext = this;

            secilenTarih = DateTime.Today;
            lblSecilenTarih.Text = lblSecilenTarihMobile.Text =
                secilenTarih.ToString("dd MMMM yyyy", new CultureInfo("tr-TR"));

            YilAyYukle();
            _ = PlanliGunleriYukleVeTakvimiCiz();

            mobileLayout.IsVisible = DeviceInfo.Idiom == DeviceIdiom.Phone;
            desktopLayout.IsVisible = !mobileLayout.IsVisible;

            SizeChanged += (_, __) => RecomputeScale();
            RecomputeScale();
        }

        // ========= Ay Gezgini =========
        private void BtnOncekiAy_Clicked(object sender, EventArgs e)
        {
            secilenTarih = secilenTarih.AddMonths(-1);
            YilAyYukle();
            _ = PlanliGunleriYukleVeTakvimiCiz();
        }

        private void BtnSonrakiAy_Clicked(object sender, EventArgs e)
        {
            secilenTarih = secilenTarih.AddMonths(1);
            YilAyYukle();
            _ = PlanliGunleriYukleVeTakvimiCiz();
        }

        private void YilAyYukle()
        {
            var ayYil = secilenTarih.ToString("MMMM yyyy", new CultureInfo("tr-TR")).ToUpper();
            lblAyYili.Text = lblAyYiliMobile.Text = ayYil;
        }

        // ========= Veri =========
        private async Task PlanliGunleriYukleVeTakvimiCiz()
        {
            try
            {
                string url = $"/api/Planlar/tarihler?yil={secilenTarih.Year}&ay={secilenTarih.Month}";
                var tarihler = await _http.GetFromJsonAsync<List<DateTime>>(url);
                planliGunler = tarihler?.Select(d => d.Date).Distinct().ToList() ?? new();
            }
            catch { planliGunler = new(); }

            TakvimiCiz();
            PlanlariYukle();
        }

        private void TakvimiCiz()
        {
            TakvimiYenidenCiz(TakvimGrid);
            TakvimiYenidenCiz(TakvimGridMobile);
        }

        private void TakvimiYenidenCiz(Grid hedefGrid)
        {
            hedefGrid.Children.Clear();
            hedefGrid.RowDefinitions.Clear();
            hedefGrid.ColumnDefinitions.Clear();

            for (int i = 0; i < 7; i++)
                hedefGrid.ColumnDefinitions.Add(new ColumnDefinition());

            DateTime ilkGun = new DateTime(secilenTarih.Year, secilenTarih.Month, 1);
            int ilkGunIndex = (int)ilkGun.DayOfWeek;
            if (ilkGunIndex == 0) ilkGunIndex = 7;

            int toplamGun = DateTime.DaysInMonth(secilenTarih.Year, secilenTarih.Month);
            int satirSayisi = (toplamGun + ilkGunIndex - 1 + 6) / 7;

            for (int i = 0; i < satirSayisi; i++)
                hedefGrid.RowDefinitions.Add(new RowDefinition());

            for (int gun = 1; gun <= toplamGun; gun++)
            {
                DateTime tarih = new DateTime(secilenTarih.Year, secilenTarih.Month, gun);
                int row = (ilkGunIndex + gun - 2) / 7;
                int col = (ilkGunIndex + gun - 2) % 7;

                var btn = new Button
                {
                    Text = gun.ToString(),
                    BackgroundColor = planliGunler.Contains(tarih.Date) ? Color.FromArgb("#e8f5e9") : Colors.White,
                    TextColor = Colors.Black,
                    CornerRadius = 10,
                    BorderWidth = tarih.Date == secilenTarih.Date ? 2 : 0,
                    BorderColor = tarih.Date == secilenTarih.Date ? Color.FromArgb("#2196F3") : Colors.Transparent,
                    Padding = 5,
                    WidthRequest = DayCellSize,
                    HeightRequest = DayCellSize,
                    FontSize = ScaledFontSize3,
                    FontAttributes = FontAttributes.Bold
                };

                if (tarih.Date == secilenTarih.Date)
                    seciliGunButon = btn;

                btn.Clicked += (s, e) =>
                {
                    if (seciliGunButon != null)
                    {
                        seciliGunButon.BorderWidth = 0;
                        seciliGunButon.BorderColor = Colors.Transparent;
                    }

                    secilenTarih = tarih;
                    lblSecilenTarih.Text = lblSecilenTarihMobile.Text =
                        tarih.ToString("dd MMMM yyyy", new CultureInfo("tr-TR"));

                    btn.BorderColor = Color.FromArgb("#2196F3");
                    btn.BorderWidth = 2;
                    seciliGunButon = btn;

                    PlanlariYukle();
                };

                Grid.SetRow(btn, row);
                Grid.SetColumn(btn, col);
                hedefGrid.Children.Add(btn);
            }
        }

        // ========= CRUD =========
        private async void PlanlariYukle()
        {
            try
            {
                string url = $"/api/Planlar?date={secilenTarih:yyyy-MM-dd}";
                var sonuc = await _http.GetFromJsonAsync<List<Plan>>(url);
                planlar = new ObservableCollection<Plan>(sonuc ?? new List<Plan>());
                planListesi.ItemsSource = planlar;
                planListesiMobile.ItemsSource = planlar;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Planlar yüklenemedi: {ex.Message}", "Tamam");
            }
        }

        private async void PlanEkle_Clicked(object sender, EventArgs e)
        {
            var entry = (mobileLayout.IsVisible) ? entryPlanMobile : entryPlan;

            if (string.IsNullOrWhiteSpace(entry.Text))
            {
                await DisplayAlert("Uyarı", "Plan içeriği boş olamaz", "Tamam");
                return;
            }

            try
            {
                var yeniPlan = new PlanEkleDto
                {
                    Baslik = entry.Text.Trim(),
                    Aciklama = "",
                    Tarih = secilenTarih
                };

                var response = await _http.PostAsJsonAsync("/api/Planlar", yeniPlan);

                if (response.IsSuccessStatusCode)
                {
                    entry.Text = string.Empty;
                    await PlanliGunleriYukleVeTakvimiCiz();
                }
                else
                {
                    string hataMesaji = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Hata", $"Plan eklenemedi:\n{hataMesaji}", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Bir hata oluştu: {ex.Message}", "Tamam");
            }
        }

        private async void PlanSil_Clicked(object sender, EventArgs e)
        {
            var button = sender as ImageButton;
            var silinecekPlan = button?.BindingContext as Plan;
            if (silinecekPlan == null) return;

            bool onay = await DisplayAlert("Planı Sil", $"\"{silinecekPlan.Baslik}\" silinsin mi?", "Evet", "Hayır");
            if (!onay) return;

            try
            {
                var response = await _http.DeleteAsync($"/api/Planlar/{silinecekPlan.Id}");
                if (response.IsSuccessStatusCode)
                {
                    planlar.Remove(silinecekPlan);
                }
                else
                {
                    await DisplayAlert("Hata", "Plan silinemedi.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Silme hatası: {ex.Message}", "Tamam");
            }
        }

        private async void PlanDuzenle_Clicked(object sender, EventArgs e)
        {
            var button = sender as ImageButton;
            var plan = button?.BindingContext as Plan;
            if (plan == null) return;

            string yeniBaslik = await DisplayPromptAsync("Planı Düzenle", "Yeni plan başlığı:", initialValue: plan.Baslik);
            if (string.IsNullOrWhiteSpace(yeniBaslik)) return;

            try
            {
                var guncellenmis = new
                {
                    Id = plan.Id,
                    Baslik = yeniBaslik.Trim(),
                    Aciklama = plan.Aciklama ?? "",
                    Tarih = plan.Tarih
                };

                var response = await _http.PutAsJsonAsync($"/api/Planlar/{plan.Id}", guncellenmis);

                if (response.IsSuccessStatusCode)
                {
                    plan.Baslik = yeniBaslik.Trim();
                }
                else
                {
                    string hataMesaji = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Hata", $"Plan güncellenemedi:\n{hataMesaji}", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Bir hata oluştu: {ex.Message}", "Tamam");
            }
        }
    }
}
