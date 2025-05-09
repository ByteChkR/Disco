using Disco.Core.Queue;
using Disco.Remote;

namespace Disco.Server;

class Program
{
    static async Task Main(string[] args)
    {
        var serverCts = new CancellationTokenSource();
        var prefix = "http://localhost:4578/";
        var server = DiscoRemote.CreateServer(new DiscoLocalTaskQueue(), prefix);
        await server.StartAsync(serverCts.Token);
    }
}