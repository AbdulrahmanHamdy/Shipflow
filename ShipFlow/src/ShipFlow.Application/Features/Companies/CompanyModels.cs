using ShipFlow.Domain.Companies;

namespace ShipFlow.Application.Features.Companies;

public sealed record CompanyDto(Guid Id, string Name, bool IsActive, DateTime CreatedAtUtc, string MyRole);

public sealed record MemberDto(
    Guid UserId, string Email, string FullName, string Role, bool IsActive, DateTime JoinedAtUtc, DateTime? LeftAtUtc);

internal static class CompanyMapping
{
    public static CompanyDto ToDto(this TransportCompany company, CompanyRole myRole) =>
        new(company.Id, company.Name, company.IsActive, company.CreatedAtUtc, myRole.ToString());
}
