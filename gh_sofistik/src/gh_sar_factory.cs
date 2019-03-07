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
   public class GS_StructuralArea : GH_GeometricGoo<Brep>, IGH_PreviewData, IGH_BakeAwareData, IGS_StructuralElement
   {
      public int Id { get; set; } = 0;
      public int GroupId { get; set; } = 0;
      public int MaterialId { get; set; } = 0;
      public int ReinforcementId { get; set; } = 0;
      public double Thickness { get; set; } = 0.0;
      public Vector3d DirectionLocalX { get; set; } = new Vector3d();      
      public string Alignment { get; set; } = "CENT";
      public string MeshOptions { get; set; } = "AUTO";      
      public double ElementSize { get; set; } = 0.0;
      public string UserText { get; set; } = string.Empty;

      private LocalFrameVisualisation _localFrame = new LocalFrameVisualisation();

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
         get { return "GS_StructuralArea"; }
      }

      public override string ToString()
      {
         return "Structural Area, Id = " + Id.ToString();
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GS_StructuralArea()
         {
            Value = this.Value.DuplicateBrep(),
            Id = this.Id,
            GroupId = this.GroupId,
            MaterialId = this.MaterialId,
            ReinforcementId = this.ReinforcementId,
            Thickness = this.Thickness,
            DirectionLocalX = this.DirectionLocalX,
            Alignment = this.Alignment,
            MeshOptions = this.MeshOptions,
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
         var dup = this.DuplicateGeometry() as GS_StructuralArea;
         xmorph.Morph(dup.Value);

         return dup;
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         var dup = this.DuplicateGeometry() as GS_StructuralArea;
         dup.Value.Transform(xform);

         return dup;
      }

      public BoundingBox ClippingBox
      {
         get { return DrawUtil.GetClippingBoxLocalframe(Value.GetBoundingBox(false)); }
      }

      public void DrawViewportWires(GH_PreviewWireArgs args)
      {
         //ClippingBox
         //args.Pipeline.DrawBox(ClippingBox, System.Drawing.Color.Black);
         if (Value != null)
         {  
            System.Drawing.Color col = args.Color;
            if(!DrawUtil.CheckSelection(col))
               col = DrawUtil.DrawColorStructuralElements;
            else
               drawLocalFrame(args.Pipeline);

            args.Pipeline.DrawBrepWires(Value, col, -1);
         }
      }

      public void DrawViewportMeshes(GH_PreviewMeshArgs args)
      {
         if (Value != null)
         {  
            System.Drawing.Color col = args.Material.Diffuse;
            Rhino.Display.DisplayMaterial areaStrcMaterial = new Rhino.Display.DisplayMaterial(args.Material);
            if (!DrawUtil.CheckSelection(col))
            {
               col = DrawUtil.DrawColorStructuralElements;

               areaStrcMaterial.Diffuse = col;
               areaStrcMaterial.Specular = col;
               areaStrcMaterial.Emission = col;
               areaStrcMaterial.BackDiffuse = col;
               areaStrcMaterial.BackSpecular = col;
               areaStrcMaterial.BackEmission = col;
            }
            
            args.Pipeline.DrawBrepShaded(Value, areaStrcMaterial);
         }
      }

      private void drawLocalFrame(Rhino.Display.DisplayPipeline pipeline)
      {
         if (DrawUtil.ScaleFactorLocalFrame > 0.0001)
         {
            if (!_localFrame.isValid)
            {
               updateLocalFrameTransforms();
            }
            _localFrame.Draw(pipeline);
         }
      }

      private void updateLocalFrameTransforms()
      {
         _localFrame.Transforms.Clear();

         Transform tScale = Rhino.Geometry.Transform.Scale(Point3d.Origin, DrawUtil.ScaleFactorLocalFrame);

         foreach (BrepFace bf in Value.Faces)
         {
            int n_seg_u = Math.Max(1, (int)(bf.Domain(0).Length * DrawUtil.DensityFactorLocalFrame));
            int n_seg_v = Math.Max(1, (int)(bf.Domain(1).Length * DrawUtil.DensityFactorLocalFrame));

            for (int i = 0; i <= n_seg_v; i++)
            {
               for (int j = 0; j <= n_seg_u; j++)
               {

                  double para_u = bf.Domain(0).ParameterAt((double)j / (double)n_seg_u);
                  double para_v = bf.Domain(1).ParameterAt((double)i / (double)n_seg_v);

                  if (DrawUtil.DensityFactorLocalFrame < 0.001)
                  {
                     para_u = bf.Domain(0).ParameterAt(0.5);
                     para_v = bf.Domain(1).ParameterAt(0.5);
                  }

                  Point3d ap = bf.PointAt(para_u, para_v);


                  Transform t = TransformUtils.GetGlobalTransformArea(ap, bf, DirectionLocalX);

                  Transform tTranslate = Rhino.Geometry.Transform.Translation(new Vector3d(ap));



                  _localFrame.Transforms.Add(tTranslate * t * tScale);
               }
            }
         }
      }

      private void updateLocalFrameTransformsOld()
      {
         _localFrame.Transforms.Clear();
         
         foreach (BrepFace bf in Value.Faces)
         {
            foreach (int beIndex in bf.AdjacentEdges())
            {
               _localFrame.Transforms.AddRange(DrawUtil.GetCurveTransforms(Value.Edges[beIndex], true, DirectionLocalX, bf, DrawUtil.ScaleFactorLocalFrame, DrawUtil.DensityFactorLocalFrame));
            }
         }
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

            if(DirectionLocalX.Length > 1.0e-6)
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
         : base("Structural Area","Structural Area","Creates SOFiSTiK Structural Areas","SOFiSTiK", "Structure")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_area_24x24; }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddBrepParameter("Brep", "Brep", "Brep / Surface Geometry", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Number", "Number", "Identifier of structural area", GH_ParamAccess.list, 0);
         pManager.AddIntegerParameter("Group", "Group", "Group numbers", GH_ParamAccess.list, 0);
         pManager.AddNumberParameter("Thickness", "Thickness", "Thickness of structural area", GH_ParamAccess.list, 0.0);
         pManager.AddIntegerParameter("Material", "Material", "Material number", GH_ParamAccess.list, 0);
         pManager.AddIntegerParameter("ReinforcementMaterial", "ReinfMat", "Reinforcement material number", GH_ParamAccess.list, 0);
         pManager.AddVectorParameter("Dir x", "Dir x", "Direction of local x-axis", GH_ParamAccess.list, new Vector3d());
         pManager.AddTextParameter("Alignment", "Alignment", "Alignment of the volume in relation to the surface area (Centered = default, Above, Below)", GH_ParamAccess.list, "Centered");
         pManager.AddTextParameter("Mesh Options", "Mesh Options", "Mesh Options for the SOFiSTiK FE meshing (Automatic = default, Regular, Single Quad, Deactivate)", GH_ParamAccess.list, "Automatic");
         pManager.AddNumberParameter("Element Size", "Element Size", "Size of FE elements [m]", GH_ParamAccess.list, 0.0);
         pManager.AddTextParameter("User Text", "User Text", "Custom User Text to be passed to the SOFiSTiK input", GH_ParamAccess.list, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Structural Area", "Sar", "SOFiSTiK Structural Area", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var breps = da.GetDataList<Brep>(0);
         var ids = da.GetDataList<int>(1);
         var groups = da.GetDataList<int>(2);
         var thicknss = da.GetDataList<double>(3);
         var materials = da.GetDataList<int>(4);
         var matreinfs = da.GetDataList<int>(5);
         var xdirs = da.GetDataList<Vector3d>(6);
         var alignment = da.GetDataList<string>(7);
         var meshOptions = da.GetDataList<string>(8);
         var elementSize = da.GetDataList<double>(9);
         var userText = da.GetDataList<string>(10);

         var gh_structural_areas = new List<GS_StructuralArea>();

         for(int i=0; i<breps.Count; ++i)
         {
            var b = breps[i];

            if (!(b is null))
            {
               var ga = new GS_StructuralArea()
               {
                  Value = b,
                  Id = ids.GetItemOrCountUp(i),
                  GroupId = groups.GetItemOrLast(i),
                  MaterialId = materials.GetItemOrLast(i),
                  ReinforcementId = matreinfs.GetItemOrLast(i),
                  Thickness = thicknss.GetItemOrLast(i),
                  DirectionLocalX = xdirs.GetItemOrLast(i),
                  Alignment = parseAlignmentString(alignment.GetItemOrLast(i)),
                  MeshOptions = parseMeshOptionsString(meshOptions.GetItemOrLast(i)),
                  ElementSize = elementSize.GetItemOrLast(i),
                  UserText = userText.GetItemOrLast(i)
               };
               gh_structural_areas.Add(ga);
            }
         }

         da.SetDataList(0, gh_structural_areas);
      }

      private string parseAlignmentString(string s)
      {
         string slow = s.Trim().ToLower();

         string res = "CENT";
         if (slow.Equals("centered"))
            res = "CENT";
         else if (slow.Equals("above"))
            res = "ABOV";
         else if (slow.Equals("below"))
            res = "BELO";
         else
            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Alignment string is not valid. default value \"Centered\" will be used");

         return res;
      }

      private string parseMeshOptionsString(string s)
      {
         string slow = s.Trim().ToLower();

         string res = "AUTO";
         if(slow.Equals("automatic"))
            res = "AUTO";
         else if (slow.Equals("regular"))
            res = "REGM";
         else if (slow.Equals("single quad") || slow.Equals("singlequad"))
            res = "SNGQ";
         else if (slow.Equals("deactivate"))
            res = "OFF";
         else
            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Mesh Options string is not valid. default value \"Automatic\" will be used");

         return res;
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("FC0973D1-0EC1-435A-ABD2-F6D9C7D3D7F3"); }
      }
   }

}