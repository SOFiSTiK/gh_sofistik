using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace gh_sofistik
{
   public class Calculate : GH_Component
   {
      private List<string> _text_input = new List<string>();

      private string _file_path = string.Empty;
      private bool _stream_content = false;

      public Calculate()
         : base("CALCULATE", "CALCULATE", "Calculates the given input with SOFiSTiK", "SOFiSTiK", "General")
      { }

      protected override System.Drawing.Bitmap Icon
      {
         get { return Properties.Resources.calculate_24x24; }
      }

      public override Guid ComponentGuid
      {
         get { return new Guid("4043271E-D4CD-4321-8281-2805050159D1"); }
      }

      protected override void RegisterInputParams(GH_InputParamManager pManager)
      {
         //pManager.AddTextParameter("Destination", "D", "Path to file destination", GH_ParamAccess.item, string.Empty);
         pManager.AddTextParameter("SOFiSTiK input", "I", "Input for SOFiSTiK calculation", GH_ParamAccess.list, string.Empty);
      }

      protected override void RegisterOutputParams(GH_OutputParamManager pManager)
      {
         // nothing yet
      }

      public override bool Write(GH_IWriter writer)
      {
         writer.SetString("SOF_FILE_PATH",_file_path);
         writer.SetBoolean("SOF_STREAM_CONTENT", _stream_content);
         return base.Write(writer);
      }

      public override bool Read(GH_IReader reader)
      {
         reader.TryGetString("SOF_FILE_PATH", ref _file_path);
         reader.TryGetBoolean("SOF_STREAM_CONTENT", ref _stream_content);
         return base.Read(reader);
      }

      protected override void SolveInstance(IGH_DataAccess DA)
      {
         _text_input.Clear();
         _text_input.AddRange(DA.GetDataList<string>(0));

         if(_stream_content)
         {
            string dat_file = GetDestinationDatFile();
            WriteDatContentTo(dat_file);
         }
      }

      public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
      {
         base.AppendAdditionalComponentMenuItems(menu);

         Menu_AppendSeparator(menu);
         Menu_AppendItem(menu, "Select Project File", Menu_OnSelectProjectFile,Properties.Resources.folder_open_icon_16x16);
         Menu_AppendItem(menu, "Stream Input", Menu_OnStreamContentClicked, true, _stream_content);
         Menu_AppendSeparator(menu);
         Menu_AppendItem(menu, "Calculate Project", Menu_OnCalculateWPS);
         Menu_AppendItem(menu, "Open Animator", Menu_OnOpenAnimator);
         Menu_AppendItem(menu, "Open Teddy", Menu_OnOpenTeddy);
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

         if(dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
         {
            if(!string.IsNullOrWhiteSpace(dlg.FileName))
            {
               _file_path = dlg.FileName;
            }
         }
      }

      private void Menu_OnCalculateWPS(Object sender, EventArgs e)
      {
         string dat_file = GetDestinationDatFile();

         if(!_stream_content)
         {
            WriteDatContentTo(dat_file);
         }

         // start wps
         var process = new System.Diagnostics.Process();
         process.StartInfo.FileName = "wps.exe";
         process.StartInfo.Arguments = "/B \"" + dat_file + "\"";

         if (!process.Start())
            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unable to start SOFiSTiK WPS.exe");
      }

      private void Menu_OnOpenAnimator(Object sender, EventArgs e)
      {
         string dat_file = GetDestinationDatFile();

         var process = new System.Diagnostics.Process();
         process.StartInfo.FileName = "animator.exe";
         process.StartInfo.Arguments = System.IO.Path.ChangeExtension(dat_file, ".cdb");

         process.Start();
      }

      private void Menu_OnOpenTeddy(Object sender, EventArgs e)
      {
         string dat_file = GetDestinationDatFile();

         var process = new System.Diagnostics.Process();
         process.StartInfo.FileName = "ted.exe";
         process.StartInfo.Arguments = dat_file;

         process.Start();
      }


      private string GetDestinationDatFile()
      {
         string file_path = _file_path;

         if (string.IsNullOrEmpty(file_path))
         {
            file_path = Grasshopper.Instances.ActiveCanvas?.Document?.FilePath ?? string.Empty;

            if (string.IsNullOrEmpty(file_path))
               file_path = System.IO.Path.GetTempFileName();

            file_path = System.IO.Path.ChangeExtension(file_path, ".dat");
         }
         else
         {
            var dir_name = System.IO.Path.GetDirectoryName(file_path);
            if (!System.IO.Directory.Exists(dir_name))
               throw new Exception("Given directory at parameter D does not exist");

            if (string.IsNullOrEmpty(System.IO.Path.GetFileName(file_path)))
               throw new Exception("No valid file name given at parameter D");

            file_path = System.IO.Path.ChangeExtension(file_path, ".dat");
         }

         return file_path;
      }

      private void WriteDatContentTo(string path)
      {
         // stream content into file
         using (var sw = new System.IO.StreamWriter(path, false))
         {
            foreach (var txt in _text_input)
            {
               sw.Write(txt);
               sw.WriteLine();
            }
         }
      }
   }
}
