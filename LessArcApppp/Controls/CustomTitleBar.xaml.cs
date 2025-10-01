using System;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics; // Color

namespace LessArcApppp.Controls
{
    public partial class CustomTitleBar : ContentView
    {
        public CustomTitleBar()
        {
            InitializeComponent();
            // initial apply
            TitleLabel.Text = Title ?? string.Empty;
            TitleLabel.IsVisible = IsTitleVisible;
            UpdateBackVisibility();
        }

        // ========== BOYUT / STÝL ==========
        public static readonly BindableProperty BarHeightProperty =
            BindableProperty.Create(nameof(BarHeight), typeof(double), typeof(CustomTitleBar), 72d);

        public double BarHeight
        {
            get => (double)GetValue(BarHeightProperty);
            set => SetValue(BarHeightProperty, value);
        }

        public static readonly BindableProperty BarPaddingProperty =
            BindableProperty.Create(nameof(BarPadding), typeof(Thickness), typeof(CustomTitleBar), new Thickness(12, 8));

        public Thickness BarPadding
        {
            get => (Thickness)GetValue(BarPaddingProperty);
            set => SetValue(BarPaddingProperty, value);
        }

        public static readonly BindableProperty BarBackgroundProperty =
            BindableProperty.Create(nameof(BarBackground), typeof(Color), typeof(CustomTitleBar), Color.FromArgb("#5D5A56"));

        public Color BarBackground
        {
            get => (Color)GetValue(BarBackgroundProperty);
            set => SetValue(BarBackgroundProperty, value);
        }

        public static readonly BindableProperty TitleFontSizeProperty =
            BindableProperty.Create(nameof(TitleFontSize), typeof(double), typeof(CustomTitleBar), 22d, propertyChanged: (b, o, n) =>
            {
                var bar = (CustomTitleBar)b;
                if (bar.TitleLabel != null) bar.TitleLabel.FontSize = (double)n;
            });

        public double TitleFontSize
        {
            get => (double)GetValue(TitleFontSizeProperty);
            set => SetValue(TitleFontSizeProperty, value);
        }

        public static readonly BindableProperty IconSizeProperty =
            BindableProperty.Create(nameof(IconSize), typeof(double), typeof(CustomTitleBar), 24d);

        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public static readonly BindableProperty IconTouchSizeProperty =
            BindableProperty.Create(nameof(IconTouchSize), typeof(double), typeof(CustomTitleBar), 44d);

        public double IconTouchSize
        {
            get => (double)GetValue(IconTouchSizeProperty);
            set => SetValue(IconTouchSizeProperty, value);
        }

        // ========== DAVRANIÞ ==========
        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(CustomTitleBar), string.Empty,
                propertyChanged: (b, o, n) =>
                {
                    var bar = (CustomTitleBar)b;
                    var text = (string?)n ?? string.Empty;
                    if (bar.TitleLabel != null)
                    {
                        bar.TitleLabel.Text = text;
                    }
                    bar.OnPropertyChanged(nameof(IsTitleVisible));
                    if (bar.TitleLabel != null) bar.TitleLabel.IsVisible = bar.IsTitleVisible;
                });

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly BindableProperty ShowBackProperty =
            BindableProperty.Create(nameof(ShowBack), typeof(bool), typeof(CustomTitleBar), false,
                propertyChanged: (b, o, n) => ((CustomTitleBar)b).UpdateBackVisibility());

        public bool ShowBack
        {
            get => (bool)GetValue(ShowBackProperty);
            set => SetValue(ShowBackProperty, value);
        }

        public static readonly BindableProperty BackCommandProperty =
            BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(CustomTitleBar), null);

        public ICommand? BackCommand
        {
            get => (ICommand?)GetValue(BackCommandProperty);
            set => SetValue(BackCommandProperty, value);
        }

        // ========== SAÐ SLOT ==========
        public static readonly BindableProperty RightContentProperty =
            BindableProperty.Create(nameof(RightContent), typeof(View), typeof(CustomTitleBar), null,
                propertyChanged: (b, o, n) =>
                {
                    var bar = (CustomTitleBar)b;
                    bar.RightHost.Children.Clear();
                    if (n is View v) bar.RightHost.Children.Add(v);
                    bar.OnPropertyChanged(nameof(RightContentVisible));
                });

        public View? RightContent
        {
            get => (View?)GetValue(RightContentProperty);
            set => SetValue(RightContentProperty, value);
        }

        // ========== YARDIMCI (XAML'de okunur) ==========
        // XAML: IsVisible="{Binding Source={x:Reference self}, Path=IsTitleVisible}"
        public bool IsTitleVisible => !string.IsNullOrWhiteSpace(Title);

        // XAML: IsVisible="{Binding Source={x:Reference self}, Path=RightContentVisible}"
        public bool RightContentVisible => RightHost?.Children?.Count > 0;

        private void UpdateBackVisibility()
        {
            if (BackHost != null) BackHost.IsVisible = ShowBack;
        }

        private async void BackButton_Clicked(object? sender, EventArgs e)
        {
            if (BackCommand?.CanExecute(null) == true)
            {
                BackCommand.Execute(null);
                return;
            }

            var nav = Application.Current?.MainPage?.Navigation;
            if (nav is null) return;

            if (nav.ModalStack?.Count > 0) { await nav.PopModalAsync(true); return; }
            if (nav.NavigationStack?.Count > 1) { await nav.PopAsync(true); return; }

            if (Shell.Current is not null)
            {
                try { await Shell.Current.GoToAsync(".."); } catch { /* ignore */ }
            }
        }
    }
}
