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
   public class Utils
   {
      public static void FillIdentifierList(List<int> ids, int Size)
      {
         if (ids.Count == 0 || ids.First() == 0)
         {
            while (ids.Count < Size)
               ids.Add(0);
         }
         else
         {
            while (ids.Count < Size)
               ids.Add(ids.Last() + 1);
         }
      }
   }

   // class implementing a GH_ container for Rhino.Geometry.Point
   public class GH_StructuralPoint : GH_GeometricGoo<Point>, IGH_PreviewData, IGH_BakeAwareData
   {
      public int Id { get; set; } = 0;
      public Vector3d DirectionLocalX { get; set; } = new Vector3d();
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
         get { return "GH_StructuralPoint"; }
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GH_StructuralPoint()
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
         var dup = this.DuplicateGeometry() as GH_StructuralPoint;
         xmorph.Morph(dup.Value);

         return dup;
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         var dup = this.DuplicateGeometry() as GH_StructuralPoint;
         dup.Value.Transform(xform);

         return dup;
      }

      public override string ToString()
      {
         return this.Value.ToString();
      }

      public BoundingBox ClippingBox
      {
         get { return Boundingbox; }
      }

      public void DrawViewportWires(GH_PreviewWireArgs args)
      {
         if(Value != null)
         {
            args.Pipeline.DrawPoint(Value.Location, Rhino.Display.PointStyle.X, 5, System.Drawing.Color.Red);
         }
      }

      public void DrawViewportMeshes(GH_PreviewMeshArgs args)
      {
         // no need to draw meshes 
      }

      public bool BakeGeometry(RhinoDoc doc, ObjectAttributes baking_attributes, out Guid obj_guid)
      {
         if(Value != null)
         {
            var att = baking_attributes.Duplicate();

            att.SetUserString("SOF_OBJ_TYPE", "SPT");
            att.SetUserString("SOF_ID", this.Id.ToString());
            att.SetUserString("SOF_FIX", this.FixLiteral);
            // TODO

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
         : base("SPT", "SPT", "Create SOFiSTiK Structural Points", "SOFiSTiK", "Geometry")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_point16; }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         var ids = new List<int>(); ids.Add(0);

         pManager.AddPointParameter("Point", "P", "List of Points", GH_ParamAccess.list);
         pManager.AddIntegerParameter("Number(s)", "NO", "List of Ids (or start Id if only one given)", GH_ParamAccess.list, 0);
         pManager.AddVectorParameter("Local X", "TX", "Direction of local x-axis", GH_ParamAccess.item, new Vector3d());
         pManager.AddVectorParameter("Local Z", "TZ", "Direction of local z-axis", GH_ParamAccess.item, new Vector3d());
         pManager.AddTextParameter("Fixation", "FIX", "Support condition literal", GH_ParamAccess.item, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Point", "P", "Point with SOFiSTiK properties", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var points = new List<Point3d>();

         var ids = new List<int>();
         var sx = new Vector3d();
         var sz = new Vector3d();
         string fix_literal = string.Empty;

         if (!DA.GetDataList(0, points)) return;
         if (!DA.GetDataList(1, ids)) return;
         if (!DA.GetData(2, ref sx)) return;
         if (!DA.GetData(3, ref sz)) return;
         if (!DA.GetData(4, ref fix_literal)) return;

         Utils.FillIdentifierList(ids, points.Count);

         var gh_structural_points = new List<GH_StructuralPoint>();

         for (int i = 0; i < points.Count; ++i)
         {
            var p3d = points[i];

            var gp = new GH_StructuralPoint()
            {
               Value = new Point(p3d),
               Id = ids[i],
               DirectionLocalX = sx,
               DirectionLocalZ = sz,
               FixLiteral = fix_literal
            };
            gh_structural_points.Add(gp);
         }

         DA.SetDataList(0, gh_structural_points);

         //var geometry_points = new List<Point>();

         //for(int i=0; i<points.Count; ++i)
         //{
         //   var p3 = points[i];

         //   var p = new Point(p3);
         //   p.SetUserString("SOF_ID", ids[i].ToString());
         //   p.SetUserString("SOF_SPT_FIX", fix_literal);

         //   geometry_points.Add(p);
         //}

         //DA.SetDataList(0, geometry_points);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("F9EE1A70-3B5F-491B-BD2E-C9B44C72F209"); }
      }
   }
}
