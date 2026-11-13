using FitZone.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FitZone.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext db)
        {
            AddReviewModeration(db);
            AddTimeSlots(db);
            AddClosedDays(db);
            SetStartPasswords(db);
        }

        private static void AddReviewModeration(AppDbContext db)
        {
            db.Database.ExecuteSqlRaw(@"
IF COL_LENGTH(N'dbo.Reviews', N'IsApproved') IS NULL
    ALTER TABLE dbo.Reviews ADD IsApproved bit NOT NULL CONSTRAINT DF_Reviews_IsApproved DEFAULT 1;");
        }

        private static void AddTimeSlots(AppDbContext db)
        {
            db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.TimeSlots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TimeSlots (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TimeSlots PRIMARY KEY,
        SlotTime time NOT NULL CONSTRAINT UQ_TimeSlots_SlotTime UNIQUE
    );
    INSERT INTO dbo.TimeSlots (SlotTime) VALUES ('09:00'), ('10:30'), ('12:00'), ('14:00'), ('16:00'), ('18:00'), ('19:30');
END");
        }

        private static void AddClosedDays(AppDbContext db)
        {
            db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ClosedDays', N'U') IS NULL
    CREATE TABLE dbo.ClosedDays (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClosedDays PRIMARY KEY,
        ClosedDate date NOT NULL CONSTRAINT UQ_ClosedDays_ClosedDate UNIQUE
    );");
        }

        private static void SetStartPasswords(AppDbContext db)
        {
            var hasher = new PasswordHasher<User>();
            var users = db.Users.Where(u => u.PasswordHash == null || u.PasswordHash == "").ToList();

            foreach (var user in users)
            {
                var password = "User1234";
                if (user.Role == Roles.Admin) password = "Admin123";
                if (user.Role == Roles.Manager) password = "Manager123";
                user.PasswordHash = hasher.HashPassword(user, password);
            }

            if (users.Count > 0)
            {
                db.SaveChanges();
            }
        }
    }
}
