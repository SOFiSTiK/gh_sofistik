using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace gh_sofistik
{
   public class CreateFeabenchInput : GH_Component
   {
      public class AnalysisType
      {
         private bool _physical_nonl = false;
         private bool _geometrical_nonl = false;

         // Constructor:         
         public AnalysisType()
         {
            PhysicalNonlinear = false;
            GeometricalNonlinear = false;
         }

         public bool PhysicalNonlinear
         {
            get { return _physical_nonl; }
            set
            {
               _physical_nonl = value;
            }
         }
         public bool GeometricalNonlinear
         {
            get { return _geometrical_nonl; }
            set
            {
               _geometrical_nonl = value;
            }
         }
      }

      public AnalysisType Analysis { get; } = new AnalysisType();
      private BoundingBox _boundingBox = new BoundingBox();

      // default constructor
      public CreateFeabenchInput()
         : base("FEABENCH", "FEABENCH", "Creates analysis input file", "SOFiSTiK", "Analysis")
      {
         IssueMessage();
      }

      public void IssueMessage()
      {
         if (Analysis.PhysicalNonlinear & Analysis.GeometricalNonlinear)
         {
            Message = "Physically & Geometrically nonlinear FEA";
         }
         else if (Analysis.PhysicalNonlinear)
         {
            Message = "Physically nonlinear FEA";
         }
         else if (Analysis.GeometricalNonlinear)
         {
            Message = "Geometrically nonlinear FEA";
         }
         else //(!PhysicalNonlinear & !GeometricalNonlinear)
         {
            Message = "Linear FEA";
         }
      }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.feabench_24x24; }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("EF71CD5D-1BE9-4170-BD2A-DC8680F8ACC4"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddTextParameter("Title", "Title", "Title of the task", GH_ParamAccess.item, "");
         pManager.AddTextParameter("Number of Threads", "Number of Threads", "Number of threads to be used for parallel computation", GH_ParamAccess.item, "-");
         pManager.AddTextParameter("Additional Controls", "User Ctrls", "Additional FEABENCH controls", GH_ParamAccess.item, string.Empty);
         pManager.AddGenericParameter("LoadCase", "LoadCase", "LoadCase control", GH_ParamAccess.list);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("Text input", "O", "FEABENCH text input", GH_ParamAccess.item);
      }

      public override void DrawViewportWires(IGH_PreviewArgs args)
      {
         base.DrawViewportWires(args);

         if (this.Attributes.Selected)
         {
            args.Display.DrawBox(_boundingBox, args.WireColour_Selected);
         }
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         string title = da.GetData<string>(0);
         string ctrl_core = da.GetData<string>(1);
         string user_ctrls = da.GetData<string>(2);
         // get load case definitions
         var loadcases = new List<GS_LoadCase>();
         var idx = Params.IndexOfInputParam("LoadCase");
         if (Params.Input.ElementAtOrDefault(idx).SourceCount < 1)
         {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "LoadCase must be connected.");
            return;
         }
         foreach (var it in da.GetDataList<IGH_Goo>(3))
         {
            if (it is GS_LoadCase)
               loadcases.Add(it as GS_LoadCase);
            else
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + it.TypeName + " to GS_LoadCase.");
         }


         var sb = new StringBuilder();

         sb.AppendLine("+PROG FEABENCH");
         sb.AppendLine("HEAD " + title);
         sb.AppendLine("PAGE UNII 0"); // export always in SOFiSTiK database units
         sb.AppendLine("ECHO STAT EXTR"); // time statistics hhdebug
         sb.AppendFormat("CTRL CORE {0}\n", ctrl_core);
         sb.AppendFormat("!test 30 4  ! 1 = ISO | 2 = CS | 3 = EAS1 | 4 = EAS2 (def) | 5 =EAS3 (def)\n"); // debug
         // add additional text
         if (!string.IsNullOrEmpty(user_ctrls))
         {
            sb.Append(user_ctrls);
         }
         sb.AppendLine();
         var phys = "'LINE' ";
         if (Analysis.PhysicalNonlinear)
         {
            phys = "'NONL' ";
         }
         var geom = "'TH1' ";
         if (Analysis.GeometricalNonlinear)
         {
            geom = "'TH3' ";
         }
         sb.AppendFormat("TASK 'FEA' PHYS " + phys + "GEOM " + geom + "\n");
         for (int i = 0; i < loadcases.Count; ++i)
         {
            var loadcase = loadcases.GetItemOrLast(i);
            sb.AppendFormat("{0}\n",loadcase.ToCadinp()); // hhdebug
         }

         sb.AppendLine();
         sb.AppendLine("END");

         da.SetData(0, sb.ToString());
      }

      protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
      {
         base.AppendAdditionalComponentMenuItems(menu);

         var task_drop_down = new ToolStripDropDown();
         var task_button = new ToolStripDropDownButton();
         task_button.Text = "Analysis tasks";
         task_button.DropDown = task_drop_down;
         task_button.DropDownDirection = ToolStripDropDownDirection.Right;
         task_button.ShowDropDownArrow = true;
         var fea_button = new ToolStripButton("FE Analysis");
         var eige_button = new ToolStripButton("Dynamic Eigenvalue Analysis");
         var buck_button = new ToolStripButton("Buckling Eigenvalue Analysis");
         task_drop_down.Items.AddRange(new ToolStripItem[] { fea_button, eige_button, buck_button });
         //Menu_AppendSeparator(menu);
         //Menu_AppendCustomItem(menu, task_drop_down);
         Menu_AppendSeparator(menu);
         Menu_AppendItem(menu, "Physically nonlinear FEA", Menu_OnPhysicalNonlinearClicked, true, Analysis.PhysicalNonlinear);
         Menu_AppendItem(menu, "Geometrically nonlinear FEA", Menu_OnGeometricalNonlinearClicked, true, Analysis.GeometricalNonlinear);
      }

      private void Menu_OnPhysicalNonlinearClicked(Object sender, EventArgs e)
      {
         if (sender is System.Windows.Forms.ToolStripMenuItem)
         {
            var mi = sender as System.Windows.Forms.ToolStripMenuItem;
            Analysis.PhysicalNonlinear = !mi.Checked;
         }
         else // fallback
         {
            Analysis.PhysicalNonlinear = !Analysis.PhysicalNonlinear;
         }
         IssueMessage();
         ExpireSolution(true); // This will make sure all caches are erased, all downstream objects are expired and that the event is raised
      }

      private void Menu_OnGeometricalNonlinearClicked(Object sender, EventArgs e)
      {
         if (sender is System.Windows.Forms.ToolStripMenuItem)
         {
            var mi = sender as System.Windows.Forms.ToolStripMenuItem;
            Analysis.GeometricalNonlinear = !mi.Checked;
         }
         else // fallback
         {
            Analysis.GeometricalNonlinear = !Analysis.GeometricalNonlinear;
         }
         IssueMessage();
         ExpireSolution(true); // This will make sure all caches are erased, all downstream objects are expired and that the event is raised
      }

   }

}

