using System.ComponentModel.DataAnnotations.Schema;

namespace FitZone.Models
{
    // Таблица services (направления тренировок)
    [Table("services")]
    public class Service
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int DurationMinutes { get; set; }
        public int? MaxGroupSize { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal PricePerSession { get; set; }
    }
}
