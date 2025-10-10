using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics; // Colors
using LessArcApppp.Models;

namespace LessArcApppp
{
    public partial class BildirimGonderPage : ContentPage
    {
        // ============================
        // 📏 RESPONSIVE ÖLÇEKLEME
        // ============================
        private const double GlobalShrink = 0.92;
        public double BaseWidth { get; set; } = 460.0;
        public double MaxDesktopScale { get; set; } = 1.65;
        public double MaxMobileScale { get; set; } = 1.18;
        public double? UserZoomFactor { get; set; } = null;
        public double DesktopEffectiveWidthCap { get; set; } = 2200;

        private readonly (double w, double s)[] _desktopScaleCurve =
        {
            (  800, 0.98), (1000, 1.04), (1200, 1.10), (1366, 1.16),
            ( 1500, 1.22), (1680, 1.30), (1920, 1.42), (2200, 1.54), (2560, 1.62),
        };

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private double AutoScaleFromCurve(double widthDip)
        {
            if (widthDip <= _desktopScaleCurve[0].w) return _desktopScaleCurve[0].s;
            if (widthDip >= _desktopScaleCurve[^1].w) return _desktopScaleCurve[^1].s;
            for (int i = 0; i < _desktopScaleCurve.Length - 1; i++)
            {
                var (w1, s1) = _desktopScaleCurve[i];
                var (w2, s2) = _desktopScaleCurve[i + 1];
                if (widthDip >= w1 && widthDip <= w2)
                    return Lerp(s1, s2, (widthDip - w1) / (w2 - w1));
            }
            return 1.0;
        }

        private void RecomputeScale()
        {
            var w = Width;
            if (double.IsNaN(w) || w <= 0) return;

            bool isDesktop = DeviceInfo.Idiom == DeviceIdiom.Desktop;
            double widthDip = isDesktop ? Math.Min(w, DesktopEffectiveWidthCap) : w;

            double scale = isDesktop
                ? Math.Clamp((UserZoomFactor is > 0 ? AutoScaleFromCurve(widthDip) * UserZoomFactor.Value
                                                    : AutoScaleFromCurve(widthDip)), 0.9, MaxDesktopScale)
                : Math.Clamp(widthDip / BaseWidth, 0.95, MaxMobileScale);

            scale *= GlobalShrink;

            ScaledFontSize = 24 * scale;
            ScaledFontSize2 = 20 * scale;
            ScaledFontSize3 = 16 * scale;
            ScaledFontSize4 = 14 * scale;

            ScaledPadding = new Thickness(20 * scale);
            ScaledSmallPadding = new Thickness(Math.Max(8, 12 * scale));
            ScaledSpacing = Math.Max(8, 16 * scale);
            ScaledSmallSpacing = Math.Max(6, 10 * scale);

            ScaledButtonHeight = Math.Max(36, 44 * scale);
            ScaledPrimaryButtonWidth = Math.Max(130, 180 * scale);
            ScaledSecondaryButtonWidth = Math.Max(170, 260 * scale);

            ScaledIcon = Math.Max(14, 20 * scale);
            ScaledIconTouch = Math.Max(28, 32 * scale);

            ScaledEditorHeight = Math.Max(84, 110 * scale);

            ScaledDialogWidth = Math.Max(400, 520 * scale);
            ScaledDialogHeight = Math.Max(440, 560 * scale);
            ScaledDialogWidthMobile = Math.Max(300, 360 * scale);
            ScaledDialogHeightMobile = Math.Max(420, 520 * scale);

            ScaledDateWidth = Math.Max(150, 200 * scale);
            ScaledSearchWidth = Math.Max(190, 260 * scale);
            ScaledDateWidthMobile = Math.Max(130, 150 * scale);
            ScaledSearchWidthMobile = Math.Max(140, 170 * scale);

            ScaledPickAdminWidth = Math.Max(170, 220 * scale);
            ScaledListHeightMobile = Math.Max(250, 320 * scale);

            OnPropertyChanged(nameof(ScaledFontSize));
            OnPropertyChanged(nameof(ScaledFontSize2));
            OnPropertyChanged(nameof(ScaledFontSize3));
            OnPropertyChanged(nameof(ScaledFontSize4));
            OnPropertyChanged(nameof(ScaledPadding));
            OnPropertyChanged(nameof(ScaledSmallPadding));
            OnPropertyChanged(nameof(ScaledSpacing));
            OnPropertyChanged(nameof(ScaledSmallSpacing));
            OnPropertyChanged(nameof(ScaledButtonHeight));
            OnPropertyChanged(nameof(ScaledPrimaryButtonWidth));
            OnPropertyChanged(nameof(ScaledSecondaryButtonWidth));
            OnPropertyChanged(nameof(ScaledIcon));
            OnPropertyChanged(nameof(ScaledIconTouch));
            OnPropertyChanged(nameof(ScaledEditorHeight));
            OnPropertyChanged(nameof(ScaledDialogWidth));
            OnPropertyChanged(nameof(ScaledDialogHeight));
            OnPropertyChanged(nameof(ScaledDialogWidthMobile));
            OnPropertyChanged(nameof(ScaledDialogHeightMobile));
            OnPropertyChanged(nameof(ScaledDateWidth));
            OnPropertyChanged(nameof(ScaledSearchWidth));
            OnPropertyChanged(nameof(ScaledDateWidthMobile));
            OnPropertyChanged(nameof(ScaledSearchWidthMobile));
            OnPropertyChanged(nameof(ScaledPickAdminWidth));
            OnPropertyChanged(nameof(ScaledListHeightMobile));
        }

