namespace Teacher.Common.Vnc;

/// <param name="Pixels">BGRA (B,G,R,A per pixel), same layout as 32bpp ARGB bitmap memory and Avalonia Bgra8888.</param>
public sealed record VncFrameCapture(int Width, int Height, int Stride, byte[] Pixels);
