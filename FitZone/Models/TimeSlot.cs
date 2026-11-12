namespace FitZone.Models
{
    // Таблица TimeSlots (время занятий, которое доступно для записи)
    public class TimeSlot
    {
        public int Id { get; set; }
        public TimeSpan SlotTime { get; set; }
    }
}
