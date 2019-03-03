using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;

namespace gh_sofistik
{
   // extends linq queries
   static class LinqExtension
   {
      public static T GetItemOrLast<T>(this IList<T> list, int index)
      {
         if (list.Count == 0)
            return default(T);
         else if (index >= list.Count)
            return list[list.Count - 1];
         else
            return list[index];
      }

      public static int GetItemOrCountUp(this IList<int> list, int index)
      {
         if (index >= list.Count)
         {
            if (list[list.Count - 1] > 0)
               return list[list.Count - 1] + (index - list.Count + 1);
         }
         else if (list.Count > 0)
         {
            return list[index];
         }
         return 0;
      }
   }

   // extends the IGH interface
   static class IGH_Extension
   {
      // extends the GetData method
      public static T GetData<T>(this IGH_DataAccess da, int index)
      {
         var item = default(T);

         da.GetData(index, ref item);

         return item;
      }

      // extension reads data and returns type
      public static List<T> GetDataList<T>(this IGH_DataAccess da, int index)
      {
         var list = new List<T>();

         da.GetDataList(index, list);

         return list;
      }

      // extension reads data and returns type
      public static GH_Structure<T> GetDataTree<T>(this IGH_DataAccess da, int index) where T : IGH_Goo
      {
         var geometry = new GH_Structure<T>();

         da.GetDataTree(index, out geometry);

         return geometry;
      }
   }

   static class Util
   {
      public static bool CastCurveTo<Q>(Rhino.Geometry.Curve curve, out Q target)
      {
         if (curve != null)
         {
            // cast to GH_Curve (Caution: this loses all structural information)
            if (typeof(Q).IsAssignableFrom(typeof(GH_Curve)))
            {
               var gc = new GH_Curve(curve);
               target = (Q)(object)gc;
               return true;
            }
            // cast to GH_Line (Caution: this loses all structural information)
            else if (typeof(Q).IsAssignableFrom(typeof(GH_Line)))
            {
               if (curve is Rhino.Geometry.LineCurve)
               {
                  var gl = new GH_Line((curve as Rhino.Geometry.LineCurve).Line);
                  target = (Q)(object)gl;
                  return true;
               }
            }
            // cast to GH_Arc (Caution: this loses all structural information)
            else if (typeof(Q).IsAssignableFrom(typeof(GH_Arc)))
            {
               if (curve is Rhino.Geometry.ArcCurve)
               {
                  var ga = new GH_Arc((curve as Rhino.Geometry.ArcCurve).Arc);
                  target = (Q)(object)ga;
                  return true;
               }
            }
            else
            {
               throw new Exception("Unable to cast to type: " + typeof(Q).ToString());
            }
         }

         target = default(Q);
         return false;
      }

      public static bool CastBrepTo<Q>(Rhino.Geometry.Brep brep, out Q target)
      {
         if (brep != null)
         {
            if (typeof(Q).IsAssignableFrom(typeof(GH_Brep)))
            {
               var gb = new GH_Brep(brep);
               target = (Q)(object)gb;
               return true;
            }
         }

         target = default(Q);
         return false;
      }
   }

   static class DrawUtil
   {
      private static double scaleFactorLoads = 1.0;

      public static double ScaleFactorLoads
      {
         get
         {
            return scaleFactorLoads;
         }
         set
         {
            scaleFactorLoads = value < 0 ? 0 : value;
         }
      }

      public static double DensityFactorLoads { get; set; } = 1.0;

      private static double scaleFactorSupports = 1.0;

      public static double ScaleFactorSupports
      {
         get
         {
            return scaleFactorSupports;
         }
         set
         {
            scaleFactorSupports = value < 0 ? 0 : value;
         }
      }

      public static double DensityFactorSupports { get; set; } = 1.0;

      private static double scaleFactorLocalFrame = 0.0;

      public static double ScaleFactorLocalFrame
      {
         get
         {
            return scaleFactorLocalFrame;
         }
         set
         {
            scaleFactorLocalFrame = value < 0 ? 0 : value;
         }
      }

      public static double DensityFactorLocalFrame { get; set; } = 1.0;

      public static System.Drawing.Color DrawColorStructuralElements { get; set; } = System.Drawing.Color.Red;

      public static System.Drawing.Color DrawColorLoads { get; set; } = System.Drawing.Color.Red;

