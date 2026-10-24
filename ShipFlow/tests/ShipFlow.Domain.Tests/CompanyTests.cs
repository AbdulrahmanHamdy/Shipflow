using ShipFlow.Domain.Common;
using ShipFlow.Domain.Companies;
using ShipFlow.Domain.Fleet;
using ShipFlow.Domain.Tests.Support;
using ShipFlow.Domain.ValueObjects;

namespace ShipFlow.Domain.Tests;

public class TransportCompanyTests
{
    private static readonly DateTime Now = Fixture.Now;

    [Fact]
    public void Create_StartsActiveWithTheFirstAdmin()
    {
        var admin = Guid.NewGuid();

        var company = TransportCompany.Create("  Acme  ", admin, Now);

        Assert.Equal("Acme", company.Name);
        Assert.True(company.IsActive);
        var membership = Assert.Single(company.Memberships);
        Assert.Equal(CompanyRole.Admin, membership.Role);
        Assert.Equal(admin, membership.UserId);
        Assert.Equal(company.Id, membership.CompanyId);
        Assert.True(company.HasActiveMember(admin, CompanyRole.Admin));
        Assert.False(company.HasActiveMember(admin, CompanyRole.Driver));
    }

    [Fact]
    public void Create_RequiresNameAndAdmin()
    {
        DomainAssert.Fails(DomainErrorCodes.Validation, () => TransportCompany.Create(" ", Guid.NewGuid(), Now));
        DomainAssert.Fails(DomainErrorCodes.Validation, () => TransportCompany.Create("Acme", Guid.Empty, Now));
    }

    [Fact]
    public void AddMember_RejectsDuplicateActiveMember()
    {
        var admin = Guid.NewGuid();
        var company = TransportCompany.Create("Acme", admin, Now);

        DomainAssert.Fails(DomainErrorCodes.Validation, () => company.AddMember(admin, CompanyRole.Dispatcher, Now));
    }

