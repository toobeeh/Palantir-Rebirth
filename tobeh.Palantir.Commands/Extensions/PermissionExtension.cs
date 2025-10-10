using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;

namespace tobeh.Palantir.Commands.Extensions;

public static class PermissionExtension
{
    public static void EnsurePermissions(this CommandContext context)
    {
        var permissionAttributes = context.Command.Attributes.OfType<RequirePermissionsAttribute>();

        foreach (var permissionAttribute in permissionAttributes)
        {
            var hasPermissions = context.Member?.Permissions.HasAllPermissions(permissionAttribute.UserPermissions) ==
                                 true;
            if (!hasPermissions)
            {
                throw new Exception("USer permission check failed");
            }
        }
    }
}