        // XAML binding targetları
        public double ScaledFontSize { get; private set; } = 24;
        public double ScaledFontSize2 { get; private set; } = 20;
        public double ScaledFontSize3 { get; private set; } = 16;
        public double ScaledFontSize4 { get; private set; } = 14;

        public Thickness ScaledPadding { get; private set; } = new(20);
        public Thickness ScaledSmallPadding { get; private set; } = new(12);
        public double ScaledSpacing { get; private set; } = 16;
        public double ScaledSmallSpacing { get; private set; } = 10;

        public double ScaledButtonHeight { get; private set; } = 44;
        public double ScaledPrimaryButtonWidth { get; private set; } = 180;
        public double ScaledSecondaryButtonWidth { get; private set; } = 260;

        public double ScaledIcon { get; private set; } = 20;
        public double ScaledIconTouch { get; private set; } = 32;

        public double ScaledEditorHeight { get; private set; } = 110;

        public double ScaledDialogWidth { get; private set; } = 520;
        public double ScaledDialogHeight { get; private set; } = 560;
        public double ScaledDialogWidthMobile { get; private set; } = 360;
        public double ScaledDialogHeightMobile { get; private set; } = 520;

        public double ScaledDateWidth { get; private set; } = 200;
        public double ScaledSearchWidth { get; private set; } = 260;
        public double ScaledDateWidthMobile { get; private set; } = 150;
        public double ScaledSearchWidthMobile { get; private set; } = 170;

        public double ScaledPickAdminWidth { get; private set; } = 220;
        public double ScaledListHeightMobile { get; private set; } = 320;

        // ============================
        // 🔧 SAYFA LOJİĞİ
        // ============================
        private readonly string token;
        private readonly HttpClient _http;

        private ObservableCollection<Kullanici> adminKullanicilar = new();
        private readonly ObservableCollection<Kullanici> _adminFiltre = new(); // popup’ta gösterilen

        private ObservableCollection<Bildirim> gonderilenBildirimler = new();

