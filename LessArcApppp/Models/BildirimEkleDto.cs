using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    class BildirimEkleDto
    {
        public string Mesaj { get; set; }
        public DateTime GonderimTarihi { get; set; }
        public int AliciId { get; set; }  // admin kullanıcıya gidecek
        public List<int> AdminIdListesi { get; set; }

    }
}
