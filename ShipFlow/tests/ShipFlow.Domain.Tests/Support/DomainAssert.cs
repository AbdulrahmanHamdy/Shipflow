using ShipFlow.Domain.Common;

namespace ShipFlow.Domain.Tests.Support;

public static class DomainAssert
{
    /// <summary>Asserts the action throws exactly <see cref="DomainException"/> with the given error code.</summary>
    public static DomainException Fails(string expectedCode, Action action)
    {
        var ex = Assert.Throws<DomainException>(action);
        Assert.Equal(expectedCode, ex.Code);
        return ex;
    }
}
