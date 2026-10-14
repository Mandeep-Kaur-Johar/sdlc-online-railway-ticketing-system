using RailwayTicketing.Api.Contracts;

namespace RailwayTicketing.Api.Services;

public interface ISupportService
{
    Task<SupportRequestDto> CreateAsync(CreateSupportRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<SupportRequestDto>> ListAsync(string userId, CancellationToken ct = default);

    Task<SupportRequestDto> GetAsync(string reference, CancellationToken ct = default);

    Task<SupportRequestDto> UpdateAsync(string reference, SupportUpdateRequest request, CancellationToken ct = default);
}
