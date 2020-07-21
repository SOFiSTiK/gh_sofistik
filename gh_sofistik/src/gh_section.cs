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

   public enum SectionPointType
   {
      StressPoint,
      PolygonPoint,
      ReinforcementPoint,
   }

   public enum SectionLoopType
   {
      Outer = 1,
      Inner = 2,
   }

   public enum EdgeTransitionType
   {
      Fillet,
      Chamfer,
   }

   public class SectionPoint
   {
      public string Id { get; set; } = "0";
      public SectionPointType Type { get; set; }
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
            Type = Type,
            Y = Y,
            Z = Z,
            EType = EType,
            EdgeTransitionValue1 = EdgeTransitionValue1,
            EdgeTransitionValue2 = EdgeTransitionValue2,
         };
      }
      public Point3d ToPoint3d()
      {
         return new Point3d(0, Y, Z);
      }
   }

   public class SectionLoop : List<int>, IComparable
   {
      public string Id { get; set; } = "";
      public SectionLoopType Type { get; set; } = SectionLoopType.Outer;
      public int MaterialId { get; set; } = 0;
      public int ConstructionStage { get; set; } = 0;
      public SectionLoop Duplicate()
      {
         var res = new SectionLoop()
         {
            Id = Id,
            Type = Type,
            MaterialId = MaterialId,
            ConstructionStage = ConstructionStage,
         };
         foreach(var i in this)
            res.Add(i);
         return res;
      }
      public int CompareTo(object obj)
      {
         if (!(obj is SectionLoop)) return -1;
         var otherLoop = obj as SectionLoop;
         return TypeComparison.Invoke(this, otherLoop);
      }
      public static Comparison<SectionLoop> TypeComparison = delegate (SectionLoop object1, SectionLoop object2)
      {
         return object1.Type.CompareTo(object2.Type);
      };
      public static Comparison<SectionLoop> StageComparison = delegate (SectionLoop object1, SectionLoop object2)
      {
         return object1.ConstructionStage.CompareTo(object2.ConstructionStage);
      };
   }

   public class SectionFace
   {
      public string Id { get; set; } = "0";
      public int OuterLoopIdx { get; set; } = -1;
      public List<int> InnerLoopIdxList { get; } = new List<int>();
      public SectionFace Duplicate()
      {
         var res = new SectionFace()
         {
            Id = Id,
            OuterLoopIdx = OuterLoopIdx,
         };
         foreach (var iLoopIdx in InnerLoopIdxList)
            res.InnerLoopIdxList.Add(iLoopIdx);
         return res;
      }
   }

   public class Section
   {
      public string Name { get; set; }
      public int Id { get; set; }
      public int MaterialId { get; set; }
      public List<SectionPoint> Points { get; }
      public List<SectionLoop> Loops { get; }
      public List<SectionFace> Faces { get; }
      public Units.Unit_Length Unit { get; set; }
      public string SofistikProfile { get; set; }
      public Point2d SofistikProfileReference { get; set; }
      public List<string> UserText { get; set; }

      public Section()
      {
         Name = "new section";
         Id = 0;
         MaterialId = 0;
         Points = new List<SectionPoint>();
         Loops = new List<SectionLoop>();
         Faces = new List<SectionFace>();
         Unit = Units.Unit_Length.None;
         SofistikProfile = null;
         SofistikProfileReference = Point2d.Origin;
         UserText = new List<string>();
      }

      public virtual Section Duplicate()
      {
         var res = new Section()
         {
            Id = Id,
            Name = Name,
            MaterialId = MaterialId,
            Unit = Unit,
            SofistikProfile = SofistikProfile,
            SofistikProfileReference = SofistikProfileReference,
         };
         foreach (var pt in Points)
            res.Points.Add(pt.Duplicate());
         foreach (var lp in Loops)
            res.Loops.Add(lp.Duplicate());
         foreach (var fc in Faces)
            res.Faces.Add(fc.Duplicate());
         foreach (var ut in UserText)
            res.UserText.Add(ut);
         return res;
      }

      public override string ToString()
      {
         string unit = Units.UnitHelper.MapToString(Unit);
         string res = "Name: " + Name + "\nId: " + Id + "\nUnit: " + (string.IsNullOrEmpty(unit) ? "RhinoUnit" : unit) + "\nPoints: " + Points.Count + "\nLoops: " + Loops.Count + "\nFaces: " + Faces.Count;
         return res;
      }

      public virtual (GH_RuntimeMessageLevel, string) SetReferencePoint(string referencePtId)
      {
         if (!string.IsNullOrEmpty(referencePtId))
         {
            var referencePt = Points.Find(pt => pt.Id == referencePtId);

            if (referencePt == null)
               return (GH_RuntimeMessageLevel.Warning, "Point " + referencePtId + " not available in section");

            var my = referencePt.Y;
            var mz = referencePt.Z;

            foreach (var pt in Points)
            {
               pt.Y -= my;
               pt.Z -= mz;
            }

            if (!string.IsNullOrEmpty(SofistikProfile))
            {
               SofistikProfileReference = new Point2d(my + SofistikProfileReference.X, mz + SofistikProfileReference.Y);
            }
         }
         return (GH_RuntimeMessageLevel.Blank, "");
      }

      public virtual void Evaluate()
      {

      }

      public virtual void SetVariable(string name, double value)
      {

      }

      public virtual List<(string, string, string)> GetVariableNames()
      {
         return new List<(string, string, string)>();
      }

      public virtual List<Curve> Get3DReinforcements()
      {
         return new List<Curve>();
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

      protected PolyCurve GetPolyCurve(SectionLoop loop)
      {
         var curve = new PolyCurve();
         if (loop.Count < 2)
            return curve;

         var lines = new List<Line>();
         var fillets = new Dictionary<int, NurbsCurve>();
         var chamfers = new Dictionary<int, Line>();

         // create lines
         Point3d p1 = Points[loop[0]].ToPoint3d();
         for (int i = 1; i < loop.Count; ++i)
         {
            Point3d p2 = Points[loop[i]].ToPoint3d();

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

      protected void getUnitScalingInformation(out bool scalingRequired, out double scalingFactor, out Transform scalingTransform)
      {
         scalingRequired = false;
         scalingFactor = 1.0;
         scalingTransform = Transform.Identity;
         var sectionUnitsRhino = Units.UnitHelper.MapToRhinoUnits(Unit);
         if (Unit != Units.Unit_Length.None && sectionUnitsRhino != Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem)
         {
            scalingRequired = true;
            scalingFactor = Rhino.RhinoMath.UnitScale(sectionUnitsRhino, Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem);
            scalingTransform = Transform.Scale(Point3d.Origin, scalingFactor);
         }
      }

      public List<List<PolyCurve>> Get3DBounds()
      {
         var res = new List<List<PolyCurve>>();

         getUnitScalingInformation(out var scalingRequired, out var scalingFactor, out var scalingTransform);

         foreach (var face in Faces)
         {
            var faceBounds = new List<PolyCurve>();

            var outerBound = GetPolyCurve(Loops[face.OuterLoopIdx]);
            if (outerBound.SpanCount > 0)
               faceBounds.Add(outerBound);

            foreach(var innerLoopIdx in face.InnerLoopIdxList)
            {
               var innerBound = GetPolyCurve(Loops[innerLoopIdx]);
               if (innerBound.SpanCount > 0)
                  faceBounds.Add(innerBound);
            }

            if (scalingRequired)
               foreach (var bound in faceBounds)
                  bound.Transform(scalingTransform);

            res.Add(faceBounds);
         }

         return res;
      }

      public Dictionary<string, Point3d> Get3DPoints()
      {
         var points3d = new Dictionary<string, Point3d>();

         getUnitScalingInformation(out var scalingRequired, out var scalingFactor, out var scalingTransform);

         foreach (var pt in Points)
         {
            if (!points3d.ContainsKey(pt.Id))
            {
               var pt3d = pt.ToPoint3d();
               if (scalingRequired)
                  pt3d *= scalingFactor;
               points3d.Add(pt.Id, pt3d);
            }
         }

         return points3d;
      }

      public virtual int GetUnitSet()
      {
         return 0;
      }

      public virtual (GH_RuntimeMessageLevel, string) GetSectionDefinition(StringBuilder sb)
      {
         // calc unit conversion factor
         var unitFactor = 1.0;
         if (Unit != Units.Unit_Length.None)
            unitFactor = Rhino.RhinoMath.UnitScale(Units.UnitHelper.MapToRhinoUnits(Unit), Rhino.UnitSystem.Meters);
         else
            unitFactor = Rhino.RhinoMath.UnitScale(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem, Rhino.UnitSystem.Meters);

         if (!string.IsNullOrEmpty(SofistikProfile))
         {
            sb.Append("PROF " + Id);
            if (MaterialId != 0)
               sb.Append(" MNO " + MaterialId);

            sb.Append(" TYPE");
            var typeArray = SofistikProfile.Trim().Split(':');
            foreach (var typeString in typeArray)
               sb.Append(" " + typeString.Trim());

            sb.Append(" YM " + (-1 * SofistikProfileReference.X * unitFactor) + " ZM " + (-1 * SofistikProfileReference.Y * unitFactor));

            foreach (var userText in UserText)
               sb.Append(" " + userText);

            sb.AppendLine();
         }
         else
         {
            // write section
            sb.Append("SECT " + Id);
            if (MaterialId != 0)
               sb.Append(" MNO " + MaterialId);
            sb.Append(" TITL '" + Name + "'");
            foreach (var userText in UserText)
               sb.Append(" " + userText);
            sb.AppendLine();

            sb.AppendLine();

            foreach (var pt in Points)
            {
               if (pt.Type == SectionPointType.StressPoint)
               {
                  appendPointDefinition(sb, pt, unitFactor);
               }
            }

            sb.AppendLine();

            var lpList = new List<SectionLoop>(Loops);
            lpList.Sort(SectionLoop.StageComparison);
            foreach (var lp in lpList)
            {
               appendLoopDefinition(sb, lp, unitFactor);
               sb.AppendLine();
            }
         }

         sb.AppendLine();

         return (GH_RuntimeMessageLevel.Blank, "");
      }

      private void appendLoopDefinition(StringBuilder sb, SectionLoop lp, double uFac)
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
               appendPointDefinition(sb, Points[index], uFac);
         }
      }

      private static void appendPointDefinition(StringBuilder sb, SectionPoint pt, double uFac)
      {
         if (pt != null)
         {
            if (pt.Type == SectionPointType.StressPoint)
            {
               sb.Append("SPT " + pt.Id + " " + pt.Y * uFac + " " + pt.Z * uFac + " MNO 0");
            }
            else
            {
               sb.Append("VERT " + pt.Id + " " + pt.Y * uFac + " " + pt.Z * uFac);
               if (pt.EType == EdgeTransitionType.Fillet && pt.EdgeTransitionValue1 > 1.0E-8)
               {
                  sb.Append(" R " + pt.EdgeTransitionValue1 * uFac + " TYPE TP");
               }
               if (pt.EType == EdgeTransitionType.Chamfer && pt.EdgeTransitionValue1 > 1.0E-8)
               {
                  sb.Append(" R " + (-1.0 * pt.EdgeTransitionValue1 * uFac) + " TYPE TP");
               }
            }
            sb.AppendLine();
         }
      }

   }

   public class SectionAttributes
   {
      public int? Id { get; set; }
      public string Name { get; set; }
      public int? MaterialId { get; set; }
      public string ReferencePoint { get; set; }
      public List<string> UserText { get; set; } = new List<string>();
      public List<SectionAttributesLoop> LoopAttributes { get; set; } = new List<SectionAttributesLoop>();

      public SectionAttributes Duplicate()
      {
         var res = new SectionAttributes()
         {
            Id = Id,
            Name = Name,
            MaterialId = MaterialId,            
            ReferencePoint = ReferencePoint,
         };
         foreach (var ut in UserText)
            res.UserText.Add(ut);
         foreach (var lpAttr in LoopAttributes)
            res.LoopAttributes.Add(lpAttr.Duplicate());
         return res;
      }

      public override string ToString()
      {
         var res = "";
         bool hasEntry = false;
         if (Id.HasValue)
         {
            res += "Id: " + Id;
            hasEntry = true;
         }
         if (!string.IsNullOrEmpty(Name))
         {
            if (hasEntry)
               res += ", ";
            res += "Name: " + Name;
            hasEntry = true;
         }
         if (MaterialId.HasValue)
         {
            if (hasEntry)
               res += ", ";
            res += "Material: " + MaterialId;
            hasEntry = true;
         }
         if (!string.IsNullOrEmpty(ReferencePoint))
         {
            if (hasEntry)
               res += ", ";
            res += "ReferencePoint: " + ReferencePoint;
            hasEntry = true;
         }

         return res;
      }

      public List<(GH_RuntimeMessageLevel, string)> SetAttributes(Section section)
      {
         var res = new List<(GH_RuntimeMessageLevel, string)>();

         if (Id != null)
            section.Id = Id.Value;
         if (!string.IsNullOrEmpty(Name))
            section.Name = Name;
         if (MaterialId != null)
            section.MaterialId = MaterialId.Value;

         var setRefPointRes = section.SetReferencePoint(ReferencePoint);
         if (setRefPointRes.Item1 != GH_RuntimeMessageLevel.Blank)
            res.Add(setRefPointRes);

         if (UserText.Any())
         {
            section.UserText.Clear();
            foreach (var ut in UserText)
               section.UserText.Add(ut);
         }

         foreach(var lpAttr in LoopAttributes)
            res.AddRange(lpAttr.SetAttributes(section));

         return res;
      }
   }

   public class SectionAttributesLoop
   {
      public string Id { get; set; }
      public int? MaterialId { get; set; }
      public int? ConstructionStage { get; set; }

      public SectionAttributesLoop Duplicate()
      {
         var res = new SectionAttributesLoop()
         {
            Id = Id,
            MaterialId = MaterialId,
            ConstructionStage = ConstructionStage,
         };
         return res;
      }

      public override string ToString()
      {
         var res = "";
         res += "Loop Id: " + Id;
         if (MaterialId.HasValue)
            res += ", Material: " + MaterialId;
         if (ConstructionStage.HasValue)
            res += ", ConstructionStage: " + ConstructionStage;
         return res;
      }

      public List<(GH_RuntimeMessageLevel, string)> SetAttributes(Section section)
      {
         var res = new List<(GH_RuntimeMessageLevel, string)>();

         SectionLoop loop = null;

         if (section.Loops.Count == 0)
         {
            res.Add((GH_RuntimeMessageLevel.Warning, "Section has no loops"));
         }
         else if (section.Loops.Count == 1)
         {
            loop = section.Loops.First();
         }
         else
         {
            if (string.IsNullOrWhiteSpace(Id))
            {
               res.Add((GH_RuntimeMessageLevel.Warning, "Section has more than one loop, but no loop id is specified for LoopAttribute: Please specify Id for LoopAttribute"));
            }
            else
            {
               var loops = section.Loops.Where(e => e.Id.ToLower() == Id.ToLower());
               if (loops.Any())
               {
                  loop = loops.First();
               }
               else
               {
                  res.Add((GH_RuntimeMessageLevel.Warning, "Loop with id: " + Id + " is not available"));
               }
            }
         }

         if (loop != null)
         {
            if (MaterialId != null)
               loop.MaterialId = MaterialId.Value;
            if (ConstructionStage != null)
               loop.ConstructionStage = ConstructionStage.Value;
         }

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
         else if (typeof(Q).IsAssignableFrom(typeof(GH_Number)))
         {
            var id = new GH_Number(Value.Id);
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
         return new GH_SectionAttributes() { Value = Value.Duplicate() };
      }

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   public class GH_SectionAttributesLoop : GH_Goo<SectionAttributesLoop>
   {
      public override bool IsValid => Value != null;

      public override string TypeName => "GH_SectionAttributesLoop";

      public override string TypeDescription => "GH_SectionAttributesLoop";

      public override IGH_Goo Duplicate()
      {
         return new GH_SectionAttributesLoop() { Value = Value.Duplicate() };
      }

      public override string ToString()
      {
         return Value.ToString();
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
         pManager.AddIntegerParameter("Section Id", "Id", "Id of Section", GH_ParamAccess.item);
         pManager.AddTextParameter("Section Name", "Name", "Name of Section", GH_ParamAccess.item);
         pManager.AddIntegerParameter("Material", "Material", "Material Number", GH_ParamAccess.item);
         pManager.AddTextParameter("Reference Point", "RefPt", "Id of section point to use as root point", GH_ParamAccess.item);
         pManager.AddGenericParameter("Loop Attributes", "LpAttr", "SOFiSTiK Loop Attributes for Cross Sections", GH_ParamAccess.list);
         pManager.AddTextParameter("User Text", "User Text", "Additional text input being placed in the SECT header", GH_ParamAccess.list);

         pManager[0].Optional = true;
         pManager[1].Optional = true;
         pManager[2].Optional = true;
         pManager[3].Optional = true;
         pManager[4].Optional = true;
         pManager[5].Optional = true;
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGenericParameter("Section Attributes", "SecAttr", "SOFiSTiK Cross Section Attributes", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var id = DA.GetData<GH_Integer>(0);
         var name = DA.GetData<string>(1);
         var materialId = DA.GetData<GH_Integer>(2);
         var referencePt = DA.GetData<string>(3);
         var loopAttrList = DA.GetDataList<GH_SectionAttributesLoop>(4);
         var userTextList = DA.GetDataList<string>(5);

         var secAt = new SectionAttributes();
         if (id != null)
            secAt.Id = id.Value;
         if (!string.IsNullOrWhiteSpace(name))
            secAt.Name = name;
         if (materialId != null)
            secAt.MaterialId = materialId.Value;
         if (!string.IsNullOrWhiteSpace(referencePt))
            secAt.ReferencePoint = referencePt;
         foreach (var lpAttr in loopAttrList)
            secAt.LoopAttributes.Add(lpAttr.Value);
         foreach (var userText in userTextList)
            if (!string.IsNullOrWhiteSpace(userText))
               secAt.UserText.Add(userText);

         DA.SetData(0, new GH_SectionAttributes() { Value = secAt });
      }
   }

   public class GH_SectionAttributesLoop_Component : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public GH_SectionAttributesLoop_Component()
        : base("Loop Attributes", "LpAttr", "Defines SOFiSTiK Loop Attibutes for Cross Sections", "SOFiSTiK", "Section")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "section_attributes_loop_24x24.png");
            return _icon;
         }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("4F24AF2D-7B16-4A4E-A77D-7955BBF7EDA4"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddTextParameter("Loop Id", "Lp Id", "Id of Section Loop", GH_ParamAccess.item);
         pManager.AddIntegerParameter("Material Number", "Material", "Material Number", GH_ParamAccess.item);
         pManager.AddIntegerParameter("Construction Stage", "Stage", "Construction Stage", GH_ParamAccess.item);

         pManager[0].Optional = true;
         pManager[1].Optional = true;
         pManager[2].Optional = true;
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGenericParameter("Loop Attributes", "LpAttr", "SOFiSTiK Loop Attributes for Cross Sections", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var id = DA.GetData<string>(0);
         var materialId = DA.GetData<GH_Integer>(1);
         var constructionStageId = DA.GetData<GH_Integer>(2);

         var secAt = new SectionAttributesLoop();
         if (!string.IsNullOrWhiteSpace(id))
            secAt.Id = id;
         if (materialId != null)
            secAt.MaterialId = materialId.Value;
         if (constructionStageId != null)
            secAt.ConstructionStage = constructionStageId.Value;

         DA.SetData(0, new GH_SectionAttributesLoop() { Value = secAt });
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
         pManager.AddBrepParameter("Planar Brep", "Brp", "Planar Brep specifying the boundaries of the Cross Section", GH_ParamAccess.item);
         pManager.AddPointParameter("Additional Points", "Pts", "Additional section points for the Cross Section", GH_ParamAccess.list);
         pManager.AddGeometryParameter("Plane/Point", "Pln/Pt", "Construction Plane for this Section or point of origin", GH_ParamAccess.item);
         pManager.AddGenericParameter("Section Attributes", "SecAttr", "SOFiSTiK Cross Section Attributes", GH_ParamAccess.item);

         pManager[1].Optional = true;
         pManager[2].Optional = true;
         pManager[3].Optional = true;
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGenericParameter("Section", "Sec", "SOFiSTiK Cross Section", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var brp = DA.GetData<Brep>(0);
         var ptsList = DA.GetDataList<Point3d>(1);
         var refGeo = DA.GetData<IGH_Goo>(2);
         var secAttrGH = DA.GetData<GH_SectionAttributes>(3);

         GH_Section outSec = null;
         
         var pln = Plane.Unset;

         if (refGeo != null)
         {
            if (refGeo is GH_Plane)
            {
               pln = (refGeo as GH_Plane).Value;
            }
            else if (refGeo is GH_Point)
            {
               pln = calcPlane(brp);
               if (pln != Plane.Unset)
               {
                  pln.Origin = (refGeo as GH_Point).Value;
               }
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input geometry " + refGeo.TypeName + " not supported. Please specify Plane or Point.");
            }
         }

         if (pln == Plane.Unset)
         {
            pln = calcPlane(brp);
         }

         if (pln == Plane.Unset)
         {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not create reference plane.");
         }
         else
         {
            var sec = CreateSection(brp, ptsList, pln);

            var secAttr = secAttrGH == null ? new SectionAttributes() : secAttrGH.Value;
            var secAttrToAssign = new SectionAttributes()
            {
               Id = secAttr.Id == null ? 1 : secAttr.Id.Value,
               Name = string.IsNullOrEmpty(secAttr.Name) ? "New Section" : secAttr.Name,
               MaterialId = secAttr.MaterialId == null ? 1 : secAttr.MaterialId.Value,
               ReferencePoint = secAttr.ReferencePoint,
            };
            foreach (var lpAttr in secAttr.LoopAttributes)
               secAttrToAssign.LoopAttributes.Add(lpAttr.Duplicate());
            foreach (var ut in secAttr.UserText)
               secAttrToAssign.UserText.Add(ut);

            foreach (var setSecAttrRes in secAttrToAssign.SetAttributes(sec))
               AddRuntimeMessage(setSecAttrRes.Item1, setSecAttrRes.Item2);

            sec.Unit = Units.Unit_Length.None;

            outSec = new GH_Section() { Value = sec };
         }

         DA.SetData(0, outSec);
      }

      private Plane calcPlane(Brep brp)
      {
         var brpPts = new List<Point3d>();
         foreach (var v in brp.Vertices)
            brpPts.Add(v.Location);

         if (Plane.FitPlaneToPoints(brpPts, out var fitPln) == PlaneFitResult.Success)
         {
            if (fitPln.ZAxis * Vector3d.ZAxis < 1.0E-8)
            {
               if (Math.Abs(fitPln.ZAxis * Vector3d.ZAxis) < 1.0E-8)
               {
                  if (fitPln.ZAxis * Vector3d.XAxis < 1.0E-8)
                  {
                     fitPln.Flip();
                  }
               }
               else
               {
                  fitPln.Flip();
               }
            }

            var ly = Vector3d.CrossProduct(fitPln.ZAxis, Vector3d.XAxis);
            if (ly.IsTiny())
            {
               ly = Vector3d.CrossProduct(fitPln.ZAxis, Vector3d.YAxis);
            }
            ly.Unitize();
            var lx = Vector3d.CrossProduct(ly, fitPln.ZAxis);
            lx.Unitize();
            fitPln = new Plane(fitPln.Origin, lx, ly);

            var amPropOrg = AreaMassProperties.Compute(brp);

            fitPln.Origin = amPropOrg.Centroid;

            var tx = Transform.ChangeBasis(Plane.WorldXY, fitPln);

            var brpDup = brp.DuplicateBrep();

            brpDup.Transform(tx);

            var amProp = AreaMassProperties.Compute(brpDup);

            var iXX = amProp.WorldCoordinatesMomentsOfInertia.X;
            var iYY = amProp.WorldCoordinatesMomentsOfInertia.Y;
            var iXY = amProp.WorldCoordinatesProductMoments.X;

            double phi = 0.5 * 0.5 * Math.PI;
            if (Math.Abs(iXX - iYY) > 1.0E-8)
            {
               phi = 0.5 * Math.Atan((2 * iXY) / (iYY - iXX));
            }

            double iEta = (0.5 * (iXX + iYY)) + (0.5 * (iXX - iYY) * Math.Cos(2 * phi)) - (iXY * Math.Sin(2 * phi));
            double iZeta = (0.5 * (iXX + iYY)) - (0.5 * (iXX - iYY) * Math.Cos(2 * phi)) + (iXY * Math.Sin(2 * phi));

            if (iZeta > iEta)
            {
               phi += 0.5 * Math.PI;
            }

            fitPln.Rotate(phi, fitPln.ZAxis);

            if (fitPln.YAxis * Vector3d.ZAxis < 0.0)
            {
               fitPln.Rotate(Math.PI, fitPln.ZAxis);
            }

            return fitPln;
         }

         return Plane.Unset;
      }

      public static Section CreateSection(Brep brp, List<Point3d> ptsList, Plane pln)
      {
         var sec = new Section();
         string loopPtId = "P101";
         string stressPtId = "S101";

         var tx = Transform.ChangeBasis(Plane.WorldYZ, Util.SofiSectionBasePlane) * Transform.ChangeBasis(Plane.WorldXY, pln);

         int pointIndex = 0;
         int loopIndex = 0;
         int faceIndex = 0;
         foreach (var fc in brp.Faces)
         {
            var secFace = new SectionFace()
            {
               Id = (faceIndex++).ToString(),
            };
            foreach (var lp in fc.Loops)
            {
               var crv = lp.To3dCurve();

               crv.Transform(tx);

               var polyLine = Util.CreatePolyLine(crv);
               if (polyLine != null)
               {
                  var secLp = new SectionLoop()
                  {
                     Id = loopIndex.ToString(),
                     Type = lp.LoopType == BrepLoopType.Outer ? SectionLoopType.Outer : SectionLoopType.Inner,
                  };

                  int firstIndex = -1;
                  int i = 0;
                  foreach (var pt in polyLine)
                  {
                     if (i < polyLine.Count - 1)
                     {
                        var secPt = new SectionPoint()
                        {
                           Id = loopPtId,
                           Type = SectionPointType.PolygonPoint,
                           EType = EdgeTransitionType.Fillet,
                           EdgeTransitionValue1 = 0.0,
                           EdgeTransitionValue2 = 0.0,
                           Y = pt.Y,
                           Z = pt.Z,
                        };
                        loopPtId = Util.CountStringUp(loopPtId);

                        if (i++ == 0)
                           firstIndex = pointIndex;

                        sec.Points.Add(secPt);
                        secLp.Add(pointIndex);
                        pointIndex++;
                     }
                  }
                  if (firstIndex > -1)
                     secLp.Add(firstIndex);

                  sec.Loops.Add(secLp);

                  if (secLp.Type == SectionLoopType.Outer)
                     secFace.OuterLoopIdx = loopIndex;
                  else
                     secFace.InnerLoopIdxList.Add(loopIndex);

                  loopIndex++;
               }
            }
            sec.Faces.Add(secFace);
         }

         foreach(var pt in ptsList)
         {
            pt.Transform(tx);
            var secPt = new SectionPoint()
            {
               Id = stressPtId,
               Type = SectionPointType.StressPoint,
               EType = EdgeTransitionType.Fillet,
               EdgeTransitionValue1 = 0.0,
               EdgeTransitionValue2 = 0.0,
               Y = pt.Y,
               Z = pt.Z,
            };
            stressPtId = Util.CountStringUp(stressPtId);
            sec.Points.Add(secPt);
         }

         return sec;
      }

   }

   public class GH_ViewSection_Component : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      private static readonly int InfoMarginLeft = 15;
      private static readonly int InfoMarginTop = 42;
      private static readonly int InfoLineLength = 20;
      private static readonly int InfoLineThickness = 3;
      private static readonly double InfoTextHeight = 0.9;
      private static readonly double InfoTextPadding = 0.25;
      private static readonly System.Drawing.Color ReinforcementColor = System.Drawing.Color.Red;
      private static readonly double BlobDistance = 50;
      private static readonly int BlobMaxCount = 5;

      private List<string> _sectionInfo = new List<string>();
      private List<List<(Point3d, string)>> _pointsInfo = new List<List<(Point3d, string)>>();
      private List<List<(Curve, string, System.Drawing.Color)>> _loopsInfo = new List<List<(Curve, string, System.Drawing.Color)>>();
      private List<List<Brep>> _reinforcementsInfo = new List<List<Brep>>();
      private Rhino.Display.DisplayMaterial _ReinforcementMaterial
      {
         get
         {
            if (_reinforcementMaterial == null)
            {
               _reinforcementMaterial = new Rhino.Display.DisplayMaterial();
               _reinforcementMaterial.Shine = 0.2;
               _reinforcementMaterial.Transparency = 0.2;
               DrawUtil.SetMaterialColors(_reinforcementMaterial, ReinforcementColor);
            }
            return _reinforcementMaterial;
         }
      }
      private Rhino.Display.DisplayMaterial _reinforcementMaterial;

      public GH_ViewSection_Component()
         : base("View Section", "View Section", "View and place a SOFiSTiK Section", "SOFiSTiK", "Section")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "section_view_24x24.png");
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
         var textHeight = (int)(GH_FontServer.StandardAdjusted.Height * InfoTextHeight);
         var infoGapY = (int)(textHeight * (1 + InfoTextPadding));
         if (this.Attributes.Selected)
         {
            int idxLine = 0;
            for (int i = 0; i < _sectionInfo.Count; i++)
            {
               args.Display.Draw2dText(_sectionInfo[i], System.Drawing.Color.Black, new Point2d(InfoMarginLeft, InfoMarginTop - 0.5 * textHeight + infoGapY * idxLine), false, textHeight);
               idxLine++;
               foreach (var loopInfo in _loopsInfo[i])
               {
                  args.Display.DrawCurve(loopInfo.Item1, loopInfo.Item3, InfoLineThickness);
                  args.Display.Draw2dLine(new System.Drawing.PointF(InfoMarginLeft, InfoMarginTop + infoGapY * idxLine), new System.Drawing.PointF(InfoMarginLeft + InfoLineLength, InfoMarginTop + infoGapY * idxLine), loopInfo.Item3, InfoLineThickness);
                  args.Display.Draw2dText(loopInfo.Item2, System.Drawing.Color.Black, new Point2d(InfoMarginLeft + InfoLineLength + 10, InfoMarginTop - 0.5 * textHeight + infoGapY * idxLine), false, textHeight);
                  idxLine++;
               }
            }
            foreach (var reinforcementList in _reinforcementsInfo)
            {
               foreach (var reinforcement in reinforcementList)
               {
                  args.Display.DrawCurve(reinforcement.Curves3D.First(), System.Drawing.Color.Black);
               }
            }
            foreach (var pointList in _pointsInfo)
            {
               var blobList = new List<(Point2d, List<Point2d>, List<string>)>();
               foreach (var pointInfo in pointList)
               {
                  if (args.Viewport.IsVisible(pointInfo.Item1))
                  {
                     var pt2d = args.Viewport.WorldToClient(pointInfo.Item1);
                     int idxBlob = -1;
                     for (int i = 0; i < blobList.Count; i++)
                     {
                        if (blobList[i].Item1.DistanceTo(pt2d) < BlobDistance)
                        {
                           idxBlob = i;
                           break;
                        }
                     }
                     if (idxBlob == -1)
                     {
                        blobList.Add((pt2d, new List<Point2d>() { pt2d }, new List<string>() { pointInfo.Item2 }));
                     }
                     else
                     {
                        var blob = blobList[idxBlob];
                        var bpList = blob.Item2;
                        var bsList = blob.Item3;
                        bpList.Add(pt2d);
                        var pCent = bpList.First();
                        for (int i = 1; i < bpList.Count; i++)
                           pCent += bpList[i];
                        pCent *= 1.0 / bpList.Count;
                        bsList.Add(pointInfo.Item2);
                        blobList[idxBlob] = (pCent, bpList, bsList);
                     }
                  }
               }
               foreach (var blob in blobList)
               {
                  var text = blob.Item3.First();
                  for (int i = 1; i < blob.Item3.Count && i < BlobMaxCount; i++)
                     text += "\n" + blob.Item3[i];
                  if (blob.Item3.Count > BlobMaxCount)
                     text += "\n...";
                  args.Display.DrawDot((float)blob.Item1.X + 25, (float)blob.Item1.Y, text, DrawUtil.DrawColorInfoDotBack, DrawUtil.DrawColorInfoDotText);
               }
               foreach (var pointInfo in pointList)
               {
                  // args.Display.Draw2dText(text, System.Drawing.Color.DarkRed, ptTuple.Item1, false, 20);
                  //var pt2d = args.Viewport.WorldToClient(pointInfo.Item1);
                  //pt2d.X += 25;
                  //args.Display.DrawDot((float)pt2d.X, (float)pt2d.Y, pointInfo.Item2, DrawUtil.DrawColorInfoDotBack, DrawUtil.DrawColorInfoDotText);
                  args.Display.DrawPoint(pointInfo.Item1);
               }
            }
         }
      }

      public override void DrawViewportMeshes(IGH_PreviewArgs args)
      {
         base.DrawViewportMeshes(args);
         if (this.Attributes.Selected)
         {
            foreach (var reinforcementList in _reinforcementsInfo)
            {
               foreach (var reinforcement in reinforcementList)
               {
                  args.Display.DrawBrepShaded(reinforcement, _ReinforcementMaterial);
               }
            }
         }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGenericParameter("Section", "Sec", "SOFiSTiK Section", GH_ParamAccess.item);
         pManager.AddPlaneParameter("Reference Plane", "Pln", "Reference Plane for placing and displaying Section", GH_ParamAccess.item, Plane.WorldYZ);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddBrepParameter("Brep", "Brp", "Geometry of Section as Planar Brep", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var ghSec = DA.GetData<GH_Section>(0);
         var ghPln = DA.GetData<GH_Plane>(1);

         var outListBrp = new List<GH_Brep>();

         double tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

         if (RunCount == 1)
         {
            _sectionInfo.Clear();
            _pointsInfo.Clear();
            _loopsInfo.Clear();
            _reinforcementsInfo.Clear();
         }

         var sec = ghSec?.Value?.Duplicate();
         var pln = Plane.Unset;
         if (ghPln != null)
            pln = ghPln.Value;
         if (sec != null && pln!=Plane.Unset)
         {
            Transform tx = Transform.PlaneToPlane(Plane.WorldYZ, pln) * Transform.ChangeBasis(Plane.WorldXY, Util.SofiSectionBasePlane);

            var secGeo = sec.Get3DBounds();
            foreach (var face in secGeo)
            {
               var brps = Brep.CreatePlanarBreps(face, tolerance);
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
            }

            // prepare section for visualization
            _sectionInfo.Add("Id: " + sec.Id.ToString() + " - Name: " + sec.Name + " - Unit: " + sec.Unit.ToString() + " - Material: " + sec.MaterialId.ToString() + " - Faces: " + sec.Faces.Count.ToString() + " - Loops: " + sec.Loops.Count.ToString() + " - Points: " + sec.Points.Count.ToString());

            // prepare points for visualization
            var ptInfoList = new List<(Point3d, string)>();
            foreach (var kvp in sec.Get3DPoints())
            {
               var p = kvp.Value;
               p.Transform(tx);
               ptInfoList.Add((p, kvp.Key));
            }
            _pointsInfo.Add(ptInfoList);

            // prepare loops for visualization
            var lpList = new List<(Curve, string, System.Drawing.Color)>();
            var lps3d = sec.Get3DBounds();
            var dPhi = 360 / sec.Loops.Count;
            var phi = 255;
            for (int idxFace = 0; idxFace < sec.Faces.Count; idxFace++)
            {
               var secFace = sec.Faces[idxFace];
               var secFace3d = lps3d[idxFace];
               for (int idxLoop = 0; idxLoop < secFace3d.Count; idxLoop++)
               {
                  var secLoop = sec.Loops[idxLoop == 0 ? secFace.OuterLoopIdx : secFace.InnerLoopIdxList[idxLoop - 1]];
                  var secLoop3d = secFace3d[idxLoop];
                  secLoop3d.Transform(tx);
                  var text = "Id: " + secLoop.Id + (secLoop.MaterialId != 0 ? " - Material: " + secLoop.MaterialId.ToString() : "") + (secLoop.ConstructionStage != 0 ? " - ConstructionStage: " + secLoop.ConstructionStage.ToString() : "");
                  lpList.Add((secLoop3d, text, DrawUtil.HsvToColor(phi, 1, 1)));
                  phi += dPhi;
               }
            }
            _loopsInfo.Add(lpList);

            // prepare reinforcements for visualization
            var reinforcementList = new List<Brep>();
            foreach (var circ in sec.Get3DReinforcements())
            {
               var breps = Brep.CreatePlanarBreps(circ, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
               if (breps.Any())
               {
                  var brep = breps.First();
                  brep.Transform(tx);
                  reinforcementList.Add(brep);
               }
            }
            _reinforcementsInfo.Add(reinforcementList);
         }

         DA.SetDataList(0, outListBrp);
      }

   }

   #endregion

}