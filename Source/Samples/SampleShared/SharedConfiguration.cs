using System.Configuration;
using System.Text;

namespace SampleShared
{
    public static class SharedConfiguration
    {
        #region Constructor
        static SharedConfiguration()
        {
            System.Collections.Specialized.NameValueCollection oSettings = ConfigurationManager.AppSettings;
            if (oSettings["EnableTrace"] != null)
            {
                EnableTrace = bool.Parse(oSettings["EnableTrace"]);
            }
            if (oSettings["EnableMetrics"] != null)
            {
                EnableMetrics = bool.Parse(oSettings["EnableMetrics"]);
            }
            if (oSettings["EnableCompression"] != null)
            {
                EnableCompression = bool.Parse(oSettings["EnableCompression"]);
            }
            if (oSettings["EnableEncryption"] != null)
            {
                EnableEncryption = bool.Parse(oSettings["EnableEncryption"]);
            }
            if (oSettings["EnableChaos"] != null)
            {
                EnableChaos = bool.Parse(oSettings["EnableChaos"]);
            }
            if (oSettings["EnableDashboard"] != null)
            {
                EnableDashboard = bool.Parse(oSettings["EnableDashboard"]);
            }
            if (oSettings["EnableHistory"] != null)
            {
                EnableHistory = bool.Parse(oSettings["EnableHistory"]);
            }
            if (oSettings["DashboardApiUrl"] != null)
            {
                DashboardApiUrl = oSettings["DashboardApiUrl"];
            }
        }
        #endregion

        #region Public Props
        public static bool EnableTrace { get; }
        public static bool EnableMetrics { get; }
        public static bool EnableCompression { get; }
        public static bool EnableEncryption { get; }
        public static bool EnableChaos { get; }
        public static bool EnableDashboard { get; }
        public static bool EnableHistory { get; }
        public static string DashboardApiUrl { get; } = "http://192.168.0.2:9998";
        #endregion

        public static string AllSettings
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Tracing:");
                sb.Append(EnableTrace.ToString());
                sb.Append(" Chaos:");
                sb.Append(EnableChaos.ToString());
                sb.Append(" Metrics:");
                sb.Append(EnableMetrics.ToString());
                sb.Append(" Compression:");
                sb.Append(EnableCompression.ToString());
                sb.Append(" Encryption:");
                sb.Append(EnableEncryption.ToString());
                sb.Append(" Dashboard:");
                sb.Append(EnableDashboard.ToString());
                sb.Append(" History:");
                sb.Append(EnableHistory.ToString());
                return sb.ToString();
            }
        }
    }
}
