namespace Robentex.Fanuc.OpcUa;

/// <summary>
/// Cartesian PR[] members. Null Cartesian members request a partial update.
/// CONFIG is optional. UF/UT remain modeled for caller intent but are not writable.
/// </summary>
public sealed record FanucPosition(
    double? X = null,
    double? Y = null,
    double? Z = null,
    double? W = null,
    double? P = null,
    double? R = null,
    FanucPositionConfiguration? Configuration = null,
    int? TurnJ1 = null,
    int? TurnJ2 = null,
    int? TurnJ3 = null,
    int? UserFrame = null,
    int? UserTool = null)
{
    /// <summary>Gets whether all six Cartesian components are specified.</summary>
    public bool HasCompleteCartesian => X.HasValue && Y.HasValue && Z.HasValue && W.HasValue && P.HasValue && R.HasValue;
    /// <summary>Gets whether at least one Cartesian component is specified.</summary>
    public bool HasAnyCartesian => X.HasValue || Y.HasValue || Z.HasValue || W.HasValue || P.HasValue || R.HasValue;
}

/// <summary>Cartesian FANUC CONFIG intent. LEFT is preserved when omitted.</summary>
public sealed record FanucPositionConfiguration(
    string? Flip = null,
    string? UpDown = null,
    string? TopBottom = null,
    short? Left = null,
    short? Turn4 = null,
    short? Turn5 = null,
    short? Turn6 = null);
