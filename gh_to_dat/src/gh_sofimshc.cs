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
         pManager.AddTextParameter("Control Values", "C", "SOFiMSHC control settings", GH_ParamAccess.item,string.Empty);
         pManager.AddGeometryParameter("Geometry", "G", "Collection of geometry objects", GH_ParamAccess.tree);

      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("File", "F", "SOFiMSHC input data", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         string control_string = string.Empty;
         var geometry = new Grasshopper.Kernel.Data.GH_Structure<IGH_GeometricGoo>();

         if (!DA.GetData(0, ref control_string)) return;
         if (!DA.GetDataTree(1, out geometry)) return;

         var sout = new StringBuilder();

         sout.AppendLine("+PROG SOFIMSHC");
         sout.AppendLine("HEAD");

         // add control string
         sout.Append(control_string);
         sout.AppendLine();

         // write structural lines
         foreach( var g in geometry )
         {
            if(g is GH_StructuralPoint)
            {
               var gp = g as GH_StructuralPoint;
               Point3d p = gp.Value.Location;

               string id_string = gp.Id > 0 ? gp.Id.ToString() : "-";

               sout.AppendFormat("SPT {0} X {1:F6} {2:F6} {3:F6}",id_string, p.X, p.Y, p.Z);

               if (gp.DirectionLocalX.Length > 0.0)
                  sout.AppendFormat(" SX {0:F6} {1:F6} {2:F6}", gp.DirectionLocalX.X, gp.DirectionLocalX.Y, gp.DirectionLocalX.Z);

               if (gp.DirectionLocalZ.Length > 0.0)
                  sout.AppendFormat(" NX {0:F6} {1:F6} {2:F6}", gp.DirectionLocalZ.X, gp.DirectionLocalZ.Y, gp.DirectionLocalZ.Z);

               if (string.IsNullOrWhiteSpace(gp.FixLiteral) == false)
                  sout.AppendFormat(" FIX {0}", gp.FixLiteral);

               sout.AppendLine();
            }
            else if(g is GH_StructuralLine)
            {
               var gc = g as GH_StructuralLine;
               var c = gc.Value;

               string id_string = gc.Id > 0 ? gc.Id.ToString() : "-";

               sout.AppendFormat("SLN {0} GRP {1} SNO {2}", id_string, gc.GroupId, gc.SectionId);

               if (gc.DirectionLocalZ.Length > 0.0)
                  sout.AppendFormat(" DRX {0:F6} {1:F6} {2:F6}", gc.DirectionLocalZ.X, gc.DirectionLocalZ.Y, gc.DirectionLocalZ.Z);

               if (string.IsNullOrWhiteSpace(gc.FixLiteral) == false)
                  sout.AppendFormat(" FIX {0}", gc.FixLiteral);

               sout.AppendLine();

               if(c is LineCurve)
               {
                  var l = c as LineCurve;
                  Point3d pa = l.Line.From;
                  Point3d pe = l.Line.To;

                  sout.AppendFormat("SLNB X1 {0:F6} {1:F6} {2:F6} ", pa.X, pa.Y, pa.Z);
                  sout.AppendFormat(" X2 {0:F6} {1:F6} {2:F6} ", pe.X, pe.Y, pe.Z);
                  sout.AppendLine();
               }
               else if(c is ArcCurve)
               {
                  var a = c as ArcCurve;
                  Point3d pa = a.PointAtStart;
                  Point3d pe = a.PointAtEnd;
                  Point3d pm = a.Arc.Center;
                  Vector3d n = a.Arc.Plane.Normal;

                  sout.AppendFormat("SLNB X1 {0:F6} {1:F6} {2:F6} ", pa.X, pa.Y, pa.Z);
                  sout.AppendFormat(" X2 {0:F6} {1:F6} {2:F6} ", pe.X, pe.Y, pe.Z);
                  sout.AppendFormat(" XM {0:F6} {1:F6} {2:F6} ", pm.X, pm.Y, pm.Z);
                  sout.AppendFormat(" NX {0:F6} {1:F6} {2:F6} ", n.X, n.Y, n.Z);
                  sout.AppendLine();
               }
               else if(c is NurbsCurve)
               {
                  var n = c as NurbsCurve;
                  
                  for( int i=0; i<n.Knots.Count; ++i)
                  {
                     sout.AppendFormat("SLNN S {0:F6}", n.Knots[i]);
                     if (i == 0)
                        sout.AppendFormat(" DEGR {0}", n.Degree);
                     sout.AppendLine();
                  }

                  bool first = true;
                  foreach( var p in n.Points)
                  {
                     sout.AppendFormat("SLNP X {0:F6} {1:F6} {2:F6}", p.Location.X, p.Location.Y, p.Location.Z);
                     if (p.Weight != 1.0)
                     {
                        sout.AppendFormat(" W {0:F6}", p.Weight);
                     }
                     if (first)
                     {
                        sout.Append(" TYPE NURB");
                        first = false;
                     }
                     sout.AppendLine();
                  }
               }
               else
               {
                  throw new ArgumentException("Encountered curve type is not supported: " + g.GetType().ToString());
               }
            }
            //else if(g is GH_Surface)
            //{
            //   sout.AppendFormat("SAR ");
            //   sout.AppendLine();
            //}
            else
            {
               throw new ArgumentException("Encountered type not supported: " + g.GetType().ToString());
            }
         }
         sout.AppendLine();
         sout.AppendLine("END");

         DA.SetData(0, sout.ToString());
      }
   }
}
