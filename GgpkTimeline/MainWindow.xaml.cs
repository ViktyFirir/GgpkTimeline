using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using GgpkTimeline.Controls;

namespace GgpkTimeline;

public partial class MainWindow : Window
{
    // ---- Размеры макета (дизайн 1920x1080) ----
    private const double StageWidth = 1920;
    private const double SectionWidth = 1800;     // шаг между эпохами
    private const double FooterTop = 980;         // верхняя граница футера
    private const double SilhouetteScale = 2.45;  // масштаб силуэта города

    private readonly List<TimelineSection> _sections = new();
    private readonly List<RadioButton> _dots = new();
    private readonly List<string> _footerLabels = new();
    private TimelineData _data = new();

    private StackPanel? _futurePanel;
    private bool _futureRevealed;

    private int _current = -1;
    private double _targetOffset;
    private bool _animating;
    private int _animId;

    // ---- Свойство для плавной анимации прокрутки ----
    public static readonly DependencyProperty ScrollPositionProperty =
        DependencyProperty.Register(nameof(ScrollPosition), typeof(double), typeof(MainWindow),
            new PropertyMetadata(0.0, (d, e) => ((MainWindow)d).Scroller.ScrollToHorizontalOffset((double)e.NewValue)));

    public double ScrollPosition
    {
        get => (double)GetValue(ScrollPositionProperty);
        set => SetValue(ScrollPositionProperty, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        AboutPage.CloseRequested += CloseAbout;
    }

    private double MaxOffset => Math.Max(0, TimelineCanvas.Width - StageWidth);

    // =====================================================================
    //  Запуск
    // =====================================================================
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!LoadData()) { Close(); return; }

