namespace Aenaon.Infrastructure.Fiscal;

/// <summary>Πλατφορμική εξουσιοδότηση ελέγχου πηγών, ανεξάρτητη από ρόλους Tenant Owner.</summary>
public interface IFiscalStagingAuthorizationPolicy
{
    Task<bool> CanSubmitAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> CanReviewAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Εξ ορισμού δεν επιτρέπεται καμία υποβολή ή έγκριση χωρίς ειδική ενσωμάτωση RBAC.</summary>
public sealed class DenyAllFiscalStagingAuthorizationPolicy : IFiscalStagingAuthorizationPolicy
{
    public Task<bool> CanSubmitAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> CanReviewAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
