// expect parameters: 4
using System;

public class Parameters
{
    // Six parameters: one finding
    public void TooMany(int a, int b, int c, int d, int e, int f) { }

    // Two bool parameters: one finding
    public void TwoFlags(string text, bool trim, bool? upper) { }

    // Six parameters with two bools: two findings
    public Parameters(int a, int b, int c, int d, bool e, Boolean f) { }

    // Five parameters and one bool: fine
    public void Fine(int a, int b, int c, int d, bool e) { }

    // Lambdas are not checked
    private readonly Action<bool, bool> _callback = (x, y) => { };
}
