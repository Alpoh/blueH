namespace BlueH.Vendors.Xiaomi;

public sealed record XiaomiFrame(byte Type, byte Opcode, byte[] Body);
