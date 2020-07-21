using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace gh_sofistik.Structure
{
   // class implementing a GH_ container for Rhino.Geometry.Curve
   public class GS_StructuralLine : GH_GeometricGoo<Curve>, IGH_PreviewData, IGH_BakeAwareData, IGS_StructuralElement
   {
      public int Id { get; set; } = 0;
      public int GroupId { get; set; } = 0;    
      public int SectionIdStart { get; set; } = 0;
      public int SectionIdEnd { get; set; } = 0;
      public Vector3d DirectionLocalZ { get; set; } = new Vector3d();      
      private string fixLiteral = string.Empty;
      public string FixLiteral
      {
         get
         {
            return fixLiteral;
         }
         set
         {
            if (value is null)
               fixLiteral = "";
            else
               fixLiteral = value;
            _supp_condition = new SupportCondition(fixLiteral);
         }
      }      
      public string ElementType { get; set; }= "B";
      public double ElementSize { get; set; } = 0.0;
      public string UserText { get; set; } = string.Empty;

      private SupportCondition _supp_condition = null;
      private LocalFrameVisualisation _localFrame = new LocalFrameVisualisation();
      private InfoPanel _infoPanel;

      public override BoundingBox Boundingbox
      {
         get { return Value.GetBoundingBox(true); }
      }

      public override string TypeDescription
      {
         get { return Value.ToString() + " with Structural Properties"; }
      }

      public override string TypeName
      {
         get { return "GS_StructuralLine"; }
      }

      public override string ToString()
      {
         return "Structural Line, Id = " + Id.ToString();
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GS_StructuralLine()
         {
            Value = this.Value.DuplicateCurve(),
            Id = 0,
            GroupId = this.GroupId,
            SectionIdStart = this.SectionIdStart,
            SectionIdEnd = this.SectionIdEnd,
            DirectionLocalZ = this.DirectionLocalZ,
            FixLiteral = this.FixLiteral,
            ElementType = this.ElementType,
            ElementSize = this.ElementSize,
            UserText = this.UserText
         };
      }

      public override BoundingBox GetBoundingBox(Transform xform)
      {
         return xform.TransformBoundingBox(Value.GetBoundingBox(true));
      }

      public override bool CastTo<Q>(out Q target)
      {
         return Util.CastCurveTo(this.Value, out target);
      }

      public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
      {
         var dup = this.DuplicateGeometry() as GS_StructuralLine;
         xmorph.Morph(dup.Value);

         return dup;
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         var dup = this.DuplicateGeometry() as GS_StructuralLine;

         dup.Value.Transform(xform);

         var localVZ = dup.DirectionLocalZ;
         localVZ.Transform(xform);
         dup.DirectionLocalZ = localVZ;

         return dup;
      }

      public BoundingBox ClippingBox
      {
         get
         {
            if (fixLiteral.Equals(""))
               return DrawUtil.GetClippingBoxLocalframe(Value.GetBoundingBox(false));
            else
               return DrawUtil.GetClippingBoxSuppLocal(Value.GetBoundingBox(false));
         }
      }

      public void DrawViewportWires(GH_PreviewWireArgs args)
      {
         //ClippingBox
         //args.Pipeline.DrawBox(ClippingBox, System.Drawing.Color.Black);
         if (Value != null)
         {            
            System.Drawing.Color colStr = args.Color;
            System.Drawing.Color colSup = args.Color;
            if (!DrawUtil.CheckSelection(colStr))
            {
               colStr = DrawUtil.DrawColorStructuralElements;
               colSup = System.Drawing.Color.Black;
            }
            else
            {
               drawLocalFrame(args.Pipeline);
               drawInfoPanel(args.Pipeline, args.Viewport);
            }

            args.Pipeline.DrawCurve(Value, colStr, args.Thickness+1);

            drawSupportLine(args.Pipeline, colSup, false);
         }
      }

      public void DrawViewportMeshes(GH_PreviewMeshArgs args)
      {
         // no need to draw meshes
         if (Value != null)
         {
            drawSupportLine(args.Pipeline, DrawUtil.DrawColorSupports, true);
         }
      }

      private void drawInfoPanel(Rhino.Display.DisplayPipeline pipeline, Rhino.Display.RhinoViewport viewport)
      {
         if (DrawUtil.DrawInfo)
         {
            if (_infoPanel == null)
            {
               _infoPanel = new InfoPanel();
               _infoPanel.Positions.Add(Value.PointAtNormalizedLength(0.5));
               if (Id != 0)
                  _infoPanel.Content.Add("Id: " + Id);
               if (GroupId != 0)
                  _infoPanel.Content.Add("Grp: " + GroupId);
               if (SectionIdStart != 0)
                  _infoPanel.Content.Add("Sec: " + SectionIdStart + (SectionIdEnd == 0 || SectionIdStart == SectionIdEnd ? "" : "." + SectionIdEnd));
            }
            _infoPanel.Draw(pipeline, viewport);
         }
      }

      private void drawLocalFrame(Rhino.Display.DisplayPipeline pipeline)
      {
         if (DrawUtil.ScaleFactorLocalFrame > 0.0001)
         {
            if (!_localFrame.IsValid)
            {
               updateLocalFrameTransforms();
            }
            _localFrame.Draw(pipeline);
         }
      }

      private void updateLocalFrameTransforms()
      {
         _localFrame.Transforms.Clear();

         var dz = DirectionLocalZ.IsTiny() ? -1 * Vector3d.ZAxis : DirectionLocalZ;

         if (DrawUtil.DensityFactorLocalFrame < 0.001)
         {
            double p = Value.Domain.ParameterAt(0.5);
            Point3d pMid = Value.PointAt(p);
            Vector3d tMid = Value.TangentAt(p);
            Transform tScale = Rhino.Geometry.Transform.Scale(Point3d.Origin, DrawUtil.ScaleFactorLocalFrame);
            Transform tOri = TransformUtils.GetGlobalTransformLine(tMid, dz);
            Transform tTrans = Rhino.Geometry.Transform.Translation(new Vector3d(pMid));
            _localFrame.Transforms.Add(tTrans * tOri * tScale);
         }
         else
         {
            _localFrame.Transforms.AddRange(DrawUtil.GetCurveTransforms(Value, true, dz, null, DrawUtil.ScaleFactorLocalFrame, DrawUtil.DensityFactorLocalFrame));
         }  
      }

      private void drawSupportLine(Rhino.Display.DisplayPipeline pipeline, System.Drawing.Color col, bool shaded)
      {
         if (DrawUtil.ScaleFactorSupports > 0.0001 && _supp_condition.HasSupport)
         {
            if (!_supp_condition.IsValid)
            {
               updateSupportTransforms();
            }
            _supp_condition.Draw(pipeline, col, shaded);
         }
      }

      private void updateSupportTransforms()
      {
         _supp_condition.Transforms.Clear();

         var dz = DirectionLocalZ.IsTiny() ? Vector3d.ZAxis : DirectionLocalZ;

         _supp_condition.Transforms.AddRange(DrawUtil.GetCurveTransforms(Value, _supp_condition.LocalFrame, dz, null, DrawUtil.ScaleFactorSupports, DrawUtil.DensityFactorSupports));
      }

      public bool BakeGeometry(RhinoDoc doc, ObjectAttributes baking_attributes, out Guid obj_guid)
      {
         if (Value != null)
         {
            var att = baking_attributes.Duplicate();

            // set user strings
            att.SetUserString("SOF_OBJ_TYPE", "SLN");
            att.SetUserString("SOF_ID", Math.Max(0,Id).ToString());

            if(GroupId > 0)
               att.SetUserString("SOF_GRP", GroupId.ToString());
            if(SectionIdStart > 0)
            {
               att.SetUserString("SOF_STYP",ElementType);
               att.SetUserString("SOF_STYP2", "E");
               att.SetUserString("SOF_SNO", SectionIdStart.ToString());

               if(SectionIdEnd > 0)
                  att.SetUserString("SOF_SNOE", SectionIdEnd.ToString());
               else
                  att.SetUserString("SOF_SNOE", "SOF_PROP_COMBO_NONE");
            }

            if(ElementSize != 0.0)
               att.SetUserString("SOF_SDIV", ElementSize.ToString());

            if(DirectionLocalZ.Length > 1.0E-6)
            {
               var dir_z = DirectionLocalZ; dir_z.Unitize();

               att.SetUserString("SOF_DRX", dir_z.X.ToString("F6"));
               att.SetUserString("SOF_DRY", dir_z.Y.ToString("F6"));
               att.SetUserString("SOF_DRZ", dir_z.Z.ToString("F6"));
            }

            if (string.IsNullOrWhiteSpace(fixLiteral) == false)
            {
               string fix_literal = FixLiteral.Replace("PP", "PXPYPZ").Replace("MM", "MXMYMZ");

               if (fix_literal == "F")
                  fix_literal = "PXPYPZMXMYMZ";

               att.SetUserString("SOF_FIX", fix_literal);
            }

            if (string.IsNullOrWhiteSpace(UserText) == false)
               att.SetUserString("SOF_USERTXT", UserText);

            obj_guid = doc.Objects.AddCurve(Value, att);
         }
         else
         {
            obj_guid = new Guid();
         }
         return true;
      }
   }

   // create structural line
   public class CreateStructuralLine : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public CreateStructuralLine()
         : base("Structural Line","Structural Line","Creates SOFiSTiK Structural Lines","SOFiSTiK", "Structure")
      {}

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "structural_line_24x24.png");
            return _icon;
         }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddCurveParameter("Curve", "Crv", "Curve Geometry", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Number", "Number", "Identifier of structural line", GH_ParamAccess.list, 0);
         pManager.AddIntegerParameter("Group", "Group", "Group number of structural line", GH_ParamAccess.list, 0);
         pManager.AddGenericParameter("Section", "Section", "Section, Identifier of cross section or start and end section separated by '.' (e.g. '1.2')", GH_ParamAccess.list);
         pManager.AddVectorParameter("Dir Z", "Dir z", "Direction of local z-axis", GH_ParamAccess.list, new Vector3d());
         pManager.AddTextParameter("Fixation", "Fixation", "Support condition literal", GH_ParamAccess.list, string.Empty);
         pManager.AddTextParameter("Element Type", "Element Type", "Element Type of this structural line (Beam = default, Truss, Cable)", GH_ParamAccess.list, "Beam");
         pManager.AddNumberParameter("Element Size", "Element Size", "Size of FE elements [m]", GH_ParamAccess.list, 0.0);
         pManager.AddTextParameter("User Text", "User Text", "Custom User Text to be passed to the SOFiSTiK input", GH_ParamAccess.list, string.Empty);

         pManager[3].Optional = true;
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Structural Line", "Sln", "SOFiSTiK Structural Line", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var curves = da.GetDataList<Curve>(0);
         var identifiers = da.GetDataList<int>(1);
         var groups = da.GetDataList<int>(2);
         var sections = da.GetDataList<IGH_Goo>(3);
         var zdirs = da.GetDataList<Vector3d>(4);
         var fixations = da.GetDataList<string>(5);
         var elementType = da.GetDataList<string>(6);
         var elementSize = da.GetDataList<double>(7);
         var userText = da.GetDataList<string>(8);

         var gh_structural_curves = new List<GS_StructuralLine>();

         for( int i=0; i<curves.Count; ++i)
         {
            var c = curves[i];

            if (!(c is null))
            {
               var section_ids = new Tuple<int, int>(0, 0);
               var sectionGoo = sections.GetItemOrLast(i);
               if (sectionGoo != null)
               {
                  if (sectionGoo is GH_String)
                  {
                     var sectionIdString = (sectionGoo as GH_String).Value;
                     section_ids = Util.ParseSectionIdentifier(sectionIdString);
                  }
                  else
                  {
                     var sectionId = 0;
                     if (sectionGoo is Section.GH_Section)
                        sectionId = (sectionGoo as Section.GH_Section).Value.Id;
                     else if (sectionGoo is GH_Integer)
                        sectionId = (sectionGoo as GH_Integer).Value;
                     else if (sectionGoo is GH_Number)
                        sectionId = (int)(sectionGoo as GH_Number).Value;
                     else
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Could not extract section id from type " + sectionGoo.TypeName);
                     section_ids = new Tuple<int, int>(sectionId, sectionId);
                  }
               }

               var gc = new GS_StructuralLine()
               {
                  Value = c,
                  Id = identifiers.GetItemOrCountUp(i),
                  GroupId = groups.GetItemOrLast(i),
                  SectionIdStart = section_ids.Item1,
                  SectionIdEnd = section_ids.Item2,
                  DirectionLocalZ = zdirs.GetItemOrLast(i),
                  FixLiteral = fixations.GetItemOrLast(i),
                  ElementType = parseElementTypeString(elementType.GetItemOrLast(i)),
                  ElementSize = elementSize.GetItemOrLast(i),
                  UserText = userText.GetItemOrLast(i),
               };
               gh_structural_curves.Add(gc);
            }
         }

         da.SetDataList(0, gh_structural_curves);
      }

      private string parseElementTypeString(string s)
      {
         if (s is null)
         {
            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Element Type string is null. default value \"Beam\" will be used");
            return "B";
         }

         string slow = s.Trim().ToLower();

         string res = "B";
         if (slow.Equals("beam"))
            res = "B";
         else if (slow.Equals("truss"))
            res = "T";
         else if (slow.Equals("cable"))
            res = "C";
         else
            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Element Type string is not valid. default value \"Beam\" will be used");

         return res;
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("743DEE7B-D30B-4286-B74B-1415B92E87F7"); }
      }
   }
}
