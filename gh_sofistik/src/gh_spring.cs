using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gh_sofistik.src
{

   public class GH_Spring : GH_GeometricGoo<GH_CouplingStruc>, IGH_PreviewData
   {
      private CouplingCondition _springCond = new CouplingCondition();

      public int GroupId { get; set; } = 0;

      public double Axial_stiffness { get; set; } = 0.0;

      public double Rotational_stiffness { get; set; } = 0.0;

      public Vector3d Direction { get; set; } = new Vector3d();

      public BoundingBox ClippingBox
      {
         get
         {
            BoundingBox bBox = new BoundingBox();
            if (Value.IsACurve)
               bBox.Union(Value.CurveA.GetBoundingBox(false));
            else
               bBox.Union(Value.PointA.GetBoundingBox(false));
            if (Value.IsBCurve)
               bBox.Union(Value.CurveB.GetBoundingBox(false));
            else
               bBox.Union(Value.PointB.GetBoundingBox(false));
            return bBox;
         }
      }

      public override BoundingBox Boundingbox
      {
         get
         {
            BoundingBox bBox = new BoundingBox();
            if (Value.IsACurve)
               bBox.Union(Value.CurveA.GetBoundingBox(true));
            else
               bBox.Union(Value.PointA.GetBoundingBox(true));
            if (Value.IsBCurve)
               bBox.Union(Value.CurveB.GetBoundingBox(true));
            else
               bBox.Union(Value.PointB.GetBoundingBox(true));
            return bBox;
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
            return "Spring between Points and Lines";
         }
      }

      public void DrawViewportMeshes(GH_PreviewMeshArgs args)
      {

      }

      public void DrawViewportWires(GH_PreviewWireArgs args)
      {
         //ClippingBox
         //args.Pipeline.DrawBox(ClippingBox, System.Drawing.Color.Black);
         if (!_springCond.isValid)
         {
            updateSprings();
         }

         System.Drawing.Color col = args.Color;
         if (!DrawUtil.CheckSelection(col))
            col = System.Drawing.Color.DarkViolet;
         else
            _springCond.DrawInfo(args.Pipeline, (GroupId == 0 ? "" : "Grp Id: " + GroupId + "\n") + "Ax. Stf: " + Axial_stiffness + "\n" + "Rot. Stf: " + Rotational_stiffness + (Direction.IsTiny() ? "" : "\nDir: " + Direction));

         /*
         if (Value.IsACurve)
            args.Pipeline.DrawCurve(Value.CurveA, col, args.Thickness + 1);
         else
            args.Pipeline.DrawPoint(Value.PointA.Location, Rhino.Display.PointStyle.X, 5, col);
         if (Value.IsBCurve)
            args.Pipeline.DrawCurve(Value.CurveB, col, args.Thickness + 1);
         else
            args.Pipeline.DrawPoint(Value.PointB.Location, Rhino.Display.PointStyle.X, 5, col);         
         */

         _springCond.Draw(args.Pipeline, col);
      }

      private void updateSprings()
      {
         _springCond = new CouplingCondition();
         _springCond.CreateCouplingSymbols(Value.GetConnectionLines());
         //_springCond.createSpringSymbols(Value.GetConnectionLines(), axial_stiffness, rotational_stiffness);
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         GH_Spring nc = new GH_Spring();
         nc.Value = new GH_CouplingStruc();
         if (!(Value.Reference_A is null))
            nc.Value.Reference_A = Value.Reference_A;
         if (!(Value.Reference_B is null))
            nc.Value.Reference_B = Value.Reference_B;

         if (Value.IsACurve)
            nc.Value.SetA(Value.CurveA.DuplicateCurve());
         else
            nc.Value.SetA(new Point(Value.PointA.Location));
         if (Value.IsBCurve)
            nc.Value.SetB(Value.CurveB.DuplicateCurve());
         else
            nc.Value.SetB(new Point(Value.PointB.Location));

         nc.GroupId = GroupId;
         nc.Axial_stiffness = Axial_stiffness;
         nc.Rotational_stiffness = Rotational_stiffness;
         nc.Direction = Direction;
         return nc;
      }

      public override BoundingBox GetBoundingBox(Transform xform)
      {
         return xform.TransformBoundingBox(ClippingBox);
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         GH_Spring nc = this.DuplicateGeometry() as GH_Spring;
         if (nc.Value.IsACurve)
            nc.Value.CurveA.Transform(xform);
         else
            nc.Value.PointA.Transform(xform);
         if (nc.Value.IsBCurve)
            nc.Value.CurveB.Transform(xform);
         else
            nc.Value.PointB.Transform(xform);
         return nc;
      }

      public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
      {
         GH_Spring nc = this.DuplicateGeometry() as GH_Spring;
         if (nc.Value.IsACurve)
            xmorph.Morph(nc.Value.CurveA);
         else
            xmorph.Morph(nc.Value.PointA);
         if (nc.Value.IsBCurve)
            xmorph.Morph(nc.Value.CurveB);
         else
            xmorph.Morph(nc.Value.PointB);
         return nc;
      }

      public override string ToString()
      {
         return "Spring" + (GroupId == 0 ? "" : ", Grp Id: " + GroupId) + ", AxStf: " + Axial_stiffness + ", RotStf: " + Rotational_stiffness + (Direction.IsTiny() ? "" : ", Dir: " + Direction);
      }
   }

   public class CreateSpring : GH_Component
   {
      public CreateSpring()
            : base("Spring", "Spring", "Creates SOFiSTiK Point/Point, Point/Line or Line/Line Spring", "SOFiSTiK", "Structure")
      { }

      public override Guid ComponentGuid
      {
         get
         {
            return new Guid("C6F702F1-E3E2-47AC-880D-7210465E7530");
         }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGeometryParameter("A: Point/Curve", "A", "Root geometry (Point / Curve) of this spring", GH_ParamAccess.list);
         pManager.AddGeometryParameter("B: Reference Point/Curve", "B", "Reference geometry (Point / Curve) of this spring", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Group", "Group", "Group number of this spring", GH_ParamAccess.list, 0);
         pManager.AddNumberParameter("Axial Stiffness", "Ax. Stf.", "Stiffness of this spring in axial direction [kN/m^3]", GH_ParamAccess.list, 0.0);
         pManager.AddNumberParameter("Rotational Stiffness", "Rot. Stf", "Stiffness of this spring in rotational direction [kNm/rad]", GH_ParamAccess.list, 0.0);
         pManager.AddVectorParameter("Explicit Spring Direction", "Dir", "Explicit Direction of this Spring. If no direction is given, the spring is aligned towards its reference point (default)", GH_ParamAccess.list, new Vector3d());
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Spring", "spr", "SOFiSTiK Point/Point Point/Line Line/Line Spring", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {

         List<IGH_GeometricGoo> a_list = da.GetDataList<IGH_GeometricGoo>(0);
         List<IGH_GeometricGoo> b_list = da.GetDataList<IGH_GeometricGoo>(1);
         List<int> groups = da.GetDataList<int>(2);
         List<double> axial_stiffness = da.GetDataList<double>(3);
         List<double> rotational_stiffness = da.GetDataList<double>(4);
         List<Vector3d> alignment = da.GetDataList<Vector3d>(5);

         List<GH_Spring> out_list = new List<GH_Spring>();

         int count = Math.Min(a_list.Count, b_list.Count);

         for (int i = 0; i < count; i++)
         {
            IGH_GeometricGoo a_goo = a_list[i];
            IGH_GeometricGoo b_goo = b_list[i];
            
            GH_Spring spr = new GH_Spring();
            spr.Value = new GH_CouplingStruc();
            spr.GroupId = groups.GetItemOrLast(i);
            spr.Axial_stiffness = axial_stiffness.GetItemOrLast(i);
            spr.Rotational_stiffness = rotational_stiffness.GetItemOrLast(i);
            spr.Direction = alignment.GetItemOrLast(i);
            
            Enum state = spr.Value.SetInputs(a_goo, b_goo);
            if (state.Equals(GH_CouplingStruc.State.OK))
               out_list.Add(spr);
            else if (state.Equals(GH_CouplingStruc.State.InvalidA))
               this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input param A: only (structural)points/lines allowed");
            else if (state.Equals(GH_CouplingStruc.State.InvalidB))
               this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input param B: only (structural)points/lines allowed");
            else if (state.Equals(GH_CouplingStruc.State.Modified))
            {
               this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameters swapped, only Springs from Lines to Points are supported");
               out_list.Add(spr);
            }
         }

         da.SetDataList(0, out_list);
      }
   }
}
