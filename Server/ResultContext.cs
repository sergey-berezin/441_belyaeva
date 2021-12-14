﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Collections;
using ModelLibrary;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Server
{
    public class DetectedObject
    {
        public int DetectedObjectId { get; set; }
        public string Path { get; set; }
        public float x1 { get; set; }
        public float y1 { get; set; }
        public float x2 { get; set; }
        public float y2 { get; set; }
        public byte[] BitmapImage { get; set; }
        public string OutputPath { get; set; }
        public Results Type { get; set; }
        public override string ToString()
        {
            return OutputPath;
        }
    }

    public class Results// : IEnumerable<string>
    {
        public int ResultsId { get; set; }
        public string Type { get; set; }
        public ICollection<DetectedObject> DetectedObjects { get; set; }

        public override string ToString()
        {
            return Type;
        }
    }

    
    public class ResultContext : DbContext
    {
        //public UserContext() : base("DbConnection") { }
        public DbSet<Results> Results { get; set; }
        public DbSet<DetectedObject> DetectedObject { get; set; }
        //public DbSet<DetectedObjectDetails> DetectedObjectDetails { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder o)
           => o.UseSqlite(@"Data Source=D:\MLResults.db");

        public static byte[] ImageToByte2(Image img)
        {
            using (var stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }

        public void AddElem (string type, string path, float[] BBox, Bitmap bitmap)
        {
            var dobj = new DetectedObject();

            dobj.Type = new Results();
            var query = Results.Where(p => type == p.Type);
            byte[] byteArrImage = ImageToByte2(bitmap);
            if (query.Count() > 0)
            {
                dobj.Type = query.First();
            }
            else
            {
                dobj.Type = new Results();
                dobj.Type.Type = type;
                Results.Add(dobj.Type);
            }

            var f = DetectedObject.Where(p => p.x1 == BBox[0] && p.y1 == BBox[1] && p.x2 == BBox[2] && p.y2 == BBox[3]
                                         && p.Type.Type == type);

            if (f.Count() == 0 || !byteArrImage.SequenceEqual(f.First().BitmapImage))
            {
                dobj.x1 = BBox[0];
                dobj.y1 = BBox[1];
                dobj.x2 = BBox[2];
                dobj.y2 = BBox[3];
                dobj.BitmapImage = ImageToByte2(bitmap);
                DetectedObject.Add(dobj);
            }

            SaveChanges();
        }

        public IEnumerable<string> GetTypes()
        {
            foreach (var res in Results)
            {
                yield return res.Type;
            }
        }

        public IEnumerable<byte[]> GetObjectsByType(string type)
        {
            foreach (var obj in DetectedObject.Where(p => p.Type.Type == type))
            {
                yield return obj.BitmapImage;
            }
        }

        public void Clear()
        {
            foreach (var dobj in DetectedObject)
            {
                DetectedObject.Remove(dobj);
            }
            foreach (var res in Results)
            {
                Results.Remove(res);
            }
            SaveChanges();
        }
        

        public void DeleteType(string type)
        {
            foreach (var obj in DetectedObject.Where(p => p.Type.Type == type))
            {
                //yield return obj.BitmapImage;
                DetectedObject.Remove(obj);
            }

            foreach (var res in Results)
            {
                if (res.Type == type)
                {
                    Results.Remove(res);
                }
            }

            SaveChanges();
        } 
    }

    public class Sandbox
    {
        public static async Task Consumer()
        {
            var db = new ResultContext();
            
                while (true)
                {
                    string type;
                    string image;
                    Bitmap bitmap;
                    float[] BBox;

                    (type, image, bitmap, BBox) = await Detector.resultBufferBlock.ReceiveAsync();
                    if (type == "end")
                    {
                        db.SaveChanges();
                        break;
                    }
                    else
                    {
                        db.AddElem(type, image, BBox, bitmap);
                    }

                }
        }
    }
}
