using RailwayTicketing.Api.Contracts;
using RailwayTicketing.Api.Services;
using Xunit;

namespace RailwayTicketing.Tests;

/// <summary>User Story 3004 - create, update and select saved passenger profiles.</summary>
public class PassengerProfileServiceTests
{
    private static PassengerProfileRequest Request(string name = "Asha Rao", string userId = "user-001") =>
        new(userId, name, 32, "Female", "asha@example.com", "+440000000001", "P1234567");

    [Fact]
    public async Task Profiles_are_created_with_a_masked_identity_document()
    {
        using var harness = new TestHarness();

        var created = await harness.Profiles.CreateAsync(Request());

        Assert.True(created.Id > 0);
        Assert.Equal("XXXX4567", created.IdDocumentMasked);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task Profiles_can_be_updated_by_their_owner()
    {
        using var harness = new TestHarness();
        var created = await harness.Profiles.CreateAsync(Request());

        var updated = await harness.Profiles.UpdateAsync(created.Id, Request("Asha R Rao"));

        Assert.Equal("Asha R Rao", updated.FullName);
    }

    [Fact]
    public async Task Profiles_cannot_be_updated_by_another_user()
    {
        using var harness = new TestHarness();
        var created = await harness.Profiles.CreateAsync(Request());

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => harness.Profiles.UpdateAsync(created.Id, Request("Hacker", "user-999")));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Saved_profiles_are_listed_for_selection_during_booking()
    {
        using var harness = new TestHarness();
        await harness.Profiles.CreateAsync(Request("Asha Rao"));
        await harness.Profiles.CreateAsync(Request("Bimal Rao"));

        var profiles = await harness.Profiles.ListAsync("user-001");

        Assert.Equal(2, profiles.Count);
    }

    [Fact]
    public async Task Deactivated_profiles_are_not_listed()
    {
        using var harness = new TestHarness();
        var created = await harness.Profiles.CreateAsync(Request());

        await harness.Profiles.DeactivateAsync(created.Id);
        var profiles = await harness.Profiles.ListAsync("user-001");

        Assert.Empty(profiles);
    }

    [Fact]
    public async Task Invalid_email_is_rejected()
    {
        using var harness = new TestHarness();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => harness.Profiles.CreateAsync(new PassengerProfileRequest("user-001", "Asha", 30, "Female", "not-an-email", "+44", "P1")));

        Assert.Equal("INVALID_PROFILE", ex.Code);
    }
}
