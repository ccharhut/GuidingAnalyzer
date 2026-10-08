// Frozen source from the distributed 1.0.0.2 archive, for RA compatibility checks.
using System;
using System.Collections.Generic;
using System.Linq;
using GuidingAnalyzer.Models;

namespace GuidingAnalyzer.Tests.Fixtures
{
    /// <summary>Approximate unguided motion = measured position minus applied guide displacements.</summary>
    internal class MountCurveV002
    {
        private const double FallbackRate = 0.01504 * 0.5;
        public List<MountCurvePoint> ReconstructSession(GuidingSession session)
        {
            var info = session.SessionInfo;
            // Current calibrated rates already include the RA declination projection.
            double raRate = info != null && info.MountXRate > 0
                ? info.MountXRate * session.PixelScale / 1000 : session.GuidingRateArcsecPerMs;
            double decRate = info != null && info.MountYRate > 0
                ? info.MountYRate * session.PixelScale / 1000 : (info?.GuideRateDEC ?? 0) / 1000;
            return ReconstructMountCurve(session.RawPoints, session.PixelScale, raRate, decRate);
        }

        private static double AppliedGuide(double? requested, double pulse, double rate, bool knownRate)
        {
            if (pulse == 0) return 0;
            if (requested is double distance && double.IsFinite(distance))
            {
                // PHD2 logs the algorithm's requested distance even if max duration limits the pulse.
                return knownRate ? Math.CopySign(Math.Min(Math.Abs(distance), Math.Abs(pulse) * rate), distance) : distance;
            }
            return -pulse * rate;
        }
        public List<MountCurvePoint> ReconstructMountCurve(List<GuidingPoint> points, double pixelScale,
            double guideRateArcsecPerMs = 0, double decGuideRateArcsecPerMs = 0)
        {
            double raRate = guideRateArcsecPerMs > 0 ? guideRateArcsecPerMs : FallbackRate;
            double decRate = decGuideRateArcsecPerMs > 0 ? decGuideRateArcsecPerMs : raRate;
            var curve = new List<MountCurvePoint>(points.Count);
            double raGuides = 0, decGuides = 0, raOffset = 0, decOffset = 0;
            double lastRa = 0, lastDec = 0;
            bool afterDither = false;
            foreach (var p in points)
            {
                // Guide distances use the PHD2 analysis convention. W/S pulses have
                // positive guide distances; stored signed durations have opposite signs.
                double raGuide = AppliedGuide(p.RaGuideDistanceArcsec, p.RaCorrection, raRate, guideRateArcsecPerMs > 0);
                double decGuide = AppliedGuide(p.DecGuideDistanceArcsec, p.DecCorrection, decRate, decGuideRateArcsecPerMs > 0);
                bool excluded = p.IsDitherOrSettling || !p.IsGuideStep
                    || !double.IsFinite(p.RaError) || !double.IsFinite(p.DecError);
                if (p.IsDitherOrSettling)
                {
                    afterDither = true;
                    raGuides = decGuides = 0;
                }
                else if (!excluded)
                {
                    if (afterDither)
                    {
                        // New lock position: retain continuity, exclude intentional motion.
                        raOffset = lastRa - p.RaError;
                        decOffset = lastDec - p.DecError;
                        afterDither = false;
                    }
                    lastRa = p.RaError - raGuides + raOffset;
                    lastDec = p.DecError - decGuides + decOffset;
                }
                curve.Add(new MountCurvePoint {
                    FrameIndex = p.FrameIndex, Timestamp = p.Timestamp,
                    RaMountPosition = lastRa, DecMountPosition = lastDec,
                    IsDitherOrSettling = excluded,
                    RaRawError = p.RaError, DecRawError = p.DecError,
                    RaCorrectionArcsec = raGuide, DecCorrectionArcsec = decGuide
                });
                // Pulse n acts after measurement n. Rejected frames may still have pulses.
                if (!p.IsDitherOrSettling)
                {
                    raGuides += raGuide;
                    decGuides += decGuide;
                }
            }
            var valid = curve.Where(p => !p.IsDitherOrSettling).ToList();
            if (valid.Count > 0)
            {
                double raMean = valid.Average(p => p.RaMountPosition);
                double decMean = valid.Average(p => p.DecMountPosition);
                foreach (var p in curve) { p.RaMountPosition -= raMean; p.DecMountPosition -= decMean; }
            }
            return curve;
        }
        public double[] GetRaSignal(List<MountCurvePoint> curve)
            => curve.Where(p => !p.IsDitherOrSettling).Select(p => p.RaMountPosition).ToArray();
        public double[] GetDecSignal(List<MountCurvePoint> curve)
            => curve.Where(p => !p.IsDitherOrSettling).Select(p => p.DecMountPosition).ToArray();
        public double[] GetRaUncorrectedSignal(List<GuidingPoint> points)
            => points.Where(p => p.IsGuideStep && !p.IsDitherOrSettling).Select(p => p.RaError).ToArray();
        public double[] GetDecUncorrectedSignal(List<GuidingPoint> points)
            => points.Where(p => p.IsGuideStep && !p.IsDitherOrSettling).Select(p => p.DecError).ToArray();
    }
}
