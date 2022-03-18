using System;
using System.Globalization;
using System.Numerics;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroMev.Shared
{
    public class ZMDecimalConverter : JsonConverter<ZMDecimal>
    {
        public override ZMDecimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = JsonSerializer.Deserialize<string>(ref reader, options);
            return ZMDecimal.Parse(s);
        }

        public override void Write(Utf8JsonWriter writer, ZMDecimal value, JsonSerializerOptions options)
        {
            var dto = value.ToString();
            JsonSerializer.Serialize(writer, dto, options);
        }
    }

    public static class ZMDecimalExtensions
    {
        public static decimal? ToUsd(this ZMDecimal value)
        {
            if (value > decimal.MaxValue || value < decimal.MinValue)
                return null;

            return (decimal)value.RoundAwayFromZero(2);
        }

        public static ZMDecimal? Shorten(this ZMDecimal value)
        {
            if (value > decimal.MaxValue || value < decimal.MinValue)
                return null;

            if (value < 1)
                return (decimal)value.RoundAwayFromZero(7);
            else if (value < 10)
                return (decimal)value.RoundAwayFromZero(5);
            else
                return (decimal)value.RoundAwayFromZero(2);
        }

        public static ZMDecimal Pow(this ZMDecimal x, uint y)
        {
            BitArray e = new BitArray(BitConverter.GetBytes(y));
            int t = e.Count;

            ZMDecimal A = 1;
            for (int i = t - 1; i >= 0; --i)
            {
                A *= A;
                if (e[i] == true)
                    A *= x;
            }
            return A;
        }
    }

    /// sourced from the BigDecimal classes in the Netherum .net integrtion library https://github.com/Nethereum/Nethereum under the MIT license
    /// renamed ZMDecimal to avoid clashes with that library, which some projects also reference
    /// broken out of the library to avoid the ZeroMev.Client needing to reference it, as this must be kept light to reduce Blazor download times
    public struct ZMDecimal : IComparable, IComparable<ZMDecimal>
    {
        /// <summary>
        ///     Sets the maximum precision of division operations.
        ///     If AlwaysTruncate is set to true all operations are affected.
        /// </summary>
        public const int Precision = 50;

        public ZMDecimal(ZMDecimal ZMDecimal, bool alwaysTruncate = false) : this(ZMDecimal.Mantissa,
            ZMDecimal.Exponent, alwaysTruncate)
        {
        }

        public ZMDecimal(decimal value, bool alwaysTruncate = false) : this((ZMDecimal)value, alwaysTruncate)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="mantissa"></param>
        /// <param name="exponent">
        ///     The number of decimal units for example (-18). A positive value will be normalised as 10 ^
        ///     exponent
        /// </param>
        /// <param name="alwaysTruncate">
        ///     Specifies whether the significant digits should be truncated to the given precision after
        ///     each operation.
        /// </param>
        public ZMDecimal(BigInteger mantissa, int exponent, bool alwaysTruncate = false) : this()
        {
            Mantissa = mantissa;
            Exponent = exponent;
            NormaliseExponentBiggerThanZero();
            Normalize();
            if (alwaysTruncate)
                Truncate();
        }

        public BigInteger Mantissa { get; internal set; }
        public int Exponent { get; internal set; }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(obj, null) || !(obj is ZMDecimal))
                throw new ArgumentException();
            return CompareTo((ZMDecimal)obj);
        }

        public int CompareTo(ZMDecimal other)
        {
            return this < other ? -1 : (this > other ? 1 : 0);
        }

        public void NormaliseExponentBiggerThanZero()
        {
            if (Exponent > 0)
            {
                Mantissa = Mantissa * BigInteger.Pow(10, Exponent);
                Exponent = 0;
            }
        }

        /// <summary>
        ///     Removes trailing zeros on the mantissa
        /// </summary>
        public void Normalize()
        {
            if (Exponent == 0) return;

            if (Mantissa.IsZero)
            {
                Exponent = 0;
            }
            else
            {
                BigInteger remainder = 0;
                while (remainder == 0)
                {
                    var shortened = BigInteger.DivRem(Mantissa, 10, out remainder);
                    if (remainder != 0)
                        continue;
                    Mantissa = shortened;
                    Exponent++;
                }

                NormaliseExponentBiggerThanZero();
            }
        }

        /// <summary>
        ///     Truncate the number to the given precision by removing the least significant digits.
        /// </summary>
        /// <returns>The truncated number</returns>
        internal ZMDecimal Truncate(int precision = Precision)
        {
            // copy this instance (remember its a struct)
            var shortened = this;
            // save some time because the number of digits is not needed to remove trailing zeros
            shortened.Normalize();
            // remove the least significant digits, as long as the number of digits is higher than the given Precision
            while (shortened.Mantissa.NumberOfDigits() > precision)
            {
                shortened.Mantissa /= 10;
                shortened.Exponent++;
            }

            return shortened;
        }

        /// <summary>
        /// Rounds the number to the specified amount of significant digits.
        /// Midpoints (like 0.5 or -0.5) are rounded away from 0 (e.g. to 1 and -1 respectively).
        /// </summary>
        public ZMDecimal RoundAwayFromZero(int significantDigits)
        {
            if (significantDigits < 0 || significantDigits > 2_000_000_000)
                throw new ArgumentOutOfRangeException(paramName: nameof(significantDigits));

            if (Exponent >= -significantDigits) return this;

            bool negative = this.Mantissa < 0;
            var shortened = negative ? -this : this;
            shortened.Normalize();

            while (shortened.Exponent < -significantDigits)
            {
                shortened.Mantissa = BigInteger.DivRem(shortened.Mantissa, 10, out var rem);
                shortened.Mantissa += rem >= 5 ? +1 : 0;
                shortened.Exponent++;
            }

            return negative ? -shortened : shortened;
        }

        /// <summary>
        ///     Truncate the number, removing all decimal digits.
        /// </summary>
        /// <returns>The truncated number</returns>
        public ZMDecimal Floor()
        {
            return Truncate(Mantissa.NumberOfDigits() + Exponent);
        }

        private static int NumberOfDigits(BigInteger value)
        {
            return value.NumberOfDigits();
        }

        public override string ToString()
        {
            Normalize();
            bool isNegative = Mantissa < 0;

            var s = BigInteger.Abs(Mantissa).ToString();
            if (Exponent != 0)
            {
                var decimalPos = s.Length + Exponent;
                if (decimalPos < s.Length)
                    if (decimalPos >= 0)
                        s = s.Insert(decimalPos, decimalPos == 0 ? "0." : ".");
                    else
                        s = "0." + s.PadLeft(decimalPos * -1 + s.Length, '0');
                else
                    s = s.PadRight(decimalPos, '0');
            }

            return isNegative ? $"-{s}" : s;
        }

        public bool Equals(ZMDecimal other)
        {
            var first = this;
            var second = other;
            first.Normalize();
            second.Normalize();
            return second.Mantissa.Equals(first.Mantissa) && second.Exponent == first.Exponent;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is ZMDecimal && Equals((ZMDecimal)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Mantissa.GetHashCode() * 397) ^ Exponent;
            }
        }

        #region Conversions

        public static implicit operator ZMDecimal(int value)
        {
            return new ZMDecimal(value, 0);
        }

        public static implicit operator ZMDecimal(BigInteger value)
        {
            return new ZMDecimal(value, 0);
        }

        public static implicit operator ZMDecimal(double value)
        {
            var mantissa = (long)value;
            var exponent = 0;
            double scaleFactor = 1;
            while (Math.Abs(value * scaleFactor - (double)mantissa) > 0)
            {
                exponent -= 1;
                scaleFactor *= 10;
                mantissa = (long)(value * scaleFactor);
            }

            return new ZMDecimal(mantissa, exponent);
        }

        public static implicit operator ZMDecimal(decimal value)
        {
            var mantissa = (BigInteger)value;
            var exponent = 0;
            decimal scaleFactor = 1;
            while ((decimal)mantissa != value * scaleFactor)
            {
                exponent -= 1;
                scaleFactor *= 10;
                mantissa = (BigInteger)(value * scaleFactor);
            }

            return new ZMDecimal(mantissa, exponent);
        }

        public static explicit operator double(ZMDecimal value)
        {
            return double.Parse(value.ToString(), CultureInfo.InvariantCulture);
        }

        public static explicit operator float(ZMDecimal value)
        {
            return float.Parse(value.ToString(), CultureInfo.InvariantCulture);
        }

        public static explicit operator decimal(ZMDecimal value)
        {
            return decimal.Parse(value.ToString(), CultureInfo.InvariantCulture);
        }

        public static explicit operator int(ZMDecimal value)
        {
            return Convert.ToInt32((decimal)value);
        }

        public static explicit operator uint(ZMDecimal value)
        {
            return Convert.ToUInt32((decimal)value);
        }

        #endregion

        #region Operators

        public static ZMDecimal operator +(ZMDecimal value)
        {
            return value;
        }

        public static ZMDecimal operator -(ZMDecimal value)
        {
            value.Mantissa *= -1;
            return value;
        }

        public static ZMDecimal operator ++(ZMDecimal value)
        {
            return value + 1;
        }

        public static ZMDecimal operator --(ZMDecimal value)
        {
            return value - 1;
        }

        public static ZMDecimal operator +(ZMDecimal left, ZMDecimal right)
        {
            return Add(left, right);
        }

        public static ZMDecimal operator -(ZMDecimal left, ZMDecimal right)
        {
            return Add(left, -right);
        }

        private static ZMDecimal Add(ZMDecimal left, ZMDecimal right)
        {
            return left.Exponent > right.Exponent
                ? new ZMDecimal(AlignExponent(left, right) + right.Mantissa, right.Exponent)
                : new ZMDecimal(AlignExponent(right, left) + left.Mantissa, left.Exponent);
        }

        public static ZMDecimal operator *(ZMDecimal left, ZMDecimal right)
        {
            return new ZMDecimal(left.Mantissa * right.Mantissa, left.Exponent + right.Exponent);
        }

        public static ZMDecimal operator /(ZMDecimal dividend, ZMDecimal divisor)
        {
            var exponentChange = Precision - (NumberOfDigits(dividend.Mantissa) - NumberOfDigits(divisor.Mantissa));
            if (exponentChange < 0)
                exponentChange = 0;
            dividend.Mantissa *= BigInteger.Pow(10, exponentChange);
            return new ZMDecimal(dividend.Mantissa / divisor.Mantissa,
                dividend.Exponent - divisor.Exponent - exponentChange);
        }

        public static bool operator ==(ZMDecimal left, ZMDecimal right)
        {
            return left.Exponent == right.Exponent && left.Mantissa == right.Mantissa;
        }

        public static bool operator !=(ZMDecimal left, ZMDecimal right)
        {
            return left.Exponent != right.Exponent || left.Mantissa != right.Mantissa;
        }

        public static bool operator <(ZMDecimal left, ZMDecimal right)
        {
            return left.Exponent > right.Exponent
                ? AlignExponent(left, right) < right.Mantissa
                : left.Mantissa < AlignExponent(right, left);
        }

        public static bool operator >(ZMDecimal left, ZMDecimal right)
        {
            return left.Exponent > right.Exponent
                ? AlignExponent(left, right) > right.Mantissa
                : left.Mantissa > AlignExponent(right, left);
        }

        public static bool operator <=(ZMDecimal left, ZMDecimal right)
        {
            return left.Exponent > right.Exponent
                ? AlignExponent(left, right) <= right.Mantissa
                : left.Mantissa <= AlignExponent(right, left);
        }

        public static bool operator >=(ZMDecimal left, ZMDecimal right)
        {
            return left.Exponent > right.Exponent
                ? AlignExponent(left, right) >= right.Mantissa
                : left.Mantissa >= AlignExponent(right, left);
        }

        public static ZMDecimal Parse(string value)
        {
            //todo culture format
            var decimalCharacter = ".";
            var indexOfDecimal = value.IndexOf(".");
            var exponent = 0;
            if (indexOfDecimal != -1)
                exponent = (value.Length - (indexOfDecimal + 1)) * -1;
            var mantissa = BigInteger.Parse(value.Replace(decimalCharacter, ""));
            return new ZMDecimal(mantissa, exponent);
        }

        /// <summary>
        ///     Returns the mantissa of value, aligned to the exponent of reference.
        ///     Assumes the exponent of value is larger than of value.
        /// </summary>
        private static BigInteger AlignExponent(ZMDecimal value, ZMDecimal reference)
        {
            return value.Mantissa * BigInteger.Pow(10, value.Exponent - reference.Exponent);
        }

        #endregion

        #region Additional mathematical functions

        public static ZMDecimal Exp(double exponent)
        {
            var tmp = (ZMDecimal)1;
            while (Math.Abs(exponent) > 100)
            {
                var diff = exponent > 0 ? 100 : -100;
                tmp *= Math.Exp(diff);
                exponent -= diff;
            }

            return tmp * Math.Exp(exponent);
        }

        public static ZMDecimal Pow(double basis, double exponent)
        {
            var tmp = (ZMDecimal)1;
            while (Math.Abs(exponent) > 100)
            {
                var diff = exponent > 0 ? 100 : -100;
                tmp *= Math.Pow(basis, diff);
                exponent -= diff;
            }

            return tmp * Math.Pow(basis, exponent);
        }

        #endregion

        #region Formatting

        public string ToString(string formatSpecifier, IFormatProvider format)
        {
            char fmt = NumberFormatting.ParseFormatSpecifier(formatSpecifier, out int digits);
            if (fmt != 'c' && fmt != 'C')
                throw new NotImplementedException();

            Normalize();
            if (Exponent == 0)
                return Mantissa.ToString(formatSpecifier, format);

            var numberFormatInfo = NumberFormatInfo.GetInstance(format);
            return ZMDecimalFormatter.ToCurrencyString(this, digits, numberFormatInfo);
        }

        #endregion
    }
}
