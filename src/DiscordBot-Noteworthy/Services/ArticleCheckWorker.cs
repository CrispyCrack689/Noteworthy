using DiscordBot.Noteworthy.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Noteworthy.Services;

/// <summary>
/// 定期的に記事をチェックしてフォーラムに投稿するバックグラウンドワーカー。
/// </summary>
public sealed class ArticleCheckWorker : BackgroundService
{
    private readonly ArticleScraperService _scraper;
    private readonly ForumPosterService _poster;
    private readonly BotConfig _config;
    private readonly ILogger<ArticleCheckWorker> _logger;

    public ArticleCheckWorker(
        ArticleScraperService scraper,
        ForumPosterService poster,
        IOptions<BotConfig> config,
        ILogger<ArticleCheckWorker> logger)
    {
        _scraper = scraper;
        _poster = poster;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Discord クライアントの準備完了を待つ
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        var totalSites = _config.ForumChannels.Sum(fc => fc.TargetSites.Count);
        _logger.LogInformation(
            "記事チェックワーカー開始 — 間隔: {Interval}分, フォーラム数: {ForumCount}, 対象サイト数: {SiteCount}",
            _config.CheckIntervalMinutes,
            _config.ForumChannels.Count,
            totalSites);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_config.CheckIntervalMinutes));

        // 初回は即座に実行
        await CheckAndPostAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckAndPostAsync(stoppingToken);
        }
    }

    private async Task CheckAndPostAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("記事チェックを実行中...");

        foreach (var forumChannel in _config.ForumChannels)
        {
            foreach (var site in forumChannel.TargetSites)
            {
                try
                {
                    _logger.LogInformation(
                        "サイトをチェック中: {Url} (タグ: {Tag}, チャンネル: {ChannelId})",
                        site.Url, site.TagName, forumChannel.ForumChannelId);

                    var feedUrl = await _scraper.DiscoverFeedUrlAsync(site.Url, cancellationToken);
                    var articles = await _scraper.FetchArticlesFromRssAsync(
                        feedUrl,
                        maxCount: 5,
                        cancellationToken);

                    await _poster.PostArticlesAsync(
                        articles, forumChannel.ForumChannelId, site, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "サイト {Url} の記事チェック中にエラーが発生しました", site.Url);
                }
            }
        }

        _logger.LogInformation("記事チェック完了");
    }
}
