namespace NutriCare.Domain.Interfaces;

public interface IAppointmentReservationLock
{
    Task<T> ExecuteAsync<T>(long nutritionistId, Func<Task<T>> action, CancellationToken cancellationToken = default);
}
