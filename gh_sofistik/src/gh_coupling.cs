using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino;
using Rhino.DocObjects;

namespace gh_sofistik.src
{
   public class GH_CouplingStruc
   {
      private Point _a_point;
      private Point _b_point;
      private Curve _a_curve;
      private Curve _b_curve;

      public bool IsACurve { get; set; } = false;
      public bool IsBCurve { get; set; } = false;

      public IGS_StructuralElement Reference_A { get; set; }

      public IGS_StructuralElement Reference_B { get; set; }

      public enum State { OK, InvalidA, InvalidB, Modified };

      public void SetA(Point p)
      {
         _a_point = p;
         IsACurve = false;
      }

      public void SetA(Curve c)
      {
         _a_curve = c;
         IsACurve = true;
      }

      public void SetB(Point p)
      {
         _b_point = p;
         IsBCurve = false;
      }

      public void SetB(Curve c)
      {
         _b_curve = c;
         IsBCurve = true;
      }

      public Point PointA
      {
         get { return _a_point; }
      }

      public Curve CurveA
      {
         get { return _a_curve; }
      }

      public Point PointB
      {
         get { return _b_point; }
      }

      public Curve CurveB
      {
         get { return _b_curve; }
      }

      public Enum SetInput(IGH_GeometricGoo gg, bool a)
      {
         Enum res = State.OK;
         bool isPoint = false;
         GS_StructuralPoint spt = null;
         GS_StructuralLine sln = null;
         if (gg is GH_Point)
         {
            spt = new GS_StructuralPoint();
            spt.Value = new Point((gg as GH_Point).Value);
            isPoint = true;
         }
         else if (gg is GS_StructuralPoint)
         {
            spt = gg as GS_StructuralPoint;
            isPoint = true;
         }
         else if (gg is GH_Curve)
         {
            sln = new GS_StructuralLine();
            sln.Value = (gg as GH_Curve).Value;
         }
         else if (gg is GH_Line)
         {
            sln = new GS_StructuralLine();
            sln.Value = new LineCurve((gg as GH_Line).Value);
         }
         else if (gg is GH_Arc)
         {
            sln = new GS_StructuralLine();
            sln.Value = new ArcCurve((gg as GH_Arc).Value);
         }
         else if (gg is GS_StructuralLine)
         {
            sln = gg as GS_StructuralLine;
         }
         //else
         //throw new InvalidCastException("Input param: only (structural)points/lines allowed");

         if ((spt is null) && (sln is null))
         {
            if (a)
               res = State.InvalidA;
            else
               res = State.InvalidB;
         }
         else
         {
            if (a)
            {
               if (isPoint)
               {
                  SetA(spt.Value);
                  Reference_A = spt;
               }
               else
               {
                  SetA(sln.Value);
                  Reference_A = sln;
               }
            }
            else
            {
               if (isPoint)
               {
                  SetB(spt.Value);
                  Reference_B = spt;
               }
               else
               {
                  SetB(sln.Value);
                  Reference_B = sln;
               }
            }
         }

         return res;
      }

      public Enum SetInputs(IGH_GeometricGoo a_goo, IGH_GeometricGoo b_goo)
      {
         Enum res = SetInput(a_goo, true);
         if (res.Equals(State.OK))
         {
            res = SetInput(b_goo, false);
            if (res.Equals(State.OK))
            {
               if (!IsACurve && IsBCurve)
               {
                  swapInputs();
                  res = State.Modified;
               }
            }
         }
         return res;
      }

      private void swapInputs()
      {
         Point pa_temp = PointA;
         IGS_StructuralElement pRef_temp = Reference_A;
         SetA(CurveB);
         Reference_A = Reference_B;
         SetB(pa_temp);
         Reference_B = pRef_temp;
      }

