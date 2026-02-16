using Reqnroll;

namespace Example.Bindings;

[Binding]
public class StringBindings
{
    private readonly IAssert _assert;
    private string? _lastResult;

    public StringBindings(IAssert assert)
    {
        _assert = assert;
    }

    [When("(.*) is appended with (.*)")]
    public void Append(string? left, string? right)
    {
        // Enforce commong null behaviour between frameworks here
        if (left == "null") left = null;

        _lastResult = string.Concat(left ?? "", right ?? "");
    }

    [Then("the result string should be (.*)")]
    public async Task Equals(string? right)
    {
        await _assert.IsTrue(right == _lastResult);
    }

}