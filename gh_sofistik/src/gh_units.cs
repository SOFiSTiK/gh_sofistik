using Rhino.Geometry;


namespace gh_sofistik.Units
{
   public enum Unit_Length
   {
      MilliMeters,
      Meters,
      KiloMeters,
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
            case Unit_Length.Meters:
               return Rhino.UnitSystem.Meters;
            case Unit_Length.KiloMeters:
               return Rhino.UnitSystem.Kilometers;
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
            case Rhino.UnitSystem.Meters:
               return Unit_Length.Meters;
            case Rhino.UnitSystem.Kilometers:
               return Unit_Length.KiloMeters;
            case Rhino.UnitSystem.Feet:
               return Unit_Length.Feet;
            case Rhino.UnitSystem.Inches:
               return Unit_Length.Inches;
            case Rhino.UnitSystem.None:
               return Unit_Length.None;
         }
         return Unit_Length.Meters;
      }


      public static string MapToSofiString(Rhino.UnitSystem us)
      {
         switch (us)
         {
            case Rhino.UnitSystem.Kilometers:
               return "km";
            case Rhino.UnitSystem.Meters:
               return "m";
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
            case "km":
               return Unit_Length.KiloMeters;
            case "m":
               return Unit_Length.Meters;
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
            case Unit_Length.KiloMeters:
               return "km";
            case Unit_Length.Meters:
               return "m";
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