using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace NonAffineImageTransform2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool isDragging;
        int indexDragging;
        Point3D[] pointsTransformed = new Point3D[4];

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs args)
        {
            Point pt = args.GetPosition(viewport3d);

            // Obtain the Visual3D objects under the mouse pointer.
            HitTestResult result = VisualTreeHelper.HitTest(viewport3d, pt);

            // Cast result parameter to RayMeshGeometry3DHitTestResult.
            RayMeshGeometry3DHitTestResult resultMesh =
                                    result as RayMeshGeometry3DHitTestResult;

            // This should not happen, but play it safe anyway.
            if (resultMesh == null)
                return;

            // Obtain clicked ModelVisual3D.
            ModelVisual3D vis = resultMesh.VisualHit as ModelVisual3D;

            // This should not happen, but play it safe anyway.
            if (vis == null)
                return;

            // Determine which vertex the mouse is closest to.
            if (resultMesh.VertexWeight1 < resultMesh.VertexWeight2)
            {
                if (resultMesh.VertexWeight2 < resultMesh.VertexWeight3)
                    indexDragging = resultMesh.VertexIndex3;
                else
                    indexDragging = resultMesh.VertexIndex2;
            }
            else if (resultMesh.VertexWeight3 > resultMesh.VertexWeight1)
                indexDragging = resultMesh.VertexIndex3;
            else
                indexDragging = resultMesh.VertexIndex1;

            // Get current transformed points.
            for (int i = 0; i < 4; i++)
                pointsTransformed[i] = xform.Matrix.Transform(mesh.Positions[i]);

            // Obtain new transform and commence dragging operation.
            pointsTransformed[indexDragging] = Simple2Dto3D(viewport3d, pt);
            xform.Matrix = CalculateNonAffineTransform(pointsTransformed);
            isDragging = true;
            CaptureMouse();
            args.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs args)
        {
            base.OnMouseMove(args);

            if (isDragging)
            {
                Point ptMouse = args.GetPosition(viewport3d);
                pointsTransformed[indexDragging] = Simple2Dto3D(viewport3d, ptMouse);
                xform.Matrix = CalculateNonAffineTransform(pointsTransformed);
                args.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs args)
        {
            base.OnMouseUp(args);

            if (isDragging)
            {
                isDragging = false;
                ReleaseMouseCapture();
                args.Handled = true;
            }
        }

        // The input array of points describes a 2D rectangle
        //  (with Z assumed to be zero) in the order
        //  lower-left, upper-left, lower-right, upper-right.
        // The returned transform maps the points (0, 0, 0),
        //  (0, 1, 0), (1, 0, 0), and (1, 1, 0) to these points.
        Matrix3D CalculateNonAffineTransform(Point3D[] points)
        {
            // Affine transform
            // ----------------
            // This matrix maps (0, 0) --> (x0, y0)
            //                  (0, 1) --> (x1, y1)
            //                  (1, 0) --> (x2, y2)
            //                  (1, 1) --> (x2 + x1 + x0, y2 + y1 + y0)
            Matrix3D A = new Matrix3D();
            A.M11 = points[2].X - points[0].X; // scaleX = LR - LL
            A.M12 = points[2].Y - points[0].Y; // skrewY = LR - LL
            A.M21 = points[1].X - points[0].X; // skrewX = UR - LL
            A.M22 = points[1].Y - points[0].Y; // scaleY = UR - LL
            A.OffsetX = points[0].X; // LL
            A.OffsetY = points[0].Y; // LL

            // Calculate point (a, b) that get mapped by the affine transform to (x3, y3)
            double den = A.M11 * A.M22 - A.M12 * A.M21;
            double a = (A.M22 * points[3].X - A.M21 * points[3].Y + 
                        A.M21 * A.OffsetY - A.M22 * A.OffsetX) / den;

            double b = (A.M11 * points[3].Y - A.M12 * points[3].X + 
                        A.M12 * A.OffsetX - A.M11 * A.OffsetY) / den;

            // Non-affine transform
            // --------------------
            // This matrix maps (0, 0) --> (0, 0)
            //                  (0, 1) --> (0, 1)
            //                  (1, 0) --> (1, 0)
            //                  (1, 1) --> (a, b)

            Matrix3D B = new Matrix3D();
            B.M11 = a / (a + b - 1);
            B.M22 = b / (a + b - 1);
            B.M14 = B.M11 - 1;
            B.M24 = B.M22 - 1; 
            return B * A;
        }

        // The following two methods only work with OrthographicCamera,
        // with LookDirection of (0, 0, -1) and UpDirection of (0, 1, 0).
        // More advanced conversion routines can be found in the 
        // Petzold.Media3D library.

        // Converts a 2D point in device-independent coordinates relative 
        //  to Viewport3D to 3D space.
        Point3D Simple2Dto3D(Viewport3D vp, Point pt)
        {
            OrthographicCamera cam = CheckRestrictions(vp);
            double scale = cam.Width / vp.ActualWidth;
            double x = scale * (pt.X - vp.ActualWidth / 2) + cam.Position.X;
            double y = scale * (vp.ActualHeight / 2 - pt.Y) + cam.Position.Y;

            return new Point3D(x, y, 0);
        }

        // Converts a 3D point to 2D in device-independent coordinates
        //  relative to Viewport3D.
        Point Simple3Dto2D(Viewport3D vp, Point3D point)
        {
            OrthographicCamera cam = CheckRestrictions(vp);
            double scale = vp.ActualWidth / cam.Width;
            double x = vp.ActualWidth / 2 + scale * (point.X - cam.Position.X);
            double y = vp.ActualHeight / 2 - scale * (point.Y - cam.Position.Y);
            return new Point(x, y);
        }

        OrthographicCamera CheckRestrictions(Viewport3D vp)
        {
            OrthographicCamera cam = vp.Camera as OrthographicCamera;

            if (cam == null)
                throw new ArgumentException("Camera must be OrthographicCamera");

            if (cam.LookDirection != new Vector3D(0, 0, -1))
                throw new ArgumentException("Camera LookDirection must be (0, 0, -1)");

            if (cam.UpDirection != new Vector3D(0, 1, 0))
                throw new ArgumentException("Camera UpDirection must be (0, 1, 0)");

            return cam;
        }
    }
}