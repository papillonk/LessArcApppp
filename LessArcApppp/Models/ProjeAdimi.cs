using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace LessArcApppp.Models
{
    internal class ProjeAdimi
    {
        public int Id { get; set; }
        public string AdimBasligi { get; set; } = string.Empty;
        public int TamamlanmaYuzdesi { get; set; } = 0;
        public int ProjeId { get; set; }
        public int KullaniciId { get; set; }

        [JsonIgnore]
        public Proje Proje { get; set; }

        [JsonIgnore]
        public Kullanici? Kullanici { get; set; }

        // 🔹 Yeni: her yüzde güncellendiğinde set edilecek
        public DateTime GuncellemeTarihi { get; set; } = DateTime.UtcNow;
    }
    }
