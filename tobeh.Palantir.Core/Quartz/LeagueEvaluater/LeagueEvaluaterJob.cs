using DSharpPlus;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Commands.Commands;
using tobeh.Palantir.Core.Quartz.DropScheduler;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Core.Quartz.LeagueEvaluater;

public class LeagueEvaluaterJob(
    ILogger<DropSchedulerJob> logger,
    Leagues.LeaguesClient leaguesClient,
    Splits.SplitsClient splitsClient,
    Members.MembersClient membersClient,
    Guilds.GuildsClient guildsClient
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        /* fetch results for passed league */
        var now = DateTime.UtcNow;
        var yesterday = now.AddDays(-1);
        var eval = await leaguesClient.EvaluateLeagueSeasonSplitsAsync(new EvaluateSeasonRequest
        {
            Year = yesterday.Year,
            Month = yesterday.Month
        });

        /* create splits */
        var split = await splitsClient.CreateSplitRewardAsync(new()
        {
            Name = "League Season " + yesterday.ToString("MMMM yyyy"),
            Description = "You have been ranked in the leaderboard of that season.",
            Value = 0
        });

        /* create rewards */
        foreach (var result in eval.Evaluation)
        {
            var member = await membersClient.GetMemberByDiscordIdAsync(new() { Id = result.UserId });
            await splitsClient.RewardSplitAsync(new()
            {
                RewardeeLogin = member.Login,
                Comment = result.Comment,
                SplitId = split.Id,
                ValueOverride = Convert.ToInt32(result.Splits)
            });
        }

        /* send result message */
        var homeServers = await guildsClient.GetValidGuilds(new Empty()).ToListAsync();

        var client = new DiscordWebhookClient();
        foreach (var homeServer in homeServers)
        {
            var options = await guildsClient.GetGuildOptionsByIdAsync(new GetGuildOptionsByIdMessage
                { GuildId = homeServer.GuildId });
            if (options.AnnouncementsWebhook is { } webhookUrl)
            {
                var guildWebhook = await client.AddWebhookAsync(new Uri(webhookUrl));
                var embed = LeagueCommands.BuildLeagueSplitsEmbed(eval, null);
            }
        }
    }
}