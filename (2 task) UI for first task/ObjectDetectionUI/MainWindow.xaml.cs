using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using YOLOv4MLNet.DataStructures;
using YOLOv4MLNet;
using System.IO;
using System.Threading;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Linq;
using System.Collections.Specialized;
using System.Collections;
using System.Windows.Controls;

namespace ObjectDetectionUI
{

    public class ObjectsData : IEnumerable<string>, INotifyCollectionChanged
    {
        public class Value
        {
            public int count { get; set; }
            public Dictionary<string, List<float[]>> dict { get; set; }

            public Value(int count, Dictionary<string, List<float[]>> dict)
            {
                this.count = count;
                this.dict = dict;
            }
        }

        // dictionary data structure: <object_name, <count, list<files, list<boundaries>>>>
        public Dictionary<string, Value> detectedObjects { get; set; }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public ObjectsData()
        {
            detectedObjects = new Dictionary<string, Value>();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Clear()
        {
            detectedObjects.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Add(List<YoloV4Result> objectsList, string fileName)
        {
            foreach (YoloV4Result obj in objectsList)
                if (detectedObjects.ContainsKey(obj.Label)) // outer dictionary already has this object
                    if (detectedObjects[obj.Label].dict.ContainsKey(fileName)) // inner dictionary already has such filename
                    {
                        detectedObjects[obj.Label].count += 1;
                        detectedObjects[obj.Label].dict[fileName].Add(obj.BBox);
                    }
                    else
                    {
                        detectedObjects[obj.Label].count += 1;
                        detectedObjects[obj.Label].dict.Add(fileName, new List<float[]>{ obj.BBox });
                    }

                else // outer dictionary doesn't have this object
                {
                    detectedObjects.Add(obj.Label, new Value(1, new Dictionary<string, List<float[]>> { { fileName, new List<float[]>{ obj.BBox } } }));
                }

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach(string obj in detectedObjects.Keys)
            {
                yield return obj;
                // detectedObjects[obj].Item1.ToString() + " " + obj + "(s)" 
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<string> processedFiles { get; set; }
        public ObjectsData objectsDict;
        private static object myLocker = new object();
        private Thread t;


        private void PredictorEventHandler(string message, List<YoloV4Result> objectsList)
        {
            lock (myLocker)
            {
                Dispatcher.Invoke(new Action(() => { objectsDict.Add(objectsList, message); processedFiles.Add(message); }));
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            processedFiles = new ObservableCollection<string>();
            objectsDict = new ObjectsData();

            Predictor.Notify += PredictorEventHandler;
            ProcessedFilesListBox.SelectionChanged += ProcessedFilesListBox_SelectionChanged;
            ObjectsListBox.SelectionChanged += ObjectsListBox_SelectionChanged;

            ProcessedFilesListBox.ItemsSource = processedFiles;
            ObjectsListBox.ItemsSource = objectsDict;
        }


        private void ObjectsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // draw all images with "selected object"
            ImageListBox.Items.Clear();
            foreach (string filename in objectsDict.detectedObjects[(string)ObjectsListBox.SelectedItem].dict.Keys)
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
            foreach (string filename in objectsDict.detectedObjects[(string)ObjectsListBox.SelectedItem].dict.Keys)
            {
                foreach (float[] box in objectsDict.detectedObjects[(string)ObjectsListBox.SelectedItem].dict[filename])
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
            try
            {
                SelectedImage.Source = new BitmapImage(new Uri((string)ProcessedFilesListBox.SelectedItem));
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }


        public void ClearAll()
        {
            processedFiles.Clear();
            objectsDict.Clear();
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
                t = new Thread(() =>
                {
                    try
                    {
                        Predictor.MakePredictions(folderPath);
                    }
                    catch (ThreadInterruptedException exc)
                    {
                        Predictor.cancelTokenSource.Cancel();
                        MessageBox.Show(exc.Message);
                    }
                });
                t.Start();
            }
            
        }


        private void SelectFolder_ButtonClicked(object sender, RoutedEventArgs e)
        {
            OpenMenu_ItemClicked(sender, e);
        }
        private void Abort_ButtonClicked(object sender, RoutedEventArgs e)
        {
            SelectedFolderListBox.Text = "Abort is in process";
            //t.Interrupt();
        }
    }
}
