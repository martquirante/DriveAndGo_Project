using System;

namespace DriveAndGo_API.Helpers
{
    public static class NumberToWordsHelper
    {
        public static string ConvertNumberToWords(decimal number)
        {
            if (number == 0) return "Zero Pesos Only";

            long whole = (long)Math.Floor(number);
            int cents = (int)Math.Round((number - whole) * 100);

            string words = ConvertWholeNumber(whole) + " Pesos";
            if (cents > 0)
            {
                words += " and " + ConvertWholeNumber(cents) + " Cents";
            }
            return words + " Only";
        }

        private static string ConvertWholeNumber(long number)
        {
            if (number == 0) return "Zero";
            if (number < 0) return "Minus " + ConvertWholeNumber(Math.Abs(number));

            string words = "";

            if ((number / 1000000000) > 0)
            {
                words += ConvertWholeNumber(number / 1000000000) + " Billion ";
                number %= 1000000000;
            }

            if ((number / 1000000) > 0)
            {
                words += ConvertWholeNumber(number / 1000000) + " Million ";
                number %= 1000000;
            }

            if ((number / 1000) > 0)
            {
                words += ConvertWholeNumber(number / 1000) + " Thousand ";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += ConvertWholeNumber(number / 100) + " Hundred ";
                number %= 100;
            }

            if (number > 0)
            {
                var unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
                var tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

                if (number < 20)
                    words += unitsMap[number];
                else
                {
                    words += tensMap[number / 10];
                    if ((number % 10) > 0)
                        words += "-" + unitsMap[number % 10];
                }
            }

            return words.Trim();
        }
    }
}
