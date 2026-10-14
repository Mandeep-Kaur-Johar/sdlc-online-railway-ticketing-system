using RailwayTicketing.Api.Contracts;

namespace RailwayTicketing.Api.Services;

public interface IPassengerProfileService
{
    Task<PassengerProfileDto> CreateAsync(PassengerProfileRequest request, CancellationToken ct = default);

    Task<PassengerProfileDto> UpdateAsync(int id, PassengerProfileRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<PassengerProfileDto>> ListAsync(string userId, CancellationToken ct = default);

    Task<PassengerProfileDto> GetAsync(int id, CancellationToken ct = default);

    Task DeactivateAsync(int id, CancellationToken ct = default);
}
