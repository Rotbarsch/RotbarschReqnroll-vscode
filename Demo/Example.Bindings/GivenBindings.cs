using Reqnroll;

namespace Example.Bindings;

[Binding]
public class GivenBindings
{
    private readonly IAssert _assert;
    private string? _message;

    public GivenBindings(IAssert assert)
    {
        _assert = assert;
    }

    [Given("the system is ready")]
    public void SystemIsReady()
    {
        // no-op: used as a Background step
    }

    [Given("the following message:")]
    public void SetMessage(string message)
    {
        _message = message;
    }

    [Then("the message should be:")]
    public async Task MessageShouldBe(string expected)
    {
        await _assert.IsTrue(_message?.Trim() == expected.Trim());
    }
}
