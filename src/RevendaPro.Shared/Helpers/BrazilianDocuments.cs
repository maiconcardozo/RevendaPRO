namespace RevendaPro.Shared.Helpers
{
    /// <summary>
    /// Validates Brazilian documents by their check digits.
    ///
    /// The input mask is cosmetic: it formats, it does not validate. A CPF such as
    /// 111.111.111-11 passes any mask and is invalid. This is what decides.
    /// </summary>
    public static class BrazilianDocuments
    {
        /// <summary>Keeps only the digits of the value.</summary>
        /// <param name="value">Raw value, masked or not.</param>
        /// <returns>The digits, or an empty string.</returns>
        public static string DigitsOnly(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : new string(value.Where(char.IsDigit).ToArray());

        /// <summary>Whether the value is a valid CPF.</summary>
        /// <param name="value">CPF, masked or not.</param>
        /// <returns>True when the check digits match.</returns>
        public static bool IsValidCpf(string? value)
        {
            var cpf = DigitsOnly(value);

            if (cpf.Length != 11 || cpf.All(d => d == cpf[0]))
            {
                return false;
            }

            return CheckDigit(cpf, 9, 10) == cpf[9] && CheckDigit(cpf, 10, 11) == cpf[10];
        }

        /// <summary>Whether the value is a valid CNPJ.</summary>
        /// <param name="value">CNPJ, masked or not.</param>
        /// <returns>True when the check digits match.</returns>
        public static bool IsValidCnpj(string? value)
        {
            var cnpj = DigitsOnly(value);

            if (cnpj.Length != 14 || cnpj.All(d => d == cnpj[0]))
            {
                return false;
            }

            int[] firstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
            int[] secondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

            return DigitByWeights(cnpj, firstWeights) == cnpj[12]
                   && DigitByWeights(cnpj, secondWeights) == cnpj[13];
        }

        /// <summary>Accepts CPF or CNPJ. Empty is valid: the field can be optional.</summary>
        /// <param name="value">Document, masked or not.</param>
        /// <returns>True when empty or valid.</returns>
        public static bool IsValidCpfOrCnpj(string? value)
        {
            var digits = DigitsOnly(value);

            return digits.Length switch
            {
                0 => true,
                11 => IsValidCpf(digits),
                14 => IsValidCnpj(digits),
                _ => false
            };
        }

        /// <summary>Landline (10) or mobile (11) with area code. Empty is valid.</summary>
        /// <param name="value">Phone, masked or not.</param>
        /// <returns>True when empty or valid.</returns>
        public static bool IsValidPhone(string? value)
        {
            var digits = DigitsOnly(value);

            if (digits.Length == 0)
            {
                return true;
            }

            if (digits.Length is not (10 or 11))
            {
                return false;
            }

            // Area codes run from 11 to 99; a mobile number carries a 9 in front.
            var areaCode = int.Parse(digits[..2], System.Globalization.CultureInfo.InvariantCulture);

            return areaCode >= 11 && (digits.Length == 10 || digits[2] == '9');
        }

        private static char CheckDigit(string document, int length, int startingWeight)
        {
            var sum = 0;

            for (var i = 0; i < length; i++)
            {
                sum += (document[i] - '0') * (startingWeight - i);
            }

            var remainder = sum % 11;

            return (char)('0' + (remainder < 2 ? 0 : 11 - remainder));
        }

        private static char DigitByWeights(string document, int[] weights)
        {
            var sum = 0;

            for (var i = 0; i < weights.Length; i++)
            {
                sum += (document[i] - '0') * weights[i];
            }

            var remainder = sum % 11;

            return (char)('0' + (remainder < 2 ? 0 : 11 - remainder));
        }
    }
}
