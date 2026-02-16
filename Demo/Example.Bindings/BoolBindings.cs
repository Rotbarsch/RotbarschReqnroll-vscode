using Reqnroll;

namespace Example.Bindings;

[Binding]
public class BoolBindings
{
    private readonly IAssert _assert;
    private bool? _lastResult;

    public BoolBindings(IAssert assert)
    {
        _assert = assert;
    }

    [When("(.*) OR (.*)")]
    public void Or(bool? left, bool? right)
    {
        _lastResult = left.GetValueOrDefault(false) || right.GetValueOrDefault(false);
    }

    [Then("the result bool should be (.*)")]
    public async Task Equals(bool? right)
    {
        await _assert.IsTrue(right == _lastResult);
    }
}