using System.Security.Cryptography;
using System.Text;
using SistemaTurnos.Aplicacion.Abstracciones;

namespace SistemaTurnos.Infraestructura.SqlServer.Seguridad;

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        if(string.IsNullOrWhiteSpace(password)||password.Length>128||password.EnumerateRunes().Count()<15) throw new ArgumentException("Longitud de contraseña inválida.",nameof(password));
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32);
        return $"600000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
    public bool Verify(string password, string hash)
    {
        if(password is null||password.Length>128||hash is null||hash.Length>200) return false;
        try
        {
            var parts = hash.Split('.');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int iterations)
                || iterations is < 100000 or > 2000000) return false;
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expected = Convert.FromBase64String(parts[2]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}
