using System.Security.Claims;
using FitZone.Data;
using FitZone.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitZone.Controllers
{
    // Админ-панель: доступна только пользователям с ролью «Администратор»
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : Controller
    {
        private const long MaxPhotoBytes = 600 * 1024;
        private const string TrainerPhotoDir = "img/trainers/";
        private static readonly string[] PhotoExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif" };
        private static readonly string[] Tabs = { "req", "mod", "svc", "tr", "sch", "usr", "set" };

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public AdminController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        [HttpGet]
        public IActionResult Index(string? tab)
        {
            var model = new AdminViewModel
            {
                Tab = (tab != null && Tabs.Contains(tab)) ? tab : "req",
                CurrentUserId = CurrentUserId(),
                CurrentUserName = User.Identity?.Name ?? "Администратор",
                Bookings = (from b in _db.Bookings
                            join u in _db.Users on b.UserId equals u.Id
                            join s in _db.Services on b.ServiceId equals s.Id
                            join t in _db.Trainers on b.TrainerId equals t.Id
                            orderby b.CreatedAt descending
                            select new AdminBookingRow
                            {
                                Id = b.Id,
                                ClientName = u.FullName,
                                ClientEmail = u.Email,
                                ClientPhone = u.Phone,
                                ServiceName = s.Name,
                                TrainerName = t.FullName,
                                Date = b.TrainingDate,
                                Time = b.TrainingTime,
                                Status = b.Status
                            }).ToList(),
                PendingReviews = _db.Reviews.Where(r => !r.IsApproved).OrderByDescending(r => r.ReviewDate).ThenByDescending(r => r.Id).ToList(),
                PublishedReviews = _db.Reviews.Where(r => r.IsApproved).OrderByDescending(r => r.ReviewDate).ThenByDescending(r => r.Id).ToList(),
                Services = _db.Services.OrderBy(s => s.Id).ToList(),
                Trainers = _db.Trainers.OrderBy(t => t.Id).ToList(),
                Slots = _db.TimeSlots.OrderBy(s => s.SlotTime).ToList(),
                ClosedDays = _db.ClosedDays.OrderBy(c => c.ClosedDate).ToList(),
                Users = _db.Users.OrderBy(u => u.Id).ToList(),
                Contact = _db.Contacts.FirstOrDefault() ?? new Contact()
            };
            return View(model);
        }

        // ---------- Заявки ----------

        [HttpPost]
        public IActionResult SaveBookings(IFormCollection form)
        {
            var wanted = new Dictionary<int, string>();
            foreach (var key in form.Keys)
            {
                if (key.StartsWith("status_") && int.TryParse(key.Substring(7), out var id))
                {
                    wanted[id] = form[key].ToString();
                }
            }

            var ids = wanted.Keys.ToList();
            var rows = _db.Bookings.Where(b => ids.Contains(b.Id)).ToList();
            var changed = 0;
            foreach (var b in rows)
            {
                var status = wanted[b.Id];
                if (BookingStatuses.All.Contains(status) && b.Status != status)
                {
                    b.Status = status;
                    changed++;
                }
            }
            _db.SaveChanges();

            TempData["Ok"] = changed > 0 ? "Статусы заявок сохранены." : "Изменений в статусах нет.";
            return Back("req");
        }

        [HttpPost]
        public IActionResult DeleteBooking(int id)
        {
            var b = _db.Bookings.Find(id);
            if (b != null)
            {
                _db.Bookings.Remove(b);
                _db.SaveChanges();
                TempData["Ok"] = "Заявка удалена.";
            }
            return Back("req");
        }

        // ---------- Отзывы ----------

        [HttpPost]
        public IActionResult ApproveReview(int id)
        {
            var r = _db.Reviews.Find(id);
            if (r != null)
            {
                r.IsApproved = true;
                _db.SaveChanges();
                TempData["Ok"] = "Отзыв опубликован на сайте.";
            }
            return Back("mod");
        }

        [HttpPost]
        public IActionResult DeleteReview(int id)
        {
            var r = _db.Reviews.Find(id);
            if (r != null)
            {
                _db.Reviews.Remove(r);
                _db.SaveChanges();
                TempData["Ok"] = "Отзыв удалён.";
            }
            return Back("mod");
        }

        // ---------- Услуги ----------

        // id = 0 — новая услуга, иначе правим существующую
        [HttpPost]
        public IActionResult SaveService(int id, string? name, int duration, int? maxGroup, int price, string? desc)
        {
            var title = (name ?? "").Trim();
            var text = (desc ?? "").Trim();

            if (title.Length == 0 || title.Length > 100 || duration < 10 || price < 0 || (maxGroup.HasValue && maxGroup.Value < 1) || text.Length > 500)
            {
                TempData["Err"] = "Проверьте поля услуги: название до 100 символов, длительность от 10 минут, цена не меньше 0, описание до 500 символов.";
                return Back("svc");
            }

            var s = id == 0 ? new Service() : _db.Services.Find(id);
            if (s == null)
            {
                TempData["Err"] = "Услуга не найдена.";
                return Back("svc");
            }

            s.Name = title;
            s.DurationMinutes = duration;
            s.MaxGroupSize = maxGroup;
            s.PricePerSession = price;
            s.Description = text.Length == 0 ? null : text;

            if (id == 0)
            {
                _db.Services.Add(s);
            }
            _db.SaveChanges();

            TempData["Ok"] = id == 0 ? "Услуга добавлена." : "Услуга сохранена.";
            return Back("svc");
        }

        [HttpPost]
        public IActionResult DeleteService(int id)
        {
            var s = _db.Services.Find(id);
            if (s == null)
            {
                return Back("svc");
            }
            if (_db.Bookings.Any(b => b.ServiceId == id))
            {
                TempData["Err"] = "Нельзя удалить услугу «" + s.Name + "»: на неё есть записи. Сначала удалите или завершите эти заявки.";
                return Back("svc");
            }

            // Отзывы остаются, просто теряют привязку к услуге
            foreach (var r in _db.Reviews.Where(x => x.ServiceId == id).ToList())
            {
                r.ServiceId = null;
            }
            _db.Services.Remove(s);
            _db.SaveChanges();

            TempData["Ok"] = "Услуга удалена.";
            return Back("svc");
        }

        // ---------- Тренеры ----------

        // id = 0 — новый тренер, иначе правим существующего
        [HttpPost]
        public IActionResult SaveTrainer(int id, string? name, string? spec, int experience, string? bio, string? awards, IFormFile? photo)
        {
            var fullName = (name ?? "").Trim();
            var specialization = (spec ?? "").Trim();
            var biography = (bio ?? "").Trim();
            var achievements = (awards ?? "").Trim();

            if (fullName.Length == 0 || fullName.Length > 150 || specialization.Length == 0 || specialization.Length > 200
                || experience < 0 || experience > 70 || biography.Length > 1000 || achievements.Length > 1000)
            {
                TempData["Err"] = "Проверьте поля тренера: имя до 150 символов, специализация до 200, опыт от 0 до 70 лет, биография и достижения до 1000 символов.";
                return Back("tr");
            }

            var t = id == 0 ? new Trainer() : _db.Trainers.Find(id);
            if (t == null)
            {
                TempData["Err"] = "Тренер не найден.";
                return Back("tr");
            }

            string? newPath = null;
            if (photo != null && photo.Length > 0)
            {
                var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
                if (photo.Length > MaxPhotoBytes || !PhotoExtensions.Contains(ext))
                {
                    TempData["Err"] = "Фото должно быть изображением (jpg, png, webp, gif, avif) размером до 600 КБ.";
                    return Back("tr");
                }

                var dir = Path.Combine(_env.WebRootPath, "img", "trainers");
                Directory.CreateDirectory(dir);
                var fileName = Guid.NewGuid().ToString("N") + ext;
                using (var stream = System.IO.File.Create(Path.Combine(dir, fileName)))
                {
                    photo.CopyTo(stream);
                }
                newPath = TrainerPhotoDir + fileName;
            }

            t.FullName = fullName;
            t.Specialization = specialization;
            t.ExperienceYears = experience;
            t.Biography = biography.Length == 0 ? null : biography;
            t.Achievements = achievements.Length == 0 ? null : achievements;

            if (newPath != null)
            {
                DeleteUploadedPhoto(t.ImagePath);
                t.ImagePath = newPath;
            }

            if (id == 0)
            {
                _db.Trainers.Add(t);
            }
            _db.SaveChanges();

            TempData["Ok"] = id == 0 ? "Тренер добавлен." : "Тренер сохранён.";
            return Back("tr");
        }

        [HttpPost]
        public IActionResult DeleteTrainer(int id)
        {
            var t = _db.Trainers.Find(id);
            if (t == null)
            {
                return Back("tr");
            }
            if (_db.Bookings.Any(b => b.TrainerId == id))
            {
                TempData["Err"] = "Нельзя удалить тренера «" + t.FullName + "»: на него есть записи. Сначала удалите или завершите эти заявки.";
                return Back("tr");
            }

            foreach (var r in _db.Reviews.Where(x => x.TrainerId == id).ToList())
            {
                r.TrainerId = null;
            }
            DeleteUploadedPhoto(t.ImagePath);
            _db.Trainers.Remove(t);
            _db.SaveChanges();

            TempData["Ok"] = "Тренер удалён.";
            return Back("tr");
        }

        // Удаляем файл только если его загрузили через админку (картинки сайта из img/ не трогаем)
        private void DeleteUploadedPhoto(string? imagePath)
        {
            if (imagePath == null || !imagePath.StartsWith(TrainerPhotoDir) || imagePath.Contains(".."))
            {
                return;
            }
            var full = Path.Combine(_env.WebRootPath, imagePath.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(full))
            {
                System.IO.File.Delete(full);
            }
        }

        // ---------- Расписание ----------

        [HttpPost]
        public IActionResult AddTime(string? newTime)
        {
            if (!TimeSpan.TryParse(newTime, out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromHours(24))
            {
                TempData["Err"] = "Введите время в формате ЧЧ:ММ.";
                return Back("sch");
            }
            time = new TimeSpan(time.Hours, time.Minutes, 0);

            if (_db.TimeSlots.Any(s => s.SlotTime == time))
            {
                TempData["Err"] = "Такое время уже есть в списке.";
                return Back("sch");
            }

            _db.TimeSlots.Add(new TimeSlot { SlotTime = time });
            _db.SaveChanges();

            TempData["Ok"] = "Время добавлено.";
            return Back("sch");
        }

        [HttpPost]
        public IActionResult DeleteTime(int id)
        {
            var s = _db.TimeSlots.Find(id);
            if (s != null)
            {
                _db.TimeSlots.Remove(s);
                _db.SaveChanges();
                TempData["Ok"] = "Время убрано из расписания (уже созданные записи не меняются).";
            }
            return Back("sch");
        }

        [HttpPost]
        public IActionResult AddClosedDay(DateTime newClosed)
        {
            var day = newClosed.Date;
            if (day.Year < 2000)
            {
                TempData["Err"] = "Выберите дату.";
                return Back("sch");
            }
            if (_db.ClosedDays.Any(c => c.ClosedDate == day))
            {
                TempData["Err"] = "Этот день уже закрыт.";
                return Back("sch");
            }

            _db.ClosedDays.Add(new ClosedDay { ClosedDate = day });
            _db.SaveChanges();

            TempData["Ok"] = "День закрыт для записи.";
            return Back("sch");
        }

        [HttpPost]
        public IActionResult DeleteClosedDay(int id)
        {
            var c = _db.ClosedDays.Find(id);
            if (c != null)
            {
                _db.ClosedDays.Remove(c);
                _db.SaveChanges();
                TempData["Ok"] = "День снова открыт для записи.";
            }
            return Back("sch");
        }

        // ---------- Пользователи и роли ----------

        [HttpPost]
        public IActionResult SaveRoles(IFormCollection form)
        {
            var me = CurrentUserId();
            var wanted = new Dictionary<int, string>();
            foreach (var key in form.Keys)
            {
                if (key.StartsWith("role_") && int.TryParse(key.Substring(5), out var id))
                {
                    wanted[id] = form[key].ToString();
                }
            }

            var ids = wanted.Keys.ToList();
            var rows = _db.Users.Where(u => ids.Contains(u.Id)).ToList();
            var changed = 0;
            foreach (var u in rows)
            {
                var role = wanted[u.Id];
                // Свою роль менять нельзя, чтобы не лишить сайт последнего администратора
                if (u.Id != me && Roles.All.Contains(role) && u.Role != role)
                {
                    u.Role = role;
                    changed++;
                }
            }
            _db.SaveChanges();

            TempData["Ok"] = changed > 0
                ? "Роли сохранены. Пользователь получит новую роль при следующем входе."
                : "Изменений в ролях нет.";
            return Back("usr");
        }

        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            if (id == CurrentUserId())
            {
                TempData["Err"] = "Свой аккаунт удалить нельзя.";
                return Back("usr");
            }

            var u = _db.Users.Find(id);
            if (u != null)
            {
                // Записи пользователя удаляются вместе с ним (иначе их держит внешний ключ)
                _db.Bookings.RemoveRange(_db.Bookings.Where(b => b.UserId == id));
                _db.Users.Remove(u);
                _db.SaveChanges();
                TempData["Ok"] = "Пользователь и его записи удалены.";
            }
            return Back("usr");
        }

        // ---------- Контакты и настройки ----------

        [HttpPost]
        public IActionResult SaveSettings(string? address, string? phone, string? email, string? hours)
        {
            var a = (address ?? "").Trim();
            var p = (phone ?? "").Trim();
            var e = (email ?? "").Trim();
            var h = (hours ?? "").Trim();

            if (a.Length == 0 || p.Length == 0 || e.Length == 0 || h.Length == 0
                || a.Length > 200 || p.Length > 50 || e.Length > 100 || h.Length > 200 || !e.Contains('@'))
            {
                TempData["Err"] = "Заполните все поля контактов (e-mail должен содержать @).";
                return Back("set");
            }

            var c = _db.Contacts.FirstOrDefault();
            if (c == null)
            {
                c = new Contact();
                _db.Contacts.Add(c);
            }
            c.Address = a;
            c.Phone = p;
            c.Email = e;
            c.WorkingHours = h;
            _db.SaveChanges();

            TempData["Ok"] = "Контакты сохранены: они обновились на всех страницах сайта.";
            return Back("set");
        }

        // ---------- Вспомогательное ----------

        private IActionResult Back(string tab)
        {
            return RedirectToAction("Index", new { tab });
        }

        private int CurrentUserId()
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
            return id;
        }
    }
}
