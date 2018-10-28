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
         : base("SOFiMSHC", "SOFiMSHC", "Creates a SOFiMSHC input file","SOFiSTiK", "Geometry")
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
         pManager.AddNumberParameter("Intersection tolerance", "TOLG", "Intersection tolerance", GH_ParamAccess.item, 0.01);
         pManager.AddBooleanParameter("Create mesh", "MESH", "Activates mesh generation", GH_ParamAccess.item, true);
         pManager.AddNumberParameter("Mesh density", "HMIN", "Allows to set the global mesh density in [m]", GH_ParamAccess.item, 1.0);
         pManager.AddTextParameter("Additional text input", "TXT", "Additional SOFiMSHC text input", GH_ParamAccess.item, string.Empty);
         pManager.AddGeometryParameter("Structural Elements", "G", "Collection of SOFiSTiK Structural elements", GH_ParamAccess.list);

      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("File", "F", "SOFiMSHC input data", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         double tolg = da.GetData<double>(0);
         bool mesh = da.GetData<bool>(1);
         double hmin = da.GetData<double>(2);
         string ctrl = da.GetData<string>(3);

         var structural_elements = new List<IGS_StructuralElement>();
         foreach( var it in da.GetDataList<IGH_Goo>(4))
         {
            if (it is IGS_StructuralElement)
               structural_elements.Add(it as IGS_StructuralElement);
            else
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + it.TypeName + " to IGS_StructuralElement.");
         }

         var sb = new StringBuilder();

         sb.AppendLine("+PROG SOFIMSHC");
         sb.AppendLine("HEAD");
         sb.AppendLine("PAGE UNII 0"); // export always in SOFiSTiK database units
         sb.AppendLine("SYST 3D GDIR NEGZ GDIV -1000");
         sb.AppendFormat("CTRL TOLG {0:F6}\n", tolg);
         if(mesh)
         {
            sb.AppendLine("CTRL MESH 1");
            sb.AppendFormat("CTRL HMIN {0:F3}\n", hmin);
         }

         // add control string
         if(!string.IsNullOrEmpty(ctrl))
            sb.Append(ctrl);
         sb.AppendLine();

         // write structural lines
         foreach( var se in structural_elements )
         {
            if(se is GS_StructuralPoint)
            {
               var spt = se as GS_StructuralPoint;
               Point3d p = spt.Value.Location;

               string id_string = spt.Id > 0 ? spt.Id.ToString() : "-";

               sb.AppendFormat("SPT {0} X {1:F8} {2:F8} {3:F8}",id_string, p.X, p.Y, p.Z);

               if (spt.DirectionLocalX.Length > 0.0)
                  sb.AppendFormat(" SX {0:F6} {1:F6} {2:F6}", spt.DirectionLocalX.X, spt.DirectionLocalX.Y, spt.DirectionLocalX.Z);

               if (spt.DirectionLocalZ.Length > 0.0)
                  sb.AppendFormat(" NX {0:F6} {1:F6} {2:F6}", spt.DirectionLocalZ.X, spt.DirectionLocalZ.Y, spt.DirectionLocalZ.Z);

               if (string.IsNullOrWhiteSpace(spt.FixLiteral) == false)
                  sb.AppendFormat(" FIX {0}", spt.FixLiteral);

               sb.AppendLine();
            }
            // write structural lines
            else if(se is GS_StructuralLine)
            {
               var sln = se as GS_StructuralLine;

               string id_string = sln.Id > 0 ? sln.Id.ToString() : "-";
               string id_group = sln.GroupId > 0 ? sln.GroupId.ToString() : "-";
               string id_section = sln.SectionId > 0 ? sln.SectionId.ToString() : "-";

               sb.AppendFormat("SLN {0} GRP {1} SNO {2}", id_string, id_group, id_section);

               if (sln.DirectionLocalZ.Length > 0.0)
                  sb.AppendFormat(" DRX {0:F6} {1:F6} {2:F6}", sln.DirectionLocalZ.X, sln.DirectionLocalZ.Y, sln.DirectionLocalZ.Z);

               if (string.IsNullOrWhiteSpace(sln.FixLiteral) == false)
                  sb.AppendFormat(" FIX {0}", sln.FixLiteral);

               sb.AppendLine();

               AppendCurveGeometry(sb, sln.Value);
            }
            // write structural areas
            else if (se is GS_StructuralArea)
            {
               var sar = se as GS_StructuralArea;
               var brep = sar.Value;

               string id_string = sar.Id > 0 ? sar.Id.ToString() : "-";
               string grp_string = sar.GroupId > 0 ? sar.GroupId.ToString() : "-";
               string thk_string = sar.Thickness.ToString("F6");

               // some preparations
               brep.CullUnusedSurfaces();
               brep.CullUnusedFaces();
               brep.CullUnusedEdges();

               // loop over all faces within the brep
               foreach( var fc in brep.Faces)
               {
                  // checks and preparations
                  fc.ShrinkFace(BrepFace.ShrinkDisableSide.ShrinkAllSides);

                  if(fc.IsClosed(0) || fc.IsClosed(1))
                  {
                     this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A given Surface is closed in one direction.\nSuch surfaces cannot be handled by SOFiMSHC and need to be split.");
                  }

                  // write SAR header
                  sb.AppendLine();
                  sb.AppendFormat("SAR {0} GRP {1} T {2}", id_string, grp_string, thk_string);
                  id_string = string.Empty; // set only the 1st time

                  if (sar.MaterialId > 0)
                     sb.AppendFormat(" MNO {0}", sar.MaterialId.ToString());
                  if (sar.ReinforcementId > 0)
                     sb.AppendFormat(" MRF {0}", sar.ReinforcementId.ToString());
                  if(sar.DirectionLocalX.Length > 1.0E-8)
                  {
                     sb.AppendFormat(" DRX {0:F6} {1:F6} {2:F6}", sar.DirectionLocalX.X, sar.DirectionLocalX.Y, sar.DirectionLocalX.Z);
                  }

                  sb.AppendLine();

                  // outer boundary
                  AppendSurfaceBoundary(sb, fc);

                  // write geometry
                  if(fc.IsPlanar() == false)
                  {
                     AppendSurfaceGeometry(sb, fc.ToNurbsSurface());
                  }
               }
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unsupported type encountered: " + se.TypeName);
            }
         }
         sb.AppendLine();
         sb.AppendLine("END");

         da.SetData(0, sb.ToString());
      }

      #region CURVE GEOMETRY
      private void AppendCurveGeometry(StringBuilder sb, LineCurve l)
      {
         Point3d pa = l.Line.From;
         Point3d pe = l.Line.To;

         sb.AppendFormat(" SLNB X1 {0:F8} {1:F8} {2:F8} ", pa.X, pa.Y, pa.Z);
         sb.AppendFormat(" X2 {0:F8} {1:F8} {2:F8} ", pe.X, pe.Y, pe.Z);
         sb.AppendLine();
      }

      private void AppendCurveGeometry(StringBuilder sb, ArcCurve a)
      {
         Point3d pa = a.PointAtStart;
         Point3d pe = a.PointAtEnd;
         Point3d pm = a.Arc.Center;
         Vector3d n = a.Arc.Plane.Normal;

         sb.AppendFormat(" SLNB X1 {0:F8} {1:F8} {2:F8} ", pa.X, pa.Y, pa.Z);
         sb.AppendFormat(" X2 {0:F8} {1:F8} {2:F8} ", pe.X, pe.Y, pe.Z);
         sb.AppendFormat(" XM {0:F8} {1:F8} {2:F8} ", pm.X, pm.Y, pm.Z);
         sb.AppendFormat(" NX {0:F8} {1:F8} {2:F8} ", n.X, n.Y, n.Z);
         sb.AppendLine();
      }

      private void AppendCurveGeometry(StringBuilder sb, NurbsCurve n)
      {
         if(n.Knots.Count == 2 && n.Degree == 1) // is a straight line: write as SLNB-record
         {
            var pa = n.PointAtStart;
            var pe = n.PointAtEnd;

            sb.AppendFormat(" SLNB X1 {0:F8} {1:F8} {2:F8}", pa.X, pa.Y, pa.Z);
            sb.AppendFormat(" X2 {0:F8} {1:F8} {2:F8}", pe.X, pe.Y, pe.Z);
            sb.AppendLine();
         }
         else // write as nurbs
         {
            double l = n.GetLength();
            double k0 = n.Knots.First();
            double kn = n.Knots.Last();

            // re-parametrize knot vectors to length of curve
            double scale = 1.0;
            if (Math.Abs(kn - k0) > 1.0E-6)
               scale = l / (kn - k0);

            for (int i = 0; i < n.Knots.Count; ++i)
            {
               double si = (n.Knots[i] - k0) * scale;

               sb.AppendFormat(" SLNN S {0:F8}", si);
               if (i == 0)
                  sb.AppendFormat(" DEGR {0}", n.Degree);
               sb.AppendLine();
            }

            bool first = true;
            foreach (var p in n.Points)
            {
               sb.AppendFormat(" SLNP X {0:F8} {1:F8} {2:F8}", p.Location.X, p.Location.Y, p.Location.Z);
               if (p.Weight != 1.0)
               {
                  sb.AppendFormat(" W {0:F8}", p.Weight);
               }
               if (first)
               {
                  sb.Append(" TYPE NURB");
                  first = false;
               }
               sb.AppendLine();
            }
         }
      }

      private void AppendCurveGeometry(StringBuilder sb, Curve c)
      {
         if (c is LineCurve)
         {
            AppendCurveGeometry(sb, c as LineCurve);
         }
         else if (c is ArcCurve)
         {
            AppendCurveGeometry(sb, c as ArcCurve);
         }
         else if (c is NurbsCurve)
         {
            AppendCurveGeometry(sb, c as NurbsCurve);
         }
         else if(c is PolylineCurve)
         {
            var n = (c as PolylineCurve).ToNurbsCurve();
            if(n != null)
               AppendCurveGeometry(sb, n);
         }
         else if(c is PolyCurve)
         {
            var n = (c as PolyCurve).ToNurbsCurve();
            if (n != null)
               AppendCurveGeometry(sb, n);
         }
         else
         {
            throw new ArgumentException("Encountered curve type is currently not supported: " + c.GetType().ToString());
         }
      }
      #endregion

      #region SURFACE_GEOMETRY
      private void AppendSurfaceBoundary(StringBuilder sb, BrepFace fc)
      {
         foreach (var loop in fc.Loops)
         {
            string type;
            if (loop.LoopType == BrepLoopType.Outer)
               type = "OUT";
            else if (loop.LoopType == BrepLoopType.Inner)
               type = "IN";
            else
               continue;

            foreach (var tr in loop.Trims)
            {
               var ed = tr.Edge;
               if (ed != null)
               {
                  sb.AppendFormat("SARB {0}", type);
                  sb.AppendLine();
                  AppendCurveGeometry(sb, ed.EdgeCurve);
               }
            }
         }
      }

      private void AppendSurfaceGeometry(StringBuilder sb, NurbsSurface ns)
      {
         if (ns == null)
            return;

         double ulength;
         double vlength;

         // reparametrize to real world length to minimize distortion in the map from parameter space to 3D
         if(ns.GetSurfaceSize(out ulength, out vlength))
         {
            ns.SetDomain(0, new Interval(0, ulength));
            ns.SetDomain(1, new Interval(1, vlength));
         }

         // write knot vectors
         bool first = true;
         foreach( var ku in ns.KnotsU )
         {
            sb.AppendFormat(" SARN S {0:F8}", ku);
            if(first)
            {
               sb.AppendFormat(" DEGS {0}", ns.OrderU - 1);
               first = false;
            }
            sb.AppendLine();
         }

         first = true;
         foreach (var kv in ns.KnotsV)
         {
            sb.AppendFormat(" SARN T {0:F8}", kv);
            if (first)
            {
               sb.AppendFormat(" DEGT {0}", ns.OrderV - 1);
               first = false;
            }
            sb.AppendLine();
         }

         // write control points
         for( int i=0; i<ns.Points.CountV; ++i)
         {
            for(int j=0; j<ns.Points.CountU; ++j)
            {
               var cpt = ns.Points.GetControlPoint(j, i);
               double w = cpt.Weight;

               sb.AppendFormat(" SARP NURB {0} {1}", j + 1, i + 1);
               sb.AppendFormat(" X {0:F8} {1:F8} {2:F8} {3:F8}", cpt.X/w, cpt.Y/w, cpt.Z/w, w);
               sb.AppendLine();
            }
         }

      }
      #endregion
   }
}
