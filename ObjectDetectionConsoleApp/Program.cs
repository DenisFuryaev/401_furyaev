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
        static int imagesProcessed = 0;

        private static void DisplayMessage(string message)
        {
            imagesProcessed++;
            int progress = (int)((float)imagesProcessed / Predictor.imagesCount * 100);
            Console.WriteLine($"{(int)progress} %  processing {Path.GetFileName(message)}");
        }

        static void Main()
        {
            Predictor.Notify += DisplayMessage;

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