      public List<Point3d> GetSingleInputPoints()
      {
         List<Point3d> res = new List<Point3d>();
         if (IsACurve)
         {
            int wiresA = Math.Max(1, (int)(CurveA.GetLength(0.01) * DrawUtil.DensityFactorSupports / 2.0 + 0.99));
            for (int i = 0; i <= wiresA; ++i)
               res.Add(CurveA.PointAtNormalizedLength((double)i / (double)(wiresA)));
         }
         else
         {
            res.Add(PointA.Location);
         }
         return res;
      }

      public List<Line> GetConnectionLines()
      {
         List<Point3d> a_list = new List<Point3d>();
         List<Point3d> b_list = new List<Point3d>();

         int wiresA = 0;
         if (IsACurve) wiresA = Math.Max(1, (int)(CurveA.GetLength(0.01) * DrawUtil.DensityFactorSupports / 2.0 + 0.99));
         int wiresB = 0;
         if (IsBCurve) wiresB = Math.Max(1, (int)(CurveB.GetLength(0.01) * DrawUtil.DensityFactorSupports / 2.0 + 0.99));

         int wires = Math.Max(wiresA, wiresB);

         bool reverseOneCurve = false;
         if (IsACurve && IsBCurve)
         {
            Point3d startPointA = CurveA.PointAtStart;
            Point3d startPointB = CurveB.PointAtStart;
            Point3d endPointA = CurveA.PointAtEnd;
            Point3d endPointB = CurveB.PointAtEnd;
            if (startPointA.DistanceTo(endPointB) + endPointA.DistanceTo(startPointB) < startPointA.DistanceTo(startPointB) + endPointA.DistanceTo(endPointB))
               reverseOneCurve = true;
         }


         if (IsACurve)
         {
            for (int i = 0; i <= wires; ++i)
            {
               a_list.Add(CurveA.PointAtNormalizedLength((double)i / (double)(wires)));
            }
         }
         else
         {
            for (int i = 0; i <= wires; ++i)
               a_list.Add(PointA.Location);
         }

         if (IsBCurve)
         {
            if (reverseOneCurve)
               for (int i = 0; i <= wires; ++i)
               {
                  b_list.Add(CurveB.PointAtNormalizedLength(1 - ((double)i / (double)(wires))));
               }
            else
               for (int i = 0; i <= wires; ++i)
               {
                  b_list.Add(CurveB.PointAtNormalizedLength((double)i / (double)(wires)));
               }
         }
         else
         {
            for (int i = 0; i <= wires; ++i)
               b_list.Add(PointB.Location);
         }

         List<Line> res = new List<Line>();
         for (int i = 0; i <= wires; ++i)
            res.Add(new Line(a_list[i], b_list[i]));

         return res;
      }
   }

   public class GH_Coupling : GH_GeometricGoo<GH_CouplingStruc>, IGH_PreviewData
   {
      private CouplingCondition _cplCond = new CouplingCondition();

      public int GroupId { get; set; } = 0;

      public string FixLiteral { get; set; } = "";

      public BoundingBox ClippingBox
      {
         get
         {
            BoundingBox bBox;
            if (Value.IsACurve)
               bBox = Value.CurveA.GetBoundingBox(false);
            else
               bBox = Value.PointA.GetBoundingBox(false);
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
            BoundingBox bBox;
            if (Value.IsACurve)
               bBox = Value.CurveA.GetBoundingBox(true);
            else
               bBox = Value.PointA.GetBoundingBox(true);
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
            return "GH_Coupling";
         }
      }

      public override string TypeDescription
      {
         get
         {
            return "Coupling between Points and Lines";
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
            updateCouplings();
         }

         System.Drawing.Color col = args.Color;
         if (!DrawUtil.CheckSelection(col))
            col = System.Drawing.Color.Yellow;
         else
            _cplCond.DrawInfo(args);

         _cplCond.Draw(args.Pipeline, col);

         /*
         if(Value.IsACurve)
            args.Pipeline.DrawCurve(Value.CurveA, col, args.Thickness + 1);
         else
            args.Pipeline.DrawPoint(Value.PointA.Location, Rhino.Display.PointStyle.X, 5, col);
         if (Value.IsBCurve)
            args.Pipeline.DrawCurve(Value.CurveB, col, args.Thickness + 1);
         else
            args.Pipeline.DrawPoint(Value.PointB.Location, Rhino.Display.PointStyle.X, 5, col);
         */
      }

