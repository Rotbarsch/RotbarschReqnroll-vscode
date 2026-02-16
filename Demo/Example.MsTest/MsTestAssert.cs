using System.Threading.Tasks;
using Example.Bindings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Example.MsTest;

public class MsTestAssert : IAssert
{
    public async Task IsTrue(bool input)
    {
        Assert.IsTrue(input);
    }
}