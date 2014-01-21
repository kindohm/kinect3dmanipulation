using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace Kinect3dManipulation
{
    public partial class MainWindow : Window
    {
        BackgroundWorker imageWorker;
        HandsTrackball trackball;

        public MainWindow()
        {
            InitializeComponent();

            this.trackball = new HandsTrackball();
            this.trackball.Dispatcher = this.Dispatcher;
            this.trackball.EventSource = this.trackBorder;

            CompositionTarget.Rendering += new EventHandler(CompositionTarget_Rendering);
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            this.SizeChanged += new SizeChangedEventHandler(MainWindow_SizeChanged);
        }

        void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.trackball.Width = this.trackBorder.ActualWidth;
            this.trackball.Height = this.trackBorder.ActualHeight;
        }

        void CompositionTarget_Rendering(object sender, System.EventArgs e)
        {
            if (!this.imageWorker.IsBusy)
            {
                this.imageWorker.RunWorkerAsync();
            }
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.viewport.Camera.Transform = this.trackball.Transform;

            this.imageWorker = new BackgroundWorker();
            this.imageWorker.DoWork += new DoWorkEventHandler(imageWorker_DoWork);
        }

        void imageWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.Sleep(20);
            Dispatcher.BeginInvoke((Action)delegate
            {
                this.depthImage.Source = Kinect.GetDepthFrame();
                this.rawImage.Source = Kinect.GetColorVideoFrame();
            });
        }
    }
}
