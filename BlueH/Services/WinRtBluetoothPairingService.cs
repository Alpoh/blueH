using System.Globalization;
using BlueH.Diagnostics;
using BlueH.Models;
using Windows.Devices.Enumeration;

namespace BlueH.Services;

public sealed class WinRtBluetoothPairingService : IBluetoothPairingService
{
    private const string PairOperation = "Pair Bluetooth device";
    private const string UnpairOperation = "Unpair Bluetooth device";
    private const string ChallengeOperation = "Answer pairing challenge";
    private const string UnsupportedChallengeDetail = "unsupported pairing ceremony";
    private const string NoPairedEndpointDetail = "no paired endpoint for the container";
    private const string ContainerSelectorFormat =
        "System.Devices.Aep.ContainerId:=\"{{{0}}}\" AND System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True";

    private const DevicePairingKinds SupportedCeremonies =
        DevicePairingKinds.ConfirmOnly
        | DevicePairingKinds.ConfirmPinMatch
        | DevicePairingKinds.DisplayPin
        | DevicePairingKinds.ProvidePin;

    private readonly IDiagnosticsLog _diagnosticsLog;

    public WinRtBluetoothPairingService(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    public IBluetoothDiscoveryWatcher CreateDiscoveryWatcher()
    {
        return new WinRtBluetoothDiscoveryWatcher(_diagnosticsLog);
    }

    public async Task<PairingResult> PairAsync(
        string endpointId,
        Func<PairingChallenge, CancellationToken, Task<PairingResponse>> respond,
        CancellationToken cancellationToken)
    {
        DeviceInformation device = await DeviceInformation.CreateFromIdAsync(endpointId).AsTask(cancellationToken);
        DeviceInformationCustomPairing custom = device.Pairing.Custom;

        void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            _ = AnswerChallengeAsync(args, respond, cancellationToken);
        }

        custom.PairingRequested += OnPairingRequested;

        try
        {
            DevicePairingResult result = await custom
                .PairAsync(SupportedCeremonies, DevicePairingProtectionLevel.Default)
                .AsTask(cancellationToken);
            PairingResult mapped = PairingStatusMapper.FromPairing(result.Status);

            _diagnosticsLog.Record(
                mapped.Outcome is PairingOutcome.Succeeded or PairingOutcome.AlreadyDone
                    ? DiagnosticOutcome.Success
                    : DiagnosticOutcome.Failure,
                PairOperation,
                $"{endpointId} {result.Status} protection={result.ProtectionLevelUsed}");

            return mapped;
        }
        finally
        {
            custom.PairingRequested -= OnPairingRequested;
        }
    }

    public async Task<PairingResult> UnpairAsync(Guid containerId, CancellationToken cancellationToken)
    {
        string selector = string.Format(CultureInfo.InvariantCulture, ContainerSelectorFormat, containerId);
        DeviceInformationCollection endpoints = await DeviceInformation
            .FindAllAsync(selector, Array.Empty<string>(), DeviceInformationKind.AssociationEndpoint)
            .AsTask(cancellationToken);

        if (endpoints.Count == 0)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Information, UnpairOperation, NoPairedEndpointDetail);

            return new PairingResult(PairingOutcome.AlreadyDone, NoPairedEndpointDetail);
        }

        PairingResult? combined = null;

        foreach (DeviceInformation endpoint in endpoints)
        {
            DeviceUnpairingResult result = await endpoint.Pairing.UnpairAsync().AsTask(cancellationToken);
            PairingResult mapped = PairingStatusMapper.FromUnpairing(result.Status);

            _diagnosticsLog.Record(
                mapped.Outcome == PairingOutcome.Failed ? DiagnosticOutcome.Failure : DiagnosticOutcome.Success,
                UnpairOperation,
                $"{endpoint.Id} {result.Status}");

            combined = combined is null || mapped.Outcome == PairingOutcome.Succeeded ? mapped : combined;
        }

        return combined!;
    }

    private async Task AnswerChallengeAsync(
        DevicePairingRequestedEventArgs args,
        Func<PairingChallenge, CancellationToken, Task<PairingResponse>> respond,
        CancellationToken cancellationToken)
    {
        Windows.Foundation.Deferral deferral = args.GetDeferral();

        try
        {
            if (PairingStatusMapper.ChallengeKindOf(args.PairingKind) is not { } kind)
            {
                _diagnosticsLog.Record(DiagnosticOutcome.Failure, ChallengeOperation, $"{UnsupportedChallengeDetail} {args.PairingKind}");

                return;
            }

            PairingResponse response = await respond(new PairingChallenge(kind, args.Pin), cancellationToken);

            if (!response.IsAccepted)
            {
                return;
            }

            if (kind == PairingChallengeKind.ProvidePin)
            {
                args.Accept(response.Pin ?? string.Empty);
            }
            else
            {
                args.Accept();
            }
        }
        catch (OperationCanceledException)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Information, ChallengeOperation, nameof(OperationCanceledException));
        }
        catch (Exception failure)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, ChallengeOperation, failure.Message);
        }
        finally
        {
            deferral.Complete();
        }
    }
}
