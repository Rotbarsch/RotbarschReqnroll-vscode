using Reqnroll;

namespace Example.Bindings;

[Binding]
public class NumbersBindings
{
    private readonly IAssert _assert;

    private double? _lastResult;

    public NumbersBindings(IAssert assert)
    {
        _assert = assert;
    }

    [When("I add (.*) and (.*)")]
    public void Addition(string? summand1, string? summand2)
    {
        double l = 0;
        double r = 0;
        double.TryParse(summand1, out l);
        double.TryParse(summand2, out r);


        _lastResult = l + r;
    }

    [Then("the result should be (.*)")]
    public async Task Equals(double? left)
    {
        // Ugly floating point hack (don't try this at home)
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        await _assert.IsTrue(Math.Round(left.GetValueOrDefault(0),2) == Math.Round(_lastResult.GetValueOrDefault(0),2));
    }
}