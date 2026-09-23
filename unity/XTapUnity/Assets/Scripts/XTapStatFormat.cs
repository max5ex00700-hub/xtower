using System;
using System.Globalization;
using System.Text;

public static class XTapStatFormat
{
    public static double SafeAdd(double a, double b)
    {
        if (double.IsNaN(a)) a = 0d;
        if (double.IsNaN(b)) b = 0d;

        if (a >= double.MaxValue || b >= double.MaxValue)
            return double.MaxValue;

        if (a <= -double.MaxValue || b <= -double.MaxValue)
            return -double.MaxValue;

        double result = a + b;

        if (double.IsPositiveInfinity(result))
            return double.MaxValue;
        if (double.IsNegativeInfinity(result))
            return -double.MaxValue;

        return result;
    }

    public static string Compact(int value)
    {
        return Compact((double)value);
    }

    public static string Compact(long value)
    {
        return Compact((double)value);
    }

    public static string Compact(double value)
    {
        if (double.IsNaN(value)) return "0";
        if (value == 0d) return "0";

        if (double.IsPositiveInfinity(value)) value = double.MaxValue;
        if (double.IsNegativeInfinity(value)) value = -double.MaxValue;

        bool negative = value < 0d;
        double scaled = Math.Abs(value);
        int unit = 0;

        while (scaled >= 1000d)
        {
            scaled /= 1000d;
            unit++;
        }

        double display;
        string pattern;

        if (unit == 0)
        {
            display = Math.Floor(scaled);
            pattern = "0";
        }
        else if (scaled >= 100d)
        {
            display = Math.Floor(scaled);
            pattern = "0";
        }
        else if (scaled >= 10d)
        {
            display = Math.Floor(scaled * 10d) / 10d;
            pattern = "0.#";
        }
        else
        {
            display = Math.Floor(scaled * 100d) / 100d;
            pattern = "0.##";
        }

        string number = display.ToString(pattern, CultureInfo.InvariantCulture);
        return (negative ? "-" : "") + number + Suffix(unit);
    }

    static string Suffix(int unit)
    {
        if (unit <= 0) return "";
        if (unit == 1) return "k";
        if (unit == 2) return "m";

        // unit 3 = a, 4 = b ... 28 = z, 29 = aa, 30 = ab ...
        return AlphaSuffix(unit - 2);
    }

    static string AlphaSuffix(int index)
    {
        StringBuilder sb = new StringBuilder();

        while (index > 0)
        {
            index--;
            sb.Insert(0, (char)('a' + (index % 26)));
            index /= 26;
        }

        return sb.ToString();
    }
}
