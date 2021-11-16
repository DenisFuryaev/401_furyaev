using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using YOLOv4MLNet.DataStructures;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;

namespace YOLOv4MLNet
{
    class ImageObject
    {
        public int ImageObjectId { get; set; }
        public string Label { get; set; }
        public string FileName { get; set; }
    }

    class ObjectContext : DbContext
    {
        public DbSet<ImageObject> ImageObjects { get; set; }

        public ObjectContext() : base()
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder o) => o.UseSqlite("Data Source=database.db");
    }


    class Program
    {
        private const string imageFolder = @"C:\Users\denis\Documents\Programming\C#\Homework 4 course\401_furyaev\(1 task) Concurrent Object Detection\ObjectDetectionConsoleApp\images";
        private static List<YoloV4Result> modelOutput;
        private static readonly Dictionary<string, int> modelDictOutput = new Dictionary<string, int>();
        static int imagesProcessed = 0;
        static Object myLocker = new Object();
        static int objectId = 0;


        static void Main()
        {
            ConfigureDatabase();

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

        private static void ConfigureDatabase()
        {
            using (ObjectContext context = new ObjectContext())
            {
                
            }
        }
        protected static void MyHandler(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine("\nStopping all threads and exiting program");
            Predictor.cancellationTokenSource.Cancel();
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

                // putting objects in database
                using (ObjectContext context = new ObjectContext())
                {
                    foreach (YoloV4Result detectedObject in objectsList)
                    {
                        objectId++;
                        ImageObject imageObject = new ImageObject { ImageObjectId = objectId, FileName = Path.GetFileName(message), Label = detectedObject.Label };
                        context.ImageObjects.Add(imageObject);
                    }
                    context.SaveChanges();
                }
            }
        }
    }
}
