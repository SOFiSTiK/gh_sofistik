using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace gh_sofistik
{
   public class CreateStructuralLine : GH_Component
   {
      public CreateStructuralLine()
         : base("SLN","SLN","Sets Structural line properties","SOFiSTiK","Geometry")
      {}

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_line16; }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddCurveParameter("Curve", "C", "List of Curves", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Group", "GRP", "Group membership", GH_ParamAccess.item, 0);
         pManager.AddIntegerParameter("Section", "SNO", "Identifier of cross section", GH_ParamAccess.item, 0);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddCurveParameter("Curve", "C", "Curve", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var curves = new List<Curve>();

         int group_id = 0;
         int section_id = 0;

         if (!DA.GetDataList(0, curves)) return;
         if (!DA.GetData(1, ref group_id)) return;
         if (!DA.GetData(2, ref section_id)) return;

         foreach( var c in curves)
         {
            c.SetUserString("SOF_SLN_GRP", group_id.ToString());
            c.SetUserString("SOF_SLN_SNO", section_id.ToString());
         }

         DA.SetDataList(0, curves);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("743DEE7B-D30B-4286-B74B-1415B92E87F7"); }
      }
   }
}
