using System.IO;
using System.Windows.Media.Imaging;

namespace GgpkTimeline;

/// <summary>Загрузка картинок: сначала с диска (рядом с exe), потом из ресурсов Assets.</summary>
public static class Assets
{
    private static readonly Dictionary<string, BitmapImage?> Cache = new();

    public static BitmapImage? Load(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (Cache.TryGetValue(name, out var cached)) return cached;

        var img = TryFile(name) ?? TryResource(name);
        Cache[name] = img;
        return img;
    }

    private static BitmapImage? TryFile(string name)
    {
        foreach (var rel in new[] { name, Path.Combine("Assets", name), Path.Combine("Images", name) })
        {
            var full = Path.Combine(AppContext.BaseDirectory, rel);
            if (File.Exists(full)) return Create(new Uri(full));
        }
        return null;
    }

    private static BitmapImage? TryResource(string name)
    {
        try { return Create(new Uri($"pack://application:,,,/Assets/{name}")); }
        catch { return null; }
    }

    private static BitmapImage Create(Uri uri)
    {
        var b = new BitmapImage();
        b.BeginInit();
        b.UriSource = uri;
        b.CacheOption = BitmapCacheOption.OnLoad;
        b.EndInit();
        b.Freeze();
        return b;
    }
}