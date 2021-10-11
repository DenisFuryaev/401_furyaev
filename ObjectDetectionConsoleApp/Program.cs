using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using YOLOv4MLNet.DataStructures;
using System.Threading;

namespace YOLOv4MLNet
{
    class Program
    {
        private const string imageFolder = @"..\..\..\..\ObjectDetectionLib\assets\images";
        private static List<YoloV4Result> modelOutput;
        private static readonly Dictionary<string, int> modelDictOutput = new Dictionary<string, int>();
        static int imagesProcessed = 0;
        static Object myLocker = new Object();


        static void Main()
        {
            Console.WriteLine(" Press CTRL + C to stop program execution\n");

            Predictor.Notify += DisplayMessage;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(MyHandler);

            var sw = new Stopwatch();
            sw.Start();

            modelOutput = Predictor.MakePredictions(Path.GetFullPath(imageFolder));
            Console.WriteLine($"\n Objects found in images from folder {Path.GetFullPath(imageFolder)}:");

            foreach (YoloV4Result entry in modelOutput)           
                if (modelDictOutput.ContainsKey(entry.Label))
                    modelDictOutput[entry.Label] += 1;
                else
                    modelDictOutput.Add(entry.Label, 1);
            foreach (KeyValuePair<string, int> entry in modelDictOutput)
                Console.WriteLine($"    {entry.Value} {entry.Key}(s)");
            
            sw.Stop();
            Console.WriteLine($"\nDone in {sw.ElapsedMilliseconds}ms.");
        }

        protected static void MyHandler(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine("\nStopping all threads and exiting program");
            Predictor.cancelTokenSource.Cancel();
            System.Environment.Exit(0);
        }
        private static void DisplayMessage(string message, List<YoloV4Result> objectsList)
        {
            lock (myLocker)
            {
                imagesProcessed++;
                int progress = (int)((float)imagesProcessed / Predictor.imagesCount * 100);
                Console.Write($"    {(int)progress} % {Path.GetFileName(message)} : ");
                foreach (YoloV4Result detectedObject in objectsList)
                    Console.Write($"{detectedObject.Label}, ");
                Console.WriteLine();
            }
        }
    }
}
