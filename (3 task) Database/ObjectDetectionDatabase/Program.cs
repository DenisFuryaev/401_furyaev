using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using YOLOv4MLNet.DataStructures;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Linq;

namespace YOLOv4MLNet
{
    class ImageObject
    {
        public int ImageObjectId { get; set; }
        public string Label { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] ObjectImage { get; set; }
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


        static void Main()
        {
            ConfigureDatabase();

            Console.WriteLine(" Press CTRL + C to stop program execution\n");

            Predictor.Notify += DisplayMessage;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(MyHandler);


            var sw = new Stopwatch();
            sw.Start();

            modelOutput = Predictor.MakePredictions(Path.GetFullPath(imageFolder));

            //Console.WriteLine($"\n Objects found in images from folder {Path.GetFullPath(imageFolder)}:");
            //foreach (YoloV4Result entry in modelOutput)
            //    if (modelDictOutput.ContainsKey(entry.Label))
            //        modelDictOutput[entry.Label] += 1;
            //    else
            //        modelDictOutput.Add(entry.Label, 1);
            //foreach (KeyValuePair<string, int> entry in modelDictOutput)
            //    Console.WriteLine($"    {entry.Value} {entry.Key}(s)");

            sw.Stop();
            Console.WriteLine($"\nDone in {sw.ElapsedMilliseconds}ms.");
        }

        private static void ConfigureDatabase()
        {
            using (ObjectContext context = new ObjectContext())
            {
                // printing database rows to console
                var query = context.ImageObjects;
                var sb = new System.Text.StringBuilder();
                sb.Append(String.Format("{0,3} {1,15} {2,5} {3,5} {4,5} {5,5}\n\n", "Id", "Label", "X", "Y", "Width", "Height"));
                foreach (var item in query)
                    sb.Append(String.Format("{0,3} {1,15} {2,5} {3,5} {4,5} {5,5}\n", item.ImageObjectId, item.Label, item.X, item.Y, item.Width, item.Height));
                Console.WriteLine(sb);

                // deleting all database rows if user input is 'y'
                char input;
                Console.Write("Do you want to delete recorded objects from Database? (y/n): ");
                while (((input = Console.ReadLine()[0]) != 'y') && (input != 'n'))
                    Console.Write("Please enter y or n: ");
                if (input == 'y')
                {
                    context.ImageObjects.RemoveRange(context.ImageObjects);
                    context.SaveChanges();
                }
            }
        }
        protected static void MyHandler(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine("\nStopping all threads and exiting program");
            Predictor.cancellationTokenSource.Cancel();
        }
        private static byte[] ImageToByteArray(System.Drawing.Image imageIn)
        {
            using (var ms = new MemoryStream())
            {
                imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Gif);

                return ms.ToArray();
            }
        }
        private static bool DatabaseHasObject(int x, int y, int width, int height, byte[] blob)
        {
            bool hasObject = false;
            using (ObjectContext context = new ObjectContext())
            {
                var blobQuery = from item in context.ImageObjects
                                where item.X == x && item.Y == y && item.Width == width && item.Height == height
                                select item.ObjectImage;
                foreach(byte[] blobItem in blobQuery)
                {
                    if (blobItem.SequenceEqual(blob))
                    {
                        hasObject = true;
                        break;
                    }
                }
            }
            return hasObject;
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

                        int x = (int)detectedObject.BBox[0];
                        int y = (int)detectedObject.BBox[1];
                        int width = (int)detectedObject.BBox[2] - x;
                        int height = (int)detectedObject.BBox[3] - y;

                        Image image = Image.FromFile(message);
                        Bitmap bmpImage = new Bitmap(image);
                        Bitmap croppedImage = bmpImage.Clone(new Rectangle(x, y, width, height), bmpImage.PixelFormat);
                        byte[] blob = ImageToByteArray(croppedImage);

                        if (DatabaseHasObject(x, y, width, height, blob))
                            continue;

                        ImageObject imageObject = new ImageObject { Label = detectedObject.Label, X = x, Y = y, Width = width, Height = height, ObjectImage = blob };
                        context.ImageObjects.Add(imageObject);
                    }
                    context.SaveChanges();
                }
            }
        }
    }
}
