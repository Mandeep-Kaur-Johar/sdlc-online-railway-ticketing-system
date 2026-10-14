using Microsoft.EntityFrameworkCore;
using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Data;
using RailwayTicketing.Api.Domain;

namespace RailwayTicketing.Api.Services;

/// <summary>
/// Implements User Story 3004 - create, update and select saved passenger profiles.
/// </summary>
public class PassengerProfileService : IPassengerProfileService
{
    private readonly RailwayDbContext _db;

    public PassengerProfileService(RailwayDbContext db)
    {
        _db = db;
    }

    public async Task<PassengerProfileDto> CreateAsync(PassengerProfileRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var profile = new PassengerProfile
        {
            UserId = request.UserId.Trim(),
            FullName = request.FullName.Trim(),
            Age = request.Age,
            Gender = request.Gender.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            IdDocumentMasked = Mask(request.IdDocument),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _db.PassengerProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return Map(profile);
    }

    public async Task<PassengerProfileDto> UpdateAsync(int id, PassengerProfileRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var profile = await _db.PassengerProfiles.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainException("PROFILE_NOT_FOUND", $"Passenger profile {id} was not found.", 404);

        if (!string.Equals(profile.UserId, request.UserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("FORBIDDEN", "A passenger profile can only be updated by its owner.", 403);
        }

        profile.FullName = request.FullName.Trim();
        profile.Age = request.Age;
        profile.Gender = request.Gender.Trim();
        profile.Email = request.Email.Trim();
        profile.Phone = request.Phone.Trim();

        if (!string.IsNullOrWhiteSpace(request.IdDocument))
        {
            profile.IdDocumentMasked = Mask(request.IdDocument);
        }

        profile.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(profile);
    }

    public async Task<IReadOnlyList<PassengerProfileDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        var profiles = await _db.PassengerProfiles
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderBy(p => p.FullName)
            .ToListAsync(ct);

        return profiles.Select(Map).ToList();
    }

    public async Task<PassengerProfileDto> GetAsync(int id, CancellationToken ct = default)
    {
        var profile = await _db.PassengerProfiles.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainException("PROFILE_NOT_FOUND", $"Passenger profile {id} was not found.", 404);

        return Map(profile);
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        var profile = await _db.PassengerProfiles.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainException("PROFILE_NOT_FOUND", $"Passenger profile {id} was not found.", 404);

        profile.IsActive = false;
        profile.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(PassengerProfileRequest request)
    {
        if (request is null)
        {
            throw new DomainException("INVALID_PROFILE", "Passenger profile payload is required.");
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new DomainException("INVALID_PROFILE", "UserId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new DomainException("INVALID_PROFILE", "Passenger full name is required.");
        }

        if (request.Age < 0 || request.Age > 120)
        {
            throw new DomainException("INVALID_PROFILE", "Passenger age must be between 0 and 120.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            throw new DomainException("INVALID_PROFILE", "A valid contact email is required.");
        }
    }

    private static string Mask(string? idDocument)
    {
        if (string.IsNullOrWhiteSpace(idDocument))
        {
            return string.Empty;
        }

        var trimmed = idDocument.Trim();
        return trimmed.Length <= 4
            ? new string('X', trimmed.Length)
            : new string('X', trimmed.Length - 4) + trimmed[^4..];
    }

    private static PassengerProfileDto Map(PassengerProfile p) => new(
        p.Id, p.UserId, p.FullName, p.Age, p.Gender, p.Email, p.Phone, p.IdDocumentMasked, p.IsActive);
}
