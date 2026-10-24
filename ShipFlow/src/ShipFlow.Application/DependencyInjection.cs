using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShipFlow.Application.Features.Auth.Login;
using ShipFlow.Application.Features.Auth.Register;
using ShipFlow.Application.Features.Companies;
using ShipFlow.Application.Features.Dispatching;
using ShipFlow.Application.Features.Fleet;
using ShipFlow.Application.Features.Quotations;
using ShipFlow.Application.Features.Shipments;

namespace ShipFlow.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers application-layer services: validators (by assembly scan) and the feature handlers.
    /// Handlers are plain classes called by controllers; there is no mediator library (see DECISIONS D-016).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);

        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();

        services.AddScoped<ViewerResolver>();
        services.AddScoped<CreateShipmentHandler>();
        services.AddScoped<GetShipmentByIdHandler>();
        services.AddScoped<GetShipmentsHandler>();
        services.AddScoped<OpenForQuotingHandler>();
        services.AddScoped<CancelShipmentHandler>();
        services.AddScoped<SubmitQuoteHandler>();
        services.AddScoped<WithdrawQuoteHandler>();
        services.AddScoped<GetQuotesHandler>();
        services.AddScoped<AcceptQuoteHandler>();

        services.AddScoped<CreateCompanyHandler>();
        services.AddScoped<GetMyCompanyHandler>();
        services.AddScoped<GetMembersHandler>();
        services.AddScoped<AddMemberHandler>();
        services.AddScoped<RemoveMemberHandler>();

        services.AddScoped<RegisterDriverHandler>();
        services.AddScoped<ListDriversHandler>();
        services.AddScoped<GetDriverHandler>();
        services.AddScoped<UpdateDriverHandler>();
        services.AddScoped<DriverActivationHandler>();
        services.AddScoped<RegisterVehicleHandler>();
        services.AddScoped<ListVehiclesHandler>();
        services.AddScoped<GetVehicleHandler>();
        services.AddScoped<VehicleManagementHandler>();

        services.AddScoped<GetAvailableVehiclesHandler>();
        services.AddScoped<GetAvailableDriversHandler>();
        services.AddScoped<AssignShipmentHandler>();
        services.AddScoped<UnassignShipmentHandler>();
        services.AddScoped<GetShipmentAssignmentHandler>();

        return services;
    }
}
