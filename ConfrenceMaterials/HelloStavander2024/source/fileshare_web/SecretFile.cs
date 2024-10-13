public record SecretFile
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }

public required string Size {get;set;}
public required FileRights Rights {get;set;}
public bool Delete => Rights == FileRights.Owner;
public bool Download => Rights == FileRights.Owner || Rights == FileRights.Shared;
public bool Share => Rights == FileRights.Owner;
}
