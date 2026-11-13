using System.Security.Claims;
using FitZone.Data;
using FitZone.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitZone.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : Controller
    {
        private static readonly string[] Tabs = { "req", "mod", "svc", "tr", "sch", "usr", "set" };
        private static readonly string[] PhotoExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif" };

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
                Tab = tab != null && Tabs.Contains(tab) ? tab : "req",
                CurrentUserId = GetUserId(),
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

        [HttpPost]
        public IActionResult SaveBookings(IFormCollection form)
        {
            foreach (var booking in _db.Bookings.ToList())
            {
                var status = form["status_" + booking.Id].ToString();
                if (BookingStatuses.All.Contains(status))
                {
                    booking.Status = status;
                }
            }
            _db.SaveChanges();

            return Done("req", "Статусы заявок сохранены.");
        }

        [HttpPost]
        public IActionResult DeleteBooking(int id)
        {
            var booking = _db.Bookings.Find(id);
            if (booking != null)
            {
                _db.Bookings.Remove(booking);
                _db.SaveChanges();
            }
            return Done("req", "Заявка удалена.");
        }

        [HttpPost]
        public IActionResult ApproveReview(int id)
        {
            var review = _db.Reviews.Find(id);
            if (review != null)
            {
                review.IsApproved = true;
                _db.SaveChanges();
            }
            return Done("mod", "Отзыв опубликован на сайте.");
        }

        [HttpPost]
        public IActionResult DeleteReview(int id)
        {
            var review = _db.Reviews.Find(id);
            if (review != null)
            {
                _db.Reviews.Remove(review);
                _db.SaveChanges();
            }
            return Done("mod", "Отзыв удалён.");
        }

        [HttpPost]
        public IActionResult SaveService(int id, string? name, int duration, int? maxGroup, int price, string? desc)
        {
            var title = (name ?? "").Trim();
            var text = (desc ?? "").Trim();

            if (title.Length == 0 || title.Length > 100 || duration < 10 || price < 0 || (maxGroup ?? 1) < 1 || text.Length > 500)
            {
                return Fail("svc", "Проверьте поля услуги: название до 100 символов, длительность от 10 минут, цена не меньше 0, описание до 500 символов.");
            }

            var service = id == 0 ? new Service() : _db.Services.Find(id);
            if (service == null)
            {
                return Fail("svc", "Услуга не найдена.");
            }

            service.Name = title;
            service.DurationMinutes = duration;
            service.MaxGroupSize = maxGroup;
            service.PricePerSession = price;
            service.Description = text.Length == 0 ? null : text;

            if (id == 0)
            {
                _db.Services.Add(service);
            }
            _db.SaveChanges();

            return Done("svc", "Услуга сохранена.");
        }

        [HttpPost]
        public IActionResult DeleteService(int id)
        {
            var service = _db.Services.Find(id);
            if (service == null)
            {
                return Back("svc");
            }
            if (_db.Bookings.Any(b => b.ServiceId == id))
            {
                return Fail("svc", "Нельзя удалить услугу «" + service.Name + "»: на неё есть записи.");
            }

            foreach (var review in _db.Reviews.Where(r => r.ServiceId == id))
            {
                review.ServiceId = null;
            }
            _db.Services.Remove(service);
            _db.SaveChanges();

            return Done("svc", "Услуга удалена.");
        }

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
                return Fail("tr", "Проверьте поля тренера: имя до 150 символов, специализация до 200, опыт от 0 до 70 лет, биография и достижения до 1000 символов.");
            }

            var trainer = id == 0 ? new Trainer() : _db.Trainers.Find(id);
            if (trainer == null)
            {
                return Fail("tr", "Тренер не найден.");
            }

            if (photo != null && photo.Length > 0)
            {
                var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                if (photo.Length > 600 * 1024 || !PhotoExtensions.Contains(extension))
                {
                    return Fail("tr", "Фото должно быть изображением (jpg, png, webp, gif, avif) размером до 600 КБ.");
                }

                var fileName = Guid.NewGuid().ToString("N") + extension;
                var folder = Path.Combine(_env.WebRootPath, "img", "trainers");
                Directory.CreateDirectory(folder);
                using (var stream = System.IO.File.Create(Path.Combine(folder, fileName)))
                {
                    photo.CopyTo(stream);
                }
                trainer.ImagePath = "img/trainers/" + fileName;
            }

            trainer.FullName = fullName;
            trainer.Specialization = specialization;
            trainer.ExperienceYears = experience;
            trainer.Biography = biography.Length == 0 ? null : biography;
            trainer.Achievements = achievements.Length == 0 ? null : achievements;

            if (id == 0)
            {
                _db.Trainers.Add(trainer);
            }
            _db.SaveChanges();

            return Done("tr", "Тренер сохранён.");
        }

        [HttpPost]
        public IActionResult DeleteTrainer(int id)
        {
            var trainer = _db.Trainers.Find(id);
            if (trainer == null)
            {
                return Back("tr");
            }
            if (_db.Bookings.Any(b => b.TrainerId == id))
            {
                return Fail("tr", "Нельзя удалить тренера «" + trainer.FullName + "»: на него есть записи.");
            }

            foreach (var review in _db.Reviews.Where(r => r.TrainerId == id))
            {
                review.TrainerId = null;
            }
            _db.Trainers.Remove(trainer);
            _db.SaveChanges();

            return Done("tr", "Тренер удалён.");
        }

        [HttpPost]
        public IActionResult AddTime(string? newTime)
        {
            if (!TimeSpan.TryParse(newTime, out var parsed) || parsed < TimeSpan.Zero || parsed >= TimeSpan.FromHours(24))
            {
                return Fail("sch", "Введите время в формате ЧЧ:ММ.");
            }

            var time = new TimeSpan(parsed.Hours, parsed.Minutes, 0);
            if (_db.TimeSlots.Any(s => s.SlotTime == time))
            {
                return Fail("sch", "Такое время уже есть в списке.");
            }

            _db.TimeSlots.Add(new TimeSlot { SlotTime = time });
            _db.SaveChanges();

            return Done("sch", "Время добавлено.");
        }

        [HttpPost]
        public IActionResult DeleteTime(int id)
        {
            var slot = _db.TimeSlots.Find(id);
            if (slot != null)
            {
                _db.TimeSlots.Remove(slot);
                _db.SaveChanges();
            }
            return Done("sch", "Время убрано из расписания.");
        }

        [HttpPost]
        public IActionResult AddClosedDay(DateTime newClosed)
        {
            var day = newClosed.Date;
            if (day.Year < 2000)
            {
                return Fail("sch", "Выберите дату.");
            }
            if (_db.ClosedDays.Any(c => c.ClosedDate == day))
            {
                return Fail("sch", "Этот день уже закрыт.");
            }

            _db.ClosedDays.Add(new ClosedDay { ClosedDate = day });
            _db.SaveChanges();

            return Done("sch", "День закрыт для записи.");
        }

        [HttpPost]
        public IActionResult DeleteClosedDay(int id)
        {
            var day = _db.ClosedDays.Find(id);
            if (day != null)
            {
                _db.ClosedDays.Remove(day);
                _db.SaveChanges();
            }
            return Done("sch", "День снова открыт для записи.");
        }

        [HttpPost]
        public IActionResult SaveRoles(IFormCollection form)
        {
            var myId = GetUserId();
            foreach (var user in _db.Users.Where(u => u.Id != myId).ToList())
            {
                var role = form["role_" + user.Id].ToString();
                if (Roles.All.Contains(role))
                {
                    user.Role = role;
                }
            }
            _db.SaveChanges();

            return Done("usr", "Роли сохранены. Пользователь получит новую роль при следующем входе.");
        }

        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            if (id == GetUserId())
            {
                return Fail("usr", "Свой аккаунт удалить нельзя.");
            }

            var user = _db.Users.Find(id);
            if (user != null)
            {
                _db.Bookings.RemoveRange(_db.Bookings.Where(b => b.UserId == id));
                _db.Users.Remove(user);
                _db.SaveChanges();
            }
            return Done("usr", "Пользователь и его записи удалены.");
        }

        [HttpPost]
        public IActionResult SaveSettings(string? address, string? phone, string? email, string? hours)
        {
            var newAddress = (address ?? "").Trim();
            var newPhone = (phone ?? "").Trim();
            var newEmail = (email ?? "").Trim();
            var newHours = (hours ?? "").Trim();

            if (newAddress.Length == 0 || newPhone.Length == 0 || newEmail.Length == 0 || newHours.Length == 0
                || newAddress.Length > 200 || newPhone.Length > 50 || newEmail.Length > 100 || newHours.Length > 200
                || !newEmail.Contains('@'))
            {
                return Fail("set", "Заполните все поля контактов (e-mail должен содержать @).");
            }

            var contact = _db.Contacts.FirstOrDefault();
            if (contact == null)
            {
                contact = new Contact();
                _db.Contacts.Add(contact);
            }

            contact.Address = newAddress;
            contact.Phone = newPhone;
            contact.Email = newEmail;
            contact.WorkingHours = newHours;
            _db.SaveChanges();

            return Done("set", "Контакты сохранены: они обновились на всех страницах сайта.");
        }

        private IActionResult Back(string tab)
        {
            return RedirectToAction("Index", new { tab });
        }

        private IActionResult Done(string tab, string message)
        {
            TempData["Ok"] = message;
            return Back(tab);
        }

        private IActionResult Fail(string tab, string message)
        {
            TempData["Err"] = message;
            return Back(tab);
        }

        private int GetUserId()
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
            return id;
        }
    }
}
