using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using YOLOv4MLNet.DataStructures;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;
using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YOLOv4MLNet
{
    public class Predictor
    {
        // model is available here: https://github.com/onnx/models/tree/master/vision/object_detection_segmentation/yolov4
        private static string modelPath = Path.GetFullPath(@"..\..\..\..\ObjectDetectionLib\assets\model\yolov4.onnx");

        static readonly string[] classesNames = new string[] { "person", "bicycle", "car", "motorbike", "aeroplane", "bus", "train",
            "truck", "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse",
            "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
            "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket",
            "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange", "broccoli",
            "carrot", "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant", "bed", "diningtable", "toilet", "tvmonitor",
            "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book",
            "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush" };
        private static ConcurrentBag<YoloV4Result> modelOutput;
        public delegate void BarHandler(string fileName, List<YoloV4Result> objectsList);
        static public event BarHandler Notify;
        public static int imagesCount;
        public static CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
        private static readonly CancellationToken token = cancelTokenSource.Token;

        public static List<YoloV4Result> MakePredictions(string imageFolder, int threadCount = 2)
        {
            modelOutput = new ConcurrentBag<YoloV4Result>();
            imagesCount = Directory.GetFiles(imageFolder, "*", SearchOption.TopDirectoryOnly).Length;
            MLContext mlContext = new MLContext();
            var pipeline = mlContext.Transforms.ResizeImages(inputColumnName: "bitmap", outputColumnName: "input_1:0", imageWidth: 416, imageHeight: 416, resizing: ResizingKind.IsoPad)
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
            var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<YoloV4BitmapData>()));
            string[] fileEntries = Directory.GetFiles(imageFolder);
            

            var actionBlock = new ActionBlock<string>(imagePath =>
            {
                ConcurrentBag<YoloV4Result> objectsBag = new ConcurrentBag<YoloV4Result>();
                var predictionEngine = mlContext.Model.CreatePredictionEngine<YoloV4BitmapData, YoloV4Prediction>(model);
                using var bitmap = new Bitmap(Image.FromFile(imagePath));
                var predict = predictionEngine.Predict(new YoloV4BitmapData() { Image = bitmap });
                var results = predict.GetResults(classesNames, 0.3f, 0.7f);

                foreach (YoloV4Result res in results)
                {
                    modelOutput.Add(res); 
                    objectsBag.Add(res);
                }

                Notify?.Invoke(imagePath, objectsBag.ToList());

                objectsBag.Clear();

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = threadCount
            });

            Parallel.ForEach(fileEntries, new ParallelOptions { CancellationToken = token }, imagePath => { actionBlock.Post(imagePath); });

            actionBlock.Complete();
            actionBlock.Completion.Wait();
            return modelOutput.ToList();
        }

        
    }
}



//using (var g = Graphics.FromImage(bitmap))
//{
//    foreach (var res in results)
//    {
//        // draw predictions
//        var x1 = res.BBox[0];
//        var y1 = res.BBox[1];
//        var x2 = res.BBox[2];
//        var y2 = res.BBox[3];
//        g.DrawRectangle(Pens.Red, x1, y1, x2 - x1, y2 - y1);
//        using (var brushes = new SolidBrush(Color.FromArgb(50, Color.Red)))
//        {
//            g.FillRectangle(brushes, x1, y1, x2 - x1, y2 - y1);
//        }
//
//        g.DrawString(res.Label + " " + res.Confidence.ToString("0.00"),
//                     new Font("Arial", 12), Brushes.Blue, new PointF(x1, y1));
//    }
//    bitmap.Save(Path.Combine(imageOutputFolder, Path.ChangeExtension(imageName, "_processed" + Path.GetExtension(imageName))));
//}