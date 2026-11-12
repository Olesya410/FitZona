namespace FitZone.Models
{
    // Таблица ClosedDays (даты, на которые запись закрыта, например праздники)
    public class ClosedDay
    {
        public int Id { get; set; }
        public DateTime ClosedDate { get; set; }
    }
}
