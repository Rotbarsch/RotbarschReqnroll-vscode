using Example.Bindings;
using Xunit;

namespace Example.XUnit3;

public class XUnit3Assert : IAssert
{
    public async Task IsTrue(bool input)
    {
        Assert.True(input);
    }
}