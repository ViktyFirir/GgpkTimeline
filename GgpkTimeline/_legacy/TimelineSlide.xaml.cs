using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GgpkTimeline;

public partial class TimelineSlide : UserControl
{
    public TimelineSlide(SlideData data)
    {
        InitializeComponent();

        Bg.Source = Assets.Load("PreMain.png");
        YearText.Text = data.Year;
        TitleText.Text = data.Title;
        BodyText.Text = data.Text;

        var photo = Assets.Load(data.Image);
        if (photo != null)
        {
            Photo.Background = new ImageBrush(photo) { Stretch = Stretch.UniformToFill };
            Photo.Visibility = Visibility.Visible;
        }

        PrepareTransform(Card);
        PrepareTransform(YearText);
        PrepareTransform(Photo);
    }

    private static void PrepareTransform(FrameworkElement el)
    {
        el.RenderTransformOrigin = new Point(0.5, 0.5);
        el.RenderTransform = new TransformGroup
        {
            Children = { new ScaleTransform(1, 1), new TranslateTransform() }
        };
    }

    /// <summary>Анимация появления: карточка выезжает слева, год «вырастает», фото всплывает.</summary>
    public void PlayIntro(int delayMs = 0)
    {
        Reveal(Card, -90, 0, 0.97, delayMs);
        Reveal(YearText, 120, 0, 0.88, delayMs + 120);
        Reveal(Photo, 0, 60, 0.95, delayMs + 260);
    }

    private static void Reveal(FrameworkElement el, double dx, double dy, double fromScale, int delayMs)
    {
        var group = (TransformGroup)el.RenderTransform;
        var scale = (ScaleTransform)group.Children[0];
        var move = (TranslateTransform)group.Children[1];

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var begin = TimeSpan.FromMilliseconds(delayMs);
        var dur = TimeSpan.FromMilliseconds(800);

        el.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur) { BeginTime = begin, EasingFunction = ease });
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dx, 0, dur) { BeginTime = begin, EasingFunction = ease });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(dy, 0, dur) { BeginTime = begin, EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fromScale, 1, dur) { BeginTime = begin, EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(fromScale, 1, dur) { BeginTime = begin, EasingFunction = ease });
    }

    private void MoreBtn_Click(object sender, RoutedEventArgs e)
    {
        // TODO: заглушка. Здесь позже откроем подробную страницу выбранного года.
    }
}
