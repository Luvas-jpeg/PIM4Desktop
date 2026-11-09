using PIM4AdminDesktop.Services;

namespace PIM4AdminDesktop.Models;

public sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public UserModel User { get; init; } = new();
}

public sealed class UserModel
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public sealed class CourseModel
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Image { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string DeliveryMode { get; init; } = string.Empty;
    public decimal Preco { get; init; }
    public int WorkloadHours { get; init; }
    public bool IsActive { get; init; }
    public int ModuleCount { get; init; }
    public int LessonCount { get; init; }
    public List<CourseClassModel> Classes { get; init; } = [];
}

public sealed class CourseClassModel
{
    public int Id { get; init; }
    public int CourseId { get; init; }
    public int Capacity { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime LocalStartDate => StartDate.ToLocalTime();
    public DateTime StartDate { get; init; }
    public string Local { get; init; } = string.Empty;
    public string Instructor { get; init; } = string.Empty;
    public int AvailableSeats { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusLabel => AdminLabels.Status(Status);
}

public sealed class OrderModel
{
    public int Id { get; init; }
    public DateTime DataPedido { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusLabel => AdminLabels.Status(Status);
    public string PaymentStatus { get; init; } = string.Empty;
    public string PaymentStatusLabel => AdminLabels.Status(PaymentStatus);
    public decimal Total { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public List<OrderItemModel> Itens { get; init; } = [];
    public DateTime LocalOrderDate => DataPedido.ToLocalTime();
    public UserSummaryModel? Usuario { get; init; }
}

public sealed class UserSummaryModel
{
    public string Nome { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class StudentModel
{
    public string CourseId { get; init; } = string.Empty;
    public int? EnrollmentId { get; init; }
    public ParticipationModel? Participation { get; init; }
    public string ParticipationLabel => Participation == null ? "—" : AdminLabels.Status(Participation.Status);
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public string EnrollmentDate { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusLabel => AdminLabels.Status(Status);
}

public sealed class ApiError
{
    public string Message { get; init; } = string.Empty;
}

public sealed class ParticipationModel
{
    public string Status { get; init; } = string.Empty;
    public string StatusLabel => AdminLabels.Status(Status);
    public DateTime? ConfirmedAt { get; init; }
}

public sealed class OrderItemModel
{
    public string Nome { get; init; } = string.Empty;
    public int Quantidade { get; init; }
    public decimal PrecoUnitario { get; init; }
    public int? TurmaId { get; init; }
}

public sealed class CourseRequest
{
    public string Nome { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int WorkloadHours { get; set; }
    public string DeliveryMode { get; set; } = "presencial";
    public bool IsActive { get; set; } = true;
}

public sealed class CourseClassRequest
{
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Local { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = "scheduled";
}

public sealed class StudentRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string EnrollmentDate { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}
