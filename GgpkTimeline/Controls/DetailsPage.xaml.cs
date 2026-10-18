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

        private readonly List<Border> _photos = new();  // две фотографии периода (images[0], images[1])
        private readonly List<Border> _ovals = new();   // овал с портретом (images[2])
        private readonly List<double> _photoRatios = new();   // пропорции (ширина / высота) двух фотографий
        private const double MaxPhotoHeight = 450;       // максимальная высота ряда с фотографиями

        public DetailsPage()
        {
            InitializeComponent();
            PhotosGrid.SizeChanged += (_, _) => FitPhotos();
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
            IntroText.Text = period.Text;
            IntroText.Visibility = string.IsNullOrWhiteSpace(period.Text) ? Visibility.Collapsed : Visibility.Visible;

            var images = period.Images ?? new List<PeriodImage>();
            var photos = images.Take(2).ToList();      // images[0], images[1] - две фотографии в ряд
            var portraits = images.Skip(2).ToList();   // images[2] - портрет с заголовком и описанием

            Lightbox.Visibility = Visibility.Collapsed;   // закрыть просмотр, если был открыт
            Lightbox.Opacity = 0;

            BuildPhotos(photos);
            BuildDetails(period.Details, portraits);
            BuildPortraits(portraits);

            DetailsScroller.ScrollToTop();
        }

        /// <summary>Две фотографии периода (images[0] и images[1]) - в один ряд, на месте бывших бежевых блоков.</summary>
        private void BuildPhotos(List<PeriodImage> photos)
        {
            PhotosGrid.Children.Clear();
            PhotosGrid.ColumnDefinitions.Clear();
            _photos.Clear();
            _photoRatios.Clear();

            for (int i = 0; i < photos.Count; i++)
            {
                var photo = CreatePhoto(photos[i].File, out double ratio);
                _photoRatios.Add(ratio);

                if (photo.Tag is ImageSource photoSource)
                {
                    photo.Cursor = Cursors.Hand;
                    photo.MouseLeftButtonUp += (_, _) => OpenLightbox(photoSource);
                }

                // ширина колонки пропорциональна пропорциям фото - фотографии одной высоты, без обрезки
                PhotosGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });

                photo.Margin = photos.Count == 2
                    ? (i == 0 ? new Thickness(0, 0, 25, 0) : new Thickness(25, 0, 0, 0))
                    : new Thickness(0);
                Grid.SetColumn(photo, i);
                PhotosGrid.Children.Add(photo);
                _photos.Add(photo);
            }
            PhotosGrid.Visibility = photos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            FitPhotos();
        }

        /// <summary>Подгоняет размер рамок под пропорции картинок: общая высота, ширина по пропорции - ничего не обрезается.</summary>
        private void FitPhotos()
        {
            if (_photos.Count == 0 || PhotosGrid.ActualWidth <= 0) return;

            double gap = _photos.Count == 2 ? 50 : 0;   // 25 + 25 между фотографиями
            double height = Math.Min(MaxPhotoHeight, (PhotosGrid.ActualWidth - gap) / _photoRatios.Sum());

            for (int i = 0; i < _photos.Count; i++)
            {
                _photos[i].Height = height;
                _photos[i].Width = height * _photoRatios[i];
            }
        }

        /// <summary>
        /// Абзацы из details - обычным текстом под фотографиями.
        /// Абзац, который повторяет заголовок/описание портрета, пропускается (он уже выводится рядом с портретом).
        /// </summary>
        private void BuildDetails(List<string> details, List<PeriodImage> portraits)
        {
            ExtraPanel.Children.Clear();

            var items = (details ?? new List<string>())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Where(d => !DuplicatesPortrait(d, portraits))
                .ToList();

            foreach (var item in items)
                ExtraPanel.Children.Add(CreateParagraph(item, 18));
            ExtraPanel.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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

        /// <summary>Портрет (images[2]): овал с фото слева, заголовок и описание справа.</summary>
        private void BuildPortraits(List<PeriodImage> images)
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
                {
                    oval.Background = new ImageBrush(source) { Stretch = Stretch.UniformToFill };
                    oval.Cursor = Cursors.Hand;
                    oval.MouseLeftButtonUp += (_, _) => OpenLightbox(source);
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

        private static Border CreatePhoto(string file, out double ratio)
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
            var source = LoadImage(file);
            if (source != null)
            {
                if (source.Height > 0) ratio = source.Width / source.Height;
                photo.Background = new ImageBrush(source) { Stretch = Stretch.Fill };   // рамка уже по пропорции картинки
                photo.Tag = source;   // для просмотра на весь экран
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

            // 4. Две фотографии периода
            for (int i = 0; i < _photos.Count; i++)
            {
                Fade(_photos[i], 600, 500 + i * 100, easeOut);
                Pop(_photos[i], 0.9, 600, 500 + i * 100, easeOut);
            }

            // 5. Овал с портретом
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
        private void OpenLightbox(ImageSource source)
        {
            LightboxImage.Source = source;
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
