namespace DiscordBot.Noteworthy.Configuration;

/// <summary>
/// 記事取得対象サイトの設定。
/// </summary>
public sealed class TargetSiteConfig
{
    /// <summary>サイトの URL。</summary>
    public required string Url { get; init; }

    /// <summary>フォーラムタグの名前。</summary>
    public required string TagName { get; init; }

    /// <summary>フォーラムタグの絵文字（省略可）。</summary>
    public string? TagEmoji { get; init; }
}
