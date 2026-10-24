using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Security;
using ShipFlow.Application.Features.Companies;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Fleet;
using ShipFlow.Domain.Common;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Fleet;

namespace ShipFlow.Application.Tests.Dispatching;

public class CompanyAndFleetHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task CreateCompany_MakesTheCallerAdmin_AndGrantsTheRoleInTheSameCommit()
    {
        var w = new DispatchWorld();
        var user = Guid.NewGuid();
        w.Directory.Users[user] = new(user, "u@example.com", "U");
        w.W.AsCustomer(user);

        var dto = await w.CreateCompany().HandleAsync(new CreateCompanyCommand("  Fresh Freight "), Ct);

        Assert.Equal("Fresh Freight", dto.Name);
        Assert.Equal("Admin", dto.MyRole);
        Assert.True(w.CompanyStore.Companies[dto.Id].HasActiveMember(user, CompanyRole.Admin));
        Assert.Contains((user, Roles.CompanyAdmin), w.RoleGrants.Granted);
        Assert.Equal(1, w.W.Uow.Saves);
    }

    [Fact]
    public async Task CreateCompany_WhenAlreadyAMember_InactiveOrInvalid_IsRejected()
    {
        var w = new DispatchWorld();
        w.W.AsDispatcherA();
        w.Directory.Users[w.W.DispatcherA] = new(w.W.DispatcherA, "d@example.com", "D");
        await Assert.ThrowsAsync<ConflictException>(() => w.CreateCompany().HandleAsync(new CreateCompanyCommand("Second"), Ct));

        w.W.AsCustomer(w.W.OtherCustomer);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.CreateCompany().HandleAsync(new CreateCompanyCommand("Ghost"), Ct));

        w.Directory.Users[w.W.OtherCustomer] = new(w.W.OtherCustomer, "o@example.com", "O");
        await Assert.ThrowsAsync<ValidationFailedException>(() => w.CreateCompany().HandleAsync(new CreateCompanyCommand(" "), Ct));
        Assert.Empty(w.RoleGrants.Granted);
    }

    [Fact]
    public async Task AddMember_GrantsTheMatchingRole_AndOnlyAdminsMayAdd()
    {
        var w = new DispatchWorld();
        var newcomer = Guid.NewGuid();
        w.Directory.Users[newcomer] = new(newcomer, "new@example.com", "New");
        w.CompanyStore.Users[newcomer] = w.Directory.Users[newcomer];
        var cmd = new AddMemberCommand("NEW@example.com", CompanyRole.Dispatcher);

        w.W.AsDispatcherA();
        await Assert.ThrowsAsync<ForbiddenException>(() => w.AddMember().HandleAsync(cmd, Ct));

        w.AsAdminA();
        var dto = await w.AddMember().HandleAsync(cmd, Ct);

        Assert.Equal("Dispatcher", dto.Role);
        Assert.True(w.A.HasActiveMember(newcomer, CompanyRole.Dispatcher));
        Assert.Contains((newcomer, Roles.Dispatcher), w.RoleGrants.Granted);
    }

    [Fact]
    public async Task AddMember_Rules_AdminRoleUnknownEmailAndMemberElsewhere()
    {
        var w = new DispatchWorld();
        w.AsAdminA();
        await Assert.ThrowsAsync<ValidationFailedException>(() => w.AddMember().HandleAsync(new AddMemberCommand("a@example.com", CompanyRole.Admin), Ct));
        await Assert.ThrowsAsync<ValidationFailedException>(() => w.AddMember().HandleAsync(new AddMemberCommand("not-an-email", CompanyRole.Driver), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => w.AddMember().HandleAsync(new AddMemberCommand("nobody@example.com", CompanyRole.Driver), Ct));

        w.Directory.Users[w.W.DispatcherB] = new(w.W.DispatcherB, "b@example.com", "B");
        await Assert.ThrowsAsync<ConflictException>(() => w.AddMember().HandleAsync(new AddMemberCommand("b@example.com", CompanyRole.Driver), Ct));
        Assert.Empty(w.RoleGrants.Granted);
    }

    [Fact]
    public async Task RemoveMember_DeactivatesTheDriver_RevokesTheRole_InOneCommit()
    {
        var w = new DispatchWorld();
        w.RoleGrants.Granted.Add((w.DriverUser, Roles.Driver));
        w.AsAdminA();

        await w.RemoveMember().HandleAsync(w.DriverUser, Ct);

        Assert.False(w.A.HasActiveMember(w.DriverUser));
        Assert.False(w.Driver.IsActive);
        Assert.DoesNotContain((w.DriverUser, Roles.Driver), w.RoleGrants.Granted);
        Assert.Equal(1, w.W.Uow.Saves);
    }

    [Fact]
    public async Task RemoveMember_RemovedDriverCanNoLongerBeAssigned()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.AsAdminA();
        await w.RemoveMember().HandleAsync(w.DriverUser, Ct);
        w.W.AsDispatcherA();

        var ex = await Assert.ThrowsAsync<DomainException>(() => w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct));
        Assert.Equal(DomainErrorCodes.InvalidState, ex.Code);
    }

    [Fact]
    public async Task RemoveMember_WithAnActiveAssignment_IsRefused_AndKeepsTheDriverActive()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();
        await w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);
        w.AsAdminA();

        await Assert.ThrowsAsync<ConflictException>(() => w.RemoveMember().HandleAsync(w.DriverUser, Ct));

        Assert.True(w.Driver.IsActive);
        Assert.Equal(0, w.W.Uow.Saves - 1);
    }

    [Fact]
    public async Task RemoveMember_LastAdmin_IsRefused_AndOthersCannotCall()
    {
        var w = new DispatchWorld();
        // company A has two admins in the fixture (the creator and AdminA); remove one, then the last one is protected
        var creatorAdmin = w.A.Memberships.First(m => m.Role == CompanyRole.Admin && m.UserId != w.AdminA).UserId;
        w.AsAdminA();
        await w.RemoveMember().HandleAsync(creatorAdmin, Ct);
        await Assert.ThrowsAsync<DomainException>(() => w.RemoveMember().HandleAsync(w.AdminA, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => w.RemoveMember().HandleAsync(Guid.NewGuid(), Ct));

        w.W.AsDispatcherA();
        await Assert.ThrowsAsync<ForbiddenException>(() => w.RemoveMember().HandleAsync(w.DriverUser, Ct));
    }

    [Fact]
    public async Task RegisterDriver_RequiresAnActiveDriverMemberOfTheCallersCompany()
    {
        var w = new DispatchWorld();
        w.AsAdminA();
        var dispatcherUser = w.W.DispatcherA;
        var driverMember = Guid.NewGuid();
        w.A.AddMember(driverMember, CompanyRole.Driver, w.Now);

        await Assert.ThrowsAsync<NotFoundException>(() => w.RegisterDriverHandler().HandleAsync(new RegisterDriverCommand(dispatcherUser, "X", "L9"), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => w.RegisterDriverHandler().HandleAsync(new RegisterDriverCommand(Guid.NewGuid(), "X", "L9"), Ct));
        await Assert.ThrowsAsync<ValidationFailedException>(() => w.RegisterDriverHandler().HandleAsync(new RegisterDriverCommand(driverMember, "", "L9"), Ct));

        var dto = await w.RegisterDriverHandler().HandleAsync(new RegisterDriverCommand(driverMember, "Mo", "l9"), Ct);
        Assert.Equal("L9", dto.LicenseNumber);
        Assert.True(dto.IsActive);

        w.W.AsDispatcherA();
        await Assert.ThrowsAsync<ForbiddenException>(() => w.RegisterDriverHandler().HandleAsync(new RegisterDriverCommand(driverMember, "Mo", "L10"), Ct));
    }

    [Fact]
    public async Task GetDriver_OfAnotherCompany_IsNotFound()
    {
        var w = new DispatchWorld();
        var foreign = w.RegisterDriver(w.B, Guid.NewGuid(), "Olly", "LIC-9");
        w.W.AsDispatcherA();

        await Assert.ThrowsAsync<NotFoundException>(() => w.GetDriver().HandleAsync(foreign.Id, Ct));
        Assert.Equal(w.Driver.Id, (await w.GetDriver().HandleAsync(w.Driver.Id, Ct)).Id);
    }

    [Fact]
    public async Task DriverActivation_DeactivateIsBlockedByAnActiveAssignment_ActivateNeedsMembership()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();
        await w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);
        w.AsAdminA();
        await Assert.ThrowsAsync<ConflictException>(() => w.DriverActivation().DeactivateAsync(w.Driver.Id, Ct));
        Assert.True(w.Driver.IsActive);

        w.W.AsDispatcherA();
        await w.Unassign().HandleAsync(shipment.Id, new UnassignShipmentCommand("free"), Ct);
        w.AsAdminA();
        Assert.False((await w.DriverActivation().DeactivateAsync(w.Driver.Id, Ct)).IsActive);
        Assert.True((await w.DriverActivation().ActivateAsync(w.Driver.Id, Ct)).IsActive);

        w.A.RemoveMember(w.DriverUser, w.Now);
        w.Driver.Deactivate();
        var ex = await Assert.ThrowsAsync<DomainException>(() => w.DriverActivation().ActivateAsync(w.Driver.Id, Ct));
        Assert.Equal(DomainErrorCodes.InvalidState, ex.Code);
    }

    [Fact]
    public async Task RegisterVehicle_ValidatesCapacity_AndOnlyAdminsMay()
    {
        var w = new DispatchWorld();
        w.AsAdminA();
        foreach (var bad in new[]
                 {
                     new RegisterVehicleCommand("P 1", VehicleType.Van, 0m, 5m),
                     new RegisterVehicleCommand("P 1", VehicleType.Van, 5m, -1m),
                     new RegisterVehicleCommand("P 1", VehicleType.Van, 5m, 5.0001m),
                     new RegisterVehicleCommand("P 1", VehicleType.Van, 2_000_000m, 5m),
                     new RegisterVehicleCommand(" ", VehicleType.Van, 5m, 5m),
                     new RegisterVehicleCommand("P 1", (VehicleType)99, 5m, 5m)
                 })
        {
            await Assert.ThrowsAsync<ValidationFailedException>(() => w.RegisterVehicle().HandleAsync(bad, Ct));
        }

        var dto = await w.RegisterVehicle().HandleAsync(new RegisterVehicleCommand(" ab 12 ", VehicleType.Van, 800.5m, 6m), Ct);
        Assert.Equal("AB12", dto.PlateNumber);
        Assert.Equal(w.A.Id, w.Fleet.Vehicles.Single(v => v.Id == dto.Id).CompanyId);

        w.W.AsDispatcherA();
        await Assert.ThrowsAsync<ForbiddenException>(() => w.RegisterVehicle().HandleAsync(new RegisterVehicleCommand("Z 1", VehicleType.Van, 5m, 5m), Ct));
    }

    [Fact]
    public async Task VehicleCapacity_CanGrowAlways_ShrinkOnlyWhenNotAssigned()
    {
        var w = new DispatchWorld();
        var shipment = w.AcceptedShipment();
        w.W.AsDispatcherA();
        await w.Assign().HandleAsync(shipment.Id, w.Cmd(w.Driver, w.Truck, w.Window()), Ct);
        w.AsAdminA();

        var grown = await w.VehicleManagement().UpdateCapacityAsync(w.Truck.Id, new UpdateVehicleCapacityCommand(9000m, 90m), Ct);
        Assert.Equal(9000m, grown.MaxWeightKg);
        await Assert.ThrowsAsync<ConflictException>(() => w.VehicleManagement().UpdateCapacityAsync(w.Truck.Id, new UpdateVehicleCapacityCommand(1000m, 90m), Ct));
        await Assert.ThrowsAsync<ConflictException>(() => w.VehicleManagement().DeactivateAsync(w.Truck.Id, Ct));
        Assert.Equal(9000m, w.Truck.Capacity.MaxWeightKg);

        w.W.AsDispatcherA();
        await w.Unassign().HandleAsync(shipment.Id, new UnassignShipmentCommand("free"), Ct);
        w.AsAdminA();
        Assert.Equal(1000m, (await w.VehicleManagement().UpdateCapacityAsync(w.Truck.Id, new UpdateVehicleCapacityCommand(1000m, 10m), Ct)).MaxWeightKg);
        Assert.False((await w.VehicleManagement().DeactivateAsync(w.Truck.Id, Ct)).IsActive);
        Assert.True((await w.VehicleManagement().ActivateAsync(w.Truck.Id, Ct)).IsActive);
    }

    [Fact]
    public async Task VehicleManagement_OfAnotherCompany_IsNotFound()
    {
        var w = new DispatchWorld();
        var foreign = w.AddVehicle(w.B, "OTH 1", 9000m, 90m);
        w.AsAdminA();

        await Assert.ThrowsAsync<NotFoundException>(() => w.VehicleManagement().DeactivateAsync(foreign.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => w.VehicleManagement().UpdateCapacityAsync(foreign.Id, new UpdateVehicleCapacityCommand(1m, 1m), Ct));
        Assert.True(foreign.IsActive);
    }
}
