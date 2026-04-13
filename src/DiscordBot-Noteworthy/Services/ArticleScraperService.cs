using System.ServiceModel.Syndication;
using System.Xml;
using DiscordBot.Noteworthy.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Noteworthy.Services;

/// <summary>
/// 対象サイトから記事を取得するサービス。
/// RSS フィードから記事情報を抽出する。
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
    /// RSS 2.0 / Atom に加え、RDF 1.0 (RSS 1.0) フォーマットにも対応する。
    /// </summary>
    public async Task<IReadOnlyList<Article>> FetchArticlesFromRssAsync(
        string feedUrl,
        int maxCount = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("RSS フィードを取得中: {FeedUrl}", feedUrl);

        var xml = await _httpClient.GetStringAsync(feedUrl, cancellationToken);

        // RDF 1.0 (RSS 1.0) かどうかを判定
        if (xml.Contains("http://www.w3.org/1999/02/22-rdf-syntax-ns#", StringComparison.Ordinal))
        {
            return ParseRdfFeed(xml, maxCount);
        }

        using var stringReader = new System.IO.StringReader(xml);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            MaxCharactersFromEntities = 1024,
        };
        using var reader = XmlReader.Create(stringReader, settings);
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
    /// RDF 1.0 (RSS 1.0) フォーマットのフィードをパースする。
    /// </summary>
    private IReadOnlyList<Article> ParseRdfFeed(string xml, int maxCount)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var nsManager = new XmlNamespaceManager(doc.NameTable);
        nsManager.AddNamespace("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
        nsManager.AddNamespace("rss", "http://purl.org/rss/1.0/");
        nsManager.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");

        var items = doc.SelectNodes("//rss:item", nsManager);
        var articles = new List<Article>();

        if (items is null)
        {
            _logger.LogWarning("RDF フィードに記事が見つかりませんでした");
            return articles;
        }

        foreach (XmlNode item in items.Cast<XmlNode>().Take(maxCount))
        {
            var title = item.SelectSingleNode("rss:title", nsManager)?.InnerText ?? "No Title";
            var link = item.SelectSingleNode("rss:link", nsManager)?.InnerText ?? string.Empty;
            var description = item.SelectSingleNode("rss:description", nsManager)?.InnerText;
            var author = item.SelectSingleNode("dc:creator", nsManager)?.InnerText;
            var dateStr = item.SelectSingleNode("dc:date", nsManager)?.InnerText;

            if (string.IsNullOrEmpty(link))
            {
                continue;
            }

            DateTimeOffset? publishedAt = null;
            if (DateTimeOffset.TryParse(dateStr, out var parsed))
            {
                publishedAt = parsed;
            }

            articles.Add(new Article
            {
                Title = title,
                Url = link,
                Description = description,
                Author = author,
                PublishedAt = publishedAt,
            });
        }

        _logger.LogInformation("{Count} 件の記事を RDF フィードから取得しました", articles.Count);
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