        public BildirimGonderPage(HttpClient httpClient, string kullaniciToken)
        {
            InitializeComponent();
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            token = kullaniciToken ?? string.Empty;

            if (!_http.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
                _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var auth = _http.DefaultRequestHeaders.Authorization;
            if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                if (!string.IsNullOrWhiteSpace(token))
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            BindingContext = this;

            SizeChanged += (_, __) => RecomputeScale();
            RecomputeScale();

            EnsureAdminListTemplates();
            EnsureAdminPickTemplates(); // XAML’de ItemTemplate varsa dokunmaz

            _ = AdminleriYukle();
            _ = GonderilenBildirimleriYukle();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            RecomputeScale();

            // Arama kutusuna canlı filtre bağla (XAML’de event yoksa da işler)
            if (this.FindByName<Entry>("entryAdminAra") is Entry ara)
            {
                ara.TextChanged -= EntryAdminAra_TextChanged;
                ara.TextChanged += EntryAdminAra_TextChanged;
            }

            if (adminKullanicilar.Count == 0)
                await AdminleriYukle();
        }

        // Admin listesinde AdSoyad görünsün
        private void EnsureAdminListTemplates()
        {
            if (lstAdminler == null) return;

            lstAdminler.ItemTemplate = new DataTemplate(() =>
            {
                var frame = new Frame
                {
                    BackgroundColor = Color.FromArgb("#eae6e2"),
                    CornerRadius = 12,
                    Padding = 12,
                    Margin = new Thickness(4),
                    HasShadow = false
                };
                var label = new Label
                {
                    FontSize = ScaledFontSize3,
                    TextColor = Color.FromArgb("#34495e")
                };
                label.SetBinding(Label.TextProperty, "AdSoyad");
                frame.Content = label;
                return frame;
            });
        }

        // XAML’de ItemTemplate tanımlıysa değiştirme; yoksa basit bir tane ver
        private void EnsureAdminPickTemplates()
        {
            if (cvAdminSecim == null) return;
            if (cvAdminSecim.ItemTemplate != null) return; // XAML tasarımını koru

            cvAdminSecim.ItemTemplate = new DataTemplate(() =>
            {
                var frame = new Frame
                {
                    BackgroundColor = Color.FromArgb("#ecf0f1"),
                    CornerRadius = 12,
                    Padding = 10,
                    Margin = new Thickness(4),
                    HasShadow = false
                };
                var label = new Label
                {
                    FontSize = ScaledFontSize3,
                    TextColor = Colors.Black
                };
                label.SetBinding(Label.TextProperty, "AdSoyad");
                frame.Content = label;
                return frame;
            });
        }

        // 🚫 Null/yanlış tip korumalı + UI thread güvenli seçim komutu
        public ICommand ToggleSelectionCommand => new Command<object>(item =>
        {
            if (item is not Kullanici admin) return;
            if (cvAdminSecim?.SelectedItems is null) return;

            void Toggle()
            {
                var list = cvAdminSecim!.SelectedItems;
                if (list.Contains(admin)) list.Remove(admin);
                else list.Add(admin);
            }

            if (Dispatcher.IsDispatchRequired) Dispatcher.Dispatch(Toggle);
            else Toggle();
        });

        private async Task AdminleriYukle()
        {
            try
            {
                var admins = await _http.GetFromJsonAsync<List<Kullanici>>("/api/KullanicilarApi/adminler");
                adminKullanicilar = new ObservableCollection<Kullanici>(admins ?? new());

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (lstAdminler != null) lstAdminler.ItemsSource = adminKullanicilar;

                        _adminFiltre.Clear();
                        foreach (var a in adminKullanicilar) _adminFiltre.Add(a);

                        if (cvAdminSecim != null)
                            cvAdminSecim.ItemsSource = _adminFiltre;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"AdminleriYukle -> UI set hata: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Adminler yüklenemedi: {ex.Message}", "Tamam");
            }
        }

        // 🔎 Arama kutusu -> canlı filtre
        private void EntryAdminAra_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyAdminFilter(e.NewTextValue);
        }

        private void ApplyAdminFilter(string? text)
        {
            string q = (text ?? string.Empty).Trim().ToLowerInvariant();

            var src = string.IsNullOrEmpty(q)
                ? adminKullanicilar
                : new ObservableCollection<Kullanici>(
                    adminKullanicilar.Where(a =>
                        (a.AdSoyad ?? string.Empty).ToLowerInvariant().Contains(q)));

            _adminFiltre.Clear();
            foreach (var a in src) _adminFiltre.Add(a);
        }

        // “Temizle” butonu için
        private void BtnAdminAraTemizle_Clicked(object sender, EventArgs e)
        {
            if (this.FindByName<Entry>("entryAdminAra") is Entry ara)
                ara.Text = string.Empty;
        }