    [Fact]
    public void RemoveMember_LastActiveAdmin_IsRejected()
    {
        var admin = Guid.NewGuid();
        var company = TransportCompany.Create("Acme", admin, Now);

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () => company.RemoveMember(admin, Now));
        Assert.True(company.HasActiveMember(admin));
    }

    [Fact]
    public void RemoveMember_AdminIsRemovable_WhenAnotherAdminExists()
    {
        var admin = Guid.NewGuid();
        var second = Guid.NewGuid();
        var company = TransportCompany.Create("Acme", admin, Now);
        company.AddMember(second, CompanyRole.Admin, Now);

        company.RemoveMember(admin, Now);

        Assert.False(company.HasActiveMember(admin));
        Assert.True(company.HasActiveMember(second));
    }

    [Fact]
    public void RemoveMember_UnknownUser_IsNotFound()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);

        DomainAssert.Fails(DomainErrorCodes.NotFound, () => company.RemoveMember(Guid.NewGuid(), Now));
    }

    [Fact]
    public void RemovedMember_CanBeReAdded()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);
        var user = Guid.NewGuid();
        company.AddMember(user, CompanyRole.Dispatcher, Now);
        company.RemoveMember(user, Now);

        company.AddMember(user, CompanyRole.Driver, Now);

        Assert.True(company.HasActiveMember(user, CompanyRole.Driver));
        Assert.False(company.HasActiveMember(user, CompanyRole.Dispatcher));
    }

    [Fact]
    public void RegisterDriver_RequiresActiveDriverMembershipInThisCompany()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);
        var outsider = Guid.NewGuid();
        var dispatcher = Guid.NewGuid();
        company.AddMember(dispatcher, CompanyRole.Dispatcher, Now);

        DomainAssert.Fails(DomainErrorCodes.CrossCompany, () => company.RegisterDriver(outsider, "Out Sider", "L1"));
        DomainAssert.Fails(DomainErrorCodes.CrossCompany, () => company.RegisterDriver(dispatcher, "Dis Patcher", "L2"));
    }

    [Fact]
    public void RegisterDriver_BindsDriverToTheCompany_AndNormalizesLicense()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);
        var user = Guid.NewGuid();
        company.AddMember(user, CompanyRole.Driver, Now);

        var driver = company.RegisterDriver(user, "Dana Driver", " lic-77 ");

        Assert.Equal(company.Id, driver.CompanyId);
        Assert.Equal(user, driver.UserId);
        Assert.Equal("LIC-77", driver.LicenseNumber);
        Assert.True(driver.IsActive);
    }

    [Fact]
    public void RegisterDriver_ForRemovedMember_IsRejected()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);
        var user = Guid.NewGuid();
        company.AddMember(user, CompanyRole.Driver, Now);
        company.RemoveMember(user, Now);

        DomainAssert.Fails(DomainErrorCodes.CrossCompany, () => company.RegisterDriver(user, "Dana", "L1"));
    }

    [Fact]
    public void RegisterVehicle_BindsToCompany_AndNormalizesPlate()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);

        var vehicle = company.RegisterVehicle(" ab 12-cd ", VehicleType.Van, Capacity.Of(500m, 5m));

        Assert.Equal(company.Id, vehicle.CompanyId);
        Assert.Equal("AB12-CD", vehicle.PlateNumber);
        Assert.True(vehicle.IsActive);
    }

    [Fact]
    public void RegisterVehicle_RejectsBlankPlate()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);

        DomainAssert.Fails(DomainErrorCodes.Validation, () => company.RegisterVehicle(" ", VehicleType.Van, Capacity.Of(1m, 1m)));
    }

    [Fact]
    public void InactiveCompany_CannotRegisterDriversVehiclesOrMembers()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);
        var user = Guid.NewGuid();
        company.AddMember(user, CompanyRole.Driver, Now);
        company.Deactivate();

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () => company.RegisterDriver(user, "Dana", "L1"));
        DomainAssert.Fails(DomainErrorCodes.InvalidState, () => company.RegisterVehicle("AB1", VehicleType.Van, Capacity.Of(1m, 1m)));
        DomainAssert.Fails(DomainErrorCodes.InvalidState, () => company.AddMember(Guid.NewGuid(), CompanyRole.Dispatcher, Now));
    }
    [Fact]
    public void ActivateDriver_Works_WhileTheUserIsAnActiveDriverMember()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);
        var user = Guid.NewGuid();
        company.AddMember(user, CompanyRole.Driver, Now);
        var driver = company.RegisterDriver(user, "Dana", "L1");
        driver.Deactivate();

        company.ActivateDriver(driver);

        Assert.True(driver.IsActive);
    }

    [Fact]
    public void ActivateDriver_AfterTheMembershipEnded_IsRejected()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);
        var user = Guid.NewGuid();
        company.AddMember(user, CompanyRole.Driver, Now);
        var driver = company.RegisterDriver(user, "Dana", "L1");
        company.RemoveMember(user, Now);
        driver.Deactivate();

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () => company.ActivateDriver(driver));
        Assert.False(driver.IsActive);
    }

    [Fact]
    public void ActivateDriver_OfAnotherCompany_IsRejected()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);
        var (_, otherDriver, _) = Fixture.OtherCompany();
        otherDriver.Deactivate();

        DomainAssert.Fails(DomainErrorCodes.CrossCompany, () => company.ActivateDriver(otherDriver));
        Assert.False(otherDriver.IsActive);
    }

    [Fact]
    public void ActivateDriver_WhenTheUserNowHoldsADifferentRole_IsRejected()
    {
        var company = TransportCompany.Create("Acme", Guid.NewGuid(), Now);
        var user = Guid.NewGuid();
        company.AddMember(user, CompanyRole.Driver, Now);
        var driver = company.RegisterDriver(user, "Dana", "L1");
        company.RemoveMember(user, Now);
        company.AddMember(user, CompanyRole.Dispatcher, Now);
        driver.Deactivate();

        DomainAssert.Fails(DomainErrorCodes.InvalidState, () => company.ActivateDriver(driver));
    }
}
