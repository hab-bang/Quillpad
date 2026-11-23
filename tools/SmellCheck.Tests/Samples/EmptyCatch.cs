// expect empty-catch: 2, catch-all: 1
using System;
using System.IO;

public class EmptyCatch
{
    public void Run()
    {
        // Empty: one finding
        try { Work(); }
        catch (IOException) { }

        // A comment is not a statement, so this is empty too. It is also a catch-all.
        try { Work(); }
        catch (Exception)
        {
            // ignored
        }

        // Has a statement: fine
        try { Work(); }
        catch (IOException ex) { Console.WriteLine(ex.Message); }
    }

    private static void Work() { }
}
