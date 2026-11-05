using KRT.Onboarding.Domain.Common;

namespace KRT.Onboarding.Domain.Accounts;

/// <summary>
/// Value Object de CPF. Garante que só existam CPFs válidos no domínio
/// e armazena apenas os 11 dígitos (sem máscara).
/// </summary>
public sealed record Cpf
{
    public const int Length = 11;

    public string Value { get; }

    private Cpf(string value) => Value = value;

    public static Cpf Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new DomainException("O CPF é obrigatório.");

        if (input.Any(c => !char.IsDigit(c) && c is not '.' and not '-' and not ' '))
            throw new DomainException("O CPF informado é inválido.");

        var digits = new string(input.Where(char.IsDigit).ToArray());

        if (!HasValidCheckDigits(digits))
            throw new DomainException("O CPF informado é inválido.");

        return new Cpf(digits);
    }

    private static bool HasValidCheckDigits(string digits)
    {
        if (digits.Length != Length || digits.All(c => c == digits[0]))
            return false;

        return CalculateCheckDigit(digits, 9) == digits[9] - '0'
            && CalculateCheckDigit(digits, 10) == digits[10] - '0';
    }

    private static int CalculateCheckDigit(string digits, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += (digits[i] - '0') * (length + 1 - i);

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    public override string ToString() => Value;
}
