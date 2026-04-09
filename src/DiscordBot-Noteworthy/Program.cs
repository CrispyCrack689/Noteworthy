using Discord;
using Discord.WebSocket;
using DiscordBot.Noteworthy.Configuration;
using DiscordBot.Noteworthy.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// トークンを別ファイルから読み込み（git 管理外）
builder.Configuration.AddJsonFile("appsettings.Secret.json", optional: false, reloadOnChange: false);

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
builder.Services.AddSingleton<ForumPosterService>();
builder.Services.AddHostedService<DiscordBotService>();
builder.Services.AddHostedService<ArticleCheckWorker>();

var host = builder.Build();
await host.RunAsync();
