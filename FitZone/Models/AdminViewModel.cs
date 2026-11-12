namespace FitZone.Models
{
    // Строка таблицы заявок в админ-панели (запись + имена из связанных таблиц)
    public class AdminBookingRow
    {
        public int Id { get; set; }
        public string ClientName { get; set; } = "";
        public string ClientEmail { get; set; } = "";
        public string? ClientPhone { get; set; }
        public string ServiceName { get; set; } = "";
        public string TrainerName { get; set; } = "";
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public string Status { get; set; } = "";
    }

    // Все данные для страницы админ-панели
    public class AdminViewModel
    {
        public string Tab { get; set; } = "req";
        public int CurrentUserId { get; set; }
        public string CurrentUserName { get; set; } = "";
        public List<AdminBookingRow> Bookings { get; set; } = new();
        public List<Review> PendingReviews { get; set; } = new();
        public List<Review> PublishedReviews { get; set; } = new();
        public List<Service> Services { get; set; } = new();
        public List<Trainer> Trainers { get; set; } = new();
        public List<TimeSlot> Slots { get; set; } = new();
        public List<ClosedDay> ClosedDays { get; set; } = new();
        public List<User> Users { get; set; } = new();
        public Contact Contact { get; set; } = new();
    }
}
