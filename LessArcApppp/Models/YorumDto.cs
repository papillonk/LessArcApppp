using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    class YorumDto
    {
        public int Id { get; set; }
        public int ProjeId { get; set; }
        public int KullaniciId { get; set; }
        public string KullaniciAdSoyad { get; set; } = string.Empty;
        public string YorumMetni { get; set; } = string.Empty;
        public DateTime OlusturmaTarihi { get; set; }

        // 🔽 UI için
        public bool IsEditable { get; set; }
        public bool IsDeletable { get; set; }
    }
}
