using Discord;
using Discord.WebSocket;
using DiscordBot.Noteworthy.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Noteworthy.Services;

/// <summary>
/// Discord クライアントのライフサイクルを管理するホステッドサービス。
/// </summary>
public sealed class DiscordBotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _config;
    private readonly ILogger<DiscordBotService> _logger;

    public DiscordBotService(
        DiscordSocketClient client,
        IOptions<BotConfig> config,
        ILogger<DiscordBotService> logger)
    {
        _client = client;
        _config = config.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += LogAsync;
        _client.Ready += OnReadyAsync;

        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();

        _logger.LogInformation("Discord Bot を起動しました");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Discord Bot を停止中...");
        await _client.StopAsync();
    }

    private Task OnReadyAsync()
    {
        _logger.LogInformation("Bot がログインしました: {BotName}#{Discriminator}",
            _client.CurrentUser.Username,
            _client.CurrentUser.Discriminator);
        return Task.CompletedTask;
    }

    private Task LogAsync(LogMessage message)
    {
        var logLevel = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information,
        };

        _logger.Log(logLevel, message.Exception, "[Discord] {Message}", message.Message);
        return Task.CompletedTask;
    }
}