        BuildTimeline();
        BuildFooter();
        PlayIntro();
        UpdateState();
    }

    private bool LoadData()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "timeline.json");
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            _data = JsonSerializer.Deserialize<TimelineData>(json, options) ?? new TimelineData();
            if (_data.Periods.Count == 0) throw new InvalidDataException("В timeline.json нет ни одного периода.");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось прочитать Data/timeline.json:\n" + ex.Message,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    // =====================================================================
    //  Построение прокручиваемой ленты
    // =====================================================================
    private void BuildTimeline()
    {
        int count = _data.Periods.Count;
        TimelineCanvas.Width = (count + 1) * SectionWidth + 120;

        // --- Силуэт города (плитки, каждая вторая отражена - стык незаметен) ---
        var sil = new BitmapImage();
        sil.BeginInit();
        sil.UriSource = new Uri("pack://application:,,,/Assets/Silhouette.png");
        sil.CacheOption = BitmapCacheOption.OnLoad;
        sil.EndInit();
        sil.Freeze();

        double tileW = sil.PixelWidth * SilhouetteScale;
        double tileH = sil.PixelHeight * SilhouetteScale;
        int tiles = (int)Math.Ceiling(TimelineCanvas.Width / tileW);

        for (int k = 0; k < tiles; k++)
        {
            var img = new Image
            {
                Source = sil,
                Width = tileW,
                Height = tileH,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            if (k % 2 == 1)
            {
                img.RenderTransformOrigin = new Point(0.5, 0.5);
                img.RenderTransform = new ScaleTransform(-1, 1);
            }
            Canvas.SetLeft(img, k * tileW);
            Canvas.SetTop(img, FooterTop + 10 - tileH);
            TimelineCanvas.Children.Add(img);
        }

        // --- Эпохи ---
        for (int i = 0; i < count; i++)
        {
            var section = new TimelineSection(_data.Periods[i]);
            section.DetailsRequested += OnDetailsRequested;
            Canvas.SetLeft(section, i * SectionWidth);
            Canvas.SetTop(section, 0);
            TimelineCanvas.Children.Add(section);
            _sections.Add(section);
        }

        // --- Финальный экран «Всё ещё впереди!» ---
        _futurePanel = new StackPanel { Opacity = 0, IsHitTestVisible = false };
        _futurePanel.Children.Add(new TextBlock
        {
            Text = _data.FutureTitle,
            Width = 1500,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = (FontFamily)FindResource("HeavyFont"),
            FontWeight = FontWeights.ExtraBold,
            FontSize = 200,
            LineHeight = 215,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xF0))
        });
        _futurePanel.RenderTransform = new TranslateTransform(120, 0);
        Canvas.SetLeft(_futurePanel, count * SectionWidth + 120);
        Canvas.SetTop(_futurePanel, 300);
        TimelineCanvas.Children.Add(_futurePanel);
    }

    // =====================================================================
    //  Футер: кружки + подписи
    // =====================================================================
    private void BuildFooter()
    {
        int n = _data.Periods.Count + 1;           // эпохи + «всё ещё впереди»
        double first = 511, pitch = 180;           // как в макете
        if (first + (n - 1) * pitch > 1810)        // если эпох много - сжимаем шаг
        {
            first = 415;
            pitch = (1810 - first) / (n - 1);
        }

        var mainFont = (FontFamily)FindResource("MainFont");
        var dotStyle = (Style)FindResource("DotStyle");

        for (int i = 0; i < n; i++)
        {
            int index = i;
            double cx = first + i * pitch;
            bool isFuture = i == n - 1;

            var dot = new RadioButton
            {
                Style = dotStyle,
                GroupName = "timeline",
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0, 0)
            };
            dot.Click += (_, _) => NavigateTo(index);
            Canvas.SetLeft(dot, cx - 24);
            Canvas.SetTop(dot, -13);
            DotsCanvas.Children.Add(dot);
            _dots.Add(dot);

            var label = new TextBlock
            {
                Text = isFuture ? _data.FutureLabel : _data.Periods[i].Year,
                Width = isFuture ? 120 : 170,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontFamily = mainFont,
                FontWeight = isFuture ? FontWeights.Light : FontWeights.Bold,
                FontSize = isFuture ? 17 : 26,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Opacity = 0
            };
            if (isFuture)
            {
                label.LineHeight = 19;
                label.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            }
            label.MouseLeftButtonDown += (_, _) => NavigateTo(index);
            Canvas.SetLeft(label, cx - label.Width / 2);
            Canvas.SetTop(label, isFuture ? 40 : 44);
            DotsCanvas.Children.Add(label);

            _footerLabels.Add(isFuture ? _data.FutureLabel : _data.Periods[i].FooterLabel);
        }
    }

    // =====================================================================
    //  Анимация при запуске
    // =====================================================================
    private void PlayIntro()
    {
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

        // Логотип: опускается сверху
        Logo.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(800)) { EasingFunction = easeOut });
        LogoShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-40, 0, TimeSpan.FromMilliseconds(900)) { EasingFunction = easeOut });

        // Футер: выезжает снизу
        FooterShift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(100, 0, TimeSpan.FromMilliseconds(700)) { BeginTime = TimeSpan.FromMilliseconds(150), EasingFunction = easeOut });

        // Кружки и подписи: появляются по очереди
        int step = 0;
        for (int i = 0; i < _dots.Count; i++)
        {
            var delay = TimeSpan.FromMilliseconds(600 + step * 70);
            var st = (ScaleTransform)_dots[i].RenderTransform;
            var grow = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450))
            {
                BeginTime = delay,
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 }
            };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

            // подпись лежит сразу после кружка в DotsCanvas
            var label = DotsCanvas.Children[i * 2 + 1];
            label.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)) { BeginTime = delay });
            step++;
        }
    }

    // =====================================================================
    //  Навигация и прокрутка
    // =====================================================================
    private void NavigateTo(int index)
    {
        index = Math.Clamp(index, 0, _dots.Count - 1);
        double target = Math.Min(index * SectionWidth, MaxOffset);
        AnimateScroll(target, 950);
    }

    private void AnimateScroll(double to, int ms)
    {
        int id = ++_animId;
        _animating = true;
        _targetOffset = to;

        var anim = new DoubleAnimation(Scroller.HorizontalOffset, to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        anim.Completed += (_, _) => { if (id == _animId) _animating = false; };
        BeginAnimation(ScrollPositionProperty, anim);
    }

    private void Scroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (!_animating) _targetOffset = Scroller.HorizontalOffset;
        double target = Math.Clamp(_targetOffset - e.Delta * 2.0, 0, MaxOffset);
        AnimateScroll(target, 500);
    }

    private void Scroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!_animating) _targetOffset = Scroller.HorizontalOffset;
        UpdateState();
    }

    /// <summary>Подсвечивает текущий кружок, меняет подпись футера, запускает анимации появления</summary>
    private void UpdateState()
    {
        if (_dots.Count == 0) return;
        double offset = Scroller.HorizontalOffset;

        // появление эпох, которые вошли в экран
        for (int i = 0; i < _sections.Count; i++)
        {
            if (!_sections[i].Revealed && i * SectionWidth + 140 < offset + StageWidth - 120)
                _sections[i].Reveal();
        }
        if (!_futureRevealed && _futurePanel != null &&
            _sections.Count * SectionWidth + 120 < offset + StageWidth - 120)
        {
            _futureRevealed = true;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            _futurePanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(900)) { EasingFunction = ease });
            ((TranslateTransform)_futurePanel.RenderTransform)
                .BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(120, 0, TimeSpan.FromMilliseconds(1000)) { EasingFunction = ease });
        }

        // текущая эпоха
        int idx = Math.Clamp((int)Math.Round(offset / SectionWidth), 0, _dots.Count - 1);
        if (idx != _current)
        {
            _current = idx;
            _dots[idx].IsChecked = true;
            FooterLabel.Text = _footerLabels[idx];
            FooterLabel.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(300)));
        }
    }

    // =====================================================================
    //  Клавиатура
    // =====================================================================
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_aboutOpen)
        {
            if (e.Key == Key.Escape) CloseAbout();
            return;
        }

        switch (e.Key)
        {
            case Key.F1: OpenAbout(); break;
            case Key.Escape: Close(); break;
            case Key.Right:
            case Key.PageDown: NavigateTo(_current + 1); break;
            case Key.Left:
            case Key.PageUp: NavigateTo(_current - 1); break;
            case Key.Home: NavigateTo(0); break;
            case Key.End: NavigateTo(_dots.Count - 1); break;
        }
    }
    private bool _aboutOpen;

    private void Logo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => OpenAbout();

    private void OpenAbout()
    {
        if (_aboutOpen) return;
        _aboutOpen = true;
        AboutPage.Visibility = Visibility.Visible;
        AboutPage.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
        AboutPage.Play();
    }

    private void CloseAbout()
    {
        if (!_aboutOpen) return;
        _aboutOpen = false;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
        fade.Completed += (_, _) => { if (!_aboutOpen) AboutPage.Visibility = Visibility.Collapsed; };
        AboutPage.BeginAnimation(OpacityProperty, fade);
    }
    // =====================================================================
    //  Кнопка «Читать подробнее» - пока заглушка
    // =====================================================================
    private void OnDetailsRequested(TimelineSection section)
    {
        ShowToast($"«Читать подробнее» — {section.Period.Year}: страница в разработке");
    }

    private void ShowToast(string text)
    {
        ToastText.Text = text;
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2450))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2850))));
        Toast.BeginAnimation(OpacityProperty, anim);
    }
}
