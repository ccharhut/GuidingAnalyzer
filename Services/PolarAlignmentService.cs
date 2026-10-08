using System;
using System.Collections.Generic;
using System.Linq;
using GuidingAnalyzer.Models;
using MathNet.Numerics.LinearAlgebra;

namespace GuidingAnalyzer.Services
{
    // ═══════════════════════════════════════════════════════════════════════════
    // MODÈLE DE RÉSULTAT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Complete result of the polar error calculation for a night.
    /// Exposed to the ViewModel for display in the Polar Alignment tab.
    /// </summary>
    public class PolarAlignmentResult
    {
        // ── Computed errors ──────────────────────────────────────────────────

        /// <summary>Erreur azimutale (arcmin). + = trop Est, − = trop Ouest.</summary>
        public double ErrAzArcmin  { get; set; }

        /// <summary>Erreur altitudinale (arcmin). + = trop haut, − = trop bas.</summary>
        public double ErrAltArcmin { get; set; }

        /// <summary>Pole separation: sqrt((az*cos(latitude))² + alt²), arcmin.</summary>
        public double LatitudeDeg { get; set; }
        public double TotalArcmin => Math.Sqrt(Math.Pow(ErrAzArcmin * Math.Cos(LatitudeDeg * Math.PI / 180), 2) + ErrAltArcmin * ErrAltArcmin);

        // ── Measurement quality ───────────────────────────────────────────────

        /// <summary>Conditionnement de la matrice A (rapport valeur singulière max/min).</summary>
        public double Conditioning  { get; set; }

        /// <summary>RMS des résidus en mas/min.</summary>
        public double ResidualRmsMasPerMin { get; set; }

        /// <summary>Nombre de sessions ayant contribué au calcul.</summary>
        public int    SessionCount  { get; set; }

        /// <summary>Sessions excluded for missing metadata or insufficient uninterrupted measurements.</summary>
        public int    ExcludedCount { get; set; }

        /// <summary>True si le calcul a pu aboutir (≥ 2 sessions valides).</summary>
        public bool   IsValid       { get; set; }

        /// <summary>Message d'erreur si IsValid = false.</summary>
        public string ErrorMessage  { get; set; } = string.Empty;

        // ── Quality: labels and colours ──────────────────────────────────

        public PolarAlignmentQuality Quality =>
            TotalArcmin < 0.5 ? PolarAlignmentQuality.Excellent :
            TotalArcmin < 1.0 ? PolarAlignmentQuality.Good      :
            TotalArcmin < 2.0 ? PolarAlignmentQuality.Fair      :
                                PolarAlignmentQuality.Poor;

        public string QualityLabel => Quality switch
        {
            PolarAlignmentQuality.Excellent => "Excellent",
            PolarAlignmentQuality.Good      => "Good",
            PolarAlignmentQuality.Fair      => "Fair",
            _                               => "Needs correction"
        };

        public string QualityColor => Quality switch
        {
            PolarAlignmentQuality.Excellent => "#4CAF50",   // vert
            PolarAlignmentQuality.Good      => "#8BC34A",   // vert clair
            PolarAlignmentQuality.Fair      => "#FFC107",   // orange
            _                               => "#F44336"    // rouge
        };

        public string ConditioningLabel =>
            Conditioning < 50   ? "Excellent"   :
            Conditioning < 300  ? "Good"         :
            Conditioning < 1000 ? "Low"      : "Unreliable";

        public string ConditioningColor =>
            Conditioning < 50   ? "#4CAF50" :
            Conditioning < 300  ? "#8BC34A" :
            Conditioning < 1000 ? "#FFC107" : "#F44336";

        // ── Human-readable directions ────────────────────────────────────────────────

        /// <summary>Direction azimut : "Est" ou "Ouest".</summary>
        public string AzDirection => Math.Abs(ErrAzArcmin) < 0.005 ? "No correction" : ErrAzArcmin > 0 ? "Correct towards West" : "Correct towards East";

        /// <summary>Direction altitude : "Haut" ou "Bas".</summary>
        public string AltDirection => Math.Abs(ErrAltArcmin) < 0.005 ? "No correction" : ErrAltArcmin > 0 ? "Correct downwards" : "Correct upwards";

        // ── Conversion to screw turns ─────────────────────────────────────────

        /// <summary>Fractions de tour vis azimut (base paramétrable, défaut 30 arcmin/tour).</summary>
        public double AzTurns(double arcminPerTurn = 30.0)
            => arcminPerTurn > 0 ? Math.Abs(ErrAzArcmin) / arcminPerTurn : 0;

