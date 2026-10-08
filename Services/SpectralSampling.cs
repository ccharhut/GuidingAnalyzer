using System;
using System.Collections.Generic;
using System.Linq;
using GuidingAnalyzer.Models;

namespace GuidingAnalyzer.Services
{
    public static class SpectralSampling
    {
        // Never compress time across dithers, dropped frames or acquisition gaps.
        public static List<MountCurvePoint> LongestSegment(List<MountCurvePoint> curve)
        {
            var intervals = curve.Zip(curve.Skip(1), (a, b) => (b.Timestamp - a.Timestamp).TotalSeconds)
                .Where(t => t > 0).OrderBy(t => t).ToArray();
            if (intervals.Length == 0) return new();
            double gapLimit = 3 * intervals[intervals.Length / 2];
            var best = new List<MountCurvePoint>();
            var current = new List<MountCurvePoint>();
            foreach (var p in curve)
            {
                if (p.IsDitherOrSettling || (current.Count > 0 &&
                    ((p.Timestamp - current[^1].Timestamp).TotalSeconds > gapLimit
                     || p.Timestamp <= current[^1].Timestamp)))
                {
                    if (current.Count > best.Count) best = current;
                    current = new();
                }
                if (!p.IsDitherOrSettling) current.Add(p);
            }
            return current.Count > best.Count ? current : best;
        }

        public static double[] Resample(List<MountCurvePoint> points, Func<MountCurvePoint, double> value)
        {
            if (points.Count < 2) return Array.Empty<double>();
            var result = new double[points.Count];
            double dt = (points[^1].Timestamp - points[0].Timestamp).TotalSeconds / (points.Count - 1);
            int j = 0;
            for (int i = 0; i < result.Length; i++)
            {
                double t = i * dt;
                while (j < points.Count - 2 && (points[j + 1].Timestamp - points[0].Timestamp).TotalSeconds < t) j++;
                double t0 = (points[j].Timestamp - points[0].Timestamp).TotalSeconds;
                double t1 = (points[j + 1].Timestamp - points[0].Timestamp).TotalSeconds;
                double f = Math.Clamp((t - t0) / (t1 - t0), 0, 1);
                result[i] = value(points[j]) * (1 - f) + value(points[j + 1]) * f;
            }
            return result;
        }
    }
}
