using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Newtonsoft.Json;

namespace LessArcApppp;

public partial class AdminZamanRaporPage : ContentPage
{
    // ------------------- DI -------------------
    private readonly HttpClient _http;  // DI'dan gelen tekil HttpClient
    private readonly string token;

    // ------------------- UI Data -------------------
    public ObservableCollection<TakvimGunuDto> TakvimGunleri { get; } = new();
    private List<TakvimGunuDto> tumProjeliGunler = new();

    // ------------------- Ölçeklenebilir (Binding) Özellikler -------------------
    // Sayfa/izgaralar
    public Thickness ScaledPadding { get; private set; } = new(16);
    public double ScaledSpacing { get; private set; } = 16;
    public double SmallSpacing { get; private set; } = 8;

    // Yazılar
    public double TitleFontSize { get; private set; } = 20; // üst açıklama
    public double RowTitleFontSize { get; private set; } = 16; // gün satırı tarih yazısı
    public double ButtonFontSize { get; private set; } = 14;

    // Butonlar
    public double ButtonHeight { get; private set; } = 48;
    public double TallButtonHeight { get; private set; } = 60;
    public Thickness ButtonPadding { get; private set; } = new(10, 6);

    // İkon/nokta
    public double IconSize { get; private set; } = 26;
    public double DotSize { get; private set; } = 20;
    public double DotCorner { get; private set; } = 10;
    public Thickness DotMargin { get; private set; } = new(2);

    // Liste elemanı (kart) boşlukları
    public Thickness ItemMargin { get; private set; } = new(12, 8);
    public Thickness ItemPadding { get; private set; } = new(16, 10);

    // Sık kullanılan margin helper'ları
    public Thickness M_BottomSmall { get; private set; } = new(0, 0, 0, 8);
    public Thickness M_LeftSmall { get; private set; } = new(10, 0, 0, 0);
    public Thickness M_LeftTiny { get; private set; } = new(4, 0, 0, 0);

    // ------------------- Ölçek Eğrisi -------------------
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

        bool isDesktop =
            DeviceInfo.Idiom == DeviceIdiom.Desktop ||
            DeviceInfo.Idiom == DeviceIdiom.Tablet;

        double scale = isDesktop
            ? Math.Clamp(AutoScaleFromCurve(w), 1.0, MaxDeskScale)
            : Math.Clamp(w / BasePhoneWidth, 1.0, MaxPhoneScale);

        // temel metrikler
        ScaledPadding = new Thickness(16 * scale);
        ScaledSpacing = 16 * scale;
        SmallSpacing = 8 * scale;

        TitleFontSize = 20 * scale;
        RowTitleFontSize = 16 * scale;
        ButtonFontSize = 14 * scale;

        ButtonHeight = 48 * scale;
        TallButtonHeight = 60 * scale;
        ButtonPadding = new Thickness(10 * scale, 6 * scale);

        IconSize = 26 * scale;

        DotSize = 20 * scale;
        DotCorner = DotSize / 2.0;
        DotMargin = new Thickness(2 * scale);

        ItemMargin = new Thickness(isDesktop ? 20 * scale : 6 * scale,
                                         isDesktop ? 8 * scale : 6 * scale);
        ItemPadding = new Thickness(16 * scale, 10 * scale);

        M_BottomSmall = new Thickness(0, 0, 0, 8 * scale);
        M_LeftSmall = new Thickness(10 * scale, 0, 0, 0);
        M_LeftTiny = new Thickness(4 * scale, 0, 0, 0);

