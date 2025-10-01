public class TamamlanmisProjeDto
{
    public string Baslik { get; set; }
    public string KullaniciAdSoyad { get; set; }
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }

    public string BaslangicTarihiFormatted => $"Başlangıç Tarihi: {BaslangicTarihi:dd MMMM yyyy}";
    public string BitisTarihiFormatted => $"Bitiş Tarihi: {BitisTarihi:dd MMMM yyyy}";
    public string SureFormatted
    {
        get
        {
            var fark = BitisTarihi - BaslangicTarihi;
            return $"Süre: {fark.Days / 30} ay, {fark.Days % 30} gün";
        }
    }
}
