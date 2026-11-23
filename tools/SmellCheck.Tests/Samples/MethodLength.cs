// expect method-length: 1
// The test runs this sample with a limit of 10 lines, to keep it small.
using System;

public class MethodLength
{
    // 11 lines from "public" to the closing brace: one finding. The attribute does not count.
    [Obsolete]
    public void TooLong()
    {
        Console.WriteLine(1);
        Console.WriteLine(2);
        Console.WriteLine(3);
        Console.WriteLine(4);
        Console.WriteLine(5);
        Console.WriteLine(6);
        Console.WriteLine(7);
        Console.WriteLine(8);
    }

    // 10 lines: fine
    [Obsolete]
    public void JustRight()
    {
        Console.WriteLine(1);
        Console.WriteLine(2);
        Console.WriteLine(3);
        Console.WriteLine(4);
        Console.WriteLine(5);
        Console.WriteLine(6);
        Console.WriteLine(7);
    }
}