      private void updateCouplings()
      {
         _cplCond = new CouplingCondition();
         List<string> sl = new List<string>();
         if (!FixLiteral.Equals(""))
            sl.Add(FixLiteral);

         _cplCond.CreateDottedLineSymbols(Value.GetConnectionLines(), sl);
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         GH_Coupling nc = new GH_Coupling();
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
         nc.FixLiteral = FixLiteral;
         return nc;
      }

      public override BoundingBox GetBoundingBox(Transform xform)
      {
         return xform.TransformBoundingBox(ClippingBox);
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         GH_Coupling nc = this.DuplicateGeometry() as GH_Coupling;
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
         GH_Coupling nc = this.DuplicateGeometry() as GH_Coupling;
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
         return "Coupling" + (GroupId == 0 ? "" : ", Grp Id: " + GroupId) + ", Fix: " + FixLiteral;
      }
   }

   public class CreateCoupling : GH_Component
   {
      public CreateCoupling()
            : base("Coupling", "Coupling", "Creates SOFiSTiK Point/Point, Point/Line or Line/Line Coupling", "SOFiSTiK", "Structure")
      { }

      public override Guid ComponentGuid
      {
         get
         {
            return new Guid("A3ED35F3-9038-47B9-BB3F-2F2276FB014F");
         }
      }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_constraint_24x24; }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGeometryParameter("A: Point/Curve", "A", "Root geometry (Point / Curve) of this coupling", GH_ParamAccess.list);
         pManager.AddGeometryParameter("B: Reference Point/Curve", "B", "Reference geometry (Point / Curve) of this coupling", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Group", "Group", "Group number of this coupling", GH_ParamAccess.list, 0);
         pManager.AddTextParameter("Fixation", "Fix", "Coupling fixation literal", GH_ParamAccess.list, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Coupling", "Cpl", "SOFiSTiK Point/Point Point/Line Line/Line Coupling", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {

         List<IGH_GeometricGoo> a_list = da.GetDataList<IGH_GeometricGoo>(0);
         List<IGH_GeometricGoo> b_list = da.GetDataList<IGH_GeometricGoo>(1);
         List<int> groups = da.GetDataList<int>(2);
         List<string> fixations = da.GetDataList<string>(3);

         List<GH_Coupling> out_list = new List<GH_Coupling>();

         int count = Math.Min(a_list.Count, b_list.Count);

         for (int i = 0; i < count; i++)
         {
            IGH_GeometricGoo a_goo = a_list[i];
            IGH_GeometricGoo b_goo = b_list[i];

            GH_Coupling cpl = new GH_Coupling();
            cpl.Value = new GH_CouplingStruc();
            cpl.GroupId = groups.GetItemOrLast(i);
            cpl.FixLiteral = DrawUtil.ReconstructFixString(fixations.GetItemOrLast(i));

            Enum state = cpl.Value.SetInputs(a_goo, b_goo);
            if (state.Equals(GH_CouplingStruc.State.OK))
               out_list.Add(cpl);
            else if (state.Equals(GH_CouplingStruc.State.InvalidA))
               this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input param A: only (structural)points/lines allowed");
            else if (state.Equals(GH_CouplingStruc.State.InvalidB))
               this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input param B: only (structural)points/lines allowed");
            else if (state.Equals(GH_CouplingStruc.State.Modified))
            {
               this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameters swapped, only Couplings from Lines to Points are supported");
               out_list.Add(cpl);
            }
         }

         da.SetDataList(0, out_list);
      }
   }
}
