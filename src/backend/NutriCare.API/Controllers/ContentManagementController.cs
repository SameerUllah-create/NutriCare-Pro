namespace NutriCare.API.Controllers;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

[ApiController, Route("api/contact")]
public class ContactController(NutriCareDbContext db) : ControllerBase
{
    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Contact(ContactRequest input, CancellationToken ct)
    {
        // The hidden website field traps simple form bots without disclosing the trap.
        if (string.IsNullOrWhiteSpace(input.Website))
        {
            db.ContactInquiries.Add(new ContactInquiry { Name = input.Name.Trim(), Email = input.Email.Trim(), Message = input.Message.Trim() });
            await db.SaveChangesAsync(ct);
        }
        return Accepted(ApiResponse<object>.SuccessResponse(new { received = true }, "Your inquiry has been received",202));
    }
}

[ApiController, Route("api/management"), Authorize(Roles = "Admin,Nutritionist")]
public class ContentManagementController(NutriCareDbContext db, ICurrentUserService current) : ControllerBase
{
    [HttpGet("content")]
    public async Task<IActionResult> Content(CancellationToken ct) => Ok(ApiResponse<object>.SuccessResponse(await db.PublicContents.AsNoTracking().Where(c => c.DeletedAt == null).OrderByDescending(c => c.CreatedAt).ToListAsync(ct)));

    [HttpPost("content")]
    public async Task<IActionResult> Create(ContentRequest input, CancellationToken ct)
    {
        ValidateKind(input.Kind);
        var row = new PublicContent(); Apply(row,input); db.Add(row); await db.SaveChangesAsync(ct);
        return StatusCode(201,ApiResponse<object>.SuccessResponse(row,"Draft content saved",201));
    }

    [HttpPut("content/{id:long}")]
    public async Task<IActionResult> Update(long id, ContentRequest input, CancellationToken ct)
    {
        var row = await FindContent(id,ct); Concurrency(row,input.RowVersion);
        if (row.IsPublished) throw new ConflictException("Unpublish this content before editing");
        ValidateKind(input.Kind); Apply(row,input); await db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.SuccessResponse(row));
    }

    [HttpPost("content/{id:long}/publish")]
    public async Task<IActionResult> Publish(long id, PublicationRequest input, CancellationToken ct)
    {
        var row = await FindContent(id,ct); Concurrency(row,input.RowVersion);
        if (!input.ApprovalConfirmed) throw new ArgumentException("Explicit approval is required");
        if (string.IsNullOrWhiteSpace(row.TitleUrdu) || string.IsNullOrWhiteSpace(row.ContentUrdu)) throw new ArgumentException("Approved English and Urdu content is required");
        if (row.Kind is "Testimonial" or "SuccessStory" && string.IsNullOrWhiteSpace(row.ConsentReference)) throw new ArgumentException("A valid consent reference is required");
        if (row.Kind == "Credentials" && string.IsNullOrWhiteSpace(row.VerificationReference)) throw new ArgumentException("Credential verification evidence is required");
        row.IsPublished = true; row.ApprovedAt = DateTime.UtcNow; row.ApprovedByUserId = current.UserId;
        await db.SaveChangesAsync(ct); return Ok(ApiResponse<object>.SuccessResponse(row));
    }

    [HttpPost("content/{id:long}/unpublish")]
    public async Task<IActionResult> Unpublish(long id, PublicationRequest input, CancellationToken ct)
    {
        var row = await FindContent(id,ct); Concurrency(row,input.RowVersion); row.IsPublished=false;
        await db.SaveChangesAsync(ct); return Ok(ApiResponse<object>.SuccessResponse(row));
    }

    [HttpGet("inquiries")]
    public async Task<IActionResult> Inquiries(CancellationToken ct) => Ok(ApiResponse<object>.SuccessResponse(await db.ContactInquiries.AsNoTracking().OrderByDescending(i => i.CreatedAt).Take(200).ToListAsync(ct)));

    [HttpPut("inquiries/{id:long}")]
    public async Task<IActionResult> Reply(long id, InquiryReply input, CancellationToken ct)
    {
        var row = await db.ContactInquiries.FindAsync([id],ct) ?? throw new EntityNotFoundException("Inquiry",id);
        if (input.Status is not ("Received" or "InProgress" or "Resolved")) throw new ArgumentException("Invalid inquiry status");
        row.Reply = input.Reply; row.Status = input.Status; row.RepliedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return Ok(ApiResponse<object>.SuccessResponse(row,"Reply recorded; send through an approved contact channel"));
    }

    [HttpGet("feedback")]
    public async Task<IActionResult> Feedback(CancellationToken ct) => Ok(ApiResponse<object>.SuccessResponse(await db.PatientFeedbacks.AsNoTracking().Where(f => f.DeletedAt == null).OrderByDescending(f => f.CreatedAt).ToListAsync(ct)));

    [HttpPost("feedback/{id:long}/moderate")]
    public async Task<IActionResult> Moderate(long id, ModerationRequest input, CancellationToken ct)
    {
        var row = await db.PatientFeedbacks.FindAsync([id],ct) ?? throw new EntityNotFoundException("Feedback",id); Concurrency(row,input.RowVersion);
        if (input.Status is not ("Approved" or "Rejected")) throw new ArgumentException("Invalid moderation status");
        if (input.Status == "Approved" && (!row.PublicationConsent || row.WithdrawnAt != null)) throw new ConflictException("Publication consent is absent or withdrawn");
        row.Status=input.Status; row.ModeratedAt=DateTime.UtcNow; row.ModeratedByUserId=current.UserId;
        await db.SaveChangesAsync(ct); return Ok(ApiResponse<object>.SuccessResponse(row));
    }

    private async Task<PublicContent> FindContent(long id,CancellationToken ct) => await db.PublicContents.SingleOrDefaultAsync(c => c.Id == id && c.DeletedAt == null,ct) ?? throw new EntityNotFoundException("Content",id);
    internal static void Concurrency(BaseEntity entity, byte[]? expected) { if (expected is not { Length: >0 } || !expected.SequenceEqual(entity.RowVersion)) throw new ConflictException("Record changed; reload before saving"); }
    private static void ValidateKind(string kind) { if (kind is not ("FAQ" or "About" or "Credentials" or "Contact" or "SuccessStory" or "Testimonial" or "Article" or "Privacy")) throw new ArgumentException("Invalid content kind"); }
    private static void Apply(PublicContent row,ContentRequest input) { row.Kind=input.Kind;row.Title=input.Title;row.TitleUrdu=input.TitleUrdu;row.Content=input.Content;row.ContentUrdu=input.ContentUrdu;row.ConsentReference=input.ConsentReference;row.VerificationReference=input.VerificationReference; }
}

