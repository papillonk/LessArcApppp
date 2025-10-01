using Newtonsoft.Json;
using System;

namespace LessArcApppp.Models
{
    public class CalisanProjeDto
    {
        public int ProjeId { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }

        // API "ortalamaAdimTamamlanmaYuzdesi" gönderiyor (camelCase)
        [JsonProperty("ortalamaAdimTamamlanmaYuzdesi")]
        public double OrtalamaAdimTamamlanmaYuzdesi { get; set; }

        // UI yardımcı
        public string BaslangicText => BaslangicTarihi?.ToString("dd.MM.yyyy") ?? "-";
        public string BitisText => BitisTarihi?.ToString("dd.MM.yyyy") ?? "-";
        [JsonIgnore]
        public double OrtalamaTamamlanmaYuzdesi => OrtalamaAdimTamamlanmaYuzdesi;
    }
}
