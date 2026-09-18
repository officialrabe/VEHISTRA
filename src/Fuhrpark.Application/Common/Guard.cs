using Fuhrpark.Domain.Exceptions;

namespace Fuhrpark.Application.Common;

/// <summary>Kompakte Eingabepruefungen mit fachlichen Fehlermeldungen in deutscher Sprache.</summary>
public static class Guard
{
    public static string NotEmpty(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException($"Das Feld '{fieldName}' darf nicht leer sein.");
        }

        return value.Trim();
    }

    public static string MaxLength(string? value, int maxLength, string fieldName)
    {
        if (value is not null && value.Length > maxLength)
        {
            throw new BusinessRuleException($"Das Feld '{fieldName}' darf hoechstens {maxLength} Zeichen enthalten.");
        }

        return value?.Trim() ?? string.Empty;
    }

    public static int NotNegative(int value, string fieldName)
    {
        if (value < 0)
        {
            throw new BusinessRuleException($"Das Feld '{fieldName}' darf nicht negativ sein.");
        }

        return value;
    }

    public static void That(bool condition, string message)
    {
        if (!condition)
        {
            throw new BusinessRuleException(message);
        }
    }
}
