// expect duplicate: 4
using System;
using System.Collections.Generic;

public class Duplicate
{
    // The same six statements as in Second, with other local names and literals: one finding here
    public void First(List<string> lines)
    {
        Console.WriteLine("first");
        var count = 0;
        foreach (var line in lines)
        {
            count += line.Length;
        }
        var average = count / Math.Max(1, lines.Count);
        Console.WriteLine(average);
        Save(average);
    }

    // ...and one here
    public void Second(List<string> rows)
    {
        var total = 10;
        foreach (var row in rows)
        {
            total += row.Length;
        }
        var mean = total / Math.Max(2, rows.Count);
        Console.WriteLine(mean);
        Save(mean);
        Console.WriteLine("second");
    }

    // Two short method bodies that are the same after normalizing: one finding each
    public void LogA(Exception ex)
    {
        Console.WriteLine("A failed");
        Console.WriteLine(ex.Message);
        Save(1);
    }

    public void LogB(Exception error)
    {
        Console.WriteLine("B failed");
        Console.WriteLine(error.Message);
        Save(2);
    }

    // Same shape, but it calls something else: fine
    public void LogC(Exception error)
    {
        Console.WriteLine("C failed");
        Console.WriteLine(error.StackTrace);
        Save(3);
    }

    // Six equal statements in a row overlap themselves and are not a duplicate
    public void Repeat()
    {
        Save(1);
        Save(1);
        Save(1);
        Save(1);
        Save(1);
        Save(1);
    }

    private static void Save(int value) { }
}
