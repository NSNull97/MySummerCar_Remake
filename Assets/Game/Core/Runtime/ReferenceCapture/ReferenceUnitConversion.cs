using System;

namespace MSC.Core.ReferenceCapture
{
    public static class ReferenceUnitConversion
    {
        public static bool TryConvert(double value, ReferenceUnit from, ReferenceUnit to, out double result)
        {
            result = value;
            if (from == to) return from != ReferenceUnit.Unknown;

            if (TryToBase(value, from, out double baseValue, out UnitFamily fromFamily) &&
                TryFromBase(baseValue, to, out result, out UnitFamily toFamily) && fromFamily == toFamily)
                return true;

            result = 0d;
            return false;
        }

        private static bool TryToBase(double value, ReferenceUnit unit, out double result, out UnitFamily family)
        {
            switch (unit)
            {
                case ReferenceUnit.Millimeter: result = value / 1000d; family = UnitFamily.Length; return true;
                case ReferenceUnit.Centimeter: result = value / 100d; family = UnitFamily.Length; return true;
                case ReferenceUnit.Meter: result = value; family = UnitFamily.Length; return true;
                case ReferenceUnit.Kilometer: result = value * 1000d; family = UnitFamily.Length; return true;
                case ReferenceUnit.MeterPerSecond: result = value; family = UnitFamily.Speed; return true;
                case ReferenceUnit.KilometerPerHour: result = value / 3.6d; family = UnitFamily.Speed; return true;
                case ReferenceUnit.Gram: result = value / 1000d; family = UnitFamily.Mass; return true;
                case ReferenceUnit.Kilogram: result = value; family = UnitFamily.Mass; return true;
                case ReferenceUnit.Second: result = value; family = UnitFamily.Time; return true;
                case ReferenceUnit.Minute: result = value * 60d; family = UnitFamily.Time; return true;
                case ReferenceUnit.Hour: result = value * 3600d; family = UnitFamily.Time; return true;
                case ReferenceUnit.Degree: result = value * Math.PI / 180d; family = UnitFamily.Angle; return true;
                case ReferenceUnit.Radian: result = value; family = UnitFamily.Angle; return true;
                default: result = 0d; family = UnitFamily.Unknown; return false;
            }
        }

        private static bool TryFromBase(double value, ReferenceUnit unit, out double result, out UnitFamily family)
        {
            switch (unit)
            {
                case ReferenceUnit.Millimeter: result = value * 1000d; family = UnitFamily.Length; return true;
                case ReferenceUnit.Centimeter: result = value * 100d; family = UnitFamily.Length; return true;
                case ReferenceUnit.Meter: result = value; family = UnitFamily.Length; return true;
                case ReferenceUnit.Kilometer: result = value / 1000d; family = UnitFamily.Length; return true;
                case ReferenceUnit.MeterPerSecond: result = value; family = UnitFamily.Speed; return true;
                case ReferenceUnit.KilometerPerHour: result = value * 3.6d; family = UnitFamily.Speed; return true;
                case ReferenceUnit.Gram: result = value * 1000d; family = UnitFamily.Mass; return true;
                case ReferenceUnit.Kilogram: result = value; family = UnitFamily.Mass; return true;
                case ReferenceUnit.Second: result = value; family = UnitFamily.Time; return true;
                case ReferenceUnit.Minute: result = value / 60d; family = UnitFamily.Time; return true;
                case ReferenceUnit.Hour: result = value / 3600d; family = UnitFamily.Time; return true;
                case ReferenceUnit.Degree: result = value * 180d / Math.PI; family = UnitFamily.Angle; return true;
                case ReferenceUnit.Radian: result = value; family = UnitFamily.Angle; return true;
                default: result = 0d; family = UnitFamily.Unknown; return false;
            }
        }

        private enum UnitFamily { Unknown, Length, Speed, Mass, Time, Angle }
    }
}
