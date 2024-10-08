using Microsoft.Extensions.Logging;
using Quartz;
using tobeh.TypoLinkedRolesService;
using tobeh.Valmar;
using tobeh.Valmar.Client.Util;

namespace tobeh.Palantir.Core.Quartz.RoleUpdater;

public class RoleUpdaterJob(
    ILogger<RoleUpdaterJob> logger,
    MemberRoleUpdateCollector collector,
    Members.MembersClient membersClient,
    LinkedRoles.LinkedRolesClient linkedRolesClient) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("Execute({context})", context);

        var ids = collector.CollectIds();
        var logins = collector.CollectLogins();
        logger.LogInformation("Found {nId} ids and {nLogin} logins to update", ids.Count, logins.Count);

        var members = await membersClient.GetMembersByLogin(new GetMembersByLoginMessage { Logins = { logins } })
            .ToListAsync();
        ids.AddRange(members.Select(member => member.DiscordId));

        await linkedRolesClient.UpdateUserMetadataAsync(new UpdateUserMetadataMessage { UserIds = { ids } });
    }
}