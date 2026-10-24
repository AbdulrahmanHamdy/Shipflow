using Microsoft.EntityFrameworkCore;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;

namespace ShipFlow.Infrastructure.Persistence.Stores;

public sealed class CompanyAccess : ICompanyAccess
{
    private readonly AppDbContext _db;

    public CompanyAccess(AppDbContext db) => _db = db;

    public Task<CompanyMembershipInfo?> FindActiveMembershipAsync(Guid userId, CancellationToken cancellationToken) =>
        (from m in _db.CompanyMemberships.AsNoTracking()
         join c in _db.TransportCompanies.AsNoTracking() on m.CompanyId equals c.Id
         where m.UserId == userId && m.IsActive && c.IsActive
         orderby m.JoinedAtUtc
         select new CompanyMembershipInfo(m.CompanyId, m.Role))
        .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>
/// Commits the DbContext. A lost optimistic-concurrency race becomes <see cref="ConcurrencyConflictException"/> and a
/// unique-index violation becomes <see cref="ConflictException"/> (both HTTP 409) instead of a 500.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private const string GenericUniqueMessage = "The change conflicts with existing data (a value that must be unique is already in use).";

    // Index name -> message that is safe to show. SQL Server's own message names the index; it is never returned as is.
    private static readonly (string Index, string Message)[] UniqueMessages =
    [
        ("UX_Vehicles_Company_Plate", "A vehicle with this plate number already exists in your company."),
        ("UX_Drivers_Company_License", "A driver with this licence number already exists in your company."),
        ("UX_Drivers_Company_User", "This member already has a driver profile."),
        ("UX_CompanyMemberships_User_Active", "That user already belongs to a transport company."),
        ("UX_CompanyMemberships_Company_User_Active", "That user is already an active member of this company."),
        ("UX_Assignments_Shipment_Active", "The shipment already has an active assignment.")
    ];

    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db) => _db = db;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException ex) when (SqlErrors.Find(ex) is { Number: SqlErrors.UniqueIndexViolation or SqlErrors.UniqueConstraintViolation } sql)
        {
            throw new ConflictException(MessageFor(sql.Message));
        }
    }

    private static string MessageFor(string sqlMessage)
    {
        foreach (var (index, message) in UniqueMessages)
        {
            if (sqlMessage.Contains(index, StringComparison.Ordinal))
            {
                return message;
            }
        }

        return GenericUniqueMessage;
    }
}
