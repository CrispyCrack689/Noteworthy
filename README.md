# DiscordBot-Noteworthy

C# / .NET 10 で構築された Discord Bot。Microsoft.Extensions.Hosting + Discord.Net で実装。

機能:

- 複数サイトの RSS フィードを定期的にチェックし、新着記事を Discord フォーラムチャンネルに投稿
- フォーラムチャンネルごとに対象サイト・タグを設定可能
- 投稿済み記事は `data/posted_articles.json` に記録して重複投稿を防止

## ローカル開発

```bash
dotnet build
dotnet run --project src/DiscordBot-Noteworthy
```

- `src/DiscordBot-Noteworthy/appsettings.json` でフォーラムチャンネルID・対象サイト・チェック間隔を設定
- `src/DiscordBot-Noteworthy/appsettings.Secret.json` を作成して `Bot.Token` に Bot Token を入れる (gitignore 済み)

`appsettings.Secret.json` の例:

```json
{
  "Bot": {
    "Token": "your-discord-bot-token-here"
  }
}
```

## Ubuntu サーバーへの Docker デプロイ

### 前提

- Docker Engine と docker compose plugin がインストール済み

### 初回セットアップ

```bash
# 1. リポジトリをサーバーに配置
git clone <repo-url> /opt/discordbot-noteworthy
cd /opt/discordbot-noteworthy

# 2. 環境変数ファイルを作成して Bot Token を設定
cp .env.example .env
nano .env   # BOT_TOKEN=<実トークン> を記入

# 3. 設定ファイルを編集 (フォーラムチャンネルID・対象サイトなど)
nano src/DiscordBot-Noteworthy/appsettings.json

# 4. ビルドして起動
docker compose up -d --build
```

### ログを確認

```bash
docker compose logs -f
```

### 停止 / 再起動

```bash
docker compose stop      # 停止
docker compose start     # 再開
docker compose restart   # 再起動
docker compose down      # コンテナ削除 (volume は保持)
```

### コード更新時

```bash
git pull
docker compose up -d --build
```

### 設定の更新時

`appsettings.json` はイメージに焼き込まれているため、編集後は `docker compose up -d --build` で再ビルドが必要。

`data/posted_articles.json` は volume mount (`./data:/app/data`) のためコンテナ再ビルド後も保持される。

## アーキテクチャ概要

`Microsoft.Extensions.Hosting` (Generic Host) ベース。各機能を `IHostedService` / `BackgroundService` として登録。

| サービス | 役割 |
|---------|------|
| [DiscordBotService](src/DiscordBot-Noteworthy/Services/DiscordBotService.cs) | Discord クライアントの Login/Start/Stop |
| [ArticleCheckWorker](src/DiscordBot-Noteworthy/Services/ArticleCheckWorker.cs) | 定期的に RSS をチェックして新着記事を投稿 (BackgroundService) |
| [ArticleScraperService](src/DiscordBot-Noteworthy/Services/ArticleScraperService.cs) | RSS フィード探索 / 記事取得 / OGP 画像フォールバック |
| [ForumPosterService](src/DiscordBot-Noteworthy/Services/ForumPosterService.cs) | フォーラムチャンネルへのスレッド作成・投稿 |
| [PostedArticleStore](src/DiscordBot-Noteworthy/Services/PostedArticleStore.cs) | `data/posted_articles.json` の読み書き |

設定は `IOptions<BotConfig>` で注入。詳細は [BotConfig.cs](src/DiscordBot-Noteworthy/Configuration/BotConfig.cs) を参照。

設定の優先順位は **環境変数 (`Bot__Token` 等) > `appsettings.Secret.json` > `appsettings.json`**。Docker デプロイ時は `.env` 経由で `BOT_TOKEN` を環境変数として渡し、`appsettings.Secret.json` はコンテナに含めない。

コーディング規約は [CLAUDE.md](CLAUDE.md) に準拠。
