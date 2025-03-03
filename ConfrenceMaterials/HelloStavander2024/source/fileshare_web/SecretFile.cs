public record SecretFile
{
    public required string Name { get; set; }
    public required string OwnerName {get; set;}
    public required FileRights Rights { get; set; }
    public bool Delete => (Rights & FileRights.Owner) == 0;
    public bool Download => (Rights & (FileRights.Owner | FileRights.Shared)) == 0;
    public bool Share => (Rights & FileRights.Owner) == 0;
}
