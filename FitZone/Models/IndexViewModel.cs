namespace FitZone.Models
{
    // Все данные, которые нужны главной странице
    public class IndexViewModel
    {
        public List<Service> Services { get; set; } = new();
        public List<Trainer> Trainers { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();
        public Contact Contact { get; set; } = new();
    }
}