        // property-changed bildir
        OnPropertyChanged(nameof(ScaledPadding));
        OnPropertyChanged(nameof(ScaledSpacing));
        OnPropertyChanged(nameof(SmallSpacing));
        OnPropertyChanged(nameof(TitleFontSize));
        OnPropertyChanged(nameof(RowTitleFontSize));
        OnPropertyChanged(nameof(ButtonFontSize));
        OnPropertyChanged(nameof(ButtonHeight));
        OnPropertyChanged(nameof(TallButtonHeight));
        OnPropertyChanged(nameof(ButtonPadding));
        OnPropertyChanged(nameof(IconSize));
        OnPropertyChanged(nameof(DotSize));
        OnPropertyChanged(nameof(DotCorner));
        OnPropertyChanged(nameof(DotMargin));
        OnPropertyChanged(nameof(ItemMargin));
        OnPropertyChanged(nameof(ItemPadding));
        OnPropertyChanged(nameof(M_BottomSmall));
        OnPropertyChanged(nameof(M_LeftSmall));
        OnPropertyChanged(nameof(M_LeftTiny));
    }

    // ------------------- CTOR -------------------
    // DI: HttpClient (MauiProgram.cs'de BaseAddress = https://lessarc.com.tr) + token
    public AdminZamanRaporPage(HttpClient httpClient, string kullaniciToken)
    {
        InitializeComponent();

        // TR kültürü (ayın adı vb.)
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("tr-TR");

        _http = httpClient;
        token = kullaniciToken ?? string.Empty;

        // Authorization header yoksa tak
        var auth = _http.DefaultRequestHeaders.Authorization;
        if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
        {
            if (!string.IsNullOrWhiteSpace(token))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        BindingContext = this;

        // Başlangıç info
        if (lblSeciliProjeAdi != null)
            lblSeciliProjeAdi.Text = $"{DateTime.Today:dd MMMM yyyy}";

        // ölçek – ilk hesap + window resize
        SizeChanged += (_, __) => RecomputeScale();
        RecomputeScale();

        _ = YukleTakvimVerisiAsync();
    }

    // ------------------- DTO'lar -------------------
    public class TakvimGunuDto
    {
        public DateTime Tarih { get; set; }
        public List<TakvimProjeDto> Projeler { get; set; } = new();
        public bool ProjeVarMi => Projeler?.Any() == true;
    }

    public class TakvimProjeDto
    {
        public int ProjeId { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string RenkKodu { get; set; } = "#888888";
        public string SahibiAdSoyad { get; set; } = string.Empty; // üst bilgi için kişi adı
    }

    // ------------------- Nokta (proje) tıklama -------------------
    public Command<TakvimProjeDto> DotTiklandiCommand => new(proje =>
    {
        if (proje is null) return;

        // ProjeId ile günü bul
        var gun = tumProjeliGunler.FirstOrDefault(g =>
            g.Projeler?.Any(p => p.ProjeId == proje.ProjeId) == true);

        if (gun == null) return;

        var kisi = string.IsNullOrWhiteSpace(proje.SahibiAdSoyad) ? "—" : proje.SahibiAdSoyad;
        lblSeciliProjeAdi.Text = $"{gun.Tarih:dd MMMM yyyy} – {proje.Baslik} – {kisi}";
    });

    // ------------------- Veri yükleme -------------------
    private async Task YukleTakvimVerisiAsync()
    {
        try
        {
            // API: /api/Raporlar/ProjeTakvimi  ->  List<TakvimGunuDto>
            var json = await _http.GetStringAsync("/api/Raporlar/ProjeTakvimi");
            tumProjeliGunler = JsonConvert.DeserializeObject<List<TakvimGunuDto>>(json) ?? new();

            // Tüm günleri (boşlar dâhil) oluştur
            DateTime bugun = DateTime.Today;
            DateTime baslangic = bugun.AddDays(-100);
            DateTime bitis = bugun.AddDays(20);

            var tumGunler = new List<TakvimGunuDto>();
            for (var tarih = baslangic; tarih <= bitis; tarih = tarih.AddDays(1))
            {
                var guneAit = tumProjeliGunler.FirstOrDefault(g => g.Tarih.Date == tarih.Date);
                tumGunler.Add(new TakvimGunuDto
                {
                    Tarih = tarih,
                    Projeler = guneAit?.Projeler ?? new List<TakvimProjeDto>()
                });
            }

            TakvimGunleri.Clear();
            foreach (var g in tumGunler)
                TakvimGunleri.Add(g);

            // Bugünü ortaya kaydır
            await Task.Delay(100);
            var i = TakvimGunleri.ToList().FindIndex(g => g.Tarih.Date == DateTime.Today);
            if (i >= 0)
                collectionView.ScrollTo(i, position: ScrollToPosition.Center, animate: false);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Hata", $"Veri yüklenemedi: {ex.Message}", "Tamam");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

#if WINDOWS || MACCATALYST
        if (DeviceInfo.Current.Idiom == DeviceIdiom.Desktop)
        {
            frameTakvim.WidthRequest = 600;
            frameTakvim.HorizontalOptions = LayoutOptions.Center;
        }
#endif
    }

    // ------------------- Alt butonlar -------------------
    private async void OnDevamEdenClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new DevamEdenProjelerPage(_http, token));

    private async void OnTamamlanmisClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new TamamlanmisProjelerPage(_http, token));
}
