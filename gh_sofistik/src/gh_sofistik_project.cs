using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace gh_sofistik.General
{
   public enum ProjectState
   {
      Default,
      StartCalculation,
      CalculationOutputReceived,
      CalculationFinished,
   }

   public class GH_Project_Component : GH_Component
   {
      private Bitmap _icon;

      public Bitmap IconTeddy { get { if (_iconTeddy == null) { _iconTeddy = Util.GetBitmap(GetType().Assembly, "teddy_24x24.png"); } return _iconTeddy; } }
      public Bitmap IconAnimator { get { if (_iconAnimator == null) { _iconAnimator = Util.GetBitmap(GetType().Assembly, "animator_24x24.png"); } return _iconAnimator; } }
      public Bitmap IconGraphic { get { if (_iconGraphic == null) { _iconGraphic = Util.GetBitmap(GetType().Assembly, "wingraf_24x24.png"); } return _iconGraphic; } }
      public Bitmap IconResult { get { if (_iconResult == null) { _iconResult = Util.GetBitmap(GetType().Assembly, "resultviewer_24x24.png"); } return _iconResult; } }
      public Bitmap IconReport { get { if (_iconReport == null) { _iconReport = Util.GetBitmap(GetType().Assembly, "report_browser_24x24.png"); } return _iconReport; } }
      public Bitmap IconOpenExplorer { get { if (_iconOpenExplorer == null) { _iconOpenExplorer = Util.GetBitmap(GetType().Assembly, "open_explorer_24x24.png"); } return _iconOpenExplorer; } }
      private Bitmap _iconTeddy;
      private Bitmap _iconAnimator;
      private Bitmap _iconGraphic;
      private Bitmap _iconResult;
      private Bitmap _iconReport;
      private Bitmap _iconOpenExplorer;

      public bool ExistsDatPath { get { return !string.IsNullOrEmpty(_projectPathDat) && ExistsProjectPath; } }
      public bool ExistsCdbPath { get { return !string.IsNullOrEmpty(_projectPathCdb) && System.IO.File.Exists(_projectPathCdb); } }
      public bool ExistsPlbPath { get { return !string.IsNullOrEmpty(_projectPathPlb) && System.IO.File.Exists(_projectPathPlb); } }
      public bool ExistsProjectPath { get { return !string.IsNullOrEmpty(_projectPath) && System.IO.Directory.Exists(_projectPath); } }

      private string _projectPathDat;
      private string _projectPathCdb;
      private string _projectPathPlb;
      private string _projectPath;

      private string _combinedDat;
      private string _sofiExeDir;

      public ValueWrapper<bool> StreamContent { get; set; } = new ValueWrapper<bool>(false);
      public ValueWrapper<bool> SilentCalc { get; set; } = new ValueWrapper<bool>(false);

      private bool _calculationProcessRunning;

      private ProjectState State;

      private string _errorMessage;

      private static readonly string DATSUFFIX="_model";

      public GH_Project_Component()
         : base("SOFiSTiK Project", "Project", "Create SOFiSTiK project", "SOFiSTiK", "General")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get
         {
            if (_icon == null)
               _icon = Util.GetBitmap(GetType().Assembly, "sofistik_project_24x24.png");
            return _icon;
         }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("6FD21FC9-8BBF-4FF0-8C1F-9F318598FEDC"); }
      }

      public override void CreateAttributes()
      {
         m_attributes = new GH_Project_Attributes(this);
      }

      public override bool Write(GH_IWriter writer)
      {
         writer.SetBoolean("SOF_STREAM_CONTENT", StreamContent.Value);
         writer.SetBoolean("SOF_SILENT_CALC", SilentCalc.Value);
         return base.Write(writer);
      }

      public override bool Read(GH_IReader reader)
      {
         var streamContent = false;
         if (reader.TryGetBoolean("SOF_STREAM_CONTENT", ref streamContent))
            StreamContent.Value = streamContent;
         var silentCalc = false;
         if (reader.TryGetBoolean("SOF_SILENT_CALC", ref silentCalc))
            SilentCalc.Value = silentCalc;
         return base.Read(reader);
      }

      public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
      {
         base.AppendAdditionalComponentMenuItems(menu);

         Menu_AppendSeparator(menu);
         Menu_AppendItem(menu, "Stream", Menu_OnStream, true, StreamContent.Value);
         Menu_AppendItem(menu, "Silent calc", Menu_OnSilent, true, SilentCalc.Value);
         if (ExistsProjectPath)
         {
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Open Explorer", Menu_OnOpenPath, IconOpenExplorer);
         }
         if (ExistsDatPath)
         {
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Teddy", Menu_OnOpenTeddy, IconTeddy);
            Menu_AppendItem(menu, "Calculate", Menu_OnCalculate);
         }
         if (ExistsCdbPath)
         {
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Animator", Menu_OnOpenAnimator, IconAnimator);
            Menu_AppendItem(menu, "Graphic", Menu_OnOpenGraphic, IconGraphic);
            Menu_AppendItem(menu, "Result Viewer", Menu_OnOpenResult, IconResult);
         }
         if (ExistsPlbPath)
         {
            Menu_AppendItem(menu, "Report Browser", Menu_OnOpenReport, IconReport);
         }
      }

      private void Menu_OnStream(Object sender, EventArgs e)
      {
         ToggleStream();
      }
      private void Menu_OnSilent(Object sender, EventArgs e)
      {
         ToggleSilent();
      }
      private void Menu_OnOpenPath(Object sender, EventArgs e)
      {
         OpenPath();
      }
      private void Menu_OnCalculate(Object sender, EventArgs e)
      {
         PrepareCalculation();
      }
      private void Menu_OnOpenTeddy(Object sender, EventArgs e)
      {
         OpenTeddy();
      }
      private void Menu_OnOpenAnimator(Object sender, EventArgs e)
      {
         OpenAnimator();
      }
      private void Menu_OnOpenGraphic(Object sender, EventArgs e)
      {
         OpenGraphic();
      }
      private void Menu_OnOpenResult(Object sender, EventArgs e)
      {
         OpenResult();
      }
      private void Menu_OnOpenReport(Object sender, EventArgs e)
      {
         OpenReport();
      }


      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddGenericParameter("SOFiSTiK Data", "O", "SOFiSTiK calculation data", GH_ParamAccess.tree);
         pManager.AddTextParameter("Project Name", "Name", "SOFiSTiK Project Name. Default is name of GH file.", GH_ParamAccess.item, string.Empty);
         pManager.AddTextParameter("Project Folder", "Folder", "Project folder for calculation. Default is .SOFiSTIK folder at location of GH file", GH_ParamAccess.item, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         pManager.AddTextParameter("Cdb Path", "Cdb Path", "Path to cdb on filesystem", GH_ParamAccess.item);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         if (State == ProjectState.Default)
         {
            var ghSofiDataStruc = da.GetDataTree<IGH_Goo>(0);
            var projectName = da.GetData<string>(1);
            var projectDir = da.GetData<string>(2);

            var currentDoc = Grasshopper.Instances.ActiveCanvas.Document;

            checkExeDir();

            var actualProjectName = getProjectName(projectName, currentDoc.FilePath);
            var actualProjectDir = getProjectDir(projectDir, currentDoc.FilePath);

            if (!string.IsNullOrEmpty(actualProjectName) && !string.IsNullOrEmpty(actualProjectDir))
            {
               var projectPathDat = System.IO.Path.Combine(actualProjectDir, actualProjectName + DATSUFFIX + ".dat");
               var projectPathCdb = System.IO.Path.Combine(actualProjectDir, actualProjectName + ".cdb");
               var projectPathPlb = System.IO.Path.Combine(actualProjectDir, actualProjectName + DATSUFFIX + ".plb");
               if (_projectPathDat != projectPathDat)
               {
                  _projectPathDat = projectPathDat;
                  _projectPathCdb = projectPathCdb;
                  _projectPathPlb = projectPathPlb;
                  _projectPath = actualProjectDir;
                  if (System.IO.File.Exists(projectPathCdb))
                  {
                     da.SetData(0, projectPathCdb);
                  }
               }

               createCombinedDat(ghSofiDataStruc, actualProjectName);

               if (StreamContent.Value)
                  streamDat();
            }
            else
            {
               _projectPathDat = "";
               _projectPathCdb = "";
               _projectPathPlb = "";
               _projectPath = "";
            }
         }
         else if (State == ProjectState.StartCalculation)
         {
            if (!string.IsNullOrEmpty(_projectPathDat))
            {
               streamDat();
               RunCalculation();
            }
         }
         else if (State == ProjectState.CalculationFinished)
         {
            if (!string.IsNullOrEmpty(_errorMessage))
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, _errorMessage);
            _errorMessage = null;

            if (!string.IsNullOrEmpty(_projectPathCdb))
            {
               if (System.IO.File.Exists(_projectPathCdb))
               {
                  da.SetData(0, _projectPathCdb);
               }
            }
         }
         else if (State == ProjectState.CalculationOutputReceived)
         {

         }

         State = ProjectState.Default;
      }

      private void streamDat()
      {
         if (!System.IO.File.Exists(_projectPathDat))
         {
            var streamObj = System.IO.File.Create(_projectPathDat);
            streamObj.Close();
         }

         using (var sw = new System.IO.StreamWriter(_projectPathDat, false))
         {
            sw.Write(_combinedDat);
         }
      }

      public void ToggleStream()
      {
         StreamContent.Value = !StreamContent.Value;
         OnDisplayExpired(true);
      }
      public void ToggleSilent()
      {
         SilentCalc.Value = !SilentCalc.Value;
         OnDisplayExpired(true);
      }
      public void PrepareCalculation()
      {
         State = ProjectState.StartCalculation;
         ExpireSolution(true);
      }
      public void RunCalculation()
      {
         if (_calculationProcessRunning)
         {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Calculation in progress");
         }
         else
         {
            if (SilentCalc.Value)
            {
               runProcess("sps.exe", _projectPathDat, "", true, true);
            }
            else
            {
               runProcess("wps.exe", _projectPathDat, "-run -noclose", true, false);
            }
         }
      }
      public void OpenPath()
      {
         System.Diagnostics.Process.Start(@_projectPath);
      }
      public void OpenTeddy()
      {
         streamDat();
         runProcess("ted.exe", _projectPathDat, "", false, false);
      }
      public void OpenAnimator()
      {
         runProcess("animator.exe", _projectPathCdb, "", false, false);
      }
      public void OpenGraphic()
      {
         runProcess("wingraf.exe", _projectPathCdb, "", false, false);
      }
      public void OpenResult()
      {
         runProcess("resultviewer.exe", _projectPathCdb, "", false, false);
      }
      public void OpenReport()
      {
         runProcess("ursula.exe", _projectPathPlb, "", false, false);
      }

      private void runProcess(string exeFileName, string projectPath, string args, bool calculationTask, bool captureOutput)
      {
         if (!string.IsNullOrEmpty(_sofiExeDir))
         {
            var exeFile = System.IO.Path.Combine(_sofiExeDir, exeFileName);
            if (System.IO.File.Exists(exeFile))
            {
               var processStartInfo = new System.Diagnostics.ProcessStartInfo();
               processStartInfo.CreateNoWindow = true;
               processStartInfo.UseShellExecute = false;
               processStartInfo.Arguments = args + (string.IsNullOrEmpty(args) ? "" : " ") + "\"" + projectPath + "\"";
               processStartInfo.FileName = exeFile;
               if(captureOutput)
                  processStartInfo.RedirectStandardOutput = true;

               var process = new System.Diagnostics.Process();
               process.StartInfo = processStartInfo;

               if (calculationTask)
               {
                  process.EnableRaisingEvents = true;
                  process.Exited += new EventHandler(OnProcessExited);
                  if (captureOutput)
                     process.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(OnProcessInfoReceived);
               }
               if (process.Start())
               {
                  if (calculationTask)
                  {
                     _calculationProcessRunning = true;
                     if (captureOutput)
                        process.BeginOutputReadLine();
                     this.Message = "calculating...";
                  }
               }
               else
               {
                  if (calculationTask)
                  {
                     _calculationProcessRunning = false;
                     this.Message = "";
                  }
                  AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not start " + exeFileName);
               }
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, exeFileName + " not found");
            }
         }
      }

      private void OnProcessInfoReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
      {
         if (!string.IsNullOrEmpty(e.Data))
            Rhino.RhinoApp.InvokeOnUiThread(new Action<string>(calculationInfoUIThread), e.Data);
      }
      private void calculationInfoUIThread(string line)
      {
         if (line.Contains("***"))
         {
            line = line.Replace("***", "").Trim();
            this.Message = line;
            if (line.Contains("error"))
               _errorMessage = "Error in SOFiSTiK calculation";
            State = ProjectState.CalculationOutputReceived;
            ExpireSolution(true);
         }
      }

      private void OnProcessExited(object sender, System.EventArgs e)
      {
         Rhino.RhinoApp.InvokeOnUiThread(new Action(calculationFinishedUIThread), null);
      }

      private void calculationFinishedUIThread()
      {
         _calculationProcessRunning = false;
         this.Message = "";
         State = ProjectState.CalculationFinished;
         ExpireSolution(true);
      }

      private void createCombinedDat(GH_Structure<IGH_Goo> data, string projectName)
      {
         // var modelInfoList = new List<SofistikModel>();
         // var stringList = new List<string>();
         var cadInpList = new List<string>();
         foreach (var goo in data.AllData(true))
         {
            if (goo is GH_SofistikModel)
            {
               // modelInfoList.Add((goo as GH_SofistikModel).Value);
               cadInpList.Add((goo as GH_SofistikModel).Value.CadInp);
            }
            else if (goo is GH_String)
            {
               // stringList.Add((goo as GH_String).Value);
               cadInpList.Add((goo as GH_String).Value);
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Data conversion failed from " + goo.TypeName + " to GH_String.");
            }
         }

         // modelInfoList.Sort();

         // foreach (var modelInfo in modelInfoList)
         //    cadInpList.Add(modelInfo.CadInp);
         // foreach (var s in stringList)
         //    cadInpList.Add(s);

         _combinedDat = "#DEFINE PROJECT = " + projectName + "\n";
         foreach (var cadInp in cadInpList)
            _combinedDat += cadInp + "\n";
      }

      private string getProjectDir(string projectDir, string ghFilePath)
      {
         string res = null;
         if (!string.IsNullOrEmpty(projectDir))
         {
            if (System.IO.Directory.Exists(projectDir))
            {
               res = projectDir;
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Specified project directory does not exist");
            }
         }
         else
         {
            if (!string.IsNullOrEmpty(ghFilePath) && System.IO.File.Exists(ghFilePath))
            {
               var currentDir = System.IO.Directory.GetParent(ghFilePath).FullName;
               res = System.IO.Path.Combine(currentDir, ".SOFiSTiK");
               if (!System.IO.Directory.Exists(res))
               {
                  System.IO.Directory.CreateDirectory(res);
               }
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please save gh project or specify gh_sofistik project folder");
            }
         }
         return res;
      }

      private string getProjectName(string projectName, string ghFilePath)
      {
         string res = null;
         if (!string.IsNullOrEmpty(projectName))
         {
            res = projectName;
         }
         else
         {
            if (!string.IsNullOrEmpty(ghFilePath) && System.IO.File.Exists(ghFilePath))
            {
               res = System.IO.Path.GetFileNameWithoutExtension(ghFilePath);
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please save gh project or specify gh_sofistik project name");
            }
         }
         return res;
      }

      private void checkExeDir()
      {
         if (string.IsNullOrEmpty(_sofiExeDir))
         {
            var exeDir = AssemblyHelper.GetSofistikExecutableDir();
            if (!string.IsNullOrEmpty(exeDir) && System.IO.Directory.Exists(exeDir))
            {
               _sofiExeDir = exeDir;
            }
            else
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No SOFiSTiK installation found");
            }
         }
      }

   }

   public class GH_Project_Attributes : GH_ComponentAttributes
   {
      private int _width;
      private int _height;

      // dont change
      private readonly int COMPONENTPADDINGH = 6;
      private readonly int COMPONENTPADDINGV = 2;

      //
      private readonly int OUTERPADDING = 10;

      private readonly int TOPLABELHEIGHT = 20;

      private readonly int CALCBTNHEIGHT = 20;

      private readonly int CHECKBOXPANELPADDING = 2;
      private readonly int CHECKBOXHEIGHT = 20;
      private int _checkboxPanelHeight;

      private readonly int BUTTONPANELPADDING = 2;
      private readonly int BUTTONSIZE = 24;
      private readonly int _buttonPanelHeight;

      public readonly int TEXTPADDINGTOP = 0;

      private readonly Color INNERBOUNDSCOLOR = Color.FromArgb(50, 50, 50);
      private readonly Color PANELCOLOR = Color.FromArgb(80, 80, 80);

      private Rectangle _innerBounds;
      private Rectangle _topLabelBounds;
      private string _topLabelString;
      private int _topLabelStringWidth;
      private PButton _calcBtn;
      private Rectangle _checkBoxPanelBounds;
      private Rectangle _buttonPanelBounds;

      private PCheckBox _cbStream;
      private PCheckBox _cbSilent;

      private PButton _btnOpenExplorer;
      private PButton _btnTeddy;
      private PButton _btnAnimator;
      private PButton _btnGraphic;
      private PButton _btnResult;
      private PButton _btnReport;

      public GH_Project_Attributes(GH_Project_Component owner) : base(owner)
      {
         _checkboxPanelHeight = 2 * CHECKBOXHEIGHT + 3 * CHECKBOXPANELPADDING;
         _buttonPanelHeight = BUTTONSIZE + 2 * BUTTONPANELPADDING;

         _topLabelString = owner.Name;
         _topLabelStringWidth = GH_FontServer.StringWidth(_topLabelString, GH_FontServer.LargeAdjusted);

         _calcBtn = new PButton("Calculate", owner.PrepareCalculation);

         _cbStream = new PCheckBox("Stream dat", owner.ToggleStream, owner.StreamContent);
         _cbSilent = new PCheckBox("Silent calculation", owner.ToggleSilent, owner.SilentCalc);
         _cbStream.IsEnabled = true;
         _cbSilent.IsEnabled = true;

         _btnOpenExplorer = new PButton(owner.IconOpenExplorer, owner.OpenPath);
         _btnTeddy = new PButton(owner.IconTeddy, owner.OpenTeddy);
         _btnAnimator = new PButton(owner.IconAnimator, owner.OpenAnimator);
         _btnGraphic = new PButton(owner.IconGraphic, owner.OpenGraphic);
         _btnResult = new PButton(owner.IconResult, owner.OpenResult);
         _btnReport = new PButton(owner.IconReport, owner.OpenReport);
      }

      public override void AppendToAttributeTree(List<IGH_Attributes> attributes)
      {
         attributes.Add(this);
         foreach (var p in Owner.Params.Input)
            attributes.Add(p.Attributes);
         foreach (var p in Owner.Params.Output)
            attributes.Add(p.Attributes);
      }

      protected override void Layout()
      {
         var maxWidthInput = 0;
         foreach (var ip in Owner.Params.Input)
         {
            int pWidth = GH_FontServer.StringWidth(ip.NickName, GH_FontServer.StandardAdjusted);
            if (pWidth > maxWidthInput)
               maxWidthInput = pWidth;
         }
         var maxWidthOutput = 0;
         foreach (var op in Owner.Params.Output)
         {
            int pWidth = GH_FontServer.StringWidth(op.NickName, GH_FontServer.StandardAdjusted);
            if (pWidth > maxWidthOutput)
               maxWidthOutput = pWidth;
         }

         _width = 2 * OUTERPADDING + 6 * BUTTONSIZE + 7 * BUTTONPANELPADDING + maxWidthInput + maxWidthOutput + 2 * COMPONENTPADDINGH;
         _height = 5 * OUTERPADDING + TOPLABELHEIGHT + CALCBTNHEIGHT + _checkboxPanelHeight + _buttonPanelHeight + 2 * COMPONENTPADDINGV;

         Bounds = new Rectangle(new Point((int)Pivot.X, (int)Pivot.Y), new Size(_width, _height));

         _innerBounds = new Rectangle(new Point((int)Bounds.Left + maxWidthInput + COMPONENTPADDINGH, (int)Bounds.Top + COMPONENTPADDINGV), new Size((int)Bounds.Width - maxWidthInput - maxWidthOutput - 2 * COMPONENTPADDINGH, (int)Bounds.Height - 2 * COMPONENTPADDINGV));

         LayoutInputParams(Owner, _innerBounds);
         LayoutOutputParams(Owner, _innerBounds);

         //
         _topLabelBounds = new Rectangle(new Point(_innerBounds.Left + OUTERPADDING, _innerBounds.Top + OUTERPADDING), new Size(_innerBounds.Width - 2 * OUTERPADDING, TOPLABELHEIGHT));
         _calcBtn.Bounds = new Rectangle(new Point(_innerBounds.Left + OUTERPADDING, _topLabelBounds.Bottom + OUTERPADDING), new Size(_innerBounds.Width - 2 * OUTERPADDING, CALCBTNHEIGHT));
         _checkBoxPanelBounds = new Rectangle(new Point(_innerBounds.Left + OUTERPADDING, _calcBtn.Bounds.Bottom + OUTERPADDING), new Size(_innerBounds.Width - 2 * OUTERPADDING, _checkboxPanelHeight));
         _buttonPanelBounds = new Rectangle(new Point(_innerBounds.Left + OUTERPADDING, _checkBoxPanelBounds.Bottom + OUTERPADDING), new Size(_innerBounds.Width - 2 * OUTERPADDING, _buttonPanelHeight));

         //
         _cbStream.Bounds = new Rectangle(_checkBoxPanelBounds.Left + CHECKBOXPANELPADDING, _checkBoxPanelBounds.Top + CHECKBOXPANELPADDING, _checkBoxPanelBounds.Width - 2 * CHECKBOXPANELPADDING, CHECKBOXHEIGHT);
         _cbSilent.Bounds = new Rectangle(_checkBoxPanelBounds.Left + CHECKBOXPANELPADDING, _cbStream.Bounds.Bottom + CHECKBOXPANELPADDING, _checkBoxPanelBounds.Width - 2 * CHECKBOXPANELPADDING, CHECKBOXHEIGHT);

         //
         _btnOpenExplorer.Bounds = new Rectangle(_buttonPanelBounds.Left + BUTTONPANELPADDING, _buttonPanelBounds.Top + BUTTONPANELPADDING, BUTTONSIZE, BUTTONSIZE);
         _btnTeddy.Bounds = new Rectangle(_btnOpenExplorer.Bounds.Right + BUTTONPANELPADDING, _buttonPanelBounds.Top + BUTTONPANELPADDING, BUTTONSIZE, BUTTONSIZE);
         _btnAnimator.Bounds = new Rectangle(_btnTeddy.Bounds.Right + BUTTONPANELPADDING, _buttonPanelBounds.Top + BUTTONPANELPADDING, BUTTONSIZE, BUTTONSIZE);
         _btnGraphic.Bounds = new Rectangle(_btnAnimator.Bounds.Right + BUTTONPANELPADDING, _buttonPanelBounds.Top + BUTTONPANELPADDING, BUTTONSIZE, BUTTONSIZE);
         _btnResult.Bounds = new Rectangle(_btnGraphic.Bounds.Right + BUTTONPANELPADDING, _buttonPanelBounds.Top + BUTTONPANELPADDING, BUTTONSIZE, BUTTONSIZE);
         _btnReport.Bounds = new Rectangle(_btnResult.Bounds.Right + BUTTONPANELPADDING, _buttonPanelBounds.Top + BUTTONPANELPADDING, BUTTONSIZE, BUTTONSIZE);

         //
         if (Owner is GH_Project_Component)
         {
            var projectComponent = Owner as GH_Project_Component;
            _btnOpenExplorer.IsEnabled = projectComponent.ExistsProjectPath;
            _calcBtn.IsEnabled = projectComponent.ExistsDatPath;
            _btnTeddy.IsEnabled = projectComponent.ExistsDatPath;
            _btnAnimator.IsEnabled = projectComponent.ExistsCdbPath;
            _btnGraphic.IsEnabled = projectComponent.ExistsCdbPath;
            _btnResult.IsEnabled = projectComponent.ExistsCdbPath;
            _btnReport.IsEnabled = projectComponent.ExistsPlbPath;
         }
      }

      protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
      {
         // Render all the wires that connect the Owner to all its Sources.
         if (channel == GH_CanvasChannel.Wires)
         {
            base.Render(canvas, graphics, channel);
            return;
         }

         // Render the parameter capsule and any additional text on top of it.
         if (channel == GH_CanvasChannel.Objects)
         {
            var palette = GH_Palette.Normal;
            switch (Owner.RuntimeMessageLevel)
            {
               case GH_RuntimeMessageLevel.Warning:
                  palette = GH_Palette.Warning;
                  break;

               case GH_RuntimeMessageLevel.Error:
                  palette = GH_Palette.Error;
                  break;
            }

            RenderComponentCapsule(canvas, graphics);

            //
            doCapsule(graphics, _innerBounds, palette, INNERBOUNDSCOLOR);

            graphics.DrawString(_topLabelString, GH_FontServer.LargeAdjusted, Brushes.White, (int)(_topLabelBounds.Left + _topLabelBounds.Width * 0.5 - _topLabelStringWidth * 0.5), _topLabelBounds.Top + TEXTPADDINGTOP);

            doCapsule(graphics, _calcBtn.Bounds, palette, PANELCOLOR);
            _calcBtn.Draw(graphics);

            //
            doCapsule(graphics, _checkBoxPanelBounds, palette, PANELCOLOR);
            doCapsule(graphics, _buttonPanelBounds, palette, PANELCOLOR);

            //
            _cbStream.Draw(graphics);
            _cbSilent.Draw(graphics);

            //
            _btnOpenExplorer.Draw(graphics);
            _btnTeddy.Draw(graphics);
            _btnAnimator.Draw(graphics);
            _btnGraphic.Draw(graphics);
            _btnResult.Draw(graphics);
            _btnReport.Draw(graphics);
         }
      }

      private static void doCapsule(Graphics g, RectangleF bounds, GH_Palette palette, Color color)
      {
         GH_Capsule capsule = GH_Capsule.CreateCapsule(bounds, palette, 3, 6);
         capsule.Render(g, color);
         capsule.Dispose();
         capsule = null;
      }

      private PGUIElement _mouseDownElement;
      public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
      {
         _mouseDownElement = null;
         var pt = new Point((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y);
         if (e.Button == MouseButtons.Left)
         {
            if (_innerBounds.Contains(pt))
            {
               if (_calcBtn.Bounds.Contains(pt))
               {
                  _mouseDownElement = _calcBtn;
               }
               else if (_checkBoxPanelBounds.Contains(pt))
               {
                  if (_cbStream.Bounds.Contains(pt))
                  {
                     _mouseDownElement = _cbStream;
                  }
                  else if (_cbSilent.Bounds.Contains(pt))
                  {
                     _mouseDownElement = _cbSilent;
                  }
               }
               else if (_buttonPanelBounds.Contains(pt))
               {
                  if (_btnOpenExplorer.Bounds.Contains(pt))
                  {
                     _mouseDownElement = _btnOpenExplorer;
                  }
                  if (_btnTeddy.Bounds.Contains(pt))
                  {
                     _mouseDownElement = _btnTeddy;
                  }
                  else if (_btnAnimator.Bounds.Contains(pt))
                  {
                     _mouseDownElement = _btnAnimator;
                  }
                  else if (_btnGraphic.Bounds.Contains(pt))
                  {
                     _mouseDownElement = _btnGraphic;
                  }
                  else if (_btnResult.Bounds.Contains(pt))
                  {
                     _mouseDownElement = _btnResult;
                  }
                  else if (_btnReport.Bounds.Contains(pt))
                  {
                     _mouseDownElement = _btnReport;
                  }
               }
            }
         }
         if (_mouseDownElement != null)
         {
            _mouseDownElement.OnMouseDown();
            Owner.OnDisplayExpired(true);
            return GH_ObjectResponse.Capture;
         }
         else
         {
            return base.RespondToMouseDown(sender, e);
         }
      }

      public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
      {
         if (e.Button == MouseButtons.Left)
         {
            if (_mouseDownElement != null)
            {
               var pt = new Point((int)e.CanvasLocation.X, (int)e.CanvasLocation.Y);
               if (_mouseDownElement.Bounds.Contains(pt))
               {
                  _mouseDownElement.InvokeAction();
               }
               _mouseDownElement.OnMouseUp();
               _mouseDownElement = null;
               Owner.OnDisplayExpired(true);
               return GH_ObjectResponse.Release;
            }
         }

         return base.RespondToMouseUp(sender, e);
      }

      private abstract class PGUIElement
      {
         public readonly Brush BRUSHDISABLED = new SolidBrush(Color.FromArgb(100, 50, 50, 50));
         public readonly Brush FOREGROUND = Brushes.White;
         public readonly Brush BRUSHACTIVE = new SolidBrush(Color.FromArgb(100, 100, 100, 100));
         public readonly Brush BRUSHNOTACTIVE = new SolidBrush(Color.FromArgb(255, 100, 100, 100));

         public Rectangle Bounds { get; set; }
         public string Text { get; set; } = "";

         public bool IsEnabled { get; set; } = false;
         public bool IsActive { get; set; } = false;

         public Action OnClicked { get; set; }

         public PGUIElement(Action action)
         {
            OnClicked = action;
         }

         public void InvokeAction()
         {
            if (OnClicked != null && IsEnabled)
               OnClicked.Invoke();
         }
         public virtual void OnMouseDown() { }
         public virtual void OnMouseUp() { }
         public abstract void Draw(Graphics g);
      }
      private class PButton : PGUIElement
      {
         public readonly int TEXTPADDINGTOP = 2;
         public readonly int BORDERSIZE = 0;
         public Bitmap Icon { get; set; }
         public int TextWidth { get; set; } = 0;
         public Pen BorderPen { get; set; }
         public PButton(string text, Action action) : base(action)
         {
            Text = text;
            TextWidth = GH_FontServer.StringWidth(text, GH_FontServer.StandardAdjusted);
            BorderPen = new Pen(Color.Black, BORDERSIZE);
         }
         public PButton(Bitmap icon, Action action) : base(action)
         {
            Icon = icon;
            BorderPen = new Pen(Color.Black, BORDERSIZE);
         }
         public override void OnMouseDown()
         {
            IsActive = true;
         }
         public override void OnMouseUp()
         {
            IsActive = false;
         }
         public override void Draw(Graphics g)
         {
            // g.DrawRectangle(BorderPen, Bounds.Left, Bounds.Top, Bounds.Width, Bounds.Height);
            if (Icon == null)
            {
               // g.FillRectangle(BRUSHNOTACTIVE, Bounds.Left + BORDERSIZE, Bounds.Top + BORDERSIZE, Bounds.Width - 2 * BORDERSIZE, Bounds.Height - 2 * BORDERSIZE);
               g.DrawString(Text, GH_FontServer.StandardAdjusted, FOREGROUND, (int)(Bounds.Left + 0.5 * Bounds.Width - 0.5 * TextWidth), Bounds.Top + TEXTPADDINGTOP + BORDERSIZE);
            }
            else
            {
               g.DrawImage(Icon, Bounds.Left + BORDERSIZE, Bounds.Top + BORDERSIZE, Bounds.Width - 2 * BORDERSIZE, Bounds.Height - 2 * BORDERSIZE);
            }
            if (IsActive)
               g.FillRectangle(BRUSHACTIVE, Bounds);
            if(!IsEnabled)
               g.FillRectangle(BRUSHDISABLED, Bounds);
         }
      }
      private class PCheckBox : PGUIElement
      {
         public readonly int SIZE = 8;
         public readonly int THICKNESS = 2;
         public readonly int PADDING = 4;
         public readonly int TEXTPADDINGTOP = 2;
         public Pen CBPen { get; set; }
         public ValueWrapper<bool> ValWrapper;
         public PCheckBox(string text, Action action, ValueWrapper<bool> valueWrapper) : base(action)
         {
            Text = text;
            CBPen = new Pen(FOREGROUND, THICKNESS);
            ValWrapper = valueWrapper;
         }
         public override void Draw(Graphics g)
         {
            g.DrawRectangle(CBPen, Bounds.Left + PADDING, (int)(Bounds.Top + Bounds.Height * 0.5 - SIZE * 0.5), SIZE, SIZE);
            if (ValWrapper.Value)
            {
               g.DrawLine(CBPen, Bounds.Left + PADDING + THICKNESS, (int)(Bounds.Top + Bounds.Height * 0.5 - SIZE * 0.5 + THICKNESS), Bounds.Left + PADDING + SIZE - THICKNESS, (int)(Bounds.Top + Bounds.Height * 0.5 + SIZE * 0.5 - THICKNESS));
               g.DrawLine(CBPen, Bounds.Left + PADDING + THICKNESS, (int)(Bounds.Top + Bounds.Height * 0.5 + SIZE * 0.5 - THICKNESS), Bounds.Left + PADDING + SIZE - THICKNESS, (int)(Bounds.Top + Bounds.Height * 0.5 - SIZE * 0.5 + THICKNESS));
            }
            g.DrawString(Text, GH_FontServer.StandardAdjusted, FOREGROUND, Bounds.Left + 2 * PADDING + SIZE, Bounds.Top + TEXTPADDINGTOP);
            if (!IsEnabled)
               g.FillRectangle(BRUSHDISABLED, Bounds);
         }
      }
   }

   public class ValueWrapper<T> where T : struct
   {
      public T Value { get; set; }
      public ValueWrapper(T value) { this.Value = value; }
   }
}
