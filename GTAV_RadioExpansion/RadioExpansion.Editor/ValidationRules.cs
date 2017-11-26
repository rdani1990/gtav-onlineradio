using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;

namespace RadioExpansion.Editor
{
    public class PercentValidationRule : ValidationRule
    {
        private const ushort UpperLimit = 1000;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (IsValid(value))
            {
                return ValidationResult.ValidResult;
            }
            else
            {
                return new ValidationResult(false, $"Illegal value, please enter a non-negative number, which is not more than {UpperLimit}!");
            }
        }

        public static bool IsValid(object value)
        {
            ushort parsedResult;
            return UInt16.TryParse((string)value, out parsedResult) && parsedResult <= UpperLimit;
        }
    }
}
