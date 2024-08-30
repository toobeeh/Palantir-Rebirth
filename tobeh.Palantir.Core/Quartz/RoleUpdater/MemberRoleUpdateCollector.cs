namespace tobeh.Palantir.Core.Quartz.RoleUpdater;

public class MemberRoleUpdateCollector
{
    private static Dictionary<long, bool> _memberIds = new();
    private static Dictionary<int, bool> _memberLogins = new();

    public void MarkIdForUpdate(long id)
    {
        _memberIds.TryAdd(id, true);
    }

    public void MarkLoginForUpdate(int id)
    {
        _memberLogins.TryAdd(id, true);
    }

    public List<long> CollectIds()
    {
        var newList = _memberIds.Keys.ToList();
        _memberIds.Clear();
        return newList;
    }

    public List<int> CollectLogins()
    {
        var newList = _memberLogins.Keys.ToList();
        _memberLogins.Clear();
        return newList;
    }
}