using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    internal class Proje
    {

        public int Id { get; set; }
        public string Baslik { get; set; }
        public string? Aciklama { get; set; }

        // ⬇️ bunlar mutlaka nullable olsun
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }

        public int? KullaniciId { get; set; }
        public string? RenkKodu { get; set; }
    }
}
