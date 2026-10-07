using System.Security.Cryptography;
using System.Text;

namespace BusinessSearcher.Application.Commons
{
    // Hash determinístico (SHA-256, sin sal) para las API keys de sincronización: a diferencia
    // de una contraseña, la key ya es un secreto de alta entropía generado por el propio sistema
    // (ISecureTokenGenerator), así que no necesita sal — y necesitamos poder buscar el tenant
    // dueño de una key directo por su hash (Tenants.FirstOrDefault(t => t.SyncApiKeyHash == hash)),
    // algo que un hash con sal (BCrypt) no permite sin iterar todos los tenants.
    public static class SyncApiKeyHasher
    {
        public static string Hash(string rawKey)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
