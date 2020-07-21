using System;
using System.Collections.Generic;
using System.Text;

using Grasshopper.Kernel;
using Rhino.Geometry;


namespace gh_sofistik.Geometry
{
   // creates SOFiSTiK axis input from a curve definition in GH
   public class CreateGeometricAxis : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public CreateGeometricAxis()
         : base("Geometric Axis","Geom Axis", "Creates a SOFiSTiK geometry axis definition from a curve","SOFiSTiK", "General")
      {}

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "structural_axis_24x24.png");
            return _icon;
         }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddCurveParameter("Curve", "Crv", "Curve Geometry", GH_ParamAccess.list);
         pManager.AddTextParameter("Id", "Id", "Identifier of axis (4 char)", GH_ParamAccess.list, string.Empty);
         pManager.AddTextParameter("Type", "Type", "Type of SOFiSTiK Axis (acc. SOFiMSHC manual)", GH_ParamAccess.list, "LANE");
         // pManager.AddBooleanParameter("Scale Param", "ScaleP", "Scale Parametrization to Curve Length", GH_ParamAccess.item, true);

      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("Text input", "O", "SOFiSTiK text input", GH_ParamAccess.list);
         pManager.AddNumberParameter("Length", "L", "Lengths of curves", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var curves = da.GetDataList<Curve>(0);
         var names = da.GetDataList<string>(1);
         var types = da.GetDataList<string>(2);

         var definitions = new List<string>();
         var lengths = new List<double>();

         var tU = Units.UnitHelper.GetUnitTransformToMeters();
         bool scaleUnit = Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem != Rhino.UnitSystem.Meters;

         int count = Math.Max(curves.Count, names.Count);

         // over all curves passed in
         for (int i = 0; i < count; ++i)
         {
            var crv = curves.GetItemOrLast(i).DuplicateCurve();
            var name = names.GetItemOrLast(i)?.Trim().ToUpper();
            var type = types.GetItemOrLast(i);

            // scale if neccessary
            var lengthRhino = crv.GetLength();
            if (scaleUnit)
               crv.Transform(tU);

            // identifier
            if (string.IsNullOrWhiteSpace(name))
            {
               name = "G_" + (i + 1).ToString();
            }
            else if (names.Count < curves.Count && i >= names.Count - 1)
            {
               name = name + (i + 1).ToString();
            }
            if (name.Length > 4)
               AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Generated Identifier of Curve exceeds maximum allowed length of 4 characters");

            // type
            if (string.IsNullOrWhiteSpace(type))
               type = "LANE";

            definitions.Add(GetGeometryAxisDefinition(crv, name, type));
            lengths.Add(lengthRhino);
         }

         da.SetDataList(0, definitions);
         da.SetDataList(1, lengths);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("E0EE9840-CF44-4550-9640-2A0E56C1C768"); }
      }

      public static string GetGeometryAxisDefinition(Curve crv, string name, string type)
      {
         var sb = new StringBuilder(1024);
         // write
         if (crv is LineCurve)
         {
            sb.AppendFormat("GAX {0} TYPE {1}\n", name, type);
            AppendLineDefinition(sb, crv);
         }
         else if (crv is ArcCurve)
         {
            sb.AppendFormat("GAX {0} TYPE {1}\n", name, type);
            AppendArcDefinition(sb, (crv as ArcCurve));
         }
         else
         {
            NurbsCurve nb = (crv as NurbsCurve) ?? crv.ToNurbsCurve();
            if (nb == null)
               throw new Exception("Unable to cast Curve " + name + " to NurbsCurve");

            if (nb.Knots.Count == 2 && nb.Degree == 1)
            {
               sb.AppendFormat("GAX {0} TYPE {1}\n", name, type);
               AppendLineDefinition(sb, nb);
            }
            else
            {
               sb.AppendFormat("GAX {0} TYPE {1} TYPC NURB DEGR {2}\n", name, type, nb.Degree);
               AppendNurbsDefinition(sb, nb);
            }
         }
         sb.AppendLine();
         return sb.ToString();
      }

      private static void AppendLineDefinition(StringBuilder sb, Curve c)
      {
         Point3d pa = c.PointAtStart;
         Point3d pe = c.PointAtEnd;

         sb.AppendFormat(" GAXB X1 {0:F8} {1:F8} {2:F8} S1 {3:F8} ", pa.X, pa.Y, pa.Z, c.Domain.Min);
         sb.AppendFormat(" X2 {0:F8} {1:F8} {2:F8} S2 {3:F8} ", pe.X, pe.Y, pe.Z, c.Domain.Max);
         sb.AppendLine();
      }

      private static void AppendArcDefinition(StringBuilder sb, ArcCurve ar)
      {
         Point3d pa = ar.PointAtStart;
         Point3d pe = ar.PointAtEnd;
         Point3d pm = ar.Arc.Center;
         Vector3d n = ar.Arc.Plane.Normal;

         sb.AppendFormat(" GAXB X1 {0:F8} {1:F8} {2:F8} S1 {3:F8} ", pa.X, pa.Y, pa.Z, ar.Domain.Min);
         sb.AppendFormat(" X2 {0:F8} {1:F8} {2:F8} S2 {3:F8} ", pe.X, pe.Y, pe.Z, ar.Domain.Max);
         sb.AppendFormat(" XM {0:F8} {1:F8} {2:F8} ", pm.X, pm.Y, pm.Z);
         sb.AppendFormat(" NX {0:F8} {1:F8} {2:F8} ", n.X, n.Y, n.Z);
         sb.AppendLine();
      }

      private static void AppendNurbsDefinition(StringBuilder sb, NurbsCurve nb)
      {
         for (int i = 0; i < nb.Knots.Count; ++i)
         {
            sb.AppendFormat(" GAXN S {0:F8}", nb.Knots[i]);
            sb.AppendLine();
         }

         foreach (var p in nb.Points)
         {
            sb.AppendFormat(" GAXC X {0:F8} {1:F8} {2:F8}", p.Location.X, p.Location.Y, p.Location.Z);
            if (p.Weight != 1.0)
            {
               sb.AppendFormat(" W {0:F8}", p.Weight);
            }
            sb.AppendLine();
         }
      }
   }
}