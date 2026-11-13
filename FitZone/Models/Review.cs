namespace FitZone.Models
{
    public class Review
    {
        public int Id { get; set; }
        public string AuthorName { get; set; } = "";
        public int Rating { get; set; }
        public DateTime ReviewDate { get; set; }
        public string Comment { get; set; } = "";
        public int? TrainerId { get; set; }
        public int? ServiceId { get; set; }

        public bool IsApproved { get; set; } = true;
    }
}
