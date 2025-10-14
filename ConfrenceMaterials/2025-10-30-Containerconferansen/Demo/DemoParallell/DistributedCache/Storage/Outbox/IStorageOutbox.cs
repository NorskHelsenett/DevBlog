public interface IStorageOutbox
{
    public DataTypes.Error? Enqueue(DcItem item);
    public (DataTypes.Error? Error, DcItem? NextItem) RetrieveNext();
    public DataTypes.Error? DeleteNext();
    public DataTypes.Error? MarkNextFailed();
    // public (bool )
}
