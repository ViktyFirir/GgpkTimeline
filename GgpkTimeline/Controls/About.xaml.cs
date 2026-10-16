using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace GgpkTimeline.Controls
{
    public partial class About : UserControl
    {
        // Страница просит главное окно закрыть её
        public event Action? CloseRequested;

        public About()
        {
            InitializeComponent();
        }

        // Запускает анимацию появления (вызывается при каждом открытии страницы)
        public void Play()
        {
            var sb = (Storyboard)Resources["IntroAnimation"];
            sb.Begin(this, true);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke();
        }

        // Кнопка "О программе" — пока заглушка
        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: действие появится позже
        }
    }
}