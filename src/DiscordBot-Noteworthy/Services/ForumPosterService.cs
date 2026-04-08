using Discord;
using Discord.WebSocket;
using DiscordBot.Noteworthy.Configuration;
using DiscordBot.Noteworthy.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Noteworthy.Services;

/// <summary>
/// Discord フォーラムチャンネルに記事をスレッドとして投稿するサービス。
/// </summary>
public sealed class ForumPosterService
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _config;
    private readonly ILogger<ForumPosterService> _logger;

    public ForumPosterService(
        DiscordSocketClient client,
        IOptions<BotConfig> config,
        ILogger<ForumPosterService> logger)
    {
        _client = client;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// 記事をフォーラムチャンネルに新しいスレッドとして投稿する。
    /// </summary>
    public async Task PostArticleAsync(Article article, CancellationToken cancellationToken = default)
    {
        var channel = _client.GetChannel(_config.ForumChannelId) as IForumChannel;
        if (channel is null)
        {
            _logger.LogError("フォーラムチャンネルが見つかりません: {ChannelId}", _config.ForumChannelId);
            return;
        }

        // 同じ URL のスレッドが既に存在するかチェック
        if (await IsAlreadyPostedAsync(channel, article.Url))
        {
            _logger.LogDebug("既に投稿済みの記事をスキップ: {Title}", article.Title);
            return;
        }

        var embed = BuildEmbed(article);

        // フォーラムにスレッドを作成
        await channel.CreatePostAsync(
            title: Truncate(article.Title, 100),
            text: article.Url,
            embed: embed,
            options: new RequestOptions { CancelToken = cancellationToken });

        _logger.LogInformation("記事を投稿しました: {Title}", article.Title);
    }

    /// <summary>
    /// 複数の記事を一括投稿する。
    /// </summary>
    public async Task PostArticlesAsync(IEnumerable<Article> articles, CancellationToken cancellationToken = default)
    {
        foreach (var article in articles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await PostArticleAsync(article, cancellationToken);

            // レートリミット対策
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    private static Embed BuildEmbed(Article article)
    {
        var builder = new EmbedBuilder()
            .WithTitle(Truncate(article.Title, 256))
            .WithUrl(article.Url)
            .WithColor(new Color(0x5865F2)) // Discord Blurple
            .WithTimestamp(article.PublishedAt ?? DateTimeOffset.Now);

        if (!string.IsNullOrWhiteSpace(article.Description))
        {
            // HTML タグを除去して説明を設定
            var plainDescription = System.Text.RegularExpressions.Regex.Replace(
                article.Description, "<[^>]+>", string.Empty);
            builder.WithDescription(Truncate(plainDescription, 4096));
        }

        if (!string.IsNullOrWhiteSpace(article.ThumbnailUrl))
        {
            builder.WithImageUrl(article.ThumbnailUrl);
        }

        if (!string.IsNullOrWhiteSpace(article.Author))
        {
            builder.WithAuthor(article.Author);
        }

        builder.WithFooter("Noteworthy Bot");

        return builder.Build();
    }

    private static async Task<bool> IsAlreadyPostedAsync(IForumChannel channel, string articleUrl)
    {
        var threads = await channel.GetActiveThreadsAsync();
        foreach (var thread in threads)
        {
            var messages = await thread.GetMessagesAsync(1).FlattenAsync();
            var firstMessage = messages.FirstOrDefault();
            if (firstMessage?.Content?.Contains(articleUrl, StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }
}
