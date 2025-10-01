using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    class AdminProjeListItemVM
    {
        public int Id { get; set; }
        public string Baslik { get; set; } = "";
        public string Baslangic { get; set; } = "-";
        public string Bitis { get; set; } = "-";
        public string Durum { get; set; } = "-";
    }
}
