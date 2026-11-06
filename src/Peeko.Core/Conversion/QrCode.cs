using System.Text;

namespace Peeko.Core.Conversion;

/// <summary>
/// Pembuat kode QR kecil (ISO/IEC 18004): mode byte (UTF-8), koreksi galat M, versi 1–10 dipilih
/// otomatis, mask terbaik menurut aturan penalti standar. Cukup untuk tautan pendek seperti alamat
/// "Kirim dari HP"; tidak butuh pustaka luar.
/// </summary>
public sealed class QrCode
{
    // Per versi 1–10, level M: (kodeword koreksi per blok, [jumlah blok grup 1, data per blok], [grup 2…]).
    private static readonly (int Ec, int B1, int D1, int B2, int D2)[] BlocksM =
    [
        (10, 1, 16, 0, 0),
        (16, 1, 28, 0, 0),
        (26, 1, 44, 0, 0),
        (18, 2, 32, 0, 0),
        (24, 2, 43, 0, 0),
        (16, 4, 27, 0, 0),
        (18, 4, 31, 0, 0),
        (22, 2, 38, 2, 39),
        (22, 3, 36, 2, 37),
        (26, 4, 43, 1, 44),
    ];

    private static readonly int[][] AlignmentCenters =
    [
        [],
        [6, 18],
        [6, 22],
        [6, 26],
        [6, 30],
        [6, 34],
        [6, 22, 38],
        [6, 24, 42],
        [6, 26, 46],
        [6, 28, 50],
    ];

    private readonly bool[,] _dark;
    private readonly bool[,] _function;

    public int Version { get; }

    public int Size { get; }

    public bool this[int x, int y] => _dark[y, x];

    private QrCode(int version)
    {
        Version = version;
        Size = 17 + 4 * version;
        _dark = new bool[Size, Size];
        _function = new bool[Size, Size];
    }

    public static QrCode Encode(string text)
    {
        var data = Encoding.UTF8.GetBytes(text);
        for (int v = 1; v <= 10; v++)
        {
            var b = BlocksM[v - 1];
            int capacity = b.B1 * b.D1 + b.B2 * b.D2;
            int countBits = v <= 9 ? 8 : 16;
            if (4 + countBits + data.Length * 8 <= capacity * 8)
                return Build(v, data, capacity, countBits);
        }
        throw new ArgumentException("Teks terlalu panjang untuk kode QR", nameof(text));
    }

