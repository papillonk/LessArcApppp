using System;

namespace LessArcApppp.Models
{
    public class ProjeViewModel
    {
        public int Id { get; set; }
        public string Baslik { get; set; }
        public string KullaniciAdSoyad { get; set; }
        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }
        public string Aciklama { get; set; } // 💜 Eklendi


        public string BaslangicTarihiFormatted { get; set; }
        public string BitisTarihiFormatted { get; set; }
        public string SureFormatted { get; set; }
    }
}
