using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace GgpkTimeline.Controls
{
    public partial class DetailsPage : UserControl
    {
        public event Action? CloseRequested;

        // ---- Цвета макета ----
        private static readonly Color BeigeColor = Color.FromRgb(0xEB, 0xD9, 0xBC);
        private static readonly Color BeigeBorder = Color.FromArgb(0x55, 0xEB, 0xD9, 0xBC);
        private static readonly Color OrangeColor = Color.FromRgb(0xE6, 0x66, 0x35);
        private static readonly Brush SoftText = Frozen(Color.FromRgb(0xC9, 0xD3, 0xEE));
        private static readonly Brush DarkText = Frozen(Color.FromRgb(0x0B, 0x1A, 0x4A));

        private readonly List<Border> _cards = new();   // бежевые блоки с текстом
        private readonly List<Border> _ovals = new();   // овалы с фото

        public DetailsPage()
        {
            InitializeComponent();
            LoadHeaderBackground();
        }

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>Фон шапки. Нет файла Assets/DetailFon.png - страница просто открывается без него.</summary>
        private void LoadHeaderBackground()
        {
            try
            {
                HeaderBg.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/DetailFon.png", UriKind.Absolute));
            }
            catch
            {
                HeaderBg.Visibility = Visibility.Collapsed;
            }
        }

        // =====================================================================
        //  Данные: один период из Data/timeline.json
        // =====================================================================
        public void SetPeriod(Period period)
        {
            TitleText.Text = period.Year;
            SubTitleText.Text = period.Title;
            SubTitleText.Visibility = string.IsNullOrWhiteSpace(period.Title) ? Visibility.Collapsed : Visibility.Visible;
            IntroText.Text = period.Text;
            IntroText.Visibility = string.IsNullOrWhiteSpace(period.Text) ? Visibility.Collapsed : Visibility.Visible;

            BuildDetails(period.Details);
            BuildImages(period.Images);

            DetailsScroller.ScrollToTop();
        }

        /// <summary>Первые два абзаца details - в бежевые блоки, остальные - обычным текстом ниже.</summary>
        private void BuildDetails(List<string> details)
        {
            CardsGrid.Children.Clear();
            CardsGrid.ColumnDefinitions.Clear();
            ExtraPanel.Children.Clear();
            _cards.Clear();

            var items = details.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

            int cardCount = Math.Min(2, items.Count);
            for (int i = 0; i < cardCount; i++)
            {
                CardsGrid.ColumnDefinitions.Add(new ColumnDefinition());

                var card = CreateCard(items[i]);
                card.Margin = cardCount == 2
                    ? (i == 0 ? new Thickness(0, 0, 25, 0) : new Thickness(25, 0, 0, 0))
                    : new Thickness(0);
                Grid.SetColumn(card, i);
                CardsGrid.Children.Add(card);
                _cards.Add(card);
            }
            CardsGrid.Visibility = cardCount == 0 ? Visibility.Collapsed : Visibility.Visible;

            for (int i = 2; i < items.Count; i++)
                ExtraPanel.Children.Add(CreateParagraph(items[i], 18));
            ExtraPanel.Visibility = items.Count > 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Каждая картинка из images: овал с фото слева, заголовок и описание справа.</summary>
        private void BuildImages(List<PeriodImage> images)
        {
            ImagesPanel.Children.Clear();
            _ovals.Clear();

            foreach (var img in images)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 60) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var oval = new Border
                {
                    Width = 400,
                    Height = 500,
                    CornerRadius = new CornerRadius(200),
                    Background = new SolidColorBrush(BeigeColor),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(BeigeBorder),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(0.8, 0.8),
                    Opacity = 0
                };

                // нет файла - остаётся бежевая заглушка
                var source = LoadImage(img.File);
                if (source != null)
                    oval.Background = new ImageBrush(source) { Stretch = Stretch.UniformToFill };

                Grid.SetColumn(oval, 0);
                row.Children.Add(oval);
                _ovals.Add(oval);

                var text = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(60, 0, 0, 0)
                };
                if (!string.IsNullOrWhiteSpace(img.Title))
                {
                    text.Children.Add(new TextBlock
                    {
                        Text = img.Title,
                        FontSize = 38,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 15)
                    });
                }
                if (!string.IsNullOrWhiteSpace(img.Description))
                    text.Children.Add(CreateParagraph(img.Description, 0));

                Grid.SetColumn(text, 1);
                row.Children.Add(text);

                ImagesPanel.Children.Add(row);
            }
        }

        private static TextBlock CreateParagraph(string text, double bottom) => new TextBlock
        {
            Text = text,
            FontSize = 20,
            LineHeight = 30,
            Foreground = SoftText,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, bottom)
        };

        private static Border CreateCard(string text)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(BeigeColor),
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(BeigeBorder),
                Padding = new Thickness(32, 28, 32, 32),
                MinHeight = 260,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
                Opacity = 0,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 20,
                    LineHeight = 30,
                    Foreground = DarkText,
                    TextWrapping = TextWrapping.Wrap
                }
            };

            // Наведение: блок чуть увеличивается, рамка становится оранжевой
            card.MouseEnter += (_, _) => Hover(card, 1.02, OrangeColor, 200);
            card.MouseLeave += (_, _) => Hover(card, 1.0, BeigeBorder, 250);
            return card;
        }

        private static void Hover(Border b, double scale, Color border, int ms)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var st = (ScaleTransform)b.RenderTransform;
            var a = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            ((SolidColorBrush)b.BorderBrush).BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(border, TimeSpan.FromMilliseconds(ms)));
        }

        /// <summary>Файл лежит относительно папки с exe, например Images/Years/1956.png. Нет файла - null.</summary>
        private static ImageSource? LoadImage(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;   // файл не блокируется
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        // =====================================================================
        //  Анимация появления
        // =====================================================================
        public void Play()
        {
            DetailsScroller.ScrollToTop();
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 1. Год
            Fade(TitleText, 600, 0, easeOut);
            Slide(TitleShift, -40, 700, 0, easeOut);

            // 2. Кнопка закрытия
            Fade(CloseButton, 600, 200, easeOut);

            // 3. Основной контент
            Fade(BodyPanel, 800, 300, easeOut);
            Slide(BodyShift, 40, 800, 300, easeOut);

            // 4. Бежевые блоки
            for (int i = 0; i < _cards.Count; i++)
            {
                Fade(_cards[i], 600, 500 + i * 100, easeOut);
                Pop(_cards[i], 0.9, 600, 500 + i * 100, easeOut);
            }

            // 5. Овалы с фото
            for (int i = 0; i < _ovals.Count; i++)
            {
                Fade(_ovals[i], 700, 700 + i * 150, easeOut);
                Pop(_ovals[i], 0.8, 700, 700 + i * 150, easeOut);
            }
        }

        private static void Fade(UIElement e, int ms, int delay, IEasingFunction ease)
        {
            e.BeginAnimation(UIElement.OpacityProperty, null);
            e.Opacity = 0;
            e.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms))
                { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = ease });
        }

        private static void Slide(TranslateTransform t, double from, int ms, int delay, IEasingFunction ease)
        {
            t.BeginAnimation(TranslateTransform.YProperty, null);
            t.Y = from;
            t.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(from, 0, TimeSpan.FromMilliseconds(ms))
                { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = ease });
        }

        private static void Pop(Border b, double from, int ms, int delay, IEasingFunction ease)
        {
            var st = (ScaleTransform)b.RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            st.ScaleX = from;
            st.ScaleY = from;
            var a = new DoubleAnimation(from, 1, TimeSpan.FromMilliseconds(ms))
            { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = ease };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke();
        }
    }
}
