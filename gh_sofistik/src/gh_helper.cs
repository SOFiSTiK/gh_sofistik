using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;

namespace gh_sofistik
{
   // extends linq queries
   static class LinqExtension
   {
      public static T GetItemOrLast<T>(this IList<T> list, int index)
      {
         if (list.Count == 0)
            return default(T);
         else if (index >= list.Count)
            return list[list.Count - 1];
         else
            return list[index];
      }

      public static int GetItemOrCountUp(this IList<int> list, int index)
      {
         if (list.Count == 0)
            return 0;
         else if (index >= list.Count)
            return list[list.Count - 1] + (index - list.Count + 1);
         else
            return list[index];
      }
   }

   // extends the IGH interface
   static class IGH_Extension
   {
      // extends the GetData method
      public static T GetData<T>(this IGH_DataAccess da, int index)
      {
         var item = default(T);

         da.GetData(index, ref item);

         return item;
      }

      // extension reads data and returns type
      public static List<T> GetDataList<T>(this IGH_DataAccess da, int index)
      {
         var list = new List<T>();

         da.GetDataList(index, list);

         return list;
      }

      // extension reads data and returns type
      public static GH_Structure<T> GetDataTree<T>(this IGH_DataAccess da, int index) where T : IGH_Goo
      {
         var geometry = new GH_Structure<T>();

         da.GetDataTree(index, out geometry);

         return geometry;
      }
   }
}
