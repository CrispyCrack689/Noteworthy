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

        _logger.LogInformation(
            "記事チェックワーカー開始 — 間隔: {Interval}分, 対象: {Url}",
            _config.CheckIntervalMinutes,
            _config.TargetSiteUrl);

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
        try
        {
            _logger.LogInformation("記事チェックを実行中...");

            var feedUrl = await _scraper.DiscoverFeedUrlAsync(_config.TargetSiteUrl, cancellationToken);
            var articles = await _scraper.FetchArticlesFromRssAsync(
                feedUrl,
                maxCount: 5,
                cancellationToken);

            // RSS でサムネイルが取れなかった記事は OGP から取得
            var enrichedArticles = new List<Models.Article>();
            foreach (var article in articles)
            {
                if (string.IsNullOrEmpty(article.ThumbnailUrl))
                {
                    var ogpImage = await _scraper.FetchOgpImageAsync(article.Url, cancellationToken);
                    enrichedArticles.Add(article with { ThumbnailUrl = ogpImage });
                }
                else
                {
                    enrichedArticles.Add(article);
                }
            }

            await _poster.PostArticlesAsync(enrichedArticles, cancellationToken);

            _logger.LogInformation("記事チェック完了");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "記事チェック中にエラーが発生しました");
        }
    }
}
