namespace Robentex.Fanuc.OpcUa;

/// <summary>A 1-based, inclusive FANUC Holding Register address range.</summary>
public readonly record struct FanucHoldingRegisterRange(int StartAddress, int EndAddress)
{
    internal void Validate(string parameterName)
    {
        if (StartAddress is < 1 or > 16384 || EndAddress < StartAddress || EndAddress > 16384)
            throw new ArgumentOutOfRangeException(parameterName, "Holding Register ranges must be within 1 through 16384 and have an inclusive end not below the start.");
    }

    internal bool Overlaps(FanucDynamicAssignment assignment) =>
        StartAddress <= assignment.EndAddress && assignment.StartAddress <= EndAddress;
}
