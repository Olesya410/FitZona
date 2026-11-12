using FitZone.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FitZone.Data
{
    // Запускается один раз при старте сайта. Все команды безопасно повторять:
    // если колонка/таблица уже есть, ничего не меняется.
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext db)
        {
            // 1) Модерация отзывов: у существующих отзывов IsApproved = 1 (остаются на сайте)
            db.Database.ExecuteSqlRaw(@"
IF COL_LENGTH(N'dbo.Reviews', N'IsApproved') IS NULL
    ALTER TABLE dbo.Reviews ADD IsApproved bit NOT NULL CONSTRAINT DF_Reviews_IsApproved DEFAULT 1;");

            // 2) Время занятий (вкладка «Расписание»); сразу заполняем прежними слотами
            db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.TimeSlots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TimeSlots (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TimeSlots PRIMARY KEY,
        SlotTime time NOT NULL CONSTRAINT UQ_TimeSlots_SlotTime UNIQUE
    );
    INSERT INTO dbo.TimeSlots (SlotTime) VALUES ('09:00'), ('10:30'), ('12:00'), ('14:00'), ('16:00'), ('18:00'), ('19:30');
END");

            // 3) Закрытые дни (вкладка «Расписание»)
            db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ClosedDays', N'U') IS NULL
    CREATE TABLE dbo.ClosedDays (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClosedDays PRIMARY KEY,
        ClosedDate date NOT NULL CONSTRAINT UQ_ClosedDays_ClosedDate UNIQUE
    );");

            // 4) В бэкапе у пользователей нет паролей (PasswordHash = NULL) — задаём стартовые
            var hasher = new PasswordHasher<User>();
            var withoutPassword = db.Users.Where(u => u.PasswordHash == null || u.PasswordHash == "").ToList();
            foreach (var u in withoutPassword)
            {
                string start = u.Role == Roles.Admin ? "Admin123"
                             : u.Role == Roles.Manager ? "Manager123"
                             : "User1234";
                u.PasswordHash = hasher.HashPassword(u, start);
            }
            if (withoutPassword.Count > 0)
            {
                db.SaveChanges();
            }
        }
    }
}
