using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace LessArcApppp
{
    public static class TemaYonetici
    {
        private const string TemaKey = "secilenTema"; // Kalıcı anahtar

        // Tema değiştirildiğinde hem uygula hem kaydet
        public static void TemaDegistir(string temaAdi)
        {
            var appResources = Application.Current.Resources;
            appResources.MergedDictionaries.Clear();

            if (temaAdi == "Koyu")
                appResources.MergedDictionaries.Add((ResourceDictionary)appResources["DarkTheme"]);
            else if (temaAdi == "Renkli")
                appResources.MergedDictionaries.Add((ResourceDictionary)appResources["ColorfulTheme"]);
            // Açık tema ise varsayılan

            Preferences.Set(TemaKey, temaAdi); // 🎯 Seçimi sakla
        }

        // Uygulama başlarken çalışacak
        public static void TemayiYukle()
        {
            string temaAdi = Preferences.Get(TemaKey, "Acik");
            TemaDegistir(temaAdi);
        }
    }
}
