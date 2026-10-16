using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GgpkTimeline.Controls;

/// <summary>
/// Год из четырёх «барабанов», как в игровом автомате.
/// При смене года все цифры прокручиваются, останавливаясь слева направо.
/// </summary>
public sealed class YearReel : Canvas
{
    private const int Digits = 4;
    private const int Copies = 5;       // длина ленты: 5 раз по 0123456789
    private const int HomeRow = 20;     // «домашняя» строка (третья копия)

    private const int ExtraRows = 0;        // доп. строки сверх нужной цифры (0 = как одометр, 10 = полный оборот)
    private const int DurationMs = 2200;    // время прокрутки одной цифры
    private const int StaggerMs = 200;      // задержка между цифрами

    private readonly double _lineH;
    private readonly TranslateTransform[] _shift = new TranslateTransform[Digits];
    private readonly int[] _row = new int[Digits];

    private bool _busy;
    private string? _pendingYear;
    private bool _pendingForward;

    public string CurrentYear { get; private set; } = "0000";

    public YearReel(FontFamily font, double fontSize, Brush brush)
    {
        Background = Brushes.Transparent;   // чтобы работало наведение
        ClipToBounds = false;

        // ширина колонки = самая широкая цифра, высота = высота строки
        double maxW = 0, h = 0;
        for (int d = 0; d <= 9; d++)
        {
            var probe = new TextBlock
            {
                Text = d.ToString(), FontFamily = font, FontWeight = FontWeights.ExtraBold, FontSize = fontSize
            };
            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            maxW = Math.Max(maxW, probe.DesiredSize.Width);
            h = probe.DesiredSize.Height;
        }
        double colW = Math.Ceiling(maxW) + 8;
        _lineH = Math.Ceiling(h);

        Width = colW * Digits;
        Height = _lineH;

        for (int c = 0; c < Digits; c++)
        {
            var column = new Canvas { Width = colW, Height = _lineH, ClipToBounds = true };

            var strip = new StackPanel { Width = colW };
            for (int k = 0; k < 10 * Copies; k++)
            {
                strip.Children.Add(new TextBlock
                {
                    Text = (k % 10).ToString(),
                    Width = colW,
                    Height = _lineH,
                    TextAlignment = TextAlignment.Center,
                    FontFamily = font,
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = fontSize,
                    Foreground = brush
                });
            }
            _shift[c] = new TranslateTransform();
            strip.RenderTransform = _shift[c];

            column.Children.Add(strip);
            SetLeft(column, c * colW);
            SetTop(column, 0);
            Children.Add(column);
        }

        SetInstant("0000");
    }

    /// <summary>Поставить год сразу, без прокрутки</summary>
    public void SetInstant(string year)
    {
        year = Normalize(year);
        CurrentYear = year;
        for (int c = 0; c < Digits; c++)
        {
            _row[c] = HomeRow + (year[c] - '0');
            _shift[c].BeginAnimation(TranslateTransform.YProperty, null);
            _shift[c].Y = -_row[c] * _lineH;
        }
    }

    /// <summary>Прокрутить барабаны к новому году. forward = вперёд по времени (цифры едут вверх).</summary>
    public void RollTo(string year, bool forward)
    {
        year = Normalize(year);
        if (_busy)
        {
            _pendingYear = year;            // запоминаем только последнее пожелание
            _pendingForward = forward;
            return;
        }
        if (year == CurrentYear) return;

        _busy = true;
        CurrentYear = year;
        int finished = 0;

        for (int c = 0; c < Digits; c++)
        {
            int col = c;
            int from = _row[col] % 10;
            int to = year[col] - '0';

            // разница до нужной цифры + (необязательно) лишние обороты
            int travel = forward ? ((to - from + 10) % 10) + ExtraRows
                                 : -(((from - to + 10) % 10) + ExtraRows);
            int targetRow = _row[col] + travel;

            var anim = new DoubleAnimation(-targetRow * _lineH, TimeSpan.FromMilliseconds(DurationMs))
            {
                BeginTime = TimeSpan.FromMilliseconds(col * StaggerMs), // остановка по очереди
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }   // плавный старт и остановка
            };
            anim.Completed += (_, _) =>
            {
                // возвращаем ленту в «дом» (цифра та же - скачка не видно)
                _row[col] = HomeRow + to;
                _shift[col].BeginAnimation(TranslateTransform.YProperty, null);
                _shift[col].Y = -_row[col] * _lineH;

                if (++finished == Digits)
                {
                    _busy = false;
                    if (_pendingYear != null)
                    {
                        var next = _pendingYear;
                        _pendingYear = null;
                        RollTo(next, _pendingForward);
                    }
                }
            };
            _shift[col].BeginAnimation(TranslateTransform.YProperty, anim);
        }
    }

    private static string Normalize(string year)
    {
        year = new string(year.Where(char.IsDigit).ToArray());
        return year.PadLeft(Digits, '0')[^Digits..];
    }
}