        private async void BtnGonder_Clicked(object sender, EventArgs e)
        {
            try
            {
                var btn = (Button)sender;
                await btn.ScaleTo(0.9, 100);
                await btn.ScaleTo(1, 100);

                List<Kullanici> secilenAdminler;
                string mesaj;

                if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
                {
                    secilenAdminler = lstAdminler?.SelectedItems?.Cast<Kullanici>().ToList() ?? new List<Kullanici>();
                    mesaj = entryMesajDesktop?.Text ?? string.Empty;
                }
                else
                {
                    secilenAdminler = cvAdminSecim?.SelectedItems?.Cast<Kullanici>().ToList() ?? new List<Kullanici>();
                    mesaj = entryMesajMobile?.Text ?? string.Empty;
                }

                if (!secilenAdminler.Any())
                {
                    await DisplayAlert("Uyarı", "Lütfen en az bir admin seçin.", "Tamam");
                    return;
                }

                if (string.IsNullOrWhiteSpace(mesaj))
                {
                    await DisplayAlert("Uyarı", "Mesaj boş olamaz.", "Tamam");
                    return;
                }

                var dto = new
                {
                    Mesaj = mesaj.Trim(),
                    GonderimTarihi = DateTime.Now,
                    AdminIdListesi = secilenAdminler.Select(a => a.Id).ToList()
                };

                var json = JsonSerializer.Serialize(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync("/api/Bildirimler", content);

                if (response.IsSuccessStatusCode)
                {
                    if (entryMesajDesktop != null) entryMesajDesktop.Text = string.Empty;
                    if (entryMesajMobile != null) entryMesajMobile.Text = string.Empty;
                    await DisplayAlert("Başarılı", "Bildirim(ler) gönderildi.", "Tamam");
                    await GonderilenBildirimleriYukle();
                }
                else
                {
                    string msg = await response.Content.ReadAsStringAsync();
                    await DisplayAlert("Hata", $"Gönderilemedi: {msg}", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Bir hata oluştu: {ex.Message}", "Tamam");
            }
        }

        private async Task GonderilenBildirimleriYukle()
        {
            try
            {
                var bildirimler = await _http.GetFromJsonAsync<List<Bildirim>>("/api/Bildirimler/gonderilen");
                gonderilenBildirimler = new ObservableCollection<Bildirim>(bildirimler ?? new());

                var bugunku = gonderilenBildirimler
                    .Where(b => b.GonderimTarihi.Date == DateTime.Today)
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (lstGecmisBildirimlerDesktop != null)
                            lstGecmisBildirimlerDesktop.ItemsSource = new ObservableCollection<Bildirim>(bugunku);
                        if (lstGecmisBildirimlerMobile != null)
                            lstGecmisBildirimlerMobile.ItemsSource = new ObservableCollection<Bildirim>(bugunku);

                        if (lstGecmisPopupDesktop != null) lstGecmisPopupDesktop.ItemsSource = gonderilenBildirimler;
                        if (lstGecmisPopupMobile != null) lstGecmisPopupMobile.ItemsSource = gonderilenBildirimler;
                    }
                    catch (Exception ex) { Debug.WriteLine($"UI set hata: {ex}"); }
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Geçmiş bildirimler yüklenemedi: {ex.Message}", "Tamam");
            }
        }

        // Güvenli popup açma / kapama
        private async void BtnGecmisGoster_Clicked(object sender, EventArgs e)
        {
            try
            {
                var hedef = DeviceInfo.Idiom == DeviceIdiom.Desktop ? frameGecmisDesktop : frameGecmisMobile;
                if (hedef == null) return;

                await MainThread.InvokeOnMainThreadAsync(() => ShowPopupSafeAsync(hedef));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Popup açılamadı: {ex.Message}", "Tamam");
            }
        }

        private async Task ShowPopupSafeAsync(View hedef)
        {
            try
            {
                hedef.IsVisible = true;
                hedef.Opacity = 0;
                hedef.Scale = 0.95;

                await Task.Delay(40);

                await Task.WhenAll(
                    hedef.FadeTo(1, 250),
                    hedef.ScaleTo(1, 250, Easing.CubicOut)
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowPopupSafeAsync hata: {ex}");
                try { hedef.IsVisible = false; } catch { }
            }
        }

        private async void BtnGecmisGizle_Clicked(object sender, EventArgs e)
        {
            try
            {
                var hedef = DeviceInfo.Idiom == DeviceIdiom.Desktop ? frameGecmisDesktop : frameGecmisMobile;
                if (hedef == null) return;

                await MainThread.InvokeOnMainThreadAsync(() => HidePopupSafeAsync(hedef));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Popup kapatılamadı: {ex.Message}", "Tamam");
            }
        }

        private async Task HidePopupSafeAsync(View hedef)
        {
            try
            {
                await hedef.FadeTo(0, 200);
                await Task.Delay(30);
                hedef.IsVisible = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HidePopupSafeAsync hata: {ex}");
                try { hedef.IsVisible = false; } catch { }
            }
        }

        private void EntryAra_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string anahtar = e.NewTextValue?.ToLower() ?? "";
                var filtreli = gonderilenBildirimler
                    .Where(b => (b.Mesaj ?? string.Empty).ToLower().Contains(anahtar))
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
                            lstGecmisPopupDesktop.ItemsSource = new ObservableCollection<Bildirim>(filtreli);
                        else
                            lstGecmisPopupMobile.ItemsSource = new ObservableCollection<Bildirim>(filtreli);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"EntryAra_TextChanged -> UI set hata: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EntryAra_TextChanged hata: {ex}");
            }
        }

