using System.Security.Cryptography;

namespace FuturaTech.Domain.Helpers
{
    /// <summary>
    /// Provides helper methods for validating Brazilian CPF (Cadastro de Pessoas Físicas) and CNPJ (Cadastro Nacional de Pessoa Jurídica) numbers.
    /// </summary>
    public static class CpfCnpjValidationHelper
    {
        private static readonly int[] _multiplicador1_cpf = [10, 9, 8, 7, 6, 5, 4, 3, 2];
        private static readonly int[] _multiplicador2_cpf = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];
        private static readonly int[] _multiplicador1_cnpj = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        private static readonly int[] _multiplicador2_cnpj = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        private static bool AllDigits(this ReadOnlySpan<char> chars)
        {
            foreach (ref readonly var c in chars)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }
            return true;
        }
        public static string GenerateRandomCpf()
        {
            Span<char> tempCpf = stackalloc char[11];
            GenerateRandomNumber(tempCpf);
            CalculateDigits(tempCpf, _multiplicador1_cpf, _multiplicador2_cpf);
            return tempCpf.ToString();
        }
        public static string GenerateRandomCnpj()
        {
            Span<char> tempCnpj = stackalloc char[14];
            GenerateRandomNumber(tempCnpj);
            CalculateDigits(tempCnpj, _multiplicador1_cnpj, _multiplicador2_cnpj);
            return tempCnpj.ToString();
        }
        /// <summary>
        /// Validates a Brazilian CPF (Cadastro de Pessoas Físicas) number.
        /// </summary>
        /// <param name="cpf">The CPF number to be validated.</param>
        /// <returns>True if the CPF number is valid, false otherwise.</returns>
        public static bool ValidateCpf(ReadOnlySpan<char> cpf)
        {
            if (cpf.Length != 11 || !cpf.AllDigits())
            {
                return false;
            }
            Span<char> tempCpf = stackalloc char[11];
            cpf[..9].CopyTo(tempCpf);
            CalculateDigits(tempCpf, _multiplicador1_cpf, _multiplicador2_cpf);
            return cpf[^2] == tempCpf[^2] && cpf[^1] == tempCpf[^1];
        }

        /// <summary>
        /// Validates a CNPJ (Cadastro Nacional de Pessoa Jurídica) number.
        /// </summary>
        /// <param name="cnpj">The CNPJ number to be validated.</param>
        /// <returns>True if the CNPJ is valid, false otherwise.</returns>
        public static bool ValidateCnpj(ReadOnlySpan<char> cnpj)
        {
            if (cnpj.Length != 14 || !cnpj.AllDigits())
            {
                return false;
            }
            Span<char> tempCnpj = stackalloc char[14];
            cnpj[..12].CopyTo(tempCnpj);
            CalculateDigits(tempCnpj, _multiplicador1_cnpj, _multiplicador2_cnpj);
            return cnpj[^2] == tempCnpj[^2] && cnpj[^1] == tempCnpj[^1];
        }
        /// <summary>
        /// Validates a Brazilian CPF (Cadastro de Pessoas Físicas) or CNPJ (Cadastro Nacional de Pessoa Jurídica) number.
        /// </summary>
        /// <param name="cpfOrCnpj">The CPF or CNPJ number to be validated.</param>
        /// <returns>True if the CPF or CNPJ number is valid, false otherwise.</returns>
        public static bool ValidateCpfOrCnpj(ReadOnlySpan<char> cpfOrCnpj)
        {
            return ValidateCpf(cpfOrCnpj) || ValidateCnpj(cpfOrCnpj);
        }
        /// <summary>
        /// Calculates the verification digits for a given number using the specified multipliers.
        /// </summary>
        /// <param name="tempNumber">The number for which the verification digits are calculated.</param>
        /// <param name="multiplicador1">The first set of multipliers.</param>
        /// <param name="multiplicador2">The second set of multipliers.</param>
        /// <returns>The calculated verification digits.</returns>
        private static void CalculateDigits(Span<char> tempNumber, ReadOnlySpan<int> multiplicador1, ReadOnlySpan<int> multiplicador2)
        {
            tempNumber[^2] = CalculateDigit(tempNumber, multiplicador1);
            tempNumber[^1] = CalculateDigit(tempNumber, multiplicador2);
        }

        /// <summary>
        /// Calculates the verification digit for a given number using the specified multiplicadores.
        /// </summary>
        /// <param name="number">The number to calculate the digit for.</param>
        /// <param name="multiplicadores">The array of multiplicadores to use in the calculation.</param>
        /// <returns>The calculated verification digit.</returns>
        private static char CalculateDigit(ReadOnlySpan<char> number, ReadOnlySpan<int> multiplicadores)
        {
            int soma = 0;
            for (int i = 0; i < multiplicadores.Length; i++)
            {
                soma += (number[i] - '0') * multiplicadores[i];
            }
            int resto = soma % 11;
            return resto < 2 ? '0' : (char)(11 - resto + '0');
        }
        private static void GenerateRandomNumber(Span<char> builder)
        {
            foreach (ref var number in builder[..^2])
            {
                number = (char)RandomNumberGenerator.GetInt32(48, 58);
            }
        }
    }
}