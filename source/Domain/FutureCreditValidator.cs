using System;

namespace FlexHours
{
    /// <summary>Future credits must fit both the imported total and the remaining weekdays.</summary>
    public static class FutureCreditValidator
    {
        public static decimal Maximum(DateTime month, DateTime asOf, decimal total)
        {
            var target = WorkHoursCalculator.Calculate(month, asOf, new MonthData());
            return Math.Min(total, target.Target - target.Expected);
        }

        public static void Validate(DateTime month, DateTime asOf, decimal total, decimal credit)
        {
            if (credit < 0 || credit != Math.Truncate(credit) || credit > Maximum(month, asOf, total))
                throw new ArgumentException("Future credits must be whole minutes, cannot exceed the Pay Period total, and must fit the weekdays after the comparison date. Maximum: "
                    + WorkHoursCalculator.Hours(Maximum(month, asOf, total)) + ".");
        }
    }
}
