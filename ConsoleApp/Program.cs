using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ModelLibrary;
using System.Collections.Generic;
using System.Data.Entity;
using Microsoft.ML;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using YOLOv4MLNet.DataStructures;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;
using System.Collections.Concurrent;
using System.Threading;

namespace ConsoleApp
{
    public class DetectedObject
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        //public float x1 { get; set; }
        //public float y1 { get; set; }
        //public float x2 { get; set; }
        //public float y2 { get; set; }
        public byte[] BitmapImage { get; set; }
    }

    class UserContext : DbContext
    {
        public UserContext() : base("DbConnection") { }

        public DbSet<DetectedObject> DetectedObjects { get; set; }
    }

    class Program
    {
        public static byte[] ImageToByte2(Image img)
        {
            using (var stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }
        private static async Task Consumer()
        {
            string type;
            string image;
            Bitmap bitmap;

            using (UserContext db = new UserContext())
            {
                while (true)
                {
                    (type, image, bitmap) = await Detector.resultBufferBlock.ReceiveAsync();
                    if (type == "end")
                    {
                        db.SaveChanges();
                        break;
                    }
                    DetectedObject result = new DetectedObject { Type = type, Path = image, BitmapImage = ImageToByte2(bitmap)};
                    db.DetectedObjects.Add(result);
                }

                var detectedObjects = db.DetectedObjects;
                Console.WriteLine("Список объектов:");
                foreach (DetectedObject dobj in detectedObjects)
                {
                    Console.WriteLine("{0} - {1}", dobj.Type, dobj.Path);
                }
            }
        }
        static async Task Main()
        {
            //var bufferBlock = new BufferBlock<string>();
            Console.WriteLine("Write input path: ");
            //string imageFolder = Console.ReadLine();
            string imageFolder = @"D:\models\Assets\Images";
            //await Detector.DetectImage(imageFolder);

            await Task.WhenAll(Detector.DetectImage(imageFolder), Consumer());

            /*
            while (Detector.bufferBlock.TryReceive(out string value))
            {
                Console.WriteLine(value);
            }
            */
        }
    }
}
