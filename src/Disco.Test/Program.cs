namespace Disco.Test;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        await TestRemote.Run(args);
    }
}