      public static System.Drawing.Color DrawColorSupports { get; set; } = System.Drawing.Color.Red;

      public static bool CheckSelection(System.Drawing.Color c)
      {
         bool res = false;

         if(!(Grasshopper.Instances.ActiveCanvas is null))
         {
            if(!(Grasshopper.Instances.ActiveCanvas.Document is null))
            {
               System.Drawing.Color scol = Grasshopper.Instances.ActiveCanvas.Document.PreviewColourSelected;
               if (c.R == scol.R && c.G == scol.G && c.B == scol.B)
                  res = true;
            }
         }

         return res;
      }

      public static BoundingBox GetClippingBoxLoads(BoundingBox bBox, double forceLength, double momentLength)
      {
         bBox.Inflate(Math.Max(forceLength, momentLength * 0.05 + momentLength / 2) * ScaleFactorLoads);
         return bBox;
      }

      public static BoundingBox GetClippingBoxSupports(BoundingBox bBox)
      {
         bBox.Inflate(0.25*scaleFactorSupports);
         return bBox;
      }

      public static List<Transform> GetCurveTransforms(Curve crv, bool uhl, Vector3d localDir, BrepFace bf, double scale, double density)
      {
         List<Transform> res = new List<Transform>();


         Transform tScale = Rhino.Geometry.Transform.Scale(Point3d.Origin, scale);

         int n_seg = Math.Max(1, (int)(crv.GetLength(0.01) * density / 2.0 + 0.99));

         for (int i = 0; i <= n_seg; ++i)
         {
            double s = crv.Domain.ParameterAt((double)i / (double)(n_seg)); // convert normalized parameter to true parameter

            Transform t = Transform.Identity;
            if (uhl) //transform needed
            {
               if (bf is null) t = TransformUtils.GetGlobalTransformLine(crv.TangentAt(s), localDir); //setup transform for single curve, no brepFace
               else
               {
                  Plane plane = TransformUtils.GetPlaneAtAreaPoint(crv.PointAt(s), bf);

                  Vector3d dx = localDir;
                  if (localDir.IsTiny() || !(plane.ZAxis.IsParallelTo(localDir, 0.0001) == 0))
                     dx = plane.XAxis;
                  
                  // orient towards global negative z (own weight)
                  Vector3d dz = (Vector3d.ZAxis * plane.ZAxis > 0) ? -1 * plane.ZAxis : plane.ZAxis;

                  t = TransformUtils.GetGlobalTransformPoint(dx, dz); //setup transform for edge of brepFace
               }
            }
            
            Transform tTranslate = Rhino.Geometry.Transform.Translation(new Vector3d(crv.PointAt(s)));

            res.Add(tTranslate * t * tScale);
         }

         return res;
      }

      public static string ReconstructFixString(string s)
      {
         string res = "";
         bool[,,] bits = ParseFixString(s);
         bool local = false;
         for(int i = 0; i < 2; i++)
         {
            for (int j = 0; j < 3; j++)
            {
               for (int k = 0; k < 2; k++)
               {
                  if (bits[i, j, k])
                  {
                     if (i == 0) res += "P";
                     else res += "M";

                     if (j == 0) res += "X";
                     else if (j == 1) res += "Y";
                     else res += "Z";

                     if (k == 1) local = true;
                  }
               }
            }
         }
         if (local) res = "L" + res;
         return res;
      }

      public static bool[,,] ParseFixString(string s)
      {
         bool[,,] bits = { { { false, false }, { false, false }, { false, false } }, { { false, false }, { false, false }, { false, false } } };
         String lowS = s.Trim().ToLower();
         lowS = lowS.Replace("f", "pxpypzmxmymz").Replace("pp", "pxpypz").Replace("mm", "mxmymz");
         lowS = ".." + lowS;
         char[] ca = lowS.ToCharArray();
         for (int i = 1; i < ca.Length; i++)
         {
            if (ca[i].Equals('x')) checkPrevChar(bits, ca[i - 1], ca[i - 2], 0);
            if (ca[i].Equals('y')) checkPrevChar(bits, ca[i - 1], ca[i - 2], 1);
            if (ca[i].Equals('z')) checkPrevChar(bits, ca[i - 1], ca[i - 2], 2);
         }
         return bits;
      }

