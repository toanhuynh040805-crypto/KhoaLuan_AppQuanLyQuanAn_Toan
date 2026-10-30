namespace QuanAnBackend.Services;

public interface IPaymentProvider
{
    bool IsConfigured { get; }

    Task<PaymentInstructions> CreateTransferInstructionsAsync(
        decimal amount,
        string reference,
        CancellationToken cancellationToken = default);

    Task<VerifiedPaymentNotification> VerifyNotificationAsync(
        ReadOnlyMemory<byte> requestBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentInstructions(
    string ProviderReference,
    string QrContent,
    decimal Amount);

public sealed record VerifiedPaymentNotification(
    string ProviderTransactionId,
    string ProviderReference,
    string ReceivingAccount,
    decimal Amount);

public sealed class UnconfiguredPaymentProvider : IPaymentProvider
{
    public bool IsConfigured => false;

    public Task<PaymentInstructions> CreateTransferInstructionsAsync(
        decimal amount,
        string reference,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Chưa cấu hình nhà cung cấp thanh toán có khả năng đối soát giao dịch.");
    }

    public Task<VerifiedPaymentNotification> VerifyNotificationAsync(
        ReadOnlyMemory<byte> requestBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Chưa cấu hình cơ chế xác minh chữ ký/thông báo thanh toán của nhà cung cấp.");
    }
}
