using System;
using System.ComponentModel;

namespace LessArcApppp.Models
{
    public class Plan : INotifyPropertyChanged
    {
        private string baslik = string.Empty;
        private string? aciklama;
        private DateTime tarih;
        private string icerik = string.Empty;

        public int Id { get; set; }
        public int KullaniciId { get; set; }

        public string Baslik
        {
            get => baslik;
            set
            {
                if (baslik != value)
                {
                    baslik = value;
                    OnPropertyChanged(nameof(Baslik));
                    OnPropertyChanged(nameof(Icerik)); // Icerik de değişir çünkü içinde Baslik var
                }
            }
        }

        public string? Aciklama
        {
            get => aciklama;
            set
            {
                if (aciklama != value)
                {
                    aciklama = value;
                    OnPropertyChanged(nameof(Aciklama));
                }
            }
        }

        public DateTime Tarih
        {
            get => tarih;
            set
            {
                if (tarih != value)
                {
                    tarih = value;
                    OnPropertyChanged(nameof(Tarih));
                    OnPropertyChanged(nameof(Icerik));
                }
            }
        }

        public string Icerik => $"{Baslik} - {Tarih:dd.MM.yyyy}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