      private static void checkPrevChar(bool[,,] bits, char pc, char pc2, int d)
      {
         if (pc.Equals('p'))
         {
            if (pc2.Equals('l')) bits[0, d, 1] = true;
            else bits[0, d, 0] = true;
         }

         else if (pc.Equals('m'))
         {
            if (pc2.Equals('l')) bits[1, d, 1] = true;
            else bits[1, d, 0] = true;
         }
      }

   }

   static class TransformUtils
   {
      public static Plane GetPlaneAtAreaPoint(Point3d areaPoint, BrepFace bf)
      {
         double u;
         double v;
         bf.ClosestPoint(areaPoint, out u, out v);     
         Plane oPlane = new Plane();
         bf.FrameAt(u, v, out oPlane);
         return oPlane;
      }      

      public static Transform GetGlobalTransformLine(Vector3d tangent, Vector3d refLZ)
      {

         var dx = new Vector3d(tangent);
         var dz = new Vector3d(refLZ);

         dx.Unitize();
         dz.Unitize();

         var dy = Vector3d.CrossProduct(dz, dx);
         if (dy.Length < 0.0001) // dx is tangent to dz (length=sin(alph)) --> use global Z
         {
            dy = Vector3d.CrossProduct(-1.0 * Vector3d.ZAxis, dx);
            if (dy.Length < 0.0001) // still parallel? use global X
            {
               dy = Vector3d.CrossProduct(Vector3d.XAxis, dx);
            }
         }
         dy.Unitize();

         dz = Vector3d.CrossProduct(dx, dy);
         dz.Unitize();

         return Transform.ChangeBasis(dx, dy, dz, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis);
      }

      public static Transform GetGlobalTransformPoint(Vector3d refLX, Vector3d refLZ)
      {
         // similar procedure as above
         // lz master. if both local directions are given from referencePoint, then DirectionLocalX gets adjusted to form an orthogonal right handed coordinate frame
         Vector3d dx = new Vector3d(refLX);
         Vector3d dz = new Vector3d(refLZ);

         dx.Unitize();
         dz.Unitize();

         Vector3d dy = Vector3d.CrossProduct(dz, dx);
         if (dy.Length < 0.0001)
         {
            dy = Vector3d.CrossProduct(dz, -1 * Vector3d.ZAxis);
            if (dy.Length < 0.0001)
            {
               dy = Vector3d.CrossProduct(dz, Vector3d.XAxis);
            }
         }

         dy.Unitize();
         dx = Vector3d.CrossProduct(dy, dz);
         dx.Unitize();
         return Rhino.Geometry.Transform.ChangeBasis(dx, dy, dz, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis);
      }
   }

   class LoadCondition
   {

      private class SymbolF
      {
         private Line _line;

         public Line ALine { get { return _line; } }

         public SymbolF(Transform t)
         {
            _line = new Line(new Point3d(Vector3d.ZAxis), Point3d.Origin);
            _line.Transform(t);
         }

         public SymbolF(SymbolF s)
         {
            this._line = new Line(new Point3d(s.ALine.From), new Point3d(s.ALine.To));
         }

         public void Transform(Transform t)
         {
            _line.Transform(t);
         }

         public void Draw(Rhino.Display.DisplayPipeline pipeline, System.Drawing.Color col)
         {
            pipeline.DrawArrow(_line, col, 0.0, 0.2);
         }
      }

      private class SymbolM
      {
         private Arc _arc;
         private Point3d _arrowPoint;
         private Vector3d _arrowDir;

         public Point3d APoint { get { return _arrowPoint; } }

         public SymbolM(Transform t)
         {
            _arc = new Arc(Point3d.Origin, 0.5, 2 * Math.PI * 0.75);
            _arc.Transform(t);
            _arrowPoint = _arc.PointAt(0);
            _arrowDir = _arc.TangentAt(0);
            _arrowDir.Reverse();
         }

         public SymbolM(SymbolM s)
         {
            this._arc = new Arc(new Plane(s._arc.Plane), s._arc.Radius, s._arc.Angle);
            _arrowPoint = _arc.PointAt(0);
            _arrowDir = _arc.TangentAt(0);
            _arrowDir.Reverse();
         }

         public void Transform(Transform t)
         {
            _arc.Transform(t);
            _arrowPoint.Transform(t);
            _arrowDir = _arc.TangentAt(0);
            _arrowDir.Reverse();
         }

