using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace gh_sofistik
{
   public class CreateSofimshcInput : GH_Component
   {
      public CreateSofimshcInput()
         : base("SOFiMSHC", "SOFiMSHC", "Creates a SOFiMSHC input file","SOFiSTiK","Geometry")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.sofistik_32x32; }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("F46E8DA9-205A-4623-8331-8F911C7DA0DC"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddBooleanParameter("Create Mesh", "MESH", "Activates mesh generation", GH_ParamAccess.item, true);
         pManager.AddNumberParameter("Mesh density", "HMIN", "Allows to set the global mesh density in [m]", GH_ParamAccess.item, 1.0);
         pManager.AddTextParameter("Additional text input", "TXT", "Additional SOFiMSHC text input", GH_ParamAccess.item, string.Empty);
         pManager.AddGeometryParameter("Geometry", "G", "Collection of geometry objects", GH_ParamAccess.tree);

      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("File", "F", "SOFiMSHC input data", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         bool mesh = true;
         double hmin = 1.0;
         string control_string = string.Empty;
         var geometry = new Grasshopper.Kernel.Data.GH_Structure<IGH_GeometricGoo>();

         if (!DA.GetData(0, ref mesh)) return;
         if (!DA.GetData(1, ref hmin)) return;
         if (!DA.GetData(2, ref control_string)) return;
         if (!DA.GetDataTree(3, out geometry)) return;

         var sb = new StringBuilder();

         sb.AppendLine("+PROG SOFIMSHC");
         sb.AppendLine("HEAD");
         sb.AppendLine("PAGE UNII 0"); // export always in SOFiSTiK database units
         sb.AppendLine("CTRL 3D GDIR NEGZ GDIV -1000");
         if(mesh)
         {
            sb.AppendLine("CTRL MESH 1");
            sb.AppendFormat("CTRL HMIN {0:F3}\n", hmin);
         }

         // add control string
         if(!string.IsNullOrEmpty(control_string))
            sb.Append(control_string);
         sb.AppendLine();

         // write structural lines
         foreach( var g in geometry )
         {
            if(g is GH_StructuralPoint)
            {
               var gp = g as GH_StructuralPoint;
               Point3d p = gp.Value.Location;

               string id_string = gp.Id > 0 ? gp.Id.ToString() : "-";

               sb.AppendFormat("SPT {0} X {1:F6} {2:F6} {3:F6}",id_string, p.X, p.Y, p.Z);

               if (gp.DirectionLocalX.Length > 0.0)
                  sb.AppendFormat(" SX {0:F6} {1:F6} {2:F6}", gp.DirectionLocalX.X, gp.DirectionLocalX.Y, gp.DirectionLocalX.Z);

               if (gp.DirectionLocalZ.Length > 0.0)
                  sb.AppendFormat(" NX {0:F6} {1:F6} {2:F6}", gp.DirectionLocalZ.X, gp.DirectionLocalZ.Y, gp.DirectionLocalZ.Z);

               if (string.IsNullOrWhiteSpace(gp.FixLiteral) == false)
                  sb.AppendFormat(" FIX {0}", gp.FixLiteral);

               sb.AppendLine();
            }
            // write structural lines
            else if(g is GH_StructuralLine)
            {
               var gc = g as GH_StructuralLine;

               string id_string = gc.Id > 0 ? gc.Id.ToString() : "-";

               sb.AppendFormat("SLN {0} GRP {1} SNO {2}", id_string, gc.GroupId, gc.SectionId);

               if (gc.DirectionLocalZ.Length > 0.0)
                  sb.AppendFormat(" DRX {0:F6} {1:F6} {2:F6}", gc.DirectionLocalZ.X, gc.DirectionLocalZ.Y, gc.DirectionLocalZ.Z);

               if (string.IsNullOrWhiteSpace(gc.FixLiteral) == false)
                  sb.AppendFormat(" FIX {0}", gc.FixLiteral);

               sb.AppendLine();

               AppendCurveGeometry(sb, gc.Value);
            }
            // write structural areas
            else if (g is GH_StructuralArea)
            {
               var ga = g as GH_StructuralArea;
               var brep = ga.Value;

               string id_string = ga.Id > 0 ? ga.Id.ToString() : "-";
               string grp_string = ga.GroupId > 0 ? ga.GroupId.ToString() : "-";
               string thk_string = ga.Thickness.ToString("F6");

               foreach( var fc in brep.Faces)
               {
                  // write SAR header
                  sb.AppendLine();
                  sb.AppendFormat("SAR {0} GRP {1} T {2}", id_string, grp_string, thk_string);
                  id_string = string.Empty; // set only the 1st time

                  if (ga.MaterialId > 0)
                     sb.AppendFormat(" MNR {0}", ga.MaterialId.ToString());
                  if (ga.ReinforcementId > 0)
                     sb.AppendFormat(" MBW {0}", ga.ReinforcementId.ToString());

                  sb.AppendLine();

                  // outer boundary
                  foreach( var loop in fc.Loops)
                  {
                     string type;
                     if (loop.LoopType == BrepLoopType.Outer)
                        type = "OUT";
                     else if (loop.LoopType == BrepLoopType.Inner)
                        type = "IN";
                     else
                        continue;

                     sb.AppendFormat("SARB {0}", type);
                     sb.AppendLine();

                     foreach(var tr in loop.Trims)
                     {
                        var ed = tr.Edge;
                        if (ed != null)
                        {
                           AppendCurveGeometry(sb, ed.EdgeCurve);
                        }
                     }
                  }

                  // write geometry
                  if(fc.IsPlanar() == false)
                  {
                     AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Curved surfaces currently not supported");
                  }
               }
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Encountered type not supported: " + g.GetType().ToString());
            }
         }
         sb.AppendLine();
         sb.AppendLine("END");

         DA.SetData(0, sb.ToString());
      }

      private void AppendCurveGeometry(StringBuilder sb, Curve c)
      {
         if (c is LineCurve)
         {
            var l = c as LineCurve;
            Point3d pa = l.Line.From;
            Point3d pe = l.Line.To;

            sb.AppendFormat("SLNB X1 {0:F6} {1:F6} {2:F6} ", pa.X, pa.Y, pa.Z);
            sb.AppendFormat(" X2 {0:F6} {1:F6} {2:F6} ", pe.X, pe.Y, pe.Z);
            sb.AppendLine();
         }
         else if (c is ArcCurve)
         {
            var a = c as ArcCurve;
            Point3d pa = a.PointAtStart;
            Point3d pe = a.PointAtEnd;
            Point3d pm = a.Arc.Center;
            Vector3d n = a.Arc.Plane.Normal;

            sb.AppendFormat("SLNB X1 {0:F6} {1:F6} {2:F6} ", pa.X, pa.Y, pa.Z);
            sb.AppendFormat(" X2 {0:F6} {1:F6} {2:F6} ", pe.X, pe.Y, pe.Z);
            sb.AppendFormat(" XM {0:F6} {1:F6} {2:F6} ", pm.X, pm.Y, pm.Z);
            sb.AppendFormat(" NX {0:F6} {1:F6} {2:F6} ", n.X, n.Y, n.Z);
            sb.AppendLine();
         }
         else if (c is NurbsCurve)
         {
            var n = c as NurbsCurve;

            for (int i = 0; i < n.Knots.Count; ++i)
            {
               sb.AppendFormat("SLNN S {0:F6}", n.Knots[i]);
               if (i == 0)
                  sb.AppendFormat(" DEGR {0}", n.Degree);
               sb.AppendLine();
            }

            bool first = true;
            foreach (var p in n.Points)
            {
               sb.AppendFormat("SLNP X {0:F6} {1:F6} {2:F6}", p.Location.X, p.Location.Y, p.Location.Z);
               if (p.Weight != 1.0)
               {
                  sb.AppendFormat(" W {0:F6}", p.Weight);
               }
               if (first)
               {
                  sb.Append(" TYPE NURB");
                  first = false;
               }
               sb.AppendLine();
            }
         }
         else
         {
            throw new ArgumentException("Encountered curve type is currently not supported: " + c.GetType().ToString());
         }
      }
   }
}
