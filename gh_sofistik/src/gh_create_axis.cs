using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;


namespace gh_sofistik
{
   // creates SOFiSTiK axis input from a curve definition in GH
   public class CreateGeometricAxis : GH_Component
   {
      public CreateGeometricAxis()
         : base("Geometric Axis","Geom Axis", "Creates a SOFiSTiK geometry axis definition from a curve","SOFiSTiK","Geometry")
      {}

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_axis_24x24; }
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

         // over all curves passed in
         for(int ic = 0; ic<curves.Count; ++ic)
         {
            var sb = new StringBuilder(1024);

            var c = curves[ic];

            // identifier
            string name = names.GetItemOrLast(1)?.Trim().ToUpper();
            if(string.IsNullOrWhiteSpace(name))
            {
               name = "G_" + (ic + 1).ToString();
            }
            else if(names.Count != curves.Count && ic >= names.Count - 1)
            {
               name = name + (ic + 1).ToString();
            }
            if (name.Length > 4)
               AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Generated Identifier of Curve exceeds maximum allowed length of 4 characters");

            // type
            string type = types.GetItemOrLast(ic);
            if (string.IsNullOrWhiteSpace(type))
               type = "LANE";

            // write
            if(c is LineCurve)
            {
               sb.AppendFormat("GAX {0} TYPE {1}\n", name, type);
               AppendLineDefinition(sb, c);
            }
            else if(c is ArcCurve)
            {
               sb.AppendFormat("GAX {0} TYPE {1}\n", name, type);
               AppendArcDefinition(sb, (c as ArcCurve));
            }
            else
            {
               NurbsCurve nb = (c as NurbsCurve) ?? c.ToNurbsCurve();
               if (nb == null)
                  throw new Exception("Unable to cast Curve at index " + (ic+1).ToString() + " to NurbsCurve");

               if(nb.Knots.Count==2 && nb.Degree==1)
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

            definitions.Add(sb.ToString());
            lengths.Add(c.GetLength());
         }

         da.SetDataList(0, definitions);
         da.SetDataList(1, lengths);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("E0EE9840-CF44-4550-9640-2A0E56C1C768"); }
      }

      private void AppendLineDefinition(StringBuilder sb, Curve c)
      {
         Point3d pa = c.PointAt(0);
         Point3d pe = c.PointAt(1);

         sb.AppendFormat(" GAXB X1 {0:F8} {1:F8} {2:F8} ", pa.X, pa.Y, pa.Z);
         sb.AppendFormat(" X2 {0:F8} {1:F8} {2:F8} ", pe.X, pe.Y, pe.Z);
         sb.AppendLine();
      }

      private void AppendArcDefinition(StringBuilder sb, ArcCurve ar)
      {
         Point3d pa = ar.PointAtStart;
         Point3d pe = ar.PointAtEnd;
         Point3d pm = ar.Arc.Center;
         Vector3d n = ar.Arc.Plane.Normal;

         sb.AppendFormat(" GAXB X1 {0:F8} {1:F8} {2:F8} ", pa.X, pa.Y, pa.Z);
         sb.AppendFormat(" X2 {0:F8} {1:F8} {2:F8} ", pe.X, pe.Y, pe.Z);
         sb.AppendFormat(" XM {0:F8} {1:F8} {2:F8} ", pm.X, pm.Y, pm.Z);
         sb.AppendFormat(" NX {0:F8} {1:F8} {2:F8} ", n.X, n.Y, n.Z);
         sb.AppendLine();
      }

      private void AppendNurbsDefinition(StringBuilder sb, NurbsCurve nb)
      {
         double l = nb.GetLength();
         double k0 = nb.Knots[0];
         double kn = nb.Knots[nb.Knots.Count-1];

         // re-parametrize knot vectors to length of curve
         double scale = 1.0;
         if (Math.Abs(kn - k0) > 1.0E-6)
            scale = l / (kn - k0);

         for (int i = 0; i < nb.Knots.Count; ++i)
         {
            double si = (nb.Knots[i] - k0) * scale;

            sb.AppendFormat(" GAXN S {0:F8}", si);
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