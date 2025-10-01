using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    public class ProjeAdimiDto
    {
        public int Id { get; set; }
        public string AdimBasligi { get; set; }
        public int TamamlanmaYuzdesi { get; set; }
        public int ProjeId { get; set; }
    }
}
