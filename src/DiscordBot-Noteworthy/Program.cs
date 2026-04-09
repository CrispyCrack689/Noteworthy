using Discord;
using Discord.WebSocket;
using DiscordBot.Noteworthy.Configuration;
using DiscordBot.Noteworthy.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var builder = Host.CreateApplicationBuilder(args);

// コンソールログの色付き表示を有効化
// Warning → 黄色、Error/Critical → 赤色
builder.Logging.AddSimpleConsole(options =>
{
    options.ColorBehavior = LoggerColorBehavior.Enabled;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

// 実行ファイルのディレクトリを基準にする（dotnet run / exe 両対応）
var baseDir = AppContext.BaseDirectory;
builder.Configuration
    .SetBasePath(baseDir)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Secret.json", optional: false, reloadOnChange: false);

// 設定のバインド
builder.Services.Configure<BotConfig>(builder.Configuration.GetSection(BotConfig.SectionName));

// Discord クライアント（シングルトン）
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages,
    LogLevel = LogSeverity.Info,
}));

// HTTP クライアント
builder.Services.AddHttpClient<ArticleScraperService>();

// サービス登録
builder.Services.AddSingleton<PostedArticleStore>();
builder.Services.AddSingleton<ForumPosterService>();
builder.Services.AddHostedService<DiscordBotService>();
builder.Services.AddHostedService<ArticleCheckWorker>();

var host = builder.Build();
await host.RunAsync();
