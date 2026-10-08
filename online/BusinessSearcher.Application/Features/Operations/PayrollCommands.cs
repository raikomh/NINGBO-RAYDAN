using BusinessSearcher.Application.Commons.Interfaces;
using BusinessSearcher.Application.DTOs.Operations;
using BusinessSearcher.Domain.BoundedContext.Operations.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Operations.Enums;
using BusinessSearcher.Domain.BoundedContext.Operations.Repositories;
using BusinessSearcher.Domain.Exceptions;
using MediatR;

// ── Nómina: calcula y registra el gasto de salario a partir de los roles configurados ──
namespace BusinessSearcher.Application.Features.Operations.Payroll
{
    public record GetPayrollPreviewQuery(DateTime From, DateTime To) : IRequest<PayrollPreviewDto>;
    public record RegisterPayrollExpenseCommand(RegisterPayrollExpenseDto Dto) : IRequest<ExpenseDto>;

    /// <summary>Cálculo compartido entre la previsualización y el registro: recalcular siempre en el
    /// servidor evita que un cliente manipule los montos que finalmente se registran como gasto.</summary>
    internal static class PayrollCalculator
    {
        public const string MinimoExentoSettingKey = "MinimoExento";
        public const string UtcOffsetHoursSettingKey = "UtcOffsetHours";

        public static async Task<PayrollPreviewDto> ComputeAsync(
            Guid tenantId, DateTime from, DateTime to,
            IOperationsUserRepository users, IRoleSalaryConfigRepository roleConfigs,
            ISettingRepository settings, ISaleRepository sales, CancellationToken ct)
        {
            // Los query params llegan como DateTime "Unspecified" (p.ej. "2026-08-11"): representan el
            // día calendario LOCAL del negocio, no UTC. Se convierten los límites del día local a un
            // instante UTC usando el desfase configurado (p.ej. -4 para Cuba en horario de verano), porque
            // las ventas se guardan en UTC y "hoy" debe coincidir con el día real del negocio, no con UTC.
            var offsetSetting = await settings.GetByKeyAsync(tenantId, UtcOffsetHoursSettingKey, ct);
            var offsetHours = offsetSetting is not null && decimal.TryParse(offsetSetting.Value, out var oh) ? oh : 0m;
            var offset = TimeSpan.FromHours((double)offsetHours);

            var fromUtc = DateTime.SpecifyKind(from.Date - offset, DateTimeKind.Utc);
            var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1).AddTicks(-1) - offset, DateTimeKind.Utc);

            var minimoExentoSetting = await settings.GetByKeyAsync(tenantId, MinimoExentoSettingKey, ct);
            var minimoExento = minimoExentoSetting is not null && decimal.TryParse(minimoExentoSetting.Value, out var m) ? m : 0m;

            var configs = await roleConfigs.GetByTenantAsync(tenantId, ct);
            var workers = (await users.GetByTenantAsync(tenantId, ct)).Where(u => u.IsActive).ToList();

            var lines = new List<PayrollWorkerLineDto>();
            foreach (var worker in workers)
            {
                var cfg = configs.FirstOrDefault(c => c.Role == worker.Role);
                var baseSalary = cfg?.BaseSalary ?? 0m;
                var pct = cfg?.SalesPercentage ?? 0m;

                var workerSales = await sales.GetByTenantAsync(tenantId, fromUtc, toUtc, null, worker.Id, ct: ct);
                var ventas = workerSales.Where(s => s.Status != SaleStatus.Refunded).Sum(s => s.Total);

                var comision = Math.Max(0, ventas - minimoExento) * (pct / 100m);
                var salarioACobrar = baseSalary + comision;

                lines.Add(new PayrollWorkerLineDto(worker.Id, worker.Name, worker.Role.ToString(),
                    baseSalary, minimoExento, pct, ventas, comision, salarioACobrar));
            }

            return new PayrollPreviewDto(fromUtc, toUtc, minimoExento, lines, lines.Sum(l => l.SalarioACobrar));
        }
    }

    public class GetPayrollPreviewHandler : IRequestHandler<GetPayrollPreviewQuery, PayrollPreviewDto>
    {
        private readonly IOperationsUserRepository _users; private readonly IRoleSalaryConfigRepository _roleConfigs;
        private readonly ISettingRepository _settings; private readonly ISaleRepository _sales; private readonly ICurrentUserService _u;
        public GetPayrollPreviewHandler(IOperationsUserRepository users, IRoleSalaryConfigRepository roleConfigs,
            ISettingRepository settings, ISaleRepository sales, ICurrentUserService u)
        { _users = users; _roleConfigs = roleConfigs; _settings = settings; _sales = sales; _u = u; }

        public async Task<PayrollPreviewDto> Handle(GetPayrollPreviewQuery r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u);
            if (r.To < r.From) throw new DomainException("El rango de fechas es inválido.");
            return await PayrollCalculator.ComputeAsync(t, r.From, r.To, _users, _roleConfigs, _settings, _sales, ct);
        }
    }

    public class RegisterPayrollExpenseHandler : IRequestHandler<RegisterPayrollExpenseCommand, ExpenseDto>
    {
        private readonly IOperationsUserRepository _users; private readonly IRoleSalaryConfigRepository _roleConfigs;
        private readonly ISettingRepository _settings; private readonly ISaleRepository _sales;
        private readonly IExpenseRepository _expenses; private readonly IOperationsUnitOfWork _uow; private readonly ICurrentUserService _u;
        public RegisterPayrollExpenseHandler(IOperationsUserRepository users, IRoleSalaryConfigRepository roleConfigs,
            ISettingRepository settings, ISaleRepository sales, IExpenseRepository expenses, IOperationsUnitOfWork uow, ICurrentUserService u)
        { _users = users; _roleConfigs = roleConfigs; _settings = settings; _sales = sales; _expenses = expenses; _uow = uow; _u = u; }

        public async Task<ExpenseDto> Handle(RegisterPayrollExpenseCommand r, CancellationToken ct)
        {
            var t = OpsMapper.RequireTenant(_u); var d = r.Dto;
            if (d.To < d.From) throw new DomainException("El rango de fechas es inválido.");

            var preview = await PayrollCalculator.ComputeAsync(t, d.From, d.To, _users, _roleConfigs, _settings, _sales, ct);
            var lines = d.WorkerIds is { Count: > 0 }
                ? preview.Workers.Where(w => d.WorkerIds.Contains(w.WorkerId)).ToList()
                : preview.Workers.ToList();
            if (lines.Count == 0) throw new DomainException("No hay trabajadores para calcular la nómina.");

            var total = lines.Sum(l => l.SalarioACobrar);
            if (total <= 0) throw new DomainException("El monto de la nómina debe ser mayor que cero.");

            var description = string.IsNullOrWhiteSpace(d.Description)
                ? $"Nómina {d.From:dd/MM/yyyy}–{d.To:dd/MM/yyyy} ({lines.Count} trabajador{(lines.Count == 1 ? "" : "es")})"
                : d.Description!.Trim();

            var expense = Expense.Create(t, ExpenseType.Salary, total, description, d.AmountUSD, _u.AccountId);
            await _expenses.AddAsync(expense, ct);
            await _uow.SaveChangesAsync(ct);
            return OpsMapper.ToDto(expense);
        }
    }
}