         public void Draw(Rhino.Display.DisplayPipeline pipeline, System.Drawing.Color col)
         {
            pipeline.DrawArc(_arc, col);
            pipeline.DrawArrowHead(_arrowPoint, _arrowDir, col, 0.0, _arc.Radius * 0.2);
         }
      }

      public List<Transform> Transforms { get; set; } = new List<Transform>();

      private SymbolF _symbolF;

      private SymbolM _symbolM;

      private bool _valid = false;

      public bool isValid
      {
         get
         {
            _valid = true;
            if (Math.Abs(_scale - DrawUtil.ScaleFactorLoads) > 0.0001)
            {
               _valid = false;
            }
            if (Math.Abs(_density - DrawUtil.DensityFactorLoads) > 0.0001)
            {
               _valid = false;
            }
            return _valid;
         }
         set
         {
            _valid = value;
         }
      }
      
      private double _scale = 0;
      private double _density = 0;      

      public LoadCondition()
      {

      }

      public LoadCondition(Vector3d force, Vector3d moment)
      {
         _scale = DrawUtil.ScaleFactorLoads;
         _density = DrawUtil.DensityFactorLoads;

         Transform _orientationF = TransformUtils.GetGlobalTransformPoint(Vector3d.XAxis, new Vector3d(force) * -1);
         Transform _orientationM = TransformUtils.GetGlobalTransformPoint(Vector3d.XAxis, new Vector3d(moment) * -1);
         Transform _lengthF = Rhino.Geometry.Transform.Scale(Point3d.Origin, force.Length);
         Transform _lengthM = Rhino.Geometry.Transform.Scale(Point3d.Origin, moment.Length);

         Transform tF = _orientationF * _lengthF;
         Transform tM = _orientationM * _lengthM;

         if (!force.IsTiny()) _symbolF = new SymbolF(tF);
         if (!moment.IsTiny()) _symbolM = new SymbolM(tM);
      }

      public void Draw(Rhino.Display.DisplayPipeline pipeline, System.Drawing.Color col)
      {
         List<Point3d> _endPoints_F = new List<Point3d>();
         List<Point3d> _endPoints_M = new List<Point3d>();

         foreach (Transform t in Transforms)
         {
            if (!(_symbolF is null))
            {
               SymbolF _temp = new SymbolF(_symbolF);
               _temp.Transform(t);
               _temp.Draw(pipeline, col);               
               _endPoints_F.Add(_temp.ALine.From);
            }
            if (!(_symbolM is null))
            {
               SymbolM _temp = new SymbolM(_symbolM);
               _temp.Transform(t);
               _temp.Draw(pipeline, col);
               _endPoints_M.Add(_temp.APoint);
            }
            
         }

         pipeline.DrawDottedPolyline(_endPoints_F, col, false);
         pipeline.DrawDottedPolyline(_endPoints_M, col, false);
      }

      public void Draw2(Rhino.Display.DisplayPipeline pipeline, System.Drawing.Color col)
      {
         List<Point3d> _endPoints_F = new List<Point3d>();
         List<Point3d> _endPoints_M = new List<Point3d>();

         foreach (Transform t in Transforms)
         {

            pipeline.PushModelTransform(t);

            if (!(_symbolF is null))
            {
               _symbolF.Draw(pipeline, col);
               Point3d p = new Point3d(_symbolF.ALine.From);
               p.Transform(t);
               _endPoints_F.Add(p);
            }
            if (!(_symbolM is null))
            {
               _symbolM.Draw(pipeline, col);
               Point3d p = new Point3d(_symbolM.APoint);
               p.Transform(t);
               _endPoints_M.Add(p);
            }

            pipeline.PopModelTransform();

         }
        
         pipeline.DrawDottedPolyline(_endPoints_F, DrawUtil.DrawColorLoads, false);
         pipeline.DrawDottedPolyline(_endPoints_M, DrawUtil.DrawColorLoads, false);
      }
   }

   class SupportCondition
   {
      private class Symbol
      {
         private Mesh _fixationSymbol = new Mesh();
         private Mesh _cube = new Mesh();
         private Brep _momentSymbol = new Brep();
         private List<Line> _freePositionLines = new List<Line>();
         private List<Line> _momentLines = new List<Line>();

         private Rhino.Display.DisplayMaterial _material = new Rhino.Display.DisplayMaterial();

