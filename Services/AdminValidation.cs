using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace PIM4AdminDesktop.Services;

public static class AdminValidation
{
    public static string Required(string text, string label)
        => string.IsNullOrWhiteSpace(text) ? throw new ArgumentException($"{label} é obrigatório.") : text.Trim();
    public static string Email(string text)
    {
        text = Required(text, "E-mail");
        if (!MailAddress.TryCreate(text, out var address) || address.Address != text || !address.Host.Contains('.'))
            throw new ArgumentException("Informe um e-mail válido.");
        return text;
    }
    public static decimal Money(string text)
    {
        text = text.Trim();
        if (!Regex.IsMatch(text, @"^(?:[0-9]+|[0-9]{1,3}(?:\.[0-9]{3})+)(?:,[0-9]{1,2})?$") ||
            !decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var value) || value < 0)
            throw new ArgumentException("Informe um preço válido, como 199,90, maior ou igual a zero.");
        return value;
    }
    public static int Integer(string text, string label, int minimum = 0)
    {
        if (!int.TryParse(text, out var value) || value < minimum)
            throw new ArgumentException($"{label} deve ser um número inteiro maior ou igual a {minimum}.");
        return value;
    }
    public static DateTime LocalDateTime(string text, string label)
    {
        if (!DateTime.TryParseExact(text.Trim(), "dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var value))
            throw new ArgumentException($"{label}: use dd/MM/aaaa HH:mm, por exemplo 20/12/2026 09:00.");
        value = DateTime.SpecifyKind(value, DateTimeKind.Local);
        if (TimeZoneInfo.Local.IsInvalidTime(value)) throw new ArgumentException($"{label}: horário local inválido.");
        return value.ToUniversalTime();
    }
}

public static class AdminLabels
{
    public static string Status(string value) => value switch
    {
        "active" => "Ativo", "completed" => "Concluído", "cancelled" => "Cancelado",
        "scheduled" => "Agendada", "pending" => "Pendente", "processing" => "Em andamento",
        "paid" => "Pago", "refunded" => "Reembolsado", "refused" => "Recusado",
        "confirmed" => "Confirmada", "not_confirmed" => "Não confirmada",
        "not_applicable" => "Não se aplica", "pending_confirmation" => "Aguardando confirmação",
        "unavailable" => "Indisponível", "waiting" => "Aguardando abertura", "closed" => "Encerrada", _ => value
    };
    public static IEnumerable<KeyValuePair<string, string>> Choices(params string[] values)
        => values.Select(value => new KeyValuePair<string, string>(value, Status(value)));
}
