namespace Robentex.Fanuc.OpcUa;

/// <summary>Raw PR[] configuration fields in the documented PR[n]@19.8 order.</summary>
public sealed record FanucPositionRegisterConfiguration(
    short Flip,
    short Left,
    short Up,
    short Front,
    short Turn4,
    short Turn5,
    short Turn6,
    short ValidCartesian);
