// expect comment-run: 3
// The header above the first using is a license block and is not counted,
// even when it is three lines or more, like this one.
using System;

public class CommentRun
{
    /// <summary>
    /// XML docs do not count,
    /// however long they are.
    /// </summary>
    public void Run()
    {
        // Three lines in a row:
        // one finding.
        // Done.
        Console.WriteLine(1);

        // Two lines are fine.
        // Done.
        Console.WriteLine(2);

        Console.WriteLine(3); // Comments after code
        Console.WriteLine(4); // do not make
        Console.WriteLine(5); // a run.

        //Console.WriteLine(6);
        //Console.WriteLine(7);
        //Console.WriteLine(8);
        //Console.WriteLine(9);

        // Four slashes is a normal comment, not a doc comment
        ////Console.WriteLine(10);
        ////Console.WriteLine(11);
        ////Console.WriteLine(12);
    }
}
