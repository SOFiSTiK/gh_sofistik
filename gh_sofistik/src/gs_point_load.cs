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
   public interface IGS_Load
   {
      int LoadCase { get; }
      string TypeName { get; }
   }

   public class GS_PointLoad : GH_GeometricGoo<Point>, IGS_Load
   {
      public int LoadCase { get; set; } = 0;
      public bool UseHostLocal { get; set; } = false;
      public Vector3d Forces { get; set; } = new Vector3d();
      public Vector3d Moments { get; set; } = new Vector3d();

      public int ReferencePointId { get; set; } = 0;

      public override string TypeName
      {
         get { return "GS_PointLoad"; }
      }

      public override string TypeDescription
      {
         get { return "Point Load of load case " + LoadCase.ToString(); }
      }

      public override string ToString()
      {
         return "Point Load, LC = " + LoadCase.ToString();
      }

      public override bool CastTo<Q>(out Q target)
      {
         if(Value != null)
         {
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

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GS_PointLoad()
         {
            Value = new Point(this.Value.Location),
            LoadCase = this.LoadCase,
            Forces = this.Forces,
            Moments = this.Moments,
            UseHostLocal = this.UseHostLocal
         };
      }

      public override BoundingBox Boundingbox
      {
         get { return Value.GetBoundingBox(true); }
      }

      public override BoundingBox GetBoundingBox(Transform xform)
      {
         return xform.TransformBoundingBox(Value.GetBoundingBox(true));
      }

      public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
      {
         var dup = this.DuplicateGeometry() as GS_PointLoad;
         xmorph.Morph(dup.Value);

         return dup;
      }

      public override IGH_GeometricGoo Transform(Transform xform)
      {
         var dup = this.DuplicateGeometry() as GS_PointLoad;
         dup.Value.Transform(xform);

         return dup;
      }
   }


   public class CreatePointLoad : GH_Component
   {
      public CreatePointLoad()
         : base("Point Load","Point Load","Creates SOFiSTiK Point Loads", "SOFiSTiK", "Loads")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.structural_point_load_16; } // TODO
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGeometryParameter("Hosting Point", "Pt", "Hosting Point / SOFiSTiK Structural Point", GH_ParamAccess.list);
         pManager.AddIntegerParameter("LoadCase", "LoadCase", "Id of Load Case", GH_ParamAccess.list, 1);
         pManager.AddVectorParameter("Force", "Force", "Acting Force", GH_ParamAccess.list, new Vector3d());
         pManager.AddVectorParameter("Moment", "Moment", "Acting Moment", GH_ParamAccess.list, new Vector3d());
         pManager.AddBooleanParameter("HostLocal", "HostLocal", "Use local coordinate system of host", GH_ParamAccess.list, false);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Point Load", "PLd", "SOFiSTiK Point Load", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var points = da.GetDataList<IGH_GeometricGoo>(0);
         var loadcases = da.GetDataList<int>(1);
         var forces = da.GetDataList<Vector3d>(2);
         var moments = da.GetDataList<Vector3d>(3);
         var hostlocals = da.GetDataList<bool>(4);

         var gs_point_loads = new List<GS_PointLoad>();

         for(int i=0; i<points.Count; ++i)
         {
            var pl = new GS_PointLoad()
            {
               LoadCase = loadcases.GetItemOrLast(i),
               Forces = forces.GetItemOrLast(i),
               Moments = moments.GetItemOrLast(i),
               UseHostLocal = hostlocals.GetItemOrLast(i)
            };

            if(points[i] is GS_StructuralPoint)
            {
               var spt = points[i] as GS_StructuralPoint;

               pl.Value = spt.Value;
               pl.ReferencePointId = spt.Id; // pass id of structural point
            }
            else if(points[i] is GH_Point)
            {
               pl.Value = new Point((points[i] as GH_Point).Value);
            }
            else
            {
               throw new Exception("Unable to Cast input to Point Geometry");
            }

            gs_point_loads.Add(pl);
         }

         da.SetDataList(0, gs_point_loads);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("CA3198C1-C700-4A67-BA9F-F9F83EE73B93"); }
      }
   }
}
