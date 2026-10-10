namespace BlueH.Models;

public sealed record BluetoothAudioDevice(
    Guid ContainerId,
    string Name,
    BluetoothAudioDeviceKind Kind,
    bool? IsConnected)
{
    public static IReadOnlyList<BluetoothAudioDevice> FromEndpoints(IEnumerable<BluetoothAudioEndpoint> endpoints)
    {
        return endpoints
            .GroupBy(endpoint => endpoint.ContainerId)
            .Select(FromEndpointGroup)
            .ToArray();
    }

    private static BluetoothAudioDevice FromEndpointGroup(IEnumerable<BluetoothAudioEndpoint> group)
    {
        BluetoothAudioEndpoint[] endpoints = [.. group];

        return new BluetoothAudioDevice(
            endpoints[0].ContainerId,
            NameOf(endpoints),
            KindOf(endpoints),
            ConnectionStateOf(endpoints));
    }

    private static string NameOf(BluetoothAudioEndpoint[] endpoints)
    {
        return endpoints
            .Select(endpoint => endpoint.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty;
    }

    private static BluetoothAudioDeviceKind KindOf(BluetoothAudioEndpoint[] endpoints)
    {
        return endpoints
            .Select(endpoint => BluetoothAudioDeviceKinds.FromMinorClass(endpoint.MinorClass))
            .FirstOrDefault(kind => kind != BluetoothAudioDeviceKind.Unknown, BluetoothAudioDeviceKind.Unknown);
    }

    private static bool? ConnectionStateOf(BluetoothAudioEndpoint[] endpoints)
    {
        bool?[] states = [.. endpoints.Select(endpoint => endpoint.IsConnected)];

        if (states.Any(state => state == true))
        {
            return true;
        }

        return states.Any(state => state == false) ? false : null;
    }
}
