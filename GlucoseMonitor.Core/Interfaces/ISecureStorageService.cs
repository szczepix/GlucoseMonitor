namespace GlucoseMonitor.Core.Interfaces;

/// <summary>
/// Service for encrypting and decrypting sensitive data.
/// Uses Windows DPAPI for secure storage with user-specific encryption.
/// </summary>
public interface ISecureStorageService
{
    /// <summary>
    /// Encrypts plain text using DPAPI with current user scope.
    /// </summary>
    /// <param name="plainText">The text to encrypt.</param>
    /// <returns>Base64-encoded encrypted data, or empty string if input is null/empty.</returns>
    string Encrypt(string? plainText);

    /// <summary>
    /// Decrypts Base64-encoded encrypted data using DPAPI.
    /// </summary>
    /// <param name="encryptedText">The Base64-encoded encrypted data.</param>
    /// <returns>The decrypted plain text, or empty string if input is null/empty or decryption fails.</returns>
    string Decrypt(string? encryptedText);
}
