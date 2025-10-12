using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using LessArcApppp.Models;

namespace LessArcApppp
{
    public partial class TamamlanmisProjelerPage : ContentPage
    {
        private readonly HttpClient _http;
        private readonly string _token;
        private const string CloudFallbackBaseUrl = "https://lessarc.com.tr";

        private ObservableCollection<ProjeViewModel> tumProjeler = new();
        private readonly ObservableCollection<ProjeViewModel> filtreliProjeler = new();
        private readonly UiScale _ui = new();

        public TamamlanmisProjelerPage(HttpClient httpClient, string kullaniciToken, string? baseUrlOverride = null)
        {
            InitializeComponent();

            _token = kullaniciToken ?? string.Empty;
            _http = httpClient;

            // BaseAddress
            if (_http.BaseAddress is null)
            {
                var effectiveBase = string.IsNullOrWhiteSpace(baseUrlOverride)
                    ? CloudFallbackBaseUrl
                    : baseUrlOverride.Trim();
                _http.BaseAddress = new Uri(effectiveBase, UriKind.Absolute);
            }

            // Token
            var auth = _http.DefaultRequestHeaders.Authorization;
            if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                if (!string.IsNullOrWhiteSpace(_token))
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            }

            // Ölçek BindingContext
            BindingContext = _ui;

            // ItemsSource atamaları (varsa)
            if (cvProjelerMobile != null)
                cvProjelerMobile.ItemsSource = filtreliProjeler;
            if (cvProjeler != null)
                cvProjeler.ItemsSource = filtreliProjeler;

            ApplyResponsiveSizing(isInitial: true);

            _ = ProjeleriYukleAsync();
        }

        // ===== VM =====
        private class ProjeViewModel
        {
            public int Id { get; set; }
            public string Baslik { get; set; } = "";
            public string? KullaniciAdSoyad { get; set; } = "-";
            public DateTime? BaslangicTarihi { get; set; }
            public DateTime? BitisTarihi { get; set; }
            public string BaslangicTarihiFormatted { get; set; } = "-";
            public string BitisTarihiFormatted { get; set; } = "-";
            public string SureFormatted { get; set; } = "-";
        }

        // ===== UI SCALE =====
        private sealed class UiScale : INotifyPropertyChanged
        {
            double fTitle = 26, fBody = 15, fSmall = 13;
            public double F_Title { get => fTitle; set => Set(ref fTitle, value); }
            public double F_Body { get => fBody; set => Set(ref fBody, value); }
            public double F_Small { get => fSmall; set => Set(ref fSmall, value); }

            double sL = 20, sM = 14, sS = 8;
            public double S_L { get => sL; set => Set(ref sL, value); }
            public double S_M { get => sM; set => Set(ref sM, value); }
            public double S_S { get => sS; set => Set(ref sS, value); }

            double cRadius = 20, cSmallRadius = 12;
            public double C_Radius { get => cRadius; set => Set(ref cRadius, value); }
            public double C_SmallRadius { get => cSmallRadius; set => Set(ref cSmallRadius, value); }

            Thickness pPage = new(20), pCardPad = new(16), pItemPad = new(16);
            public Thickness P_Page { get => pPage; set { pPage = value; OnPropertyChanged(); } }
            public Thickness P_CardPad { get => pCardPad; set { pCardPad = value; OnPropertyChanged(); } }
            public Thickness P_ItemPad { get => pItemPad; set { pItemPad = value; OnPropertyChanged(); } }

            double hButton = 44;
            public double H_Button { get => hButton; set => Set(ref hButton, value); }

