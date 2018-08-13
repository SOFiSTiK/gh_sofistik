using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace gh_sofistik
{
   // class implementing a GH_ container for Rhino.Geometry.Curve
   public class GH_StructuralLine : GH_GeometricGoo<Curve>, IGH_PreviewData, IGH_BakeAwareData
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
         get { return "GH_StructuralLine"; }
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GH_StructuralLine()
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

      public override bool CastTo<Q>(out Q target)
      {
         if (Value != null)
         {
            // cast to GH_Curve (Caution: this loses all structural information)
            if (typeof(Q).IsAssignableFrom(typeof(GH_Curve)))
            {
               var gc = new GH_Curve(this.Value);
               target = (Q)(object)gc;
               return true;
            }
            // cast to GH_Line (Caution: this loses all structural information)
            else if (typeof(Q).IsAssignableFrom(typeof(GH_Line)))
            {
               if (this.Value is LineCurve)
               {
                  var gl = new GH_Line((Value as LineCurve).Line);
                  target = (Q)(object)gl;
                  return true;
               }
            }
            // cast to GH_Arc (Caution: this loses all structural information)
            else if (typeof(Q).IsAssignableFrom(typeof(GH_Arc)))
            {
               if(this.Value is ArcCurve)
               {
                  var ga = new GH_Arc((Value as ArcCurve).Arc);
                  target = (Q)(object)ga;
                  return true;
               }
            }
            else
            {
               throw new Exception("Unable to cast to type: " + typeof(Q).ToString());
            }
         }

         target = default(Q);
         return false;
      }

      public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
      {
         var dup = this.DuplicateGeometry() as GH_StructuralLine;
         xmorph.Morph(dup.Value);

         return dup;
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         var dup = this.DuplicateGeometry() as GH_StructuralLine;
         dup.Value.Transform(xform);

         return dup;
      }

      public override string ToString()
      {
         return Value.ToString();
      }

      public BoundingBox ClippingBox
      {
         get { return Boundingbox; }
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

      public bool BakeGeometry(RhinoDoc doc, ObjectAttributes baking_attributes, out Guid obj_guid)
      {
         if (Value != null)
         {
            var att = baking_attributes.Duplicate();

            var str_id = this.Id > 0 ? Id.ToString() : "0"; // string.Empty;

            att.SetUserString("SOF_OBJ_TYPE", "SLN");
            att.SetUserString("SOF_ID", str_id);

            if(this.GroupId > 0)
               att.SetUserString("SOF_GRP", this.GroupId.ToString());
            if(this.SectionId > 0)
            {
               att.SetUserString("SOF_STYP", "B");
               att.SetUserString("SOF_STYP2", "E");
               att.SetUserString("SOF_SNO", this.SectionId.ToString());
               att.SetUserString("SOF_SNOE", "SOF_PROP_COMBO_NONE");
            }
            att.SetUserString("SOF_SDIV", "0.0");

            if(DirectionLocalZ.Length > 1.0E-8)
            {
               att.SetUserString("SOF_DRX", DirectionLocalZ.X.ToString("F6"));
               att.SetUserString("SOF_DRY", DirectionLocalZ.Y.ToString("F6"));
               att.SetUserString("SOF_DRZ", DirectionLocalZ.Z.ToString("F6"));
            }

            if (string.IsNullOrEmpty(FixLiteral) == false)
               att.SetUserString("SOF_FIX", FixLiteral);

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
      public CreateStructuralLine()
         : base("SLN","SLN","Create SOFiSTiK Structural Lines","SOFiSTiK","Geometry")
      {}

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_line16; }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddCurveParameter("Curve", "Crv", "List of Curves", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Number(s)", "NO", "List of Ids (or start Id if only one given)", GH_ParamAccess.list, 0);
         pManager.AddIntegerParameter("Group", "GRP", "Group membership", GH_ParamAccess.item, 0);
         pManager.AddIntegerParameter("Section", "SNO", "Identifier of cross section", GH_ParamAccess.item, 0);
         pManager.AddVectorParameter("Local Z", "DRZ", "Direction of local z-axis", GH_ParamAccess.item, new Vector3d());
         pManager.AddTextParameter("Fixation", "FIX", "Support condition literal", GH_ParamAccess.item, string.Empty);
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
         var local_z = new Vector3d();
         string fix_literal = string.Empty;

         if (!DA.GetDataList(0, curves)) return;
         if (!DA.GetDataList(1, ids)) return;
         if (!DA.GetData(2, ref group_id)) return;
         if (!DA.GetData(3, ref section_id)) return;
         if (!DA.GetData(4, ref local_z)) return;
         if (!DA.GetData(5, ref fix_literal)) return;

         Utils.FillIdentifierList(ids, curves.Count);

         var gh_structural_curves = new List<GH_StructuralLine>();

         for( int i=0; i<curves.Count; ++i)
         {
            var c = curves[i];

            var gc = new GH_StructuralLine()
            {
               Value = c,
               Id = ids[i],
               GroupId = group_id,
               SectionId = section_id,
               DirectionLocalZ = local_z,
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
