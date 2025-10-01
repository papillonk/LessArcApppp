using System.Text.Json.Serialization;

namespace LessArcApppp.Models
{
    public class TokenResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("rol")]
        public string Rol { get; set; }

        [JsonPropertyName("ad")]
        public string Ad { get; set; }

        [JsonPropertyName("soyad")]
        public string Soyad { get; set; }

        [JsonPropertyName("eposta")]
        public string Eposta { get; set; }

        [JsonPropertyName("kullaniciAdi")]
        public string KullaniciAdi { get; set; }
    }
}
