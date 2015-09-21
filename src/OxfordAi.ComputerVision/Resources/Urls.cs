using System.Configuration;

namespace OxfordAi.ComputerVision.Resources
{
    public static class Urls
    {
        private static readonly string OxfordUrl = ConfigurationManager.AppSettings["OxfordUrl"];

        public static string VisionAnalyses = string.Format("{0}/vision/v1/analyses", OxfordUrl);
    }
}
