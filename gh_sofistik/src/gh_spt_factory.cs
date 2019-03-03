using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace gh_sofistik
{
   public interface IGS_StructuralElement
   {
      int Id { get; set; }
      string TypeName { get; }
   }

   // class implementing a GH_ container for Rhino.Geometry.Point
   public class GS_StructuralPoint : GH_GeometricGoo<Point>, IGH_PreviewData, IGH_BakeAwareData, IGS_StructuralElement
   {
      public int Id { get; set; } = 0;
      public Vector3d DirectionLocalX { get; set; } = new Vector3d();
      public Vector3d DirectionLocalZ { get; set; } = new Vector3d();

      private SupportCondition _supp_condition = null;

      private LocalFrameVisualisation _localFrame = new LocalFrameVisualisation();

      private string fixLiteral = string.Empty;    

      public string FixLiteral
      {
         get
         {
            return fixLiteral;
         }
         set
         {
            fixLiteral = value;
            _supp_condition = new SupportCondition(fixLiteral);
         }
      }

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
         get { return "GS_StructuralPoint"; }
      }

      public override string ToString()
      {
         return "Structural Point, Id = " + Id.ToString();
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GS_StructuralPoint()
         {
            Value = new Point(this.Value.Location),
            Id = this.Id,
            DirectionLocalX = this.DirectionLocalX,
            DirectionLocalZ = this.DirectionLocalZ,
            FixLiteral = this.FixLiteral
         };
      }

      public override BoundingBox GetBoundingBox(Transform xform)
      {
         return xform.TransformBoundingBox(Value.GetBoundingBox(true));
      }

      public override bool CastTo<Q>( out Q target)
      {
         if(Value != null)
         {
            // cast to GH_Point (Caution: this loses all structural information)
            if (typeof(Q).IsAssignableFrom(typeof(GH_Point)))
            {
               var gp = new GH_Point(this.Value.Location);
               target = (Q)(object)gp;
               return true;
            }
         }

         target = default(Q);
         return false;
      }

      public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
      {
         var dup = this.DuplicateGeometry() as GS_StructuralPoint;
         xmorph.Morph(dup.Value);

         return dup;
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         var dup = this.DuplicateGeometry() as GS_StructuralPoint;
         dup.Value.Transform(xform);

         return dup;
      }

      public BoundingBox ClippingBox
      {
         get
         {
            if (fixLiteral.Equals(""))
               return Value.GetBoundingBox(false);
            else
               return DrawUtil.GetClippingBoxSupports(Value.GetBoundingBox(false));
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
               drawLocalFrame(args.Pipeline);

            args.Pipeline.DrawPoint(Value.Location, Rhino.Display.PointStyle.X, 5, colStr);

            drawSupportPoint(args.Pipeline, colSup, false);
         }
      }

      public void DrawViewportMeshes(GH_PreviewMeshArgs args)
      {
         // no need to draw meshes 
         if (Value != null)
         {
            drawSupportPoint(args.Pipeline, DrawUtil.DrawColorSupports, true);
         }
      }

      private void drawLocalFrame(Rhino.Display.DisplayPipeline pipeline)
      {
         if (DrawUtil.ScaleFactorLocalFrame > 0.0001)
         {
            if (!_localFrame.isValid)
               updateLocalFrameTransforms();
            _localFrame.Draw(pipeline);
         }
      }

      private void updateLocalFrameTransforms()
      {
         _localFrame.Transforms.Clear();

         Transform tScale = Rhino.Geometry.Transform.Scale(Point3d.Origin, DrawUtil.ScaleFactorLocalFrame);

         Vector3d lx = DirectionLocalX.IsTiny() ? Vector3d.XAxis : DirectionLocalX;
         Vector3d lz = DirectionLocalZ.IsTiny() ? -1 * Vector3d.ZAxis : DirectionLocalZ;

         Transform tFinal = TransformUtils.GetGlobalTransformPoint(lx, lz);

         Transform tTranslate = Rhino.Geometry.Transform.Translation(new Vector3d(Value.Location));

         tFinal = tTranslate * tFinal * tScale;

         _localFrame.Transforms.Add(tFinal);
      }

      private void drawSupportPoint(Rhino.Display.DisplayPipeline pipeline, System.Drawing.Color col, bool shaded)
      {
         if (DrawUtil.ScaleFactorSupports > 0.0001)
         {
            if (!_supp_condition.isValid)
            {
               updateSupportTransforms();
            }
            _supp_condition.Draw(pipeline, col, shaded);
         }
      }

      private void updateSupportTransforms()
      {
         _supp_condition.Transforms.Clear();

         Transform tScale = Rhino.Geometry.Transform.Scale(Point3d.Origin, DrawUtil.ScaleFactorSupports);

         Vector3d lx = DirectionLocalX.IsTiny() ? Vector3d.XAxis : DirectionLocalX;
         Vector3d lz = DirectionLocalZ.IsTiny() ? Vector3d.ZAxis : DirectionLocalZ;

         Transform tFinal = Rhino.Geometry.Transform.Identity;

         if (_supp_condition.LocalFrame)
         {
            tFinal = TransformUtils.GetGlobalTransformPoint(lx, lz);
         }

         Transform tTranslate = Rhino.Geometry.Transform.Translation(new Vector3d(Value.Location));

         tFinal = tTranslate * tFinal * tScale;

         _supp_condition.Transforms.Add(tFinal);
      }

      public bool BakeGeometry(RhinoDoc doc, ObjectAttributes baking_attributes, out Guid obj_guid)
      {
         if(Value != null)
         {
            var att = baking_attributes.Duplicate();

            string fix_literal = this.FixLiteral
               .Replace("PP", "PXPYPZ")
               .Replace("MM", "MXMYMZ");

            if (fix_literal == "F")
               fix_literal = "PXPYPZMXMYMZ";


            att.SetUserString("SOF_OBJ_TYPE", "SPT");
            att.SetUserString("SOF_ID", this.Id.ToString());

            if(DirectionLocalX.Length > 1.0E-6)
            {
               att.SetUserString("SOF_SX", this.DirectionLocalX.X.ToString("F6"));
               att.SetUserString("SOF_SY", this.DirectionLocalX.Y.ToString("F6"));
               att.SetUserString("SOF_SZ", this.DirectionLocalX.Z.ToString("F6"));
            }
            if(DirectionLocalZ.Length > 1.0E-6)
            {
               att.SetUserString("SOF_NX", this.DirectionLocalZ.X.ToString("F6"));
               att.SetUserString("SOF_NY", this.DirectionLocalZ.Y.ToString("F6"));
               att.SetUserString("SOF_NZ", this.DirectionLocalZ.Z.ToString("F6"));
            }

            if(string.IsNullOrEmpty(fix_literal) == false)
               att.SetUserString("SOF_FIX", fix_literal);


            obj_guid = doc.Objects.AddPoint(Value.Location, att);
         }
         else
         {
            obj_guid = new Guid();
         }
         return true;
      }
   }

   // create structural point
   public class CreateStructuralPoint : GH_Component
   {
      public CreateStructuralPoint()
         : base("Structural Point", "Structural Point", "Creates SOFiSTiK Structural Points", "SOFiSTiK", "Structure")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_point16; }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddPointParameter("Point", "Pt", "Point Geometry", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Number", "Number", "Identifier of structural point", GH_ParamAccess.list, 0);
         pManager.AddVectorParameter("Dir x", "Dir x", "Direction of local x-axis", GH_ParamAccess.list, new Vector3d());
         pManager.AddVectorParameter("Dir z", "Dir z", "Direction of local z-axis", GH_ParamAccess.list, new Vector3d());
         pManager.AddTextParameter("Fixation", "Fixation", "Support condition literal", GH_ParamAccess.list, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Structural Point", "Spt", "SOFiSTiK Structural Point", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var points = da.GetDataList<Point3d>(0);
         var identifiers = da.GetDataList<int>(1);
         var xdirs = da.GetDataList<Vector3d>(2);
         var zdirs = da.GetDataList<Vector3d>(3);
         var fixations = da.GetDataList<string>(4);

         var gh_structural_points = new List<GS_StructuralPoint>();

         for (int i = 0; i < points.Count; ++i)
         {
            var p3d = points[i];

            var gp = new GS_StructuralPoint()
            {
               Value = new Point(p3d),
               Id = identifiers.GetItemOrCountUp(i),
               DirectionLocalX = xdirs.GetItemOrLast(i),
               DirectionLocalZ = zdirs.GetItemOrLast(i),
               FixLiteral = fixations.GetItemOrLast(i)
            };
            gh_structural_points.Add(gp);
         }

         da.SetDataList(0, gh_structural_points);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("F9EE1A70-3B5F-491B-BD2E-C9B44C72F209"); }
      }
   }
}
