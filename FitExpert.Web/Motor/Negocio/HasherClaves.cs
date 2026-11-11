using System.Security.Cryptography;
using System.Text;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Derivacion de contrasenas con PBKDF2-SHA256. La contrasena nunca se guarda:
/// se almacena el resultado de la derivacion y la sal aleatoria con que se obtuvo.
/// </summary>
public static class HasherClaves
{
    public const int Iteraciones = 100_000;
    private const int TamanoSal = 16;
    private const int TamanoHash = 32;

    public static (byte[] Hash, byte[] Sal) Generar(string clave)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanoSal);
        return (Derivar(clave, sal), sal);
    }

    public static bool Verificar(string clave, byte[] hashAlmacenado, byte[] sal)
    {
        var calculado = Derivar(clave, sal);
        return CryptographicOperations.FixedTimeEquals(calculado, hashAlmacenado);
    }

    private static byte[] Derivar(string clave, byte[] sal) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(clave),
            sal,
            Iteraciones,
            HashAlgorithmName.SHA256,
            TamanoHash);
}