         public void Draw(Rhino.Display.DisplayPipeline pipeline, System.Drawing.Color col, bool shaded)
         {
            if (_fixationSymbol.Vertices.Count > 0)
            {               
               if (shaded)
                  pipeline.DrawMeshShaded(_fixationSymbol, _material);
               else
                  pipeline.DrawMeshWires(_fixationSymbol, col);
            }

            if (_cube.Vertices.Count > 0)
            {
               if (shaded)
                  pipeline.DrawMeshShaded(_cube, _material);
               else
                  pipeline.DrawMeshWires(_cube, col);
            }

            if (_momentSymbol.IsValid)
            {
               if (shaded)
                  pipeline.DrawBrepShaded(_momentSymbol, _material);
               else
                  pipeline.DrawBrepWires(_momentSymbol, col, -1);
            }

            if (_freePositionLines.Count > 0)
            {
               foreach (Line l in _freePositionLines)
               {                  
                  pipeline.DrawLine(l, col);
               }
            }

            if (_momentLines.Count > 0)
            {
               foreach (Line l in _momentLines)
               {
                  pipeline.DrawLine(l, col);
               }
            }            
         }

         public void CreateMaterial()
         {
            _material = new Rhino.Display.DisplayMaterial();
            _material.Diffuse = DrawUtil.DrawColorSupports;
            _material.Specular = DrawUtil.DrawColorSupports;
            _material.Emission = DrawUtil.DrawColorSupports;
         }

         public void CreatePyramid(Transform t)
         {
            _fixationSymbol = getPyramid(t);
         }

         public void CreateCube(Transform t, bool big)
         {
            _cube = getCube(t, big);
         }

         public void CreateCylinder(Transform t, bool upwards)
         {
            _momentSymbol = getCylinder(t, upwards);
         }

         public void CreateFixationLines(Transform t, int amount)
         {
            _freePositionLines = getFreeDegreeLine(t, amount);
         }

         public void CreateFork(Transform t)
         {
            _momentLines = getFork(t);
         }
         
         private static Mesh getPyramid(Transform t)
         {
            double preScale = 0.2;
            Point3d[] pyramidPoints = new Point3d[4];
            pyramidPoints[0] = new Point3d(-0.5, -0.5, -1);
            pyramidPoints[1] = new Point3d(-0.5, 0.5, -1);
            pyramidPoints[2] = new Point3d(0.5, 0.5, -1);
            pyramidPoints[3] = new Point3d(0.5, -0.5, -1);

            Mesh mesh = new Mesh();
            mesh.Vertices.Add(0,0,0);
            for (int i = 0; i < 4; i++)
               mesh.Vertices.Add(pyramidPoints[i].X, pyramidPoints[i].Y, pyramidPoints[i].Z);
            mesh.Faces.AddFace(1, 4, 0);
            mesh.Faces.AddFace(2, 1, 0);
            mesh.Faces.AddFace(3, 2, 0);
            mesh.Faces.AddFace(4, 3, 0);
            mesh.Faces.AddFace(1, 2, 3, 4);
            mesh.Normals.ComputeNormals();
            mesh.Compact();

            mesh.Scale(preScale);
            mesh.Transform(t);
            return mesh;
         }

         private static Mesh getCube(Transform t, bool big)
         {
            double preScale = 0.1;
            Point3d[] cubePoints = new Point3d[8];
            cubePoints[0] = new Point3d(-1, -1, -1);
            cubePoints[1] = new Point3d(1, -1, -1);
            cubePoints[2] = new Point3d(1, 1, -1);
            cubePoints[3] = new Point3d(-1, 1, -1);
            cubePoints[4] = new Point3d(-1, -1, 1);
            cubePoints[5] = new Point3d(1, -1, 1);
            cubePoints[6] = new Point3d(1, 1, 1);
            cubePoints[7] = new Point3d(-1, 1, 1);

            Mesh mesh = new Mesh();
            for (int i = 0; i < 8; i++)
               mesh.Vertices.Add(cubePoints[i].X, cubePoints[i].Y, cubePoints[i].Z);
            mesh.Faces.AddFace(0, 1, 5, 4);
            mesh.Faces.AddFace(3, 0, 4, 7);
            mesh.Faces.AddFace(1, 2, 6, 5);
            mesh.Faces.AddFace(4, 5, 6, 7);
            mesh.Faces.AddFace(0, 3, 2, 1);
            mesh.Faces.AddFace(7, 6, 2, 3);
            mesh.Normals.ComputeNormals();
            mesh.Compact();

            if (big)
               mesh.Translate(-1 * Vector3d.ZAxis);
            else
               mesh.Scale(0.5);

            mesh.Scale(preScale);
            mesh.Transform(t);

            return mesh;
         }

