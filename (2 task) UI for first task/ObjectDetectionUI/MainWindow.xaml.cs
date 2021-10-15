using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using YOLOv4MLNet.DataStructures;
using YOLOv4MLNet;
using System.IO;
using System.Threading;

namespace ObjectDetectionUI
{
    public partial class MainWindow : Window
    {
        public List<YoloV4Result> modelOutput { get; set; }
        static object myLocker = new object();

        public MainWindow()
        {
            modelOutput = new List<YoloV4Result>();
            InitializeComponent();
            Predictor.Notify += DisplayMessage;

            MainListBox.ItemsSource = modelOutput;

        }

        private void OpenMenuItemClicked(object sender, RoutedEventArgs e)
        {
            modelOutput = new List<YoloV4Result>();
            ProcessedFilesTextBlock.Items.Clear();
            System.Windows.Forms.FolderBrowserDialog openFileDlg = new System.Windows.Forms.FolderBrowserDialog();
            openFileDlg.ShowDialog();
            string folderPath = openFileDlg.SelectedPath;
            SelectedFolderTextBlock.Text = folderPath;
            if (!string.IsNullOrEmpty(folderPath))
            {
                var t = new Thread(() =>
                {
                    modelOutput = Predictor.MakePredictions(folderPath);
                    Dispatcher.Invoke(new Action(() => { MainListBox.ItemsSource = modelOutput; }));
                });
                t.Start();
            }
            
        }

        private void DisplayMessage(string message, List<YoloV4Result> objectsList)
        {
            lock (myLocker)
            {
                //MessageBox.Show(message);
                Dispatcher.Invoke(new Action(() => { ProcessedFilesTextBlock.Items.Add(System.IO.Path.GetFileName(message)); }));
            }
        }

        private void SelectFolderButtonClicked(object sender, RoutedEventArgs e)
        {
            OpenMenuItemClicked(sender, e);
        }

        private void AbortButtonClicked(object sender, RoutedEventArgs e)
        {
            
            SelectedFolderTextBlock.Text = "Abort is in process";
            MainListBox.ItemsSource = null;
            MainListBox.ItemsSource = modelOutput;
        }
    }
}
