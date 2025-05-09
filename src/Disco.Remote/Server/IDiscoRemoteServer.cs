namespace Disco.Remote.Server;

public interface IDiscoRemoteServer
{
    public void Start(CancellationToken cancellationToken);
    public Task StartAsync(CancellationToken cancellationToken);
}