// expect delay: 3
using System.Threading;
using System.Threading.Tasks;

public class Delay
{
    public async Task Run()
    {
        await Task.Delay(100);
        Thread.Sleep(10);
        await System.Threading.Tasks.Task.Delay(5);

        // Other methods with the same name are fine
        Delay();
        await Other.Delay();
    }

    private static void Delay() { }

    private static class Other
    {
        public static Task Delay() => Task.CompletedTask;
    }
}
