using System.Globalization;

namespace Robentex.Fanuc.OpcUa;

/// <summary>A connection-scoped FANUC Holding Registers dynamic assignment.</summary>
public sealed record FanucDynamicAssignment(int StartAddress, int Size, string VariableName, decimal? Multiply = null)
{
    public int EndAddress => checked(StartAddress + Size - 1);

    internal string ToSetAsgCommand()
    {
        if (StartAddress is < 1 or > 16384)
            throw new ArgumentOutOfRangeException(nameof(StartAddress), "Holding Register addresses must be in the range 1 through 16384.");
        if (Size is < 1 or > 16384 || EndAddress > 16384)
            throw new ArgumentOutOfRangeException(nameof(Size), "The assignment must end at or before Holding Register address 16384.");
        if (string.IsNullOrWhiteSpace(VariableName) || VariableName.Any(char.IsWhiteSpace))
            throw new ArgumentException("A FANUC variable name without whitespace is required.", nameof(VariableName));

        var command = $"SETASG {StartAddress} {Size} {VariableName}";
        if (Multiply.HasValue)
            command += " " + Multiply.Value.ToString(CultureInfo.InvariantCulture);
        if (command.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(VariableName), "A FANUC Command must not exceed 128 characters.");
        return command;
    }
}
