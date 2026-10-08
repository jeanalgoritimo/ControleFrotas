using System.Text.RegularExpressions;
namespace ControleFrotas;
public static class Validation
{
    public static string Plate(string? value) => (value ?? "").Trim().Replace("-", "").ToUpperInvariant();
    public static bool ValidPlate(string value) => Regex.IsMatch(value, @"^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static bool Digits(string? value, int count) => value is not null && value.Length == count && value.All(c => c is >= '0' and <= '9');
    public static bool ValidCpf(string? value)
    {
        if (value is null || !Digits(value, 11) || value.Distinct().Count() == 1) return false;
        for (int length = 9; length <= 10; length++)
        {
            int sum = 0;
            for (int i = 0; i < length; i++) sum += (value[i] - '0') * (length + 1 - i);
            int digit = (sum * 10) % 11;
            if (digit == 10) digit = 0;
            if (digit != value[length] - '0') return false;
        }
        return true;
    }
    public static string? VehicleError(VehicleRequest r)
    {
        if (!ValidPlate(Plate(r.Plate))) return "Informe uma placa brasileira válida.";
        if (!new[] { "Carro", "Moto", "Van", "Utilitário", "Caminhão", "Cavalo mecânico", "Implemento" }.Contains(r.Category)) return "Categoria inválida.";
        if (string.IsNullOrWhiteSpace(r.Brand) || r.Brand.Trim().Length > 80 || string.IsNullOrWhiteSpace(r.Model) || r.Model.Trim().Length > 100) return "Informe marca e modelo dentro dos limites.";
        if (r.Year < 1900 || r.Year > DateTime.UtcNow.Year + 1) return "Ano inválido.";
        if (r.Odometer < 0 || r.Odometer > 999999999 || decimal.Round(r.Odometer, 3) != r.Odometer) return "Hodômetro deve ser positivo ou zero, com até três casas decimais.";
        return null;
    }
    public static string? DriverError(DriverRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 150) return "Informe o nome com até 150 caracteres.";
        if (!ValidCpf(r.Cpf)) return "CPF inválido. Informe somente os 11 números.";
        if (!Digits(r.License, 11)) return "CNH deve conter 11 números. Esta validação não confirma situação no órgão emissor.";
        if (!new[] { "A", "B", "C", "D", "E", "AB", "AC", "AD", "AE" }.Contains(r.LicenseCategory)) return "Categoria de CNH inválida.";
        if (r.LicenseExpiry < new DateOnly(1900, 1, 1)) return "Informe a validade da CNH.";
        return null;
    }
}
