using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    class AdminProjeListDto
    {
        public int Id { get; set; }
        public string Baslik { get; set; } = "";
        public DateTime? BaslangicTarihi { get; set; }   // nullable
        public DateTime? BitisTarihi { get; set; }       // nullable
        public string? Durum { get; set; }

        // İstersen liste için hazır metinler:
        public string BaslangicText => BaslangicTarihi?.ToString("dd.MM.yyyy") ?? "-";
        public string BitisText => BitisTarihi?.ToString("dd.MM.yyyy") ?? "-";
        public string? KullaniciAdSoyad { get; set; }
        public int? KullaniciId { get; set; }
    }
}
