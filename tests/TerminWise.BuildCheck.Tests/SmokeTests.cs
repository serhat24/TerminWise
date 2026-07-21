using TerminWise.BuildCheck;
using Xunit;

namespace TerminWise.BuildCheck.Tests;

/// <summary>
/// Phase 0 smoke test — proves the `dotnet build` + `dotnet test` CI path is real and green.
/// Replaced by domain/architecture/integration tests in Phase 1.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void Marker_reports_phase_zero()
    {
        Assert.Equal("0", BuildCheckMarker.Phase);
    }

    [Fact]
    public void Describe_is_not_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(BuildCheckMarker.Describe()));
    }
}
