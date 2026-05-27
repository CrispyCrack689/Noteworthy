using Discord;
using Discord.WebSocket;
using DiscordBot.Noteworthy.Configuration;
using DiscordBot.Noteworthy.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
    options.ColorBehavior = LoggerColorBehavior.Enabled;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    // ローカル開発時のみ存在 (Docker では Bot__Token 環境変数を使う)
    .AddJsonFile("appsettings.Secret.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Services.Configure<BotConfig>(builder.Configuration.GetSection(BotConfig.SectionName));

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages,
    LogLevel = LogSeverity.Info,
}));

builder.Services.AddHttpClient<ArticleScraperService>();

builder.Services.AddSingleton<PostedArticleStore>();
builder.Services.AddSingleton<ForumPosterService>();
builder.Services.AddHostedService<DiscordBotService>();
builder.Services.AddHostedService<ArticleCheckWorker>();

var host = builder.Build();
await host.RunAsync();
