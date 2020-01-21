using System;
using Grasshopper.Kernel;

namespace gh_sofistik.General
{
   public class CreateGlobalSettings : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public CreateGlobalSettings() : base("Global Settings ", "Settings", "Global settings for scaling load arrows / adjusting colors / etc.", "SOFiSTiK", "General")
      {
      }

      public override Guid ComponentGuid
      {
         get
         {
            return new Guid("32EDDB4F-DD7F-42E5-9F7A-298D5035BE9F");
         }
      }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "user_options_24x24.png");
            return _icon;
         }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddColourParameter("Color Structural Elements", "Color Structural Elements", "Adjust Color for Structural Points/Lines/Areas", GH_ParamAccess.item, DrawUtil.DrawColorStructuralElements);
         pManager.AddColourParameter("Color Loads", "Color Loads", "Adjust Color for Point/Line/Area Load Forces and Moments", GH_ParamAccess.item, DrawUtil.DrawColorLoads);
         pManager.AddColourParameter("Color Supports", "Color Supports", "Adjust Color for Point and Line Supports", GH_ParamAccess.item, DrawUtil.DrawColorSupports);
         pManager.AddNumberParameter("Scale Factor Loads", "Scale Factor Loads", "Global Scale Factor for displayed SOFiSTiK Loads", GH_ParamAccess.item, DrawUtil.ScaleFactorLoads);
         pManager.AddNumberParameter("Density Factor Loads", "Density Factor Loads", "Global Density for Line/Area Loads", GH_ParamAccess.item, DrawUtil.DensityFactorLoads);
         pManager.AddNumberParameter("Scale Factor Supports", "Scale Factor Supports", "Global Scale Factor for displayed SOFiSTiK Support Symbols", GH_ParamAccess.item, DrawUtil.ScaleFactorSupports);
         pManager.AddNumberParameter("Density Factor Supports", "Density Factor Supports", "Global Density for Line Supports", GH_ParamAccess.item, DrawUtil.DensityFactorSupports);
         pManager.AddNumberParameter("Scale Factor LocalFrame", "Scale Factor LocalFrame", "Global Scale Factor for displayed local coordinate frames", GH_ParamAccess.item, DrawUtil.ScaleFactorLocalFrame);
         pManager.AddNumberParameter("Density Factor LocalFrame", "Density Factor LocalFrame", "Global Density Factor for displayed local coordinate frames", GH_ParamAccess.item, DrawUtil.DensityFactorLocalFrame);
         pManager.AddNumberParameter("Scale miscellaneous Elements", "Scale miscellaneous Elements", "Global Scale Factor for miscellaneous Elements", GH_ParamAccess.item, DrawUtil.ScaleFactorMisc);
         pManager.AddBooleanParameter("Show Info", "Show Info", "Show/Hide additional Information of Structural Elements when selected", GH_ParamAccess.item, DrawUtil.DrawInfo);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         //no output. just for scaling displayed forces
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         DrawUtil.DrawColorStructuralElements = DA.GetData<System.Drawing.Color>(0);
         DrawUtil.DrawColorLoads = DA.GetData<System.Drawing.Color>(1);
         DrawUtil.DrawColorSupports = DA.GetData<System.Drawing.Color>(2);
         DrawUtil.ScaleFactorLoads = DA.GetData<double>(3);
         DrawUtil.DensityFactorLoads = DA.GetData<double>(4);
         DrawUtil.ScaleFactorSupports = DA.GetData<double>(5);
         DrawUtil.DensityFactorSupports = DA.GetData<double>(6);
         DrawUtil.ScaleFactorLocalFrame = DA.GetData<double>(7);
         DrawUtil.DensityFactorLocalFrame = DA.GetData<double>(8);
         DrawUtil.ScaleFactorMisc = DA.GetData<double>(9);
         DrawUtil.DrawInfo = DA.GetData<bool>(10);
      }
   }
}