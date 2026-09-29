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
    public DateTime StartDate { get; init; }
    public string Local { get; init; } = string.Empty;
    public string Instructor { get; init; } = string.Empty;
    public int AvailableSeats { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class OrderModel
{
    public int Id { get; init; }
    public DateTime DataPedido { get; init; }
    public string Status { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public UserSummaryModel? Usuario { get; init; }
}

public sealed class UserSummaryModel
{
    public string Nome { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class StudentModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public string EnrollmentDate { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class ApiError
{
    public string Message { get; init; } = string.Empty;
}