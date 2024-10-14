using KafkaBlobChunking;

public class UserAccessMappingStateService
{
    private Dictionary<string, UserAccessMapping> _userAccessMappings = [];

    public bool TryGetUserAccessMapping(string blobId, out UserAccessMapping? result)
    {
        if (_userAccessMappings.TryGetValue(blobId, out result))
        {
            return true;
        }
        result = default;
        return false;
    }

    public void SetUserAccessMapping(string blobId, UserAccessMapping mapping)
    {
        _userAccessMappings[blobId] = mapping;
    }

    public void RemoveUserAccessMapping(string blobId)
    {
        if(_userAccessMappings.ContainsKey(blobId))
            _userAccessMappings.Remove(blobId);
    }

    public IEnumerable<UserAccessMapping> GetAllUserAccessMappings()
    {
        return _userAccessMappings.Values;
    }
}