         private static Brep getCylinder(Transform t, bool upwards)
         {
            double preScale = 0.1;
            Circle ci = new Circle(new Plane(new Point3d(-1 * preScale, 0, 0), Vector3d.XAxis), 0.318 * preScale);            
            Cylinder c = new Cylinder(ci, 2 * preScale);
            Brep res= c.ToBrep(true, true);
            if (upwards)
            {
               Transform ts = Rhino.Geometry.Transform.RotationZYX(0, Math.PI / 2, 0);
               res.Transform(ts);
            }

            res.Transform(t);

            return res;
         }

         private static List<Line> getFreeDegreeLine(Transform t, int amount)
         {
            double preScale = 0.2;
            Point3d a = new Point3d(0, 0, -1.2);
            List<Line> lines = new List<Line>();

            for (int j = 0; j < amount; j++)
            {
               Vector3d dirFree = Vector3d.Zero;
               if (j == 0)
                  dirFree = new Vector3d(1, 0, 0);
               else
                  dirFree = new Vector3d(0, 1, 0);
               Vector3d cdir = Vector3d.CrossProduct(Vector3d.ZAxis, dirFree);
               cdir.Unitize();

               Point3d b1 = a + cdir * 0.5;
               Point3d b2 = a - cdir * 0.5;

               Point3d[] pl = new Point3d[4];
               pl[0] = b1 + dirFree * 0.4;
               pl[1] = b1 - dirFree * 0.4;
               pl[2] = b2 + dirFree * 0.4;
               pl[3] = b2 - dirFree * 0.4;

               for (int i = 0; i < 4; i++)
               {
                  pl[i] *= preScale;
                  pl[i].Transform(t);
               }

               lines.Add(new Line(pl[0], pl[1]));
               lines.Add(new Line(pl[2], pl[3]));
            }
            return lines;
         }

         private static List<Line> getFork(Transform t)
         {
            double preScale = 0.1;
            Point3d[] pts = new Point3d[4];
            pts[0] = new Point3d(-0.5, -1, 0);
            pts[1] = new Point3d(-0.5, 1, 0);
            pts[2] = new Point3d(0.5, 1, 0);
            pts[3] = new Point3d(0.5, -1, 0);
            for (int i = 0; i < 4; i++)
            {
               pts[i] *= preScale;
               pts[i].Transform(t);
            }

            List<Line> lines = new List<Line>();
            lines.Add(new Line(pts[0], pts[1]));
            lines.Add(new Line(pts[2], pts[3]));
            return lines;
         }

      }

      public List<Transform> Transforms { get; set; } = new List<Transform>();

      private bool _valid = false;

      public bool isValid
      {
         get
         {
            if (!_col.Equals(DrawUtil.DrawColorSupports)) UpdateMaterial();
            _valid = true;
            if (Math.Abs(_scale - DrawUtil.ScaleFactorSupports) > 0.0001)
            {
               _scale = DrawUtil.ScaleFactorSupports;
               _valid = false;
            }
            if (Math.Abs(_density - DrawUtil.DensityFactorSupports) > 0.0001)
            {
               _density = DrawUtil.DensityFactorSupports;
               _valid = false;
            }
            return _valid;
         }
         set
         {
            _valid = value;
         }
      }

      public bool LocalFrame { get; set; } = false;

      private Symbol _symbol;

      private double _scale = 0;
      private double _density = 0;
      private System.Drawing.Color _col = new System.Drawing.Color();

      public SupportCondition(string fixLiteral)
      {
         CreateSymbol(fixLiteral);
      }
      
