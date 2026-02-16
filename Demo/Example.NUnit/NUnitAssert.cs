using Example.Bindings;
using NUnit.Framework;

namespace Example.NUnit;

public class NUnitAssert : IAssert
{
    public async Task IsTrue(bool input)
    {
        Assert.IsTrue(input);
    }
}