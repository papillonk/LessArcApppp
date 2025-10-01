using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using LessArcApppp.Models;

namespace LessArcApppp
{
    static class ProjeDurumRules
    {
        static readonly DateTime MinOk = DateTime.MinValue.AddYears(1);

        // Yüzde: uygulama modelindeki mevcut alan adı
        static double Percent(CalisanProjeDto p) =>
            p == null ? 0 : p.OrtalamaTamamlanmaYuzdesi;

        public static bool IsTamamlandi(CalisanProjeDto p)
            => p != null && p.BitisTarihi > MinOk && p.BitisTarihi >= p.BaslangicTarihi;

        public static bool IsBaslamadi(CalisanProjeDto p)
            => p != null && p.BitisTarihi <= MinOk && Percent(p) <= 0;

        public static bool IsDevam(CalisanProjeDto p)
            => p != null && p.BitisTarihi <= MinOk && Percent(p) > 0;

        public static double Yuzde(CalisanProjeDto p) => Percent(p);
    }

    public class ProjeIsDevamConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is CalisanProjeDto dto && ProjeDurumRules.IsDevam(dto);
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class ProjeIsBaslamadiConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is CalisanProjeDto dto && ProjeDurumRules.IsBaslamadi(dto);
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class ProjeIsTamamConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is CalisanProjeDto dto && ProjeDurumRules.IsTamamlandi(dto);
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class ProjeDurumTextConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is CalisanProjeDto dto)
            {
                if (ProjeDurumRules.IsTamamlandi(dto)) return "Tamamlandı";
                if (ProjeDurumRules.IsDevam(dto)) return "Devam Ediyor";
                if (ProjeDurumRules.IsBaslamadi(dto)) return "Başlamadı";
            }
            return "Bilinmiyor";
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class ProjeSureConverter : IValueConverter
    {
        private static readonly DateTime MinOk = DateTime.MinValue.AddYears(1);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not CalisanProjeDto dto)
                return string.Empty;

            var bas = dto.BaslangicTarihi;
            var bit = dto.BitisTarihi;

            // Tarihler yoksa veya geçersizse
            if (!bas.HasValue || !bit.HasValue)
                return string.Empty;
            if (bas.Value <= MinOk || bit.Value <= MinOk)
                return string.Empty;

            // Gün farkı (negatifse 0'a sabitle)
            int days = (int)(bit.Value.Date - bas.Value.Date).TotalDays;
            if (days < 0) days = 0;

            // NOT: "İnclusive" (her iki gün dahil) istersen bir satırla +1 yap:
            // days = Math.Max(0, days + 1);

            return $"Süre: {days} gün";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}