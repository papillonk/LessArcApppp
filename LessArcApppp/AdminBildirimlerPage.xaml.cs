using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.ApplicationModel;

namespace LessArcApppp;

public partial class AdminBildirimlerPage : ContentPage
{
    private readonly HttpClient _http;
    private readonly string _token;

    private List<GelenBildirimDto> _tumGecmisBildirimler = new();
    private CancellationTokenSource? _searchCts;

    // 🔎 Tarih filtre durumu
    private DateTime? _filterDate = null;          // null => tüm geçmiş
    private bool _datePickerUserChanged = false;   // açılışta otomatik set'i yut

    // --- ölçek props ---
    public double BaseWidth { get; set; } = 430.0;
    public double MaxDesktopScale { get; set; } = 1.85;
    public double MaxMobileScale { get; set; } = 1.30;
    public double? UserZoomFactor { get; set; } = null;
    public double DesktopEffectiveWidthCap { get; set; } = 2200;

    private readonly (double w, double s)[] _desktopScaleCurve =
    {
        (800, 1.00), (1000, 1.06), (1200, 1.12), (1366, 1.18),
        (1500, 1.28), (1680, 1.38), (1920, 1.52), (2200, 1.66), (2560, 1.80),
    };

    public double ScaledFontSize { get; set; } = 26;
    public double ScaledFontSize2 { get; set; } = 20;
    public double ScaledFontSize3 { get; set; } = 14;
    public double ScaledFontSize4 { get; set; } = 12;
    public double ScaledPadding { get; set; } = 15;
    public double ScaledSmallPadding { get; set; } = 10;
    public double ScaledSpacing { get; set; } = 10;
    public double ScaledSmallSpacing { get; set; } = 6;
    public double ScaledButtonHeight { get; set; } = 42;
    public double ScaledButtonWidth { get; set; } = 210;
    public double ScaledButtonIcon { get; set; } = 24;

    // XAML’de popupContent.WidthRequest, buna bağlı
    public double ScaledPopupWidth { get; set; } = 380;

    // Popup için mobil kompakt faktörler
    public double PopupCompactFactor { get; set; } = 0.85;
    public double PopupFontSize { get; private set; }
    public double PopupFontSize2 { get; private set; }
    public double PopupFontSize3 { get; private set; }
    public double PopupFontSize4 { get; private set; }
    public double PopupPadding { get; private set; }
    public double PopupSmallPad { get; private set; }
    public double PopupSpacing { get; private set; }
    public double PopupSmallSpacing { get; private set; }
    public double PopupWidth { get; private set; }

    public Thickness M_ItemBetween { get; private set; }
    public Thickness M_TopSmall { get; private set; }
    public Thickness M_VertSmall { get; private set; }
    public Thickness P_BottomSmall { get; private set; }

    public AdminBildirimlerPage(HttpClient httpClient, string? kullaniciToken = null)
    {
        InitializeComponent();

        _http = httpClient;
        _token = kullaniciToken ?? string.Empty;
        BindingContext = this;

        CultureInfo.DefaultThreadCurrentCulture = new("tr-TR");
        CultureInfo.DefaultThreadCurrentUICulture = new("tr-TR");

        if (!string.IsNullOrWhiteSpace(_token))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);

        SizeChanged += (_, __) => { RecomputeScale(); ApplyPopupSizingForIdiom(); };

        // DatePicker başlangıç değerini verelim ama ilk açılışta filtre uygulanmasın.
        try { dpGecmisTarih.Date = DateTime.Today; } catch { }
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private double AutoScaleFromCurve(double wDip)
    {
        if (wDip <= _desktopScaleCurve[0].w) return _desktopScaleCurve[0].s;
        if (wDip >= _desktopScaleCurve[^1].w) return _desktopScaleCurve[^1].s;
        for (int i = 0; i < _desktopScaleCurve.Length - 1; i++)
        {
            var (w1, s1) = _desktopScaleCurve[i];
            var (w2, s2) = _desktopScaleCurve[i + 1];
            if (wDip >= w1 && wDip <= w2)
            {
                var t = (wDip - w1) / (w2 - w1);
                return Lerp(s1, s2, t);
            }
        }
        return 1.0;
    }

