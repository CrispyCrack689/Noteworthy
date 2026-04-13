namespace DiscordBot.Noteworthy.Configuration;

/// <summary>
/// フォーラムチャンネルごとの設定。
/// </summary>
public sealed class ForumChannelConfig
{
    /// <summary>投稿先のフォーラムチャンネル ID。</summary>
    public required ulong ForumChannelId { get; init; }

    /// <summary>記事を取得する対象サイトのリスト。</summary>
    public required List<TargetSiteConfig> TargetSites { get; init; }
}
