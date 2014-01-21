using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Research.Kinect.Nui;

namespace Kinect3dManipulation
{
    public static class Kinect
    {
        static bool initialized;
        static byte[] depthFrame32 = new byte[320 * 240 * 4];
        static Runtime runtime;
        static DateTime lastTime;
        static PlanarImage latestVideoImage;
        static PlanarImage latestDepthImage;
        static Joint rightHandJoint;
        static Joint leftHandJoint;

        const int RED_IDX = 2;
        const int GREEN_IDX = 1;
        const int BLUE_IDX = 0;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }
            try
            {
                runtime = new Runtime();
                runtime.Initialize(
                    RuntimeOptions.UseColor |
                    RuntimeOptions.UseDepthAndPlayerIndex |
                    RuntimeOptions.UseSkeletalTracking);

                runtime.SkeletonEngine.TransformSmooth = true;
                runtime.SkeletonEngine.SmoothParameters = new TransformSmoothParameters()
                {
                    Smoothing = .75f,
                    JitterRadius = 0.05f,
                    MaxDeviationRadius = 0.04f
                };

                runtime.VideoStream.Open(ImageStreamType.Video, Two,
                    ImageResolution.Resolution640x480, ImageType.Color);
                runtime.DepthStream.Open(ImageStreamType.Depth, Two,
                    ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);

                lastTime = DateTime.Now;

                runtime.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(RuntimeDepthFrameReady);
                runtime.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(RuntimeSkeletonFrameReady);
                runtime.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(RuntimeVideoFrameReady);

                initialized = true;
            
            }
            catch (InvalidOperationException)
            {
                Status = KinectStatus.NotConnected;
            }
        }

        public static Microsoft.Research.Kinect.Nui.Vector LeftHand { get; private set; }
        public static Microsoft.Research.Kinect.Nui.Vector RightHand { get; private set; }
        public static Microsoft.Research.Kinect.Nui.Vector Torso { get; private set; }

        static KinectStatus status = KinectStatus.WaitingToAcquire;
        public static KinectStatus Status
        {
            get
            {
                return status;
            }
            set
            {
                if (status != value)
                {
                    status = value;
                }
            }
        }

        public static BitmapSource GetColorVideoFrame()
        {
            if (latestVideoImage.Bits == null)
            {
                return null;
            }

            return BitmapSource.Create(
                latestVideoImage.Width, latestVideoImage.Height, NinetySix, NinetySix, PixelFormats.Bgr32, null,
                latestVideoImage.Bits, latestVideoImage.Width * latestVideoImage.BytesPerPixel);
        }

        const int NinetySix = 96;
        public static BitmapSource GetDepthFrame()
        {
            if (latestDepthImage.Bits == null)
            {
                return null;
            }
            var convertedDepthFrame = ConvertDepthFrame(latestDepthImage.Bits);
            return BitmapSource.Create(latestDepthImage.Width, latestDepthImage.Height, NinetySix, NinetySix,
                PixelFormats.Bgr32, null, convertedDepthFrame, latestDepthImage.Width * Four);
        }

        static void RuntimeVideoFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            latestVideoImage = e.ImageFrame.Image;
        }

        static void RuntimeSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            var skeleton = e.SkeletonFrame.Skeletons
                .Where(s => s.TrackingState == SkeletonTrackingState.Tracked)
                .FirstOrDefault();

            if (skeleton != null)
            {
                Status = KinectStatus.TrackingUser;
                leftHandJoint = skeleton.Joints[JointID.HandLeft];
                LeftHand = leftHandJoint.Position;
                rightHandJoint = skeleton.Joints[JointID.HandRight];
                RightHand = rightHandJoint.Position;
                Torso = skeleton.Joints[JointID.Spine].Position;
            }
            else
            {
                Status = KinectStatus.WaitingToAcquire;
            }
        }

        static void RuntimeDepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            latestDepthImage = e.ImageFrame.Image;
        }

        // constants for ConvertDepthFrame
        const int hexseven = 0x07;
        const int TwoFiftyFive = 255;
        const int RealDepth = 0x0fff;
        const int Five = 5;
        const int Three = 3;
        const int One = 1;
        const int Zero = 0;
        const int Two = 2;
        const int Four = 4;
        const int Six = 6;
        const int Seven = 7;

        static byte[] ConvertDepthFrame(byte[] depthFrame16)
        {
            for (int i16 = Zero, i32 = Zero;
                i16 < depthFrame16.Length && i32 < depthFrame32.Length;
                i16 += Two, i32 += Four)
            {
                int player = depthFrame16[i16] & hexseven;
                int realDepth = (depthFrame16[i16 + One] << Five) | (depthFrame16[i16] >> Three);
                byte intensity = (byte)(TwoFiftyFive - (TwoFiftyFive * realDepth / RealDepth));

                depthFrame32[i32 + RED_IDX] = Zero;
                depthFrame32[i32 + GREEN_IDX] = Zero;
                depthFrame32[i32 + BLUE_IDX] = Zero;

                switch (player)
                {
                    case Zero:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / Two);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / Two);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / Two);
                        break;
                    case One:
                        depthFrame32[i32 + RED_IDX] = intensity;
                        break;
                    case Two:
                        depthFrame32[i32 + GREEN_IDX] = intensity;
                        break;
                    case Three:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / Four);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case Four:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / Four);
                        break;
                    case Five:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / Four);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case Six:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / Two);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / Two);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case Seven:
                        depthFrame32[i32 + RED_IDX] = (byte)(TwoFiftyFive - intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(TwoFiftyFive - intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(TwoFiftyFive - intensity);
                        break;
                }
            }
            return depthFrame32;
        }

        public static Point GetRightHandDisplayPosition(double totalWidth, double totalHeight)
        {
            return GetDisplayPosition(totalWidth, totalHeight, rightHandJoint);
        }

        public static Point GetLeftHandDisplayPosition(double totalWidth, double totalHeight)
        {
            return GetDisplayPosition(totalWidth, totalHeight, leftHandJoint);
        }

        // constants for GetDisplayPosition
        const int ThreeTwenty = 320;
        const int TwoForty = 240;
        const int SixForty = 640;
        const int FourEighty = 480;

        static Point GetDisplayPosition(double totalWidth, double totalHeight, Joint joint)
        {
            float depthX, depthY;
            runtime.SkeletonEngine.SkeletonToDepthImage(joint.Position,
                out depthX, out depthY);
            depthX = Math.Max(Zero, Math.Min(depthX * ThreeTwenty, ThreeTwenty));
            depthY = Math.Max(Zero, Math.Min(depthY * TwoForty, TwoForty));

            int colorX, colorY;
            ImageViewArea iv = new ImageViewArea();
            // only ImageResolution.Resolution640x480 is supported at this point
            runtime.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(
                ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY,
                (short)0, out colorX, out colorY);

            return new Point((int)(totalWidth * colorX / SixForty),
                (int)(totalHeight * colorY / FourEighty));
        }


        public static void Cleanup()
        {
            if (runtime != null)
            {
                runtime.Uninitialize();
            }
        }

    }

}

