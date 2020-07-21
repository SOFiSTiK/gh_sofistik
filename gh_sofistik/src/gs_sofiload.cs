using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace gh_sofistik.Load
{
   public class LoadCase
   {
      public int Id { get; set; } = 0;
      public string Type { get; set; } = string.Empty;
      public double Facd { get; set; } = 0.0;
      public string Title { get; set; } = string.Empty;
      public LoadCase()
      {
         this.Id = 0;
         this.Facd = 0.0;
         this.Type = string.Empty;
         this.Title = string.Empty;
      }
      // Copy Constructor
      public LoadCase(LoadCase LoadCaseSource)
      {
         this.Id = LoadCaseSource.Id;
         this.Facd = LoadCaseSource.Facd;
         this.Type = LoadCaseSource.Type;
         this.Title = LoadCaseSource.Title;
      }
      public string ToCadinp()
      {
         var sb = new StringBuilder();
         sb.Clear();
         sb.AppendFormat("LC {0} TYPE {1}", Id, Type);
         if (Math.Abs(Facd) > 0.0)
            sb.AppendFormat(" FACD {0:F3}", Facd);
         if (string.IsNullOrEmpty(Title) == false)
            sb.AppendFormat(" TITL {0}", Title);

         return sb.ToString();
      }
      public LoadCase Duplicate()
      {
         return new LoadCase()
         {
            Id = Id,
            Title = Title,
            Type = Type,
            Facd = Facd,
         };
      }
   }
   public class GS_LoadCase : GH_Goo<LoadCase> //, IGS_LoadCase
   {
      public override string ToString()
      {
         return Value.ToCadinp();
      }

      // GS_LoadCase instances are always valid
      public override bool IsValid
      {
         get { return Value != null; }
      }

      public override string TypeName
      {
         get { return "GS_LoadCase"; }
      }

      public override string TypeDescription
      {
         get { return "Basic LoadCase information (Id, Type, Title, Facd)"; }
      }

      public override IGH_Goo Duplicate()
      {
         return new GS_LoadCase() { Value = Value.Duplicate() };
      }
   }

   public class CreateLoadCase : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public CreateLoadCase()
         : base("LoadCase Attributes", "LcAttr", "Defines loadcase attributes for SOFiLOAD", "SOFiSTiK", "Loads")
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
         pManager.AddGenericParameter("LoadCase Attributes", "LcAttr", "SOFiSTiK LoadCase Attributes", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var ids = da.GetDataList<int>(0);
         var types = da.GetDataList<string>(1);
         var facds = da.GetDataList<double>(2);
         var titls = da.GetDataList<string>(3);

         var load_cases = new List<GS_LoadCase>();

         for( int i=0; i<ids.Count; ++i)
         {
            var id = ids[i];
            var type = types.GetItemOrLast(i);
            var facd = facds.GetItemOrLast(i);
            var titl = titls.GetItemOrLast(i);

            if (string.IsNullOrEmpty(type))
               type = "NONE";

            if (Math.Abs(facd) < 1.0E-6)
               facd = 0.0;

            if (id <= 0)
               continue;

            var lc = new LoadCase()
            {
               Id = id,
               Type = type,
               Facd = facd,
               Title = titl
            };

            var gsLc = new GS_LoadCase()
            {
               Value = lc,
            };

            load_cases.Add(gsLc);
         }

         da.SetDataList(0, load_cases);
      }
   }

   public class CreateSofiloadInput : GH_Component
   {
      private System.Drawing.Bitmap _icon;
      private string _manualPath = "";

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

      public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
      {
         base.AppendAdditionalComponentMenuItems(menu);

         if (string.IsNullOrEmpty(_manualPath))
         {
            var exeDir = AssemblyHelper.GetSofistikExecutableDir();
            if (!string.IsNullOrWhiteSpace(exeDir) && System.IO.Directory.Exists(exeDir))
            {
               var manualPath = System.IO.Path.Combine(exeDir, "sofiload_1.pdf");
               if (System.IO.File.Exists(manualPath))
               {
                  _manualPath = manualPath;
               }
            }
         }

         if (!string.IsNullOrWhiteSpace(_manualPath) && System.IO.File.Exists(_manualPath))
         {
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Open Manual", Menu_OnOpenManual);
         }
      }

      private void Menu_OnOpenManual(object sender, EventArgs e)
      {
         if (!string.IsNullOrWhiteSpace(_manualPath) && System.IO.File.Exists(_manualPath))
         {
            System.Diagnostics.Process.Start(@_manualPath);
         }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGeometryParameter("Ld", "Ld", "Collection of SOFiSTiK Load Items", GH_ParamAccess.tree);
         pManager.AddGenericParameter("LoadCase", "Lc", "SOFiSTiK LoadCase Definition", GH_ParamAccess.tree);
         pManager.AddTextParameter("User Text", "User Text", "Additional text input being placed after the definition of loads", GH_ParamAccess.list, string.Empty);

         pManager[0].Optional = true;
         pManager[1].Optional = true;
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         //pManager.AddTextParameter("Text input", "O", "SOFiLOAD text input", GH_ParamAccess.item);
         pManager.AddGenericParameter("Text input", "O", "SOFiLOAD text input", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var ldStruc = da.GetDataTree<IGH_GeometricGoo>(0);
         var lcStruc = da.GetDataTree<IGH_Goo>(1);
         var textList = da.GetDataList<string>(2);

         // get load case definitions
         var all_loads = new List<IGS_Load>();
         foreach( var it in ldStruc.AllData(true))
         {
            if (it is IGS_Load)
               all_loads.Add(it as IGS_Load);
            else
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + it.TypeName + " to IGS_Load.");
         }

         // extract load case headers
         var load_cases = new Dictionary<int, string>();
         foreach (var it in lcStruc.AllData(true))
         {
            if (it is GS_LoadCase)
            {
               var ilc = it as GS_LoadCase;
               if (ilc.Value.Id != 0)
               {
                  load_cases.Add(ilc.Value.Id, ilc.Value.ToCadinp().Trim());
               }
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + it.TypeName + " to GS_LoadCase.");
            }
         }

         // calc unit conversion factor and scale-transform
         var currentUnitSystem = Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem;
         bool scaleUnit = currentUnitSystem != Rhino.UnitSystem.Meters;
         var unitFactor = Rhino.RhinoMath.UnitScale(currentUnitSystem, Rhino.UnitSystem.Meters);
         var tU = Transform.Scale(Point3d.Origin, unitFactor);

         var sb = new StringBuilder();

         sb.AppendLine("+PROG SOFILOAD");
         sb.AppendLine("HEAD");
         sb.AppendLine("PAGE UNII 0"); // export always in SOFiSTiK database units
         sb.AppendLine();

         if (!all_loads.Any() && !load_cases.Any())
         {
            sb.AppendLine("LC 1 FACD 1.0");
         }

         string[,] load_types = { { "PXX", "PYY", "PZZ", "PX", "PY", "PZ" }, { "MXX", "MYY", "MZZ", "MX", "MY", "MZ" }, { "WXX", "WYY", "WZZ", "WX", "WY", "WZ" }, { "DXX", "DYY", "DZZ", "DX", "DY", "DZ" } };

         foreach (var lc_loads in all_loads.GroupBy(ld => ld.LoadCase).OrderBy( ig => ig.Key))
         {
            if (lc_loads.Count() == 0)
               continue;

            if(load_cases.TryGetValue(lc_loads.Key,out var lc_definition))
            {
               sb.AppendLine(lc_definition);
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

                  var plGeo = pl.Value.Duplicate() as Point;
                  if (scaleUnit)
                     plGeo.Transform(tU);

                  for (int i = 0; i < 3; ++i)
                  {
                     if (Math.Abs(pl.Forces[i]) > 1.0E-6)
                     {
                        sb.AppendFormat("POIN {0} {1}", ref_string, no_string);
                        sb.AppendFormat(" TYPE {0} {1:F6}", load_types[0, i + type_off], pl.Forces[i]);
                        AppendLoadGeometry(sb, plGeo);
                        sb.AppendLine();
                     }
                  }

                  for (int i = 0; i < 3; ++i)
                  {
                     if (Math.Abs(pl.Moments[i]) > 1.0E-6)
                     {
                        sb.AppendFormat("POIN {0} {1}", ref_string, no_string);
                        sb.AppendFormat(" TYPE {0} {1:F6}", load_types[1, i + type_off], pl.Moments[i]);
                        AppendLoadGeometry(sb, plGeo);
                        sb.AppendLine();
                     }
                  }

                  for (int i = 0; i < 3; ++i)
                  {
                     if (Math.Abs(pl.Displacement[i]) > 1.0E-6)
                     {
                        sb.AppendFormat("POIN {0} {1}", ref_string, no_string);
                        sb.AppendFormat(" TYPE {0} {1:F6}", load_types[2, i + type_off], scaleUnit ? pl.Displacement[i] * unitFactor : pl.Displacement[i]);
                        AppendLoadGeometry(sb, plGeo);
                        sb.AppendLine();
                     }
                  }

                  for (int i = 0; i < 3; ++i)
                  {
                     if (Math.Abs(pl.DisplacementRotational[i]) > 1.0E-6)
                     {
                        sb.AppendFormat("POIN {0} {1}", ref_string, no_string);
                        sb.AppendFormat(" TYPE {0} {1:F6}", load_types[3, i + type_off], pl.DisplacementRotational[i] * 1000);
                        AppendLoadGeometry(sb, plGeo);
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

                  var points = new List<Point3d>();
                  if (!hosted)
                  {
                     var crvGeo = ll.Value.DuplicateCurve();
                     if (scaleUnit)
                        crvGeo.Transform(tU);
                     points = GetCurvePolygon(crvGeo);
                  }

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

                  var brpGeo = al.Value.DuplicateBrep();
                  if (scaleUnit)
                     brpGeo.Transform(tU);

                  foreach( var fc in brpGeo.Faces )
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
         foreach (var text in textList)
            if (!string.IsNullOrEmpty(text))
               sb.AppendLine(text);

         sb.AppendLine();

         sb.AppendLine("END");

         // create output objects
         var loadModel = new gh_sofistik.General.SofistikModel() { CadInp = sb.ToString(), ModelType = gh_sofistik.General.SofistikModelType.SofiLOAD };
         var ghLoadModel = new gh_sofistik.General.GH_SofistikModel() { Value = loadModel };
         // create GH_Structure and use SetDataTree so output has always one branch with index {0}
         var outStruc = new GH_Structure<gh_sofistik.General.GH_SofistikModel>();
         outStruc.Append(ghLoadModel);
         da.SetDataTree(0, outStruc);
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
