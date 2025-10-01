using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    class Bildirim
    {
        public int Id { get; set; }
        public string Mesaj { get; set; }
        public DateTime GonderimTarihi { get; set; }
        public int KullaniciId { get; set; }
        public Kullanici Kullanici { get; set; }

        
    }
}

