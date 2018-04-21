using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace gh_sofistik
{
   // class implementing a GH_ container for Rhino.Geometry.Curve
   public class GH_StructuralCurve : GH_GeometricGoo<Curve>, IGH_PreviewData
   {
      public int Id { get; set; } = 0;
      public int GroupId { get; set; } = 0;    
      public int SectionId { get; set; } = 0;
      public Vector3d DirectionLocalZ { get; set; } = new Vector3d();
      public string FixLiteral { get; set; } = string.Empty;

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
         get { return "GH_StructuralCurve"; }
      }

      public BoundingBox ClippingBox
      {
         get { return Boundingbox; }
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GH_StructuralCurve()
         {
            Value = this.Value.DuplicateCurve(),
            Id = this.Id,
            GroupId = this.GroupId,
            SectionId = this.SectionId,
            DirectionLocalZ = this.DirectionLocalZ,
            FixLiteral = this.FixLiteral
         };
      }

      public override BoundingBox GetBoundingBox(Transform xform)
      {
         return xform.TransformBoundingBox(Value.GetBoundingBox(true));
      }

      public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
      {
         var dup = this.DuplicateGeometry() as GH_StructuralCurve;
         xmorph.Morph(dup.Value);

         return dup;
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         var dup = this.DuplicateGeometry() as GH_StructuralCurve;
         dup.Value.Transform(xform);

         return dup;
      }

      public override string ToString()
      {
         return Value.ToString();
      }

      public void DrawViewportWires(GH_PreviewWireArgs args)
      {
         if(Value != null)
         {
            args.Pipeline.DrawCurve(Value, System.Drawing.Color.Red);
         }
      }

      public void DrawViewportMeshes(GH_PreviewMeshArgs args)
      {
         // no need to draw meshes
      }
   }

   // create structural line
   public class CreateStructuralLine : GH_Component
   {
      public CreateStructuralLine()
         : base("SLN","SLN","Assign SOFiSTiK Properties to Curves","SOFiSTiK","Geometry")
      {}

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_line16; }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddCurveParameter("Curve", "C", "List of Curves", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Id", "Id", "List of Ids (or start Id if only one given)", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Group", "Grp", "Group membership", GH_ParamAccess.item, 0);
         pManager.AddIntegerParameter("Section", "Sno", "Identifier of cross section", GH_ParamAccess.item, 0);
         pManager.AddVectorParameter("Local Z", "Tz", "Direction of local z-axis", GH_ParamAccess.item, new Vector3d());
         pManager.AddTextParameter("Fix", "Fix", "Support condition literal", GH_ParamAccess.item, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Curve", "C", "Curve with SOFiSTiK properties", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var curves = new List<Curve>();

         var ids = new List<int>();
         int group_id = 0;
         int section_id = 0;
         var zdir = new Vector3d();
         string fix_literal = string.Empty;

         if (!DA.GetDataList(0, curves)) return;
         if (!DA.GetDataList(1, ids)) return;
         if (!DA.GetData(2, ref group_id)) return;
         if (!DA.GetData(3, ref section_id)) return;
         if (!DA.GetData(4, ref zdir)) return;
         if (!DA.GetData(5, ref fix_literal)) return;

         Utils.FillIdentifierList(ids, curves.Count);

         var gh_structural_curves = new List<GH_StructuralCurve>();

         for( int i=0; i<curves.Count; ++i)
         {
            var c = curves[i];

            var gc = new GH_StructuralCurve()
            {
               Value = c,
               Id = ids[i],
               GroupId = group_id,
               SectionId = section_id,
               DirectionLocalZ = zdir,
               FixLiteral = fix_literal
            };
            gh_structural_curves.Add(gc);
         }

         DA.SetDataList(0, gh_structural_curves);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("743DEE7B-D30B-4286-B74B-1415B92E87F7"); }
      }
   }
}
