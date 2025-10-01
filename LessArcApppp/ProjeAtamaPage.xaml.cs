// ProjeAtamaPage.cs — Yalnız Çalışanlar + Güncelleme Sonrası Kişi Kaybolmasını Engelle + EKRAN ÖLÇEKLENDİRME
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices; // DeviceInfo, Idiom

namespace LessArcApppp
{
    public partial class ProjeAtamaPage : ContentPage, INotifyPropertyChanged
    {
        // INotifyPropertyChanged (ana sayfa için)
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ========== ViewModel ==========

        public class Kisi : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string AdSoyad { get; set; } = "";
            bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public class KullaniciItem
        {
            public int Id { get; set; }
            public string AdSoyad { get; set; } = "";
        }

        public class ProjeKart : INotifyPropertyChanged
        {
            public string ProjeAdi { get; set; } = "";
            public string? RenkKodu { get; set; }
            public ObservableCollection<KullaniciItem> Kisiler { get; } = new();

            public event PropertyChangedEventHandler? PropertyChanged;
            void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

            public void AddKisi(int id, string ad)
            {
                if (id <= 0 || string.IsNullOrWhiteSpace(ad)) return;
                if (Kisiler.Any(k => k.Id == id)) return;
                Kisiler.Add(new KullaniciItem { Id = id, AdSoyad = ad });
                Raise(nameof(Kisiler));
            }
        }

        // ========== DTO ==========

        public record KullaniciListeDto(int Id, string? Ad, string? Soyad, string? AdSoyad, string? Rol);

        public class ProjeTopluAtamaDto
        {
            public string Baslik { get; set; } = string.Empty;
            public List<int> KullaniciIdler { get; set; } = new();
            public string? RenkKodu { get; set; }
        }

        public record ProjeAtamaCevapDto(int Id, string Baslik, int KullaniciId, string? RenkKodu);

        public class ProjeDetayDto
        {
            public int Id { get; set; }
            public string Baslik { get; set; } = "";
            public int? KullaniciId { get; set; }
            public string? KullaniciAdSoyad { get; set; }
            public string? RenkKodu { get; set; }
        }

        public class ProjeRaw
        {
            public int Id { get; set; }
            public string Baslik { get; set; } = "";
            public int? KullaniciId { get; set; }
            public string? RenkKodu { get; set; }
        }

        // ========== Collections ==========

        public ObservableCollection<Kisi> TumKisiler { get; } = new();
        public ObservableCollection<Kisi> FiltreliKisiler { get; } = new();
        public ObservableCollection<Kisi> SeciliKisiler { get; } = new();
        public ObservableCollection<ProjeKart> Kartlar { get; } = new();

        string _secilenKisilerMetni = "";
        public string SecilenKisilerMetni
        {
            get => _secilenKisilerMetni;
            set { _secilenKisilerMetni = value; OnPropertyChanged(nameof(SecilenKisilerMetni)); }
        }

        // ========== HTTP ==========

        private readonly HttpClient _http;
        private readonly string _token;
        private const string CloudFallbackBaseUrl = "https://lessarc.com.tr";

