// tools/check-luminance.csx — CIELAB L* (D65) comparator for two hex colors.
// Usage: dotnet script tools/check-luminance.csx 0x0A0F19 0x1A2540
// Exits 0 if ΔL* >= 8, 1 otherwise. No external dependencies.
using System;

static int Hex(string h) => Convert.ToInt32(h.TrimStart('#'), 16);
static int R(int v) => (v >> 16) & 0xFF;
static int G(int v) => (v >>  8) & 0xFF;
static int B(int v) =>  v        & 0xFF;

// sRGB → linear (inverse companding, D65)
static double SrgbToLinear(byte c)
{
    double cs = c / 255.0;
    return cs <= 0.04045 ? cs / 12.92 : Math.Pow((cs + 0.055) / 1.055, 2.4);
}

// Linear RGB → CIE L* (D65). Yn = 1.
static double LinearToLStar(double r, double g, double b)
{
    // sRGB D65 → XYZ
    double y = r * 0.2126729 + g * 0.7151522 + b * 0.0721750;
    // XYZ → L*
    const double eps  = 216.0 / 24389.0;       // (6/29)^3
    const double kap  = 24389.0 / 27.0;        // (29/3)^3
    double fy = y > eps ? Math.Pow(y, 1.0 / 3.0) : (kap * y + 16.0) / 116.0;
    return 116.0 * fy - 16.0;
}

static double RgbToLStar(int r, int g, int b) =>
    LinearToLStar(SrgbToLinear((byte)r), SrgbToLinear((byte)g), SrgbToLinear((byte)b));

if (Args.Count < 2)
{
    Console.Error.WriteLine("Usage: dotnet script tools/check-luminance.csx <hex1> <hex2>");
    Environment.Exit(2);
}

string a = Args[0], b = Args[1];
double la = RgbToLStar(R(Hex(a)), G(Hex(a)), B(Hex(a)));
double lb = RgbToLStar(R(Hex(b)), G(Hex(b)), B(Hex(b)));
double dL = Math.Abs(la - lb);
Console.WriteLine($"L*({a}) = {la:F2}  L*({b}) = {lb:F2}  ΔL* = {dL:F2}  (gate: 8.00)");
Environment.Exit(dL >= 8.0 ? 0 : 1);