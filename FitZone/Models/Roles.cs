namespace FitZone.Models
{
    public static class Roles
    {
        public const string Admin = "Администратор";
        public const string Manager = "Менеджер";
        public const string Client = "Пользователь";

        public static readonly string[] All = { Client, Manager, Admin };
    }

    public static class BookingStatuses
    {
        public const string Pending = "На рассмотрении";
        public const string Confirmed = "Подтверждена";
        public const string Cancelled = "Отменена";
        public const string Done = "Проведена";

        public static readonly string[] All = { Pending, Confirmed, Cancelled, Done };
    }
}
