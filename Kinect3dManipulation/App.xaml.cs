using System.Windows;

namespace Kinect3dManipulation
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Kinect.Initialize();
        }
     
        protected override void OnExit(ExitEventArgs e)
        {
            Kinect.Cleanup();
            base.OnExit(e);

        }

        
    }
}
