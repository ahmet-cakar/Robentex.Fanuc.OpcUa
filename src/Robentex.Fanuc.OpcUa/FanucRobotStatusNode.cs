using Opc.Ua;

namespace Robentex.Fanuc.OpcUa;

/// <summary>A read-only OPC UA status-node discovery result. Missing nodes have <see cref="Found"/> set to false.</summary>
public sealed record FanucRobotStatusNode(
    string Name,
    bool Found,
    string? BrowsePath = null,
    string? NodeId = null,
    string? DataTypeNodeId = null,
    string? RuntimeValueType = null,
    object? Value = null,
    StatusCode? ValueStatusCode = null,
    StatusCode? DataTypeStatusCode = null);
