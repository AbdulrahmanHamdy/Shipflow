using Microsoft.AspNetCore.Mvc;
using ShipFlow.API.Middleware;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Domain.Common;

namespace ShipFlow.API.IntegrationTests.Api;

public class ProblemMapperTests
{
    // Every code the domain can raise, and the HTTP status the API promises for it. A new code added to
    // DomainErrorCodes fails DomainCodes_AreAllCovered until it is listed here and mapped deliberately.
    private static readonly Dictionary<string, int> ExpectedStatus = new()
    {
        [DomainErrorCodes.Validation] = 400,
        [DomainErrorCodes.NotFound] = 404,
        [DomainErrorCodes.NotOwner] = 403,
        [DomainErrorCodes.CrossCompany] = 403,
        [DomainErrorCodes.CapacityExceeded] = 422,
        [DomainErrorCodes.InvalidState] = 409,
        [DomainErrorCodes.InvalidTransition] = 409,
        [DomainErrorCodes.QuoteRule] = 409,
        [DomainErrorCodes.Conflict] = 409
    };

    [Fact]
    public void DomainCodes_AreAllCovered()
    {
        var declared = typeof(DomainErrorCodes)
            .GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(declared, ExpectedStatus.Keys.OrderBy(c => c, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void DomainException_MapsToItsStatus_AndKeepsItsSafeMessage()
    {
        foreach (var (code, status) in ExpectedStatus)
        {
            var mapped = ProblemMapper.Map(new DomainException(code, "Safe message."));

            Assert.Equal(status, mapped.Problem.Status);
            Assert.Equal("Safe message.", mapped.Problem.Detail);
            Assert.Equal(code, mapped.Problem.Extensions["code"]);
            Assert.True(mapped.IsExpected);
        }
    }

    [Fact]
    public void ValidationFailed_MapsTo400_WithFieldErrors()
    {
        var errors = new Dictionary<string, string[]> { ["Email"] = ["Bad email."] };

        var mapped = ProblemMapper.Map(new ValidationFailedException(errors));

        var problem = Assert.IsType<ValidationProblemDetails>(mapped.Problem);
        Assert.Equal(400, problem.Status);
        Assert.Equal(new[] { "Bad email." }, problem.Errors["Email"]);
        Assert.Equal("validation", problem.Extensions["code"]);
    }

    [Fact]
    public void AuthenticationFailed_MapsTo401() =>
        Assert.Equal(401, ProblemMapper.Map(new AuthenticationFailedException()).Problem.Status);

    [Theory]
    [InlineData("notfound", 404)]
    [InlineData("forbidden", 403)]
    [InlineData("concurrency", 409)]
    public void AppExceptions_MapToTheirStatus(string kind, int status)
    {
        Exception ex = kind switch
        {
            "notfound" => new NotFoundException(),
            "forbidden" => new ForbiddenException(),
            _ => new ConcurrencyConflictException()
        };

        Assert.Equal(status, ProblemMapper.Map(ex).Problem.Status);
    }

    [Fact]
    public void Conflict_MapsTo409() =>
        Assert.Equal(409, ProblemMapper.Map(new ConflictException("Taken.")).Problem.Status);

    [Fact]
    public void UnknownException_BecomesAGeneric500_ThatLeaksNothing()
    {
        var mapped = ProblemMapper.Map(new InvalidOperationException("Server=prod;Password=hunter2"));

        Assert.Equal(500, mapped.Problem.Status);
        Assert.Null(mapped.Problem.Detail);
        Assert.DoesNotContain("hunter2", mapped.Problem.Title);
        Assert.False(mapped.IsExpected);
    }
}
