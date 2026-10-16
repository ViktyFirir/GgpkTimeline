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
    private const double FooterTop = 980;      // верхняя граница футера
    private const double CityHeight = 640;     // высота города на экране (масштаб ~2.8)
    private const double YearLeft = 816;       // положение года
    private const double YearTop = 305;       // выше = меньше число
    private const double CityStretchX = 1.0;     // растяжение города по горизонтали (1.0 = как в файле)
    private const double MinCityScreens = 2.6;   // город не уже, чем N экранов

    private TimelineData _data = new();
    private readonly List<RadioButton> _dots = new();
    private readonly List<string> _footerLabels = new();

    private YearReel _reel = null!;
    private TextBlock _futureText = null!;

    private int _count;          // число эпох
    private int _n;              // число позиций = эпохи + «всё ещё впереди»
    private int _index;          // текущая позиция
    private double _cityPan;     // на сколько пикселей город может сдвинуться

    // ---- Прогресс ленты: 0 ... (n-1). По нему сдвигается город ----
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(MainWindow),
            new PropertyMetadata(0.0, (d, e) => ((MainWindow)d).ApplyProgress((double)e.NewValue)));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public MainWindow()
    {
        InitializeComponent();
    }

    // =====================================================================
    //  Запуск
    // =====================================================================
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!LoadData()) { Close(); return; }

        _count = _data.Periods.Count;
        _n = _count + 1;

        BuildCity();
        BuildYear();
        BuildFooter();

        Card.DetailsRequested += p => ShowToast($"«Читать подробнее» — {p.Year}: страница в разработке");

        PlayIntro();
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
    //  Город: одна картинка на весь проект
    // =====================================================================
    private void BuildCity()
    {
        var src = (BitmapSource)City.Source;
        double cityW = CityHeight * src.PixelWidth / src.PixelHeight * CityStretchX;
        cityW = Math.Max(cityW, StageWidth * MinCityScreens);

        City.Width = cityW;
        City.Height = CityHeight;
        City.Margin = new Thickness(0, FooterTop + 10 - CityHeight, 0, 0);   // стоит на футере

        _cityPan = Math.Max(0, cityW - StageWidth);
        ApplyProgress(0);
    }

    private void ApplyProgress(double p)
    {
        if (_n < 2) return;
        CityShift.X = -(p / (_n - 1)) * _cityPan;
    }

    // =====================================================================
    //  Год-барабан и финальная надпись (стоят на месте, позади города)
    // =====================================================================
    private void BuildYear()
    {
        var cream = new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xF0));

        _reel = new YearReel((FontFamily)FindResource("HeavyFont"), 330, cream)
        {
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };
        _reel.MouseEnter += (_, _) => ZoomYear(1.04, 250);
        _reel.MouseLeave += (_, _) => ZoomYear(1.0, 300);
        Canvas.SetLeft(_reel, YearLeft);
        Canvas.SetTop(_reel, YearTop);
        YearLayer.Children.Add(_reel);

        _futureText = new TextBlock
        {
            Text = _data.FutureTitle,
            Width = 1500,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = (FontFamily)FindResource("HeavyFont"),
            FontWeight = FontWeights.ExtraBold,
            FontSize = 200,
            LineHeight = 215,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Foreground = cream,
            Opacity = 0,
            IsHitTestVisible = false,
            RenderTransform = new TranslateTransform(80, 0)
        };
        Canvas.SetLeft(_futureText, 120);
        Canvas.SetTop(_futureText, 300);
        YearLayer.Children.Add(_futureText);
    }

    private void ZoomYear(double to, int ms)
    {
        var st = (ScaleTransform)_reel.RenderTransform;
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    // =====================================================================
    //  Футер: кружки + подписи
    // =====================================================================
    private void BuildFooter()
    {
        double first = 511, pitch = 180;           // как в макете
        if (first + (_n - 1) * pitch > 1810)       // если эпох много - сжимаем шаг
        {
            first = 415;
            pitch = (1810 - first) / (_n - 1);
        }

        var mainFont = (FontFamily)FindResource("MainFont");
        var dotStyle = (Style)FindResource("DotStyle");

        for (int i = 0; i < _n; i++)
        {
            int index = i;
            double cx = first + i * pitch;
            bool isFuture = i == _n - 1;

            var dot = new RadioButton
            {
                Style = dotStyle,
                GroupName = "timeline",
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0, 0)
            };
            dot.Click += (_, _) => NavigateTo(index);
            Canvas.SetLeft(dot, cx - 24);
            Canvas.SetTop(dot, -21);               // центр кружка = центр оранжевой линии (3 px от верха)
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
            Canvas.SetTop(label, isFuture ? 38 : 42);
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

        // Город: поднимается и проявляется
        City.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(1000)) { BeginTime = TimeSpan.FromMilliseconds(200), EasingFunction = easeOut });
        CityShift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(120, 0, TimeSpan.FromMilliseconds(1100)) { BeginTime = TimeSpan.FromMilliseconds(200), EasingFunction = easeOut });

        // Кружки и подписи: появляются по очереди
        for (int i = 0; i < _dots.Count; i++)
        {
            var delay = TimeSpan.FromMilliseconds(600 + i * 70);
            var st = (ScaleTransform)_dots[i].RenderTransform;
            var grow = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450))
            {
                BeginTime = delay,
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 }
            };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

            // подпись лежит сразу после кружка в DotsCanvas
            DotsCanvas.Children[i * 2 + 1].BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)) { BeginTime = delay });
        }

        // Карточка + барабаны года: стартуют с 0000 и «докручиваются» до первого года
        var first = _data.Periods[0];
        Card.SetInstant(first);
        Card.ShowIn(500);

        _index = 0;
        _dots[0].IsChecked = true;
        SetFooterLabel(0);

        _reel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(500) });
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _reel.RollTo(first.Year, true);
        };
        timer.Start();
    }

    // =====================================================================
    //  Навигация: двигается только город, год и текст меняются на месте
    // =====================================================================
    private void NavigateTo(int index)
    {
        index = Math.Clamp(index, 0, _n - 1);
        if (index == _index) return;

        int old = _index;
        _index = index;
        _dots[index].IsChecked = true;
        SetFooterLabel(index);

        // 1) город уезжает
        var move = new DoubleAnimation(Progress, index, TimeSpan.FromMilliseconds(1600))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        BeginAnimation(ProgressProperty, move);

        bool toFuture = index == _count;
        bool fromFuture = old == _count;

        if (toFuture)
        {
            // финал: карточка и год гаснут, появляется «Всё ещё впереди!»
            Card.HideOut();
            _reel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(400)));
            ShowFuture(true);
            return;
        }

        var p = _data.Periods[index];

        if (fromFuture)
        {
            ShowFuture(false);
            Card.SetInstant(p);
            Card.ShowIn(250);
            _reel.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(250) });
        }
        else
        {
            Card.SwapTo(p);                                  // 2) текст меняется
        }

        _reel.RollTo(p.Year, index > old);                   // 3) цифры крутятся
    }

    private void ShowFuture(bool show)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var shift = (TranslateTransform)_futureText.RenderTransform;
        _futureText.BeginAnimation(OpacityProperty,
            new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(show ? 900 : 300))
            { BeginTime = TimeSpan.FromMilliseconds(show ? 300 : 0), EasingFunction = ease });
        shift.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(show ? 0 : 80, TimeSpan.FromMilliseconds(show ? 1000 : 300))
            { BeginTime = TimeSpan.FromMilliseconds(show ? 300 : 0), EasingFunction = ease });
    }

    private void SetFooterLabel(int index)
    {
        FooterLabel.Text = _footerLabels[index];
        FooterLabel.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(300)));
    }

    // =====================================================================
    //  Управление: колесо, стрелки, свайп мышью/пальцем
    // =====================================================================
    private DateTime _lastWheel = DateTime.MinValue;
    private double _wheelAcc;

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        _wheelAcc += e.Delta;
        if (Math.Abs(_wheelAcc) < 120) return;

        if ((DateTime.Now - _lastWheel).TotalMilliseconds >= 600)
        {
            _lastWheel = DateTime.Now;
            NavigateTo(_index + (_wheelAcc < 0 ? 1 : -1));
        }
        _wheelAcc = 0;
    }

    private Point _dragStart;

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        => _dragStart = e.GetPosition(this);

    private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        var d = e.GetPosition(this) - _dragStart;
        if (Math.Abs(d.X) > 150 && Math.Abs(d.X) > Math.Abs(d.Y) * 2)
            NavigateTo(_index + (d.X < 0 ? 1 : -1));
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.Right:
            case Key.PageDown: NavigateTo(_index + 1); break;
            case Key.Left:
            case Key.PageUp: NavigateTo(_index - 1); break;
            case Key.Home: NavigateTo(0); break;
            case Key.End: NavigateTo(_n - 1); break;
        }
    }

    // =====================================================================
    //  Клик по логотипу - возврат на главный (стартовый) экран
    // =====================================================================
    private void Logo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => GoHome();

    private void GoHome()
    {
        var start = new StartWindow();
        Application.Current.MainWindow = start;
        start.Show();
        Close();
    }

    // =====================================================================
    //  Всплывающее сообщение для кнопки-заглушки
    // =====================================================================
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
