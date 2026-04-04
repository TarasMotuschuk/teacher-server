namespace Teacher.Common.Vnc;

public sealed record VncFrameCapture(int Width, int Height, int Stride, byte[] Pixels);
