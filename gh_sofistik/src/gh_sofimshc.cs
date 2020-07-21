using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace gh_sofistik.General
{
   public enum SofistikModelType
   {
      AQUA = 1,
      SofiMSHC = 2,
      SofiLOAD = 3,
      Tendon = 4,
      Analysis = 5,
   }

   public class SofistikModel : IComparable
   {
      public string CadInp { get; set; }
      public SofistikModelType ModelType { get; set; }
      public SofistikModel()
      {
         CadInp = "";
         ModelType = SofistikModelType.AQUA;
      }
      public SofistikModel Duplicate()
      {
         return new SofistikModel()
         {
            CadInp = CadInp,
            ModelType = ModelType,
         };
      }

      public int CompareTo(object obj)
      {
         if(obj is SofistikModel)
         {
            var other = obj as SofistikModel;
            return this.ModelType.CompareTo(other.ModelType);
         }
         return -1;
      }
   }

   public class GH_SofistikModel : GH_Goo<SofistikModel>
   {
      public override bool IsValid => Value != null;

      public override string TypeName => "GH_SofistikModel";

      public override string TypeDescription => "GH_SofistikModel";

      public override IGH_Goo Duplicate()
      {
         return new GH_SofistikModel() { Value = Value.Duplicate() };
      }

      public override string ToString()
      {
         return Value.CadInp;
      }

      public override bool CastTo<Q>(ref Q target)
      {
         if (typeof(Q).IsAssignableFrom(typeof(GH_String)))
         {
            var ghCadinpString = new GH_String(Value.CadInp);
            target = (Q)(object)ghCadinpString;
            return true;
         }
         else
            return base.CastTo(ref target);
      }
   }
}

namespace gh_sofistik.Structure
{

   public class CreateSofimshcInput : GH_Component
   {
      private System.Drawing.Bitmap _icon;
      private BoundingBox _boundingBox = new BoundingBox();
      private string _manualPath = "";