        private void DatePickerTarih_DateSelected(object sender, DateChangedEventArgs e)
        {
            try
            {
                DateTime secilen = e.NewDate.Date;
                var filtreli = gonderilenBildirimler
                    .Where(b => b.GonderimTarihi.Date == secilen)
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
                            lstGecmisPopupDesktop.ItemsSource = new ObservableCollection<Bildirim>(filtreli);
                        else
                            lstGecmisPopupMobile.ItemsSource = new ObservableCollection<Bildirim>(filtreli);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DatePickerTarih_DateSelected -> UI set hata: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DatePickerTarih_DateSelected hata: {ex}");
            }
        }

        private async void BtnBildirimSil_Clicked(object sender, EventArgs e)
        {
            var btn = sender as Button;
            var silinecek = btn?.BindingContext as Bildirim;
            if (silinecek == null) return;

            try
            {
                await btn.RelRotateTo(10, 50);
                await btn.RelRotateTo(-20, 50);
                await btn.RelRotateTo(10, 50);
            }
            catch { /* animasyon başarısız olsa da devam et */ }

            bool onay = await DisplayAlert("Sil", "Bu bildirimi silmek istiyor musunuz?", "Evet", "Hayır");
            if (!onay) return;

            try
            {
                var response = await _http.DeleteAsync($"/api/Bildirimler/{silinecek.Id}");

                if (response.IsSuccessStatusCode)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        gonderilenBildirimler.Remove(silinecek);
                        await GonderilenBildirimleriYukle();
                    });
                }
                else
                {
                    await DisplayAlert("Hata", "Bildirim silinemedi.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Silme hatası: {ex.Message}", "Tamam");
            }
        }

        // XAML: SizeChanged="ContentPage_SizeChanged"
        private void ContentPage_SizeChanged(object sender, EventArgs e) => RecomputeScale();

        private void BtnAdminSec_Clicked(object sender, EventArgs e)
        {
            if (this.FindByName<Entry>("entryAdminAra") is Entry ara)
                ApplyAdminFilter(ara.Text);
            else
                ApplyAdminFilter(string.Empty);

            if (frameAdminPopup != null) frameAdminPopup.IsVisible = true;
        }

        private void BtnAdminOnayla_Clicked(object sender, EventArgs e)
        {
            if (Dispatcher.IsDispatchRequired)
            {
                Dispatcher.Dispatch(() => BtnAdminOnayla_Clicked(sender, e));
                return;
            }

            if (frameAdminPopup != null) frameAdminPopup.IsVisible = false;

            var secilen = cvAdminSecim?.SelectedItems?.Cast<Kullanici>()
                              .Select(x => x.AdSoyad).ToList() ?? new List<string>();

            if (lblSecilenAdminler != null)
            {
                lblSecilenAdminler.Text = string.Join(", ", secilen);
                lblSecilenAdminler.IsVisible = secilen.Any();
            }
        }

        private void BtnAdminIptal_Clicked(object sender, EventArgs e)
        {
            if (frameAdminPopup != null) frameAdminPopup.IsVisible = false;
            cvAdminSecim?.SelectedItems?.Clear();
            if (lblSecilenAdminler != null)
            {
                lblSecilenAdminler.Text = "";
                lblSecilenAdminler.IsVisible = false;
            }

            if (this.FindByName<Entry>("entryAdminAra") is Entry ara)
                ara.Text = string.Empty;
        }

        private void AdminCheckBox_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.BindingContext is not Kullanici admin) return;
            var list = cvAdminSecim?.SelectedItems; if (list is null) return;

            void Apply()
            {
                bool contains = list.Cast<object>().Contains(admin);
                if (e.Value && !contains) list.Add(admin);
                else if (!e.Value && contains) list.Remove(admin);
            }
            if (Dispatcher.IsDispatchRequired) Dispatcher.Dispatch(Apply); else Apply();
        }
    }
}
