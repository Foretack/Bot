﻿using System.Text.Json;
using Bot.Models;
using Bot.Utils;
using CachingFramework.Redis.Contracts.RedisObjects;

namespace Bot.Modules;

public class WarframeAlerts: BotModule
{
    private static IRedisSet<string> Ids => Collections.GetRedisSet<string>("warframe:data:worldstate_ids");
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly BackgroundTimer _timer;

    public WarframeAlerts()
    {
        _timer = new(TimeSpan.FromMinutes(5), CheckWorldstate, PostgresQueryLock);
    }

    private async Task CheckWorldstate()
    {
        var alertReq = await GetFromRequest<Alert[]>("https://api.warframestat.us/pc/alerts?language=en", _options);
        var invasionReq = await GetFromRequest<Invasion[]>("https://api.warframestat.us/pc/invasions?language=en", _options);
        if (!alertReq.IsT0)
        {
            goto CheckInvasions;
        }

        foreach (Alert alert in alertReq.AsT0)
        {
            if (!alert.Active || !await HandleId(alert.Id))
            {
                continue;
            }

            string missionName = alert.Mission.NodeKey;
            int minLevel = alert.Mission.MinEnemyLevel;
            int maxLevel = alert.Mission.MaxEnemyLevel;
            string rewardStr = alert.Mission.Reward.AsString;
            string lower = rewardStr.ToLower();
            if (lower.Contains("forma")
                || lower.Contains("orokin catalyst")
                || lower.Contains("orokin reactor"))
            {
                await MainClient.SendMessage("pajlada", $"pajaDink 🚨 {rewardStr} alert on {missionName} ({minLevel}-{maxLevel})");
            }
        }

    CheckInvasions:
        foreach (Invasion invasion in invasionReq.AsT0)
        {
            if (invasion.Completed || !await HandleId(invasion.Id))
            {
                continue;
            }

            
            string rewardStr = $"[{invasion.Attacker.Reward?.AsString}] vs [{invasion.Defender.Reward?.AsString}]";
            string lower = rewardStr.ToLower();
            if (lower.Contains("forma")
                || lower.Contains("orokin catalyst")
                || lower.Contains("orokin reactor"))
            {
                await MainClient.SendMessage("pajlada", $"pajaDink 🚨 {rewardStr} invasion on {invasion.NodeKey}");
            }
        }
    }

    private static async Task<bool> HandleId(string id)
    {
        if (await Ids.ContainsAsync(id))
        {
            return false;
        }

        await Ids.AddAsync(id);
        return true;
    }

    protected override ValueTask OnModuleEnabled()
    {
        _timer.Start();
        return default;
    }

    protected override async ValueTask OnModuleDisabled()
    {
        await _timer.StopAsync();
    }
}
