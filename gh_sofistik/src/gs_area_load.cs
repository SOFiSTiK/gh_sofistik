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

namespace gh_sofistik.Open
{
   public class GS_AreaLoad : GH_GeometricGoo<Brep>, IGS_Load, IGH_PreviewData
   {
      public int LoadCase { get; set; } = 0;
      public bool UseHostLocal { get; set; } = false;
      public Vector3d Forces { get; set; } = new Vector3d();
      public Vector3d Moments { get; set; } = new Vector3d();

      private LoadCondition _loadCondition = new LoadCondition();

      public GS_StructuralArea ReferenceArea { get; set; }

      public override string TypeName
      {
         get { return "GS_AreaLoad"; }
      }

      public override string TypeDescription
      {
         get { return "Area Load of load case " + LoadCase.ToString(); }
      }

      public override string ToString()
      {
         return "Area Load, LC = " + LoadCase.ToString();
      }

      public override bool CastTo<Q>(out Q target)
      {
         return Util.CastBrepTo(this.Value, out target);
      }

      public override IGH_GeometricGoo DuplicateGeometry()
      {
         return new GS_AreaLoad()
         {
            Value = this.Value.DuplicateBrep(),
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

      public BoundingBox ClippingBox
      {
         get
         {
            return DrawUtil.GetClippingBoxLoads(Value.GetBoundingBox(false), Forces.Length, Moments.Length);
         }
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

      public void DrawViewportWires(GH_PreviewWireArgs args)
      {
         //ClippingBox
         //args.Pipeline.DrawBox(ClippingBox, System.Drawing.Color.Black);

         if (!(Value is null))
         {
            System.Drawing.Color col = args.Color;
            if (!DrawUtil.CheckSelection(col))
               col = DrawUtil.DrawColorLoads;
            args.Pipeline.DrawBrepWires(Value, col, -1);

            if (DrawUtil.ScaleFactorLoads > 0.0001 && !(Forces.IsTiny() && Moments.IsTiny()))
            {
               if (!_loadCondition.isValid)
               {
                  updateLoadTransforms();
               }
               _loadCondition.Draw(args.Pipeline, col);
            }
         }
      }

      private void updateLoadTransforms()
      {
         _loadCondition = new LoadCondition(Forces, Moments);

         Vector3d lx = Vector3d.Zero;
         if (!(ReferenceArea is null))
         {
            lx = ReferenceArea.DirectionLocalX;
         }
         //   iterate over BrepFaces
         foreach (BrepFace bf in Value.Faces)
         {
            //   iterate over Edges of current face and draw this edge
            foreach (int beIndex in bf.AdjacentEdges())
            {
               var txList = DrawUtil.GetCurveTransforms(Value.Edges[beIndex], UseHostLocal, lx, bf, DrawUtil.ScaleFactorLoads, DrawUtil.DensityFactorLoads);
               if (ReferenceArea != null && UseHostLocal && ReferenceArea.FlipZ)
                  foreach (var tx in txList)
                     _loadCondition.Transforms.Add(tx * Rhino.Geometry.Transform.Rotation(Math.PI, Vector3d.XAxis, Point3d.Origin));
               else
                  _loadCondition.Transforms.AddRange(txList);
            }
         }
      }


      public void DrawViewportMeshes(GH_PreviewMeshArgs args)
      {
         if (!(Value is null))
         {
            Rhino.Display.DisplayMaterial areaLoadMaterial = new Rhino.Display.DisplayMaterial(args.Material);

            System.Drawing.Color col = DrawUtil.DrawColorLoads;

            areaLoadMaterial.Diffuse = col;
            areaLoadMaterial.Specular = col;
            areaLoadMaterial.Emission = col;
            areaLoadMaterial.BackDiffuse = col;
            areaLoadMaterial.BackSpecular = col;
            areaLoadMaterial.BackEmission = col;

            args.Pipeline.DrawBrepShaded(Value, areaLoadMaterial);
         }
      }
   }

   public class CreateAreaLoad : GH_Component
   {
      private System.Drawing.Bitmap _icon;

      public CreateAreaLoad()
         : base("Area Load", "Area Load", "Creates SOFiSTiK Area Loads", "SOFiSTiK", "Loads")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "structural_area_load_24x24.png");
            return _icon;
         }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGeometryParameter("Hosting Brep / Sar", "Brp / Sar", "Hosting Brep / SOFiSTiK Structural Area", GH_ParamAccess.list);
         pManager.AddIntegerParameter("LoadCase", "LoadCase", "Id of Load Case", GH_ParamAccess.list, 1);
         pManager.AddVectorParameter("Force", "Force", "Acting Force", GH_ParamAccess.list, new Vector3d());
         pManager.AddVectorParameter("Moment", "Moment", "Acting Moment", GH_ParamAccess.list, new Vector3d());
         pManager.AddBooleanParameter("HostLocal", "HostLocal", "Use local coordinate system of host", GH_ParamAccess.list, false);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddGeometryParameter("Area Load", "ALd", "SOFiSTiK Area Load", GH_ParamAccess.list);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var areas = da.GetDataList<IGH_GeometricGoo>(0);
         var loadcases = da.GetDataList<int>(1);
         var forces = da.GetDataList<Vector3d>(2);
         var moments = da.GetDataList<Vector3d>(3);
         var hostlocals = da.GetDataList<bool>(4);

         var gs_area_loads = new List<GS_AreaLoad>();

         //int max_count = Math.Max(areas.Count, loadcases.Count); // either area or lc is the leading input

         for (int i = 0; i < areas.Count; ++i)
         {
            var area = areas.GetItemOrLast(i);

            if (!(area is null))
            {
               var ll = new GS_AreaLoad()
               {
                  LoadCase = loadcases.GetItemOrLast(i),
                  Forces = forces.GetItemOrLast(i),
                  Moments = moments.GetItemOrLast(i),
                  UseHostLocal = hostlocals.GetItemOrLast(i)
               };

               bool addArea = true;
               if (area is GS_StructuralArea)
               {
                  var sar = area as GS_StructuralArea;

                  ll.Value = sar.Value;
                  ll.ReferenceArea = sar; // pass reference of structural area
               }
               else if (area is GH_GeometricGoo<Brep>)
               {
                  ll.Value = (area as GH_GeometricGoo<Brep>).Value;
               }
               else
               {
                  AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unable to Cast input to Brep Geometry");
                  addArea = false;
               }

               if (addArea)
                  gs_area_loads.Add(ll);
            }
         }

         da.SetDataList(0, gs_area_loads);
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("0CB20B1B-AD3C-40E5-B3BD-3E4226BD02B3"); }
      }
   }
}
