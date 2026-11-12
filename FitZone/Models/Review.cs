namespace FitZone.Models
{
    // Таблица Reviews (отзывы)
    public class Review
    {
        public int Id { get; set; }
        public string AuthorName { get; set; } = "";
        public int Rating { get; set; }
        public DateTime ReviewDate { get; set; }
        public string Comment { get; set; } = "";
        public int? TrainerId { get; set; }
        public int? ServiceId { get; set; }

        // false — отзыв ждёт модерации и на сайте пока не виден
        public bool IsApproved { get; set; } = true;
    }
}