        /// <summary>Fractions de tour vis altitude (base paramétrable, défaut 30 arcmin/tour).</summary>
        public double AltTurns(double arcminPerTurn = 30.0)
            => arcminPerTurn > 0 ? Math.Abs(ErrAltArcmin) / arcminPerTurn : 0;

        // ── Measurement advice ────────────────────────────────────────────────

        /// <summary>
        /// Advice message if conditioning is degraded.
        /// Indicates which position (HA, Dec) would improve precision.
        /// </summary>
        public string MeasurementAdvice { get; set; } = string.Empty;

        // ── Contributing sessions (for display table) ──────────────────

        public List<PolarSessionRow> SessionRows { get; set; } = new();
    }

    public enum PolarAlignmentQuality { Excellent, Good, Fair, Poor }

    /// <summary>Ligne de tableau : une session et sa contribution au calcul.</summary>
    public class PolarSessionRow
    {
        public int      SessionIndex  { get; set; }
        public DateTime StartTime     { get; set; }
        public double   HourAngle     { get; set; }
        public double   DecDeg        { get; set; }
        public double   AltDeg        { get; set; }
        public double   AzDeg         { get; set; }
        public double   DecDriftRaw   { get; set; }   // arcsec/min, sur courbe monture reconstituée
        public double   Weight        { get; set; }
        public int      FrameCount    { get; set; }
        public bool     IsExcluded    { get; set; }
        public string   ExcludeReason { get; set; } = string.Empty;

