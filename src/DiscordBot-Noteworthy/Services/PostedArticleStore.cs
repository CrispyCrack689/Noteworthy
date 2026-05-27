using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Noteworthy.Services;

/// <summary>
/// 投稿済み記事の URL をローカルファイルに永続化して重複投稿を防ぐ。
/// </summary>
public sealed class PostedArticleStore
{
    private readonly string _filePath;
    private readonly ILogger<PostedArticleStore> _logger;
    private readonly HashSet<string> _postedUrls;
    private readonly Lock _lock = new();

    public PostedArticleStore(ILogger<PostedArticleStore> logger)
    {
        _logger = logger;
        // Docker では ./data をホストから volume マウントする想定。ローカル実行でも bin/.../data に書き出す
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "posted_articles.json");
        _postedUrls = Load();
    }

    /// <summary>
    /// 指定した URL が投稿済みかどうかを返す。
    /// </summary>
    public bool IsPosted(string url)
    {
        lock (_lock)
        {
            return _postedUrls.Contains(url);
        }
    }

    /// <summary>
    /// URL を投稿済みとして記録し、ファイルに保存する。
    /// </summary>
    public void MarkAsPosted(string url)
    {
        lock (_lock)
        {
            if (_postedUrls.Add(url))
            {
                Save();
                _logger.LogDebug("投稿済みとして記録: {Url}", url);
            }
        }
    }

    private HashSet<string> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var urls = JsonSerializer.Deserialize<HashSet<string>>(json);
                if (urls is not null)
                {
                    _logger.LogInformation("投稿済み記事 {Count} 件をロードしました", urls.Count);
                    return urls;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "投稿済み記事ファイルの読み込みに失敗");
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_postedUrls, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "投稿済み記事ファイルの保存に失敗");
        }
    }
}
