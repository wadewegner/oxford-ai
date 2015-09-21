using System.Collections.Generic;
using OxfordAi.ComputerVision.Models;

namespace OxfordAi.ComputerVision.Models
{
    public class Vision
    {
        public List<Category> categories { get; set; }
        public Adult adult { get; set; }
        public string requestId { get; set; }
        public Metadata metadata { get; set; }
        public List<Face> faces { get; set; }
        public Color color { get; set; }
        public ImageType imageType { get; set; }
        public string imageUrl { get; set; }
    }
}
