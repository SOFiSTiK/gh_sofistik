using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Expressions;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace gh_sofistik
{
   namespace Section
   {
      public class Variable
      {
         public string Name = string.Empty;
         public double Value = 0.0;
      }

      public class Point
      {
         public string Id = string.Empty;
         public string X = "0.0";
         public string Y = "0.0";
         public string Refp = string.Empty;
         public string Refd = string.Empty;
         public string Refs = string.Empty;
      }

      public class Polygon
      {
         public string Type = "O";
         public List<Point> Points = new List<Point>();
      }

      public class PointEvaluator
      {
         private GH_ExpressionParser _parser = null;
         private Dictionary<string, Point3d> _geo_points = null;

         public PointEvaluator()
         {
            _parser = new GH_ExpressionParser();
            _geo_points = new Dictionary<string, Point3d>();
         }

         public void AddVariable( string name, double value )
         {
            _parser.AddVariable(name, value);
         }

         //public void SetVariables( List<Variable> variables )
         //{
         //   foreach( var v in variables )
         //   {
         //      _parser.AddVariable(v.Name, v.Value);
         //   }
         //}

         public List<Point3d> EvaluatePoints( List<Point> qpoints )
         {
            var gpoints = new List<Point3d>();

            foreach (var pi in qpoints)
            {
               var v = new Vector3d(0, 0, 0);

               // evaluate formula
               var result = _parser.Evaluate(pi.X);
               if (result?.IsNumeric == true)
                  v.X = result.Data<double>();

               result = _parser.Evaluate(pi.Y);
               if (result?.IsNumeric == true)
                  v.Y = result.Data<double>();

               Point3d p0 = Point3d.Origin;
               Point3d p1 = Point3d.Origin;
               Point3d p2 = Point3d.Origin;
               Point3d p3 = Point3d.Origin;

               int kind = 0;

               if (!string.IsNullOrEmpty(pi.Refp))
               {
                  kind += 1000;
                  p1 = _geo_points[pi.Refp];
               }

               if (!string.IsNullOrEmpty(pi.Refd)) // polar reference
               {
                  if (pi.Refd[0] == '~')
                  {
                     kind += 100;
                     pi.Refd = pi.Refd.Substring(1);
                  }
                  else if (pi.Refd[0] == '+')
                  {
                     kind += 200;
                     pi.Refd = pi.Refd.Substring(1);
                  }
                  else if (pi.Refd[0] == '*')
                  {
                     kind += 300;
                     pi.Refd = pi.Refd.Substring(1);
                  }
                  else
                  {
                     kind += 400;
                  }

                  p2 = _geo_points[pi.Refd];
               }

               // check for references
               if (!string.IsNullOrEmpty(pi.Refs)) // constr. reference
               {
                  if (pi.Refs[0] == '>')
                  {
                     kind += 10;
                     pi.Refs = pi.Refs.Substring(1);
                  }
                  else if (pi.Refs[0] == '^')
                  {
                     kind += 20;
                     pi.Refs = pi.Refs.Substring(1);
                  }
                  else
                  {
                     kind += 30;
                  }

                  p3 = _geo_points[pi.Refs];
               }

               // evaluate references
               if (kind == 0) // default
               {
                  p0 = Point3d.Origin + v;
               }
               else if (kind == 1000) // only refd
               {
                  p0 = p1 + v;
               }
               else if (kind == 1400) // y z 
               {
                  p0.X = p1.X + v.X;
                  p0.Y = p2.Y + v.Y;
               }
               else if (kind / 10 == 0) // polar: absolute
               {
                  Vector3d va = p2 - p1;
                  double len = va.Length;
                  if (len > 1.0E-6)
                  {
                     double xix = kind == 1410 ? 1.0 / len : 1.0;
                     double xiy = kind != 1430 ? 1.0 / len : 1.0;

                     p0 = p1 + va * v.X * xix + new Vector3d(-va.Y, va.X, 0) * v.Y * xiy;
                  }
                  else
                     p0 = p1;

               }
               else if (kind == 1410) // constructional in x
               {
                  double dx = p2.X - p1.X;
                  if (Math.Abs(dx) > 1.0E-6)
                  {
                     double xi = (p3.X - p1.X) / dx;
                     p0 = (1.0 - xi) * p1 + xi * p2;
                  }
                  else
                     p0 = p1;
               }
               else if (kind == 1420) // constructional in y
               {
                  double dy = p2.Y - p1.Y;
                  if (Math.Abs(dy) > 1.0E-6)
                  {
                     double xi = (p3.Y - p1.Y) / dy;
                     p0 = (1.0 - xi) * p1 + xi * p2;
                  }
                  else
                     p0 = p1;
               }
               else if (kind == 1430) // perpendicular point
               {
                  var ln = new Line(p1, p2);
                  p0 = ln.ClosestPoint(p3, false);
               }
               else // fallback
               {
                  p0 = Point3d.Origin + v;
               }

               if(!string.IsNullOrEmpty(pi.Id)) // add to internal dictionary
                  _geo_points.Add(pi.Id, p0);

               gpoints.Add(p0); // add to resulting list
            }
            return gpoints;
         }
      }

   }

   public class EvaluateFunction : GH_Component
   {
      public EvaluateFunction()
         : base("Evaluate", "Evaluate", "Evaluates a function", "SOFiSTiK", "Experimental")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.sofistik_24; }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("BB963892-89AE-4FFB-883B-AB37EAB686E9"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddTextParameter("Formula", "F", "Formula to be evaluated", GH_ParamAccess.item);
         pManager.AddNumberParameter("Station", "S", "Station to be evaluated", GH_ParamAccess.item, 0.0);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddNumberParameter("Result", "Res", "Result of evaluation", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var formula = DA.GetData<string>(0);
         var s = DA.GetData<double>(1);

         if (string.IsNullOrEmpty(formula))
            return;

         var parser = new GH_ExpressionParser();

         parser.AddVariable("S", s);
         parser.AddVariable("Height", 1.0);

         var result = parser.Evaluate(formula);

         if(result != null && result.IsNumeric)
         {
            var t = result.Type;

            double res = result.Data<double>();

            DA.SetData(0, res);
         }
      }
   }

   public class CrossSectionGenerator : GH_Component
   {
      public CrossSectionGenerator()
         : base("Section", "Section", "Defines a SOFiSTiK Cross Section", "SOFiSTiK", "Experimental")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.sofistik_24; }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("183C5DD3-9287-4CEA-9ECF-769A085BFA1D"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddTextParameter("Variables", "Var", "Json Definition of Variables", GH_ParamAccess.list, string.Empty);
         pManager.AddTextParameter("Points", "Qsp", "Json Definition of Sectional Points", GH_ParamAccess.list, string.Empty);
         pManager.AddTextParameter("Polygons", "Poly", "Json Definition of Sectional Polygons", GH_ParamAccess.list);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         //pManager.AddCurveParameter("Crv", "Crv", "Geometry of Section", GH_ParamAccess.list);
         pManager.AddPointParameter("Pt", "Pt", "Geometry of Section", GH_ParamAccess.list);
         pManager.AddCurveParameter("Poly", "Crv", "Geometry of Polygon", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var jserializer = new System.Web.Script.Serialization.JavaScriptSerializer();

         var point_evaluator = new Section.PointEvaluator();

         var resulting_points = new List<Point3d>();
         var resulting_curves = new List<Curve>();

         // read variables
         foreach ( var json in da.GetDataList<string>(0))
         {
            var v = jserializer.Deserialize<Section.Variable>(json);
            if (v != null)
               point_evaluator.AddVariable(v.Name, v.Value);
         }

         // read sectional points
         var qpoints = new List<Section.Point>();
         foreach ( var json in da.GetDataList<string>(1))
         {
            var p = jserializer.Deserialize<Section.Point>(json);
            if (p != null)
               qpoints.Add(p);
         }

         // evaluate sectional points
         var gpoints = point_evaluator.EvaluatePoints(qpoints);
         if (gpoints.Count > 0)
            resulting_points.AddRange(gpoints);


         // read polygons and evaluate points
         foreach( var json in da.GetDataList<string>(2))
         {
            var poly = jserializer.Deserialize<Section.Polygon>(json);
            if(poly != null)
            {
               gpoints = point_evaluator.EvaluatePoints(poly.Points);
               if(gpoints.Count>1)
               {
                  if (gpoints.First().DistanceTo(gpoints.Last()) > 1.0E-4) // close polygon
                     gpoints.Add(gpoints.First());

                  resulting_curves.Add( new PolylineCurve(gpoints) );
               }
            }
         }
         da.SetDataList(0, resulting_points);
         da.SetDataList(1, resulting_curves);
      }
   }
}
