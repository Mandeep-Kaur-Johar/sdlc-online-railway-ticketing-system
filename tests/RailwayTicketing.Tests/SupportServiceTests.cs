using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Domain;
using RailwayTicketing.Api.Services;
using Xunit;

namespace RailwayTicketing.Tests;

/// <summary>User Story 3010 - support requests are logged, tracked and updated until closure.</summary>
public class SupportServiceTests
{
    [Fact]
    public async Task Support_requests_are_logged_with_a_reference_and_open_status()
    {
        using var harness = new TestHarness();

        var created = await harness.Support.CreateAsync(
            new CreateSupportRequest("user-001", null, "Refund not received", "My refund has not arrived after 5 days."));

        Assert.StartsWith("SR-", created.Reference);
        Assert.Equal(nameof(SupportStatus.Open), created.Status);
        Assert.Single(created.Updates);
    }

    [Fact]
    public async Task Support_requests_are_tracked_through_to_closure()
    {
        using var harness = new TestHarness();
        var created = await harness.Support.CreateAsync(
            new CreateSupportRequest("user-001", null, "Seat change", "Please move my seat."));

        await harness.Support.UpdateAsync(created.Reference, new SupportUpdateRequest(SupportStatus.InProgress, "Agent assigned.", null));
        var closed = await harness.Support.UpdateAsync(created.Reference, new SupportUpdateRequest(SupportStatus.Closed, "Seat changed.", "Seat moved to 3A-7."));

        Assert.Equal(nameof(SupportStatus.Closed), closed.Status);
        Assert.Equal("Seat moved to 3A-7.", closed.Resolution);
        Assert.Equal(3, closed.Updates.Count);
    }

    [Fact]
    public async Task Closed_support_requests_cannot_be_updated_again()
    {
        using var harness = new TestHarness();
        var created = await harness.Support.CreateAsync(
            new CreateSupportRequest("user-001", null, "Query", "Question about fare."));
        await harness.Support.UpdateAsync(created.Reference, new SupportUpdateRequest(SupportStatus.Closed, "Answered.", "Answered."));

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => harness.Support.UpdateAsync(created.Reference, new SupportUpdateRequest(SupportStatus.Open, "Reopen", null)));

        Assert.Equal("INVALID_STATE", ex.Code);
    }

    [Fact]
    public async Task Support_request_for_an_unknown_booking_is_rejected()
    {
        using var harness = new TestHarness();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => harness.Support.CreateAsync(new CreateSupportRequest("user-001", "RTS-NOTREAL", "Issue", "Details")));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Passengers_can_list_their_own_support_requests()
    {
        using var harness = new TestHarness();
        await harness.Support.CreateAsync(new CreateSupportRequest("user-001", null, "A", "Detail A"));
        await harness.Support.CreateAsync(new CreateSupportRequest("user-002", null, "B", "Detail B"));

        var list = await harness.Support.ListAsync("user-001");

        Assert.Single(list);
    }
}
