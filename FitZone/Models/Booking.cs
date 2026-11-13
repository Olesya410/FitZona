namespace FitZone.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ServiceId { get; set; }
        public int TrainerId { get; set; }
        public DateTime TrainingDate { get; set; }
        public TimeSpan TrainingTime { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
