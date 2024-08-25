using System.Collections.Concurrent;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.Palantir.Core.Discord;
using tobeh.Palantir.Core.Patreon;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Core.Quartz.FlagUpdater;

public class FlagUpdaterJob(
    ILogger<FlagUpdaterJob> logger,
    PatreonApiClient patreonClient,
    ServantApiClient discordClient,
    Members.MembersClient membersClient,
    Admin.AdminClient adminClient) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var subs = await patreonClient.GetCurrentSubscriptions();
        logger.LogInformation("Found {patronCount} patrons and {patronizerCount} patronizers", subs.Patrons.Count,
            subs.Patronizer.Count);

        var roles = await discordClient.GetRoleMembers();
        logger.LogInformation("Found {boosterCount} server boosters and {betaCount} beta members",
            roles.BoostMembers.Count, roles.BetaMembers.Count);

        // get patronized members of patronizers 
        var patronizedMembers = new ConcurrentQueue<long>();
        await Parallel.ForEachAsync(subs.Patronizer, async (patronizer, token) =>
        {
            try
            {
                var patronized = await membersClient.GetPatronizedOfMemberAsync(new() { Id = patronizer });
                patronizedMembers.Enqueue(patronized.DiscordId);
            }
            catch
            {
                // valid case, patronized may decide to patronize noone
            }
        });
        logger.LogInformation("Found {patronizedCount} patronized members", patronizedMembers.Count);

        // get temporary patrons
        var temporaryPatrons = await adminClient.GetTemporaryPatrons(new Empty()).ToListAsync();
        var temporaryPatronMembers = await membersClient.GetMembersByLogin(new GetMembersByLoginMessage
                { Logins = { temporaryPatrons.Select(p => p.Login) } })
            .ToListAsync();
        var temporaryPatronIds = temporaryPatronMembers.Select(member => member.DiscordId).ToList();
        logger.LogInformation("Found {temporaryCount} temporary patron members", temporaryPatronIds.Count);

        // update patron flags
        await adminClient.UpdateMemberFlagsAsync(new()
        {
            FlagId = 4, // patron
            MemberIds = { subs.Patrons, patronizedMembers, subs.Patronizer, temporaryPatronIds },
            InvertOthers = true,
            State = true
        });

        // update patronizer flags
        await adminClient.UpdateMemberFlagsAsync(new()
        {
            FlagId = 7, // patronizer
            MemberIds = { subs.Patronizer },
            InvertOthers = true,
            State = true
        });

        // update booster flags
        await adminClient.UpdateMemberFlagsAsync(new()
        {
            FlagId = 8, // booster
            MemberIds = { roles.BoostMembers },
            InvertOthers = true,
            State = true
        });

        // update beta flags
        await adminClient.UpdateMemberFlagsAsync(new()
        {
            FlagId = 9, // beta
            MemberIds = { roles.BetaMembers },
            InvertOthers = true,
            State = true
        });
    }
}