using Disco.Core.Queue;
using Disco.Remote;
using Disco.Remote.Server;

namespace Disco.Server;

internal class Program
{
    private static async Task Main(string[] args)
    {
        CancellationTokenSource serverCts = new CancellationTokenSource();
        string prefix = "http://localhost:4578/";
        IDiscoRemoteServer server = DiscoRemote.CreateServer(new DiscoLocalTaskQueue(), prefix);
        await server.StartAsync(serverCts.Token);
    }
}