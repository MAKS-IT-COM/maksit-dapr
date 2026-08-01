#pragma warning disable DAPR_CRYPTOGRAPHY

using Microsoft.Extensions.Logging;
using Dapr.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Dapr cryptography building block with <see cref="Result"/> outcomes.
/// </summary>
public interface IDaprCryptographyService {
  /// <summary>
  /// Encrypts plaintext bytes.
  /// </summary>
  Task<Result<ReadOnlyMemory<byte>>> EncryptAsync(
    string componentName,
    ReadOnlyMemory<byte> plaintext,
    string keyName,
    EncryptionOptions options,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Decrypts ciphertext bytes.
  /// </summary>
  Task<Result<ReadOnlyMemory<byte>>> DecryptAsync(
    string componentName,
    ReadOnlyMemory<byte> ciphertext,
    string keyName,
    DecryptionOptions? options = null,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprCryptographyService"/>.
/// </summary>
public class DaprCryptographyService(
  ILogger<DaprCryptographyService> logger,
  DaprClient client
) : IDaprCryptographyService {
  private const string ErrorMessage = "MaksIT.Dapr - Cryptography error";

  /// <inheritdoc />
  public async Task<Result<ReadOnlyMemory<byte>>> EncryptAsync(
    string componentName,
    ReadOnlyMemory<byte> plaintext,
    string keyName,
    EncryptionOptions options,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(componentName) || string.IsNullOrWhiteSpace(keyName))
      return Result<ReadOnlyMemory<byte>>.BadRequest(default, "componentName and keyName are required.");
    if (options is null)
      return Result<ReadOnlyMemory<byte>>.BadRequest(default, "options are required.");

    try {
      var ciphertext = await client.EncryptAsync(componentName, plaintext, keyName, options, cancellationToken);
      return Result<ReadOnlyMemory<byte>>.Ok(ciphertext);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<ReadOnlyMemory<byte>>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<ReadOnlyMemory<byte>>> DecryptAsync(
    string componentName,
    ReadOnlyMemory<byte> ciphertext,
    string keyName,
    DecryptionOptions? options = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(componentName) || string.IsNullOrWhiteSpace(keyName))
      return Result<ReadOnlyMemory<byte>>.BadRequest(default, "componentName and keyName are required.");

    try {
      var plaintext = options is null
        ? await client.DecryptAsync(componentName, ciphertext, keyName, cancellationToken)
        : await client.DecryptAsync(componentName, ciphertext, keyName, options, cancellationToken);
      return Result<ReadOnlyMemory<byte>>.Ok(plaintext);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<ReadOnlyMemory<byte>>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }
}

#pragma warning restore DAPR_CRYPTOGRAPHY
