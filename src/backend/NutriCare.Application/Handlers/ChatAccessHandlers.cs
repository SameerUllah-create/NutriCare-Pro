namespace NutriCare.Application.Handlers.Chat;

using MediatR;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Chat;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Domain.Exceptions;

public class EnsureChatAccessQueryHandler(
    IRepository<User> users,
    IRepository<Patient> patients,
    IRepository<Nutritionist> nutritionists,
    IRepository<Appointment> appointments,
    IRepository<DietPlan> plans, IRepository<ClinicalAssignment> assignments) : IRequestHandler<EnsureChatAccessQuery, bool>
{
    public async Task<bool> Handle(EnsureChatAccessQuery request, CancellationToken cancellationToken)
    {
        await ChatHandlerHelpers.EnsureCanChatAsync(users, patients, nutritionists, appointments, plans, assignments, request.CurrentUserId, request.OtherUserId);
        return true;
    }
}

public class GetChatContactsQueryHandler(
    IRepository<User> users,
    IRepository<Patient> patients,
    IRepository<Nutritionist> nutritionists,
    IRepository<Appointment> appointments,
    IRepository<DietPlan> plans, IRepository<ClinicalAssignment> assignments) : IRequestHandler<GetChatContactsQuery, IEnumerable<ChatContactDto>>
{
    public async Task<IEnumerable<ChatContactDto>> Handle(GetChatContactsQuery request, CancellationToken cancellationToken)
    {
        var ownPatients = (await patients.GetAsync(q => q.Where(p => p.UserId == request.CurrentUserId && p.DeletedAt == null))).Select(p => p.Id).ToArray();
        var ownClinicians = (await nutritionists.GetAsync(q => q.Where(p => p.UserId == request.CurrentUserId && p.DeletedAt == null))).Select(p => p.Id).ToArray();
        var now = DateTime.UtcNow;
        var assigned = await assignments.GetAsync(q => q.Where(a => a.DeletedAt == null && a.StartedAt <= now && (a.EndedAt == null || a.EndedAt > now) && (ownPatients.Contains(a.PatientId) || ownClinicians.Contains(a.NutritionistId))));
        var appointmentsForUser = await appointments.GetAsync(q => q.Where(a => a.DeletedAt == null && a.Status != "Cancelled" && a.Status != "Rejected" && (ownPatients.Contains(a.PatientId) || ownClinicians.Contains(a.NutritionistId))));
        var plansForUser = await plans.GetAsync(q => q.Where(p => p.DeletedAt == null && (ownPatients.Contains(p.PatientId) || ownClinicians.Contains(p.NutritionistId))));
        var patientIds = appointmentsForUser.Where(a => ownClinicians.Contains(a.NutritionistId)).Select(a => a.PatientId)
            .Concat(plansForUser.Where(p => ownClinicians.Contains(p.NutritionistId)).Select(p => p.PatientId)).Concat(assigned.Where(a => ownClinicians.Contains(a.NutritionistId)).Select(a => a.PatientId)).Distinct().ToArray();
        var clinicianIds = appointmentsForUser.Where(a => ownPatients.Contains(a.PatientId)).Select(a => a.NutritionistId)
            .Concat(plansForUser.Where(p => ownPatients.Contains(p.PatientId)).Select(p => p.NutritionistId)).Concat(assigned.Where(a => ownPatients.Contains(a.PatientId)).Select(a => a.NutritionistId)).Distinct().ToArray();
        var patientContacts = await patients.GetAsync(q => q.Where(p => patientIds.Contains(p.Id) && p.DeletedAt == null));
        var clinicianContacts = await nutritionists.GetAsync(q => q.Where(n => clinicianIds.Contains(n.Id) && n.DeletedAt == null));
        var contactIds = patientContacts.Select(p => p.UserId).Concat(clinicianContacts.Select(n => n.UserId)).Distinct().ToArray();
        var contacts = await users.GetAsync(q => q.Where(u => contactIds.Contains(u.Id) && u.IsActive && u.DeletedAt == null));
        var allowed = new List<ChatContactDto>();
        foreach (var contact in contacts)
        {
            try
            {
                await ChatHandlerHelpers.EnsureCanChatAsync(users, patients, nutritionists, appointments, plans, assignments, request.CurrentUserId, contact.Id);
                allowed.Add(new ChatContactDto(contact.Id, $"{contact.FirstName} {contact.LastName}".Trim()));
            }
            catch (ForbiddenAccessException) { }
        }
        return allowed.OrderBy(u => u.FullName).ToArray();
    }
}
