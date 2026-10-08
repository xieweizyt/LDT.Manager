using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Volo.Abp.Identity;

namespace Ldt;

// Role assignment has its own permission instead of inheriting the upstream edit-user policy.
public sealed class LdtIdentityApiConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers.Where(controller => controller.ControllerType.AsType() == typeof(IdentityUserController)))
        {
            var action = controller.Actions.Single(action => action.ActionMethod.Name == nameof(IdentityUserController.UpdateRolesAsync));
            controller.Actions.Remove(action);
        }
    }
}
