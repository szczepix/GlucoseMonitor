using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using GlucoseMonitor.Core.Interfaces;

namespace GlucoseMonitor.Infrastructure.Services;

/// <summary>
/// Secure storage service using Windows Data Protection API (DPAPI).
/// Encrypts data with user-specific Windows credentials - only the same
/// Windows user on the same machine can decrypt the data.
/// </summary>
[SupportedOSPlatform("windows")]
public class SecureStorageService : ISecureStorageService
{
    private readonly ILogger _logger;

    // Optional entropy for additional security layer
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GlucoseMonitor.SecureStorage.v1");

    public SecureStorageService(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encryptedBytes);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError($"Failed to encrypt data: {ex.Message}");
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public string Decrypt(string? encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            return string.Empty;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException ex)
        {
            _logger.LogError($"Invalid Base64 format for decryption: {ex.Message}");
            return string.Empty;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError($"Failed to decrypt data: {ex.Message}");
            return string.Empty;
        }
    }
}
