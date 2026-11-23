// expect file-length: 1
// The test runs this sample with a limit of 10 lines, to keep it small.
// The finding's value is the line count, 14, so the file may not grow.
using System;

public class FileLength
{
    public void Run()
    {
        Console.WriteLine(1);
        Console.WriteLine(2);
        Console.WriteLine(3);
    }
}