      public void CreateSymbol(string fixLiteral)
      {
         _symbol = new Symbol();
         bool[,,] fixBits = DrawUtil.ParseFixString(fixLiteral);
         int countP = countDirections(fixBits, 0);
         int countM = countDirections(fixBits, 1);
         Transform tp = getSupportOrientation(fixBits, 0);
         Transform tm = getSupportOrientation(fixBits, 1);

         if (countM == 3 && countP > 0)
         {  
            if (countP == 3)
               _symbol.CreateCube(tp, true);
            else
            {
               _symbol.CreatePyramid(tp);
               _symbol.CreateFixationLines(tp, 3 - countP);
               _symbol.CreateCube(tp, false);
            }
         }
         else
         {
            if (countP > 0)
            {
               _symbol.CreatePyramid(tp);
               _symbol.CreateFixationLines(tp, 3 - countP);               
            }
            if (countM > 0)
            {
               if (countM == 1)
                  _symbol.CreateFork(tm);
               if (countM == 2)
                  _symbol.CreateCylinder(tm, false);
               if (countM == 3)
               {
                  _symbol.CreateFork(tm);
                  _symbol.CreateCylinder(tm, true);
               }
            }
         }
      }

      private int countDirections(bool[,,] bits, int posMoment)
      {
         int count = 0;
         for (int i = 0; i < 3; i++)
         {
            if (bits[posMoment, i, 0] || bits[posMoment, i, 1])
               count++;
            if (bits[posMoment, i, 1])
               LocalFrame = true;
         }
         return count;
      }

      private static Transform getSupportOrientation(bool[,,] bits, int posMoment)
      {
         Transform tDraw = Rhino.Geometry.Transform.Identity;
         
         if (!(bits[posMoment, 2, 0] || bits[posMoment, 2, 1]))
         {
            tDraw = Rhino.Geometry.Transform.ChangeBasis(Vector3d.ZAxis, Vector3d.XAxis, Vector3d.YAxis, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis);
            if (!(bits[posMoment, 1, 0] || bits[posMoment, 1, 1]))
            {
               tDraw = Rhino.Geometry.Transform.ChangeBasis(Vector3d.YAxis, Vector3d.ZAxis, Vector3d.XAxis, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis);
            }            
         }
         else
         {
            if (!(bits[posMoment, 1, 0] || bits[posMoment, 1, 1]))
            {
               tDraw = Rhino.Geometry.Transform.RotationZYX(Math.PI/2,0,0);
            }
         }
         
         return tDraw;
      }

      private void UpdateMaterial()
      {
         _col = DrawUtil.DrawColorSupports;
         _symbol.CreateMaterial();
      }

      public void Draw(Rhino.Display.DisplayPipeline pipeline, System.Drawing.Color col, bool shaded)
      {
         foreach(Transform t in Transforms)
         {
            pipeline.PushModelTransform(t);

            _symbol.Draw(pipeline, col, shaded);

            pipeline.PopModelTransform();
         }
      }

   }

   class CouplingCondition
   {
      private List<Line> _lines;
      private double _density;
      private Point3d _mid;

      public bool isValid
      {
         get
         {
            if (Math.Abs(_density - DrawUtil.DensityFactorLoads) > 0.0001)
            {
               _density = DrawUtil.DensityFactorSupports;
               return false;
            }
            return _lines.Count != 0;
         }
      }

      public CouplingCondition()
      {
         _lines = new List<Line>();
         _density = 0;
         _mid = new Point3d();
      }

      public void CreateCouplingSymbols(List<Line> lines)
      {
         _lines = lines;

         Vector3d v_mid = new Vector3d();
         foreach (Line l in _lines)
         {
            v_mid += new Vector3d(l.From);
            v_mid += new Vector3d(l.To);
         }
         v_mid /= (2*_lines.Count);
         _mid = new Point3d(v_mid);
      }

      public void Draw(Rhino.Display.DisplayPipeline pipeline, System.Drawing.Color col)
      {
         foreach(Line l in _lines)
            pipeline.DrawPatternedLine(l, col, 0x00110101, 3);         
      }

      public void DrawInfo(Rhino.Display.DisplayPipeline pipeline, String s)
      {
         pipeline.Draw2dText(s, System.Drawing.Color.Black, _mid, true);
      }
   }

   class SpringCondition
   {
      private class Symbol_spring
      {
         Curve crv;
         int thickness;
         
