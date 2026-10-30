namespace QuanAnBackend.Services;

public enum WebReviewModerationDecision
{
    Public,
    Pending,
    Rejected,
    Hidden
}

public sealed record WebReviewClassification(
    WebReviewModerationDecision Decision,
    string? InternalReason = null);

public interface IWebReviewContentClassifier
{
    WebReviewClassification Classify(string? comment);
}
