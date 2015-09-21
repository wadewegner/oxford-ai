using System;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OxfordAi.ComputerVision.Models;
using Color = System.Drawing.Color;

namespace OxfordAi.WebApi.Controllers
{
    public class VisionController : ApiController
    {
        private readonly string _storageConnectionString = ConfigurationManager.AppSettings["StorageConnectionString"];
        private readonly string _cloudVisionKey = ConfigurationManager.AppSettings["CloudVisionKey"];

        public async Task<Vision> Get([FromUri]string generateimage, [FromUri]string pencolor, [FromUri]string fillcolor)
        {
            var vision = await GetVisionFromImage(new Uri("https://oxfordai.blob.core.windows.net/vision/tespimage.jpg"));

            return vision;
        }

        public async Task<Vision> PostFormData()
        {
            if (!Request.Content.IsMimeMultipartContent("form-data"))
            {
                var emptyVision = new Vision();
                return emptyVision;
            }

            var provider = await Request.Content.ReadAsMultipartAsync(new InMemoryMultipartFormDataStreamProvider());
            var files = provider.Files;
            var file1 = files[0];
            var fileStream = await file1.ReadAsStreamAsync();

            var keys = Request.RequestUri.ParseQueryString();
            var generateImage = true;//keys["generateimage"] == "1";
            var penColor = "123123";//keys["pencolor"];
            var fillColor = "231231";//keys["fillcolor"];

            var extension = ExtractExtension(file1);
            var contentType = file1.Headers.ContentType.ToString();
            var imageName = string.Concat(Guid.NewGuid().ToString(), extension);
            var imageNameWithRect = string.Concat(Guid.NewGuid().ToString(), "-rect", extension);

            var blobUri = UploadToBlobStorage(fileStream, imageName, contentType);

            var vision = await GetVisionFromImage(blobUri);

            if (generateImage)
            {
                var stream = GenerateImage(vision, penColor, fillColor, fileStream);

                vision.imageUrl = UploadToBlobStorage(stream, imageNameWithRect, contentType).ToString();
            }

            return vision;
        }

        private static Stream GenerateImage(Vision vision, string penColor, string fillColor, Stream fileStream)
        {
            var accentColor = ColorFromHex(vision.color.accentColor);
            var borderColor = ContrastColor(accentColor);
            var rectColor = new Color();

            if (!string.IsNullOrEmpty(penColor))
            {
                borderColor = ColorFromHex(penColor);
            }
            if (!string.IsNullOrEmpty(fillColor))
            {
                rectColor = ColorFromHex(fillColor);
            }

            var image = Image.FromStream(fileStream);

            using (var g = Graphics.FromImage(image))
            {
                foreach (var face in vision.faces)
                {
                    var x = face.faceRectangle.left;
                    var y = face.faceRectangle.top;
                    var width = face.faceRectangle.width;
                    var height = face.faceRectangle.height;

                    if (!string.IsNullOrEmpty(penColor))
                    {
                        var rectangle = new Rectangle(x, y, width, height);
                        var pen = new Pen(borderColor, 2)
                        {
                            Alignment = PenAlignment.Outset
                        };

                        g.DrawRectangle(pen, rectangle);
                    }

                    if (!string.IsNullOrEmpty(fillColor))
                    {
                        var rectangleF = new RectangleF(x, y, width, height);
                        var customColor = Color.FromArgb(50, rectColor);
                        var shadowBrush = new SolidBrush(customColor);

                        g.FillRectangles(shadowBrush, new[] {rectangleF});
                    }
                }
            }

            var stream = ToStream(image, ImageFormat.Jpeg);
            return stream;
        }

        private static Color ContrastColor(Color color)
        {
            int d = 0;

            // Counting the perceptive luminance - human eye favors green color... 
            double a = 1 - (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;

            if (a < 0.5)
                d = 0; // bright colors - black font
            else
                d = 255; // dark colors - white font

            return Color.FromArgb(d, d, d);
        }

        private static Color ColorFromHex(string hexColor)
        {
            var penColor = new Color();
            var colorConvert = new ColorConverter();

            if (!hexColor.Contains("#"))
            {
                hexColor = string.Format("#{0}", hexColor);
            }

            var convertFromString = colorConvert.ConvertFromString(hexColor);

            if (convertFromString != null)
            {
                penColor = (Color)convertFromString;
            }
            return penColor;
        }

        public static Stream ToStream(Image image, ImageFormat formaw)
        {
            var stream = new MemoryStream();
            image.Save(stream, formaw);
            stream.Position = 0;
            return stream;
        }

        private static string ExtractExtension(HttpContent file)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var fileStreamName = file.Headers.ContentDisposition.FileName;
            var fileName = new string(fileStreamName.Where(x => !invalidChars.Contains(x)).ToArray());
            var extension = Path.GetExtension(fileName);

            return extension;
        }

        private async Task<Vision> GetVisionFromImage(Uri blobUri)
        {
            var computerVisionUri = new Uri(string.Format("{0}?visualFeatures=All", ComputerVision.Resources.Urls.VisionAnalyses));

            var httpClient = new HttpClient();

            var request = new HttpRequestMessage
            {
                RequestUri = computerVisionUri,
                Method = HttpMethod.Post
            };


            var json = string.Format(@"{{""Url"":""{0}""}}", blobUri);

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Add("Ocp-Apim-Subscription-Key", _cloudVisionKey);

            var httpResponseMessage = await httpClient.SendAsync(request).ConfigureAwait(false);
            httpResponseMessage.EnsureSuccessStatusCode();

            var response = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            var jObject = JObject.Parse(response);
            var vision = JsonConvert.DeserializeObject<Vision>(jObject.ToString());
            return vision;
        }

        private Uri UploadToBlobStorage(Stream fileStream, string imageName, string contentType)
        {
            var storageAccount = CloudStorageAccount.Parse(_storageConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("vision");
            container.CreateIfNotExists();


            var blockBlob = container.GetBlockBlobReference(imageName);
            blockBlob.Properties.ContentType = contentType;
            blockBlob.UploadFromStream(fileStream);

            return blockBlob.Uri;
        }
    }
}
