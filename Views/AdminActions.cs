using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PIM4AdminDesktop.Models;
using PIM4AdminDesktop.Services;
using PIM4AdminDesktop.ViewModels;

namespace PIM4AdminDesktop.Views;

public sealed class AdminActions(Window owner, MainViewModel vm)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private EditorWindow Editor(string title) => new(owner, title)
    {
        ErrorMessage = () => vm.ErrorMessage,
        SessionActive = () => vm.IsAuthenticated
    };
    private async Task SaveAsync(EditorWindow editor, Func<Task> save, bool reloadDashboard = true)
    {
        editor.SaveAsync = () => vm.RunAsync(save, "Alteração salva.");
        if (editor.ShowDialog() == true && reloadDashboard) await vm.ReloadAfterMutationAsync();
    }
    private static string Selected(ComboBox box, string label) =>
        AdminValidation.Required(box.SelectedValue as string ?? "", label);
    private bool Confirm(string text) => MessageBox.Show(owner, text, "Confirmar alteração", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public async Task EditCourseAsync(bool create)
    {
        var course = create ? null : vm.SelectedCourse;
        if (!create && course == null) return;
        var dialog = Editor(create ? "Novo curso presencial" : "Editar curso");
        var name = dialog.Text("Nome *", course?.Nome ?? "");
        var description = dialog.Text("Descrição", course?.Description ?? "", multiline: true);
        var category = dialog.Text("Categoria", course?.Category ?? "");
        var price = dialog.Text("Preço em reais *", (course?.Preco ?? 0).ToString("F2", PtBr));
        var hours = dialog.Text("Carga horária em horas *", (course?.WorkloadHours ?? 0).ToString());
        var image = dialog.Text("Imagem (URL ou caminho)", course?.Image ?? "");
        var active = dialog.Check("Curso ativo no catálogo", course?.IsActive ?? true);
        await SaveAsync(dialog, () =>
        {
            var url = image.Text.Trim();
            return vm.Api.SaveCourseAsync(course?.Id, new CourseRequest
            {
                Nome = AdminValidation.Required(name.Text, "Nome"), Description = description.Text.Trim(),
                Category = category.Text.Trim(), Preco = AdminValidation.Money(price.Text),
                WorkloadHours = AdminValidation.Integer(hours.Text, "Carga horária"),
                Image = url, IsActive = active.IsChecked == true, DeliveryMode = "presencial"
            });
        });
    }
    public async Task ArchiveCourseAsync()
    {
        var course = vm.SelectedCourse;
        if (course == null || !Confirm($"{(course.IsActive ? "Arquivar" : "Restaurar")} o curso ‘{course.Nome}’?\nO histórico será preservado.")) return;
        if (await vm.RunAsync(() => vm.Api.SetCourseArchivedAsync(course.Id, course.IsActive)))
            await vm.ReloadAfterMutationAsync();
    }
    public async Task EditClassAsync(bool create)
    {
        var course = vm.SelectedCourse;
        var item = create ? null : vm.SelectedClass;
        if (course == null || (!create && item == null)) return;
        var dialog = Editor(create ? "Nova turma" : "Editar turma");
        dialog.Note($"Curso: {course.Nome}. Informe os horários no fuso deste computador. A API calcula as vagas disponíveis e valida a capacidade já ocupada.");
        var start = dialog.Text("Início * — dd/MM/aaaa HH:mm", (item?.StartDate.ToLocalTime() ?? DateTime.Now.Date.AddDays(7).AddHours(9)).ToString("dd/MM/yyyy HH:mm"));
        var end = dialog.Text("Término — dd/MM/aaaa HH:mm (opcional)", item?.EndDate?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "");
        var location = dialog.Text("Local *", item?.Local ?? "");
        var instructor = dialog.Text("Instrutor *", item?.Instructor ?? "");
        var capacity = dialog.Text("Capacidade total *", (item?.Capacity ?? 20).ToString());
        var status = dialog.Choice("Situação *", AdminLabels.Choices("scheduled", "completed", "cancelled"), item?.Status ?? "scheduled");
        dialog.Note("Alterar ou cancelar uma turma pode afetar alunos vinculados. Revise a situação antes de salvar.");
        await SaveAsync(dialog, () =>
        {
            var first = AdminValidation.LocalDateTime(start.Text, "Início");
            DateTime? last = string.IsNullOrWhiteSpace(end.Text) ? null : AdminValidation.LocalDateTime(end.Text, "Término");
            if (last.HasValue && last < first) throw new ArgumentException("O término não pode ser anterior ao início.");
            return vm.Api.SaveClassAsync(course.Id, item?.Id, new CourseClassRequest
            {
                StartDate = first, EndDate = last, Local = AdminValidation.Required(location.Text, "Local"),
                Instructor = AdminValidation.Required(instructor.Text, "Instrutor"),
                Capacity = AdminValidation.Integer(capacity.Text, "Capacidade", 1), Status = Selected(status, "Situação")
            });
        });
    }
    public async Task EditStudentAsync(bool create)
    {
        var item = create ? null : vm.SelectedStudent;
        if (!create && item == null) return;
        var dialog = Editor(create ? "Novo cadastro de aluno" : "Editar cadastro de aluno");
        dialog.Note("Este cadastro não cria pedido, pagamento ou matrícula de compra. A situação geral pode afetar todas as matrículas do aluno; para uma matrícula específica, use Turmas → Alunos da turma.");
        var name = dialog.Text("Nome *", item?.Name ?? "");
        var email = dialog.Text("E-mail *", item?.Email ?? "");
        var phone = dialog.Text("Telefone", item?.Phone ?? "");
        var options = vm.Courses.Select(c => new KeyValuePair<string, string>(c.Id.ToString(), c.Nome)).ToList();
        if (item != null && !options.Any(c => c.Key == item.CourseId))
            options.Add(new(item.CourseId, item.CourseName));
        var course = dialog.Choice("Curso *", options, item?.CourseId);
        // Changing this legacy field does not transfer an enrollment. Transfers have their own action.
        if (item != null) course.IsEnabled = false;
        var date = dialog.Text("Data do cadastro — texto de referência", item?.EnrollmentDate ?? DateTime.Today.ToString("yyyy-MM-dd"));
        var status = dialog.Choice("Situação geral *", AdminLabels.Choices("active", "completed", "cancelled"), item?.Status ?? "active");
        await SaveAsync(dialog, () =>
        {
            var courseId = Selected(course, "Curso");
            return vm.Api.SaveStudentAsync(item?.Id, new StudentRequest
            {
                Name = AdminValidation.Required(name.Text, "Nome"), Email = AdminValidation.Email(email.Text), Phone = phone.Text.Trim(),
                CourseId = courseId, CourseName = options.First(c => c.Key == courseId).Value,
                EnrollmentDate = date.Text.Trim(), Status = Selected(status, "Situação")
            });
        });
    }
    public async Task DeleteStudentAsync()
    {
        var student = vm.SelectedStudent;
        if (student == null || !Confirm($"Excluir o cadastro de ‘{student.Name}’?\nSomente cadastros sem matrículas vinculadas podem ser excluídos. A API verificará essa regra.")) return;
        if (await vm.RunAsync(() => vm.Api.DeleteStudentAsync(student.Id))) await vm.ReloadAfterMutationAsync();
    }
    public async Task EditOrderAsync()
    {
        var order = vm.SelectedOrder;
        if (order == null) return;
        var dialog = Editor($"Pedido #{order.Id}");
        dialog.Note($"Cliente: {order.Usuario?.Nome} — {order.Usuario?.Email}\nPagamento: {order.PaymentStatusLabel}\nTotal: {order.Total.ToString("C", PtBr)}");
        foreach (var item in order.Itens)
            dialog.Note($"{item.Nome} — {item.Quantidade} × {item.PrecoUnitario.ToString("C", PtBr)} — turma {item.TurmaId?.ToString() ?? "não informada"}");
        var status = dialog.Choice("Situação do pedido *", AdminLabels.Choices("pending", "processing", "completed", "cancelled"), order.Status);
        dialog.Note("Pedidos precisam estar pagos para avançar. Cancelar um pedido pago exige o fluxo de reembolso. Esta tela altera somente a situação operacional; não confirma pagamentos.");
        await SaveAsync(dialog, () => vm.Api.UpdateOrderStatusAsync(order.Id, Selected(status, "Situação")));
    }
    public async Task EditEnrollmentAsync(bool transfer)
    {
        var course = vm.SelectedCourse;
        var source = vm.SelectedClass;
        var student = vm.SelectedEnrollment;
        if (course == null || source == null || student == null) return;
        if (student.EnrollmentId == null)
        {
            await vm.RunAsync(() => throw new ArgumentException("A API não informou o ID da matrícula. Atualize o servidor antes de alterar esta matrícula."));
            return;
        }
        var dialog = Editor(transfer ? "Transferir matrícula" : "Alterar situação da matrícula");
        dialog.Note($"Aluno: {student.Name}\nCurso: {course.Nome}\nMatrícula: #{student.EnrollmentId}");
        ComboBox choice;
        if (transfer)
        {
            var destinations = vm.Classes.Where(c => c.Id != source.Id && c.Status == "scheduled" && c.StartDate > DateTime.UtcNow && c.AvailableSeats > 0)
                .Select(c => new KeyValuePair<string, string>(c.Id.ToString(), $"#{c.Id} — {c.LocalStartDate:dd/MM/yyyy HH:mm} — {c.Local} ({c.AvailableSeats} vagas)"));
            choice = dialog.Choice("Turma de destino *", destinations, null);
            dialog.Note("A API confirma a disponibilidade e transfere a vaga em uma transação.");
        }
        else choice = dialog.Choice("Situação *", AdminLabels.Choices("active", "completed", "cancelled"), student.Status);
        await SaveAsync(dialog, () => transfer
            ? vm.Api.TransferEnrollmentAsync(course.Id, source.Id, student, AdminValidation.Integer(Selected(choice, "Turma de destino"), "Turma de destino", 1))
            : vm.Api.UpdateEnrollmentStatusAsync(course.Id, source.Id, student, Selected(choice, "Situação")), reloadDashboard: false);
        // Refresh both capacity and roster after a saved change.
        if (dialog.DialogResult == true)
        {
            await vm.ReloadAfterMutationAsync();
            if (vm.IsAuthenticated && vm.SelectedClass != null) await vm.LoadClassStudentsAsync();
        }
    }
}