         public Symbol_spring(Line line, double axial_stiffness, double rotational_stiffness)
         {
            Vector3d lz = line.To - line.From;
            Transform t = TransformUtils.GetGlobalTransformPoint(Vector3d.XAxis, lz);

            int pointsPerPeriod = 4;
            double prescale = 0.2;

            double axial_stiffnessAdjusted = axial_stiffness;
            double rotational_stiffnessAdjusted = rotational_stiffness;
            if (axial_stiffness < 0.1) axial_stiffnessAdjusted = 0.1;
            if (rotational_stiffness < 0.1) rotational_stiffnessAdjusted = 0.1;

            int periods = (int)((1 / Math.Log(1 + axial_stiffnessAdjusted)) * line.Length * DrawUtil.DensityFactorSupports);
            
            List<Point3d> points = new List<Point3d>();
            points.Add(Point3d.Origin);
            for (int j = 0; j < periods; j++)
               for (int k = 0; k < pointsPerPeriod; k++)
                  points.Add(new Point3d(DrawUtil.ScaleFactorSupports * prescale * Math.Cos(k * 2 * Math.PI/pointsPerPeriod)/line.Length, DrawUtil.ScaleFactorSupports * prescale * Math.Sin(k * 2 * Math.PI / pointsPerPeriod)/line.Length, (((double)(j)+((double)(k)/pointsPerPeriod))/periods)));
            points.Add(new Point3d(0,0,1));

            crv = Curve.CreateInterpolatedCurve(points,3);
            crv.Scale(line.Length);
            crv.Transform(t);
            crv.Translate(new Vector3d(line.From));

            thickness = (int) Math.Ceiling(Math.Log(1+ rotational_stiffnessAdjusted * DrawUtil.ScaleFactorSupports));
         }

         public void Draw(Rhino.Display.DisplayPipeline pipeline)
         {
            pipeline.DrawCurve(crv, DrawUtil.DrawColorSupports, thickness);
         }
      }

      private List<Symbol_spring> _symbols;
      private double _density;
      private double _scale;

      public bool isValid
      {
         get
         {
            if (Math.Abs(_density - DrawUtil.DensityFactorSupports) > 0.0001)
            {
               _density = DrawUtil.DensityFactorSupports;
               return false;
            }
            if (Math.Abs(_scale - DrawUtil.ScaleFactorSupports) > 0.0001)
            {
               _scale = DrawUtil.ScaleFactorSupports;
               return false;
            }
            return _symbols.Count != 0;
         }
      }

      public SpringCondition()
      {
         _symbols = new List<Symbol_spring>();
         _density = 0;
         _scale = 0;
      }      

      public void createSpringSymbols(List<Line> ll, double axial_stiffness, double rotational_stiffness)
      {
         foreach(Line l in ll)
         {
            Symbol_spring s = new Symbol_spring(l, axial_stiffness, rotational_stiffness);
            _symbols.Add(s);
         }
      }

      public void Draw(Rhino.Display.DisplayPipeline pipeline)
      {
         foreach (Symbol_spring s in _symbols)
            s.Draw(pipeline);
      }
   }

   class LocalFrameVisualisation
   {
      public List<Transform> Transforms { get; set; }

      private bool _valid = false;
      private double _scale = 0.0;
      private double _density = 1.0;

      public bool isValid
      {
         get
         {            
            _valid = true;
            if (Math.Abs(_scale - DrawUtil.ScaleFactorLocalFrame) > 0.0001)
            {
               _scale = DrawUtil.ScaleFactorLocalFrame;
               _valid = false;
            }
            if (Math.Abs(_density - DrawUtil.DensityFactorLocalFrame) > 0.0001)
            {
               _density = DrawUtil.DensityFactorLocalFrame;
               _valid = false;
            }
            return _valid;
         }
         set
         {
            _valid = value;
         }
      }

      public LocalFrameVisualisation()
      {
         Transforms = new List<Transform>();
      }

      public void Draw(Rhino.Display.DisplayPipeline pipeline)
      {
         foreach(Transform t in Transforms)
         {
            Line xLine = new Line(Point3d.Origin, new Point3d(1, 0, 0));
            Line yLine = new Line(Point3d.Origin, new Point3d(0, 1, 0));
            Line zLine = new Line(Point3d.Origin, new Point3d(0, 0, 1));

            xLine.Transform(t);
            yLine.Transform(t);
            zLine.Transform(t);

            pipeline.DrawArrow(xLine, System.Drawing.Color.Red, 0.0, 0.2);
            pipeline.DrawArrow(yLine, System.Drawing.Color.Green, 0.0, 0.2);
            pipeline.DrawArrow(zLine, System.Drawing.Color.Blue, 0.0, 0.2);
         }
      }
   }
}
