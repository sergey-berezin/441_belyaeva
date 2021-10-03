﻿using System;
using System.Collections.Generic;
using Microsoft.ML;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using YOLOv4MLNet.DataStructures;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;


// TODO: 
// 
// more tests
// cancelling
// info of object

namespace ModelLibrary
{
    public class Detector
    {
        // model is available here:
        // https://github.com/onnx/models/tree/master/vision/object_detection_segmentation/yolov4
        const string modelPath = @"C:\yolov4.onnx";

        static readonly string[] classesNames = new string[] { "person", "bicycle", "car", "motorbike", 
            "aeroplane", "bus", "train", "truck", "boat", "traffic light", "fire hydrant", "stop sign", 
            "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", 
            "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", 
            "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard",
            "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", 
            "apple", "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", 
            "chair", "sofa", "pottedplant", "bed", "diningtable", "toilet", "tvmonitor", "laptop", "mouse", 
            "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", 
            "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush" };


        public static async Task DetectImage(string imageFolder)
        {
            MLContext mlContext = new MLContext();

            // model is available here:
            // https://github.com/onnx/models/tree/master/vision/object_detection_segmentation/yolov4

            // Define scoring pipeline
            var pipeline = mlContext.Transforms.ResizeImages(inputColumnName: "bitmap", 
                outputColumnName: "input_1:0", imageWidth: 416, imageHeight: 416, resizing: ResizingKind.IsoPad)
                .Append(mlContext.Transforms.ExtractPixels(outputColumnName: "input_1:0", scaleImage: 1f / 255f, interleavePixelColors: true))
                .Append(mlContext.Transforms.ApplyOnnxModel(
                    shapeDictionary: new Dictionary<string, int[]>()
                    {
                        { "input_1:0", new[] { 1, 416, 416, 3 } },
                        { "Identity:0", new[] { 1, 52, 52, 3, 85 } },
                        { "Identity_1:0", new[] { 1, 26, 26, 3, 85 } },
                        { "Identity_2:0", new[] { 1, 13, 13, 3, 85 } },
                    },
                    inputColumnNames: new[]
                    {
                        "input_1:0"
                    },
                    outputColumnNames: new[]
                    {
                        "Identity:0",
                        "Identity_1:0",
                        "Identity_2:0"
                    },
                    modelFile: modelPath, recursionLimit: 100));

            // Fit on empty list to obtain input data schema
            var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<YoloV4BitmapData>()));
            // Create prediction engine
            var predictionEngine = mlContext.Model.CreatePredictionEngine<YoloV4BitmapData, YoloV4Prediction>(model);

            ConcurrentBag<string> detectedObjects = new ConcurrentBag<string>();
            string[] imageNames = Directory.GetFiles(imageFolder);
            ProcessedImages processedImages = new ProcessedImages(imageNames.Length);
            object locker = new object();

            var sw = new Stopwatch();
            sw.Start();
            
            var ab = new ActionBlock<string>(async image => {
                YoloV4Prediction predict;
                lock (locker)
                {
                    var bitmap = new Bitmap(Image.FromFile(Path.Combine(image)));
                    predict = predictionEngine.Predict(new YoloV4BitmapData() { Image = bitmap });
                    processedImages.Add(image);
                }

                var results = predict.GetResults(classesNames, 0.3f, 0.7f);
                foreach (var res in results)
                {
                    detectedObjects.Add(res.Label);
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4
            });

            Parallel.For(0, imageNames.Length, i => ab.Post(imageNames[i]));
            ab.Complete();
            await ab.Completion;
            sw.Stop();

            Console.WriteLine($"Done in {sw.ElapsedMilliseconds}ms.");
            Console.WriteLine("List of finding objects: ");
            foreach (string obj in detectedObjects)
            {
                Console.WriteLine(obj);
            }
            Console.WriteLine($"Total number of objects: {detectedObjects.Count}");
        }
    }
}
