using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Noteworthy.Configuration;
using DiscordBot.Noteworthy.Models;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Noteworthy.Services;

/// <summary>
/// Discord フォーラムチャンネルに記事をスレッドとして投稿するサービス。
/// </summary>
public sealed class ForumPosterService
{
    private readonly DiscordSocketClient _client;
    private readonly PostedArticleStore _store;
    private readonly ILogger<ForumPosterService> _logger;

    public ForumPosterService(
        DiscordSocketClient client,
        PostedArticleStore store,
        ILogger<ForumPosterService> logger)
    {
        _client = client;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// 記事をフォーラムチャンネルに新しいスレッドとして投稿する。
    /// </summary>
    public async Task PostArticleAsync(
        Article article,
        ulong forumChannelId,
        TargetSiteConfig? siteConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (_client.GetChannel(forumChannelId) is not IForumChannel channel)
        {
            _logger.LogError("フォーラムチャンネルが見つかりません: {ChannelId}", forumChannelId);
            return;
        }

        // 投稿済みならスキップ
        if (_store.IsPosted(article.Url))
        {
            _logger.LogDebug("既に投稿済みの記事をスキップ: {Title}", article.Title);
            return;
        }

        var embed = BuildEmbed(article);

        // サイト設定からフォーラムタグを解決
        var tags = siteConfig is not null
            ? await ResolveTagsAsync(channel, siteConfig, cancellationToken)
            : null;

        // フォーラムにスレッドを作成（1通目: Embed + 元記事リンク）
        var thread = await channel.CreatePostAsync(
            title: Truncate(article.Title, 100),
            text: article.Url,
            embed: embed,
            tags: tags,
            options: new RequestOptions { CancelToken = cancellationToken });

        // 投稿済みとして記録
        _store.MarkAsPosted(article.Url);
        _logger.LogInformation("記事を投稿しました: {Title}", article.Title);
    }

    /// <summary>
    /// 複数の記事を一括投稿する。
    /// </summary>
    public async Task PostArticlesAsync(
        IEnumerable<Article> articles,
        ulong forumChannelId,
        TargetSiteConfig? siteConfig = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var article in articles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await PostArticleAsync(article, forumChannelId, siteConfig, cancellationToken);

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

    /// <summary>
    /// サイト設定に基づいてフォーラムタグを解決する。
    /// タグが存在しない場合は新規作成する。
    /// </summary>
    private async Task<ForumTag[]> ResolveTagsAsync(
        IForumChannel channel,
        TargetSiteConfig siteConfig,
        CancellationToken cancellationToken)
    {
        var existingTag = channel.Tags.FirstOrDefault(
            t => string.Equals(t.Name, siteConfig.TagName, StringComparison.OrdinalIgnoreCase));

        if (existingTag.Id != 0)
        {
            return [existingTag];
        }

        // タグが存在しないので新規作成を試みる
        _logger.LogInformation("フォーラムタグを作成します: {TagName}", siteConfig.TagName);

        try
        {
            IEmote? emoji = !string.IsNullOrWhiteSpace(siteConfig.TagEmoji)
                ? new Emoji(siteConfig.TagEmoji)
                : null;

            var newTag = new ForumTagBuilder(siteConfig.TagName, emoji: emoji);

            var existingTagBuilders = channel.Tags
                .Select(t => new ForumTagBuilder(t.Name, t.Id, t.IsModerated, t.Emoji));

            var allTagBuilders = existingTagBuilders
                .Append(newTag)
                .Select(b => b.Build())
                .Cast<IForumTag>()
                .ToArray();

            await channel.ModifyAsync(
                (ForumChannelProperties props) => props.Tags = new Optional<IEnumerable<IForumTag>>(allTagBuilders),
                new RequestOptions { CancelToken = cancellationToken });

            // 更新後のチャンネルからタグ ID を取得
            var refreshedChannel = (IForumChannel)await _client.GetChannelAsync(channel.Id);
            var createdTag = refreshedChannel.Tags.FirstOrDefault(
                t => string.Equals(t.Name, siteConfig.TagName, StringComparison.OrdinalIgnoreCase));

            if (createdTag.Id != 0)
            {
                return [createdTag];
            }

            _logger.LogWarning("タグの作成後に取得できませんでした: {TagName}", siteConfig.TagName);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
        {
            _logger.LogWarning(
                "タグ作成の権限がありません。Bot に「チャンネルの管理」権限を付与するか、手動でタグ「{TagName}」を作成してください",
                siteConfig.TagName);
        }

        return [];
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
