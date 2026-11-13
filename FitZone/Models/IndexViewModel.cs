namespace FitZone.Models
{
    public class IndexViewModel
    {
        public List<Service> Services { get; set; } = new();
        public List<Trainer> Trainers { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();
        public Contact Contact { get; set; } = new();
    }
}
