using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using gh_sofistik.src;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace gh_sofistik
{
   public class CreateSofimshcInput : GH_Component
   {
      private BoundingBox _boundingBox=new BoundingBox();

      public CreateSofimshcInput()
         : base("SOFiMSHC", "SOFiMSHC", "Creates a SOFiMSHC input file", "SOFiSTiK", "Structure")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.sofimshc_24x24; }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("F46E8DA9-205A-4623-8331-8F911C7DA0DC"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGeometryParameter("Structural Elements", "Se", "Collection of SOFiSTiK Structural elements", GH_ParamAccess.list);
         pManager.AddBooleanParameter("Init System", "Init System", "Initializes a new SOFiSTiK calculation system. At least one SOFiMSHC component needs this to be true. If true, all existing system data gets deleted", GH_ParamAccess.item, true);
         pManager.AddBooleanParameter("Create mesh", "Create Mesh", "Activates mesh generation", GH_ParamAccess.item, true);
         pManager.AddNumberParameter("Mesh Density", "Mesh Density", "Sets the maximum element size in [m] (parameter HMIN in SOFiMSHC)", GH_ParamAccess.item, 1.0);
         pManager.AddNumberParameter("Intersection tolerance", "Tolerance", "Geometric intersection tolerance in [m]", GH_ParamAccess.item, 0.01);
         pManager.AddIntegerParameter("Start Index", "Start Index", "Start index for automatically assigned element numbers", GH_ParamAccess.item, 50000);
         pManager.AddTextParameter("Control Values", "Add. Ctrl", "Additional SOFiMSHC control values", GH_ParamAccess.item, string.Empty);
         pManager.AddTextParameter("User Text", "User Text", "Additional text input being placed after the definition of structural elements", GH_ParamAccess.item, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("Text input", "O", "SOFiMSHC text input", GH_ParamAccess.item);
      }

      public override void DrawViewportWires(IGH_PreviewArgs args)
      {
         base.DrawViewportWires(args);

         if (this.Attributes.Selected)
         {
            args.Display.DrawBox(_boundingBox, args.WireColour_Selected);            
         }
      }   

      protected override void SolveInstance(IGH_DataAccess da)
      {
         bool initSystem = da.GetData<bool>(1);
         bool mesh = da.GetData<bool>(2);
         double hmin = da.GetData<double>(3);
         double tolg = da.GetData<double>(4);
         int idBorder = da.GetData<int>(5);
         string ctrl = da.GetData<string>(6);
         string text = da.GetData<string>(7);

         var structural_elements_pre = new List<IGS_StructuralElement>();
         var structural_elements = new List<IGS_StructuralElement>();
         List<GH_GeometricGoo<GH_CouplingStruc>> coupling_information = new List<GH_GeometricGoo<GH_CouplingStruc>>();

         foreach (var it in da.GetDataList<IGH_Goo>(0))
         {
            if (it is IGS_StructuralElement)
               structural_elements_pre.Add(it as IGS_StructuralElement);

            else if (it is GH_GeometricGoo<GH_CouplingStruc>)
               coupling_information.Add(it as GH_GeometricGoo<GH_CouplingStruc>);

            else
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + it.TypeName + " to IGS_StructuralElement.");
         }

         int id = idBorder;
         SortedSet<int> idSet = new SortedSet<int>();
         // assign auto generated IDs temporarily (just for couplings) and write "Id=0" back at end of this method
         List<IGS_StructuralElement> write_id_back_to_zero = new List<IGS_StructuralElement>();
         //id=assignIDs(id, idSet, structural_elements, write_id_back_to_zero);
         addUnknownElementsFromCouplings(id, idSet, structural_elements, coupling_information, write_id_back_to_zero);
         addStructuralElements(idSet, structural_elements, structural_elements_pre);
         
         // build hashmap for couplings: for one id (key), you get a list of couplings in which this structural element is involved
         Dictionary<int, List<GH_GeometricGoo<GH_CouplingStruc>>> couplingMap = buildCouplingMap(coupling_information);

         _boundingBox = new BoundingBox();
         if (structural_elements.Count > 0) {
            IGS_StructuralElement se = structural_elements[0];
            if (se is GS_StructuralPoint)
               _boundingBox = (se as GS_StructuralPoint).Boundingbox;
            else if (se is GS_StructuralLine)
               _boundingBox = (se as GS_StructuralLine).Boundingbox;
            else if (se is GS_StructuralArea)
               _boundingBox = (se as GS_StructuralArea).Boundingbox;
         }

         var sb = new StringBuilder();

         sb.AppendLine("+PROG SOFIMSHC");
         sb.AppendLine("HEAD");
         sb.AppendLine("PAGE UNII 0"); // export always in SOFiSTiK database units

         if(initSystem)
            sb.AppendLine("SYST 3D GDIR NEGZ GDIV -1000");
         else
            sb.AppendLine("SYST REST");

         sb.AppendFormat("CTRL TOLG {0:F6}\n", tolg);
         if (mesh)
         {
            sb.AppendLine("CTRL MESH 1");
            sb.AppendFormat("CTRL HMIN {0:F3}\n", hmin);
         }

         // add control string
         if (!string.IsNullOrEmpty(ctrl))
            sb.Append(ctrl);
         sb.AppendLine();

         // write structural lines
         foreach (var se in structural_elements)
         {
            if (se is GS_StructuralPoint)
            {
               var spt = se as GS_StructuralPoint;
               Point3d p = spt.Value.Location;

               _boundingBox.Union(spt.Boundingbox);

               string id_string = se.Id > 0 ? se.Id.ToString() : "-";

               sb.AppendFormat("SPT {0} X {1:F8} {2:F8} {3:F8}", id_string, p.X, p.Y, p.Z);

               if (spt.DirectionLocalX.Length > 1.0E-8)
                  sb.AppendFormat(" SX {0:F6} {1:F6} {2:F6}", spt.DirectionLocalX.X, spt.DirectionLocalX.Y, spt.DirectionLocalX.Z);

               if (spt.DirectionLocalZ.Length > 1.0E-8)
                  sb.AppendFormat(" NX {0:F6} {1:F6} {2:F6}", spt.DirectionLocalZ.X, spt.DirectionLocalZ.Y, spt.DirectionLocalZ.Z);

               if (string.IsNullOrWhiteSpace(spt.FixLiteral) == false)
                  sb.AppendFormat(" FIX {0}", spt.FixLiteral);

               if (string.IsNullOrWhiteSpace(spt.UserText) == false)
                  sb.AppendFormat(" {0}", spt.UserText);

               sb.AppendLine();

               AppendCouplingInformation(sb, se, couplingMap);
            }
            // write structural lines
            else if (se is GS_StructuralLine)
            {
               var sln = se as GS_StructuralLine;

               _boundingBox.Union(sln.Boundingbox);

               string id_string = se.Id > 0 ? se.Id.ToString() : "-";
               string id_group = sln.GroupId > 0 ? sln.GroupId.ToString() : "-";
               string id_section = sln.SectionId > 0 ? sln.SectionId.ToString() : "-";

               sb.AppendFormat("SLN {0} GRP {1} SNO {2}", id_string, id_group, id_section);

               if (sln.DirectionLocalZ.Length > 1.0E-8)
                  sb.AppendFormat(" DRX {0:F6} {1:F6} {2:F6}", sln.DirectionLocalZ.X, sln.DirectionLocalZ.Y, sln.DirectionLocalZ.Z);
               else
                  sb.AppendFormat(" DRX {0:F6} {1:F6} {2:F6}", 0, 0, -1);

               if (string.IsNullOrWhiteSpace(sln.FixLiteral) == false)
                  sb.AppendFormat(" FIX {0}", sln.FixLiteral);

               sb.AppendFormat(" STYP {0}", sln.ElementType);

               if(Math.Abs(sln.ElementSize) > 1.0E-8)
                  sb.AppendFormat(" SDIV {0}", sln.ElementSize);

               if (string.IsNullOrWhiteSpace(sln.UserText) == false)
                  sb.AppendFormat(" {0}", sln.UserText);

               sb.AppendLine();

               AppendCurveGeometry(sb, sln.Value);

               AppendCouplingInformation(sb, se, couplingMap);
            }
            // write structural areas
            else if (se is GS_StructuralArea)
            {
               var sar = se as GS_StructuralArea;
               var brep = sar.Value;

               _boundingBox.Union(sar.Boundingbox);

               string id_string = se.Id > 0 ? se.Id.ToString() : "-";
               string grp_string = sar.GroupId > 0 ? sar.GroupId.ToString() : "-";
               string thk_string = sar.Thickness.ToString("F6");

               // some preparations
               brep.CullUnusedSurfaces();
               brep.CullUnusedFaces();
               brep.CullUnusedEdges();

               // loop over all faces within the brep
               foreach (var fc in brep.Faces)
               {
                  // checks and preparations
                  fc.ShrinkFace(BrepFace.ShrinkDisableSide.ShrinkAllSides);

                  if (fc.IsClosed(0) || fc.IsClosed(1))
                  {
                     this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A given Surface is closed in one direction.\nSuch surfaces cannot be handled by SOFiMSHC and need to be split.");
                  }

                  // write SAR header
                  sb.AppendLine();
                  sb.AppendFormat("SAR {0} GRP {1} T {2}", id_string, grp_string, thk_string);
                  id_string = string.Empty; // set only the 1st time

                  if (sar.MaterialId > 0)
                     sb.AppendFormat(" MNO {0}", sar.MaterialId.ToString());
                  if (sar.ReinforcementId > 0)
                     sb.AppendFormat(" MRF {0}", sar.ReinforcementId.ToString());
                  if (sar.DirectionLocalX.Length > 1.0E-8)
                  {
                     sb.AppendFormat(" DRX {0:F6} {1:F6} {2:F6}", sar.DirectionLocalX.X, sar.DirectionLocalX.Y, sar.DirectionLocalX.Z);
                  }

                  if(!sar.Alignment.Equals("CENT"))
                     sb.AppendFormat(" QREF {0}", sar.Alignment);

                  if(!sar.MeshOptions.Equals("AUTO"))
                     sb.AppendFormat(" MCTL {0}", sar.MeshOptions);

                  if(Math.Abs(sar.ElementSize) > 1.0E-8)
                     sb.AppendFormat(" H1 {0}", sar.ElementSize);

                  if (string.IsNullOrWhiteSpace(sar.UserText) == false)
                     sb.AppendFormat(" {0}", sar.UserText);

                  sb.AppendLine();

                  // outer boundary
                  AppendSurfaceBoundary(sb, fc);

                  // write geometry
                  if (fc.IsPlanar() == false)
                  {
                     AppendSurfaceGeometry(sb, fc.ToNurbsSurface());
                  }
               }
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unsupported type encountered: " + se.TypeName);
            }
         }
         sb.AppendLine();

         // add additional text
         if (!string.IsNullOrEmpty(text))
         {
            sb.Append(text);
         }
         sb.AppendLine();
         sb.AppendLine("END");


         foreach (IGS_StructuralElement se in write_id_back_to_zero)
            se.Id = 0;


         da.SetData(0, sb.ToString());
      }

      #region Id distribution and preprocessing

      private int assignIDs(int id, SortedSet<int> idSet, List<IGS_StructuralElement> structural_elements, List<IGS_StructuralElement> write_id_back_to_zero)
      {
         // assign IDs to all StructuralElements which go into sofimshc and dont have an id yet
         foreach (IGS_StructuralElement se in structural_elements)
         {
            if (se.Id == 0)
            {
               se.Id = ++id;
               write_id_back_to_zero.Add(se);
            }
            idSet.Add(se.Id);
         }
         return id;
      }

      private void addStructuralElements(SortedSet<int> idSet, List<IGS_StructuralElement> structural_elements, List<IGS_StructuralElement> structural_elements_pre)
      {
         foreach(IGS_StructuralElement se in structural_elements_pre)
         {
            if(se.Id == 0 || !idSet.Contains(se.Id))
            {
               structural_elements.Add(se);
            }
         }
      }

      private int addUnknownElementsFromCouplings(int id, SortedSet<int> idSet, List<IGS_StructuralElement> structural_elements, List<GH_GeometricGoo<GH_CouplingStruc>> coupling_information, List<IGS_StructuralElement> write_id_back_to_zero)
      {
         // add StructuralElements to sofimshc which dont go directly into sofimshc but into something like Coupling and assign IDs if necessary
         foreach (GH_GeometricGoo<GH_CouplingStruc> gg in coupling_information)
         {
            if (gg.Value.Reference_A.Id != 0)
            {
               if (!idSet.Contains(gg.Value.Reference_A.Id))
               {
                  structural_elements.Add(gg.Value.Reference_A);
                  idSet.Add(gg.Value.Reference_A.Id);
               }
            }
            else
            {
               gg.Value.Reference_A.Id = ++id;
               write_id_back_to_zero.Add(gg.Value.Reference_A);
               structural_elements.Add(gg.Value.Reference_A);
               idSet.Add(gg.Value.Reference_A.Id);
            }

            if (!(gg.Value.Reference_B is null))
            {
               if (gg.Value.Reference_B.Id != 0)
               {
                  if (!idSet.Contains(gg.Value.Reference_B.Id))
                  {
                     structural_elements.Add(gg.Value.Reference_B);
                     idSet.Add(gg.Value.Reference_B.Id);
                  }
               }
               else
               {
                  gg.Value.Reference_B.Id = ++id;
                  write_id_back_to_zero.Add(gg.Value.Reference_B);
                  structural_elements.Add(gg.Value.Reference_B);
                  idSet.Add(gg.Value.Reference_B.Id);
               }
            }
         }
         return id;
      }

      private Dictionary<int, List<GH_GeometricGoo<GH_CouplingStruc>>> buildCouplingMap(List<GH_GeometricGoo<GH_CouplingStruc>> cpl_list)
      {
         Dictionary<int, List<GH_GeometricGoo<GH_CouplingStruc>>> map = new Dictionary<int, List<GH_GeometricGoo<GH_CouplingStruc>>>();
         foreach (GH_GeometricGoo<GH_CouplingStruc> gg in cpl_list)
         {
            if (!(gg.Value.Reference_A is null))
            {
               List<GH_GeometricGoo<GH_CouplingStruc>> csList_temp;
               if (!map.TryGetValue(gg.Value.Reference_A.Id, out csList_temp))
               {
                  csList_temp = new List<GH_GeometricGoo<GH_CouplingStruc>>();
                  map[gg.Value.Reference_A.Id] = csList_temp;
               }
               csList_temp.Add(gg);
            }            
         }
         return map;
      }

      #endregion

      #region Coupling processing

      private void AppendCouplingInformation(StringBuilder sb, IGS_StructuralElement se, Dictionary<int, List<GH_GeometricGoo<GH_CouplingStruc>>> map)
      {
         if (se.Id > 0)
         {
            List<GH_GeometricGoo<GH_CouplingStruc>> involvedCouplings;
            if (map.TryGetValue(se.Id, out involvedCouplings))
            {
               if (se is GS_StructuralLine)
                  AppendCouplingInformation_L(sb, involvedCouplings);
               else if (se is GS_StructuralPoint)
                  AppendCouplingInformation_P(sb, involvedCouplings);
            }
         }
      }

      private void AppendCouplingInformation_L(StringBuilder sb, List<GH_GeometricGoo<GH_CouplingStruc>> l)
      {
         foreach (GH_GeometricGoo<GH_CouplingStruc> gg in l)
         {
            if (gg is GH_Coupling)
               AppendCouplingInformation_CPL_Line(sb, (gg as GH_Coupling));
            else if (gg is GH_Elastic_Coupling)
               AppendCouplingInformation_ECPL_Line(sb, (gg as GH_Elastic_Coupling));
            else if (gg is GH_Spring)
               AppendCouplingInformation_SPR_Line(sb, (gg as GH_Spring));
         }
      }

      private void AppendCouplingInformation_P(StringBuilder sb, List<GH_GeometricGoo<GH_CouplingStruc>> l)
      {
         foreach (GH_GeometricGoo<GH_CouplingStruc> gg in l)
         {
            if (gg.Value.Reference_B is GS_StructuralLine)
               this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "no coupling connection possible from point to line. please reverse input sequence. possible connections are: point-point, line-point, line-line");

            if (gg is GH_Coupling)
               AppendCouplingInformation_CPL_Point(sb, (gg as GH_Coupling));
            else if (gg is GH_Elastic_Coupling)
               AppendCouplingInformation_ECPL_Point(sb, (gg as GH_Elastic_Coupling));
            else if (gg is GH_Spring)
               AppendCouplingInformation_SPR_Point(sb, (gg as GH_Spring));
         }
      }

      private void AppendCouplingInformation_CPL_Point(StringBuilder sb, GH_Coupling cpl)
      {
         if (cpl.Value.Reference_B.Id > 0)
         {
            // SPTP takes only points as reference, no lines
            if (cpl.Value.Reference_B is GS_StructuralPoint)
            {

               sb.AppendFormat(" SPTP");

               if (!cpl.FixLiteral.Equals(""))
                  sb.AppendFormat(" {0}", cpl.FixLiteral);
               
               sb.AppendFormat(" REF {0}", cpl.Value.Reference_B.Id);               

               if(cpl.GroupId > 0)
                  sb.AppendFormat(" GRP {0}", cpl.GroupId);

               sb.AppendLine();

            }
         }
      }

      private void AppendCouplingInformation_CPL_Line(StringBuilder sb, GH_Coupling cpl)
      {
         if (cpl.Value.Reference_B.Id > 0)
         {

            sb.AppendFormat(" SLNS");

            if (cpl.GroupId > 0)
               sb.AppendFormat(" GRP {0}", cpl.GroupId);

            if (!cpl.FixLiteral.Equals(""))
               sb.AppendFormat(" FIX {0}", cpl.FixLiteral);

            sb.Append(" REFT");

            if (cpl.Value.IsBCurve)
               sb.Append(" >SLN");
            else
               sb.Append(" >SPT");
            
            sb.AppendFormat(" {0}", cpl.Value.Reference_B.Id);

            sb.AppendLine();

         }
      }

      private void AppendCouplingInformation_ECPL_Point(StringBuilder sb, GH_Elastic_Coupling ecpl)
      {         

         if (ecpl.Value.Reference_B.Id > 0)
         {
            // SPTS takes only points as reference, no lines
            if (ecpl.Value.Reference_B is GS_StructuralPoint)
            {

               sb.AppendFormat(" SPTS REF {0}", ecpl.Value.Reference_B.Id);
               

               //sb.Append(" TYP 'C'");

               sb.AppendFormat(" CP {0}", ecpl.Axial_stiffness);
               sb.AppendFormat(" CM {0}", ecpl.Rotational_stiffness);

               if (!ecpl.Direction.IsTiny())
                  sb.AppendFormat(" DX {0} DY {1} DZ {2}", ecpl.Direction.X, ecpl.Direction.Y, ecpl.Direction.Z);

               if (ecpl.GroupId > 0)
                  sb.AppendFormat(" GRP {0}", ecpl.GroupId);

               sb.AppendLine();

            }
         }
      }
      private void AppendCouplingInformation_ECPL_Line(StringBuilder sb, GH_Elastic_Coupling ecpl)
      {

         if (ecpl.Value.Reference_B.Id > 0)
         {

            sb.AppendFormat(" SLNS");

            if (ecpl.GroupId > 0)
               sb.AppendFormat(" GRP {0}", ecpl.GroupId);

            sb.Append(" REFT");

            if (ecpl.Value.IsBCurve)
               sb.Append(" >SLN");
            else
               sb.Append(" >SPT");
            
            sb.AppendFormat(" {0}", ecpl.Value.Reference_B.Id);


            sb.AppendFormat(" CA {0}", ecpl.Axial_stiffness);
            sb.AppendFormat(" CD {0}", ecpl.Rotational_stiffness);

            if (!ecpl.Direction.IsTiny())
               sb.AppendFormat(" DRX {0} DRY {1} DRZ {2}", ecpl.Direction.X, ecpl.Direction.Y, ecpl.Direction.Z);
            

            sb.AppendLine();

         }
      }

      private void AppendCouplingInformation_SPR_Point(StringBuilder sb, GH_Spring spr)
      {
         sb.Append(" SPTS");

         //sb.Append(" TYP 'C'");

         sb.AppendFormat(" CP {0}", spr.Axial_stiffness);
         sb.AppendFormat(" CM {0}", spr.Rotational_stiffness);

         if (!spr.Direction.IsTiny())
            sb.AppendFormat(" DX {0} DY {1} DZ {2}", spr.Direction.X, spr.Direction.Y, spr.Direction.Z);

         if (spr.GroupId > 0)
            sb.AppendFormat(" GRP {0}", spr.GroupId);

         sb.AppendLine();
      }

      private void AppendCouplingInformation_SPR_Line(StringBuilder sb, GH_Spring spr)
      {
         sb.AppendFormat(" SLNS");

         if (spr.GroupId > 0)
            sb.AppendFormat(" GRP {0}", spr.GroupId);

         sb.AppendFormat(" CA {0}", spr.Axial_stiffness);
         sb.AppendFormat(" CD {0}", spr.Rotational_stiffness);

         if (!spr.Direction.IsTiny())
            sb.AppendFormat(" DRX {0} DRY {1} DRZ {2}", spr.Direction.X, spr.Direction.Y, spr.Direction.Z);

         sb.AppendLine();
      }

      #endregion

      #region CURVE GEOMETRY
      private void AppendCurveGeometry(StringBuilder sb, LineCurve l)
      {
         Point3d pa = l.Line.From;
         Point3d pe = l.Line.To;

         sb.AppendFormat(" SLNB X1 {0:F8} {1:F8} {2:F8} ", pa.X, pa.Y, pa.Z);
         sb.AppendFormat(" X2 {0:F8} {1:F8} {2:F8} ", pe.X, pe.Y, pe.Z);
         sb.AppendLine();
      }

      private void AppendCurveGeometry(StringBuilder sb, ArcCurve a)
      {
         Point3d pa = a.PointAtStart;
         Point3d pe = a.PointAtEnd;
         Point3d pm = a.Arc.Center;
         Vector3d n = a.Arc.Plane.Normal;

         sb.AppendFormat(" SLNB X1 {0:F8} {1:F8} {2:F8} ", pa.X, pa.Y, pa.Z);
         sb.AppendFormat(" X2 {0:F8} {1:F8} {2:F8} ", pe.X, pe.Y, pe.Z);
         sb.AppendFormat(" XM {0:F8} {1:F8} {2:F8} ", pm.X, pm.Y, pm.Z);
         sb.AppendFormat(" NX {0:F8} {1:F8} {2:F8} ", n.X, n.Y, n.Z);
         sb.AppendLine();
      }

      private void AppendCurveGeometry(StringBuilder sb, NurbsCurve n)
      {
         if (n.Knots.Count == 2 && n.Degree == 1) // is a straight line: write as SLNB-record
         {
            var pa = n.PointAtStart;
            var pe = n.PointAtEnd;

            sb.AppendFormat(" SLNB X1 {0:F8} {1:F8} {2:F8}", pa.X, pa.Y, pa.Z);
            sb.AppendFormat(" X2 {0:F8} {1:F8} {2:F8}", pe.X, pe.Y, pe.Z);
            sb.AppendLine();
         }
         else // write as nurbs
         {
            double l = n.GetLength();
            double k0 = n.Knots.First();
            double kn = n.Knots.Last();

            // re-parametrize knot vectors to length of curve
            double scale = 1.0;
            if (Math.Abs(kn - k0) > 1.0E-6)
               scale = l / (kn - k0);

            for (int i = 0; i < n.Knots.Count; ++i)
            {
               double si = (n.Knots[i] - k0) * scale;

               sb.AppendFormat(" SLNN S {0:F8}", si);
               if (i == 0)
                  sb.AppendFormat(" DEGR {0}", n.Degree);
               sb.AppendLine();
            }

            bool first = true;
            foreach (var p in n.Points)
            {
               sb.AppendFormat(" SLNP X {0:F8} {1:F8} {2:F8}", p.Location.X, p.Location.Y, p.Location.Z);
               if (p.Weight != 1.0)
               {
                  sb.AppendFormat(" W {0:F8}", p.Weight);
               }
               if (first)
               {
                  sb.Append(" TYPE NURB");
                  first = false;
               }
               sb.AppendLine();
            }
         }
      }

      private void AppendCurveGeometry(StringBuilder sb, Curve c)
      {
         if (c is LineCurve)
         {
            AppendCurveGeometry(sb, c as LineCurve);
         }
         else if (c is ArcCurve)
         {
            AppendCurveGeometry(sb, c as ArcCurve);
         }
         else if (c is NurbsCurve)
         {
            AppendCurveGeometry(sb, c as NurbsCurve);
         }
         else if (c is PolylineCurve)
         {
            var n = (c as PolylineCurve).ToNurbsCurve();
            if (n != null)
               AppendCurveGeometry(sb, n);
         }
         else if (c is PolyCurve)
         {
            var n = (c as PolyCurve).ToNurbsCurve();
            if (n != null)
               AppendCurveGeometry(sb, n);
         }
         else
         {
            throw new ArgumentException("Encountered curve type is currently not supported: " + c.GetType().ToString());
         }
      }
      #endregion

      #region SURFACE_GEOMETRY
      private void AppendSurfaceBoundary(StringBuilder sb, BrepFace fc)
      {
         foreach (var loop in fc.Loops)
         {
            string type;
            if (loop.LoopType == BrepLoopType.Outer)
               type = "OUT";
            else if (loop.LoopType == BrepLoopType.Inner)
               type = "IN";
            else
               continue;

            foreach (var tr in loop.Trims)
            {
               var ed = tr.Edge;
               if (ed != null)
               {
                  sb.AppendFormat("SARB {0}", type);
                  sb.AppendLine();
                  AppendCurveGeometry(sb, ed.EdgeCurve);
               }
            }
         }
      }

      private void AppendSurfaceGeometry(StringBuilder sb, NurbsSurface ns)
      {
         if (ns == null)
            return;

         double ulength;
         double vlength;

         // reparametrize to real world length to minimize distortion in the map from parameter space to 3D
         if (ns.GetSurfaceSize(out ulength, out vlength))
         {
            ns.SetDomain(0, new Interval(0, ulength));
            ns.SetDomain(1, new Interval(1, vlength));
         }

         // write knot vectors
         bool first = true;
         foreach (var ku in ns.KnotsU)
         {
            sb.AppendFormat(" SARN S {0:F8}", ku);
            if (first)
            {
               sb.AppendFormat(" DEGS {0}", ns.OrderU - 1);
               first = false;
            }
            sb.AppendLine();
         }

         first = true;
         foreach (var kv in ns.KnotsV)
         {
            sb.AppendFormat(" SARN T {0:F8}", kv);
            if (first)
            {
               sb.AppendFormat(" DEGT {0}", ns.OrderV - 1);
               first = false;
            }
            sb.AppendLine();
         }

         // write control points
         for (int i = 0; i < ns.Points.CountV; ++i)
         {
            for (int j = 0; j < ns.Points.CountU; ++j)
            {
               var cpt = ns.Points.GetControlPoint(j, i);
               double w = cpt.Weight;

               sb.AppendFormat(" SARP NURB {0} {1}", j + 1, i + 1);
               sb.AppendFormat(" X {0:F8} {1:F8} {2:F8} {3:F8}", cpt.X / w, cpt.Y / w, cpt.Z / w, w);
               sb.AppendLine();
            }
         }

      }

      #endregion
   }
}

