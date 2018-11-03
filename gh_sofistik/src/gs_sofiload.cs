using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace gh_sofistik
{

   public class CreateSofiloadInput : GH_Component
   {
      public CreateSofiloadInput()
         : base("SOFiLOAD", "SOFiLOAD", "Creates a SOFiLOAD input file", "SOFiSTiK", "Loads")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.sofiload_24x24; }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("9B69E8B6-D0DE-4B5C-A564-B127961999BD"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         //pManager.AddNumberParameter("Intersection tolerance", "TOLG", "Intersection tolerance", GH_ParamAccess.item, 0.01);
         //pManager.AddBooleanParameter("Create mesh", "MESH", "Activates mesh generation", GH_ParamAccess.item, true);
         //pManager.AddNumberParameter("Mesh density", "HMIN", "Allows to set the global mesh density in [m]", GH_ParamAccess.item, 1.0);
         //pManager.AddTextParameter("Additional text input", "TXT", "Additional SOFiMSHC text input", GH_ParamAccess.item, string.Empty);
         pManager.AddGeometryParameter("Loads", "Ld", "Collection of SOFiSTiK Loads", GH_ParamAccess.list);

      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("Text input", "O", "SOFiLOAD text input", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var all_loads = new List<IGS_Load>();
         foreach( var it in da.GetDataList<IGH_Goo>(0))
         {
            if (it is IGS_Load)
               all_loads.Add(it as IGS_Load);
            else
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + it.TypeName + " to IGS_Load.");
         }

         var sb = new StringBuilder();

         sb.AppendLine("+PROG SOFILOAD");
         sb.AppendLine("HEAD");
         sb.AppendLine("PAGE UNII 0"); // export always in SOFiSTiK database units
         sb.AppendLine();

         string[,] load_types = { { "PXX", "PYY", "PZZ", "PX", "PY", "PZ" },{ "MXX", "MYY", "MZZ", "MX","MY","MZ"} };

         foreach (var lc_loads in all_loads.GroupBy(ld => ld.LoadCase).OrderBy( ig => ig.Key))
         {
            if (lc_loads.Count() == 0)
               continue;

            sb.AppendFormat("LC {0}", lc_loads.Key);
            sb.AppendLine();

            foreach( var ld in lc_loads)
            {
               if (ld is GS_PointLoad)
               {
                  var pl = ld as GS_PointLoad;

                  //string id_string = pl.LoadCase.ToString();
                  string ref_string = pl.ReferencePointId > 0 ? "NODE" : "AUTO";
                  string no_string = pl.ReferencePointId > 0 ? pl.ReferencePointId.ToString() : "-";
                  int type_off = pl.UseHostLocal ? 3 : 0;

                  for (int i = 0; i < 3; ++i)
                  {
                     if (Math.Abs(pl.Forces[i]) > 1.0E-6)
                     {
                        sb.AppendFormat("POIN {0} {1}", ref_string, no_string);
                        sb.AppendFormat(" TYPE {0} {1:F6}", load_types[0, i + type_off], pl.Forces[i]);
                        AppendLoadGeometry(sb, pl.Value);
                        sb.AppendLine();
                     }
                  }

                  for (int i = 0; i < 3; ++i)
                  {
                     if (Math.Abs(pl.Moments[i]) > 1.0E-6)
                     {
                        sb.AppendFormat("POIN {0} {1}", ref_string, no_string);
                        sb.AppendFormat(" TYPE {0} {1:F6}", load_types[1, i + type_off], pl.Moments[i]);
                        AppendLoadGeometry(sb, pl.Value);
                        sb.AppendLine();
                     }
                  }
               }
               else if(ld is GS_LineLoad)
               {
                  var ll = ld as GS_LineLoad;

                  bool hosted = ll.ReferenceLineId > 0;

                  string cmd_string = ll.Value.IsLinear() ? "LINE" : "CURV";
                  string ref_string = hosted ? "SLN" : "AUTO";
                  string no_string = hosted ? ll.ReferenceLineId.ToString() : "-";
                  int type_off = ll.UseHostLocal ? 3 : 0;

                  var points = hosted ? new List<Point3d>() : GetCurvePolygon(ll.Value);

                  for (int i=0; i<3; ++i)
                  {
                     if(Math.Abs(ll.Forces[i]) > 1.0E-6)
                     {
                        sb.AppendFormat("{0} {1} {2}", cmd_string, ref_string, no_string);
                        sb.AppendFormat(" TYPE {0} {1:F6}", load_types[0, i + type_off], ll.Forces[i]);
                        AppendLoadGeometry(sb, points);
                        sb.AppendLine();
                     }
                  }

                  for (int i = 0; i < 3; ++i)
                  {
                     if (Math.Abs(ll.Moments[i]) > 1.0E-6)
                     {
                        sb.AppendFormat("{0} {1} {2}", cmd_string, ref_string, no_string);
                        sb.AppendFormat(" TYPE {0} {1:F6}", load_types[1, i + type_off], ll.Forces[i]);
                        AppendLoadGeometry(sb, points);
                        sb.AppendLine();
                     }
                  }
               }
               else
               {
                  AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unsupported type encountered: " + ld.TypeName);
               }
            }
         }

         sb.AppendLine();
         sb.AppendLine("END");

         da.SetData(0, sb.ToString());
      }


      private void AppendLoadGeometry(StringBuilder sb, Point pt)
      {
         sb.AppendFormat(" X {0:F6} {1:F6} {2:F6}", pt.Location.X, pt.Location.Y, pt.Location.Z);
      }

      private void AppendLoadGeometry(StringBuilder sb, List<Point3d> points)
      {
         if (points==null || points.Count == 0)
            return;

         int x_count = 0;
         for(int i=0; i<points.Count; ++i)
         {
            x_count += 1;
            if(x_count > 6)
            {
               sb.AppendLine();
               sb.AppendFormat("     TYPE CONT");
               x_count = 1;
            }

            Point3d pi = points[i];
            sb.AppendFormat(" X{0} {1:F6} {2:F6} {3:F6}", x_count, pi.X, pi.Y, pi.Z);
         }
      }

      private List<Point3d> GetCurvePolygon(Curve cv)
      {
         var points = new List<Point3d>();

         if(cv.IsLinear())
         {
            points.Add(cv.PointAtStart);
            points.Add(cv.PointAtEnd);
         }
         else
         {
            var tstart = cv.TangentAtStart;
            var tmid = cv.TangentAt(cv.Domain.Mid);

            double alph = Vector3d.VectorAngle(tstart, tmid);
            int n_seg = (int)(18.0 * alph / 3.1415);

            for(int i=0; i<n_seg+1; ++i)
            {
               double si = cv.Domain.ParameterAt((double)i / (double)n_seg);
               points.Add(cv.PointAt(si));
            }
         }

         return points;
      }


      //private void AppendLoadLine(StringBuilder sb, )
   }
}
