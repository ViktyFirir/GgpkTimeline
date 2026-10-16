using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GgpkTimeline.Controls;

public partial class TimelineSection : UserControl
{
    public Period Period { get; }
    public bool Revealed { get; private set; }

    /// <summary>Нажата кнопка «Читать подробнее» (пока заглушка)</summary>
    public event Action<TimelineSection>? DetailsRequested;

    public TimelineSection(Period period)
    {
        InitializeComponent();
        Period = period;
        YearText.Text = period.Year;
        TitleText.Text = period.Title;
        BodyText.Text = period.Text;
    }

    // ---------- Анимация появления ----------
    public void Reveal()
    {
        if (Revealed) return;
        Revealed = true;

        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

        // Карточка: проявляется и выезжает снизу
        Fade(Card, 0, 1, 650, 0, easeOut);
        Move(CardShift, TranslateTransform.YProperty, 70, 0, 800, 0, easeOut);

        // Год: проявляется и выезжает справа с небольшой задержкой
        Fade(YearText, 0, 1, 800, 200, easeOut);
        Move(YearShift, TranslateTransform.XProperty, 120, 0, 950, 200, easeOut);
    }

    // ---------- Анимация при наведении ----------
    private void Card_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Scale(CardScale, 1.02, 200, ease);
        Glow.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
    }

    private void Card_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Scale(CardScale, 1.0, 250, ease);
        Glow.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)));
    }

    private void Year_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        => Scale(YearScale, 1.04, 250, new CubicEase { EasingMode = EasingMode.EaseOut });

    private void Year_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => Scale(YearScale, 1.0, 300, new CubicEase { EasingMode = EasingMode.EaseOut });

    private void DetailsButton_Click(object sender, RoutedEventArgs e)
        => DetailsRequested?.Invoke(this);

    // ---------- Помощники ----------
    private static void Fade(UIElement target, double from, double to, int ms, int delayMs, IEasingFunction ease)
    {
        target.BeginAnimation(OpacityProperty, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = ease
        });
    }

    private static void Move(TranslateTransform t, DependencyProperty prop, double from, double to, int ms, int delayMs, IEasingFunction ease)
    {
        t.BeginAnimation(prop, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = ease
        });
    }

    private static void Scale(ScaleTransform t, double to, int ms, IEasingFunction ease)
    {
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        t.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        t.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
