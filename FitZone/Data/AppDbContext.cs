using FitZone.Models;
using Microsoft.EntityFrameworkCore;

namespace FitZone.Data
{
    // Контекст базы данных: каждая строка DbSet — это таблица в fitzoneDB
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Service> Services => Set<Service>();
        public DbSet<Trainer> Trainers => Set<Trainer>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<Contact> Contacts => Set<Contact>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
        public DbSet<ClosedDay> ClosedDays => Set<ClosedDay>();
    }
}
