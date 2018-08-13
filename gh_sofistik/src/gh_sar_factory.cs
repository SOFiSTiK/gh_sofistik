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
   public class GH_StructuralArea : GH_GeometricGoo<Brep>, IGH_PreviewData, IGH_BakeAwareData
   {
      public int Id { get; set; } = 0;
      public int GroupId { get; set; } = 0;
      public int MaterialId { get; set; } = 0;
      public int ReinforcementId { get; set; } = 0;
      public double Thickness { get; set; } = 0.0;
      public Vector3d DirectionLocalX { get; set; } = new Vector3d();

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
         get { return "GH_StructuralArea"; }
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GH_StructuralArea()
         {
            Value = this.Value.DuplicateBrep(),
            Id = this.Id,
            GroupId = this.GroupId,
            MaterialId = this.MaterialId,
            ReinforcementId = this.ReinforcementId,
            Thickness = this.Thickness,
            DirectionLocalX = this.DirectionLocalX
         };
      }

      public override BoundingBox GetBoundingBox(Transform xform)
      {
         return xform.TransformBoundingBox(Value.GetBoundingBox(true));
      }

      public override bool CastTo<Q>(out Q target)
      {
         if(Value != null)
         {
            if(typeof(Q).IsAssignableFrom(typeof(GH_Brep)))
            {
               var gb = new GH_Brep(this.Value);
               target = (Q)(object)gb;
               return true;
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
         var dup = this.DuplicateGeometry() as GH_StructuralArea;
         xmorph.Morph(dup.Value);

         return dup;
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         var dup = this.DuplicateGeometry() as GH_StructuralArea;
         dup.Value.Transform(xform);

         return dup;
      }

      public override string ToString()
      {
         return Value.ToString(); // TODO: add some more information?
      }

      public BoundingBox ClippingBox
      {
         get { return Boundingbox; }
      }

      public void DrawViewportWires(GH_PreviewWireArgs args)
      {
         if (Value != null)
         {
            args.Pipeline.DrawBrepWires(Value, System.Drawing.Color.Red);
         }
      }

      public void DrawViewportMeshes(GH_PreviewMeshArgs args)
      {
         //throw new NotImplementedException();
      }

      public bool BakeGeometry(RhinoDoc doc, ObjectAttributes baking_attributes, out Guid obj_guid)
      {
         if (Value != null)
         {
            var att = baking_attributes.Duplicate();

            var id_str = this.Id > 0 ? Id.ToString() : "0"; 
            var grp_str = this.GroupId.ToString();
            var mno_str = this.MaterialId.ToString();
            var mrf_str = this.ReinforcementId.ToString();
            var t_str = this.Thickness.ToString();

            att.SetUserString("SOF_OBJ_TYPE", "SAR");
            att.SetUserString("SOF_ID", id_str);
            att.SetUserString("SOF_T", Thickness.ToString());
            if(GroupId>0)
               att.SetUserString("SOF_GRP", GroupId.ToString());
            if (MaterialId > 0)
               att.SetUserString("SOF_MNO", MaterialId.ToString());
            if (ReinforcementId > 0)
               att.SetUserString("SOF_MRF", ReinforcementId.ToString());

            if(DirectionLocalX.Length > 1.0e-8)
            {
               att.SetUserString("SOF_DRX", DirectionLocalX.X.ToString("F6"));
               att.SetUserString("SOF_DRX", DirectionLocalX.Y.ToString("F6"));
               att.SetUserString("SOF_DRZ", DirectionLocalX.Z.ToString("F6"));
            }

            obj_guid = doc.Objects.AddBrep(Value, att);
         }
         else
         {
            obj_guid = new Guid();
         }
         return true;
      }

   }


   // Structural Area node
   public class CreateStructuralArea : GH_Component
   {
      public CreateStructuralArea()
         : base("SAR","SAR","Create SOFiSTiK Structural Area","SOFiSTiK", "Geometry")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_area16; }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddBrepParameter("Brep", "Brp", "List of Breps / Surfaces", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Number(s)", "NO", "List of Ids (or start Id if only one given)", GH_ParamAccess.list, 0);
         pManager.AddIntegerParameter("Group", "GRP", "Group membership", GH_ParamAccess.item, 0);
         pManager.AddNumberParameter("Thickness", "T", "Thickness of surface", GH_ParamAccess.item, 0.0);
         pManager.AddIntegerParameter("Material", "MNR", "Material of surface member", GH_ParamAccess.item, 0);
         pManager.AddIntegerParameter("ReinforcementMaterial", "MBW", "Reinforcement Material of surface member", GH_ParamAccess.item, 0);
         pManager.AddVectorParameter("Local X", "DRX", "Direction of local x-axis", GH_ParamAccess.item, new Vector3d());
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Brep", "Brp", "Breps / Surfaces with SOFiSTiK properties", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var breps = new List<Brep>();

         var ids = new List<int>();
         int group_id = 0;
         int material_id = 0;
         int reinforcement_id = 0;
         double thickness = 0.0;
         var local_x = new Vector3d();


         if (!DA.GetDataList(0, breps)) return;
         if (!DA.GetDataList(1, ids)) return;
         if (!DA.GetData(2, ref group_id)) return;
         if (!DA.GetData(3, ref thickness)) return;
         if (!DA.GetData(4, ref material_id)) return;
         if (!DA.GetData(5, ref reinforcement_id)) return;
         if (!DA.GetData(6, ref local_x)) return;

         Utils.FillIdentifierList(ids, breps.Count);

         var gh_structural_areas = new List<GH_StructuralArea>();

         for(int i=0; i<breps.Count; ++i)
         {
            var b = breps[i];

            var ga = new GH_StructuralArea()
            {
               Value = b,
               Id = ids[i],
               GroupId = group_id,
               MaterialId = material_id,
               ReinforcementId = reinforcement_id,
               Thickness = thickness,
               DirectionLocalX = local_x
            };
            gh_structural_areas.Add(ga);
         }

         DA.SetDataList(0, gh_structural_areas);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("FC0973D1-0EC1-435A-ABD2-F6D9C7D3D7F3"); }
      }
   }

}