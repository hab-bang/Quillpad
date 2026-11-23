// expect nesting: 3
using System;
using System.Collections.Generic;

public class Nesting
{
    // Five levels: the fifth "if" is one finding. The sixth level inside it is not counted again.
    public void TooDeep(List<int> items, bool a, bool b)
    {
        foreach (var item in items)
        {
            if (a)
            {
                while (b)
                {
                    try
                    {
                        if (item > 0)
                        {
                            if (item > 1) Console.WriteLine(item);
                        }
                    }
                    finally
                    {
                        b = false;
                    }
                }
            }
        }
    }

    // Two separate blocks at level five give two findings
    public void TwoDeepBlocks(int x)
    {
        if (x > 0)
        {
            if (x > 1)
            {
                if (x > 2)
                {
                    lock (this)
                    {
                        if (x > 4) Console.WriteLine(x);
                        while (x > 5) x--;
                    }
                }
            }
        }
    }

    // Lambdas, local functions and initializers add no level, and else-if chains stay on one level
    public void NotTooDeep(int x)
    {
        if (x > 0)
        {
            using (var scope = new Scope())
            {
                for (var i = 0; i < x; i++)
                {
                    Action action = () =>
                    {
                        if (i > 1) Console.WriteLine(i);
                    };

                    void Local()
                    {
                        if (i > 2) Console.WriteLine(i);
                    }

                    if (i == 1) Console.WriteLine(1);
                    else if (i == 2) Console.WriteLine(2);
                    else if (i == 3) Console.WriteLine(3);
                    else if (i == 4) Console.WriteLine(4);

                    var list = new List<Func<int>> { () => { if (i > 3) return 1; return 0; } };
                    action();
                    Local();
                }
            }
        }
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
