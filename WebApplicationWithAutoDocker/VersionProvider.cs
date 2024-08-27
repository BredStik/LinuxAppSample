namespace WebApplicationWithAutoDocker;

public class VersionProvider
{
    private readonly Version _version;
    
    public VersionProvider()
    {
        _version = new Version(Guid.NewGuid(), DateTime.UtcNow);
    }

    public Version GetVersion() => _version;
}

public record Version(Guid Id, DateTime StartedOn);