using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;
using ShipFlow.Domain.Companies;
using ShipFlow.Infrastructure.Persistence;

namespace ShipFlow.Infrastructure.Identity;

public sealed class UserDirectory : IUserDirectory
{
    private readonly AppDbContext _db;

    public UserDirectory(AppDbContext db) => _db = db;

    public Task<UserSummary?> FindActiveByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new UserSummary(u.Id, u.Email ?? string.Empty, u.FullName))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<UserSummary?> FindActiveByEmailAsync(string email, CancellationToken cancellationToken)
    {
        // Identity's default normalizer is upper-invariant; the unique index is on this column.
        var normalized = email.Trim().ToUpperInvariant();
        return _db.Users.AsNoTracking()
            .Where(u => u.NormalizedEmail == normalized && u.IsActive)
            .Select(u => new UserSummary(u.Id, u.Email ?? string.Empty, u.FullName))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>
/// Stages Identity role rows (<c>AspNetUserRoles</c>) on the shared DbContext without saving, so
/// <see cref="IUnitOfWork.SaveChangesAsync"/> commits them in the same transaction as the membership change. <c>UserManager</c>
/// is deliberately not used here: it saves immediately, which would split the membership and its role into two commits.
/// </summary>
public sealed class MembershipRoles : IMembershipRoles
{
    private readonly AppDbContext _db;

    public MembershipRoles(AppDbContext db) => _db = db;

    public async Task StageGrantAsync(Guid userId, CompanyRole role, CancellationToken cancellationToken)
    {
        var roleId = await FindRoleIdAsync(role, cancellationToken);
        if (_db.UserRoles.Local.Any(ur => ur.UserId == userId && ur.RoleId == roleId))
        {
            return;
        }

        if (!await _db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken))
        {
            _db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
        }
    }

    public async Task StageRevokeAsync(Guid userId, CompanyRole role, CancellationToken cancellationToken)
    {
        var roleId = await FindRoleIdAsync(role, cancellationToken);
        var row = await _db.UserRoles.SingleOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);
        if (row is not null)
        {
            _db.UserRoles.Remove(row);
        }
    }

    private async Task<Guid> FindRoleIdAsync(CompanyRole role, CancellationToken cancellationToken)
    {
        var name = CompanyRoleMapping.ToIdentityRole(role);
        var normalized = name.ToUpperInvariant();
        var id = await _db.Roles.AsNoTracking()
            .Where(r => r.NormalizedName == normalized)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Roles are created at startup (Database:SeedReferenceData). Failing loudly beats silently skipping a grant.
        return id ?? throw new InvalidOperationException($"Identity role '{name}' does not exist. Seed reference data first (docs/SETUP.md).");
    }
}