      public CreateSofimshcInput()
         : base("SOFiMSHC", "SOFiMSHC", "Creates a SOFiMSHC input file", "SOFiSTiK", "Structure")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "sofimshc_24x24.png");
            return _icon;
         }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("F46E8DA9-205A-4623-8331-8F911C7DA0DC"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGeometryParameter("Structural Elements", "Se", "Collection of SOFiSTiK Structural elements", GH_ParamAccess.tree);
         pManager.AddBooleanParameter("Init System", "Init System", "Initializes a new SOFiSTiK calculation system. At least one SOFiMSHC component needs this to be true. If true, all existing system data gets deleted", GH_ParamAccess.item, true);
         pManager.AddBooleanParameter("Create mesh", "Create Mesh", "Activates mesh generation", GH_ParamAccess.item, true);
         pManager.AddNumberParameter("Mesh Density", "Mesh Density", "Sets the maximum element size (parameter HMIN in SOFiMSHC)", GH_ParamAccess.item, 0.0);
         pManager.AddNumberParameter("Intersection tolerance", "Tolerance", "Geometric intersection tolerance in [m]", GH_ParamAccess.item, 0.01);
         pManager.AddIntegerParameter("Start Index", "Start Index", "Start index for automatically assigned Structural Element numbers", GH_ParamAccess.item, 1000);
         pManager.AddIntegerParameter("Group Divisor", "Group Divisor", "Group Divisor for assigning Element Numbers to a Group", GH_ParamAccess.item, -1000);
         pManager.AddTextParameter("Control Values", "Add. Ctrl", "Additional SOFiMSHC control values", GH_ParamAccess.list, string.Empty);
         pManager.AddTextParameter("User Text", "User Text", "Additional text input being placed after the definition of Structural Elements", GH_ParamAccess.list, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         //pManager.AddTextParameter("Text input", "O", "SOFiMSHC text input", GH_ParamAccess.item);
         pManager.AddGenericParameter("Text input", "O", "SOFiMSHC text input", GH_ParamAccess.item);
      }

      public override void DrawViewportWires(IGH_PreviewArgs args)
      {
         base.DrawViewportWires(args);

         if (this.Attributes.Selected)
         {
            args.Display.DrawBox(_boundingBox, args.WireColour_Selected);
         }
      }

      public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
      {
         base.AppendAdditionalComponentMenuItems(menu);

         if (string.IsNullOrEmpty(_manualPath))
         {
            var exeDir = AssemblyHelper.GetSofistikExecutableDir();
            if(!string.IsNullOrWhiteSpace(exeDir) && System.IO.Directory.Exists(exeDir))
            {
               var manualPath = System.IO.Path.Combine(exeDir, "sofimshc_1.pdf");
               if (System.IO.File.Exists(manualPath))
               {
                  _manualPath = manualPath;
               }
            }
         }

         if(!string.IsNullOrWhiteSpace(_manualPath) && System.IO.File.Exists(_manualPath))
         {
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Open Manual", Menu_OnOpenManual);
         }
      }

      private void Menu_OnOpenManual(object sender, EventArgs e)
      {
         if (!string.IsNullOrWhiteSpace(_manualPath) && System.IO.File.Exists(_manualPath))
         {
            System.Diagnostics.Process.Start(@_manualPath);
         }
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var ghElements = da.GetDataTree<IGH_GeometricGoo>(0);
         bool initSystem = da.GetData<bool>(1);
         bool mesh = da.GetData<bool>(2);
         double hmin = da.GetData<double>(3);
         double tolg = da.GetData<double>(4);
         int idBorder = da.GetData<int>(5);
         int gdiv = da.GetData<int>(6);
         var ctrlList = da.GetDataList<string>(7);
         var textList = da.GetDataList<string>(8);

         var structural_elements_pre = new List<IGS_StructuralElement>();
         List<GH_GeometricGoo<GH_CouplingStruc>> coupling_information = new List<GH_GeometricGoo<GH_CouplingStruc>>();
         var axis_elements = new List<IGH_Axis>();

         foreach (var it in ghElements.AllData(true))
         {
            if (it is IGS_StructuralElement)
               structural_elements_pre.Add(it as IGS_StructuralElement);

            else if (it is IGH_Axis)
               axis_elements.Add(it as IGH_Axis);

            else if (it is GH_GeometricGoo<GH_CouplingStruc>)
               coupling_information.Add(it as GH_GeometricGoo<GH_CouplingStruc>);

            else
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + it.TypeName + " to IGS_StructuralElement.");
         }

         // calc unit conversion factor and scale-transform
         var unitFactor = Rhino.RhinoMath.UnitScale(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem, Rhino.UnitSystem.Meters);
         var tU = Units.UnitHelper.GetUnitTransformToMeters();
         bool scaleUnit = Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem != Rhino.UnitSystem.Meters;

         // merge Structural Points at same Location according tolg and adjust references in coupling-like elements to point to new merged structural points
         var structural_elements_merged = new List<IGS_StructuralElement>();
         var set_references_back_A = new List<(GH_GeometricGoo<GH_CouplingStruc>, GS_StructuralPoint)>();
         var set_references_back_B = new List<(GH_GeometricGoo<GH_CouplingStruc>, GS_StructuralPoint)>();
         mergeStructuralPoints(coupling_information, structural_elements_pre, tolg, structural_elements_merged, set_references_back_A, set_references_back_B);

         // setup data for id distribution and pre-processing
         var structural_elements = new List<IGS_StructuralElement>();
         int id = idBorder;
         SortedSet<int> idSetPoint = new SortedSet<int>();
         SortedSet<int> idSetLine = new SortedSet<int>();
         // assign auto generated IDs temporarily (just for couplings) and write "Id=0" back at end of this method
         List<IGS_StructuralElement> write_id_back_to_zero = new List<IGS_StructuralElement>();

         //id=assignIDs(id, idSet, structural_elements, write_id_back_to_zero);
         id = addUnknownElementsFromCouplings(id, idSetPoint, idSetLine, structural_elements, coupling_information, write_id_back_to_zero);
         addStructuralElements(idSetPoint, idSetLine, structural_elements, structural_elements_merged);

         // build hashmap for couplings: for one id (key), you get a list of couplings in which this structural element is involved
         Dictionary<int, List<GH_GeometricGoo<GH_CouplingStruc>>> couplingMapPoint = buildCouplingMap(coupling_information, false);
         Dictionary<int, List<GH_GeometricGoo<GH_CouplingStruc>>> couplingMapLine = buildCouplingMap(coupling_information, true);

         // pre-process axis elements and perform check for structural lines if they lie on axis
         var gaxDefinitions = new StringBuilder();
         string gaxId = "AI0";
         var axisAdded = getAxisDefinitions(axis_elements, gaxDefinitions, gaxId, tU);
         var slnReferences = calcSlnReferences(structural_elements, axisAdded, tolg, write_id_back_to_zero, ref id);

         // init boundingbox
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

         // write teddy
         var sb = new StringBuilder();

         sb.AppendLine("+PROG SOFIMSHC");
         sb.AppendLine("HEAD");
         sb.AppendLine("PAGE UNII 0"); // export always in SOFiSTiK database units

         if (initSystem)
            sb.AppendLine("SYST 3D GDIR NEGZ GDIV " + gdiv);
         //sb.AppendLine("SYST 3D GDIR NEGZ GDIV -1000");
         else
            sb.AppendLine("SYST REST");

         sb.AppendFormat("CTRL TOLG {0:F6}\n", tolg);
         if (mesh)
         {
            sb.AppendLine("CTRL MESH 1");
            sb.AppendFormat("CTRL HMIN {0}\n", hmin != 0.0 ? string.Format("{0:F4}", (scaleUnit ? hmin * unitFactor : hmin)) : "-");
            if (axisAdded.Any())
               sb.AppendLine("CTRL TOPO GAXP 0");
         }

         // add control string
         foreach (var ctrl in ctrlList)
            if (!string.IsNullOrEmpty(ctrl))
               sb.AppendLine(ctrl);

         sb.AppendLine();

         // write axis elements
         sb.Append(gaxDefinitions.ToString());

         // write structural elements
         foreach (var se in structural_elements)
         {
            // write structural points
            if (se is GS_StructuralPoint)
            {
               var spt = se as GS_StructuralPoint;
               
               Point3d p = spt.Value.Location;
               if (scaleUnit)
                  p.Transform(tU);

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

               AppendCouplingInformation(sb, se, couplingMapPoint);
            }
            // write structural lines
            else if (se is GS_StructuralLine)
            {
               var sln = se as GS_StructuralLine;

               _boundingBox.Union(sln.Boundingbox);

               string id_string = se.Id > 0 ? se.Id.ToString() : "-";
               string id_group = sln.GroupId > 0 ? sln.GroupId.ToString() : "-";

               string id_section = "-";

               if (sln.SectionIdStart > 0)
               {
                  if (sln.SectionIdEnd == 0 || sln.SectionIdEnd == sln.SectionIdStart)
                     id_section = sln.SectionIdStart.ToString();
                  else
                     id_section = string.Format("\"{0}.{1}\"", sln.SectionIdStart, sln.SectionIdEnd);
               }               

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

               if(slnReferences.TryGetValue(sln.Id, out var refs))
               {
                  sb.AppendFormat(" REF '" + refs.Item1 + "' NPA " + refs.Item2 + " NPE " + refs.Item3);
                  sb.AppendLine();
               }
               else
               {
                  sb.AppendLine();
                  var crv = sln.Value.DuplicateCurve();
                  if (scaleUnit)
                     crv.Transform(tU);
                  AppendCurveGeometry(sb, crv);
               }

               AppendCouplingInformation(sb, se, couplingMapLine);
            }
            // write structural areas
            else if (se is GS_StructuralArea)
            {
               var sar = se as GS_StructuralArea;
               var brep = sar.Value.DuplicateBrep();
               if (scaleUnit)
                  brep.Transform(tU);

               _boundingBox.Union(sar.Boundingbox);

               string id_string = se.Id > 0 ? se.Id.ToString() : "-";
               string grp_string = sar.GroupId > 0 ? sar.GroupId.ToString() : "-";
               var thk_Value = sar.Thickness;
               if (scaleUnit)
                  thk_Value *= unitFactor;
               string thk_string = thk_Value.ToString("F6");

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

                  // write normal
                  var midN = fc.NormalAt(fc.Domain(0).Mid, fc.Domain(1).Mid);
                  if(sar.FlipZ)
                     sb.AppendFormat(" NX {0:F6} {1:F6} {2:F6}", midN.X, midN.Y, midN.Z);
                  else
                     sb.AppendFormat(" NX {0:F6} {1:F6} {2:F6}", -midN.X, -midN.Y, -midN.Z);

                  //
                  if (!sar.Alignment.Equals("CENT"))
                     sb.AppendFormat(" QREF {0}", sar.Alignment);

                  if(!sar.MeshOptions.Equals("AUTO"))
                     sb.AppendFormat(" MCTL {0}", sar.MeshOptions);

                  if(Math.Abs(sar.ElementSize) > 1.0E-8)
                     sb.AppendFormat(" H1 {0}", sar.ElementSize);

                  if (string.IsNullOrWhiteSpace(sar.UserText) == false)
                     sb.AppendFormat(" {0}", sar.UserText);

                  sb.AppendLine();

                  // outer boundary
                  AppendSurfaceBoundary(sb, fc, scaleUnit, unitFactor, sar.ThickessAtEdges);

                  // write geometry
                  if (fc.IsPlanar() == false)
                  {
                     AppendSurfaceGeometry(sb, fc.ToNurbsSurface());
                  }
                  // TODO: write SARC BLIN in case no direction X is given, face is planar and surface 
                  // has been generated along a bridge to force local direction following the boundary curves
               }
            }            
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unsupported type encountered: " + se.TypeName);
            }
         }
         sb.AppendLine();

         // add additional text
         foreach (var text in textList)
            if (!string.IsNullOrEmpty(text))
               sb.AppendLine(text);

         sb.AppendLine();

         sb.AppendLine("END");

         if (mesh && axisAdded.Where(e => e.Item3).Any())
         {
            sb.AppendLine();
            sb.AppendLine("+PROG AQUA");
            sb.AppendLine("HEAD");
            sb.AppendLine("INTE 0");
            sb.AppendLine("END");
         }

         foreach (IGS_StructuralElement se in write_id_back_to_zero)
            se.Id = 0;

         foreach (var tp in set_references_back_A)
            tp.Item1.Value.Reference_A = tp.Item2;

         foreach (var tp in set_references_back_B)
            tp.Item1.Value.Reference_B = tp.Item2;

         // create output objects
         var mshcModel = new gh_sofistik.General.SofistikModel() { CadInp = sb.ToString(), ModelType = gh_sofistik.General.SofistikModelType.SofiMSHC };
         var ghMshcModel = new gh_sofistik.General.GH_SofistikModel() { Value = mshcModel };
         // create GH_Structure and use SetDataTree so output has always one branch with index {0}
         var outStruc = new GH_Structure<gh_sofistik.General.GH_SofistikModel>();
         outStruc.Append(ghMshcModel);
         da.SetDataTree(0, outStruc);
      }

      #region Id distribution and preprocessing

      private void mergeStructuralPoints(List<GH_GeometricGoo<GH_CouplingStruc>> coupling_information, List<IGS_StructuralElement> structural_elements_pre, double tolg, List<IGS_StructuralElement> structural_elements_merged, List<(GH_GeometricGoo<GH_CouplingStruc>, GS_StructuralPoint)> set_references_back_A, List<(GH_GeometricGoo<GH_CouplingStruc>, GS_StructuralPoint)> set_references_back_B)
      {
         // get all spts
         var spts_noId = new List<GS_StructuralPoint>();
         var spts_Id = new List<GS_StructuralPoint>();
         SortedSet<int> idSet = new SortedSet<int>();
         foreach (var cpl in coupling_information)
         {
            if (cpl.Value.Reference_A != null && cpl.Value.Reference_A is GS_StructuralPoint)
            {
               var sptA = cpl.Value.Reference_A as GS_StructuralPoint;
               if (sptA.Id == 0)
               {
                  spts_noId.Add(sptA);
               }
               else
               {
                  if (!idSet.Contains(sptA.Id))
                  {
                     idSet.Add(sptA.Id);
                     spts_Id.Add(sptA);
                  }
               }
            }
            if (cpl.Value.Reference_B != null && cpl.Value.Reference_B is GS_StructuralPoint)
            {
               var sptB = cpl.Value.Reference_B as GS_StructuralPoint;
               if (sptB.Id == 0)
               {
                  spts_noId.Add(sptB);
               }
               else
               {
                  if (!idSet.Contains(sptB.Id))
                  {
                     idSet.Add(sptB.Id);
                     spts_Id.Add(sptB);
                  }
               }
            }
         }
         foreach (var se in structural_elements_pre)
         {
            if (se is GS_StructuralPoint)
            {
               var spt = se as GS_StructuralPoint;
               if (spt.Id == 0)
               {
                  spts_noId.Add(spt);
               }
               else
               {
                  if (!idSet.Contains(spt.Id))
                  {
                     idSet.Add(spt.Id);
                     spts_Id.Add(spt);
                  }
               }
            }
         }

         // merge
         var mergedSpts = new List<GS_StructuralPoint>();
         foreach (var spt in spts_noId)
         {
            bool merged = false;
            foreach (var mSpt in mergedSpts)
            {
               if (mSpt.Value.Location.DistanceTo(spt.Value.Location) < tolg)
               {
                  if (string.IsNullOrEmpty(mSpt.FixLiteral) && !string.IsNullOrEmpty(spt.FixLiteral))
                     mSpt.FixLiteral = spt.FixLiteral;
                  mSpt.UserText += "\n" + spt.UserText;
                  merged = true;
                  break;
               }
            }
            if (!merged)
               mergedSpts.Add(spt.DuplicateGeometry() as GS_StructuralPoint);
         }

         //adjust coupling references to new merged points
         foreach (var cpl in coupling_information)
         {
            if (cpl.Value.Reference_A != null && cpl.Value.Reference_A is GS_StructuralPoint)
            {
               var sptA = cpl.Value.Reference_A as GS_StructuralPoint;
               if (sptA.Id == 0)
               {
                  foreach (var mSpt in mergedSpts)
                  {
                     if (sptA.Value.Location.DistanceTo(mSpt.Value.Location) < tolg)
                     {
                        set_references_back_A.Add((cpl, sptA));
                        cpl.Value.Reference_A = mSpt;
                        break;
                     }
                  }
               }
            }
            if (cpl.Value.Reference_B != null && cpl.Value.Reference_B is GS_StructuralPoint)
            {
               var sptB = cpl.Value.Reference_B as GS_StructuralPoint;
               if (sptB.Id == 0)
               {
                  foreach (var mSpt in mergedSpts)
                  {
                     if (sptB.Value.Location.DistanceTo(mSpt.Value.Location) < tolg)
                     {
                        set_references_back_B.Add((cpl, sptB));
                        cpl.Value.Reference_B = mSpt;
                        break;
                     }
                  }
               }
            }
         }

         structural_elements_merged.AddRange(spts_Id);
         structural_elements_merged.AddRange(mergedSpts);
         foreach (var se in structural_elements_pre)
            if (!(se is GS_StructuralPoint))
               structural_elements_merged.Add(se);
      }

      private List<(string, Curve, bool)> getAxisDefinitions(List<IGH_Axis> axis_elements, StringBuilder axisDefinitions, string id, Transform tU)
      {
         var addedAxis = new List<(string, Curve, bool)>();
         foreach (var ax in axis_elements)
         {
            var res = ax.GetAxisDefinition(axisDefinitions, addedAxis, ref id, tU);
            if (res.Item1 != GH_RuntimeMessageLevel.Blank)
               AddRuntimeMessage(res.Item1, res.Item2);
         }
         return addedAxis;
      }
      
      private Dictionary<int, (string, string, string)> calcSlnReferences(List<IGS_StructuralElement> structural_elements, List<(string, Curve, bool)> addedAxis, double tolg, List<IGS_StructuralElement> write_id_back_to_zero, ref int id) { 

         //collect structural pts
         var sptList = new List<GS_StructuralPoint>();
         foreach (var se in structural_elements)
            if (se is GS_StructuralPoint)
               sptList.Add(se as GS_StructuralPoint);

         //check structural lines if they lie on existing axis, if yes save axis + pointStart/End reference ids
         //check if points at start/end correspond to existing structural pts, if not create them
         //check if structural pts at start/end have ids, if not assign ids
         Dictionary<int, (string, string, string)> slnReferences = new Dictionary<int, (string, string, string)>();
         List<GS_StructuralPoint> addAfter = new List<GS_StructuralPoint>();

         //check if sln lies on existing gax
         foreach (var ax in addedAxis)
         {
            var bbMain = ax.Item2.GetBoundingBox(false);
            bbMain.Max += new Vector3d(0.5, 0.5, 0.5);
            bbMain.Min -= new Vector3d(0.5, 0.5, 0.5);

            foreach (var se in structural_elements)
            {
               if (se is GS_StructuralLine)
               {
                  var sln = se as GS_StructuralLine;

                  var bbSln = sln.Value.GetBoundingBox(false);

                  if (bbMain.Contains(bbSln, true) && Util.IsOnCurve(ax.Item2, sln.Value, tolg))
                  {
                     string axRef = ax.Item1;

                     GS_StructuralPoint sptA = null;
                     GS_StructuralPoint sptE = null;
                     foreach (var spt in sptList)
                     {
                        if (spt.Value.Location.DistanceTo(sln.Value.PointAtStart) < tolg)
                           sptA = spt;
                        if (spt.Value.Location.DistanceTo(sln.Value.PointAtEnd) < tolg)
                           sptE = spt;
                     }
                     if (sptA == null)
                     {
                        sptA = new GS_StructuralPoint();
                        sptA.Value = new Point(sln.Value.PointAtStart);
                        addAfter.Add(sptA);
                        sptList.Add(sptA);
                     }
                     if (sptE == null)
                     {
                        sptE = new GS_StructuralPoint();
                        sptE.Value = new Point(sln.Value.PointAtEnd);
                        addAfter.Add(sptE);
                        sptList.Add(sptE);
                     }
                     if (sptA.Id == 0)
                     {
                        sptA.Id = ++id;
                        write_id_back_to_zero.Add(sptA);
                     }
                     if (sptE.Id == 0)
                     {
                        sptE.Id = ++id;
                        write_id_back_to_zero.Add(sptE);
                     }

                     if (sln.Id == 0)
                     {
                        sln.Id = ++id;
                        write_id_back_to_zero.Add(sln);
                     }

                     if (slnReferences.ContainsKey(sln.Id))
                     {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "SLN " + sln.Id + " lays completely in Axis " + slnReferences[sln.Id].Item1 + " and " + axRef + ". It will be assigned to " + slnReferences[sln.Id].Item1);
                     }
                     else
                     {
                        slnReferences.Add(sln.Id, (axRef, sptA.Id.ToString(), sptE.Id.ToString()));
                     }
                  }
               }
            }
         }
         structural_elements.AddRange(addAfter);
         return slnReferences;
      }

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

      private void addStructuralElements(SortedSet<int> idSetPoint, SortedSet<int> idSetLine, List<IGS_StructuralElement> structural_elements, List<IGS_StructuralElement> structural_elements_pre)
      {
         foreach(IGS_StructuralElement se in structural_elements_pre)
         {
            if(se is GS_StructuralPoint)
            {
               if (se.Id == 0 || !idSetPoint.Contains(se.Id))
                  structural_elements.Add(se);
            }
            else if (se is GS_StructuralLine)
            {
               if (se.Id == 0 || !idSetLine.Contains(se.Id))
                  structural_elements.Add(se);
            }
            else
            {
               structural_elements.Add(se);
            }
         }
      }

      private int addUnknownElementsFromCouplings(int id, SortedSet<int> idSetPoint, SortedSet<int> idSetLine, List<IGS_StructuralElement> structural_elements, List<GH_GeometricGoo<GH_CouplingStruc>> coupling_information, List<IGS_StructuralElement> write_id_back_to_zero)
      {
         // add StructuralElements to sofimshc which dont go directly into sofimshc but into something like Coupling and assign IDs if necessary
         foreach (GH_GeometricGoo<GH_CouplingStruc> gg in coupling_information)
         {
            SortedSet<int> idSet = idSetPoint;
            if (gg.Value.Reference_A is GS_StructuralLine)
               idSet = idSetLine;

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
               idSet = idSetPoint;
               if (gg.Value.Reference_B is GS_StructuralLine)
                  idSet = idSetLine;
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

      private Dictionary<int, List<GH_GeometricGoo<GH_CouplingStruc>>> buildCouplingMap(List<GH_GeometricGoo<GH_CouplingStruc>> cpl_list, bool isLine)
      {
         Dictionary<int, List<GH_GeometricGoo<GH_CouplingStruc>>> map = new Dictionary<int, List<GH_GeometricGoo<GH_CouplingStruc>>>();
         foreach (GH_GeometricGoo<GH_CouplingStruc> gg in cpl_list)
         {
            if (!(gg.Value.Reference_A is null) && ((!isLine && gg.Value.Reference_A is GS_StructuralPoint) || isLine && gg.Value.Reference_A is GS_StructuralLine))
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
               sb.AppendFormat(" CQ {0}", ecpl.Transversal_stiffness);

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
            sb.AppendFormat(" CL {0}", ecpl.Transversal_stiffness);

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
         sb.AppendFormat(" CQ {0}", spr.Transversal_stiffness);

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
         sb.AppendFormat(" CL {0}", spr.Transversal_stiffness);

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
      private void AppendSurfaceBoundary(StringBuilder sb, BrepFace fc, bool scaleUnit, double uFac, List<double> thickness_at_edges = null)
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

            for( int i=0; i< loop.Trims.Count; i++)
            {
               var ed = loop.Trims[i].Edge;
               if (ed != null)
               {
                  Curve curve = ed.EdgeCurve;

                  // check whether curve needs to be trimmed
                  var va = ed.StartVertex;
                  var ve = ed.EndVertex;

                  double t0 = ed.EdgeCurve.Domain.T0;
                  double t1 = ed.EdgeCurve.Domain.T1;

                  ed.EdgeCurve.ClosestPoint(va.Location, out t0);
                  ed.EdgeCurve.ClosestPoint(ve.Location, out t1);

                  if(Math.Abs(t1-t0) > 1.0E-3 &&
                     (Math.Abs(t0-ed.EdgeCurve.Domain.T0) > 1.0E-4 || Math.Abs(t1-ed.EdgeCurve.Domain.T1) > 1.0E-4))
                  {
                     curve = ed.EdgeCurve.Trim(t0, t1);
                  }

                  // write boundary record
                  sb.AppendFormat("SARB {0}", type);

                  if(thickness_at_edges != null) // optional thickness
                  {
                     double thk = i < thickness_at_edges.Count ? (scaleUnit ? thickness_at_edges[i] * uFac : thickness_at_edges[i]) : 0.0;
                     sb.AppendFormat(" T {0:F6}", thk);
                  }

                  sb.AppendLine();
                  AppendCurveGeometry(sb, curve);
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

   public interface IGH_Axis
   {
      (GH_RuntimeMessageLevel, string) GetAxisDefinition(StringBuilder sb, List<(string, Curve, bool)> addedAxis, ref string id, Transform tU);
   }

}