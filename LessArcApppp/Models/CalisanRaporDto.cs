using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    internal class CalisanRaporDto
    {
        public int KullaniciId { get; set; }
        public string AdSoyad { get; set; } = string.Empty;

        public int ToplamProjeSayisi { get; set; }
        public int DevamEdenProjeSayisi { get; set; }
        public int TamamlananProjeSayisi { get; set; }
        public double OrtalamaTamamlanmaYuzdesi { get; set; }
        public DateTime? SonAktifProjeTarihi { get; set; }

        public List<CalisanProjeDto> Projeler { get; set; } = new();
    }
}
