namespace BlueH.Services;

public static class AudioEndpointIdReader
{
    private const string Marker = "MMDEVAPI#";
    private const char Separator = '#';

    public static string ExtractMmDeviceId(string deviceInterfaceId)
    {
        int markerStart = deviceInterfaceId.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);

        if (markerStart < 0)
        {
            return deviceInterfaceId;
        }

        string remainder = deviceInterfaceId[(markerStart + Marker.Length)..];
        int separator = remainder.IndexOf(Separator);

        return separator < 0 ? remainder : remainder[..separator];
    }
}
