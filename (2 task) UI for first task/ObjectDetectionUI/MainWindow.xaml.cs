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

namespace ObjectDetectionUI
{
    public partial class MainWindow : Window
    {
        public List<YoloV4Result> modelOutput { get; set; }
        public ObservableCollection<string> processedFiles { get; set; }
        static object myLocker = new object();

        public MainWindow()
        {

            InitializeComponent();
            modelOutput = new List<YoloV4Result>();
            processedFiles = new ObservableCollection<string>();

            Predictor.Notify += DisplayMessage;
            ProcessedFilesListBox.SelectionChanged += ProcessedFilesListBox_SelectionChanged;

            MainListBox.ItemsSource = modelOutput;
            ProcessedFilesListBox.ItemsSource = processedFiles;
        }

        private void ProcessedFilesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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

        private void OpenMenuItemClicked(object sender, RoutedEventArgs e)
        {
            modelOutput.Clear();
            processedFiles.Clear();

            System.Windows.Forms.FolderBrowserDialog openFileDlg = new System.Windows.Forms.FolderBrowserDialog();
            openFileDlg.ShowDialog();
            string folderPath = openFileDlg.SelectedPath;
            SelectedFolderListBox.Text = folderPath;
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
                Dispatcher.Invoke(new Action(() => { processedFiles.Add(message); }));
            }
        }

        private void SelectFolderButtonClicked(object sender, RoutedEventArgs e)
        {
            OpenMenuItemClicked(sender, e);
        }

        private void AbortButtonClicked(object sender, RoutedEventArgs e)
        {
            SelectedFolderListBox.Text = "Abort is in process";
        }
    }
}
