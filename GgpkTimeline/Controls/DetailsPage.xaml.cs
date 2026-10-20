using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private readonly List<Border> _photos = new();   // все фотографии страницы (для анимации появления)
        private readonly List<Border> _ovals = new();    // овалы с портретами руководителей
        private const double MaxPhotoHeight = 450;       // максимальная высота ряда с фотографиями
        private const double PhotoGap = 50;              // расстояние между двумя фотографиями в ряду

        // Файлы вида 1956_1.jpg - обычные фотографии периода; всё остальное (Gluskin.png, Hrapko.png...) - портреты руководителей
        private static readonly System.Text.RegularExpressions.Regex PhotoNameRegex =
            new(@"^\d{4}_\d+$", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>Картинка + подпись: передаётся в просмотр на весь экран.</summary>
        private sealed record PhotoInfo(ImageSource Source, string? Title, string? Description);

        /// <summary>Ряд из одной или двух фотографий.</summary>
        private sealed class PhotoRow
        {
            public Grid Host = null!;
            public readonly List<Border> Items = new();
            public readonly List<double> Ratios = new();
        }

        public DetailsPage()
        {
            InitializeComponent();
        }

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }


        // =====================================================================
        //  Данные: один период из Data/timeline.json
        // =====================================================================
        public void SetPeriod(Period period)
        {
            TitleText.Text = period.Year;
            SubTitleText.Text = period.Title;
            SubTitleText.Visibility = string.IsNullOrWhiteSpace(period.Title) ? Visibility.Collapsed : Visibility.Visible;

            var images = period.Images ?? new List<PeriodImage>();
            var photos = images.Where(i => !IsPortrait(i)).ToList();   // обычные фотографии периода
            var portraits = images.Where(IsPortrait).ToList();         // портреты руководителей

            Lightbox.Visibility = Visibility.Collapsed;   // закрыть просмотр, если был открыт
            Lightbox.Opacity = 0;

            BuildContent(period, photos, portraits);
            BuildPortraits(portraits);

            DetailsScroller.ScrollToTop();
        }

        /// <summary>Портрет - картинка, имя файла которой не похоже на "год_номер" (например Assets/Years/Gluskin.png).</summary>
        private static bool IsPortrait(PeriodImage img)
        {
            var name = Path.GetFileNameWithoutExtension(img.File ?? "");
            return !PhotoNameRegex.IsMatch(name);
        }

        /// <summary>
        /// Основная часть страницы: абзац - две фотографии - абзац - две фотографии - ...
        /// Если фотографий нечётное число, последняя стоит одна по центру.
        /// Если абзацы закончились раньше фотографий, оставшиеся ряды идут подряд; если наоборот - оставшийся текст идёт без фотографий.
        /// </summary>
        private void BuildContent(Period period, List<PeriodImage> photos, List<PeriodImage> portraits)
        {
            ContentPanel.Children.Clear();
            _photos.Clear();

            // абзацы: вступление (text) + details, кроме абзацев, которые повторяют подпись к портрету
            var texts = new List<(string Text, double Size)>();
            if (!string.IsNullOrWhiteSpace(period.Text))
                texts.Add((period.Text, 22));
            foreach (var d in period.Details ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(d) || DuplicatesPortrait(d, portraits)) continue;
                texts.Add((d, 20));
            }

            // фотографии по две
            var groups = new List<List<PeriodImage>>();
            for (int i = 0; i < photos.Count; i += 2)
                groups.Add(photos.Skip(i).Take(2).ToList());

            int g = 0;
            foreach (var (text, size) in texts)
            {
                bool photosFollow = g < groups.Count;
                ContentPanel.Children.Add(CreateParagraph(text, size, photosFollow ? 35 : 25));
                if (photosFollow)
                    ContentPanel.Children.Add(CreatePhotoRow(groups[g++]));
            }
            while (g < groups.Count)
                ContentPanel.Children.Add(CreatePhotoRow(groups[g++]));
        }

        /// <summary>Ряд из одной или двух фотографий. Одна фотография - по центру. Ничего не обрезается.</summary>
        private Grid CreatePhotoRow(List<PeriodImage> items)
        {
            var row = new PhotoRow();
            var strip = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center   // единственная фотография оказывается по центру
            };

            for (int i = 0; i < items.Count; i++)
            {
                var photo = CreatePhoto(items[i], out double ratio);
                photo.Margin = new Thickness(i == 0 ? 0 : PhotoGap, 0, 0, 0);
                strip.Children.Add(photo);
                row.Items.Add(photo);
                row.Ratios.Add(ratio);
                _photos.Add(photo);
            }

            row.Host = new Grid { Margin = new Thickness(0, 0, 0, 50) };
            row.Host.Children.Add(strip);
            row.Host.SizeChanged += (_, _) => FitRow(row);
            return row.Host;
        }

        /// <summary>Подгоняет размер рамок под пропорции картинок: общая высота ряда, ширина по пропорции - ничего не обрезается.</summary>
        private static void FitRow(PhotoRow row)
        {
            double width = row.Host.ActualWidth;
            if (row.Items.Count == 0 || width <= 0) return;

            double gap = PhotoGap * (row.Items.Count - 1);
            double height = Math.Min(MaxPhotoHeight, (width - gap) / row.Ratios.Sum());

            for (int i = 0; i < row.Items.Count; i++)
            {
                row.Items[i].Height = height;
                row.Items[i].Width = height * row.Ratios[i];
            }
        }

        /// <summary>true, если абзац - это "Заголовок - описание" портрета, уже показанный рядом с ним.</summary>
        private static bool DuplicatesPortrait(string paragraph, List<PeriodImage> portraits)
        {
            var p = paragraph.Trim();
            foreach (var img in portraits)
            {
                var title = img.Title?.Trim();
                if (!string.IsNullOrEmpty(title) && p.StartsWith(title, StringComparison.Ordinal))
                    return true;

                var desc = img.Description?.Trim();
                if (!string.IsNullOrEmpty(desc))
                {
                    var head = desc.Length > 40 ? desc.Substring(0, 40) : desc;
                    if (p.Contains(head, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Портрет руководителя: овал с фото слева, заголовок и описание справа.</summary>
        private void BuildPortraits(List<PeriodImage> images)
        {
            ImagesPanel.Children.Clear();
            _ovals.Clear();

            foreach (var img in images)
            {
                var row = new Grid { Margin = new Thickness(0, 20, 0, 60) };
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
                {
                    oval.Background = new ImageBrush(source) { Stretch = Stretch.UniformToFill };
                    oval.Cursor = Cursors.Hand;
                    var info = new PhotoInfo(source, img.Title, img.Description);
                    oval.MouseLeftButtonUp += (_, _) => OpenLightbox(info);
                }

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
                    text.Children.Add(CreateParagraph(img.Description, 20, 0));

                Grid.SetColumn(text, 1);
                row.Children.Add(text);

                ImagesPanel.Children.Add(row);
            }
        }

        private static TextBlock CreateParagraph(string text, double fontSize, double bottom) => new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            LineHeight = Math.Round(fontSize * 1.5),
            Foreground = SoftText,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, bottom)
        };

        private Border CreatePhoto(PeriodImage img, out double ratio)
        {
            var photo = new Border
            {
                Background = new SolidColorBrush(BeigeColor),   // нет файла - бежевая заглушка
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(BeigeBorder),
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
                Opacity = 0
            };

            ratio = 1.5;   // для заглушки, если файла нет
            var source = LoadImage(img.File);
            if (source != null)
            {
                if (source.Height > 0) ratio = source.Width / source.Height;
                photo.Background = new ImageBrush(source) { Stretch = Stretch.Fill };   // рамка уже по пропорции картинки
                photo.Cursor = Cursors.Hand;
                var info = new PhotoInfo(source, img.Title, img.Description);
                photo.MouseLeftButtonUp += (_, _) => OpenLightbox(info);
            }

            // Наведение: фото чуть увеличивается, рамка становится оранжевой
            photo.MouseEnter += (_, _) => Hover(photo, 1.02, OrangeColor, 200);
            photo.MouseLeave += (_, _) => Hover(photo, 1.0, BeigeBorder, 250);
            return photo;
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

        /// <summary>Файл лежит относительно папки с exe, например Assets/Years/1956_1.png. Нет файла - null.</summary>
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

            // 4. Фотографии периода (задержка растёт по порядку, но не больше 600 мс - нижние не ждут долго)
            for (int i = 0; i < _photos.Count; i++)
            {
                int delay = 500 + Math.Min(i, 6) * 100;
                Fade(_photos[i], 600, delay, easeOut);
                Pop(_photos[i], 0.9, 600, delay, easeOut);
            }

            // 5. Овал с портретом руководителя
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

        // =====================================================================
        //  Просмотр картинки на весь экран
        // =====================================================================
        private void OpenLightbox(PhotoInfo info)
        {
            LightboxImage.Source = info.Source;

            LightboxTitle.Text = info.Title ?? "";
            LightboxTitle.Visibility = string.IsNullOrWhiteSpace(info.Title) ? Visibility.Collapsed : Visibility.Visible;
            LightboxDescription.Text = info.Description ?? "";
            LightboxDescription.Visibility = string.IsNullOrWhiteSpace(info.Description) ? Visibility.Collapsed : Visibility.Visible;

            Lightbox.Visibility = Visibility.Visible;
            Lightbox.Focus();   // чтобы работал Esc

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Lightbox.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            var zoom = new DoubleAnimation(0.95, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease };
            LightboxScale.BeginAnimation(ScaleTransform.ScaleXProperty, zoom);
            LightboxScale.BeginAnimation(ScaleTransform.ScaleYProperty, zoom);
        }

        private void CloseLightbox()
        {
            if (Lightbox.Visibility != Visibility.Visible) return;

            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
            fade.Completed += (_, _) =>
            {
                Lightbox.Visibility = Visibility.Collapsed;
                LightboxImage.Source = null;
            };
            Lightbox.BeginAnimation(OpacityProperty, fade);
        }

        private void Lightbox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => CloseLightbox();

        private void LightboxClose_Click(object sender, RoutedEventArgs e) => CloseLightbox();

        private void Lightbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseLightbox();
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke();
        }
    }
}
