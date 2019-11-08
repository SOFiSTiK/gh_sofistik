

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gh_sofistik.Open.Section
{

   public enum SectionLoopType
   {
      Inner,
      Outer,
   }

   public class SectionPoint
   {
      public string Id { get; set; } = "0";
      public double Y { get; set; } = 0;
      public double Z { get; set; } = 0;
      public double R { get; set; } = 0;
      public virtual SectionPoint Duplicate()
      {
         return new SectionPoint()
         {
            Id = Id,
            Y = Y,
            Z = Z,
            R = R,
         };
      }
   }

   public class SectionLoop : List<int>
   {
      public SectionLoopType Type { get; set; } = SectionLoopType.Outer;
      public SectionLoop Duplicate()
      {
         var res = new SectionLoop();
         res.Type = Type;
         foreach(var i in this)
            res.Add(i);
         return res;
      }
   }

   public class Section
   {
      public string Name { get; set; }
      public int Id { get; set; }
      public double FactorVariableToMeter { get; set; }
      public List<SectionPoint> Points { get; }
      public List<SectionLoop> Loops { get; }

      public Section()
      {
         Name = "new section";
         Id = 0;
         FactorVariableToMeter = 1.0;
         Points = new List<SectionPoint>();
         Loops = new List<SectionLoop>();
      }

      public virtual Section Duplicate()
      {
         var res = new Section();
         res.Name = Name;
         res.Id = 0;
         res.FactorVariableToMeter = FactorVariableToMeter;
         foreach (var pt in Points)
            res.Points.Add(pt.Duplicate());
         foreach (var lp in Loops)
            res.Loops.Add(lp.Duplicate());
         return res;
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

      private static Arc CreateTrimFillet(double R, ref Line l1, ref Line l2)
      {
         if (Math.Abs(R) < 1.0E-6)
            return Arc.Unset;

         Vector3d d1 = l1.Direction; d1.Unitize();
         Vector3d d2 = l2.Direction; d2.Unitize();

         double b1 = Math.Atan2(d1.Y, d1.X);
         double b2 = Math.Atan2(d2.Y, d2.X);
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

      private PolyCurve GetPolyCurve(SectionLoop loop)
      {
         var curve = new PolyCurve();
         if (loop.Count < 2)
            return curve;

         // create lines
         var lines = new List<Line>();

         Point3d p1 = new Point3d(0.0, Points[loop[0]].Y, Points[loop[0]].Z);
         for (int i = 1; i < loop.Count; ++i)
         {
            Point3d p2 = new Point3d(0.0, Points[loop[i]].Y, Points[loop[i]].Z);

            lines.Add(new Line(p1, p2));
            p1 = p2;
         }

         // create polycurve
         for (int i = 0; i < lines.Count; ++i)
         {
            var l2 = lines[i];

            // create fillet
            if (Points[loop[i]].R > 0.0)
            {
               var l1 = i == 0 ? lines.Last() : lines[i - 1];

               var arc = CreateTrimFillet(Points[loop[i]].R, ref l1, ref l2);
               if (arc.Radius > 1.0E-3)
                  curve.Append(arc);

            }
            curve.Append(l2);
         }

         return curve;
      }

      public List<PolyCurve> GetBounds()
      {
         var bounds = new List<PolyCurve>();

         foreach (var loop in Loops)
         {
            var bound = GetPolyCurve(loop);
            if (bound.SpanCount > 0)
               bounds.Add(bound);
         }

         return bounds;
      }

      public virtual (GH_RuntimeMessageLevel, string) GetSectionDefinition(StringBuilder sb)
      {
         sb.AppendLine("SECT " + Id + " TITL '" + Name + "'");

         sb.AppendLine();

         foreach (var lp in Loops)
         {
            if (lp.Type == SectionLoopType.Outer)
               sb.AppendLine("POLY TYPE O");
            else
               sb.AppendLine("POLY TYPE O MNO 0");
            foreach (var index in lp)
            {
               if (index > -1 && index < Points.Count)
                  appendPointDefinition(sb, Points[index]);
            }
            sb.AppendLine();
         }

         sb.AppendLine();

         return (GH_RuntimeMessageLevel.Blank, "");
      }

      private static void appendPointDefinition(StringBuilder sb, SectionPoint pt)
      {
         if (pt != null)
            sb.AppendLine("VERT " + pt.Id + " " + pt.Y + " " + pt.Z);
      }

   }

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
         return "Name: " + Value.Name + ", Pts: " + Value.Points.Count + ", Lps: " + Value.Loops.Count;
      }
   }

   public class GH_Section_Component : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public GH_Section_Component()
        : base("Section", "Section", "Defines a SOFiSTiK Cross Section", "SOFiSTiK", "Section")
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
         pManager.AddPlaneParameter("Plane", "Pln", "Plane", GH_ParamAccess.list, Plane.WorldYZ);
         pManager.AddIntegerParameter("Section Id", "Id", "Id of this Section", GH_ParamAccess.list, 10);
         pManager.AddTextParameter("Section Name", "Name", "Name of this Section", GH_ParamAccess.list, "New Section");
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGenericParameter("Section", "Sec", "SOFiSTiK Cross Section", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var brpList = DA.GetDataList<Brep>(0);
         var plnList = DA.GetDataList<Plane>(1);
         var idList = DA.GetDataList<int>(2);
         var nameList = DA.GetDataList<string>(3);

         var outList = new List<GH_Section>();

         var name = "";
         for (int i = 0; i < brpList.Count; i++)
         {
            var brp = brpList.GetItemOrLast(i);
            var pln = plnList.GetItemOrLast(i);

            var sec = CreateSection(brp, pln);

            var id = idList.GetItemOrCountUp(i);
            if (i < nameList.Count)
               name = nameList.GetItemOrLast(i);
            else
               name = Util.CountStringUp(name);

            sec.Id = id;
            sec.Name = name;

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
               foreach (var pt in polyLine)
               {
                  var secPt = new SectionPoint()
                  {
                     Id = id,
                     R = 0,
                     Y = pt.Y,
                     Z = pt.Z,
                  };
                  id = Util.CountStringUp(id);

                  secLp.Add(pointIndex++);
                  sec.Points.Add(secPt);
               }
               sec.Loops.Add(secLp);
            }
         }

         return sec;
      }

   }

   public class GH_PlaceSection_Component : GH_Component, IGH_VariableParameterComponent
   {
      private System.Drawing.Bitmap _icon;
      private int _additionalParams = 0;
      private List<(Point3d, string)> _pts = new List<(Point3d, string)>();

      public GH_PlaceSection_Component()
         : base("Place Section", "Place Section", "View and Place a SOFiSTiK Section on a reference Plane", "SOFiSTiK", "Section")
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
         get => new Guid("637136BE-52DE-40D6-8849-42062AB53AD5");
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
         pManager[0].Optional = true;
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddBrepParameter("Brep", "Brp", "Geometry of Section as Planar Brep", GH_ParamAccess.list);
      }
      
      public override void AddedToDocument(GH_Document document)
      {
         document.SolutionStart += new GH_Document.SolutionStartEventHandler(checkInputHandler);
      }

      private void checkInputHandler(object sender, GH_SolutionEventArgs e)
      {
         var inputSections = getInputSections();
         var varNames = getAllVars(inputSections);
         var ok = checkInputSections(varNames);
         if (!ok)
         {
            renewInput(varNames);
            Params.OnParametersChanged();
            ExpireSolution(true);
         }
      }

      private List<GH_Section> getInputSections()
      {
         var secList = new List<GH_Section>();
         Params.Input[0].CollectData();
         foreach (var data in Params.Input[0].VolatileData.AllData(true))
            if (data is GH_Section)
               secList.Add(data as GH_Section);
         return secList;
      }

      private static List<string> getAllVars(List<GH_Section> inputSections)
      {
         var allVarNames = new List<string>();
         foreach (var sec in inputSections)
            foreach (var vName in sec.Value.GetVariables())
               if (!allVarNames.Contains(vName))
                  allVarNames.Add(vName);
         return allVarNames;
      }

      private bool checkInputSections(List<string> varNames)
      {
         if (_additionalParams != varNames.Count)
            return false;

         for (int i = 0; i < Params.Input.Count - 2; i++)
            if (Params.Input[i + 2].NickName != varNames[i])
               return false;

         return true;
      }

      private void renewInput(List<string> varNames)
      {
         removeAllAdditionalParamSavely();
         foreach (var varName in varNames)
         {
            var param = createParameter(varName);
            Params.RegisterInputParam(param);
            _additionalParams++;
         }
      }

      private void removeAllAdditionalParamSavely()
      {
         while (_additionalParams > 0)
         {
            var p = Params.Input.Last();
            Params.UnregisterInputParameter(p, true);
            _additionalParams--;
         }
      }

      private IGH_Param createParameter(string name)
      {
         var param = new Grasshopper.Kernel.Parameters.Param_Number();
         param.Name = "Variable: " + name;
         param.NickName = name;
         param.Description = "Axis Variable";
         param.Access = GH_ParamAccess.item;
         param.Optional = true;
         return param;
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var secList = DA.GetDataList<GH_Section>(0);
         var plnList = DA.GetDataList<GH_Plane>(1);

         var outListBrp = new List<GH_Brep>();

         double tolerance = 0.001;

         _pts.Clear();

         for (int i = 0; i < secList.Count; i++)
         {
            var sec = secList.GetItemOrLast(i).Value;
            var pln = plnList.GetItemOrLast(i).Value;

            Transform tx = Transform.PlaneToPlane(Plane.WorldYZ, pln) * Transform.ChangeBasis(Plane.WorldXY, Util.SofiSectionBasePlane);

            for (int j = 0; j < Params.Input.Count - 2; j++)
            {
               var v = DA.GetData<GH_Number>(2 + j);
               if (v != null)
                  sec.SetVariable(Params.Input[j + 2].NickName, v.Value);
            }
            sec.Evaluate();

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

            foreach (var pt in sec.Points)
            {
               var p3d = new Point3d(0, pt.Y, pt.Z);
               p3d.Transform(tx);
               _pts.Add((p3d, pt.Id));
            }

         }

         DA.SetDataList(0, outListBrp);
      }

      public bool CanInsertParameter(GH_ParameterSide side, int index)
      {
         return false;
      }

      public bool CanRemoveParameter(GH_ParameterSide side, int index)
      {
         return false;
      }

      public IGH_Param CreateParameter(GH_ParameterSide side, int index)
      {
         return null;
      }

      public bool DestroyParameter(GH_ParameterSide side, int index)
      {
         return true;
      }

      public void VariableParameterMaintenance()
      {
         _additionalParams = Params.Input.Count - 2;
      }
   }

}