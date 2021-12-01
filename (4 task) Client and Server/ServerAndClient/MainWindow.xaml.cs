using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using YOLOv4MLNet.DataStructures;
using YOLOv4MLNet;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Drawing;


namespace ObjectDetectionUI
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



    public class ObjectsData : IEnumerable<string>, INotifyCollectionChanged
    {
        public class Value
        {
            public int Count { get; set; }
            public Dictionary<string, List<float[]>> Dict { get; set; }

            public Value(int count, Dictionary<string, List<float[]>> dict)
            {
                this.Count = count;
                this.Dict = dict;
            }
        }

        // dictionary data structure: <object_name, <count, list<files, list<boundaries>>>>
        public Dictionary<string, Value> DetectedObjects { get; set; }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public ObjectsData()
        {
            DetectedObjects = new Dictionary<string, Value>();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Clear()
        {
            DetectedObjects.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Add(List<YoloV4Result> objectsList, string fileName)
        {
            foreach (YoloV4Result obj in objectsList)
                if (DetectedObjects.ContainsKey(obj.Label)) // outer dictionary already has this object
                    if (DetectedObjects[obj.Label].Dict.ContainsKey(fileName)) // inner dictionary already has such filename
                    {
                        DetectedObjects[obj.Label].Count += 1;
                        DetectedObjects[obj.Label].Dict[fileName].Add(obj.BBox);
                    }
                    else
                    {
                        DetectedObjects[obj.Label].Count += 1;
                        DetectedObjects[obj.Label].Dict.Add(fileName, new List<float[]> { obj.BBox });
                    }

                else // outer dictionary doesn't have this object
                {
                    DetectedObjects.Add(obj.Label, new Value(1, new Dictionary<string, List<float[]>> { { fileName, new List<float[]> { obj.BBox } } }));
                }

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach (string obj in DetectedObjects.Keys)
            {
                yield return obj;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<string> ProcessedFiles { get; set; }
        public ObjectsData objectsDict;
        private static readonly object myLocker = new object();

        public MainWindow()
        {
            InitializeComponent();
            ProcessedFiles = new ObservableCollection<string>();
            objectsDict = new ObjectsData();

            Predictor.Notify += PredictorEventHandler;
            ProcessedFilesListBox.SelectionChanged += ProcessedFilesListBox_SelectionChanged;
            ObjectsListBox.SelectionChanged += ObjectsListBox_SelectionChanged;

            ProcessedFilesListBox.ItemsSource = ProcessedFiles;
            ObjectsListBox.ItemsSource = objectsDict;

            ConfigureDatabase();
        }
        private void PredictorEventHandler(string filePath, List<YoloV4Result> objectsList)
        {
            lock (myLocker)
            {
                Dispatcher.Invoke(new Action(() => { 
                    objectsDict.Add(objectsList, filePath);
                    ProcessedFiles.Add(Path.GetFileName(filePath));
                    // putting objects in database
                    using (ObjectContext context = new ObjectContext())
                    {
                        foreach (YoloV4Result detectedObject in objectsList)
                        {

                            int x = (int)detectedObject.BBox[0];
                            int y = (int)detectedObject.BBox[1];
                            int width = (int)detectedObject.BBox[2] - x;
                            int height = (int)detectedObject.BBox[3] - y;

                            System.Drawing.Image image = System.Drawing.Image.FromFile(filePath);
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
                }));
            }
        }
        private void ConfigureDatabase()
        {
            using (ObjectContext context = new ObjectContext())
            {
                // printing database rows to console
                DatabaseListBox.Items.Clear();
                var query = context.ImageObjects;
                var sb = new System.Text.StringBuilder();
                foreach (var item in query)
                {
                    sb = new System.Text.StringBuilder();
                    sb.Append(String.Format("{0,3} {1,15} {2,5} {3,5} {4,5} {5,5}\n", item.ImageObjectId, item.Label, item.X, item.Y, item.Width, item.Height));
                    DatabaseListBox.Items.Add(sb.ToString());
                }
                

                // deleting all database rows if user selected yes button
                if (MessageBox.Show("Do you want to delete recorded objects from Database?", "Save file", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    context.ImageObjects.RemoveRange(context.ImageObjects);
                    context.SaveChanges();
                }
            }
        }
        private byte[] ImageToByteArray(System.Drawing.Image imageIn)
        {
            using (var ms = new MemoryStream())
            {
                imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Gif);

                return ms.ToArray();
            }
        }
        private bool DatabaseHasObject(int x, int y, int width, int height, byte[] blob)
        {
            bool hasObject = false;
            using (ObjectContext context = new ObjectContext())
            {
                var blobQuery = from item in context.ImageObjects
                                where item.X == x && item.Y == y && item.Width == width && item.Height == height
                                select item.ObjectImage;
                foreach (byte[] blobItem in blobQuery)
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

        private void ObjectsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ObjectsListBox.SelectedItem == null)
                return;

            // draw all images with "selected object"
            
            ImageListBox.Items.Clear();
            foreach (string filename in objectsDict.DetectedObjects[(string)ObjectsListBox.SelectedItem].Dict.Keys)
            {
                System.Windows.Controls.Image myLocalImage = new System.Windows.Controls.Image();
                myLocalImage.Height = 200;
                myLocalImage.Margin = new Thickness(5);

                BitmapImage myImageSource = new BitmapImage();
                myImageSource.BeginInit();
                myImageSource.UriSource = new Uri(filename);
                myImageSource.EndInit();
                myLocalImage.Source = myImageSource;

                ImageListBox.Items.Add(myLocalImage);
            }

            // draw all objects that Predictor Class has labled with "selected object"
            ObjectsImagesListBox.Items.Clear();
            foreach (string filename in objectsDict.DetectedObjects[(string)ObjectsListBox.SelectedItem].Dict.Keys)
            {
                foreach (float[] box in objectsDict.DetectedObjects[(string)ObjectsListBox.SelectedItem].Dict[filename])
                {
                    int x1 = (int)box[0];
                    int y1 = (int)box[1];
                    int x2 = (int)box[2];
                    int y2 = (int)box[3];

                    System.Windows.Controls.Image myLocalImage = new System.Windows.Controls.Image();
                    myLocalImage.Height = 200;
                    myLocalImage.Margin = new Thickness(5);

                    BitmapImage myImageSource = new BitmapImage();
                    myImageSource.BeginInit();
                    myImageSource.UriSource = new Uri(filename);
                    myImageSource.EndInit();

                    CroppedBitmap cb = new CroppedBitmap((BitmapSource)myImageSource, new Int32Rect(x1, y1, x2 - x1, y2 - y1));
                    myLocalImage.Source = cb;

                    ObjectsImagesListBox.Items.Add(myLocalImage);
                }
            }
        }
        private void ProcessedFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProcessedFilesListBox.SelectedItem == null)
                return;

            try
            {
                SelectedImage.Source = new BitmapImage(new Uri(SelectedFolderListBox.Text + "\\" + ProcessedFilesListBox.SelectedItem.ToString()));
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        public void ClearAll()
        {
            ProcessedFiles.Clear();
            objectsDict.Clear();
            Predictor.cancellationTokenSource = new CancellationTokenSource();
        }
        private void OpenMenu_ItemClicked(object sender, RoutedEventArgs e)
        {
            ClearAll();

            System.Windows.Forms.FolderBrowserDialog openFileDlg = new System.Windows.Forms.FolderBrowserDialog();
            openFileDlg.ShowDialog();
            string folderPath = openFileDlg.SelectedPath;
            SelectedFolderListBox.Text = folderPath;
            if (!string.IsNullOrEmpty(folderPath))
            {
                Task t = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        _ = Dispatcher.BeginInvoke(new Action(() => { InfoButton.Background = new SolidColorBrush(Colors.Salmon); InfoButtonTextBlock.Text = "Busy"; }));
                        _ = Predictor.MakePredictions(folderPath);
                        _ = Dispatcher.BeginInvoke(new Action(() => { InfoButton.Background = new SolidColorBrush(Colors.LightGreen); InfoButtonTextBlock.Text = "Done"; }));
                    }
                    catch (Exception exc)
                    {
                        Console.Error.WriteLine(exc.Message);
                    }
                });
            }
        }

        private void SelectFolder_ButtonClicked(object sender, RoutedEventArgs e)
        {
            OpenMenu_ItemClicked(sender, e);
        }
        private void Abort_ButtonClicked(object sender, RoutedEventArgs e)
        {
            InfoButtonTextBlock.Text = "Abort!";
            InfoButton.Background = new SolidColorBrush(Colors.OrangeRed);
            Predictor.cancellationTokenSource.Cancel();
        }
    }
}
