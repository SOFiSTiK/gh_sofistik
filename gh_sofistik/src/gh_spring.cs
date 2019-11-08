using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino;
using Rhino.DocObjects;

namespace gh_sofistik.Open
{
   public class GH_Spring : GH_GeometricGoo<GH_CouplingStruc>, IGH_PreviewData
   {
      public int GroupId { get; set; } = 0;

      public double Axial_stiffness { get; set; } = 0.0;

      public double Rotational_stiffness { get; set; } = 0.0;

      public double Transversal_stiffness { get; set; } = 0.0;

      public Vector3d Direction { get; set; } = new Vector3d(0,0,1);

      private CouplingCondition _cplCond = new CouplingCondition();

      public BoundingBox ClippingBox
      {
         get
         {
            BoundingBox bBox;
            if (Value.IsACurve)
               bBox = Value.CurveA.GetBoundingBox(false);
            else
               bBox = Value.PointA.GetBoundingBox(false);
            return DrawUtil.GetClippingBoxSupport(bBox);
         }
      }

      public override BoundingBox Boundingbox
      {
         get
         {
            if (Value.IsACurve)
               return Value.CurveA.GetBoundingBox(true);
            else
               return Value.PointA.GetBoundingBox(true);
         }
      }

      public override string TypeName
      {
         get
         {
            return "GH_Spring";
         }
      }

      public override string TypeDescription
      {
         get
         {
            return "Spring fixation for Points and Lines";
         }
      }

      public void DrawViewportMeshes(GH_PreviewMeshArgs args)
      {

      }

      public void DrawViewportWires(GH_PreviewWireArgs args)
      {
         //ClippingBox
         //args.Pipeline.DrawBox(ClippingBox, System.Drawing.Color.Black);
         
         if (!_cplCond.isValid)
         {
            updateSpring();
         }

         System.Drawing.Color col = args.Color;
         if (!DrawUtil.CheckSelection(col))
            col = DrawUtil.DrawColorSupports;
         else
            _cplCond.DrawInfo(args);

         _cplCond.Draw(args.Pipeline, col);
      }

      private void updateSpring()
      {         
         _cplCond = new CouplingCondition();
         List<string> sl = new List<string>();
         sl.Add("Stf: " + Axial_stiffness + " / " + Rotational_stiffness + " / " + Transversal_stiffness);

         _cplCond.CreateSpringSymbols(Value.GetSingleInputPoints(), sl, Direction);
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         GH_Spring nc = new GH_Spring();
         nc.Value = new GH_CouplingStruc();
         if (Value.Reference_A != null)
         {
            if (Value.IsACurve)
               nc.Value.Reference_A = (Value.Reference_A as GS_StructuralLine).DuplicateGeometry() as GS_StructuralLine;
            else
               nc.Value.Reference_A = (Value.Reference_A as GS_StructuralPoint).DuplicateGeometry() as GS_StructuralPoint;
         }
         nc.GroupId = GroupId;
         nc.Axial_stiffness = Axial_stiffness;
         nc.Rotational_stiffness = Rotational_stiffness;
         nc.Transversal_stiffness = Transversal_stiffness;
         nc.Direction = Direction;
         return nc;
      }

      public override BoundingBox GetBoundingBox(Transform xform)
      {
         return xform.TransformBoundingBox(Boundingbox);
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         GH_Spring nc = this.DuplicateGeometry() as GH_Spring;
         if (nc.Value.IsACurve)
            nc.Value.CurveA.Transform(xform);
         else
            nc.Value.PointA.Transform(xform);         
         return nc;
      }

      public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
      {
         GH_Spring nc = this.DuplicateGeometry() as GH_Spring;
         if (nc.Value.IsACurve)
            xmorph.Morph(nc.Value.CurveA);
         else
            xmorph.Morph(nc.Value.PointA);         
         return nc;
      }

      public override string ToString()
      {
         return "Spring" + (GroupId == 0 ? "" : ", Grp Id: " + GroupId) + ", AxStf: " + Axial_stiffness + ", RotStf: " + Rotational_stiffness + ", TransStf: " + Transversal_stiffness + (Direction.IsTiny() ? "" : ", Dir: " + Direction);
      }
   }

   public class CreateSpring : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public CreateSpring()
            : base("Spring", "Spring", "Creates SOFiSTiK Point / Line Spring", "SOFiSTiK", "Structure")
      { }

      public override Guid ComponentGuid
      {
         get
         {
            return new Guid("BCCBA1EE-801E-432F-BC2C-DFDD65671A13");
         }
      }
      
      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "structural_spring_24x24.png");
            return _icon;
         }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGeometryParameter("Point / Curve", "Pt/Crv", "Structural Element (Point/Line) or Geometry Point / Curve", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Group", "Group", "Group number of this spring", GH_ParamAccess.list, 0);
         pManager.AddNumberParameter("Axial Stiffness", "Ax. Stf.", "Stiffness of this spring in axial direction [kN/m^3]", GH_ParamAccess.list, 0.0);
         pManager.AddNumberParameter("Rotational Stiffness", "Rot. Stf", "Stiffness of this spring in rotational direction [kNm/rad]", GH_ParamAccess.list, 0.0);
         pManager.AddNumberParameter("Transversal Stiffness", "Tr. Stf", "Stiffness of this spring in transversal direction [kN/m^3]", GH_ParamAccess.list, 0.0);
         pManager.AddVectorParameter("Direction", "Dir", "Explicit Direction of this spring", GH_ParamAccess.list, new Vector3d(0,0,1));
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Spring", "Spr", "SOFiSTiK Spring", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {

         List<IGH_GeometricGoo> a_list = da.GetDataList<IGH_GeometricGoo>(0);
         List<int> groups = da.GetDataList<int>(1);
         List<double> axial_stiffness = da.GetDataList<double>(2);
         List<double> rotational_stiffness = da.GetDataList<double>(3);
         List<double> transversal_stiffness = da.GetDataList<double>(4);
         List<Vector3d> direction = da.GetDataList<Vector3d>(5);

         List<GH_Spring> out_list = new List<GH_Spring>();
         
         for (int i = 0; i < a_list.Count; i++)
         {
            IGH_GeometricGoo a_goo = a_list[i];

            GH_Spring spr = new GH_Spring();
            spr.Value = new GH_CouplingStruc();
            spr.GroupId = groups.GetItemOrLast(i);
            spr.Axial_stiffness = axial_stiffness.GetItemOrLast(i);
            spr.Rotational_stiffness = rotational_stiffness.GetItemOrLast(i);
            spr.Transversal_stiffness = transversal_stiffness.GetItemOrLast(i);
            spr.Direction = direction.GetItemOrLast(i);
            
            Enum state = spr.Value.SetInput(a_goo, true);
            if (state.Equals(GH_CouplingStruc.State.OK))
               out_list.Add(spr);
            else
               this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input param: only (structural)points/lines allowed");
         }

         da.SetDataList(0, out_list);
      }
   }
}
