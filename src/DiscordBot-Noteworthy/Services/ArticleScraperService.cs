using System.ServiceModel.Syndication;
using System.Xml;
using DiscordBot.Noteworthy.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Noteworthy.Services;

/// <summary>
/// 対象サイトから記事を取得するサービス。
/// OGP メタタグまたは RSS フィードから記事情報を抽出する。
/// </summary>
public sealed class ArticleScraperService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArticleScraperService> _logger;

    public ArticleScraperService(HttpClient httpClient, ILogger<ArticleScraperService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// RSS フィードから最新記事の一覧を取得する。
    /// </summary>
    public async Task<IReadOnlyList<Article>> FetchArticlesFromRssAsync(
        string feedUrl,
        int maxCount = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("RSS フィードを取得中: {FeedUrl}", feedUrl);

        using var stream = await _httpClient.GetStreamAsync(feedUrl, cancellationToken);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            MaxCharactersFromEntities = 1024,
        };
        using var reader = XmlReader.Create(stream, settings);
        var feed = SyndicationFeed.Load(reader);

        var articles = new List<Article>();

        foreach (var item in feed.Items.Take(maxCount))
        {
            var link = item.Links.FirstOrDefault()?.Uri?.AbsoluteUri ?? string.Empty;
            if (string.IsNullOrEmpty(link))
            {
                continue;
            }

            var article = new Article
            {
                Title = item.Title?.Text ?? "No Title",
                Url = link,
                Description = item.Summary?.Text,
                Author = item.Authors.FirstOrDefault()?.Name,
                PublishedAt = item.PublishDate,
                ThumbnailUrl = ExtractImageFromContent(item),
            };

            articles.Add(article);
        }

        _logger.LogInformation("{Count} 件の記事を取得しました", articles.Count);
        return articles;
    }

    /// <summary>
    /// サイト URL から RSS フィードの URL を自動検出する。
    /// HTML 内の &lt;link rel="alternate" type="application/rss+xml"&gt; を探す。
    /// 見つからない場合は /feed/ を試す。
    /// </summary>
    public async Task<string> DiscoverFeedUrlAsync(string siteUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(siteUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var feedLink = doc.DocumentNode.SelectSingleNode(
                "//link[@rel='alternate' and (@type='application/rss+xml' or @type='application/atom+xml')]");

            var href = feedLink?.GetAttributeValue("href", null!);
            if (!string.IsNullOrEmpty(href))
            {
                if (!Uri.IsWellFormedUriString(href, UriKind.Absolute))
                {
                    href = new Uri(new Uri(siteUrl), href).AbsoluteUri;
                }

                _logger.LogInformation("RSS フィードを検出しました: {FeedUrl}", href);
                return href;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RSS フィード検出中にエラー。フォールバックを試みます");
        }

        // フォールバック: よくあるパスを試す
        var baseUri = new Uri(siteUrl.TrimEnd('/') + "/");
        var fallbackUrl = new Uri(baseUri, "feed/").AbsoluteUri;
        _logger.LogInformation("フォールバック RSS URL を使用: {FeedUrl}", fallbackUrl);
        return fallbackUrl;
    }

    /// <summary>
    /// ページの OGP メタタグからサムネイル画像を取得する。
    /// RSS でサムネイルが取れなかった場合のフォールバック用。
    /// </summary>
    public async Task<string?> FetchOgpImageAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(url, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var ogImage = doc.DocumentNode
                .SelectSingleNode("//meta[@property='og:image']")?
                .GetAttributeValue("content", null!);

            return ogImage;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OGP 画像の取得に失敗: {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// HTML ページから直接記事一覧をスクレイピングする。
    /// サイト構造に合わせて XPath を変更すること。
    /// </summary>
    public async Task<IReadOnlyList<Article>> FetchArticlesFromHtmlAsync(
        string pageUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("HTML ページをスクレイピング中: {PageUrl}", pageUrl);

        var html = await _httpClient.GetStringAsync(pageUrl, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var articles = new List<Article>();

        // サイト構造に合わせてセレクタを調整してください
        // 以下は一般的な記事リストの例です
        var articleNodes = doc.DocumentNode.SelectNodes("//article | //div[contains(@class,'post')] | //div[contains(@class,'entry')]");

        if (articleNodes is null)
        {
            _logger.LogWarning("記事要素が見つかりませんでした");
            return articles;
        }

        foreach (var node in articleNodes)
        {
            var titleNode = node.SelectSingleNode(".//h2/a | .//h3/a | .//a[contains(@class,'title')]");
            var imgNode = node.SelectSingleNode(".//img");

            if (titleNode is null)
            {
                continue;
            }

            var href = titleNode.GetAttributeValue("href", string.Empty);
            if (!Uri.IsWellFormedUriString(href, UriKind.Absolute))
            {
                href = new Uri(new Uri(pageUrl), href).AbsoluteUri;
            }

            var thumbnailUrl = imgNode?.GetAttributeValue("src", null!)
                ?? imgNode?.GetAttributeValue("data-src", null!);

            if (thumbnailUrl is not null && !Uri.IsWellFormedUriString(thumbnailUrl, UriKind.Absolute))
            {
                thumbnailUrl = new Uri(new Uri(pageUrl), thumbnailUrl).AbsoluteUri;
            }

            var article = new Article
            {
                Title = HtmlEntity.DeEntitize(titleNode.InnerText.Trim()),
                Url = href,
                ThumbnailUrl = thumbnailUrl,
            };

            articles.Add(article);
        }

        _logger.LogInformation("{Count} 件の記事を HTML から取得しました", articles.Count);
        return articles;
    }

    private static string? ExtractImageFromContent(SyndicationItem item)
    {
        // media:thumbnail や media:content から取得
        foreach (var ext in item.ElementExtensions)
        {
            if (ext.OuterName is "thumbnail" or "content"
                && ext.OuterNamespace == "http://search.yahoo.com/mrss/")
            {
                var element = ext.GetObject<XmlElement>();
                var url = element.GetAttribute("url");
                if (!string.IsNullOrEmpty(url))
                {
                    return url;
                }
            }
        }

        // enclosure から取得
        foreach (var link in item.Links)
        {
            if (link.RelationshipType == "enclosure"
                && link.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return link.Uri?.AbsoluteUri;
            }
        }

        // コンテンツ内の img タグから取得
        var content = (item.Content as TextSyndicationContent)?.Text ?? item.Summary?.Text;
        if (content is not null)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            var img = doc.DocumentNode.SelectSingleNode("//img");
            return img?.GetAttributeValue("src", null!);
        }

        return null;
    }
}