    private void RecomputeScale()
    {
        double w = Width; if (double.IsNaN(w) || w <= 0) return;
        bool isDesktop = DeviceInfo.Idiom == DeviceIdiom.Desktop || DeviceInfo.Idiom == DeviceIdiom.TV;
        bool isPhone = DeviceInfo.Idiom == DeviceIdiom.Phone;

        double widthDip = isDesktop ? Math.Min(w, DesktopEffectiveWidthCap) : w;

        double scale = isDesktop
            ? Math.Clamp((UserZoomFactor is > 0 ? UserZoomFactor.Value : 1) * AutoScaleFromCurve(widthDip), 1.0, MaxDesktopScale)
            : Math.Clamp(widthDip / BaseWidth, 1.0, MaxMobileScale);

        // --- metin/spacing ---
        ScaledFontSize = 26 * scale; ScaledFontSize2 = 20 * scale;
        ScaledFontSize3 = 14 * scale; ScaledFontSize4 = 12 * scale;
        ScaledPadding = 15 * scale; ScaledSmallPadding = 10 * scale;
        ScaledSpacing = 10 * scale; ScaledSmallSpacing = 6 * scale;
        ScaledButtonHeight = 40 * scale; ScaledButtonWidth = 200 * scale;
        ScaledButtonIcon = 22 * scale;

        // --- popup genişliği ---
        if (isPhone)
            ScaledPopupWidth = Math.Min(380, 340 * scale);
        else
            ScaledPopupWidth = Math.Min(760, 420 * scale);

        // --- popup içi kompakt değerler ---
        double p = PopupCompactFactor;
        PopupFontSize = ScaledFontSize * p; PopupFontSize2 = ScaledFontSize2 * p;
        PopupFontSize3 = ScaledFontSize3 * p; PopupFontSize4 = ScaledFontSize4 * p;
        PopupPadding = ScaledPadding * p; PopupSmallPad = ScaledSmallPadding * p;
        PopupSpacing = ScaledSpacing * p; PopupSmallSpacing = ScaledSmallSpacing * p;
        PopupWidth = isPhone ? Math.Min(360, 340 * scale) : Math.Min(520, 420 * p);

        M_ItemBetween = new(0, ScaledSmallSpacing, 0, 0);
        M_TopSmall = new(0, ScaledSmallSpacing, 0, 0);
        M_VertSmall = new(0, ScaledSmallSpacing, 0, ScaledSmallSpacing);
        P_BottomSmall = new(0, 0, 0, ScaledSmallSpacing);

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
        OnPropertyChanged(nameof(ScaledPopupWidth));
        OnPropertyChanged(nameof(PopupFontSize));
        OnPropertyChanged(nameof(PopupFontSize2));
        OnPropertyChanged(nameof(PopupFontSize3));
        OnPropertyChanged(nameof(PopupFontSize4));
        OnPropertyChanged(nameof(PopupPadding));
        OnPropertyChanged(nameof(PopupSmallPad));
        OnPropertyChanged(nameof(PopupSpacing));
        OnPropertyChanged(nameof(PopupSmallSpacing));
        OnPropertyChanged(nameof(PopupWidth));
        OnPropertyChanged(nameof(M_ItemBetween));
        OnPropertyChanged(nameof(M_TopSmall));
        OnPropertyChanged(nameof(M_VertSmall));
        OnPropertyChanged(nameof(P_BottomSmall));
    }

    // Sadece mobilde popup yüksekliği/scale’i küçült
    private void ApplyPopupSizingForIdiom()
    {
        if (popupContent is null) return;

        if (DeviceInfo.Idiom == DeviceIdiom.Phone)
        {
            popupContent.HeightRequest = 520;   // mobilde daha kısa
        }
        else
        {
            popupContent.HeightRequest = -1;    // auto
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { TemaYonetici.TemayiYukle(); } catch { }
        RecomputeScale();
        ApplyPopupSizingForIdiom();
        await EnsureAuthAsync();
        await YukleBugunBildirimleri();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { _searchCts?.Cancel(); _searchCts?.Dispose(); _searchCts = null; } catch { }
    }

    private async Task EnsureAuthAsync()
    {
        var h = _http.DefaultRequestHeaders.Authorization;
        if (h is { Scheme: "Bearer" } && !string.IsNullOrWhiteSpace(h.Parameter)) return;

        var tok = !string.IsNullOrWhiteSpace(_token) ? _token : await SecureStorage.GetAsync("auth_token");
        if (!string.IsNullOrWhiteSpace(tok))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok);
    }

    private async Task YukleBugunBildirimleri()
    {
        try
        {
            var inbox = await GetAdminInboxAsync();

            // ✔️ tüm okunmamış hedefleri okundu işaretle
            var allUnreadTargetIds = inbox
                .Where(x => !x.IsRead)
                .Select(x => x.HedefId)
                .Distinct()
                .ToList();

            if (allUnreadTargetIds.Count > 0)
            {
                await MarkReadAsync(allUnreadTargetIds);
                foreach (var it in inbox.Where(x => allUnreadTargetIds.Contains(x.HedefId)))
                    it.IsRead = true;
            }

            // Bugün için ayrı liste (sayfadaki “bugün” bölümü)
            var bugun = DateTime.Today;
            var bugunkuler = inbox
                .Where(b => b.GonderimTarihi.ToLocalTime().Date == bugun)
                .OrderByDescending(b => b.GonderimTarihi)
                .ToList();
            lstBugunBildirimler.ItemsSource = bugunkuler;

            // Geçmiş: TÜM ZAMANLAR
            _tumGecmisBildirimler = inbox
                .OrderByDescending(x => x.GonderimTarihi)
                .ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Hata", $"Bugünkü bildirimler yüklenemedi:\n{ex.Message}", "Tamam");
        }
    }

