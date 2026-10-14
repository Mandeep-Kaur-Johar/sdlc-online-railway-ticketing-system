using Microsoft.EntityFrameworkCore;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Data;
using RailwayTicketing.Api.Domain;

namespace RailwayTicketing.Api.Services;

/// <summary>
/// Implements User Story 3010 - support requests are logged, tracked and
/// updated until closure with status visible to the passenger.
/// </summary>
public class SupportService : ISupportService
{
    private readonly RailwayDbContext _db;

    public SupportService(RailwayDbContext db)
    {
        _db = db;
    }

    public async Task<SupportRequestDto> CreateAsync(CreateSupportRequest request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new DomainException("INVALID_SUPPORT_REQUEST", "UserId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Description))
        {
            throw new DomainException("INVALID_SUPPORT_REQUEST", "Subject and description are required.");
        }

        if (!string.IsNullOrWhiteSpace(request.BookingReference))
        {
            var exists = await _db.Bookings.AnyAsync(b => b.BookingReference == request.BookingReference, ct);
            if (!exists)
            {
                throw new DomainException("BOOKING_NOT_FOUND", $"Booking {request.BookingReference} was not found.", 404);
            }
        }

        var entity = new SupportRequest
        {
            Reference = "SR-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            UserId = request.UserId.Trim(),
            BookingReference = request.BookingReference,
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim(),
            Status = SupportStatus.Open,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        entity.Updates.Add(new SupportUpdate
        {
            Status = SupportStatus.Open,
            Note = "Support request logged.",
            Author = "System",
            CreatedUtc = DateTime.UtcNow
        });

        _db.SupportRequests.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<IReadOnlyList<SupportRequestDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        var items = await _db.SupportRequests
            .Include(s => s.Updates)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedUtc)
            .ToListAsync(ct);

        return items.Select(Map).ToList();
    }

    public async Task<SupportRequestDto> GetAsync(string reference, CancellationToken ct = default)
    {
        var entity = await _db.SupportRequests
            .Include(s => s.Updates)
            .FirstOrDefaultAsync(s => s.Reference == reference, ct)
            ?? throw new DomainException("SUPPORT_REQUEST_NOT_FOUND", $"Support request {reference} was not found.", 404);

        return Map(entity);
    }

    public async Task<SupportRequestDto> UpdateAsync(string reference, SupportUpdateRequest request, CancellationToken ct = default)
    {
        var entity = await _db.SupportRequests
            .Include(s => s.Updates)
            .FirstOrDefaultAsync(s => s.Reference == reference, ct)
            ?? throw new DomainException("SUPPORT_REQUEST_NOT_FOUND", $"Support request {reference} was not found.", 404);

        if (entity.Status == SupportStatus.Closed)
        {
            throw new DomainException("INVALID_STATE", "A closed support request cannot be updated.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Note))
        {
            throw new DomainException("INVALID_SUPPORT_UPDATE", "An update note is required.");
        }

        entity.Status = request.Status;
        entity.UpdatedUtc = DateTime.UtcNow;

        if (request.Status is SupportStatus.Resolved or SupportStatus.Closed)
        {
            entity.Resolution = request.Resolution ?? request.Note;
        }

        entity.Updates.Add(new SupportUpdate
        {
            SupportRequestId = entity.Id,
            Status = request.Status,
            Note = request.Note.Trim(),
            Author = "SupportAgent",
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    private static SupportRequestDto Map(SupportRequest s) => new(
        s.Reference,
        s.UserId,
        s.BookingReference,
        s.Subject,
        s.Description,
        s.Status.ToString(),
        s.Resolution,
        s.CreatedUtc,
        s.UpdatedUtc,
        s.Updates
            .OrderBy(u => u.CreatedUtc)
            .Select(u => new SupportUpdateDto(u.Status.ToString(), u.Note, u.Author, u.CreatedUtc))
            .ToList());
}
