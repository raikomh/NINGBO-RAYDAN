namespace BusinessSearcher.Domain.Identity
{
    /// <summary>
    /// Rol de la cuenta que autentica. Se emite como claim en el JWT y determina
    /// en qué plataforma puede iniciar sesión:
    ///   - Client → solo la app móvil (MAUI, FoodFinderApp)
    ///   - Store  → web y app móvil (mayoristas/minoristas = Tenant; app de gestión BusinessSearcher)
    ///   - Admin  → app y web
    /// </summary>
    public enum AccountRole
    {
        Client = 1,
        Store  = 2,
        Admin  = 3
    }

    /// <summary>Plataforma desde la que se intenta iniciar sesión.</summary>
    public enum ClientPlatform
    {
        Mobile = 1,
        Web    = 2
    }

    public static class AccountRoleExtensions
    {
        /// <summary>Reglas de negocio: qué rol puede loguearse desde qué plataforma.</summary>
        public static bool CanLoginFrom(this AccountRole role, ClientPlatform platform) => role switch
        {
            AccountRole.Client => platform == ClientPlatform.Mobile,
            AccountRole.Store  => true,
            AccountRole.Admin  => true,
            _ => false
        };
    }
}
