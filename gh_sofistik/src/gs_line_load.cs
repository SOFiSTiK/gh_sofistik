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
   public class GS_LineLoad : GH_GeometricGoo<Curve>, IGS_Load
   {
      public int LoadCase { get; set; } = 0;
      public bool UseHostLocal { get; set; } = false;
      public Vector3d Forces { get; set; } = new Vector3d();
      public Vector3d Moments { get; set; } = new Vector3d();

      public int ReferenceLineId { get; set; } = 0;

      public override string TypeName
      {
         get { return "GS_LineLoad"; }
      }

      public override string TypeDescription
      {
         get { return "Line Load of load case " + LoadCase.ToString(); }
      }

      public override string ToString()
      {
         return this.TypeName;
      }

      public override bool CastTo<Q>(out Q target)
      {
         return Util.CastCurveTo(this.Value, out target);
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GS_LineLoad()
         {
            Value = this.Value.DuplicateCurve(),
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

   public class CreateLineLoad : GH_Component
   {
      public CreateLineLoad()
         : base("LINE", "LINE", "Creates SOFiSTiK Line Loads", "SOFiSTiK", "Loads")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.sofistik_32x32; } // TODO
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGeometryParameter("Hosting Curve", "Crv", "Hosting Curve Geometry", GH_ParamAccess.list);
         pManager.AddIntegerParameter("LoadCase", "LC", "Id of load case", GH_ParamAccess.list, 1);
         pManager.AddVectorParameter("Forces", "F", "Acting Forces", GH_ParamAccess.list, new Vector3d());
         pManager.AddVectorParameter("Moments", "M", "Acting Moments", GH_ParamAccess.list, new Vector3d());
         pManager.AddBooleanParameter("UseHostLocal", "isHL", "Use local coordinate system of host", GH_ParamAccess.list, false);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Line Load", "L", "SOFiSTiK Line Load", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var curves = da.GetDataList<IGH_GeometricGoo>(0);
         var loadcases = da.GetDataList<int>(1);
         var forces = da.GetDataList<Vector3d>(2);
         var moments = da.GetDataList<Vector3d>(3);
         var hostlocals = da.GetDataList<bool>(4);

         var gs_line_loads = new List<GS_LineLoad>();

         for (int i = 0; i < curves.Count; ++i)
         {
            var ll = new GS_LineLoad()
            {
               LoadCase = loadcases.GetItemOrLast(i),
               Forces = forces.GetItemOrLast(i),
               Moments = moments.GetItemOrLast(i),
               UseHostLocal = hostlocals.GetItemOrLast(i)
            };

            if(curves[i] is GS_StructuralLine)
            {
               var sln = curves[i] as GS_StructuralLine;

               ll.Value = sln.Value;
               ll.ReferenceLineId = sln.Id; // pass id of structural line
            }
            else if(curves[i] is GH_Curve)
            {
               ll.Value = (curves[i] as GH_Curve).Value;
            }
            else if (curves[i] is GH_Line)
            {
               ll.Value = new LineCurve((curves[i] as GH_Line).Value);
            }
            else if (curves[i] is GH_Arc)
            {
               ll.Value = new ArcCurve((curves[i] as GH_Arc).Value);
            }
            else
            {
               throw new Exception("Unable to Cast input to Curve Geometry");
            }

            gs_line_loads.Add(ll);
         }

         da.SetDataList(0, gs_line_loads);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("992ADED6-7395-4166-8DA7-7C78AE554615"); }
      }
   }
}