    private static QrCode Build(int version, byte[] data, int capacity, int countBits)
    {
        // ── Bit data: mode byte (0100), panjang, isi, terminator, padding ──
        var bits = new List<bool>();
        void Put(int value, int length)
        {
            for (int i = length - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
        }
        Put(0b0100, 4);
        Put(data.Length, countBits);
        foreach (var b in data) Put(b, 8);
        Put(0, Math.Min(4, capacity * 8 - bits.Count));
        while (bits.Count % 8 != 0) bits.Add(false);
        var codewords = new List<byte>();
        for (int i = 0; i < bits.Count; i += 8)
        {
            int v = 0;
            for (int j = 0; j < 8; j++) v = (v << 1) | (bits[i + j] ? 1 : 0);
            codewords.Add((byte)v);
        }
        for (int pad = 0; codewords.Count < capacity; pad++) codewords.Add(pad % 2 == 0 ? (byte)0xEC : (byte)0x11);

        // ── Blok + koreksi galat, lalu disisipkan bergantian ──
        var spec = BlocksM[version - 1];
        var blocks = new List<byte[]>();
        int offset = 0;
        for (int i = 0; i < spec.B1 + spec.B2; i++)
        {
            int len = i < spec.B1 ? spec.D1 : spec.D2;
            blocks.Add(codewords.GetRange(offset, len).ToArray());
            offset += len;
        }
        var ec = blocks.Select(b => ReedSolomon(b, spec.Ec)).ToList();
        var final = new List<byte>();
        int maxData = blocks.Max(b => b.Length);
        for (int i = 0; i < maxData; i++)
            foreach (var b in blocks)
                if (i < b.Length) final.Add(b[i]);
        for (int i = 0; i < spec.Ec; i++)
            foreach (var e in ec) final.Add(e[i]);

        // ── Matriks: pola tetap, data, lalu mask terbaik ──
        var qr = new QrCode(version);
        qr.DrawFunctionPatterns();
        qr.PlaceData(final);
        int bestMask = 0;
        int bestPenalty = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            qr.ApplyMask(mask);
            qr.DrawFormat(mask);
            int penalty = qr.Penalty();
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestMask = mask;
            }
            qr.ApplyMask(mask); // XOR lagi = kembali seperti semula
        }
        qr.ApplyMask(bestMask);
        qr.DrawFormat(bestMask);
        return qr;
    }

    // ── Pola tetap ─────────────────────────────────────────────────────────

    private void Set(int x, int y, bool dark)
    {
        _dark[y, x] = dark;
        _function[y, x] = true;
    }

    private void DrawFunctionPatterns()
    {
        for (int i = 0; i < Size; i++)
        {
            Set(6, i, i % 2 == 0);
            Set(i, 6, i % 2 == 0);
        }
        DrawFinder(3, 3);
        DrawFinder(Size - 4, 3);
        DrawFinder(3, Size - 4);

        var centers = AlignmentCenters[Version - 1];
        foreach (var cx in centers)
            foreach (var cy in centers)
            {
                bool nearFinder = (cx == 6 && cy == 6) || (cx == 6 && cy == centers[^1]) || (cx == centers[^1] && cy == 6);
                if (!nearFinder) DrawAlignment(cx, cy);
            }

        DrawFormat(0); // cadangkan tempatnya; ditulis ulang setelah mask dipilih
        DrawVersion();
    }

    private void DrawFinder(int cx, int cy)
    {
        for (int dy = -4; dy <= 4; dy++)
            for (int dx = -4; dx <= 4; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= Size || y >= Size) continue;
                int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
                Set(x, y, d != 2 && d != 4);
            }
    }

    private void DrawAlignment(int cx, int cy)
    {
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                Set(cx + dx, cy + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
    }

    /// <summary>15 bit format: level M (00) + mask, BCH(15,5), XOR 101010000010010.</summary>
    public static int FormatBits(int mask)
    {
        int data = (0b00 << 3) | mask;
        int rem = data;
        for (int i = 0; i < 10; i++) rem = (rem << 1) ^ (((rem >> 9) & 1) * 0x537);
        return ((data << 10) | (rem & 0x3FF)) ^ 0x5412;
    }

    private void DrawFormat(int mask)
    {
        int bits = FormatBits(mask);
        bool Bit(int i) => ((bits >> i) & 1) != 0;
        for (int i = 0; i <= 5; i++) Set(8, i, Bit(i));
        Set(8, 7, Bit(6));
        Set(8, 8, Bit(7));
        Set(7, 8, Bit(8));
        for (int i = 9; i < 15; i++) Set(14 - i, 8, Bit(i));
        for (int i = 0; i < 8; i++) Set(Size - 1 - i, 8, Bit(i));
        for (int i = 8; i < 15; i++) Set(8, Size - 15 + i, Bit(i));
        Set(8, Size - 8, true); // modul gelap tetap
    }

    /// <summary>18 bit versi (versi ≥ 7): versi + BCH(18,6).</summary>
    public static int VersionBits(int version)
    {
        int rem = version;
        for (int i = 0; i < 12; i++) rem = (rem << 1) ^ (((rem >> 11) & 1) * 0x1F25);
        return (version << 12) | (rem & 0xFFF);
    }

    private void DrawVersion()
    {
        if (Version < 7) return;
        int bits = VersionBits(Version);
        for (int i = 0; i < 18; i++)
        {
            bool bit = ((bits >> i) & 1) != 0;
            int a = Size - 11 + i % 3, b = i / 3;
            Set(a, b, bit);
            Set(b, a, bit);
        }
    }

    // ── Data ───────────────────────────────────────────────────────────────

    private void PlaceData(List<byte> codewords)
    {
        int bit = 0;
        int total = codewords.Count * 8;
        // Kolom berpasangan dari kanan ke kiri, naik-turun bergantian; kolom 6 (timing) dilewati.
        for (int right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (int vert = 0; vert < Size; vert++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j;
                    bool upward = ((right + 1) & 2) == 0;
                    int y = upward ? Size - 1 - vert : vert;
                    if (_function[y, x]) continue;
                    if (bit < total)
                        _dark[y, x] = ((codewords[bit >> 3] >> (7 - (bit & 7))) & 1) != 0;
                    bit++;
                }
            }
        }
    }

    private void ApplyMask(int mask)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (_function[y, x]) continue;
                bool invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                };
                if (invert) _dark[y, x] = !_dark[y, x];
            }
    }

    /// <summary>Skor penalti standar (baris/kolom sewarna, kotak 2×2, pola mirip finder, keseimbangan).</summary>
    private int Penalty()
    {
        int score = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int a = 0; a < Size; a++)
            {
                int run = 1;
                for (int b = 1; b < Size; b++)
                {
                    bool cur = pass == 0 ? _dark[a, b] : _dark[b, a];
                    bool prev = pass == 0 ? _dark[a, b - 1] : _dark[b - 1, a];
                    if (cur == prev)
                    {
                        run++;
                        if (run == 5) score += 3;
                        else if (run > 5) score++;
                    }
                    else
                    {
                        run = 1;
                    }
                }
                // 1:1:3:1:1 dengan 4 modul terang di salah satu sisi.
                for (int b = 0; b + 10 < Size; b++)
                {
                    bool M(int k) => pass == 0 ? _dark[a, b + k] : _dark[b + k, a];
                    bool core = M(0) && !M(1) && M(2) && M(3) && M(4) && !M(5) && M(6);
                    if (!core) continue;
                    bool lightAfter = !M(7) && !M(8) && !M(9) && !M(10);
                    bool lightBefore = b >= 4 && (pass == 0
                        ? !_dark[a, b - 1] && !_dark[a, b - 2] && !_dark[a, b - 3] && !_dark[a, b - 4]
                        : !_dark[b - 1, a] && !_dark[b - 2, a] && !_dark[b - 3, a] && !_dark[b - 4, a]);
                    if (lightAfter || lightBefore) score += 40;
                }
            }
        }
        int dark = 0;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (_dark[y, x]) dark++;
                if (x + 1 < Size && y + 1 < Size)
                {
                    bool c = _dark[y, x];
                    if (c == _dark[y, x + 1] && c == _dark[y + 1, x] && c == _dark[y + 1, x + 1]) score += 3;
                }
            }
        int percent = dark * 100 / (Size * Size);
        score += Math.Abs(percent - 50) / 5 * 10;
        return score;
    }

    // ── Reed-Solomon di GF(256), polinomial 0x11D ─────────────────────────

    public static byte[] ReedSolomon(byte[] data, int ecLength)
    {
        // Generator: (x − α⁰)(x − α¹)…(x − α^(n−1)).
        var gen = new byte[ecLength];
        gen[ecLength - 1] = 1;
        byte root = 1;
        for (int i = 0; i < ecLength; i++)
        {
            for (int j = 0; j < ecLength; j++)
            {
                gen[j] = Multiply(gen[j], root);
                if (j + 1 < ecLength) gen[j] ^= gen[j + 1];
            }
            root = Multiply(root, 2);
        }
        var result = new byte[ecLength];
        foreach (var b in data)
        {
            byte factor = (byte)(b ^ result[0]);
            Array.Copy(result, 1, result, 0, ecLength - 1);
            result[ecLength - 1] = 0;
            for (int i = 0; i < ecLength; i++) result[i] ^= Multiply(gen[i], factor);
        }
        return result;
    }

    private static byte Multiply(byte a, byte b)
    {
        int r = 0;
        for (int i = 7; i >= 0; i--)
        {
            r = (r << 1) ^ ((r >> 7) * 0x11D);
            r ^= ((b >> i) & 1) * a;
        }
        return (byte)r;
    }
}