    // === POPUP AÇ/KAPAT ===
    private async void BtnGecmisToggle_Clicked(object sender, EventArgs e)
    {
        try
        {
            entryAra.Text = string.Empty;

            // Popup her açıldığında: tarih filtresi PASİF (tüm geçmiş)
            _filterDate = null;
            _datePickerUserChanged = false;   // ilk date change'i kullanıcı yapana kadar
            try { dpGecmisTarih.Date = DateTime.Today; } catch { }

            if (_tumGecmisBildirimler.Count == 0)
                _tumGecmisBildirimler = (await GetAdminInboxAsync())
                    .OrderByDescending(b => b.GonderimTarihi).ToList();

            ApplyPopupSizingForIdiom();

            popupOverlay.Opacity = 0;
            popupContent.Scale = (DeviceInfo.Idiom == DeviceIdiom.Phone) ? 0.88 : 1.0;
            popupOverlay.IsVisible = true;

            await Task.WhenAll(
                popupOverlay.FadeTo(1, 220, Easing.CubicIn),
                popupContent.ScaleTo(1, 220, Easing.CubicOut)
            );

            FiltreleGecmisBildirimler();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Hata", $"Geçmiş bildirimler açılırken hata:\n{ex.Message}", "Tamam");
        }
    }

    private async void BtnGecmisKapat_Clicked(object sender, EventArgs e)
    {
        try
        {
            var targetScale = (DeviceInfo.Idiom == DeviceIdiom.Phone) ? 0.88 : 1.0;
            await Task.WhenAll(
                popupOverlay.FadeTo(0, 180, Easing.CubicIn),
                popupContent.ScaleTo(targetScale, 180, Easing.CubicOut)
            );
            popupOverlay.IsVisible = false;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Hata", $"Kapatma sırasında hata:\n{ex.Message}", "Tamam");
        }
    }

    // === FİLTRELER ===
    private async void DpGecmisTarih_DateSelected(object sender, DateChangedEventArgs e)
    {
        // Bu event bazen arka arkaya tetikleniyor; UI hazır olsun.
        await Task.Yield();

        // Kullanıcı etkileşimi ile geldiğini işaretle
        _datePickerUserChanged = true;
        _filterDate = e.NewDate.Date;

        FiltreleGecmisBildirimler();
    }

    private void EntryAra_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchCts?.Cancel(); _searchCts?.Dispose();
        _searchCts = new(); var ct = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(200, ct);
                if (ct.IsCancellationRequested) return;
                MainThread.BeginInvokeOnMainThread(FiltreleGecmisBildirimler);
            }
            catch { }
        }, ct);
    }

    private static string TrFold(string? s)
        => (s ?? string.Empty).ToLower(new CultureInfo("tr-TR"));

    private void FiltreleGecmisBildirimler()
    {
        IEnumerable<GelenBildirimDto> kaynak = _tumGecmisBildirimler;

        // 📅 Eğer kullanıcı tarihe dokunduysa _filterDate set edilir; o günün kayıtlarını göster
        if (_datePickerUserChanged && _filterDate.HasValue)
        {
            var hedefGun = _filterDate.Value.Date;
            kaynak = kaynak.Where(b => b.GonderimTarihi.ToLocalTime().Date == hedefGun);
        }

        // 🔎 Kelime filtresi
        string keyword = TrFold(entryAra.Text);
        if (!string.IsNullOrEmpty(keyword))
        {
            kaynak = kaynak.Where(b =>
                TrFold(b.Mesaj).Contains(keyword) ||
                TrFold(GetSender(b)).Contains(keyword));
        }

        lstGecmisBildirimler.ItemsSource = kaynak.ToList();
    }

    // === API ===
    private async Task<List<GelenBildirimDto>> GetAdminInboxAsync()
    {
        var resp = await _http.GetAsync("/api/Bildirimler/admin/inbox");
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<List<GelenBildirimDto>>(stream,
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new List<GelenBildirimDto>();
    }

    private async Task MarkReadAsync(IEnumerable<int> hedefIds)
    {
        var ids = hedefIds?.Distinct().ToList() ?? new();
        if (ids.Count == 0) return;

        var body = new { Ids = ids };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("/api/Bildirimler/admin/mark-read", content);
        resp.EnsureSuccessStatusCode();
    }

    private static string GetSender(GelenBildirimDto b)
        => string.IsNullOrWhiteSpace(b.GonderenAdSoyad) ? b.GonderenKullanici : b.GonderenAdSoyad;
}

// DTO (API ile uyumlu)
public class GelenBildirimDto
{
    public int HedefId { get; set; }            // BildirimHedefi.Id
    public int BildirimId { get; set; }
    public string Mesaj { get; set; } = "";
    public DateTime GonderimTarihi { get; set; }
    public int GonderenId { get; set; }
    public string GonderenAdSoyad { get; set; } = "";
    public string GonderenKullanici { get; set; } = "";
    public bool IsRead { get; set; }
}
