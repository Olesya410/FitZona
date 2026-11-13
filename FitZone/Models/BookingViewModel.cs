namespace FitZone.Models
{
    public class BookingViewModel
    {
        public List<Service> Services { get; set; } = new();
        public List<Trainer> Trainers { get; set; } = new();
        public List<string> Slots { get; set; } = new();
        public List<DateTime> ClosedDays { get; set; } = new();
        public List<string> BookedTimes { get; set; } = new();
        public int? SelectedServiceId { get; set; }
        public int? SelectedTrainerId { get; set; }
        public string SelectedDate { get; set; } = "";
        public string SelectedTime { get; set; } = "";
    }
}
