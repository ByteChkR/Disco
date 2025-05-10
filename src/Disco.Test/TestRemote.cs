using Disco.Core;
using Disco.Core.Queue;
using Disco.Remote;
using Disco.Remote.Server;
using Disco.Test.TaskTypes;

using Newtonsoft.Json.Linq;

namespace Disco.Test;

internal static class TestRemote
{
    private static async Task FillQueue(IDiscoTaskQueue queue, int count)
    {
        Random rnd = new Random();

        for (int i = 0; i < count; i++)
        {
            WaitForArgs a = new WaitForArgs { Delay = rnd.Next(100, 1000) };
            //Wait for random time with different priorities
            Console.WriteLine($"Enqueueing Task {i}");
            await queue.Enqueue(nameof(WaitForTask), rnd.Next(1, 3), JToken.FromObject(a));
        }
    }

    private static IDiscoRemoteServer CreateServer(params string[] prefixes)
    {
        return DiscoRemote.CreateServer(new DiscoLocalTaskQueue(), prefixes);
    }

    public static async Task Run(string[] args)
    {
        //Create the server
        CancellationTokenSource serverCts = new CancellationTokenSource();
        string prefix = "http://localhost:4578/";
        IDiscoRemoteServer server = CreateServer(prefix);
        server.Start(serverCts.Token);

        //Create a client
        IDiscoTaskQueue client = DiscoRemote.CreateClient(prefix);

        //Add Test Data
        await FillQueue(client, 100);

        //Configure a Node that will process the tasks
        DiscoNode node = new DiscoNode("Node", 1000, 100, i => DiscoRemote.CreateClient(prefix), null)
            //Add the WaitForTask Implementation.
            //This makes this node capable of accepting tasks of this type
            .AddRunner<WaitForTask>(() => new WaitForTask());

        //Start the node
        node.Run();

        //Wait for all threads to be idle and the queue beeing empty.
        await node.StopOnIdle();
        //Wait for the server to finish closing
        await serverCts.CancelAsync();
    }
}