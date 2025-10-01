using LessArcApppp.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp
{
    public partial class AdminProjelerPage : ContentPage
    {
        // ============================
        // 📏 RESPONSIVE ÖLÇEKLEME
        // ============================

        // Tasarım referans genişliği (mobilde baseline)
        public double BaseWidth { get; set; } = 430.0;

        // Tavanlar
        public double MaxDesktopScale { get; set; } = 1.85;
        public double MaxMobileScale { get; set; } = 1.30;

        // Kullanıcı isteğe bağlı zoom (örn. 1.20 = +%20) -> null ise devre dışı
        public double? UserZoomFactor { get; set; } = null;

        // Masaüstünde aşırı geniş pencerelerde etkili DIP cap
        public double DesktopEffectiveWidthCap { get; set; } = 2200;

        // Masaüstü için genişliğe göre yumuşak ölçek eğrisi
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

        // Sayfa genelinde bağlanan değerler (XAML’de kullanılıyor)
        public double ScaledFontSize { get; private set; } = 26;
        public double ScaledFontSize2 { get; private set; } = 20;
        public double ScaledFontSize3 { get; private set; } = 14;
        public double ScaledFontSize4 { get; private set; } = 12;

        public double ScaledPadding { get; private set; } = 15;
        public double ScaledSmallPadding { get; private set; } = 10;
        public double ScaledSpacing { get; private set; } = 10;
        public double ScaledSmallSpacing { get; private set; } = 6;

        public double ScaledButtonHeight { get; private set; } = 42;
        public double ScaledButtonWidth { get; private set; } = 210;
        public double ScaledButtonIcon { get; private set; } = 24;

        // XAML’de kullanılan composite Thickness’ler
        public Thickness M_ItemBetween { get; private set; } = new Thickness(0, 6, 0, 0);

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
            return _desktopScaleCurve[^1].s;
        }

        // ⭐️ SOL DAR / SAĞ GENİŞ oran ayarlayıcı
        private void AdjustDesktopColumns(double widthDip)
        {
            // Masaüstü değilse dokunma
            if (DeviceInfo.Idiom != DeviceIdiom.Desktop) return;

            // XAML'de iki kolonlu ana grid'e x:Name="DesktopTwoColGrid" verdiğini varsayıyorum
            var grid = this.FindByName<Grid>("DesktopTwoColGrid");
            if (grid == null || grid.ColumnDefinitions.Count < 2) return;

            // Breakpoint’lere göre weights (sol daha dar, sağ daha geniş)
            // Küçükten büyüğe: 1:2  ->  1:2.4  ->  1:2.8  ->  1:3.2
            (double left, double right) w = widthDip switch
            {
                <= 1100 => (1.0, 2.0),
                <= 1366 => (1.0, 2.4),
                <= 1680 => (1.0, 2.8),
                _ => (1.0, 3.2),
            };

            // Solda mutlak max genişlik (görsel dengesizlik olmasın)
            double maxLeftPx = 520;
            if (grid.Width > 0 && grid.Width * (w.left / (w.left + w.right)) > maxLeftPx)
            {
                var targetLeftRatio = maxLeftPx / grid.Width;
                var targetRightRatio = Math.Max(0.0001, 1 - targetLeftRatio);
                // Oranı yumuşak normalize et
                w.left = targetLeftRatio * 10;
                w.right = targetRightRatio * 10;
            }

            grid.ColumnDefinitions[0].Width = new GridLength(w.left, GridUnitType.Star);
            grid.ColumnDefinitions[1].Width = new GridLength(w.right, GridUnitType.Star);
        }

        private void RecomputeScale()
        {
            var w = this.Width;
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

            ScaledFontSize = 26 * scale;
            ScaledFontSize2 = 20 * scale;
            ScaledFontSize3 = 14 * scale;
            ScaledFontSize4 = 12 * scale;

            ScaledPadding = 15 * scale;
            ScaledSmallPadding = 10 * scale;
            ScaledSpacing = 10 * scale;
            ScaledSmallSpacing = 6 * scale;

            ScaledButtonHeight = 40 * scale;
            ScaledButtonWidth = 200 * scale;
            ScaledButtonIcon = 22 * scale;

            M_ItemBetween = new Thickness(0, ScaledSmallSpacing, 0, 0);

            OnPropertyChanged(nameof(ScaledFontSize));
            OnPropertyChanged(nameof(ScaledFontSize2));
            OnPropertyChanged(nameof(ScaledFontSize3));
            OnPropertyChanged(nameof(ScaledFontSize4));
            OnPropertyChanged(nameof(ScaledPadding));
            OnPropertyChanged(nameof(ScaledSmallPadding));
            OnPropertyChanged(nameof(ScaledSpacing));
            OnPropertyChanged(nameof(ScaledSmallSpacing));
            OnPropertyChanged(nameof(ScaledButtonHeight));
            OnPropertyChanged(nameof(ScaledButtonWidth));
            OnPropertyChanged(nameof(ScaledButtonIcon));
            OnPropertyChanged(nameof(M_ItemBetween));

            // 🔥 yeni: oranları da güncelle
            AdjustDesktopColumns(widthDip);
        }

        // ============================
        // 🔧 SAYFA LOJİĞİ (mevcut kod)
        // ============================

        private const string TarihHataMetni = "Bitiş tarihi başlangıç tarihinden önce olamaz.";
        private static bool IsEndBeforeStart(DateTime bas, DateTime bit) => bit < bas;

        private readonly HttpClient _http;          // DI HttpClient
        private readonly string token;

        private List<AdminProjeListDto> tumProjeler = new();

        private int? _aktifDesktopProjeId;
        private int? _aktifMobilProjeId;

        private Editor? _desktopYorumEditor;
        private Editor? _mobilYorumEditor;

        private int _myUserId = 0;
        private string _myRole = "user";

        private readonly ObservableCollection<AdminProjeListDto> _mobilListe = new();
        private readonly ObservableCollection<AdminProjeListDto> _desktopListe = new();

        // SignalR
        private HubConnection? _hub;

        public ObservableCollection<ProjeYorumDto> MobilYorumlar { get; } = new();
        public ObservableCollection<ProjeYorumDto> DesktopYorumlar { get; } = new();

        public AdminProjelerPage(HttpClient httpClient, string kullaniciToken)
        {
            InitializeComponent();

            _http = httpClient;
            token = kullaniciToken ?? string.Empty;

            // Authorization header yoksa tak
            var auth = _http.DefaultRequestHeaders.Authorization;
            if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                if (!string.IsNullOrWhiteSpace(token))
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // BindingContext (ölçek binding’leri için gerekli)
            BindingContext = this;

            // 🪄 Responsive: pencere boyutu değiştikçe yeniden ölçekle
            SizeChanged += (_, __) => RecomputeScale();
            // İlk hesap
            RecomputeScale();

            // Mobil/desktop görünürlük
            bool isMobile = DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS;
            MobilGrid.IsVisible = isMobile;
            MasaustuGrid.IsVisible = !isMobile;

            // XAML referansları
            _desktopYorumEditor = this.FindByName<Editor>("DesktopYorumEditor");
            _mobilYorumEditor = this.FindByName<Editor>("MobilYorumEditor");

            // Masaüstü liste kaynağı ve arama olayı
            ProjelerListesi.ItemsSource = _desktopListe;
            if (this.FindByName<Entry>("entryAraDesktop") is Entry entryAra)
                entryAra.TextChanged += entryAraDesktop_TextChanged;

            // Güncelle butonları
            btnBilgileriGuncelleDesktop.Clicked += BtnBilgileriGuncelleDesktop_Clicked;
            btnBilgileriGuncelleMobile.Clicked += BtnBilgileriGuncelleMobile_Clicked;

            pickerDurumDesktop.SelectedIndexChanged += (_, __) =>
                UpdateHeaderCheckVisibility(GetPickerDurumValue(pickerDurumDesktop), isMobile: false);
            pickerDurumMobile.SelectedIndexChanged += (_, __) =>
                UpdateHeaderCheckVisibility(GetPickerDurumValue(pickerDurumMobile), isMobile: true);

            // Mobil popup listesi & arama
            cvProjeSecim.ItemsSource = _mobilListe;
            entryMobilAra.TextChanged += (_, e) => ApplyMobilFilter(e.NewTextValue ?? "");

            EnsureMobileDetailsAlwaysVisible();
            if (popupMask != null) popupMask.IsVisible = false;

            // SignalR Hub init & listeners
            InitSignalR();

            _ = ProjeleriGetir();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // İlk açılışta tekrar ölç (özellikle pencere hazır olduğunda)
            RecomputeScale();

            await EnsureIdentity();
            EnsureMobileDetailsAlwaysVisible();
            if (popupMask != null) popupMask.IsVisible = false;

            if (_hub is { State: not HubConnectionState.Connected })
            {
                try { await _hub!.StartAsync(); } catch { /* sessizce geç */ }
            }
        }

        protected override async void OnDisappearing()
        {
            try
            {
                if (_hub is { State: HubConnectionState.Connected })
                    await _hub.StopAsync();
            }
            catch { /* yoksay */ }
            base.OnDisappearing();
        }

        // ========= SignalR =========
        private void InitSignalR()
        {
            // BaseAddress + /hubs/projechat
            var hubUrl = new Uri(_http.BaseAddress!, "/hubs/projechat").ToString();

            _hub = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
                .WithAutomaticReconnect()
                .Build();

            // Sunucudan mesaj (YorumDto) geldiğinde sadece EKLE (reload yok)
            _hub.On<ProjeYorumDto>("message", msg =>
            {
                MainThread.BeginInvokeOnMainThread(() => AddIncomingMessage(msg));
            });

            // Yorum düzenleme/silme canlı bildirimleri
            _hub.On<ProjeYorumDto>("commentUpdated", msg =>
            {
                MainThread.BeginInvokeOnMainThread(() => ApplyUpdatedMessage(msg));
            });

            _hub.On<int>("commentDeleted", yorumId =>
            {
                MainThread.BeginInvokeOnMainThread(() => RemoveMessage(yorumId));
            });

            _ = Task.Run(async () =>
            {
                try { await _hub.StartAsync(); } catch { /* bağlanamazsa UI'yi bozma */ }
            });
        }

        // Sadece aktif projeye ve sadece yeni ise EKLE; sona ekle; alta kaydır
        private void AddIncomingMessage(ProjeYorumDto msg)
        {
            int? aktifId = MasaustuGrid.IsVisible ? _aktifDesktopProjeId : _aktifMobilProjeId;
            if (aktifId is not int aktifProjeId) return;
            if (msg.ProjeId != aktifProjeId) return;

            var list = MasaustuGrid.IsVisible ? DesktopYorumlar : MobilYorumlar;
            var view = MasaustuGrid.IsVisible
                ? this.FindByName<CollectionView>("DesktopYorumCollection")
                : this.FindByName<CollectionView>("MobilYorumCollection");

            if (list.Any(x => x.Id == msg.Id)) return;

            msg.IsEditable = (msg.KullaniciId == _myUserId);
            msg.IsDeletable = (msg.KullaniciId == _myUserId);

            list.Add(msg);
            ScrollToBottom(view, list.Count);
        }

        private static void ScrollToBottom(CollectionView? cv, int count)
        {
            try
            {
                if (cv == null || count <= 0) return;
                cv.ScrollTo(count - 1, position: ScrollToPosition.End, animate: true);
            }
            catch { }
        }

        private async Task JoinChatRoomAsync(int projeId)
        {
            if (_hub is null) return;

            if (_hub.State != HubConnectionState.Connected)
            {
                try { await _hub.StartAsync(); } catch { }
            }

            if (_aktifDesktopProjeId is int oldDesk && oldDesk != projeId)
                await _hub.InvokeAsync("LeaveProject", oldDesk);
            if (_aktifMobilProjeId is int oldMob && oldMob != projeId)
                await _hub.InvokeAsync("LeaveProject", oldMob);

            await _hub.InvokeAsync("JoinProject", projeId);
        }

        // ========== TAP tabanlı seçim ==========
        private void ProjeKart_Tapped(object sender, TappedEventArgs e)
        {
            var secilen = (sender as Element)?.BindingContext as AdminProjeListDto;
            if (secilen == null) return;

            _aktifDesktopProjeId = secilen.Id;

            lblSeciliProjeBaslik.Text = secilen.Baslik ?? "";
            dpBaslangicDesktop.Date = (secilen.BaslangicTarihi ?? DateTime.Today).Date;
            dpBitisDesktop.Date = (secilen.BitisTarihi ?? DateTime.Today).Date;

            // API → UI map (gelen veriyi picker'a uyarlıyoruz)
            var uiDurum = MapApiDurumToUi(secilen.Durum);
            SelectDurumOnPicker(pickerDurumDesktop, uiDurum);
            UpdateHeaderCheckVisibility(uiDurum, isMobile: false);

            ProjeDetaylariPanel.Children.Clear();
            _ = AdimlariYukle(secilen.Id, ProjeDetaylariPanel, isMobile: false);
            _ = YorumlariYukleVeGoster(secilen.Id, mobile: false);

            _ = JoinChatRoomAsync(secilen.Id);
        }

        private async void PopupKart_Tapped(object sender, TappedEventArgs e)
        {
            var secilen = (sender as Element)?.BindingContext as AdminProjeListDto;
            if (secilen == null) return;

            HidePopup();

            _aktifMobilProjeId = secilen.Id;
            lblMobilSecimMetni.Text =
                $"{secilen.Baslik}   •   {(!string.IsNullOrWhiteSpace(secilen.KullaniciAdSoyad) ? secilen.KullaniciAdSoyad : "çalışan 1")}";

            await YukuMobilProje(secilen);
            await JoinChatRoomAsync(secilen.Id);
        }

        // Popup aç/kapat
        private void BtnOpenProjePopup_Clicked(object sender, EventArgs e) => ShowPopup();
        private void TapCloseProjePopup_Tapped(object? sender, TappedEventArgs e) => HidePopup();
        private void TapCloseProjePopup_Clicked(object? sender, EventArgs e) => HidePopup();

        private void ShowPopup()
        {
            if (!MobilGrid.IsVisible) return;
            popupMask.IsVisible = true;
            entryMobilAra.Text = string.Empty;
            ApplyMobilFilter("");
            MainThread.BeginInvokeOnMainThread(() => entryMobilAra.Focus());
        }

        private void HidePopup() => popupMask.IsVisible = false;

        private void EnsureMobileDetailsAlwaysVisible()
        {
            MobilProjeDetaylariPanel.IsVisible = true;
            dpBaslangicMobile.IsEnabled = true;
            dpBitisMobile.IsEnabled = true;
            pickerDurumMobile.IsEnabled = true;
            btnBilgileriGuncelleMobile.IsEnabled = true;
        }

        // ===== PROJELER =====
        private async Task ProjeleriGetir()
        {
            try
            {
                var response = await _http.GetAsync("/api/Projeler/tum-projeler-detayli");
                if (!response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Hata", "Projeler alınamadı.", "Tamam");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                tumProjeler = JsonConvert.DeserializeObject<List<AdminProjeListDto>>(json) ?? new();

                // API → UI map ederek listeleri doldur
                tumProjeler.ForEach(p => p.Durum = MapApiDurumToUi(p.Durum));

                RefreshDesktopList();
                _mobilListe.Clear();
                foreach (var p in tumProjeler)
                    _mobilListe.Add(p);

                lblMobilSecimMetni.Text = "🔽 Proje Seç";
                EnsureMobileDetailsAlwaysVisible();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Bir hata oluştu: {ex.Message}", "Tamam");
            }
        }

        // ====== MASAÜSTÜ ARAMA / FİLTRE ======
        private void entryAraDesktop_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyDesktopFilter(e.NewTextValue ?? "");

        private void ApplyDesktopFilter(string query)
        {
            string q = Key(query ?? "");
            _desktopListe.Clear();

            IEnumerable<AdminProjeListDto> src = tumProjeler;

            if (!string.IsNullOrWhiteSpace(q))
                src = tumProjeler.Where(p =>
                    Key(p.Baslik ?? "").Contains(q) ||
                    Key(p.KullaniciAdSoyad ?? "").Contains(q));

            foreach (var p in src)
                _desktopListe.Add(p);
        }

        private void RefreshDesktopList()
        {
            var q = (this.FindByName<Entry>("entryAraDesktop")?.Text) ?? "";
            ApplyDesktopFilter(q);
        }

        // ====== MOBİL ARAMA / FİLTRE ======
        private void ApplyMobilFilter(string query)
        {
            string q = Key(query ?? "");
            _mobilListe.Clear();

            IEnumerable<AdminProjeListDto> src = tumProjeler;

            if (!string.IsNullOrWhiteSpace(q))
                src = tumProjeler.Where(p =>
                    Key(p.Baslik ?? "").Contains(q) ||
                    Key(p.KullaniciAdSoyad ?? "").Contains(q));

            foreach (var p in src)
                _mobilListe.Add(p);
        }

        // Eski gizli picker desteği
        private async void cmbProjeSecimi_SelectedIndexChanged(object sender, EventArgs e)
        {
            var index = cmbProjeSecimi.SelectedIndex;
            if (index < 0 || index >= tumProjeler.Count)
            {
                EnsureMobileDetailsAlwaysVisible();
                return;
            }
            await YukuMobilProje(tumProjeler[index]);
        }

        private async Task YukuMobilProje(AdminProjeListDto secilen)
        {
            _aktifMobilProjeId = secilen.Id;

            EnsureMobileDetailsAlwaysVisible();

            lblSeciliProjeBaslikMobile.Text = secilen.Baslik ?? "";
            dpBaslangicMobile.Date = (secilen.BaslangicTarihi ?? DateTime.Today).Date;
            dpBitisMobile.Date = (secilen.BitisTarihi ?? DateTime.Today).Date;

            var uiDurum = MapApiDurumToUi(secilen.Durum);
            SelectDurumOnPicker(pickerDurumMobile, uiDurum);
            UpdateHeaderCheckVisibility(uiDurum, isMobile: true);

            MobilAdimlarPanel.Children.Clear();
            await AdimlariYukle(secilen.Id, MobilAdimlarPanel, isMobile: true);
            await YorumlariYukleVeGoster(secilen.Id, mobile: true);
        }

        // ===== ADIMLAR =====
        private async Task AdimlariYukle(int projeId, Layout hedefPanel, bool isMobile)
        {
            try
            {
                var response = await _http.GetAsync($"/api/ProjeAdimlari/Proje/{projeId}");
                if (!response.IsSuccessStatusCode)
                {
                    hedefPanel.Children.Add(new Label { Text = "Adımlar getirilemedi.", TextColor = Colors.Red });
                    SetGenelTamamLabel(isMobile, 0);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var adimlar = JsonConvert.DeserializeObject<List<ProjeAdimiDto>>(json) ?? new();

                double ort = adimlar.Count > 0 ? adimlar.Average(a => a.TamamlanmaYuzdesi) : 0;
                SetGenelTamamLabel(isMobile, ort);

                foreach (var adim in adimlar)
                {
                    hedefPanel.Children.Add(new Frame
                    {
                        BackgroundColor = Colors.White,
                        BorderColor = Colors.LightGray,
                        CornerRadius = 8,
                        HasShadow = false,
                        Padding = new Thickness(10, 6),
                        Margin = new Thickness(0, 2),
                        Content = new VerticalStackLayout
                        {
                            Spacing = 2,
                            Children =
                            {
                                CreateBlackLabel(adim.AdimBasligi, 14, true),
                                new Label
                                {
                                    Text = $"Tamamlanma: %{adim.TamamlanmaYuzdesi}",
                                    FontSize = 13,
                                    TextColor = Colors.ForestGreen
                                }
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                hedefPanel.Children.Add(new Label { Text = $"Hata: {ex.Message}", TextColor = Colors.Red });
                SetGenelTamamLabel(isMobile, 0);
            }
        }

        private Label CreateBlackLabel(string text, double fontSize = 16, bool bold = false) => new()
        {
            Text = text,
            FontSize = fontSize,
            TextColor = Colors.Black,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
        };

        private void SetGenelTamamLabel(bool isMobile, double ort)
        {
            string text = $"Projenin Genel Tamamlanma Durumu: %{ort:F0}";
            if (isMobile) lblGenelTamamMobile.Text = text;
            else lblGenelTamamDesktop.Text = text;
        }

        // ===== GÜNCELLE =====
        private async void BtnBilgileriGuncelleDesktop_Clicked(object? sender, EventArgs e)
        {
            if (_aktifDesktopProjeId is not int pid)
            {
                await DisplayAlert("Uyarı", "Önce bir proje seçiniz.", "Tamam");
                return;
            }
            if (tumProjeler.FirstOrDefault(p => p.Id == pid) is null)
            {
                await DisplayAlert("Uyarı", "Geçerli bir proje seçiniz.", "Tamam");
                return;
            }

            DateTime bas = dpBaslangicDesktop.Date;
            DateTime bit = dpBitisDesktop.Date;
            string durumUi = GetPickerDurumValue(pickerDurumDesktop); // UI string

            await UpdateProjectAsync(pid, bas, bit, durumUi, isMobile: false);
        }

        private async void BtnBilgileriGuncelleMobile_Clicked(object? sender, EventArgs e)
        {
            if (_aktifMobilProjeId is not int pid)
            {
                await DisplayAlert("Uyarı", "Önce bir proje seçiniz.", "Tamam");
                EnsureMobileDetailsAlwaysVisible();
                return;
            }
            if (tumProjeler.FirstOrDefault(p => p.Id == pid) is null)
            {
                await DisplayAlert("Uyarı", "Geçerli bir proje seçiniz.", "Tamam");
                EnsureMobileDetailsAlwaysVisible();
                return;
            }

            DateTime bas = dpBaslangicMobile.Date;
            DateTime bit = dpBitisMobile.Date;
            string durumUi = GetPickerDurumValue(pickerDurumMobile);

            await UpdateProjectAsync(pid, bas, bit, durumUi, isMobile: true);
        }

        // ———— PUT payload ————
        private object BuildUpdatePayload(int projeId, DateTime baslangic, DateTime bitis, string durumUi)
        {
            var item = tumProjeler.FirstOrDefault(p => p.Id == projeId);
            var durumApi = MapUiDurumToApi(durumUi);
            bool isDone = Key(durumUi) == "tamamlandi";

            return new
            {
                id = projeId,
                baslik = item?.Baslik ?? string.Empty,
                baslangicTarihi = (DateTime?)baslangic,
                bitisTarihi = isDone ? (DateTime?)bitis : null, // kritik fark
                durum = string.IsNullOrWhiteSpace(durumApi) ? null : durumApi
            };
        }

        private static string FormatApiErrors(string content)
        {
            try
            {
                var jo = JObject.Parse(content);
                if (jo["errors"] is JObject errs && errs.HasValues)
                {
                    var sb = new StringBuilder();
                    foreach (var kv in errs)
                    {
                        var messages = kv.Value?.ToObject<string[]>() ?? Array.Empty<string>();
                        sb.AppendLine($"{kv.Key}: {string.Join(", ", messages)}");
                    }
                    return sb.ToString();
                }
            }
            catch { }
            return content;
        }

        private async Task UpdateProjectAsync(int projeId, DateTime baslangic, DateTime bitis, string durumUi, bool isMobile)
        {
            try
            {
                // Ön kontrol
                if (IsEndBeforeStart(baslangic, bitis))
                {
                    await DisplayAlert("Hata", TarihHataMetni, "Tamam");
                    if (isMobile) EnsureMobileDetailsAlwaysVisible();
                    return;
                }

                var endpoint = $"/api/Projeler/{projeId}";
                var payloadObj = BuildUpdatePayload(projeId, baslangic, bitis, durumUi);

                var json = System.Text.Json.JsonSerializer.Serialize(
                    payloadObj,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    }
                );
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _http.PutAsync(endpoint, content);
                if (!resp.IsSuccessStatusCode)
                {
                    var raw = await resp.Content.ReadAsStringAsync();
                    var pretty = FormatApiErrors(raw);
                    await DisplayAlert("Hata", pretty, "Tamam");
                    if (isMobile) EnsureMobileDetailsAlwaysVisible();
                    return;
                }

                // Yerelde güncelle
                var item = tumProjeler.FirstOrDefault(p => p.Id == projeId);
                if (item != null)
                {
                    item.BaslangicTarihi = baslangic;
                    item.BitisTarihi = Key(durumUi) == "tamamlandi" ? bitis : null;
                    item.Durum = MapApiDurumToUi(MapUiDurumToApi(durumUi));
                }

                RefreshDesktopList();
                _mobilListe.Clear();
                foreach (var p in tumProjeler) _mobilListe.Add(p);

                UpdateHeaderCheckVisibility(item?.Durum ?? durumUi, isMobile);

                await DisplayAlert("Başarılı", "Proje bilgileri güncellendi.", "Tamam");

                if (isMobile && _aktifMobilProjeId is int mpid)
                {
                    MobilAdimlarPanel.Children.Clear();
                    await AdimlariYukle(mpid, MobilAdimlarPanel, isMobile: true);
                    await YorumlariYukleVeGoster(mpid, mobile: true);
                }
                else if (!isMobile && _aktifDesktopProjeId is int dpid)
                {
                    ProjeDetaylariPanel.Children.Clear();
                    await AdimlariYukle(dpid, ProjeDetaylariPanel, isMobile: false);
                    await YorumlariYukleVeGoster(dpid, mobile: false);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", ex.Message, "Tamam");
                if (isMobile) EnsureMobileDetailsAlwaysVisible();
            }
        }

        // ===== YORUMLAR =====
        public sealed class ProjeYorumDto
        {
            public int Id { get; set; }
            public int ProjeId { get; set; }
            public int KullaniciId { get; set; }
            public string? KullaniciAdSoyad { get; set; }
            public string? YorumMetni { get; set; }
            public DateTime OlusturmaTarihi { get; set; }
            public bool IsEditable { get; set; }
            public bool IsDeletable { get; set; }
        }

        private void SetOwnershipFlags(IEnumerable<ProjeYorumDto> list)
        {
            foreach (var y in list)
            {
                bool benim = y.KullaniciId == _myUserId;
                y.IsEditable = benim;
                y.IsDeletable = benim;
            }
        }

        // İlk yükleme: ARTAN tarihe göre doldur (chat alttan aksın). Reload sadece burada.
        private async Task YorumlariYukleVeGoster(int projeId, bool mobile)
        {
            try
            {
                if (_myUserId == 0) await EnsureIdentity();

                var resp = await _http.GetAsync($"/api/Yorumlar/proje/{projeId}");
                if (!resp.IsSuccessStatusCode)
                {
                    if (mobile) MobilYorumlar.Clear();
                    else DesktopYorumlar.Clear();
                    return;
                }

                var json = await resp.Content.ReadAsStringAsync();
                var yorumlar = JsonConvert.DeserializeObject<List<ProjeYorumDto>>(json) ?? new();

                SetOwnershipFlags(yorumlar);

                var sirali = yorumlar.OrderBy(x => x.OlusturmaTarihi).ToList();

                if (mobile)
                {
                    MobilYorumlar.Clear();
                    foreach (var y in sirali) MobilYorumlar.Add(y);
                    var cv = this.FindByName<CollectionView>("MobilYorumCollection");
                    ScrollToBottom(cv, MobilYorumlar.Count);
                }
                else
                {
                    DesktopYorumlar.Clear();
                    foreach (var y in sirali) DesktopYorumlar.Add(y);
                    var cv = this.FindByName<CollectionView>("DesktopYorumCollection");
                    ScrollToBottom(cv, DesktopYorumlar.Count);
                }
            }
            catch (Exception ex)
            {
                if (mobile)
                {
                    MobilYorumlar.Clear();
                    MobilYorumlar.Add(new ProjeYorumDto { KullaniciAdSoyad = "Hata", YorumMetni = ex.Message });
                }
                else
                {
                    DesktopYorumlar.Clear();
                    DesktopYorumlar.Add(new ProjeYorumDto { KullaniciAdSoyad = "Hata", YorumMetni = ex.Message });
                }
            }
        }

        // --- YENİ: Yorum güncelle/sil için yerel UI helper'ları ---
        private void ApplyUpdatedMessage(ProjeYorumDto msg)
        {
            ReplaceInCollection(MobilYorumlar, msg);
            ReplaceInCollection(DesktopYorumlar, msg);

            var cv = MasaustuGrid.IsVisible
                ? this.FindByName<CollectionView>("DesktopYorumCollection")
                : this.FindByName<CollectionView>("MobilYorumCollection");
            var count = MasaustuGrid.IsVisible ? DesktopYorumlar.Count : MobilYorumlar.Count;
            ScrollToBottom(cv, count);
        }

        // Önemli: Replace (Replace event) -> UI hemen güncellenir
        private void ReplaceInCollection(ObservableCollection<ProjeYorumDto> coll, ProjeYorumDto msg)
        {
            if (coll == null || coll.Count == 0) return;
            for (int i = 0; i < coll.Count; i++)
            {
                if (coll[i].Id == msg.Id)
                {
                    var editable = msg.KullaniciId == _myUserId;
                    var newItem = new ProjeYorumDto
                    {
                        Id = msg.Id,
                        ProjeId = msg.ProjeId,
                        KullaniciId = msg.KullaniciId,
                        KullaniciAdSoyad = msg.KullaniciAdSoyad,
                        YorumMetni = msg.YorumMetni,
                        OlusturmaTarihi = msg.OlusturmaTarihi,
                        IsEditable = editable,
                        IsDeletable = editable
                    };
                    coll[i] = newItem;
                    break;
                }
            }
        }

        private void RemoveMessage(int yorumId)
        {
            RemoveFromCollection(MobilYorumlar, yorumId);
            RemoveFromCollection(DesktopYorumlar, yorumId);

            var cv = MasaustuGrid.IsVisible
                ? this.FindByName<CollectionView>("DesktopYorumCollection")
                : this.FindByName<CollectionView>("MobilYorumCollection");
            var count = MasaustuGrid.IsVisible ? DesktopYorumlar.Count : MobilYorumlar.Count;
            ScrollToBottom(cv, count);
        }

        private static void RemoveFromCollection(ObservableCollection<ProjeYorumDto> coll, int yorumId)
        {
            if (coll == null || coll.Count == 0) return;
            var item = coll.FirstOrDefault(x => x.Id == yorumId);
            if (item != null) coll.Remove(item);
        }

        // SignalR ile mesaj gönder
        private async Task SendChatAsync(int projeId, string text)
        {
            if (_hub is null || string.IsNullOrWhiteSpace(text)) return;

            var dto = new YorumEkleDtoClient
            {
                ProjeId = projeId,
                YorumMetni = text.Trim()
            };

            try
            {
                await _hub.InvokeAsync("SendMessage", dto);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Mesaj gönderilemedi: {ex.Message}", "Tamam");
            }
        }

        private async Task YorumGonderDesktop()
        {
            if (_desktopYorumEditor == null || string.IsNullOrWhiteSpace(_desktopYorumEditor.Text)) return;
            if (_aktifDesktopProjeId is not int pid) return;

            var text = _desktopYorumEditor.Text;
            _desktopYorumEditor.Text = string.Empty;
            await SendChatAsync(pid, text);
        }

        private async Task YorumGonderMobil()
        {
            if (_mobilYorumEditor == null || string.IsNullOrWhiteSpace(_mobilYorumEditor.Text)) return;
            if (_aktifMobilProjeId is not int pid) return;

            var text = _mobilYorumEditor.Text;
            _mobilYorumEditor.Text = string.Empty;
            await SendChatAsync(pid, text);
        }

        private void BtnYorumGonderMobile_Clicked(object sender, EventArgs e) => _ = YorumGonderMobil();
        private void BtnYorumGonderDesktop_Clicked(object sender, EventArgs e) => _ = YorumGonderDesktop();

        // ===== Yorum DÜZENLE (ANLIK) =====
        private async void EditYorum_Clicked(object sender, EventArgs e)
        {
            try
            {
                var yorum = (sender as ImageButton)?.CommandParameter as ProjeYorumDto;
                if (yorum == null) return;

                if (yorum.KullaniciId != _myUserId)
                {
                    await DisplayAlert("Yetki yok", "Sadece kendi yorumunuzu düzenleyebilirsiniz.", "Tamam");
                    return;
                }

                string? yeniMetin = await DisplayPromptAsync(
                    "Yorumu Düzenle",
                    "Metni güncelle:",
                    accept: "Kaydet",
                    cancel: "Vazgeç",
                    placeholder: "Yeni yorum",
                    initialValue: yorum.YorumMetni,
                    maxLength: 1000,
                    keyboard: Keyboard.Text);

                if (string.IsNullOrWhiteSpace(yeniMetin)) return;
                yeniMetin = yeniMetin.Trim();

                var body = new { YorumMetni = yeniMetin };
                var json = System.Text.Json.JsonSerializer.Serialize(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _http.PutAsync($"/api/Yorumlar/{yorum.Id}", content);

                if (!resp.IsSuccessStatusCode)
                {
                    var msg = await resp.Content.ReadAsStringAsync();
                    await DisplayAlert("Hata", $"Yorum güncellenemedi.\nSunucu: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{msg}", "Tamam");
                    return;
                }

                ProjeYorumDto? updated = null;
                try
                {
                    var payload = await resp.Content.ReadAsStringAsync();
                    updated = JsonConvert.DeserializeObject<ProjeYorumDto>(payload);
                }
                catch { /* yoksay */ }

                if (updated != null)
                {
                    updated.IsEditable = (updated.KullaniciId == _myUserId);
                    updated.IsDeletable = (updated.KullaniciId == _myUserId);
                    ApplyUpdatedMessage(updated);
                }
                else
                {
                    yorum.YorumMetni = yeniMetin;
                    ApplyUpdatedMessage(yorum);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", ex.Message, "Tamam");
            }
        }

        // ===== Yorum SİL (ANLIK) =====
        private async void SilYorum_Clicked(object sender, EventArgs e)
        {
            try
            {
                var yorum = (sender as ImageButton)?.CommandParameter as ProjeYorumDto;
                if (yorum == null) return;

                if (yorum.KullaniciId != _myUserId)
                {
                    await DisplayAlert("Yetki yok", "Sadece kendi yorumunuzu silebilirsiniz.", "Tamam");
                    return;
                }

                bool onay = await DisplayAlert("Silinsin mi?",
                                               "Bu yorumu silmek istiyor musun?",
                                               "Evet", "Hayır");
                if (!onay) return;

                var resp = await _http.DeleteAsync($"/api/Yorumlar/{yorum.Id}");
                if (!resp.IsSuccessStatusCode)
                {
                    var msg = await resp.Content.ReadAsStringAsync();
                    await DisplayAlert("Hata", $"Yorum silinemedi.\nSunucu: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{msg}", "Tamam");
                    return;
                }

                RemoveMessage(yorum.Id);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", ex.Message, "Tamam");
            }
        }

        // ===== Yardımcılar =====
        private void SelectDurumOnPicker(Picker picker, string? durumUi)
        {
            if (picker == null) return;

            string[] items = { "Başlamadı", "Devam Ediyor", "Tamamlandı" };
            if (picker.ItemsSource == null || picker.ItemsSource.Count == 0)
                picker.ItemsSource = items.ToList();

            if (string.IsNullOrWhiteSpace(durumUi))
            {
                picker.SelectedIndex = -1;
                return;
            }

            string key = Key(durumUi);
            int idx = Array.FindIndex(items, s => Key(s) == key);
            picker.SelectedIndex = idx >= 0 ? idx : -1;
        }

        private string GetPickerDurumValue(Picker picker)
            => (picker.SelectedItem as string) ?? string.Empty;

        private static string Normalize(string s) =>
            (s ?? string.Empty).Trim().ToLowerInvariant()
             .Replace("ı", "i").Replace("İ", "i")
             .Replace("ş", "s").Replace("Ş", "s")
             .Replace("ğ", "g").Replace("Ğ", "g")
             .Replace("ü", "u").Replace("Ü", "u")
             .Replace("ö", "o").Replace("Ö", "o")
             .Replace("ç", "c").Replace("Ç", "c");

        private static string Key(string s) => Normalize(s).Replace(" ", "");

        private bool IsDone(string? durumUi)
        {
            if (string.IsNullOrWhiteSpace(durumUi)) return false;
            return Key(durumUi) == "tamamlandi";
        }

        private void UpdateHeaderCheckVisibility(string? durumUi, bool isMobile)
        {
            bool visible = IsDone(durumUi);
            if (isMobile) imgCheckMobile.IsVisible = visible;
            else imgCheckDesktop.IsVisible = visible;
        }

        // ===== Kimlik =====
        private async Task EnsureIdentity()
        {
            _myUserId = 0;

            if (int.TryParse(await SecureStorage.GetAsync("UserId"), out var uid))
                _myUserId = uid;

            if (_myUserId == 0 && !string.IsNullOrWhiteSpace(token))
                _myUserId = ExtractUserIdFromJwt(token);

            _myRole = await SecureStorage.GetAsync("UserRole") ?? "user";
        }

        private int ExtractUserIdFromJwt(string jwt)
        {
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length < 2) return 0;

                static string Pad(string s) => s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');

                var payloadPart = parts[1].Replace('-', '+').Replace('_', '/');
                var payloadBytes = Convert.FromBase64String(Pad(payloadPart));
                var payloadJson = Encoding.UTF8.GetString(payloadBytes);

                var jo = JsonConvert.DeserializeObject<Dictionary<string, object>>(payloadJson)!;

                string? raw =
                    (jo.TryGetValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", out var c1) ? c1?.ToString() : null) ??
                    (jo.TryGetValue("id", out var c2) ? c2?.ToString() : null) ??
                    (jo.TryGetValue("sub", out var c3) ? c3?.ToString() : null) ??
                    (jo.TryGetValue("nameid", out var c4) ? c4?.ToString() : null) ??
                    (jo.TryGetValue("userId", out var c5) ? c5?.ToString() : null) ??
                    (jo.TryGetValue("uid", out var c6) ? c6?.ToString() : null);

                return int.TryParse(raw, out var id) ? id : 0;
            }
            catch
            {
                return 0;
            }
        }

        // ===== Durum Map'leri =====
        private static string MapUiDurumToApi(string? ui)
        {
            var k = Key(ui ?? "");
            return k switch
            {
                "baslamadi" => "Baslamadi",
                "devamediyor" => "DevamEdiyor",
                "tamamlandi" => "Tamamlandi",
                _ => string.IsNullOrWhiteSpace(ui) ? "" : ui
            };
        }

        private static string MapApiDurumToUi(string? api)
        {
            var k = Key(api ?? "");
            return k switch
            {
                "baslamadi" => "Başlamadı",
                "devamediyor" => "Devam Ediyor",
                "tamamlandi" => "Tamamlandı",
                _ => api ?? ""
            };
        }

        // Hub'a gönderim için küçük DTO
        private sealed class YorumEkleDtoClient
        {
            public int ProjeId { get; set; }
            public string YorumMetni { get; set; } = string.Empty;
        }
    }
}