            public event PropertyChangedEventHandler? PropertyChanged;
            void OnPropertyChanged([CallerMemberName] string? n = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

            bool Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
            {
                if (Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(n);
                return true;
            }
        }

        // ===== RESPONSIVE =====
        private void ApplyResponsiveSizing(bool isInitial = false)
        {
            double w = this.Width > 0
                ? this.Width
                : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;

            bool isPhone = DeviceInfo.Current.Idiom == DeviceIdiom.Phone || w < 800;

            if (isPhone)
            {
                _ui.F_Title = 22; _ui.F_Body = 15; _ui.F_Small = 13;
                _ui.S_L = 16; _ui.S_M = 12; _ui.S_S = 8;
                _ui.C_Radius = 18; _ui.C_SmallRadius = 12;
                _ui.P_Page = new Thickness(16);
                _ui.P_CardPad = new Thickness(14);
                _ui.P_ItemPad = new Thickness(14);
                _ui.H_Button = 44;

                if (mobileLayout != null) mobileLayout.IsVisible = true;
                if (desktopLayout != null) desktopLayout.IsVisible = false;
            }
            else
            {
                _ui.F_Title = 26; _ui.F_Body = 15; _ui.F_Small = 13;
                _ui.S_L = 20; _ui.S_M = 14; _ui.S_S = 10;
                _ui.C_Radius = 20; _ui.C_SmallRadius = 12;
                _ui.P_Page = new Thickness(20);
                _ui.P_CardPad = new Thickness(16);
                _ui.P_ItemPad = new Thickness(16);
                _ui.H_Button = 46;

                if (mobileLayout != null) mobileLayout.IsVisible = false;
                if (desktopLayout != null) desktopLayout.IsVisible = true;
            }

            if (isInitial) Filtrele();
        }

        // ===== LOAD DATA =====
        private async Task ProjeleriYukleAsync()
        {
            try
            {
                var projeler = await _http.GetFromJsonAsync<List<AdminProjeListDto>>(
                                   "/api/Projeler/tum-projeler-detayli")
                               ?? new List<AdminProjeListDto>();

                bool IsTamam(AdminProjeListDto p)
                {
                    var durum = p?.Durum?.Trim().ToLowerInvariant() ?? "";
                    bool durumTamam = durum.Contains("tamamlan");
                    bool bitisGecmis = p?.BitisTarihi.HasValue == true &&
                                       p.BitisTarihi!.Value.Date <= DateTime.Today;
                    return durumTamam || bitisGecmis;
                }

                var tamamlanmislar = projeler
                    .Where(IsTamam)
                    .Select(p => new ProjeViewModel
                    {
                        Id = p.Id,
                        Baslik = p.Baslik,
                        KullaniciAdSoyad = string.IsNullOrWhiteSpace(p.KullaniciAdSoyad) ? "-" : p.KullaniciAdSoyad,
                        BaslangicTarihi = p.BaslangicTarihi,
                        BitisTarihi = p.BitisTarihi,
                        BaslangicTarihiFormatted = "Başlangıç Tarihi: " + (p.BaslangicTarihi.HasValue
                            ? p.BaslangicTarihi.Value.ToString("dd MMMM yyyy", new CultureInfo("tr-TR"))
                            : "-"),
                        BitisTarihiFormatted = "Bitiş Tarihi: " + (p.BitisTarihi.HasValue
                            ? p.BitisTarihi.Value.ToString("dd MMMM yyyy", new CultureInfo("tr-TR"))
                            : "-"),
                        SureFormatted = "Süre: " + HesaplaSure(p.BaslangicTarihi, p.BitisTarihi)
                    })
                    .ToList();

                tumProjeler = new ObservableCollection<ProjeViewModel>(tamamlanmislar);

                filtreliProjeler.Clear();
                foreach (var item in tumProjeler)
                    filtreliProjeler.Add(item);

                // ---- Yıl seçenekleri ----
                int minYil = DateTime.Now.Year - 5;
                int maxYil = DateTime.Now.Year + 5;

                // (Opsiyonel) veri tabanından türet:
                // if (tumProjeler.Any())
                // {
                //     var dataMin = new[] { tumProjeler.Min(p => p.BaslangicTarihi?.Year ?? int.MaxValue),
                //                           tumProjeler.Min(p => p.BitisTarihi?.Year      ?? int.MaxValue) }.Min();
                //     var dataMax = new[] { tumProjeler.Max(p => p.BaslangicTarihi?.Year ?? int.MinValue),
                //                           tumProjeler.Max(p => p.BitisTarihi?.Year      ?? int.MinValue) }.Max();
                //     minYil = Math.Min(minYil, dataMin);
                //     maxYil = Math.Max(maxYil, dataMax);
                // }

                var yilSecenekleri = new List<string> { "Tüm Yıllar" };
                yilSecenekleri.AddRange(Enumerable.Range(minYil, maxYil - minYil + 1).Select(y => y.ToString()));

                if (pickerYil != null)
                {
                    pickerYil.ItemsSource = yilSecenekleri;
                    pickerYil.SelectedIndex = 0;
                }
                if (pickerYilMobile != null)
                {
                    pickerYilMobile.ItemsSource = yilSecenekleri;
                    pickerYilMobile.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", "Projeler yüklenirken hata oluştu:\n" + ex.Message, "Tamam");
            }
        }

        private static string HesaplaSure(DateTime? baslangic, DateTime? bitis)
        {
            if (!baslangic.HasValue || !bitis.HasValue) return "-";
            if (bitis.Value < baslangic.Value) return "-";

            var fark = bitis.Value - baslangic.Value;
            int toplamGun = (int)fark.TotalDays;

            int yil = toplamGun / 365;
            int ay = (toplamGun % 365) / 30;
            int gun = (toplamGun % 365) % 30;

            var parcalar = new List<string>();
            if (yil > 0) parcalar.Add($"{yil} yıl");
            if (ay > 0) parcalar.Add($"{ay} ay");
            parcalar.Add($"{gun} gün");

            return string.Join(", ", parcalar);
        }

        // ===== EVENTS =====
        private void entryAra_TextChanged(object sender, TextChangedEventArgs e) => Filtrele();
        private void entryAraMobile_TextChanged(object sender, TextChangedEventArgs e) => Filtrele();
        private void pickerYil_SelectedIndexChanged(object sender, EventArgs e) => Filtrele();
        private void pickerYilMobile_SelectedIndexChanged(object sender, EventArgs e) => Filtrele();

        private void Filtrele()
        {
            string kelime = (mobileLayout?.IsVisible == true ? entryAraMobile?.Text : entryAra?.Text) ?? "";
            kelime = kelime.ToLowerInvariant();

            string secilenYilStr = (mobileLayout?.IsVisible == true)
                ? pickerYilMobile?.SelectedItem?.ToString()
                : pickerYil?.SelectedItem?.ToString();

            var q = tumProjeler.Where(p =>
                (p.Baslik?.ToLowerInvariant().Contains(kelime) ?? false) ||
                ((p.KullaniciAdSoyad ?? "-").ToLowerInvariant().Contains(kelime)));

            if (!string.IsNullOrWhiteSpace(secilenYilStr) &&
                secilenYilStr != "Tüm Yıllar" &&
                int.TryParse(secilenYilStr, out int secilenYil))
            {
                q = q.Where(p =>
                    (p.BitisTarihi.HasValue && p.BitisTarihi.Value.Year == secilenYil) ||
                    (!p.BitisTarihi.HasValue && p.BaslangicTarihi.HasValue && p.BaslangicTarihi.Value.Year == secilenYil));
            }

            filtreliProjeler.Clear();
            foreach (var item in q)
                filtreliProjeler.Add(item);
        }

        private void ContentPage_SizeChanged(object sender, EventArgs e) => ApplyResponsiveSizing();
    }
}
