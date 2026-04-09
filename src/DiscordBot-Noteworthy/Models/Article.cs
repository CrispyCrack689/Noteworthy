namespace DiscordBot.Noteworthy.Models;

/// <summary>
/// スクレイピングで取得した記事情報。
/// </summary>
public sealed record Article
{
    /// <summary>記事タイトル。</summary>
    public required string Title { get; init; }

    /// <summary>記事の URL。</summary>
    public required string Url { get; init; }

    /// <summary>記事の説明・概要。</summary>
    public string? Description { get; init; }

    /// <summary>サムネイル画像の URL。</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>記事本文。</summary>
    public string? Body { get; init; }

    /// <summary>著者名。</summary>
    public string? Author { get; init; }

    /// <summary>公開日時。</summary>
    public DateTimeOffset? PublishedAt { get; init; }
}