[ApiController, Route("api/feedback"), Authorize(Roles="Patient")]
public class FeedbackController(NutriCareDbContext db, IClinicalAccessService access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) { var id=await access.GetPatientIdAsync(ct);return Ok(ApiResponse<object>.SuccessResponse(await db.PatientFeedbacks.AsNoTracking().Where(f=>f.PatientId==id).ToListAsync(ct))); }

    [HttpPost]
    public async Task<IActionResult> Create(FeedbackRequest input,CancellationToken ct)
    {
        var id=await access.GetPatientIdAsync(ct);
        if (input.AppointmentId.HasValue && !await db.Appointments.AnyAsync(a=>a.Id==input.AppointmentId && a.PatientId==id && a.Status=="Completed" && a.DeletedAt==null,ct)) throw new ForbiddenAccessException("Feedback requires your completed appointment");
        var row=new PatientFeedback { PatientId=id,AppointmentId=input.AppointmentId,Rating=input.Rating,Content=input.Content,PublicationConsent=input.PublicationConsent,ConsentedAt=input.PublicationConsent?DateTime.UtcNow:null };
        db.Add(row);await db.SaveChangesAsync(ct);return StatusCode(201,ApiResponse<object>.SuccessResponse(row,"Feedback submitted for review",201));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Withdraw(long id,CancellationToken ct)
    {
        var patientId=await access.GetPatientIdAsync(ct);var row=await db.PatientFeedbacks.SingleOrDefaultAsync(f=>f.Id==id && f.PatientId==patientId,ct)??throw new EntityNotFoundException("Feedback",id);
        row.PublicationConsent=false;row.WithdrawnAt=DateTime.UtcNow;row.Status="Withdrawn";
        await db.SaveChangesAsync(ct);return Ok(ApiResponse<object>.SuccessResponse(row));
    }
}

public class ContactRequest
{
    [Required,StringLength(100,MinimumLength=1)] public string Name { get; set; }="";
    [Required,EmailAddress,StringLength(254)] public string Email { get; set; }="";
    [Required,StringLength(5000,MinimumLength=10)] public string Message { get; set; }="";
    [StringLength(200)] public string? Website { get; set; }
}
public class PublicationRequest { public bool ApprovalConfirmed { get;set; } public byte[]? RowVersion { get;set; } }
public class ContentRequest
{
    [Required] public string Kind {get;set;}="FAQ";
    [Required,StringLength(200)] public string Title {get;set;}="";
    [StringLength(200)] public string? TitleUrdu {get;set;}
    [Required,StringLength(50000)] public string Content {get;set;}="";
    [StringLength(50000)] public string? ContentUrdu {get;set;}
    [StringLength(500)] public string? ConsentReference {get;set;}
    [StringLength(500)] public string? VerificationReference {get;set;}
    public byte[]? RowVersion {get;set;}
}
public class InquiryReply { [Required,StringLength(5000)] public string Reply {get;set;}="";public string Status {get;set;}="Resolved"; }
public class ModerationRequest { public string Status {get;set;}="Rejected"; public byte[]? RowVersion {get;set;} }
public class FeedbackRequest { public long? AppointmentId {get;set;} [Range(1,5)] public int Rating {get;set;} [Required,StringLength(3000)] public string Content {get;set;}=""; public bool PublicationConsent {get;set;} }
