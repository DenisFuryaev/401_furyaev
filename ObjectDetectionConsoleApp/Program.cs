using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using YOLOv4MLNet.DataStructures;


namespace YOLOv4MLNet
{
    class Program
    {
        private const string imageFolder = @"..\..\..\..\ObjectDetectionLib\assets\images";
        private static List<YoloV4Result> modelOutput;
        private static Dictionary<string, int> modelDictOutput = new Dictionary<string, int>();

        static void Main()
        {
            var sw = new Stopwatch();
            sw.Start();
            
            modelOutput = Predictor.MakePredictions(Path.GetFullPath(imageFolder));
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
