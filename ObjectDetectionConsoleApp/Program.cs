using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using YOLOv4MLNet.DataStructures;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;

namespace YOLOv4MLNet
{
    class Program
    {
        const string imageFolder = @"..\..\..\..\ObjectDetectionLib\assets\images";
        static List<YoloV4Result> modelOutput;
        static Dictionary<string, int> modelDictOutput = new Dictionary<string, int>();

        static void Main()
        {
            var sw = new Stopwatch();
            sw.Start();

            modelOutput = Predictor.MakePredictios(imageFolder);
            Console.WriteLine();

            foreach (YoloV4Result entry in modelOutput)           
                if (modelDictOutput.ContainsKey(entry.Label))
                    modelDictOutput[entry.Label] += 1;
                else
                    modelDictOutput.Add(entry.Label, 1);

            foreach (KeyValuePair<string, int> entry in modelDictOutput)
            {
                Console.WriteLine($"    {entry.Value} {entry.Key}(s)");
            }
            

            sw.Stop();
            Console.WriteLine($"\nDone in {sw.ElapsedMilliseconds}ms.");
        }
    }
}