        private static readonly JsonSerializerOptions _jsonRead = new() { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions _jsonSend = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private ProjeKart? _popupTargetKart = null;

        // =========================================================
        // === Responsive ölçek ===
        // =========================================================

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

        // İsteğe bağlı: mobil popup genişliği (XAML'de sabit ama koddan güncelliyoruz)
        public double W_PopupMobile { get; private set; } = 360;

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

            S_L = 16 * scale;
            S_M = 10 * scale;
            S_S = 8 * scale;

            // popup
            W_PopupDesktop = Math.Clamp(500 * scale, 420, 820);
            W_PopupMobile = Math.Clamp(360 * scale, 300, 480);

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

            // Mevcut named kontrolleri de güncelle (binding'e dokunmadan)
            try
            {
                var eProj = GetView<Entry>("entryProjeAdi");
                if (eProj != null) { eProj.FontSize = F_Body; eProj.HeightRequest = Math.Max(eProj.HeightRequest, H_Entry); }

                var eProjM = GetView<Entry>("entryProjeAdiMobile");
                if (eProjM != null) { eProjM.FontSize = F_Body; eProjM.HeightRequest = Math.Max(eProjM.HeightRequest, H_Entry); }

                var eSel = GetView<Entry>("entrySecilenKisiler");
                if (eSel != null) { eSel.FontSize = F_Body; eSel.HeightRequest = Math.Max(eSel.HeightRequest, H_Entry); }

                var eSelM = GetView<Entry>("entrySecilenKisilerMobile");
                if (eSelM != null) { eSelM.FontSize = F_Body; eSelM.HeightRequest = Math.Max(eSelM.HeightRequest, H_Entry); }

                var btnKaydet = GetView<Button>("btnKaydet");
                if (btnKaydet != null) { btnKaydet.FontSize = F_Body; btnKaydet.MinimumHeightRequest = H_Button; }

                var btnKaydetM = GetView<Button>("btnKaydetMobile");
                if (btnKaydetM != null) { btnKaydetM.FontSize = F_Body; btnKaydetM.MinimumHeightRequest = H_Button; }

                var framePopup = GetView<Frame>("frameKisiPopup");
                if (framePopup != null) framePopup.WidthRequest = W_PopupDesktop;

                var framePopupM = GetView<Frame>("frameKisiPopupMobile");
                if (framePopupM != null) framePopupM.WidthRequest = W_PopupMobile;

                var entryAra = GetView<Entry>("entryAra");
                if (entryAra != null) { entryAra.FontSize = F_Body; entryAra.HeightRequest = Math.Max(entryAra.HeightRequest, H_Entry); }

                var entryAraM = GetView<Entry>("entryAraMobile");
                if (entryAraM != null) { entryAraM.FontSize = F_Body; entryAraM.HeightRequest = Math.Max(entryAraM.HeightRequest, H_Entry); }
            }
            catch { /* yoksa sessiz geç */ }
        }

        // =========================================================

        public ProjeAtamaPage(HttpClient httpClient, string kullaniciToken, string? baseUrlOverride = null)
        {
            InitializeComponent();
            BindingContext = this;

            _http = httpClient;
            _token = kullaniciToken ?? string.Empty;

            if (_http.BaseAddress is null)
            {
                var effective = string.IsNullOrWhiteSpace(baseUrlOverride) ? CloudFallbackBaseUrl : baseUrlOverride.Trim();
                _http.BaseAddress = new Uri(effective, UriKind.Absolute);
            }

            var auth = _http.DefaultRequestHeaders.Authorization;
            if (auth is null || auth.Scheme != "Bearer" || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                if (!string.IsNullOrWhiteSpace(_token))
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            }

            // İlk ölçek
            RecomputeScale();
            // İstersen ekstra: SizeChanged += (_, __) => RecomputeScale();

            _ = InitLoad();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // Pencere ölçüleri hazır olduğunda tekrar ölçekle
            RecomputeScale();

            // Güvenli yenile – başarısızsa eldeki UI'yı bozma
            try { await KisileriYukle(); } catch { /* ignore */ }
            try { await KartlariYukle(); } catch { /* ignore */ }
        }

        // ========== Helpers ==========

        private T? GetView<T>(params string[] names) where T : class
        {
            foreach (var n in names)
            {
                if (!string.IsNullOrWhiteSpace(n) && this.FindByName(n) is T v)
                    return v;
            }
            return null;
        }

        private string ReadProjectName()
        {
            var d = GetView<Entry>("entryProjeAdi")?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(d)) return d!;
            return GetView<Entry>("entryProjeAdiMobile")?.Text?.Trim() ?? "";
        }

        private void ClearProjectName()
        {
            var d = GetView<Entry>("entryProjeAdi"); if (d != null) d.Text = "";
            var m = GetView<Entry>("entryProjeAdiMobile"); if (m != null) m.Text = "";
        }

