using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace gh_sofistik.Open
{

   public class CreateLoadCase : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public CreateLoadCase()
         : base("LoadCase", "LoadCase", "Defines Load Cases for SOFiLOAD", "SOFiSTiK", "Loads")
      {}

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "loadcase_24x24.png");
            return _icon;
         }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("335DBCBC-3320-47E5-9633-29E129A4CBCF"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddIntegerParameter("Id", "Id", "Number of Load Case", GH_ParamAccess.list);
         pManager.AddTextParameter("Type", "Type", "Type / Action of Load Case", GH_ParamAccess.list, string.Empty);
         pManager.AddNumberParameter("Facd", "Facd", "Factor of structural dead weight", GH_ParamAccess.list, 0.0);
         pManager.AddTextParameter("Title", "Title", "Title of Load Case", GH_ParamAccess.list, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("Lc", "Lc", "SOFiLOAD Input", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var ids = da.GetDataList<int>(0);
         var types = da.GetDataList<string>(1);
         var facds = da.GetDataList<double>(2);
         var titls = da.GetDataList<string>(3);

         var load_cases = new List<string>();

         var sb = new StringBuilder();

         for( int i=0; i<ids.Count; ++i)
         {
            sb.Clear();

            var id = ids[i];
            var type = types.GetItemOrLast(i);
            var facd = facds.GetItemOrLast(i);
            var titl = titls.GetItemOrLast(i);

            if (type == string.Empty) type = "NONE";

            sb.AppendFormat("LC {0} TYPE {1}", id, type);
            if (Math.Abs(facd) > 1.0E-6)
               sb.AppendFormat(" FACD {0:F3}", facd);
            if (string.IsNullOrEmpty(titl) == false)
               sb.AppendFormat(" TITL {0}", titl);
            sb.AppendLine();

            load_cases.Add(sb.ToString());
         }

         da.SetDataList(0, load_cases);
      }
   }

   public class CreateSofiloadInput : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public CreateSofiloadInput()
         : base("SOFiLOAD", "SOFiLOAD", "Creates a SOFiLOAD input file", "SOFiSTiK", "Loads")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "sofiload_24x24.png");
            return _icon;
         }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("9B69E8B6-D0DE-4B5C-A564-B127961999BD"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGeometryParameter("Ld", "Ld", "Collection of SOFiSTiK Load Items", GH_ParamAccess.list);
         pManager.AddTextParameter("Lc", "Lc", "Load Case Definition (in SOFiLOAD Syntax)", GH_ParamAccess.list, string.Empty);
         pManager.AddTextParameter("User Text", "User Text", "Additional text input being placed after the definition of loads", GH_ParamAccess.item, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("Text input", "O", "SOFiLOAD text input", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         // get load case definitions
         var all_loads = new List<IGS_Load>();
         foreach( var it in da.GetDataList<IGH_Goo>(0))
         {
            if (it is IGS_Load)
               all_loads.Add(it as IGS_Load);
            else
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + it.TypeName + " to IGS_Load.");
         }

         // extract load case headers
         var load_cases = new Dictionary<int, string>();
         foreach( var ilc in da.GetDataList<string>(1))
         {
            int id = 0;
            var slc = ilc.Split(null,3);
            if(slc.Length>2 && int.TryParse(slc[1],out id))
            {
               load_cases.Add(id, ilc);
            }
         }

         string text = da.GetData<string>(2);

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

            string lc_definition = string.Empty;
            if(load_cases.TryGetValue(lc_loads.Key,out lc_definition))
            {
               sb.AppendLine(lc_definition.Trim());
               load_cases.Remove(lc_loads.Key);
            }
            else
            {
               sb.AppendFormat("LC {0}", lc_loads.Key);
               sb.AppendLine();
            }

            foreach( var ld in lc_loads)
            {
               if (ld is GS_PointLoad)
               {
                  var pl = ld as GS_PointLoad;

                  //string id_string = pl.LoadCase.ToString();
                  string ref_string = (!(pl.ReferencePoint is null)) && pl.ReferencePoint.Id > 0 ? "NODE" : "AUTO";
                  string no_string = (!(pl.ReferencePoint is null)) && pl.ReferencePoint.Id > 0 ? pl.ReferencePoint.Id.ToString() : "-";
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

                  bool hosted = (!(ll.ReferenceLine is null)) && ll.ReferenceLine.Id > 0;

                  string cmd_string = ll.Value.IsLinear() ? "LINE" : "CURV";
                  string ref_string = hosted ? "SLN" : "AUTO";
                  string no_string = hosted ? ll.ReferenceLine.Id.ToString() : "-";
                  int type_off = ll.UseHostLocal ? 3 : 0;

                  var points = hosted ? new List<Point3d>() : GetCurvePolygon(ll.Value);

                  for (int i=0; i < 3; ++i)
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
                        sb.AppendFormat(" TYPE {0} {1:F6}", load_types[1, i + type_off], ll.Moments[i]);
                        AppendLoadGeometry(sb, points);
                        sb.AppendLine();
                     }
                  }
               }
               else if(ld is GS_AreaLoad)
               {
                  var al = ld as GS_AreaLoad;

                  bool hosted = (!(al.ReferenceArea is null)) && al.ReferenceArea.Id > 0;

                  string cmd_string = "AREA";
                  string ref_string = hosted ? "SAR" : "AUTO";
                  string no_string = hosted ? al.ReferenceArea.Id.ToString() : "-";
                  int type_off = al.UseHostLocal ? 3 : 0;

                  foreach( var fc in al.Value.Faces )
                  {
                     // checks and preparations
                     fc.ShrinkFace(BrepFace.ShrinkDisableSide.ShrinkAllSides);

                     if (fc.IsClosed(0) || fc.IsClosed(1))
                     {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A given Surface is closed in one direction.\nSuch surfaces cannot be handled properly and need to be split.");
                     }

                     var points = hosted ? new List<Point3d>() : GetFacePolygon(fc);

                     if(points.Count > 63)
                     {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Boundary of area load is too complex to be resolved properly.\nPlease simplify given brep geometry or use structural area as input.");
                        points.RemoveRange(63, points.Count - 63);
                     }

                     for (int i = 0; i < 3; ++i)
                     {
                        if (Math.Abs(al.Forces[i]) > 1.0E-6)
                        {
                           sb.AppendFormat("{0} {1} {2}", cmd_string, ref_string, no_string);
                           sb.AppendFormat(" TYPE {0} {1:F6}", load_types[0, i + type_off], al.Forces[i]);
                           AppendLoadGeometry(sb, points);
                           sb.AppendLine();
                        }
                     }

                     for (int i = 0; i < 3; ++i)
                     {
                        if (Math.Abs(al.Moments[i]) > 1.0E-6)
                        {
                           sb.AppendFormat("{0} {1} {2}", cmd_string, ref_string, no_string);
                           sb.AppendFormat(" TYPE {0} {1:F6}", load_types[1, i + type_off], al.Moments[i]);
                           AppendLoadGeometry(sb, points);
                           sb.AppendLine();
                        }
                     }
                  }
               }
               else
               {
                  AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unsupported type encountered: " + ld.TypeName);
               }
               sb.AppendLine();
            }
         }

         // write load case definitions not being considered before
         foreach( var ilc in load_cases )
         {
            sb.Append(ilc.Value);
            sb.AppendLine();
         }

         // add additional text
         if (!string.IsNullOrEmpty(text))
         {
            sb.Append(text);
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
            int max_seg = 18;
            int n_seg = Math.Max(3, (int)((double)max_seg * alph / 3.1415));
            
            for(int i=0; i<=n_seg; ++i)
            {
               double si = cv.Domain.ParameterAt((double)i / (double)n_seg);
               points.Add(cv.PointAt(si));
            }
         }

         return points;
      }

      private List<Point3d> GetFacePolygon(BrepFace fc)
      {
         List<Point3d> points = new List<Point3d>();
         foreach (BrepTrim bt in fc.OuterLoop.Trims)
         {
            Curve crv = bt.Edge?.EdgeCurve;
            if (!(crv is null))
            {
               List<Point3d>cList=GetCurvePolygon(crv);
               foreach(Point3d cP in cList)
               {
                  if (!points.Contains(cP)) points.Add(cP);
               }
            }
         }
         return points;
         //return GetCurvePolygon(fc.OuterLoop.Trims.To3dCurve());      // alternative version
      }

      /*private List<Point3d> GetFacePolygon(BrepFace fc)      //old version: adds multiple points
      {
         var points = new List<Point3d>();

         foreach( var loop in fc.Loops)
         {
            if(loop.LoopType == BrepLoopType.Outer)
            {
               foreach( var tr in loop.Trims )
               {
                  var cv = tr?.Edge?.EdgeCurve;
                  if(cv != null)
                  {
                     points.AddRange(GetCurvePolygon(cv));
                  }
               }
            }
         }

         return points;
      }*/
   }
}
