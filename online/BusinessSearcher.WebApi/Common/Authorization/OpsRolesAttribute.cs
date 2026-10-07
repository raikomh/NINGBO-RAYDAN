using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BusinessSearcher.API.Common.Authorization
{
    /// <summary>
    /// Restringe una acción/controlador del TPV a determinados roles operativos.
    /// El dueño del negocio (Tenant) es <see cref="OperationsRole.Administrador"/> y pasa siempre.
    /// Sin argumentos (<c>[OpsRoles]</c>) equivale a "solo Administrador", ya que ningún otro rol
    /// puede estar en una lista vacía. Debe combinarse con <c>[Authorize]</c> (autenticación).
    /// Si el usuario no tiene rol operativo o su rol no está permitido, responde 403.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class OpsRolesAttribute : Attribute, IAuthorizationFilter
    {
        private readonly OperationsRole[] _allowed;

        public OpsRolesAttribute(params OperationsRole[] allowed) => _allowed = allowed;

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var currentUser = context.HttpContext.RequestServices.GetService(typeof(ICurrentUserService)) as ICurrentUserService;
            var role = currentUser?.OpsRole;

            if (role is null)
            {
                context.Result = new ObjectResult(new { success = false, message = "Sesión de negocio requerida." }) { StatusCode = 403 };
                return;
            }

            // Administrador siempre pasa; el resto debe estar en la lista permitida (una lista
            // vacía —"[OpsRoles]" sin argumentos— no admite a ningún otro rol: solo Administrador).
            if (role == OperationsRole.Administrador || _allowed.Contains(role.Value))
                return;

            context.Result = new ObjectResult(new
            {
                success = false,
                message = $"Tu rol ({role}) no tiene permiso para esta operación."
            })
            { StatusCode = 403 };
        }
    }
}
