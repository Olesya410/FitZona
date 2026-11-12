using System.Security.Claims;
using FitZone.Data;
using FitZone.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FitZone.Controllers
{
    public class HomeController : Controller
    {
        private static readonly PasswordHasher<User> Hasher = new PasswordHasher<User>();

        // Через конструктор получаем доступ к базе данных
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            var model = new IndexViewModel
            {
                Services = _db.Services.OrderBy(s => s.Id).ToList(),
                Trainers = _db.Trainers.OrderBy(t => t.Id).ToList(),
                Reviews = _db.Reviews.Where(r => r.IsApproved).OrderByDescending(r => r.ReviewDate).Take(3).ToList(),
                Contact = _db.Contacts.FirstOrDefault() ?? new Contact()
            };
            return View(model);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Services()
        {
            return View(_db.Services.OrderBy(s => s.Id).ToList());
        }

        public IActionResult Trainers()
        {
            return View(_db.Trainers.OrderBy(t => t.Id).ToList());
        }

        // ---------- Запись на тренировку ----------

        // service, trainer, date, time — необязательные значения из ссылок «Записаться» и формы на главной
        [HttpGet]
        public IActionResult Booking(int? service, int? trainer, string? date, string? time)
        {
            var model = BuildBookingModel(service, trainer, date, time);
            if (TempData["BookOk"] != null)
            {
                ViewData["BookState"] = "ok";
            }
            return View(model);
        }

        [HttpPost]
        public IActionResult Booking(int service, int trainer, string? date, string? time)
        {
            var model = BuildBookingModel(service, trainer, date, time);

            if (User.Identity?.IsAuthenticated != true)
            {
                ViewData["BookState"] = "login";
                return View(model);
            }
            if (!User.IsInRole(Roles.Client))
            {
                ViewData["BookState"] = "staff";
                return View(model);
            }

            string? error = ValidateBooking(service, trainer, date, time, out var day, out var slot);
            if (error != null)
            {
                ViewData["BookState"] = "error";
                ViewData["BookError"] = error;
                return View(model);
            }

            _db.Bookings.Add(new Booking
            {
                UserId = CurrentUserId(),
                ServiceId = service,
                TrainerId = trainer,
                TrainingDate = day,
                TrainingTime = slot,
                Status = BookingStatuses.Pending,
                CreatedAt = DateTime.Now
            });
            _db.SaveChanges();

            TempData["BookOk"] = true;
            return RedirectToAction("Booking");
        }

        private BookingViewModel BuildBookingModel(int? service, int? trainer, string? date, string? time)
        {
            return new BookingViewModel
            {
                Services = _db.Services.OrderBy(s => s.Id).ToList(),
                Trainers = _db.Trainers.OrderBy(t => t.Id).ToList(),
                Slots = _db.TimeSlots.OrderBy(s => s.SlotTime).ToList().Select(s => s.SlotTime.ToString("hh\\:mm")).ToList(),
                ClosedDays = _db.ClosedDays.Where(c => c.ClosedDate >= DateTime.Today).OrderBy(c => c.ClosedDate).Select(c => c.ClosedDate).ToList(),
                SelectedServiceId = service,
                SelectedTrainerId = trainer,
                SelectedDate = date ?? "",
                SelectedTime = time ?? ""
            };
        }

        // Возвращает текст ошибки или null, если заявку можно сохранять
        private string? ValidateBooking(int service, int trainer, string? date, string? time, out DateTime day, out TimeSpan slot)
        {
            day = DateTime.MinValue;
            slot = TimeSpan.Zero;

            if (!_db.Services.Any(s => s.Id == service) || !_db.Trainers.Any(t => t.Id == trainer))
            {
                return "Выберите направление и тренера.";
            }
            if (!DateTime.TryParse(date, out var parsedDay))
            {
                return "Выберите дату занятия.";
            }
            // Внутри лямбд out-параметры использовать нельзя, поэтому работаем с локальными копиями
            var bookingDay = parsedDay.Date;
            day = bookingDay;
            if (bookingDay < DateTime.Today)
            {
                return "Нельзя записаться на прошедшую дату.";
            }
            if (bookingDay.DayOfWeek == DayOfWeek.Sunday)
            {
                return "По воскресеньям записи нет.";
            }
            if (_db.ClosedDays.Any(c => c.ClosedDate == bookingDay))
            {
                return "На эту дату запись закрыта. Выберите другой день.";
            }
            if (!TimeSpan.TryParse(time, out var bookingTime))
            {
                return "Выберите время занятия.";
            }
            slot = bookingTime;
            if (!_db.TimeSlots.Any(s => s.SlotTime == bookingTime))
            {
                return "Такого времени для записи нет. Выберите время из списка.";
            }
            if (_db.Bookings.Any(b => b.TrainerId == trainer && b.TrainingDate == bookingDay && b.TrainingTime == bookingTime && b.Status != BookingStatuses.Cancelled))
            {
                return "Это время у тренера уже занято. Выберите другое.";
            }
            return null;
        }

        // ---------- Отзывы ----------

        [HttpGet]
        public IActionResult Reviews()
        {
            return View(_db.Reviews.Where(r => r.IsApproved).OrderByDescending(r => r.ReviewDate).ToList());
        }

        [HttpPost]
        public IActionResult Reviews(int rating, string? text)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            var comment = (text ?? "").Trim();
            if (rating < 1 || rating > 5 || comment.Length < 10 || comment.Length > 1000)
            {
                TempData["ReviewError"] = "Поставьте оценку и напишите отзыв от 10 до 1000 символов.";
                return RedirectToAction("Reviews");
            }

            // Новый отзыв попадает на модерацию и появляется на сайте после одобрения админом
            var author = User.Identity?.Name ?? "Клиент";
            if (author.Length > 100)
            {
                author = author.Substring(0, 100);
            }
            _db.Reviews.Add(new Review
            {
                AuthorName = author,
                Rating = rating,
                ReviewDate = DateTime.Today,
                Comment = comment,
                IsApproved = false
            });
            _db.SaveChanges();

            TempData["ReviewOk"] = true;
            return RedirectToAction("Reviews");
        }

        public IActionResult Contacts()
        {
            return View(_db.Contacts.FirstOrDefault() ?? new Contact());
        }

        // ---------- Вход, регистрация, выход ----------

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string? email, string? password, string? returnUrl)
        {
            var mail = (email ?? "").Trim();
            var user = _db.Users.FirstOrDefault(u => u.Email == mail);

            var ok = false;
            if (user != null && !string.IsNullOrEmpty(user.PasswordHash))
            {
                ok = Hasher.VerifyHashedPassword(user, user.PasswordHash, password ?? "") != PasswordVerificationResult.Failed;
            }
            if (!ok || user == null)
            {
                ViewData["Error"] = "Неверный e-mail или пароль.";
                return View();
            }

            await SignInUser(user);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToCabinet(user.Role);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string? name, string? email, string? password, string? password2)
        {
            var fullName = (name ?? "").Trim();
            var mail = (email ?? "").Trim();
            var pass = password ?? "";

            string? error = null;
            if (fullName.Length < 2 || fullName.Length > 150)
            {
                error = "Введите имя (от 2 до 150 символов).";
            }
            else if (mail.Length > 150 || !mail.Contains('@') || mail.Contains(' '))
            {
                error = "Введите корректный e-mail.";
            }
            else if (pass.Length < 8 || !pass.Any(char.IsDigit))
            {
                error = "Пароль: минимум 8 символов и одна цифра.";
            }
            else if (pass != password2)
            {
                error = "Пароли не совпадают.";
            }
            else if (_db.Users.Any(u => u.Email == mail))
            {
                error = "Пользователь с таким e-mail уже зарегистрирован.";
            }

            if (error != null)
            {
                ViewData["Error"] = error;
                return View();
            }

            var user = new User
            {
                FullName = fullName,
                Email = mail,
                Role = Roles.Client,
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = Hasher.HashPassword(user, pass);
            _db.Users.Add(user);
            _db.SaveChanges();

            await SignInUser(user);
            return RedirectToCabinet(user.Role);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index");
        }

        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }

        private IActionResult RedirectToCabinet(string role)
        {
            if (role == Roles.Admin) return RedirectToAction("Index", "Admin");
            if (role == Roles.Manager) return RedirectToAction("Manager");
            return RedirectToAction("Account");
        }

        private int CurrentUserId()
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
            return id;
        }

        // ---------- Кабинеты ----------

        public IActionResult Account()
        {
            return View();
        }

        public IActionResult Manager()
        {
            return View();
        }

        // Ссылка «Админ-панель» в шапке ведёт сюда; сама панель — в AdminController
        [Authorize(Roles = Roles.Admin)]
        public IActionResult Admin()
        {
            return RedirectToAction("Index", "Admin");
        }
    }
}
