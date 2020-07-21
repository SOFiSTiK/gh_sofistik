using Rhino.Geometry;
using System;

namespace gh_sofistik.Units
{
   public enum Unit_Length
   {
      MilliMeters,
      CentiMeters,
      Meters,
      Feet,
      Inches,
      None,
   }

   public class UnitHelper
   {
      public static Transform GetUnitTransformToMeters()
      {
         var currentUnitSystem = Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem;
         var unitFactor = Rhino.RhinoMath.UnitScale(currentUnitSystem, Rhino.UnitSystem.Meters);
         var tU = Transform.Scale(Point3d.Origin, unitFactor);
         return tU;
      }

      public static Rhino.UnitSystem MapToRhinoUnits(Unit_Length unit)
      {
         switch (unit)
         {
            case Unit_Length.MilliMeters:
               return Rhino.UnitSystem.Millimeters;
            case Unit_Length.CentiMeters:
               return Rhino.UnitSystem.Centimeters;
            case Unit_Length.Meters:
               return Rhino.UnitSystem.Meters;
            case Unit_Length.Feet:
               return Rhino.UnitSystem.Feet;
            case Unit_Length.Inches:
               return Rhino.UnitSystem.Inches;
            case Unit_Length.None:
               return Rhino.UnitSystem.None;
         }
         return Rhino.UnitSystem.Meters;
      }

      public static Unit_Length MapUnits(Rhino.UnitSystem rUnit)
      {
         switch (rUnit)
         {
            case Rhino.UnitSystem.Millimeters:
               return Unit_Length.MilliMeters;
            case Rhino.UnitSystem.Centimeters:
               return Unit_Length.CentiMeters;
            case Rhino.UnitSystem.Meters:
               return Unit_Length.Meters;
            case Rhino.UnitSystem.Feet:
               return Unit_Length.Feet;
            case Rhino.UnitSystem.Inches:
               return Unit_Length.Inches;
            case Rhino.UnitSystem.None:
               return Unit_Length.None;
         }
         return Unit_Length.Meters;
      }

      public static Unit_Length MapFromMeterFactor(double fac)
      {
         if (Math.Abs(fac - 1.0) < 1.0E-6)
            return Unit_Length.Meters;
         if (Math.Abs(fac - 0.01) < 1.0E-6)
            return Unit_Length.CentiMeters;
         if (Math.Abs(fac - 0.001) < 1.0E-6)
            return Unit_Length.MilliMeters;
         if (Math.Abs(fac - 0.3048) < 1.0E-6)
            return Unit_Length.Feet;
         if (Math.Abs(fac - 0.0254) < 1.0E-6)
            return Unit_Length.Inches;
         return Unit_Length.None;
      }

      public static int MapToSofiUnitSet(Rhino.UnitSystem us)
      {
         switch (us)
         {
            case Rhino.UnitSystem.Millimeters:
               return 6;
            case Rhino.UnitSystem.Centimeters:
               return 1;
            case Rhino.UnitSystem.Meters:
               return 0;
            case Rhino.UnitSystem.Inches:
               return 9;
         }
         return -1;
      }

      public static Unit_Length MapFromSofiUnitSet(int unitSet)
      {
         switch (unitSet)
         {
            case 0:
               return Unit_Length.Meters;
            case 1:
               return Unit_Length.CentiMeters;
            case 6:
               return Unit_Length.MilliMeters;
            case 9:
               return Unit_Length.Inches;
         }
         return Unit_Length.None;
      }

      public static string MapToSofiString(Rhino.UnitSystem us)
      {
         switch (us)
         {
            case Rhino.UnitSystem.Kilometers:
               return "km";
            case Rhino.UnitSystem.Meters:
               return "m";
            case Rhino.UnitSystem.Centimeters:
               return "cm";
            case Rhino.UnitSystem.Millimeters:
               return "mm";
            case Rhino.UnitSystem.Miles:
               return "mi";
            case Rhino.UnitSystem.Feet:
               return "ft";
            case Rhino.UnitSystem.Inches:
               return "in";
            case Rhino.UnitSystem.Yards:
               return "yd";
         }
         return null;
      }

      public static Unit_Length MapToUnits(string unitString)
      {
         if (string.IsNullOrEmpty(unitString))
            return Unit_Length.None;
         switch (unitString.Trim().ToLower())
         {
            case "m":
               return Unit_Length.Meters;
            case "cm":
               return Unit_Length.CentiMeters;
            case "mm":
               return Unit_Length.MilliMeters;
            case "ft":
               return Unit_Length.Feet;
            case "in":
               return Unit_Length.Inches;
         }
         return Unit_Length.None;
      }

      public static string MapToString(Unit_Length unit)
      {
         switch (unit)
         {
            case Unit_Length.Meters:
               return "m";
            case Unit_Length.CentiMeters:
               return "cm";
            case Unit_Length.MilliMeters:
               return "mm";
            case Unit_Length.Feet:
               return "ft";
            case Unit_Length.Inches:
               return "in";
         }
         return null;
      }
   }

}