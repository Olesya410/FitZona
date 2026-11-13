namespace FitZone.Models
{
    public class Trainer
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Specialization { get; set; } = "";
        public int ExperienceYears { get; set; }
        public string? Biography { get; set; }
        public string? Achievements { get; set; }
        public string? ImagePath { get; set; }
    }
}
