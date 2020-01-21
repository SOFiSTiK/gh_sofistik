using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gh_sofistik.Section
{

   #region base types

   public enum SectionLoopType
   {
      Inner,
      Outer,
   }

   public enum EdgeTransitionType
   {
      Fillet,
      Chamfer,
   }

   public class SectionPoint
   {
      public string Id { get; set; } = "0";
      public double Y { get; set; } = 0.0;
      public double Z { get; set; } = 0.0;
      public EdgeTransitionType EType { get; set; } = EdgeTransitionType.Fillet;
      public double EdgeTransitionValue1 { get; set; } = 0.0;
      public double EdgeTransitionValue2 { get; set; } = 0.0;
      public virtual SectionPoint Duplicate()
      {
         return new SectionPoint()
         {
            Id = Id,
            Y = Y,
            Z = Z,
            EType=EType,
            EdgeTransitionValue1 = EdgeTransitionValue1,
            EdgeTransitionValue2 = EdgeTransitionValue2,
         };
      }
   }

   public class SectionLoop : List<int>
   {
      public SectionLoopType Type { get; set; } = SectionLoopType.Outer;
      public int MaterialId { get; set; } = 0;
      public int ConstructionStage { get; set; } = 0;
      public SectionLoop Duplicate()
      {
         var res = new SectionLoop()
         {
            Type = Type,
            MaterialId = MaterialId,
            ConstructionStage = ConstructionStage,
         };
         foreach(var i in this)
            res.Add(i);
         return res;
      }
   }

   public class Section
   {
      public string Name { get; set; }
      public int Id { get; set; }
      public int MaterialId { get; set; }
      public double FactorVariableToMeter { get; set; }
      public List<SectionPoint> Points { get; }
      public List<SectionLoop> Loops { get; }
      public Units.Unit_Length Unit { get; set; }

      public Section()
      {
         Name = "new section";
         Id = 0;
         MaterialId = 0;
         FactorVariableToMeter = 1.0;
         Points = new List<SectionPoint>();
         Loops = new List<SectionLoop>();
         Unit = Units.Unit_Length.None;
      }

      public virtual Section Duplicate()
      {
         var res = new Section();
         res.Name = Name;
         res.Id = 0;
         res.FactorVariableToMeter = FactorVariableToMeter;
         res.Unit = Unit;
         foreach (var pt in Points)
            res.Points.Add(pt.Duplicate());
         foreach (var lp in Loops)
            res.Loops.Add(lp.Duplicate());
         return res;
      }

      public override string ToString()
      {
         return "Name: " + Name + ", Id: " + Id + ", Pts: " + Points.Count + ", Lps: " + Loops.Count;
      }

      public virtual void Evaluate()
      {

      }

      public virtual void SetVariable(string name, double value)
      {

      }

      public virtual List<string> GetVariables()
      {
         return new List<string>();
      }
      /*
      private static Arc CreateTrimFillet(double R, ref Line l1, ref Line l2)
      {
         if (Math.Abs(R) < 1.0E-6)
            return Arc.Unset;

         Vector3d d1 = l1.Direction; d1.Unitize();
         Vector3d d2 = l2.Direction; d2.Unitize();

         double b1 = Math.Atan2(d1.Z, d1.Y);
         double b2 = Math.Atan2(d2.Z, d2.Y);
         if (b2 < b1) b2 += 2.0 * Math.PI;

         double alph = b2 - b1;
         if (alph > Math.PI - 1.0E-3) alph = 2.0 * Math.PI - alph;

         if (alph < 1.0E-3)
            return Arc.Unset;

         // create arc
         double lp = Math.Abs(R) * Math.Tan(0.5 * alph);

         if (lp < 0.75 * l1.Length && lp < 0.75 * l2.Length) // fits into segments
         {
            Point3d p1 = l1.To - lp * d1;
            Point3d p2 = l1.To + lp * d2;

            Arc arc = new Arc(p1, d1, p2);

            l1.To = p1;
            l2.From = p2;

            return arc;
         }

         return Arc.Unset;
      }
      */
      private static Arc CreateTrimFillet(double R, ref Line l1, ref Line l2)
      {
         if (Math.Abs(R) < 1.0E-6)
            return Arc.Unset;

         Vector3d d1 = -l1.Direction; d1.Unitize();
         Vector3d d2 = l2.Direction; d2.Unitize();

         var alpha = Math.Acos(d1 * d2);

         if (alpha < 1.0E-3)
            return Arc.Unset;

         // create arc
         double lp = Math.Abs(R) / Math.Tan(0.5 * alpha);

         if (lp < l1.Length - 1.0E-6 && lp < l2.Length - 1.0E-6) // fits into segments
         {
            Point3d p1 = l1.To + lp * d1;
            Point3d p2 = l1.To + lp * d2;

            Arc arc = new Arc(p1, -d1, p2);

            l1.To = p1;
            l2.From = p2;

            return arc;
         }

         return Arc.Unset;
      }

      private static Line CreateTrimChamfer(double v1, double v2, ref Line l1, ref Line l2)
      {
         double v1a = Math.Abs(v1);
         double v2a = Math.Abs(v2);

         if (v1a < 1.0E-6 || v2a < 1.0E-6)
            return Line.Unset;

         Vector3d d1 = -l1.Direction; d1.Unitize();
         Vector3d d2 = l2.Direction; d2.Unitize();

         if (v1a < l1.Length - 1.0E-6 && v2a < l2.Length - 1.0E-6) // fits into segments
         {
            Point3d p1 = l1.To + v1a * d1;
            Point3d p2 = l1.To + v2a * d2;

            l1.To = p1;
            l2.From = p2;

            return new Line(p1, p2);
         }

         return Line.Unset;
      }

      private PolyCurve GetPolyCurve(SectionLoop loop)
      {
         var curve = new PolyCurve();
         if (loop.Count < 2)
            return curve;

         var lines = new List<Line>();
         var fillets = new Dictionary<int, NurbsCurve>();
         var chamfers = new Dictionary<int, Line>();

         // create lines
         Point3d p1 = new Point3d(0.0, Points[loop[0]].Y, Points[loop[0]].Z);
         for (int i = 1; i < loop.Count; ++i)
         {
            Point3d p2 = new Point3d(0.0, Points[loop[i]].Y, Points[loop[i]].Z);

            lines.Add(new Line(p1, p2));
            p1 = p2;
         }

         // create Edge Transitions
         for (int i = 0; i < lines.Count; ++i)
         {
            var pt = Points[loop[i]];

            // create EdgeTransition
            if ((pt.EType == EdgeTransitionType.Fillet && pt.EdgeTransitionValue1 > 1.0E-6) || (pt.EType == EdgeTransitionType.Chamfer && pt.EdgeTransitionValue1 > 1.0E-6 && pt.EdgeTransitionValue2 > 1.0E-6))
            {
               var l1 = i == 0 ? lines.Last() : lines[i - 1];
               var l2 = lines[i];
               if (l1.Length > 1.0E-6 && l2.Length > 1.0E-6)
               {
                  bool writeBackLines = false;
                  if (pt.EType == EdgeTransitionType.Fillet)
                  {
                     var arc = CreateTrimFillet(pt.EdgeTransitionValue1, ref l1, ref l2);
                     if (arc.Radius > 1.0E-3)
                     {
                        fillets.Add(i, arc.ToNurbsCurve());
                        writeBackLines = true;
                     }
                  }
                  if (pt.EType == EdgeTransitionType.Chamfer)
                  {
                     var chamfer = CreateTrimChamfer(pt.EdgeTransitionValue1, pt.EdgeTransitionValue2, ref l1, ref l2);
                     if (chamfer.Length > 1.0E-6)
                     {
                        chamfers.Add(i, chamfer);
                        writeBackLines = true;
                     }
                  }
                  if (writeBackLines)
                  {
                     lines[i] = l2;
                     if (i == 0)
                        lines[lines.Count - 1] = l1;
                     else
                        lines[i - 1] = l1;
                  }
               }
            }
         }

         // create polycurve
         for (int i = 0; i < lines.Count; ++i)
         {
            if (fillets.TryGetValue(i, out var fillet))
               curve.Append(fillet);
            if (chamfers.TryGetValue(i, out var chamfer))
               curve.Append(chamfer);
            if (i < lines.Count - 1 && lines[i].Length > 1.0E-6)
               curve.Append(lines[i]);
         }
         if (curve.PointAtStart.DistanceTo(curve.PointAtEnd) > 1.0E-6)
            curve.Append(new Line(curve.PointAtEnd, curve.PointAtStart));

         return curve;
      }

      public List<PolyCurve> GetBounds()
      {
         var bounds = new List<PolyCurve>();

         bool scaleToGlobalUnits = false;
         Transform tU = Transform.Identity;
         var sectionUnitsRhino = Units.UnitHelper.MapToRhinoUnits(Unit);
         if (Unit != Units.Unit_Length.None && sectionUnitsRhino != Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem)
         {
            scaleToGlobalUnits = true;
            var sFac = Rhino.RhinoMath.UnitScale(sectionUnitsRhino, Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem);
            tU = Transform.Scale(Point3d.Origin, sFac);
         }

         foreach (var loop in Loops)
         {
            var bound = GetPolyCurve(loop);
            if (bound.SpanCount > 0)
            {
               if (scaleToGlobalUnits)
                  bound.Transform(tU);
               bounds.Add(bound);
            }
         }

         return bounds;
      }

      public Dictionary<string, Point3d> GetPoints()
      {
         var points3d = new Dictionary<string, Point3d>();

         bool scaleToGlobalUnits = false;
         var sFac = 1.0;
         var sectionUnitsRhino = Units.UnitHelper.MapToRhinoUnits(Unit);
         if (Unit != Units.Unit_Length.None && sectionUnitsRhino != Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem)
         {
            scaleToGlobalUnits = true;
            sFac = Rhino.RhinoMath.UnitScale(sectionUnitsRhino, Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem);
         }

         foreach (var pt in Points)
         {
            if (!points3d.ContainsKey(pt.Id))
            {
               var pt3d = new Point3d(0.0, pt.Y, pt.Z);
               if (scaleToGlobalUnits)
                  pt3d *= sFac;
               points3d.Add(pt.Id, pt3d);
            }
         }

         return points3d;
      }

      public virtual (GH_RuntimeMessageLevel, string) GetSectionDefinition(StringBuilder sb)
      {
         // calc unit conversion factor
         var unitFactor = 1.0;
         if (Unit != Units.Unit_Length.None)
            unitFactor = Rhino.RhinoMath.UnitScale(Units.UnitHelper.MapToRhinoUnits(Unit), Rhino.UnitSystem.Meters);
         else
            unitFactor = Rhino.RhinoMath.UnitScale(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem, Rhino.UnitSystem.Meters);

         // write section
         sb.Append("SECT " + Id);
         if (MaterialId != 0)
            sb.Append(" MNO " + MaterialId);
         sb.Append(" TITL '" + Name + "'");
         sb.AppendLine();

         sb.AppendLine();

         foreach (var lp in Loops)
         {
            if (lp.ConstructionStage != 0)
               sb.AppendLine("CS " + lp.ConstructionStage);

            sb.Append("POLY TYPE O");
            if (lp.Type == SectionLoopType.Outer)
            {
               if (lp.MaterialId != 0)
                  sb.Append(" MNO " + lp.MaterialId);
            }
            else
            {
               sb.Append(" MNO 0");
            }
            sb.AppendLine();

            foreach (var index in lp)
            {
               if (index > -1 && index < Points.Count)
                  appendPointDefinition(sb, Points[index], unitFactor);
            }
            sb.AppendLine();
         }

         sb.AppendLine();

         return (GH_RuntimeMessageLevel.Blank, "");
      }

      private static void appendPointDefinition(StringBuilder sb, SectionPoint pt, double uFac)
      {
         if (pt != null)
            sb.AppendLine("VERT " + pt.Id + " " + pt.Y * uFac + " " + pt.Z * uFac);
      }

   }

   public class SectionAttributes
   {
      public int Id { get; set; } = 0;
      public string Name { get; set; } = "";
      public int MaterialId { get; set; } = 0;

      public SectionAttributes Duplicate()
      {
         var res = new SectionAttributes()
         {
            Id = Id,
            Name = Name,
            MaterialId = MaterialId,
         };
         return res;
      }
   }

   #endregion

   #region gh types

   public class GH_Section : GH_Goo<Section>
   {
      public override bool IsValid => Value != null;

      public override string TypeName => "GH_Section";

      public override string TypeDescription => "GH_Section";


      public override IGH_Goo Duplicate()
      {
         var res = new GH_Section()
         {
            Value = Value.Duplicate(),
         };
         return res;
      }

      public override string ToString()
      {
         return Value.ToString();
      }

      public override bool CastTo<Q>(ref Q target)
      {
         if (typeof(Q).IsAssignableFrom(typeof(GH_Integer)))
         {
            var id = new GH_Integer(Value.Id);
            target = (Q)(object)id;
            return true;
         }
         else
            return base.CastTo(ref target);
      }
   }

   public class GH_SectionAttributes : GH_Goo<SectionAttributes>
   {
      public override bool IsValid => Value != null;

      public override string TypeName => "GH_SectionAttributes";

      public override string TypeDescription => "GH_SectionAttributes";

      public override IGH_Goo Duplicate()
      {
         var res = new GH_SectionAttributes()
         {
            Value = Value.Duplicate(),
         };
         return res;
      }

      public override string ToString()
      {
         return "Id: " + Value.Id + ", Name: " + Value.Name + ", Material: " + Value.MaterialId;
      }
   }

   #endregion

   #region components

   public class GH_SectionAttributes_Component : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public GH_SectionAttributes_Component()
        : base("Section Attributes", "SecAttr", "Defines SOFiSTiK Cross Section Attibutes", "SOFiSTiK", "Section")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "section_attributes_24x24.png");
            return _icon;
         }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("F3D972F2-FD07-45F9-81D7-1575D1133E91"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddIntegerParameter("Section Id", "Id", "Id of Section", GH_ParamAccess.list, 1);
         pManager.AddTextParameter("Section Name", "Name", "Name of Section", GH_ParamAccess.list, "New Section");
         pManager.AddIntegerParameter("Material", "Material", "Material Number", GH_ParamAccess.list, 1);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGenericParameter("Section Attributes", "SecAttr", "SOFiSTiK Cross Section Attributes", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var idList = DA.GetDataList<int>(0);
         var nameList = DA.GetDataList<string>(1);
         var materialIdList = DA.GetDataList<int>(2);

         var outList = new List<GH_SectionAttributes>();

         var count = Math.Max(idList.Count, nameList.Count);
         count = Math.Max(count, materialIdList.Count);

         for(int i = 0; i < count; i++)
         {
            var secAt = new SectionAttributes()
            {
               Id = idList.GetItemOrCountUp(i),
               Name = nameList.GetItemOrLast(i),
               MaterialId = materialIdList.GetItemOrLast(i),
            };
            var ghSecAt = new GH_SectionAttributes()
            {
               Value = secAt,
            };
            outList.Add(ghSecAt);
         }

         DA.SetDataList(0, outList);
      }

   }

   public class GH_SectionFromBrep_Component : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public GH_SectionFromBrep_Component()
        : base("SectionFromBrep", "SectionFromBrep", "Creates a SOFiSTiK Cross Section from a Brep representation", "SOFiSTiK", "Section")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "section_brep_24x24.png");
            return _icon;
         }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("56026062-7705-4ACA-B840-5F5F974065BD"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddBrepParameter("Planar Brep", "Brp", "Planar Brep specifying the boundaries of the Cross Section", GH_ParamAccess.list);
         pManager.AddPlaneParameter("Plane", "Pln", "Construction Plane for this Section", GH_ParamAccess.list, Plane.WorldYZ);
         pManager.AddGenericParameter("Section Attributes", "SecAttr", "SOFiSTiK Cross Section Attributes", GH_ParamAccess.list);

         pManager[2].Optional = true;
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGenericParameter("Section", "Sec", "SOFiSTiK Cross Section", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var brpList = DA.GetDataList<Brep>(0);
         var plnList = DA.GetDataList<Plane>(1);
         var secAttrList = DA.GetDataList<GH_SectionAttributes>(2);

         if (secAttrList.Count == 0)
            secAttrList.Add(new GH_SectionAttributes() { Value = new SectionAttributes(), });

         var outList = new List<GH_Section>();

         for (int i = 0; i < brpList.Count; i++)
         {
            var brp = brpList.GetItemOrLast(i);
            var pln = plnList.GetItemOrLast(i);

            var sec = CreateSection(brp, pln);

            var secAttr = secAttrList.GetItemOrLast(i).Value;

            var id = secAttr.Id;
            if (i >= secAttrList.Count)
               id = id - (secAttrList.Count - 1) + i;

            sec.Id = id;
            sec.Name = secAttr.Name;
            sec.MaterialId = secAttr.MaterialId;
            sec.Unit = Units.Unit_Length.None;

            var ghSec = new GH_Section()
            {
               Value = sec,
            };

            outList.Add(ghSec);
         }

         DA.SetDataList(0, outList);
      }

      public static Section CreateSection(Brep brp, Plane pln)
      {
         var sec = new Section();
         string id = "P101";

         var tx = Transform.ChangeBasis(Plane.WorldYZ, Util.SofiSectionBasePlane) * Transform.ChangeBasis(Plane.WorldXY, pln);

         int pointIndex = 0;
         foreach (var lp in brp.Loops)
         {
            var crv = lp.To3dCurve();

            crv.Transform(tx);

            var polyLine = Util.CreatePolyLine(crv);
            if (polyLine != null)
            {
               var secLp = new SectionLoop();
               secLp.Type = lp.LoopType == BrepLoopType.Outer ? SectionLoopType.Outer : SectionLoopType.Inner;
               int firstIndex = -1;
               int i = 0;
               foreach (var pt in polyLine)
               {
                  if (i < polyLine.Count - 1)
                  {
                     var secPt = new SectionPoint()
                     {
                        Id = id,
                        EType = EdgeTransitionType.Fillet,
                        EdgeTransitionValue1 = 0.0,
                        EdgeTransitionValue2 = 0.0,
                        Y = pt.Y,
                        Z = pt.Z,
                     };
                     id = Util.CountStringUp(id);

                     if (i++ == 0)
                        firstIndex = pointIndex;
                     secLp.Add(pointIndex++);
                     sec.Points.Add(secPt);
                  }
               }
               if (firstIndex > -1)
                  secLp.Add(firstIndex);
               sec.Loops.Add(secLp);
            }
         }

         return sec;
      }

   }

   public class GH_PlaceSection_Component : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      private List<(Point3d, string)> _pts = new List<(Point3d, string)>();

      public GH_PlaceSection_Component()
         : base("Place Section", "Place Section", "View and place a SOFiSTiK Section", "SOFiSTiK", "Section")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "section_place_24x24.png");
            return _icon;
         }
      }

      public override Guid ComponentGuid
      {
         get => new Guid("8B0B87C1-D041-42C0-B2E6-8E66DBE14990");
      }

      public override void DrawViewportWires(IGH_PreviewArgs args)
      {
         base.DrawViewportWires(args);
         if (this.Attributes.Selected)
            foreach (var pt in _pts)
            {
               args.Display.Draw2dText(pt.Item2, System.Drawing.Color.DarkRed, pt.Item1, false, 20);
               args.Display.DrawPoint(pt.Item1);
            }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGenericParameter("Section", "Sec", "SOFiSTiK Section", GH_ParamAccess.list);
         pManager.AddPlaneParameter("Reference Plane", "Pln", "Reference Plane for placing and displaying Section", GH_ParamAccess.list, Plane.WorldYZ);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddBrepParameter("Brep", "Brp", "Geometry of Section as Planar Brep", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var secList = DA.GetDataList<GH_Section>(0);
         var plnList = DA.GetDataList<GH_Plane>(1);

         var outListBrp = new List<GH_Brep>();

         double tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

         _pts.Clear();

         for (int i = 0; i < secList.Count; i++)
         {
            var sec = secList.GetItemOrLast(i).Value.Duplicate();
            var pln = plnList.GetItemOrLast(i).Value;

            Transform tx = Transform.PlaneToPlane(Plane.WorldYZ, pln) * Transform.ChangeBasis(Plane.WorldXY, Util.SofiSectionBasePlane);

            var brps = Brep.CreatePlanarBreps(sec.GetBounds(), tolerance);
            if (brps == null || brps.Length == 0)
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not create Planar Brep");
            else
            {
               foreach (var brp in brps)
               {
                  brp.Transform(tx);
                  outListBrp.Add(new GH_Brep(brp));
               }
            }

            foreach (var kvp in sec.GetPoints())
            {
               var p = kvp.Value;
               p.Transform(tx);
               _pts.Add((p, kvp.Key));
            }

         }

         DA.SetDataList(0, outListBrp);
      }

   }

   #endregion

}