using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Text;

namespace gh_sofistik.Section
{
   public class CreateAquaInput : GH_Component
   {
      private System.Drawing.Bitmap _icon;

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

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGenericParameter("Section", "Sec", "SOFiSTiK Cross Section", GH_ParamAccess.list);
         pManager.AddTextParameter("Control Values", "Add. Ctrl", "Additional AQUA control values", GH_ParamAccess.item, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("Text input", "O", "SOFi text input", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var sections = new Dictionary<int, GH_Section>();

         string ctrl = da.GetData<string>(1);

         foreach (var it in da.GetDataList<GH_Section>(0))
         {
            if (it != null && !sections.ContainsKey(it.Value.Id))
               sections.Add(it.Value.Id, it);
         }

         StringBuilder sb = new StringBuilder();
         sb.AppendLine("+PROG AQUA");
         sb.AppendLine("HEAD");
         sb.AppendLine("PAGE UNII 0");

         if (!string.IsNullOrEmpty(ctrl))
            sb.AppendLine(ctrl);
         else
            sb.AppendLine("CTRL REST 1");

         sb.AppendLine();

         foreach (var kvp in sections)
         {
            var res = kvp.Value.Value.GetSectionDefinition(sb);
            if (res.Item1 != GH_RuntimeMessageLevel.Blank)
               AddRuntimeMessage(res.Item1, res.Item2);
         }
         sb.AppendLine("END");

         da.SetData(0, sb.ToString());
      }


   }
}