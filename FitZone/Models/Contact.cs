namespace FitZone.Models
{
    // Таблица Contacts (контакты центра, в таблице одна строка)
    public class Contact
    {
        public int Id { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? WorkingHours { get; set; }
    }
}
