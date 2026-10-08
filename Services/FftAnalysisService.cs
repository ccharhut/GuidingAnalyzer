// Services/FftAnalysisService.cs
// FFT analysis of the mount curve to identify error frequencies.
// Uses MathNet.Numerics for the Fast Fourier Transform.
//
// FFT PRINCIPLE:
// A periodic signal can be decomposed into a sum of sinusoids.
// The FFT returns, for each frequency, the amplitude of the sinusoidal component.
// A peak identifies a periodic component; its mechanical origin requires confirmation.
//
// PEAK INTERPRETATION:
//   Period ~8–10 min  → Periodic error (worm gear)
//   Period ~1–2 min   → PE harmonic or second gear
//   Period < 30 sec   → Mechanical vibrations
//   Very low freq     → Differential flexure or atmospheric drift

using System;
using System.Collections.Generic;
using System.Linq;
using GuidingAnalyzer.Models;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace GuidingAnalyzer.Services
{
    public class FftAnalysisService
    {
        public void ComputeSessionSpectra(GuidingSession session)
        {
            var points = SpectralSampling.LongestSegment(session.MountCurve);
            double dt = points.Count > 1
                ? (points[^1].Timestamp - points[0].Timestamp).TotalSeconds / (points.Count - 1) : 0;
            session.FftRa = ComputeFft(SpectralSampling.Resample(points, p => p.RaMountPosition), dt, GuidingAxis.RA);
            session.FftDec = ComputeFft(SpectralSampling.Resample(points, p => p.DecMountPosition), dt, GuidingAxis.Dec);
            session.FftRaUncorrected = ComputeFft(SpectralSampling.Resample(points, p => p.RaRawError), dt, GuidingAxis.RA);
            session.FftDecUncorrected = ComputeFft(SpectralSampling.Resample(points, p => p.DecRawError), dt, GuidingAxis.Dec);
        }
        // Transparent screening rules, not a calibrated statistical significance test.
        private const double MinimumPowerPercent = 1;
        private const double MinimumPeakToNoise = 5;
        private const double MinimumCycles = 2;

        // Characteristic mount periods (minutes) for interpretation
        private static readonly (double MinPeriod, double MaxPeriod, string Label)[] KNOWN_PERIODS =
        {
            (6.0, 12.0, "Periodic error (main worm)"),
            (2.0, 6.0,  "PE harmonic or second gear"),
            (0.5, 2.0,  "High-frequency mechanical vibration"),
            (12.0, 60.0,"Differential flexure or slow drift"),
        };

        /// <summary>
        /// Computes the FFT of the mount curve signal.
        /// </summary>
        /// <param name="signal">Signal in arcsec (array of doubles)</param>
        /// <param name="samplingPeriodSeconds">Interval between two points in seconds</param>
        /// <param name="axis">Analysed axis (RA or Dec)</param>
        public FftResult ComputeFft(double[] signal, double samplingPeriodSeconds, GuidingAxis axis)
        {
            if (signal.Length == 0 || !double.IsFinite(samplingPeriodSeconds) || samplingPeriodSeconds <= 0
                || signal.Any(v => !double.IsFinite(v)))
                return new FftResult();

            var result = new FftResult
            {
                Axis                = axis,
                SamplingPeriodSeconds = samplingPeriodSeconds,
                PointCount          = signal.Length
            };

            if (signal.Length < 16)
            {
                // Not enough points for a meaningful FFT
                return result;
            }

            // 1. Linear detrending: removes slow drift from the signal
            //    to avoid spectral leakage
            // DEC retains its drift. Only its DC offset is removed, to avoid an
            // arbitrary zero level leaking through the window into periodic components.
            double mean = signal.Average();
            double[] detrended = axis == GuidingAxis.RA ? RemoveLinearTrend(signal)
                : signal.Select(v => v - mean).ToArray();

            // 2. Apply Hann window
            //    Reduces edge artefacts (spectral leakage)
            double[] windowed = ApplyHannWindow(detrended);

            // 3. Zero-pad to next power of two
            //    FFT is optimal for sizes N = 2^k
            int fftSize = NextPowerOfTwo(windowed.Length);
            var complexSignal = new System.Numerics.Complex[fftSize];
            for (int i = 0; i < windowed.Length; i++)
                complexSignal[i] = new System.Numerics.Complex(windowed[i], 0);
            // Remaining elements are already zero (zero-padding)

            // 4. Compute FFT via MathNet.Numerics
            Fourier.Forward(complexSignal, FourierOptions.Matlab);

            // 5. Compute amplitudes (modulus of complex number)
            //    Keep only the positive half of the spectrum (symmetry)
            int halfSize = fftSize / 2;
            var amplitudes  = new double[halfSize + 1];
            var frequencies = new double[halfSize + 1];
            var powers = new double[halfSize + 1];

            // Nyquist frequency = 1 / (2 × Te)
            // Frequency resolution = 1 / (N × Te)
            double frequencyResolution = 1.0 / (fftSize * samplingPeriodSeconds);
            double windowGain = (signal.Length - 1) / 2.0;
            double windowSquareSum = Enumerable.Range(0, signal.Length)
                .Sum(i => Math.Pow(.5 * (1 - Math.Cos(2 * Math.PI * i / (signal.Length - 1))), 2));

            for (int i = 0; i <= halfSize; i++)
            {
                // Amplitude = modulus / N × 2 (factor 2 for symmetry, except DC)
                double symmetry = i == 0 || i == halfSize ? 1 : 2;
                amplitudes[i] = complexSignal[i].Magnitude / windowGain * symmetry;
                powers[i] = complexSignal[i].Magnitude * complexSignal[i].Magnitude * symmetry
                    / (fftSize * windowSquareSum);
                // Frequency in cycles/hour
                frequencies[i] = i * frequencyResolution * 3600.0;
            }

            result.Frequencies = frequencies;
            result.Amplitudes  = amplitudes;

            // 6. Automatic detection of significant peaks
            result.SignificantPeaks = DetectPeaks(frequencies, amplitudes, powers,
                signal.Length, fftSize, samplingPeriodSeconds);

            return result;
        }

        // Removes the linear trend from the signal (detrending)
        private double[] RemoveLinearTrend(double[] signal)
        {
            int n = signal.Length;
            // Simple linear regression: y = ax + b
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int i = 0; i < n; i++)
            {
                sumX += i; sumY += signal[i];
                sumXY += i * signal[i]; sumX2 += (double)i * i;
            }
            double a = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            double b = (sumY - a * sumX) / n;

            var result = new double[n];
            for (int i = 0; i < n; i++)
                result[i] = signal[i] - (a * i + b);
            return result;
        }

        // Hann window: w[n] = 0.5 × (1 − cos(2π·n/(N−1)))
        private double[] ApplyHannWindow(double[] signal)
        {
            int n = signal.Length;
            var result = new double[n];
            for (int i = 0; i < n; i++)
                result[i] = signal[i] * 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
            return result;
        }

        private int NextPowerOfTwo(int n)
        {
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        // Detects significant peaks in the FFT spectrum
        private List<FftPeak> DetectPeaks(double[] frequencies, double[] amplitudes,
            double[] powers, int pointCount, int fftSize, double dt)
        {
            var peaks = new List<FftPeak>();
            if (amplitudes.Length < 3) return peaks;
            double totalPower = powers.Skip(1).Sum();
            if (totalPower <= 1e-20) return peaks;
            double duration = (pointCount - 1) * dt;
            var background = amplitudes.Skip(1).OrderBy(a => a).ToArray();
            double noise = background[background.Length / 2];
            double maxAmplitude = amplitudes.Skip(1).Max();
            // The Hann main lobe has a half-width of two ORIGINAL frequency bins.
            // Padding interpolates it; it does not create more independent components.
            int radius = Math.Max(1, (int)Math.Ceiling(2.0 * fftSize / pointCount));
            var candidates = Enumerable.Range(1, amplitudes.Length - 1)
                .Where(i => amplitudes[i] > amplitudes[i - 1]
                    && (i == amplitudes.Length - 1 || amplitudes[i] > amplitudes[i + 1])
                    && frequencies[i] / 3600 * duration >= MinimumCycles
                    && amplitudes[i] >= MinimumPeakToNoise * Math.Max(noise, 1e-12))
                .OrderByDescending(i => amplitudes[i]).ToList();
            var selected = new List<int>();
            foreach (int i in candidates)
                if (!selected.Any(j => Math.Abs(i - j) <= radius)) selected.Add(i);
            selected.Sort();
            for (int k = 0; k < selected.Count; k++)
            {
                int i = selected[k];
                int left = Math.Max(1, i - radius), right = Math.Min(powers.Length - 1, i + radius);
                // Partition overlapping lobes so the same power is never credited twice.
                if (k > 0) left = Math.Max(left, (selected[k - 1] + i) / 2 + 1);
                if (k + 1 < selected.Count) right = Math.Min(right, (selected[k + 1] + i) / 2);
                double lobePower = 0;
                for (int j = left; j <= right; j++) lobePower += powers[j];
                double percent = 100 * lobePower / totalPower;
                if (percent < MinimumPowerPercent) continue;
                peaks.Add(new FftPeak {
                    FrequencyCph = frequencies[i], AmplitudeArcsec = amplitudes[i],
                    RmsContributionArcsec = Math.Sqrt(lobePower), PowerPercent = percent,
                    PeakToNoiseRatio = amplitudes[i] / Math.Max(noise, 1e-12),
                    CyclesObserved = frequencies[i] / 3600 * duration,
                    Confidence = amplitudes[i] / maxAmplitude,
                    Interpretation = InterpretPeak(60 / frequencies[i])
                });
            }
            return peaks.OrderByDescending(p => p.PowerPercent).Take(10).ToList();
        }

        private string InterpretPeak(double periodMinutes)
        {
            foreach (var (min, max, label) in KNOWN_PERIODS)
                if (periodMinutes >= min && periodMinutes <= max)
                    return $"Possible {label.ToLowerInvariant()} (~{periodMinutes:F1} min)";

            return $"Unknown frequency (~{periodMinutes:F1} min)";
        }
    }
}
