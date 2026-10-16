using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GgpkTimeline.Controls;

/// <summary>Статичная карточка: стоит на месте, меняется только текст.</summary>
public partial class InfoCard : UserControl
{
    public Period? Period { get; private set; }

    /// <summary>Нажата «Читать подробнее» (пока заглушка)</summary>
    public event Action<Period>? DetailsRequested;

    private int _swapId;

    public InfoCard()
    {
        InitializeComponent();
        IsHitTestVisible = false;
    }

    /// <summary>Поставить текст сразу, без анимации</summary>
    public void SetInstant(Period p)
    {
        Period = p;
        TitleText.Text = p.Title;
        BodyText.Text = p.Text;
    }

    /// <summary>Сменить текст: старый уходит вверх, новый приходит снизу</summary>
    public void SwapTo(Period p)
    {
        int id = ++_swapId;
        Period = p;

        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        fadeOut.Completed += (_, _) =>
        {
            if (id != _swapId) return;
            TitleText.Text = p.Title;
            BodyText.Text = p.Text;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            TextHost.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380)) { EasingFunction = ease });
            TextShift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(26, 0, TimeSpan.FromMilliseconds(450)) { EasingFunction = ease });
        };
        TextHost.BeginAnimation(OpacityProperty, fadeOut);
        TextShift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-20, TimeSpan.FromMilliseconds(150)));
    }

    /// <summary>Появление карточки (при запуске и при возврате с финального экрана)</summary>
    public void ShowIn(int delayMs = 0)
    {
        IsHitTestVisible = true;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var begin = TimeSpan.FromMilliseconds(delayMs);
        Root.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(650)) { BeginTime = begin, EasingFunction = ease });
        RootShift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(800)) { BeginTime = begin, EasingFunction = ease });
    }

    /// <summary>Скрыть карточку (финальный экран «Всё ещё впереди!»)</summary>
    public void HideOut()
    {
        IsHitTestVisible = false;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        Root.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(350)) { EasingFunction = ease });
        RootShift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(40, TimeSpan.FromMilliseconds(350)) { EasingFunction = ease });
    }

    // ---------- Наведение ----------
    private void Root_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Zoom(1.02, 200);
        Glow.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
    }

    private void Root_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Zoom(1.0, 250);
        Glow.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)));
    }

    private void Zoom(double to, int ms)
    {
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (Period != null) DetailsRequested?.Invoke(Period);
    }
}
