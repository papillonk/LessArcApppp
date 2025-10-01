using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LessArcApppp.Models
{
    using System.Text.Json.Serialization;

    public class LoginModel
    {
        [JsonPropertyName("kullaniciAdi")]
        public string KullaniciAdi { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }
    }

}
