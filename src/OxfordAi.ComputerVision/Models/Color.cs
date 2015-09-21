using System.Collections.Generic;

namespace OxfordAi.ComputerVision.Models
{
    public class Color
    {
        public string dominantColorForeground { get; set; }
        public string dominantColorBackground { get; set; }
        public List<string> dominantColors { get; set; }
        public string accentColor { get; set; }
        public bool isBWImg { get; set; }
    }
}