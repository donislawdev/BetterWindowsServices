using Windows.Win32.Security;

namespace Bws.Core.Tests;

/// <summary>
/// Whether an elevated session has a twin of the same account running without elevation - security
/// report S-7, 2026-10-06. The window writes nothing into the profile when it does.
///
/// <b>Both facts, and the table says why.</b> Measured that day: a process under
/// <c>runas /trustlevel:0x20000</c> answers TokenElevationTypeFull while the role says no, so a rule
/// asking only for the type would call that process an administrator with a twin.
/// </summary>
public sealed class ElevationTwinTests
{
    [Theory]
    [InlineData(true, (int)TOKEN_ELEVATION_TYPE.TokenElevationTypeFull, true)]
    [InlineData(true, (int)TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault, false)]
    [InlineData(false, (int)TOKEN_ELEVATION_TYPE.TokenElevationTypeFull, false)]
    [InlineData(false, (int)TOKEN_ELEVATION_TYPE.TokenElevationTypeLimited, false)]
    [InlineData(true, null, true)]
    public void A_twin_needs_the_role_and_a_type_that_is_not_the_one_without_a_linked_token(
        bool elevated, int? type, bool twin)
    {
        // A type nobody could read counts as a twin - the safe side, because the answer to "twin" is
        // to write less.
        Assert.Equal(twin, Session.ElevatedWithTwin(elevated, (TOKEN_ELEVATION_TYPE?)type));
    }

    [Fact]
    public void The_type_of_this_process_can_be_read_and_never_makes_a_session_without_rights_a_twin()
    {
        // The interop itself, on whatever token runs the tests - elevated here, not on a build agent.
        Assert.NotNull(Session.ElevationType());
        Assert.True(Session.IsElevated() || !Session.IsElevatedWithTwin());
    }
}
