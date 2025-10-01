using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    internal class ZamanBazliPlanRaporDto
    {
        public DateTime Tarih { get; set; }
        public int ToplamPlanSayisi { get; set; }
        public int TamamlananPlanSayisi { get; set; }
        public int DevamEdenPlanSayisi { get; set; }
        public int BaslamayanPlanSayisi { get; set; }
    }
}
