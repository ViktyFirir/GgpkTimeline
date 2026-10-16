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
    public string Image { get; set; } = "";         // путь к картинке (для будущей страницы)
}
