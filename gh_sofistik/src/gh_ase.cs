using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace gh_sofistik.General
{
   public class CreateAnalysisInput : GH_Component
   {
      private System.Drawing.Bitmap _icon;
      private string _manualPath = "";

      public CreateAnalysisInput()
         : base("Advanced Solution Engine", "ASE", "Creates an ASE input file", "SOFiSTiK", "General")
      { }

      public override Guid ComponentGuid
      {
         get { return new Guid("C6F8349C-A6AA-43E0-96A7-D48B8AD06B31"); }
      }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "ase_24x24.png");
            return _icon;
         }
      }

      public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
      {
         base.AppendAdditionalComponentMenuItems(menu);

         if (string.IsNullOrEmpty(_manualPath))
         {
            var exeDir = AssemblyHelper.GetSofistikExecutableDir();
            if (!string.IsNullOrWhiteSpace(exeDir) && System.IO.Directory.Exists(exeDir))
            {
               var manualPath = System.IO.Path.Combine(exeDir, "ase_1.pdf");
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
         // pManager.AddTextParameter("Modul", "Modul", "Specify modul used for analysis: \"ASE\", \"FEABENCH\"", GH_ParamAccess.item, "ASE");
         pManager.AddIntegerParameter("LoadCase IDs", "LoadCase", "IDs of LoadCases to analyse", GH_ParamAccess.tree);
         pManager.AddIntegerParameter("Number of Threads", "Number of Threads", "Number of threads to be used for parallel computation", GH_ParamAccess.item, -1);
         pManager.AddTextParameter("Control Values", "Add. Ctrl", "Additional Analysis control values", GH_ParamAccess.list, string.Empty);

         pManager[0].Optional = true;
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGenericParameter("Text input", "O", "SOFiSTiK text input for ASE", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         // var modulString = da.GetData<string>(0);
         var lcStruc = da.GetDataTree<GH_Integer>(0);
         var threadCount = da.GetData<int>(1);
         var ctrlList = da.GetDataList<string>(2);

         var lcList = new List<int>();
         foreach(var it in lcStruc.AllData(true))
         {
            if (it is GH_Integer)
               lcList.Add((it as GH_Integer).Value);
            else
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + it.TypeName + " to GH_Integer.");
         }

         bool isASE = true;
         // if (!string.IsNullOrEmpty(modulString) && modulString.ToLower() == "feabench")
         // {
         //    isASE = false;
         // }

         var sb = new StringBuilder();

         sb.AppendLine("+PROG " + (isASE ? "ASE" : "FEABENCH"));
         sb.AppendLine("HEAD");
         sb.AppendLine("PAGE UNII 0");
         if (threadCount != -1)
         {
            sb.AppendLine("CTRL CORE " + threadCount.ToString());
         }
         if (isASE)
         {
            sb.AppendLine("CTRL SOLV 4");
         }

         // add control string
         foreach (var ctrl in ctrlList)
            if (!string.IsNullOrEmpty(ctrl))
               sb.AppendLine(ctrl);

         sb.AppendLine();

         if (lcList.Count == 0)
         {
            sb.AppendLine("LC ALL");
         }
         else
         {
            foreach(var lc in lcList)
            {
               sb.AppendLine("LC " + lc.ToString());
            }
         }

         sb.AppendLine();

         sb.AppendLine("END");

         // create output objects
         var analysisModel = new gh_sofistik.General.SofistikModel() { CadInp = sb.ToString(), ModelType = gh_sofistik.General.SofistikModelType.Analysis };
         var ghAnalysisModel = new gh_sofistik.General.GH_SofistikModel() { Value = analysisModel };
         // create GH_Structure and use SetDataTree so output has always one branch with index {0}
         var outStruc = new GH_Structure<gh_sofistik.General.GH_SofistikModel>();
         outStruc.Append(ghAnalysisModel);
         da.SetDataTree(0, outStruc);
      }
   }
}