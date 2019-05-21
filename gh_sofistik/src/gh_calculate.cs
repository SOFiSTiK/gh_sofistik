using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace gh_sofistik
{
   public class TextStream : GH_Component
   {
      private string _sofistik_exe_path = string.Empty;
      private string _sofistik_project_name = string.Empty;
      private string _path_by_dialog = string.Empty;
      private string _path_active = string.Empty;
      private bool _stream_content = true;
      private bool _run_immediately = false;

      public TextStream()
         : base("Text File", "TextFile", "Streams the given input to a text file (e.g. SOFiSTiK *.dat input)", "SOFiSTiK", "General")
      { }

      public override bool IsPreviewCapable
      {
         get { return false; }
      }

      public override bool IsBakeCapable
      {
         get { return false; }
      }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.file_dat_24x24; }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("4043271E-D4CD-4321-8281-2805050159D1"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         pManager.AddTextParameter("Text Input", "Txt", "Text input, e.g for SOFiSTiK calculation", GH_ParamAccess.list);
         pManager.AddTextParameter("File Path", "File Path", "Path to Text File", GH_ParamAccess.item, string.Empty);
         pManager.AddTextParameter("SOFiSTiK Binary Path", "SOFiSTiK Path", "Path to SOFiSTiK Binaries", GH_ParamAccess.item, string.Empty);
         pManager.AddTextParameter("SOFiSTiK Project Name", "Project Name", "Sets SOFiSTiK 'project' Variable", GH_ParamAccess.item, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         // nothing yet
      }

      public override bool Write(GH_IWriter writer)
      {
         writer.SetString("SOF_FILE_PATH",_path_by_dialog);
         writer.SetBoolean("SOF_STREAM_CONTENT", _stream_content);
         return base.Write(writer);
      }

      public override bool Read(GH_IReader reader)
      {
         reader.TryGetString("SOF_FILE_PATH", ref _path_by_dialog);
         reader.TryGetBoolean("SOF_STREAM_CONTENT", ref _stream_content);
         return base.Read(reader);
      }

      protected override void SolveInstance(IGH_DataAccess da)
      {
         var text_input = da.GetDataList<string>(0);

         var file_name = da.GetData<string>(1);
         if (string.IsNullOrEmpty(file_name)) // take from local variable set by dialog
         {
            file_name = _path_by_dialog;

            if(string.IsNullOrEmpty(file_name))
            {
               AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No file name specified");
               return;
            }
         }

         // check directory
         var dir_name = System.IO.Path.GetDirectoryName(file_name);
         if (!System.IO.Directory.Exists(dir_name))
         {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Given file directory does not exist!");
            return;
         }

         // Optional path to SOFiSTiK binaries..
         var exe_path = da.GetData<string>(2);
         // check directory
         if (!string.IsNullOrEmpty(exe_path) & !System.IO.Directory.Exists(exe_path))
         {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Given directory for SOFiSTiK binaries does not exist!");
            return;
         }
         _sofistik_exe_path = exe_path;

         // Optional PROJECT variable..
         var project = da.GetData<string>(3);
         if (string.IsNullOrEmpty(project))
         {
            project = System.IO.Path.GetFileNameWithoutExtension(file_name);
         }
         _sofistik_project_name = project;
         
         // write to file
         var file_exists = System.IO.File.Exists(file_name);
         if(_stream_content || file_exists == false)
         {
            WriteDatContentTo(file_name, text_input);
         }

         _path_active = file_name;

         if (_run_immediately)
         {
            RunCalculation();
         }
      }


      public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
      {
         base.AppendAdditionalComponentMenuItems(menu);

         Menu_AppendSeparator(menu);
         // Menu_AppendItem(menu, "Select Project File", Menu_OnSelectProjectFile,Properties.Resources.folder_open_icon_16x16);
         Menu_AppendItem(menu, "Stream Input", Menu_OnStreamContentClicked, true, _stream_content);
         Menu_AppendSeparator(menu);
         Menu_AppendItem(menu, "Run Immediately", Menu_OnRunImmediatelyClicked, true, _run_immediately);

         if (System.IO.Path.GetExtension(_path_active).ToLower() == ".dat")
         {
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Calculate Project", Menu_OnCalculateWPS);
            Menu_AppendItem(menu, "System Visualisation", Menu_OnOpenAnimator);
            Menu_AppendItem(menu, "Open Teddy", Menu_OnOpenTeddy);
         }
      }

      private void Menu_OnStreamContentClicked(Object sender, EventArgs e)
      {
         if(sender is System.Windows.Forms.ToolStripMenuItem)
         {
            var mi = sender as System.Windows.Forms.ToolStripMenuItem;
            _stream_content = !mi.Checked;
         }
         else // fallback
         {
            _stream_content = !_stream_content;
         }
      }

      private void Menu_OnRunImmediatelyClicked(Object sender, EventArgs e)
      {
         if (sender is System.Windows.Forms.ToolStripMenuItem)
         {
            var mi = sender as System.Windows.Forms.ToolStripMenuItem;
            _run_immediately = !mi.Checked;
         }
         else // fallback
         {
            _run_immediately = !_stream_content;
         }
      }

      private void Menu_OnSelectProjectFile(Object sender, EventArgs e)
      {
         var project_path = Grasshopper.Instances.ActiveCanvas?.Document?.FilePath ?? string.Empty;
         var project_dir = System.IO.Path.GetDirectoryName(project_path);

         var dlg = new System.Windows.Forms.OpenFileDialog
         {
            Title = "Select SOFiSTiK Input File",
            InitialDirectory = project_dir,
            DefaultExt = "dat",
            Filter = "SOFiSTiK Input Files (*.dat)|*.dat|All Files (*.*)|*.*",
            FilterIndex = 1,
            CheckFileExists = false,
            CheckPathExists = true,
            Multiselect = false
         };

         if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
         {
            if (!string.IsNullOrWhiteSpace(dlg.FileName))
            {
               _path_by_dialog = dlg.FileName;
            }
         }
      }

      private void Menu_OnCalculateWPS(Object sender, EventArgs e)
      {
         if (string.IsNullOrEmpty(_sofistik_exe_path))
         {
            MessageBox.Show("SOFiSTiK Binary path must be provided for calculation!", "SOFiSTiK");
            return;
         }

         var dat_path = System.IO.Path.ChangeExtension(_path_active, ".dat");
         if (System.IO.File.Exists(dat_path))
         {
            var process = new System.Diagnostics.Process();
            var executable = _sofistik_exe_path + "wps.exe";
            var arguments = "\"" + dat_path + "\"";
            process.StartInfo.FileName = executable;
            process.StartInfo.Arguments = arguments;
            if (!process.Start())
               MessageBox.Show("Unable to open SOFiSTiK WPS.\n" + executable, "SOFiSTiK");
         }
         else
            MessageBox.Show("SOFiSTiK dat file to be calculated does not exist:\n" + dat_path, "SOFiSTiK");
      }

      private void RunCalculation()
      {
         if (string.IsNullOrEmpty(_sofistik_exe_path))
         {
            MessageBox.Show("SOFiSTiK Binary path must be provided for calculation!", "SOFiSTiK");
            return;
         }

         var dat_path = System.IO.Path.ChangeExtension(_path_active, ".dat");
         if (System.IO.File.Exists(dat_path))
         {
            var process = new System.Diagnostics.Process();
            var executable = _sofistik_exe_path + "wps.exe";
            var arguments = "-b -run -noclose " + "\"" + dat_path + "\"";
            process.StartInfo.FileName = executable;
            process.StartInfo.Arguments = arguments;
            if (!process.Start())
               MessageBox.Show("Unable to open SOFiSTiK WPS.\n" + executable, "SOFiSTiK");
         }
         else
            MessageBox.Show("SOFiSTiK dat file to be calculated does not exist:\n" + dat_path, "SOFiSTiK");
      }

      private void Menu_OnOpenAnimator(Object sender, EventArgs e)
      {
         var cdb_path = System.IO.Path.ChangeExtension(_path_active, ".cdb");
         if (System.IO.File.Exists(cdb_path))
         {
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = cdb_path;

            if (!process.Start())
               MessageBox.Show("Unable to open SOFiSTiK Animator?Is it already installed and connected with cdb files?", "SOFiSTiK");
         }
         else
               MessageBox.Show("SOFiSTiK cdb file to be opened does not exist:\n" + cdb_path, "SOFiSTiK");
      }

      private void Menu_OnOpenTeddy(Object sender, EventArgs e)
      {
         if (string.IsNullOrEmpty(_sofistik_exe_path))
         {
            MessageBox.Show("SOFiSTiK Binary path must be provided to open Teddy!", "SOFiSTiK");
            return;
         }

         var dat_path = System.IO.Path.ChangeExtension(_path_active, ".dat");
         if (System.IO.File.Exists(dat_path))
         {
            var process = new System.Diagnostics.Process();
            var executable = _sofistik_exe_path + "ted.exe";
            var arguments = "\"" + dat_path + "\"";
            process.StartInfo.FileName = executable;
            process.StartInfo.Arguments = arguments;
            if (!process.Start())
               MessageBox.Show("Unable to open SOFiSTiK Text Editor. Is SOFiSTiK already installed?\n" + executable, "SOFiSTiK");
         }
         else
         { 
            MessageBox.Show("SOFiSTiK dat file to be opened does not exist:\n" + dat_path, "SOFiSTiK");
         }
      }

      private void WriteDatContentTo(string path, List<string> text_input)
      {
         // stream content into file
         using (var sw = new System.IO.StreamWriter(path, false))
         {
            if (!string.IsNullOrEmpty(_sofistik_project_name))
            {
               sw.Write("#define project=" + _sofistik_project_name +"\n");
            }
            foreach (var txt in text_input)
            {
               sw.Write(txt);
               sw.WriteLine();
            }
         }
      }
   }
}
