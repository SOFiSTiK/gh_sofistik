using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;

namespace gh_sofistik
{
   public class AssemblyLoader : GH_AssemblyPriority
   {
      public override GH_LoadingInstruction PriorityLoad()
      {
         bool skip = false;

         var currentAssembly = GetType().Assembly;

         if (currentAssembly.ManifestModule.Name == "gh_sofistik.gha")
         {
            foreach (var folderInfo in Grasshopper.Folders.AssemblyFolders)
            {
               if (System.IO.Directory.Exists(folderInfo.Folder))
               {
                  foreach (var file in System.IO.Directory.GetFiles(folderInfo.Folder))
                  {
                     var fileName = System.IO.Path.GetFileName(file);
                     if (fileName == "sof_gh_sofistik-70.gha" || fileName == "sof_gh_sofistik_rhino.gha")
                        skip = true;
                     if (skip)
                        break;
                  }
               }
               if (skip)
                  break;
            }
         }

         if (!skip)
         {
            createGlobalSOFiSTiKMenu();
            return GH_LoadingInstruction.Proceed;
         }

         return GH_LoadingInstruction.Abort;
      }

      private async void createGlobalSOFiSTiKMenu()
      {
         await Task.Run(() => startTimer());

         var editor = Grasshopper.Instances.DocumentEditor;

         foreach (var control in editor.Controls)
         {
            if (control is System.Windows.Forms.MenuStrip)
            {
               var gh_strip = control as System.Windows.Forms.MenuStrip;
               if (gh_strip.Name == "MainMenu")
               {
                  var sofiItem = new System.Windows.Forms.ToolStripMenuItem("SOFiSTiK");

                  var dropDown = new System.Windows.Forms.ToolStripDropDownMenu();

                  var menuItems = new List<System.Windows.Forms.ToolStripMenuItem>();
                  menuItems.Add(GH_DocumentObject.Menu_AppendItem(dropDown, "Help", OnHelpClick));
                  menuItems.Add(GH_DocumentObject.Menu_AppendItem(dropDown, "About...", OnAboutClick));

                  var maxWidth = 0;
                  foreach (var item in menuItems)
                  {
                     if (item.Width > maxWidth)
                        maxWidth = item.Width;
                     item.AutoSize = false;
                     item.Height = 26;
                  }
                  foreach (var item in menuItems)
                  {
                     item.Width = maxWidth;
                  }

                  sofiItem.DropDown = dropDown;
                  //gh_strip.Items.Insert(5, sofiItem);
                  gh_strip.Items.Add(sofiItem);
               }
            }
         }
      }

      private void OnHelpClick(object sender, EventArgs e)
      {
         System.Diagnostics.Process.Start("https://www.sofistik.de/documentation/2020/en/rhino_interface/grasshopper/index.html");
      }

      private System.Windows.Forms.Form _aboutDlg;

      private void OnAboutClick(object sender, EventArgs e)
      {
         int width = 502;
         int height = 202;
         _aboutDlg = new System.Windows.Forms.Form();
         _aboutDlg.Size = new System.Drawing.Size(width, height);
         _aboutDlg.BackgroundImage = Util.GetBitmap(GetType().Assembly, "gh_sofistik_splash_500x200.png");
         _aboutDlg.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
         _aboutDlg.MouseClick += new System.Windows.Forms.MouseEventHandler(OnAboutDlgClick);

         var ghLocation = Grasshopper.Instances.DocumentEditor.Location;
         var ghSize = Grasshopper.Instances.DocumentEditor.Size;
         double ghMidX = ghLocation.X + ghSize.Width * 0.5;
         double ghMidY = ghLocation.Y + ghSize.Height * 0.5;
         _aboutDlg.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
         _aboutDlg.Left = (int)(ghMidX - width * 0.5);
         _aboutDlg.Top = (int)(ghMidY - height * 0.5);

         _aboutDlg.ShowDialog();
      }

      private void OnAboutDlgClick(object sender, System.Windows.Forms.MouseEventArgs e)
      {
         _aboutDlg.Close();
      }

      private void startTimer()
      {
         var autoEvent = new AutoResetEvent(false);
         var timer = new Timer(checkStatus, autoEvent, 1000, 250);

         autoEvent.WaitOne();
         timer.Dispose();
      }

      private void checkStatus(Object stateInfo)
      {
         var autoEvent = (AutoResetEvent)stateInfo;

         var editor = Grasshopper.Instances.DocumentEditor;

         if (editor != null)
         {
            autoEvent.Set();
         }
      }
   }

   public static class AssemblyHelper
   {
      private static readonly string SOFIINSTALLPATHREGKEY = "Software\\SOFiSTiK\\InstallLocation";
      private static readonly string SOFIINSTALLPATHREGKEYVERSION = "sofistik_2020";

      public static string GetSofistikExecutableDir()
      {
         string installation_path = "";
         var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(SOFIINSTALLPATHREGKEY);
         if (key != null)
         {
            var val = key.GetValue(SOFIINSTALLPATHREGKEYVERSION);
            if (val != null)
            {
               installation_path = val.ToString();
               installation_path = System.IO.Path.GetFullPath(installation_path);
            }
         }
         return installation_path;
      }
   }
}