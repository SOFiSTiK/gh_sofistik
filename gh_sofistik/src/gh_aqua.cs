using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace gh_sofistik.Section
{
   public class CreateAquaInput : GH_Component
   {
      private System.Drawing.Bitmap _icon;
      private string _manualPath = "";

      public CreateAquaInput()
         : base("Aqua", "Aqua", "Creates an Aqua input file", "SOFiSTiK", "Section")
      { }

      public override Guid ComponentGuid
      {
         get { return new Guid("E1CEBD66-7B32-40B8-BCFA-86D9DEF79E00"); }
      }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "aqua_24x24.png");
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
               var manualPath = System.IO.Path.Combine(exeDir, "aqua_1.pdf");
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
         pManager.AddGenericParameter("Section", "Sec", "SOFiSTiK Cross Section", GH_ParamAccess.tree);
         pManager.AddTextParameter("Control Values", "Add. Ctrl", "Additional AQUA control values", GH_ParamAccess.list, string.Empty);

         pManager[0].Optional = true;
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGenericParameter("Text input", "O", "SOFiSTiK text input for materials and sections", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var secStruc = da.GetDataTree<IGH_Goo>(0);
         var ctrlList = da.GetDataList<string>(1);

         var sections = new Dictionary<int, GH_Section>();

         foreach (var it in secStruc.AllData(true))
         {
            if (it is GH_Section)
            {
               var ghSec = it as GH_Section;
               if (!sections.ContainsKey(ghSec.Value.Id))
                  sections.Add(ghSec.Value.Id, ghSec);
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + it.TypeName + " to GH_Section.");
            }
         }

         var ctrlListNotEmpty = ctrlList.Where(ctrl => !string.IsNullOrEmpty(ctrl));

         int unitSet = 0;
         if (sections.Any())
            unitSet = sections.First().Value.Value.GetUnitSet();

         StringBuilder sb = new StringBuilder();

         sb.AppendLine("+PROG AQUA");
         sb.AppendLine("HEAD");
         sb.AppendLine("PAGE UNII " + unitSet);
         if (ctrlListNotEmpty.Any())
         {
            foreach (var ctrl in ctrlListNotEmpty)
            {
               sb.AppendLine(ctrl);
            }
         }
         else
         {
            sb.AppendLine("CTRL REST 1");
         }

         sb.AppendLine();

         foreach (var kvp in sections)
         {
            var secUnitSet = kvp.Value.Value.GetUnitSet();
            if (secUnitSet != unitSet)
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Section " + kvp.Value.Value.Id + ": " + kvp.Value.Value.Name + " has different unit than other sections.");
            }
            else
            {
               var res = kvp.Value.Value.GetSectionDefinition(sb);
               if (res.Item1 != GH_RuntimeMessageLevel.Blank)
                  AddRuntimeMessage(res.Item1, res.Item2);
            }
         }

         sb.AppendLine("END");

         // create output objects
         var aquaModel = new gh_sofistik.General.SofistikModel() { CadInp = sb.ToString(), ModelType = gh_sofistik.General.SofistikModelType.AQUA };
         var ghAquaModel = new gh_sofistik.General.GH_SofistikModel() { Value = aquaModel };
         // create GH_Structure and use SetDataTree so output has always one branch with index {0}
         var outStruc = new GH_Structure<gh_sofistik.General.GH_SofistikModel>();
         outStruc.Append(ghAquaModel);
         da.SetDataTree(0, outStruc);
      }


   }
}