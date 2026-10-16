using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GgpkTimeline
{
    public partial class StartWindow : Window
    {
        public StartWindow()
        {
            InitializeComponent();
            Loaded += StartWindow_Loaded;
        }

        // ===== Анимация появления (по очереди) =====
        private void StartWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Appear(LogoHost, delayMs: 250, fromY: -50);
            Appear(Subtitle, delayMs: 450, fromY: 40);
            Appear(TitleText, delayMs: 650, fromY: 60);
            Appear(StartButton, delayMs: 950, fromY: 50);
            Appear(AboutButton, delayMs: 1100, fromY: 50);
        }

        private static void Appear(FrameworkElement element, int delayMs, double fromY)
        {
            var delay = TimeSpan.FromMilliseconds(delayMs);
            var duration = new Duration(TimeSpan.FromMilliseconds(700));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var translate = new TranslateTransform(0, fromY);
            element.RenderTransform = translate;
            element.Opacity = 0;

            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, duration) { BeginTime = delay, EasingFunction = ease });

            translate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(fromY, 0, duration) { BeginTime = delay, EasingFunction = ease });
        }

        // ===== «Начать» — открыть главное окно проекта =====
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Если главное окно у вас называется не MainWindow — замените имя здесь
            var main = new MainWindow();
            Application.Current.MainWindow = main;
            main.Show();
            Close();
        }

        // ===== «О проекте» — заглушка =====
        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: открыть экран «О проекте»
            MessageBox.Show("Раздел «О проекте» пока в разработке", "Политех через года");
        }

        // ===== F11 — полный экран, Esc — выйти из полного экрана =====
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                if (WindowStyle == WindowStyle.None)
                {
                    WindowStyle = WindowStyle.SingleBorderWindow;
                    WindowState = WindowState.Normal;
                }
                else
                {
                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Maximized;
                }
            }
            else if (e.Key == Key.Escape && WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
            }
        }
    }
}
