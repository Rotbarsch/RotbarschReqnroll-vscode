using Example.Bindings;
using Xunit;

namespace Example.XUnit2;

public class XUnit2Assert : IAssert
{
    public async Task IsTrue(bool input)
    {
        Assert.True(input);
    }
}