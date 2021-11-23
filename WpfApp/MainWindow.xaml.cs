using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ModelLibrary;
using Ookii.Dialogs.Wpf;

namespace WpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        public static ObservableCollection<Results> resultCollection = new ObservableCollection<Results>();

        private static async Task Consumer()
        {
            string type;
            string image;

            while (true)
            {
                (type, image) = await Detector.resultBufferBlock.ReceiveAsync();
                if (type == "end")
                {
                    break;
                }
                    
                bool flag = true;
                foreach (Results r in resultCollection)
                {
                    if (r.Info == type)
                    {
                        r.images.Add(image);
                        flag = false;
                        break;
                    }
                }
                if (flag)
                {
                    resultCollection.Add(new Results(type, image));
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = resultCollection;
        }

        private void btnSelect_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            if ((bool)dialog.ShowDialog())
                TextBox_Path.Text = dialog.SelectedPath;
        }

        private async void btnRun_Click(object sender, RoutedEventArgs e)
        {
            btnRun.IsEnabled = false;
            resultCollection.Clear();
            Detector.cancelTokenSource = new CancellationTokenSource();
            Detector.token = Detector.cancelTokenSource.Token;

            await Task.WhenAll(Detector.DetectImage(TextBox_Path.Text), Consumer());
            btnRun.IsEnabled = true;   
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Detector.cancelTokenSource.Cancel();
            btnRun.IsEnabled = true;
        }
    }
}
