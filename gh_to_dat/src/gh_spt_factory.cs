using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace gh_sofistik
{
   // class implementing a GH_ container for Rhino.Geometry.Point
   public class GH_GeometryPoint : Grasshopper.Kernel.Types.GH_GeometricGoo<Point>
   {
      public Vector3d DirectionLocalX { get; set; } = new Vector3d();
      public Vector3d DirectionLocalZ { get; set; } = new Vector3d();
      public string FixLiteral { get; set; } = string.Empty;

      public GH_GeometryPoint(Point p)
         : base()
      {
         Value = p;
      }

      public GH_GeometryPoint(Point3d location)
      {
         Value = new Point(location);
      }

      public override BoundingBox Boundingbox
      {
         get { return Value.GetBoundingBox(true); }
      }

      public override string TypeDescription
      {
         get { return "Generic Point: " + this.Value?.ToString(); }
      }

      public override string TypeName
      {
         get { return "GH_GeometryPoint"; }
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GH_GeometryPoint(this.Value.Location);
      }

      public override BoundingBox GetBoundingBox(Transform xform)
      {
         return xform.TransformBoundingBox(Value.GetBoundingBox(true));
      }

      public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
      {
         return new GH_GeometryPoint(xmorph.MorphPoint(this.Value.Location));
      }

      public override string ToString()
      {
         return this.Value.ToString();
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         Point3d location = this.Value.Location;
         location.Transform(xform);

         return new GH_GeometryPoint(location);
      }
   }


   public class CreateStructuralPoint : GH_Component
   {
      public CreateStructuralPoint()
         : base("SPT", "SPT", "Sets Structural point properties", "SOFiSTiK", "Geometry")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_point16; }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddPointParameter("Point", "P", "List of Points", GH_ParamAccess.list);
         pManager.AddVectorParameter("Local X", "TX", "Direction of local x-coordinate", GH_ParamAccess.item, new Vector3d());
         pManager.AddVectorParameter("Local Z", "TZ", "Direction of local z-coordinate", GH_ParamAccess.item, new Vector3d());
         pManager.AddTextParameter("Fix", "Fix", "Support condition literal", GH_ParamAccess.item, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Point", "P", "Point with assignments", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         var points3d = new List<Point3d>();

         string fix_literal = string.Empty;
         var sx = new Vector3d();
         var sz = new Vector3d();

         if (!DA.GetDataList(0, points3d)) return;
         if (!DA.GetData(1, ref sx)) return;
         if (!DA.GetData(2, ref sz)) return;
         if (!DA.GetData(3, ref fix_literal)) return;

         var gh_geometry_points = new List<GH_GeometryPoint>();
         foreach (var p3d in points3d)
         {
            var gp = new GH_GeometryPoint(p3d);

            if (sx.Length > 0.0)
               gp.DirectionLocalX = sx;

            if (sz.Length > 0.0)
               gp.DirectionLocalZ = sz;

            if (string.IsNullOrWhiteSpace(fix_literal) == false)
               gp.FixLiteral = fix_literal;

            gh_geometry_points.Add(gp);
         }

         DA.SetDataList(0, gh_geometry_points);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("F9EE1A70-3B5F-491B-BD2E-C9B44C72F209"); }
      }
   }
}
