using System;

namespace Cubicolor.Audio;

// The inverse MDCT Vorbis uses, unscaled: N/2 coefficients X to N samples
//   y[n] = sum_k X[k] cos(2π/N (n + 1/2 + N/4)(k + 1/2)).
// It is a DCT-IV of length M = N/2 unfolded by the transform's symmetries, and the DCT-IV is an M/2-point
// complex FFT between two twiddles, so a block costs O(N log N).
internal sealed class Mdct
{
    private readonly int _n, _m, _q;            // N, N/2, N/4
    private readonly float[] _twiddleCos, _twiddleSin;   // e^(-iπ(k + 1/8)/M), k < M/2
    private readonly float[] _fftCos, _fftSin;           // e^(-2πik/(M/2)), k < M/4
    private readonly int[] _bitReverse;
    private readonly float[] _re, _im, _u;

    public Mdct(int n)
    {
        if (n < 16 || (n & (n - 1)) != 0) throw new ArgumentOutOfRangeException(nameof(n));
        _n = n;
        _m = n / 2;
        _q = n / 4;
        _twiddleCos = new float[_q];
        _twiddleSin = new float[_q];
        for (int k = 0; k < _q; k++)
        {
            double a = -Math.PI * (k + 0.125) / _m;
            _twiddleCos[k] = (float) Math.Cos(a);
            _twiddleSin[k] = (float) Math.Sin(a);
        }
        _fftCos = new float[_q / 2];
        _fftSin = new float[_q / 2];
        for (int k = 0; k < _q / 2; k++)
        {
            double a = -2 * Math.PI * k / _q;
            _fftCos[k] = (float) Math.Cos(a);
            _fftSin[k] = (float) Math.Sin(a);
        }
        int bits = System.Numerics.BitOperations.Log2((uint) _q);
        _bitReverse = new int[_q];
        for (int i = 0; i < _q; i++)
        {
            int r = 0;
            for (int b = 0; b < bits; b++) r |= ((i >> b) & 1) << (bits - 1 - b);
            _bitReverse[i] = r;
        }
        _re = new float[_q];
        _im = new float[_q];
        _u = new float[_m];
    }

    public int Size => _n;

    // N/2 coefficients in, N samples out.
    public void Inverse(ReadOnlySpan<float> coefficients, Span<float> output)
    {
        int m = _m, q = _q;
        float[] re = _re, im = _im, u = _u;

        // v[k] = (X[2k] + i X[M-1-2k]) e^(-iπ(k + 1/8)/M), stored bit-reversed for the in-place FFT.
        for (int k = 0; k < q; k++)
        {
            float a = coefficients[2 * k], b = coefficients[m - 1 - 2 * k];
            float c = _twiddleCos[k], s = _twiddleSin[k];
            int r = _bitReverse[k];
            re[r] = a * c - b * s;
            im[r] = a * s + b * c;
        }

        for (int size = 2; size <= q; size <<= 1)
        {
            int half = size >> 1, stride = q / size;
            for (int start = 0; start < q; start += size)
                for (int j = 0; j < half; j++)
                {
                    float wc = _fftCos[j * stride], ws = _fftSin[j * stride];
                    int p = start + j, o = p + half;
                    float tr = re[o] * wc - im[o] * ws, ti = re[o] * ws + im[o] * wc;
                    re[o] = re[p] - tr;
                    im[o] = im[p] - ti;
                    re[p] += tr;
                    im[p] += ti;
                }
        }

        // The second twiddle gives the DCT-IV: u[2n] = Re, u[M-1-2n] = -Im.
        for (int k = 0; k < q; k++)
        {
            float c = _twiddleCos[k], s = _twiddleSin[k];
            u[2 * k] = re[k] * c - im[k] * s;
            u[m - 1 - 2 * k] = -(re[k] * s + im[k] * c);
        }

        // y[n] = U(n + M/2), where U(m) = u[m] on [0, M), U(2M-1-m) = -U(m) and U(m + 2M) = -U(m).
        int h = m / 2;
        for (int i = 0; i < h; i++) output[i] = u[h + i];
        for (int i = h; i < h + m; i++) output[i] = -u[2 * m - 1 - (i + h)];
        for (int i = h + m; i < 2 * m; i++) output[i] = -u[i + h - 2 * m];
    }
}
