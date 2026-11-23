// expect catch-all: 3, empty-catch: 0
using System;
using System.IO;

public class CatchAll
{
    public void Run()
    {
        // catch (Exception): one finding
        try { Work(); }
        catch (Exception ex) { Log(ex.Message); }

        // Bare catch: one finding
        try { Work(); }
        catch { Log("failed"); }

        // Full name: one finding
        try { Work(); }
        catch (System.Exception) { Log("failed"); }

        // A when filter or a narrow type: fine
        try { Work(); }
        catch (Exception ex) when (ex is IOException) { Log(ex.Message); }

        try { Work(); }
        catch (IOException ex) { Log(ex.Message); }
    }

    private static void Work() { }

    private static void Log(string message) { }
}
