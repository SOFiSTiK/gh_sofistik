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
         if (index >= list.Count)
         {
            if (list[list.Count - 1] > 0)
               return list[list.Count - 1] + (index - list.Count + 1);
         }
         else if (list.Count > 0)
         {
            return list[index];
         }
         return 0;
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

   static class Util
   {
      public static bool CastCurveTo<Q>(Rhino.Geometry.Curve curve, out Q target)
      {
         if(curve != null)
         {
            // cast to GH_Curve (Caution: this loses all structural information)
            if (typeof(Q).IsAssignableFrom(typeof(GH_Curve)))
            {
               var gc = new GH_Curve(curve);
               target = (Q)(object)gc;
               return true;
            }
            // cast to GH_Line (Caution: this loses all structural information)
            else if (typeof(Q).IsAssignableFrom(typeof(GH_Line)))
            {
               if (curve is Rhino.Geometry.LineCurve)
               {
                  var gl = new GH_Line((curve as Rhino.Geometry.LineCurve).Line);
                  target = (Q)(object)gl;
                  return true;
               }
            }
            // cast to GH_Arc (Caution: this loses all structural information)
            else if (typeof(Q).IsAssignableFrom(typeof(GH_Arc)))
            {
               if (curve is Rhino.Geometry.ArcCurve)
               {
                  var ga = new GH_Arc((curve as Rhino.Geometry.ArcCurve).Arc);
                  target = (Q)(object)ga;
                  return true;
               }
            }
            else
            {
               throw new Exception("Unable to cast to type: " + typeof(Q).ToString());
            }
         }

         target = default(Q);
         return false;
      }
   }
}
