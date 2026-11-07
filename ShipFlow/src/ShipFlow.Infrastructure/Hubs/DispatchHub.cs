using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ShipFlow.Application.Common.Security;

namespace ShipFlow.Infrastructure.Hubs;

[Authorize]
public sealed class DispatchHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user != null)
        {
            var userId = user.FindFirst(ClaimNames.Subject)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            }

            var companyClaim = user.FindFirst("company_id")?.Value;
            if (!string.IsNullOrEmpty(companyClaim))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Company_{companyClaim}");
            }

            if (user.IsInRole(Roles.Driver))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Drivers");
                if (!string.IsNullOrEmpty(companyClaim))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Company_{companyClaim}_Drivers");
                }
            }

            if (user.IsInRole(Roles.Dispatcher) || user.IsInRole(Roles.CompanyAdmin))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Dispatchers");
                if (!string.IsNullOrEmpty(companyClaim))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Company_{companyClaim}_Dispatchers");
                }
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinCompany(string companyId)
    {
        if (!string.IsNullOrWhiteSpace(companyId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Company_{companyId}");
            if (Context.User?.IsInRole(Roles.Driver) == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Company_{companyId}_Drivers");
            }
            if (Context.User?.IsInRole(Roles.Dispatcher) == true || Context.User?.IsInRole(Roles.CompanyAdmin) == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Company_{companyId}_Dispatchers");
            }
        }
    }
}
