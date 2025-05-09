using Disco.Core;
using Disco.Core.Queue;
using Disco.Test.TaskTypes;

using Newtonsoft.Json.Linq;

namespace Disco.Test;

internal static class TestLocal
{
    private static async Task FillQueue(IDiscoTaskQueue queue, int count)
    {
        Random rnd = new Random();

        for (int i = 0; i < count; i++)
        {
            WaitForArgs a = new WaitForArgs { Delay = rnd.Next(100, 1000) };
            //Wait for random time with different priorities
            await queue.Enqueue(WaitForTask.NAME, rnd.Next(1, 3), JToken.FromObject(a));
        }
    }


    public static async Task Run(string[] args)
    {
        //Create a Task Queue that is local to this process
        DiscoLocalTaskQueue queue = new DiscoLocalTaskQueue();

        //Add Test Data
        await FillQueue(queue, 1000);

        //Configure a Node that will process the tasks
        DiscoNode node = new DiscoNode("Node", 100, i => queue)
            //Add the WaitForTask Implementation.
            //This makes this node capable of accepting tasks of this type
            .AddRunner<WaitForTask>();

        //Start the node
        node.Run();

        //Wait for all threads to be idle and the queue beeing empty.
        await node.StopOnIdle();
    }
}