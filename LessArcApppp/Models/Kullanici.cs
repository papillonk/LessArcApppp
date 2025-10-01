using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    class Kullanici
    {
        public int Id { get; set; }
        public string Ad { get; set; }
        public string Soyad { get; set; }
        public string KullaniciAdi { get; set; }
        public string Eposta { get; set; }
        public string Sifre { get; set; }
        public string Role { get; set; }

        public string AdSoyad => $"{Ad ?? ""} {Soyad ?? ""}".Trim();

    }
}
