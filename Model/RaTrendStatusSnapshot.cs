using System.Text.Json.Serialization;

namespace RATrendCompensatorNINA.Model {

    /// <summary>
    /// Mirrors the dict returned by RATrendCompensatorViewModel.get_status_snapshot()
    /// in the Python app -- one JSON object per line, broadcast every
    /// status_server_interval_seconds over the status_server TCP socket.
    /// Keep property names/JSON keys in sync with that method if it changes.
    /// </summary>
    public class RaTrendStatusSnapshot {

        [JsonPropertyName("running")]
        public bool Running { get; set; }

        [JsonPropertyName("dry_run")]
        public bool DryRun { get; set; }

        [JsonPropertyName("current_offset")]
        public double? CurrentOffset { get; set; }

        [JsonPropertyName("current_ra_deviation_arcsec")]
        public double? CurrentRaDeviationArcsec { get; set; }

        [JsonPropertyName("last_slope_arcsec_per_sec")]
        public double? LastSlopeArcsecPerSec { get; set; }

        [JsonPropertyName("last_trend_n_samples")]
        public int? LastTrendNSamples { get; set; }

        [JsonPropertyName("guide_rms_arcsec")]
        public double? GuideRmsArcsec { get; set; }

        [JsonPropertyName("guide_rms_trend_arcsec_per_sec")]
        public double? GuideRmsTrendArcsecPerSec { get; set; }

        [JsonPropertyName("guide_rms_trend_n_samples")]
        public int? GuideRmsTrendNSamples { get; set; }

        [JsonPropertyName("phd2_avg_dist_arcsec")]
        public double? Phd2AvgDistArcsec { get; set; }

        [JsonPropertyName("declination_deg")]
        public double? DeclinationDeg { get; set; }

        [JsonPropertyName("side_of_pier")]
        public string SideOfPier { get; set; }

        [JsonPropertyName("timestamp")]
        public double Timestamp { get; set; }
    }
}
