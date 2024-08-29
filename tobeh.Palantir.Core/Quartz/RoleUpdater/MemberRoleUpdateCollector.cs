namespace tobeh.Palantir.Core.Quartz.RoleUpdater;

public class MemberRoleUpdateCollector
{
    private static List<long> _memberIds = new();
    private static List<int> _memberLogins = new();

    public void MarkIdForUpdate(long id)
    {
        _memberIds.Add(id);
    }

    public void MarkLoginForUpdate(long id)
    {
        _memberIds.Add(id);
    }

    public List<long> CollectIds()
    {
        var newList = new List<long>(_memberIds);
        _memberIds.Clear();
        return newList;
    }

    public List<int> CollectLogins()
    {
        var newList = new List<int>(_memberLogins);
        _memberLogins.Clear();
        return newList;
    }
}