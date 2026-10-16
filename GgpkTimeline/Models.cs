namespace GgpkTimeline;
 
/// <summary>Корень файла Data/timeline.json</summary>
public class TimelineData
{
    public string FutureLabel { get; set; } = "всё ещё впереди!";
    public string FutureTitle { get; set; } = "Всё ещё впереди!";
    public List<Period> Periods { get; set; } = new();
}
 
/// <summary>Одна эпоха (десятилетие) на таймлайне</summary>
public class Period
{
    public string Year { get; set; } = "";          // большая надпись и подпись у кружка
    public string FooterLabel { get; set; } = "";   // надпись в оранжевом блоке футера
    public string Title { get; set; } = "";         // заголовок карточки
    public string Text { get; set; } = "";          // короткий текст карточки
    public List<string> Details { get; set; } = new(); // подробности (для будущей страницы)
    public List<PeriodImage> Images { get; set; } = new(); // картинки периода с описаниями (для будущей страницы)
}
 
/// <summary>Картинка периода: файл + заголовок + описание (подпись)</summary>
public class PeriodImage
{
    public string File { get; set; } = "";          // путь к файлу относительно папки с exe, например Images/Years/1956.png
    public string Title { get; set; } = "";         // короткий заголовок картинки
    public string Description { get; set; } = "";   // описание / подпись под картинкой
}