        private Grid? CurrentPopup() => GetView<Grid>("popupOverlayMobile", "popupOverlay");
        private Entry? CurrentSearchEntry() => GetView<Entry>("entryAraMobile", "entryAra");

        private static string key(string s) =>
            (s ?? string.Empty).Trim().ToLowerInvariant()
            .Replace("ı", "i").Replace("İ", "i").Replace("ş", "s").Replace("Ş", "s")
            .Replace("ğ", "g").Replace("Ğ", "g").Replace("ü", "u").Replace("Ü", "u")
            .Replace("ö", "o").Replace("Ö", "o").Replace("ç", "c").Replace("Ç", "c");

        // ========== İlk Yük ==========

        private async Task InitLoad()
        {
            await KisileriYukle();
            await KartlariYukle();
        }

        // ---- Sadece çalışanları getir (API değişmeden) ----
        private async Task KisileriYukle()
        {
            try
            {
                var paths = new[]
                {
                    "api/Kullanicilar/calisanlar",
                    "api/Kullanicilar?role=calisan",
                    "api/Kullanici/calisanlar",
                    "api/Kullanici?role=calisan",
                    "api/Kullanicilar",
                    "api/Kullanici",
                    "api/Users",
                    "api/User"
                };

                HttpResponseMessage? resp = null;
                string? usedPath = null;

                foreach (var p in paths)
                {
                    usedPath = p;
                    try
                    {
                        resp = await _http.GetAsync(p);
                        if (resp.IsSuccessStatusCode) break;
                        if (resp.StatusCode != System.Net.HttpStatusCode.NotFound) break;
                    }
                    catch { resp = null; }
                }

                // Swagger üzerinden olası path keşfi
                if (resp is null || !resp.IsSuccessStatusCode)
                {
                    foreach (var sw in new[] { "swagger/v1/swagger.json", "swagger/v2/swagger.json", "swagger/v3/swagger.json" })
                    {
                        try
                        {
                            var swr = await _http.GetAsync(sw);
                            if (!swr.IsSuccessStatusCode) continue;

                            var s = await swr.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(s);
                            if (!doc.RootElement.TryGetProperty("paths", out var pathsNode)) continue;

                            foreach (var po in pathsNode.EnumerateObject())
                            {
                                var name = po.Name.ToLowerInvariant();
                                if (!(name.Contains("kullanici") || name.Contains("user"))) continue;
                                if (!po.Value.TryGetProperty("get", out _)) continue;

                                usedPath = po.Name.TrimStart('/');
                                resp = await _http.GetAsync(usedPath);
                                if (resp.IsSuccessStatusCode) goto GOT;
                            }
                        }
                        catch { }
                    }
                }

                // Projeler/detayli'den kişi üretme (en son çare)
                if (resp is null || !resp.IsSuccessStatusCode)
                {
                    try
                    {
                        var pr = await _http.GetAsync("api/Projeler/detayli");
                        if (pr.IsSuccessStatusCode)
                        {
                            var txt = await pr.Content.ReadAsStringAsync();
                            var prList = JsonSerializer.Deserialize<List<ProjeDetayDto>>(txt, _jsonRead) ?? new();

                            var people = prList
                                .Where(p => p.KullaniciId.HasValue)
                                .GroupBy(p => new { p.KullaniciId, p.KullaniciAdSoyad })
                                .Select(g => new KullaniciListeDto(
                                    g.Key.KullaniciId ?? 0,
                                    null, null,
                                    string.IsNullOrWhiteSpace(g.Key.KullaniciAdSoyad) ? $"Kullanıcı #{g.Key.KullaniciId}" : g.Key.KullaniciAdSoyad,
                                    "calisan"))
                                .ToList();

                            FillPeople(people);
                            return;
                        }
                    }
                    catch { }
                }

                if (resp is null || !resp.IsSuccessStatusCode)
                    throw new Exception($"Kullanıcı listesi alınamadı. Son denenen: {usedPath ?? "-"} (Status: {resp?.StatusCode})");

                GOT:
                var raw = await resp!.Content.ReadAsStringAsync();

                // Rol alanını esnek şekilde çek
                var tmp = new List<KullaniciListeDto>();
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            int id = el.TryGetProperty("id", out var pid) ? pid.GetInt32() : 0;
                            string ad = el.TryGetProperty("ad", out var pad) ? pad.GetString() ?? "" : "";
                            string soyad = el.TryGetProperty("soyad", out var psoy) ? psoy.GetString() ?? "" : "";
                            string adsoyad =
                                el.TryGetProperty("adSoyad", out var pfull) ? pfull.GetString() ?? "" :
                                $"{ad} {soyad}".Trim();

                            // Olası rol alanları
                            string rol =
                                el.TryGetProperty("rol", out var pr1) ? pr1.GetString() ?? "" :
                                el.TryGetProperty("role", out var pr2) ? pr2.GetString() ?? "" :
                                el.TryGetProperty("userRole", out var pr3) ? pr3.GetString() ?? "" :
                                el.TryGetProperty("roli", out var pr4) ? pr4.GetString() ?? "" :
                                "";

                            tmp.Add(new KullaniciListeDto(id, ad, soyad, adsoyad, rol));
                        }
                    }
                    else
                    {
                        tmp = JsonSerializer.Deserialize<List<KullaniciListeDto>>(raw, _jsonRead) ?? new();
                    }
                }
                catch
                {
                    tmp = JsonSerializer.Deserialize<List<KullaniciListeDto>>(raw, _jsonRead) ?? new();
                }

                // SADECE çalışanlar:
                IEnumerable<KullaniciListeDto> onlyWorkers = tmp;

                if (tmp.Any(x => !string.IsNullOrWhiteSpace(x.Rol)))
                    onlyWorkers = tmp.Where(x => key(x.Rol ?? "") is var k && (k.Contains("calisan") || k.Contains("employee")));
                else
                    // Rol hiç yoksa: "admin" geçen adları ayıkla
                    onlyWorkers = tmp.Where(x => !key(x.AdSoyad ?? "").Contains("admin"));

                FillPeople(onlyWorkers);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", "Kişi listesi alınamadı:\n" + ex.Message, "Tamam");
            }
        }

        private void FillPeople(IEnumerable<KullaniciListeDto> liste)
        {
            TumKisiler.Clear();
            foreach (var k in liste)
            {
                var ad = !string.IsNullOrWhiteSpace(k.AdSoyad) ? k.AdSoyad : $"{k.Ad} {k.Soyad}".Trim();
                if (!string.IsNullOrWhiteSpace(ad))
                    TumKisiler.Add(new Kisi { Id = k.Id, AdSoyad = ad });
            }
            FiltreYenile("");
        }

        private async Task KartlariYukle()
        {
            try
            {
                var yeniKartlar = new List<ProjeKart>();

                async Task fromDetayliAsync()
                {
                    var resp = await _http.GetAsync("api/Projeler/detayli");
                    if (!resp.IsSuccessStatusCode) return;

                    var text = await resp.Content.ReadAsStringAsync();
                    var list = JsonSerializer.Deserialize<List<ProjeDetayDto>>(text, _jsonRead) ?? new();

                    foreach (var grp in list.GroupBy(p => p.Baslik))
                    {
                        var kart = new ProjeKart
                        {
                            ProjeAdi = grp.Key,
                            RenkKodu = grp.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.RenkKodu))?.RenkKodu
                        };

                        foreach (var p in grp)
                        {
                            int id = p.KullaniciId ?? 0;
                            var ad = p.KullaniciAdSoyad
                                     ?? TumKisiler.FirstOrDefault(t => t.Id == p.KullaniciId)?.AdSoyad
                                     ?? (id > 0 ? $"Kullanıcı #{id}" : "");
                            if (id > 0 && !string.IsNullOrWhiteSpace(ad))
                                kart.AddKisi(id, ad);
                        }
                        yeniKartlar.Add(kart);
                    }
                }

                async Task fromRawAsync()
                {
                    var resp2 = await _http.GetAsync("api/Projeler");
                    if (!resp2.IsSuccessStatusCode) return;

                    var text2 = await resp2.Content.ReadAsStringAsync();
                    var raw = JsonSerializer.Deserialize<List<ProjeRaw>>(text2, _jsonRead) ?? new();

                    foreach (var grp in raw.GroupBy(p => p.Baslik))
                    {
                        var kart = new ProjeKart
                        {
                            ProjeAdi = grp.Key,
                            RenkKodu = grp.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.RenkKodu))?.RenkKodu
                        };

                        foreach (var p in grp)
                        {
                            int id = p.KullaniciId ?? 0;
                            var ad = TumKisiler.FirstOrDefault(t => t.Id == p.KullaniciId)?.AdSoyad
                                     ?? (id > 0 ? $"Kullanıcı #{id}" : "");
                            if (id > 0 && !string.IsNullOrWhiteSpace(ad))
                                kart.AddKisi(id, ad);
                        }
                        yeniKartlar.Add(kart);
                    }
                }

                await fromDetayliAsync();
                if (yeniKartlar.Count == 0) await fromRawAsync();

                if (yeniKartlar.Count == 0)
                {
                    // hiçbir şey gelemediyse UI'yı hiç elleme
                    return;
                }

                var mevcutByName = Kartlar.ToDictionary(k => k.ProjeAdi, StringComparer.OrdinalIgnoreCase);
                foreach (var yeni in yeniKartlar)
                {
                    if (mevcutByName.TryGetValue(yeni.ProjeAdi, out var eski))
                    {
                        if (!string.IsNullOrWhiteSpace(yeni.RenkKodu))
                            eski.RenkKodu = yeni.RenkKodu;

                        if (yeni.Kisiler.Count == 0)
                            continue;

                        foreach (var k in yeni.Kisiler)
                            if (!eski.Kisiler.Any(x => x.Id == k.Id))
                                eski.Kisiler.Add(k);
                    }
                    else
                    {
                        Kartlar.Add(yeni);
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", "Projeler yüklenemedi:\n" + ex.Message, "Tamam");
            }
        }

        // ========== Üst Bölüm ==========

        private void btnKisiSec_Clicked(object sender, EventArgs e)
        {
            _popupTargetKart = null;
            foreach (var k in TumKisiler)
                k.IsSelected = SeciliKisiler.Any(s => s.Id == k.Id);

            var pop = CurrentPopup();
            if (pop != null) pop.IsVisible = true;
        }

        private async void btnKaydet_Clicked(object sender, EventArgs e)
        {
            var projeAdi = (ReadProjectName() ?? "").Trim();
            if (string.IsNullOrWhiteSpace(projeAdi))
            { await DisplayAlert("Uyarı", "Proje adı boş olamaz.", "Tamam"); return; }

            if (SeciliKisiler.Count == 0)
            { await DisplayAlert("Uyarı", "En az bir kişi seçin.", "Tamam"); return; }

            if (Kartlar.Any(k => string.Equals((k.ProjeAdi ?? "").Trim(), projeAdi, StringComparison.OrdinalIgnoreCase)))
            { await DisplayAlert("Uyarı", "Bu isimde bir proje bulunmaktadır.", "Tamam"); return; }

            var dto = new ProjeTopluAtamaDto
            {
                Baslik = projeAdi,
                KullaniciIdler = SeciliKisiler.Select(k => k.Id).ToList(),
                RenkKodu = null
            };

            try
            {
                var payload = JsonSerializer.Serialize(dto, _jsonSend);
                var request = new StringContent(payload, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync("api/Projeler/toplu-atama", request);
                var respText = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"API {resp.StatusCode}: {respText}");

                var eklenenler = JsonSerializer.Deserialize<List<ProjeAtamaCevapDto>>(respText, _jsonRead) ?? new();
                var renk = eklenenler.FirstOrDefault()?.RenkKodu;

                var mevcutKart = Kartlar.FirstOrDefault(k =>
                    string.Equals(k.ProjeAdi, projeAdi, StringComparison.OrdinalIgnoreCase));

                if (mevcutKart == null)
                {
                    var kart = new ProjeKart { ProjeAdi = projeAdi, RenkKodu = renk };
                    foreach (var k in SeciliKisiler) kart.AddKisi(k.Id, k.AdSoyad);
                    Kartlar.Add(kart);
                }
                else
                {
                    foreach (var k in SeciliKisiler) mevcutKart.AddKisi(k.Id, k.AdSoyad);
                }

                ClearProjectName();
                SeciliKisiler.Clear();
                GuncelleSecilenKisilerMetni();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", "Kaydetme sırasında hata oluştu:\n" + ex.Message, "Tamam");
            }
        }

        // ========== Kart Silme ==========

        private async void KartSil_Clicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is not ProjeKart kart) return;

            bool onay = await DisplayAlert("Silinsin mi?",
                $"“{kart.ProjeAdi}” projesi ve tüm atamaları silinecek.", "Sil", "Vazgeç");
            if (!onay) return;

            try
            {
                var ok = await Sil_ProjeyiVeAtamalarini(kart.ProjeAdi);
                if (!ok) throw new Exception("Sunucu silme uç noktaları cevap vermedi.");
                Kartlar.Remove(kart);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", "Silme başarısız:\n" + ex.Message, "Tamam");
            }
        }

        private async Task<bool> Sil_ProjeyiVeAtamalarini(string baslik)
        {
            // 1) Bu başlığa ait kayıt ID'lerini topla (detaylı)
            var ids = new List<int>();
            try
            {
                var det = await _http.GetAsync("api/Projeler/detayli");
                if (det.IsSuccessStatusCode)
                {
                    var txt = await det.Content.ReadAsStringAsync();
                    var list = JsonSerializer.Deserialize<List<ProjeDetayDto>>(txt, _jsonRead) ?? new();
                    ids = list.Where(p => string.Equals(p.Baslik, baslik, StringComparison.OrdinalIgnoreCase))
                              .Select(p => p.Id).Distinct().ToList();
                }
            }
            catch { /* yut */ }

            // 2) Hâlâ boşsa raw endpoint’i dene
            if (ids.Count == 0)
            {
                try
                {
                    var rawResp = await _http.GetAsync("api/Projeler");
                    if (rawResp.IsSuccessStatusCode)
                    {
                        var txt = await rawResp.Content.ReadAsStringAsync();
                        var list = JsonSerializer.Deserialize<List<ProjeRaw>>(txt, _jsonRead) ?? new();
                        ids = list.Where(p => string.Equals(p.Baslik, baslik, StringComparison.OrdinalIgnoreCase))
                                  .Select(p => p.Id).Distinct().ToList();
                    }
                }
                catch { /* yut */ }
            }

            // 3) ID bulduysak tek tek sil
            if (ids.Count > 0)
            {
                foreach (var id in ids)
                {
                    var del = await _http.DeleteAsync($"api/Projeler/{id}");
                    if (!del.IsSuccessStatusCode)
                    {
                        // biri bile patlarsa başarısız say
                        return false;
                    }
                }
                return true; // hepsi silindi
            }

            // 4) Buraya geldiysek sunucuda silinecek bir kayıt yok demektir.
            //    (Tüm atamalar önceden kaldırılmış olabilir veya proje hiç kaydolmamış olabilir.)
            //    Başlıkla silen alternatif uç noktaları "best effort" dene ama sonuçtan bağımsız başarı kabul et.
            try
            {
                var title = Uri.EscapeDataString(baslik);
                var r1 = await _http.DeleteAsync($"api/Projeler?baslik={title}");
                if (r1.IsSuccessStatusCode) return true;
            }
            catch { /* yut */ }

            try
            {
                var payload = JsonSerializer.Serialize(new { Baslik = baslik }, _jsonSend);
                var req = new StringContent(payload, Encoding.UTF8, "application/json");
                var r2 = await _http.PostAsync("api/Projeler/sil-baslik", req);
                if (r2.IsSuccessStatusCode) return true;
            }
            catch { /* yut */ }

            try
            {
                var payload = JsonSerializer.Serialize(new { Baslik = baslik }, _jsonSend);
                var req = new StringContent(payload, Encoding.UTF8, "application/json");
                var r3 = await _http.PostAsync("api/Projeler/sil-toplu", req);
                if (r3.IsSuccessStatusCode) return true;
            }
            catch { /* yut */ }

            // 5) ID yok + uç noktalar da yoksa "no-op" kabul: UI’dan kaldırabiliriz.
            return true;
        }

        // ========== (+) Kişi Ekle ==========

        private void KisiEkle_Clicked(object sender, EventArgs e)
        {
            if ((sender as ImageButton)?.CommandParameter is ProjeKart kart)
            {
                _popupTargetKart = kart;
                foreach (var k in TumKisiler) k.IsSelected = false;
                var pop = CurrentPopup(); if (pop != null) pop.IsVisible = true;
            }
        }

        // ========== Popup ==========

        private void PopupKapat_Clicked(object sender, EventArgs e)
        {
            var pop = CurrentPopup(); if (pop != null) pop.IsVisible = false;
            _popupTargetKart = null;
        }

        private async void PopupTamam_Clicked(object sender, EventArgs e)
        {
            var secilenler = TumKisiler.Where(t => t.IsSelected).ToList();

            if (_popupTargetKart != null)
            {
                await Ekle_KartaKisilerAsync(_popupTargetKart, secilenler);
                _popupTargetKart = null;
                var pop1 = CurrentPopup(); if (pop1 != null) pop1.IsVisible = false;
                return;
            }

            SeciliKisiler.Clear();
            foreach (var k in secilenler) SeciliKisiler.Add(k);
            GuncelleSecilenKisilerMetni();

            var pop = CurrentPopup(); if (pop != null) pop.IsVisible = false;
        }

        private void KisiCheck_Changed(object? sender, CheckedChangedEventArgs e)
        {
            var simdiki = TumKisiler.Where(t => t.IsSelected).Select(t => t.AdSoyad);
            SecilenKisilerMetni = string.Join(", ", simdiki);
        }

        private void KisiSatir_Tapped(object? sender, TappedEventArgs e)
        {
            if (sender is Label lbl && lbl.BindingContext is Kisi kisi)
            {
                kisi.IsSelected = !kisi.IsSelected;
                KisiCheck_Changed(null, new CheckedChangedEventArgs(kisi.IsSelected));
            }
        }

        private void Overlay_Tapped(object sender, TappedEventArgs e)
        {
            var pop = CurrentPopup(); if (pop != null) pop.IsVisible = false;
            _popupTargetKart = null;
        }

        // ========== Arama ==========

        private void entryAra_TextChanged(object sender, TextChangedEventArgs e) => FiltreYenile(e.NewTextValue);

        private void btnAraTemizle_Clicked(object sender, EventArgs e)
        {
            var ara = CurrentSearchEntry(); if (ara != null) ara.Text = "";
            FiltreYenile("");
        }

        private void FiltreYenile(string? ara)
        {
            var q = (ara ?? "").Trim().ToLowerInvariant();
            FiltreliKisiler.Clear();
            IEnumerable<Kisi> src = TumKisiler;
            if (!string.IsNullOrWhiteSpace(q))
                src = src.Where(k => k.AdSoyad.ToLowerInvariant().contains(q: q));

            foreach (var k in src) FiltreliKisiler.Add(k);
        }

        private void GuncelleSecilenKisilerMetni()
            => SecilenKisilerMetni = string.Join(", ", SeciliKisiler.Select(k => k.AdSoyad));

        // ========== Desktop grid span + Ölçek ==========

        private void ContentPage_SizeChanged(object sender, EventArgs e)
        {
            // Ölçekleri güncelle
            RecomputeScale();

            // Kart ızgara sütun sayısı (desktop)
            var cv = GetView<CollectionView>("cvKartlar");
            if (cv?.ItemsLayout is GridItemsLayout grid)
            {
                var w = Width;
                grid.Span = w switch { < 600 => 1, < 980 => 2, < 1320 => 3, _ => 4 };
            }
        }

        // ========== Karttan kişi sil ==========

        private async void KisiSil_Clicked(object sender, EventArgs e)
        {
            var btn = sender as Button; if (btn == null) return;
            var kisiItem = btn.CommandParameter as KullaniciItem;
            var projeAdi = btn.ClassId;
            if (kisiItem == null || string.IsNullOrWhiteSpace(projeAdi)) return;

            var kart = Kartlar.FirstOrDefault(k => string.Equals(k.ProjeAdi, projeAdi, StringComparison.OrdinalIgnoreCase));
            if (kart == null) return;

            bool onay = await DisplayAlert("Kişiyi kaldır?", $"“{kisiItem.AdSoyad}” projeden çıkarılsın mı?", "Evet", "Vazgeç");
            if (!onay) return;

            try
            {
                await Sil_KarttanKisiAsync(kart.ProjeAdi, kisiItem.Id);
                var hedef = kart.Kisiler.FirstOrDefault(x => x.Id == kisiItem.Id);
                if (hedef != null) kart.Kisiler.Remove(hedef);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", "Kişi kaldırılırken sorun oluştu:\n" + ex.Message, "Tamam");
            }
        }

        private async Task Ekle_KartaKisilerAsync(ProjeKart kart, List<Kisi> secilenler)
        {
            if (secilenler.Count == 0) return;

            var yeniList = secilenler.Where(k => !kart.Kisiler.Any(x => x.Id == k.Id)).ToList();
            if (yeniList.Count == 0)
            {
                await DisplayAlert("Bilgi", "Yeni eklenecek kişi bulunamadı.", "Tamam");
                return;
            }

            var dto = new ProjeTopluAtamaDto
            {
                Baslik = kart.ProjeAdi,
                KullaniciIdler = yeniList.Select(k => k.Id).ToList(),
                RenkKodu = kart.RenkKodu
            };

            var payload = JsonSerializer.Serialize(dto, _jsonSend);
            var req = new StringContent(payload, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync("api/Projeler/toplu-atama", req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"API {resp.StatusCode}: {body}");

            foreach (var k in yeniList) kart.AddKisi(k.Id, k.AdSoyad);
        }

        private async Task Sil_KarttanKisiAsync(string projeAdi, int kisiId)
        {
            if (kisiId <= 0) throw new ArgumentException("Geçersiz kisiId.");

            var detResp = await _http.GetAsync("api/Projeler/detayli");
            if (!detResp.IsSuccessStatusCode) throw new Exception("Detaylar alınamadı.");

            var detText = await detResp.Content.ReadAsStringAsync();
            var detaylar = JsonSerializer.Deserialize<List<ProjeDetayDto>>(detText, _jsonRead) ?? new();

            var hedefKayitlar = detaylar
                .Where(p => p.Baslik.Equals(projeAdi, StringComparison.OrdinalIgnoreCase) && p.KullaniciId == kisiId)
                .Select(p => p.Id).Distinct().ToList();

            if (hedefKayitlar.Count == 0) throw new Exception("Silinecek kayıt bulunamadı.");

            foreach (var id in hedefKayitlar)
            {
                var del = await _http.DeleteAsync($"api/Projeler/{id}");
                if (!del.IsSuccessStatusCode)
                {
                    var txt = await del.Content.ReadAsStringAsync();
                    throw new Exception($"Silme başarısız (Id={id}): {txt}");
                }
            }
        }
    }

    // Küçük extension — Contains kısaltması (case handled yukarıda)
    internal static class StringExt
    {
        public static bool contains(this string source, string q)
            => (source ?? string.Empty).Contains(q ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
