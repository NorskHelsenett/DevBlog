using KafkaBlobChunking;

public class UserAccessMappingStateService
{
    private Dictionary<string, UserAccessMapping> _userAccessMappings = [];

    public UserAccessMapping GetUserAccessMapping(string blobId)
    {
        return _userAccessMappings[blobId];
    }

    public void SetUserAccessMapping(string blobId, UserAccessMapping mapping)
    {
        _userAccessMappings[blobId] = mapping;
    }

    public IEnumerable<UserAccessMapping> GetAllUserAccessMappings()
    {
        return _userAccessMappings.Values;
    }
}
