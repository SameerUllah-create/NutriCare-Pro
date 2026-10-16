namespace NutriCare.Application.Services;

using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public sealed class ClinicalConsentService(IRepository<ConsentRecord> consents, IRepository<PolicyDocument> policies) : IClinicalConsentService
{
    public async Task<ConsentRecord> RequireAsync(long userId, string purpose, long? consentRecordId = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var grants = await consents.GetAsync(q => q.Where(c => c.UserId == userId && c.Purpose == purpose && c.WithdrawnAt == null &&
            c.DeletedAt == null && (!consentRecordId.HasValue || c.Id == consentRecordId.Value)).OrderByDescending(c => c.GrantedAt));
        var documents = (await policies.GetAsync(q => q.Where(p => p.Purpose == purpose && p.IsPublished && p.DeletedAt == null &&
            (!p.EffectiveFrom.HasValue || p.EffectiveFrom <= now) && (!p.EffectiveTo.HasValue || p.EffectiveTo > now)))).ToArray();
        if (documents.Length == 0) throw new ConflictException("The clinic has not published an effective consent policy for this action");
        var grant = grants.FirstOrDefault(c => documents.Any(p => p.Version == c.PolicyVersion && p.Content == c.ConsentText &&
            (c.PresentedLanguage != "Urdu" || (!string.IsNullOrWhiteSpace(p.ContentUrdu) && p.ContentUrdu == c.ConsentTextUrdu))));
        return grant ?? throw new ForbiddenAccessException("Review and accept the current health-data or teleconsultation policy before this action");
    }
}
