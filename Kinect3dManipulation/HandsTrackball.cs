/// This implementation of a WPF 3D trackball is adapted from the Trackball
/// included in the WPF 3DTools source code. It has been adapted to
/// include support for the Microsoft Kinect SDK. The original comment
/// header from the original Trackball class is below.
///
/// - Mike Hodnick 7/5/2011
///

//---------------------------------------------------------------------------
//
// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Limited Permissive License.
// See http://www.microsoft.com/resources/sharedsource/licensingbasics/limitedpermissivelicense.mspx
// All other rights reserved.
//
// This file is part of the 3D Tools for Windows Presentation Foundation
// project.  For more information, see:
// 
// http://CodePlex.com/Wiki/View.aspx?ProjectName=3DTools
//
// The following article discusses the mechanics behind this
// trackball implementation: http://viewport3d.com/trackball.htm
//
// Reading the article is not required to use this sample code,
// but skimming it might be useful.
//
//---------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Kinect3dManipulation
{
    public class HandsTrackball
    {
        const double MinTorsoDistance = 0.4d;

        Point previousPosition2D;
        Point previousZoomPosition2D;
        Vector3D previousPosition3D = new Vector3D(0, 0, 1);
        Microsoft.Research.Kinect.Nui.Vector leftHand;
        Microsoft.Research.Kinect.Nui.Vector rightHand;
        Microsoft.Research.Kinect.Nui.Vector torso;
        Point leftHandPoint;
        Point rightHandPoint;

        bool movingWithRight;
        bool movingWithLeft;
        bool rightHandCanMove;
        bool leftHandCanMove;
        bool canZoom;
        bool zooming;
        
        FrameworkElement eventSource;
        Transform3DGroup transform;
        ScaleTransform3D scale = new ScaleTransform3D();
        AxisAngleRotation3D rotation = new AxisAngleRotation3D();

        DispatcherTimer timer;

        public HandsTrackball()
        {
            this.timer = new DispatcherTimer();
            this.timer.Interval = TimeSpan.FromMilliseconds(10);
            this.timer.Tick += new EventHandler(TimerTick);
            this.timer.Start();

            transform = new Transform3DGroup();
            transform.Children.Add(scale);
            transform.Children.Add(new RotateTransform3D(rotation));
        }

        public double Width { get; set; }
        public double Height { get; set; }

        void RefreshSensorPositions()
        {
            this.leftHand = Kinect.LeftHand;
            this.rightHand = Kinect.RightHand;
            this.rightHandPoint = Kinect.GetRightHandDisplayPosition(Width, Height);
            this.leftHandPoint = Kinect.GetLeftHandDisplayPosition(Width, Height);

            this.torso = Kinect.Torso;
            this.rightHandCanMove =
                Math.Abs(this.torso.Z - this.rightHand.Z) >= MinTorsoDistance &
                Math.Abs(this.torso.Z - this.leftHand.Z) < MinTorsoDistance;

            this.leftHandCanMove =
                 Math.Abs(this.torso.Z - this.leftHand.Z) >= MinTorsoDistance &
                 Math.Abs(this.torso.Z - this.rightHand.Z) < MinTorsoDistance;

            this.canZoom =
                Math.Abs(this.torso.Z - this.rightHand.Z) >= MinTorsoDistance &
                Math.Abs(this.torso.Z - this.leftHand.Z) >= MinTorsoDistance;
        }

        void TimerTick(object sender, EventArgs e)
        {
            if (Kinect.Status == KinectStatus.TrackingUser)
            {
                this.RefreshSensorPositions();

                // moving with left but left hand can no longer move
                if (this.movingWithLeft & !this.leftHandCanMove)
                {
                    this.StopMovement();
                    return;
                }

                // moving with right but right hand can no longer move
                if (this.movingWithRight & !this.rightHandCanMove)
                {
                    this.StopMovement();
                    Debug.WriteLine("Stopping with right.");
                    return;
                }

                if (!this.movingWithLeft & !this.movingWithRight & this.leftHandCanMove)
                {
                    this.movingWithLeft = true;
                    this.StartMovement(this.leftHandPoint);
                    return;
                }

                if (!this.movingWithLeft & !this.movingWithRight & this.rightHandCanMove)
                {
                    this.movingWithRight = true;
                    this.StartMovement(this.rightHandPoint);
                    Debug.WriteLine("Starting with right.");
                    return;
                }

                if (this.movingWithLeft & this.leftHandCanMove)
                {
                    this.Move(this.leftHandPoint);
                    return;
                }

                if (this.movingWithRight & this.rightHandCanMove)
                {
                    this.Move(this.rightHandPoint);
                    return;
                }

                if (!this.zooming & this.canZoom)
                {
                    this.zooming = true;
                    this.StartZoom(this.rightHandPoint);
                    return;
                }

                if (this.zooming & this.canZoom)
                {
                    this.Zoom(this.rightHandPoint);
                    return;
                }

                if (this.zooming & !this.canZoom)
                {
                    this.StopMovement();
                }
            }
        }     

        public double XOffset { get; set; }
        public double YOffset { get; set; }
        public Dispatcher Dispatcher { get; set; }

        public Transform3D Transform
        {
            get { return transform; }
        }

        public FrameworkElement EventSource
        {
            get { return eventSource; }

            set
            {
                if (eventSource != null)
                {
                    eventSource.MouseDown -= this.OnMouseDown;
                    eventSource.MouseUp -= this.OnMouseUp;
                    eventSource.MouseMove -= this.OnMouseMove;
                }

                eventSource = value;

                eventSource.MouseDown += this.OnMouseDown;
                eventSource.MouseUp += this.OnMouseUp;
                eventSource.MouseMove += this.OnMouseMove;
            }
        }

        void OnMouseDown(object sender, MouseEventArgs e)
        {
            Mouse.Capture(EventSource, CaptureMode.Element);
            previousPosition2D = e.GetPosition(EventSource);
            this.StartMovement(previousPosition2D);
        }

        void StopMovement()
        {
            this.movingWithLeft = false;
            this.movingWithRight = false;
            this.zooming = false;
        }

        void StartMovement(Point point)
        {
            previousPosition3D = ProjectToTrackball(
                EventSource.ActualWidth,
                EventSource.ActualHeight,
                point);
        }

        void StartZoom(Point point)
        {
            this.previousZoomPosition2D = point;
            this.previousPosition3D = ProjectToTrackball(
                EventSource.ActualWidth,
                EventSource.ActualHeight,
                point);
        }

        void Move(Point currentPosition)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Track(currentPosition);
                previousPosition2D = currentPosition;
            }));
        }

        void OnMouseUp(object sender, MouseEventArgs e)
        {
            Mouse.Capture(EventSource, CaptureMode.None);
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point currentPosition = e.GetPosition(EventSource);

            // Prefer tracking to zooming if both buttons are pressed.
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Move(currentPosition);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                ZoomMouse(currentPosition);
            }

        }

        void Track(Point currentPosition)
        {
            var currentPosition3D = ProjectToTrackball(
                EventSource.ActualWidth, EventSource.ActualHeight, currentPosition);

            if (previousPosition3D == currentPosition3D)
            {
                return;
            }

            var axis = Vector3D.CrossProduct(previousPosition3D, currentPosition3D);
            double angle = Vector3D.AngleBetween(previousPosition3D, currentPosition3D) * 1.5d;

            var delta = new Quaternion(axis, angle);

            // Get the current orientantion from the RotateTransform3D
            var q = new Quaternion(rotation.Axis, rotation.Angle);

            // Compose the delta with the previous orientation
            q *= delta;

            // Write the new orientation back to the Rotation3D
            rotation.Axis = q.Axis;
            rotation.Angle = q.Angle;

            previousPosition3D = currentPosition3D;
        }

        Vector3D ProjectToTrackball(double width, double height, Point point)
        {
            double x = point.X / (width / 2);    // Scale so bounds map to [0,0] - [2,2]
            double y = point.Y / (height / 2);

            x = x - 1;                           // Translate 0,0 to the center
            y = 1 - y;                           // Flip so +Y is up instead of down

            double z2 = 1 - x * x - y * y;       // z^2 = 1 - x^2 - y^2
            double z = z2 > 0 ? Math.Sqrt(z2) : 0;

            return new Vector3D(x, y, z);
        }

        void Zoom(Point currentPosition)
        {
            double xDelta = previousZoomPosition2D.X - currentPosition.X;
            double factor = Math.Exp(xDelta / 100);    // e^(yDelta/100) is fairly arbitrary.

            scale.ScaleX *= factor;
            scale.ScaleY *= factor;
            scale.ScaleZ *= factor;

            previousZoomPosition2D = currentPosition;
        }

        void ZoomMouse(Point currentPosition)
        {
            double yDelta = currentPosition.Y - previousPosition2D.Y;

            double factor = Math.Exp(yDelta / 100);    // e^(yDelta/100) is fairly arbitrary.

            scale.ScaleX *= factor;
            scale.ScaleY *= factor;
            scale.ScaleZ *= factor;
        }
    }
}