        // Résidu après ajustement (arcsec/min)
        public double   Residual      { get; set; }
        internal double CosHaResponse { get; set; }
        internal double SinHaResponse { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SERVICE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Small-angle DEC drift model, in physical celestial North coordinates:
    /// drift[arcsec/min] = -60 * omega[rad/min] * hemisphere *
    /// (azError[arcmin] * cos(latitude) * cos(HA) +
    ///  altError[arcmin] * sin(HA)).
    /// HA is positive west; errors mean polar axis too far East / too high.
    /// Time-dependent HA is integrated, then regressed like the measured curve.
    /// PHD2 DEC parity is required to combine signed drifts across pier sides.
    /// </summary>
    public class PolarAlignmentService
    {
        public const double SiderealRadiansPerMinute = 2 * Math.PI / (86164.0905 / 60);
        private const double DriftPerArcmin = 60 * SiderealRadiansPerMinute;

        public PolarAlignmentResult Compute(IEnumerable<GuidingSession> sessions,
            double latitudeDeg, double arcminPerTurn = 30)
        {
            var result = new PolarAlignmentResult { LatitudeDeg = latitudeDeg,
                ResidualRmsMasPerMin = double.NaN };
            if (!double.IsFinite(latitudeDeg) || Math.Abs(latitudeDeg) > 90
                || !double.IsFinite(arcminPerTurn) || arcminPerTurn <= 0)
            {
                result.ErrorMessage = "Invalid latitude or screw sensitivity.";
                return result;
            }
            result.SessionRows = sessions.Select(BuildRow).ToList();
            var rows = result.SessionRows.Where(r => !r.IsExcluded).ToList();
            result.SessionCount = rows.Count;
            result.ExcludedCount = result.SessionRows.Count - rows.Count;
            if (rows.Count < 2)
            {
                result.ErrorMessage = "At least two usable sessions with different hour angles are required. See exclusion reasons below.";
                return result;
            }
            var a = Matrix<double>.Build.Dense(rows.Count, 2);
            var b = Vector<double>.Build.Dense(rows.Count);
            double hemisphere = latitudeDeg < 0 ? -1 : 1;
            for (int i = 0; i < rows.Count; i++)
            {
                double weight = Math.Sqrt(rows[i].Weight);
                a[i, 0] = -hemisphere * weight * DriftPerArcmin * Math.Cos(latitudeDeg * Math.PI / 180) * rows[i].CosHaResponse;
                a[i, 1] = -hemisphere * weight * DriftPerArcmin * rows[i].SinHaResponse;
                b[i] = weight * rows[i].DecDriftRaw;
            }
            // Solve the weighted design directly, not normal equations (which square the condition number).
            var svd = a.Svd(true);
            result.Conditioning = svd.S[1] > 0 ? svd.S[0] / svd.S[1] : double.PositiveInfinity;
            if (!double.IsFinite(result.Conditioning) || result.Conditioning >= 1000)
            {
                result.ErrorMessage = "Unreliable geometry: observe near the meridian and to the East or West at low declination.";
                return result;
            }
            var x = svd.Solve(b);
            result.ErrAzArcmin = x[0];
            result.ErrAltArcmin = x[1];
            if (!double.IsFinite(x[0]) || !double.IsFinite(x[1]) || result.TotalArcmin > 60)
            {
                result.ErrorMessage = "Estimate outside the small-angle model: check calibration, guide rates and logs.";
                return result;
            }
            double residualSum = 0, weightSum = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                double residual = (b[i] - a[i, 0] * x[0] - a[i, 1] * x[1]) / Math.Sqrt(rows[i].Weight);
                rows[i].Residual = residual;
                residualSum += rows[i].Weight * residual * residual;
                weightSum += rows[i].Weight;
            }
            if (rows.Count > 2)
                result.ResidualRmsMasPerMin = 1000 * Math.Sqrt(residualSum / weightSum);
            result.IsValid = true;
            result.MeasurementAdvice = "Estimate assumes the calibration and guide displacements are accurate; flexure and backlash can bias it. "
                + (rows.Count == 2 ? "Two sessions determine two components but cannot independently validate the fit. " : "")
                + (result.Conditioning >= 300 ? "Geometry is weak: add a session near the meridian and one towards East or West. " : "")
                + "Use low-declination targets and verify with PHD2 drift alignment before adjusting the mount.";
            return result;
        }

        private static PolarSessionRow BuildRow(GuidingSession session)
        {
            var row = new PolarSessionRow { SessionIndex = session.SessionIndex, StartTime = session.StartTime };
            PolarSessionRow Exclude(string reason) { row.IsExcluded = true; row.ExcludeReason = reason; return row; }
            if (!double.IsFinite(session.PixelScale) || session.PixelScale <= 0)
                return Exclude("Missing pixel scale (arcsec/pixel)");
            var info = session.SessionInfo;
            if (info == null || !double.IsFinite(info.HourAngle) || !double.IsFinite(info.DecDeg))
                return Exclude("Missing session position (HA/Dec); an earlier calibration is not the current target");
            if (Math.Abs(info.DecDeg) >= 85 || Math.Abs(info.HourAngle) > 12)
                return Exclude("Position too close to celestial pole or invalid hour angle");
            row.HourAngle = info.HourAngle * 15;
            row.DecDeg = info.DecDeg;
            row.AltDeg = info.AltDeg;
            row.AzDeg = info.AzDeg;
            // Current session parity already incorporates any meridian flip.
            var parity = info.MountParity.Trim().TrimEnd(',').Split('/');
            if (parity.Length != 2 || (parity[1] != "+" && parity[1] != "-"))
                return Exclude("Unknown DEC parity: the direction of celestial drift cannot be determined");
            double northSign = parity[1] == "+" ? 1 : -1;
            var curve = session.MountCurve;
            if (curve.Count == 0)
                curve = new MountCurveService().ReconstructSession(session);
            var points = SpectralSampling.LongestSegment(curve);
            row.FrameCount = points.Count;
            if (points.Count < 30) return Exclude("Fewer than 30 consecutive valid frames");
            double minutes = (points[^1].Timestamp - points[0].Timestamp).TotalMinutes;
            if (minutes < 2) return Exclude("Uninterrupted measurement shorter than 120 seconds");
            if (points.Any(p => !double.IsFinite(p.DecMountPosition))) return Exclude("Non-finite reconstructed drift");
            double ha0 = info.HourAngle * Math.PI / 12
                + (points[0].Timestamp - session.StartTime).TotalMinutes * SiderealRadiansPerMinute;
            var times = points.Select(p => (p.Timestamp - points[0].Timestamp).TotalMinutes).ToArray();
            // Regression of the integrated geometry gives exactly the same time weighting
            // as the measured slope, including long sessions and uneven exposure intervals.
            row.CosHaResponse = Slope(times, times.Select(t =>
                (Math.Sin(ha0 + SiderealRadiansPerMinute * t) - Math.Sin(ha0)) / SiderealRadiansPerMinute).ToArray());
            row.SinHaResponse = Slope(times, times.Select(t =>
                (Math.Cos(ha0) - Math.Cos(ha0 + SiderealRadiansPerMinute * t)) / SiderealRadiansPerMinute).ToArray());
            row.DecDriftRaw = northSign * Slope(times, points.Select(p => p.DecMountPosition).ToArray());
            row.Weight = Math.Min(Math.Sqrt(points.Count), 50);
            return row;
        }

        internal static double Slope(double[] times, double[] values)
        {
            double meanT = times.Average(), meanY = values.Average();
            double xx = 0, xy = 0;
            for (int i = 0; i < times.Length; i++)
            {
                double t = times[i] - meanT;
                xx += t * t; xy += t * (values[i] - meanY);
            }
            return xx > 0 ? xy / xx : 0;
        }
    }
}
