﻿using Bot.Models;
using Bot.Utils;
using MiniTwitch.Irc.Interfaces;

namespace Bot.Modules;

internal class BanCollector : BotModule
{
    private readonly ILogger _logger = ForContext<BanCollector>();
    private readonly List<BanData> _bans = new(1000);
    private readonly SemaphoreSlim _ss = new(1);
    private readonly BackgroundTimer _timer;

    public BanCollector()
    {
        _timer = new(TimeSpan.FromMinutes(1), Commit, PostgresTimerSemaphore);
    }

    private async ValueTask OnUserTimeout(IUserTimeout timeout)
    {
        if (timeout.Duration.TotalSeconds < 2 || !ChannelsById[timeout.Channel.Id].IsLogged)
            return;

        await _ss.WaitAsync();
        _bans.Add(new(timeout.Target.Name,
                      timeout.Target.Id,
                      timeout.Channel.Name,
                      timeout.Channel.Id,
                      (int)timeout.Duration.TotalSeconds,
                      DateTime.Now));

        _ = _ss.Release();
        return;
    }

    private async ValueTask OnUserBan(IUserBan ban)
    {
        if (!ChannelsById[ban.Channel.Id].IsLogged)
            return;

        await _ss.WaitAsync();
        _bans.Add(new(ban.Target.Name,
                      ban.Target.Id,
                      ban.Channel.Name,
                      ban.Channel.Id,
                      -1,
                      DateTime.Now));

        _ = _ss.Release();
        return;
    }

    private async Task Commit()
    {
        if (!this.Enabled || _bans.Count == 0)
            return;

        _logger.Debug("Attempting to insert {BanCount} ban logs", _bans.Count);
        await _ss.WaitAsync();
        try
        {
            int inserted = await Postgres.ExecuteAsync("insert into ban_data values (@Username, @UserId, @Channel, @ChannelId, @Duration, @BanTime)", _bans);
            _bans.Clear();
            _logger.Debug("Inserted {BanCount} ban logs", inserted);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to insert ban logs into table");
        }
        finally
        {
            _ = _ss.Release();
        }
    }

    protected override ValueTask OnModuleEnabled()
    {
        MainClient.OnUserTimeout += OnUserTimeout;
        MainClient.OnUserBan += OnUserBan;
        AnonClient.OnUserTimeout += OnUserTimeout;
        AnonClient.OnUserBan += OnUserBan;
        _timer.Start();
        return default;
    }
    protected override async ValueTask OnModuleDisabled()
    {
        MainClient.OnUserTimeout -= OnUserTimeout;
        MainClient.OnUserBan -= OnUserBan;
        AnonClient.OnUserTimeout -= OnUserTimeout;
        AnonClient.OnUserBan -= OnUserBan;
        await _timer.StopAsync();
    }

    private readonly record struct BanData(string Username, long UserId, string Channel, long ChannelId, int Duration, DateTime BanTime);
}
