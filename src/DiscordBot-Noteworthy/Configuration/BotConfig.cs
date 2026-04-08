namespace DiscordBot.Noteworthy.Configuration;

/// <summary>
/// Bot の設定を保持するクラス。
/// </summary>
public sealed class BotConfig
{
    public const string SectionName = "Bot";

    /// <summary>Discord Bot トークン。</summary>
    public required string Token { get; init; }

    /// <summary>投稿先のフォーラムチャンネル ID。</summary>
    public required ulong ForumChannelId { get; init; }

    /// <summary>記事を取得する対象サイトの URL。</summary>
    public required string TargetSiteUrl { get; init; }

    /// <summary>記事チェックの間隔（分）。</summary>
    public int CheckIntervalMinutes { get; init; } = 30;
}
