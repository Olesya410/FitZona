using FitZone.Data;
using FitZone.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitZone.Controllers
{
    public class HomeController : Controller
    {
        private const string ManagerRoles = Roles.Manager + "," + Roles.Admin;
        private static readonly PasswordHasher<User> hasher = new PasswordHasher<User>();

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

        [HttpGet]
        public IActionResult Booking(int? service, int? trainer, string? date, string? time)
        {
            if (TempData["BookOk"] != null)
            {
                ViewData["BookState"] = "ok";
            }
            return View(BuildBookingModel(service, trainer, date, time));
        }

        [HttpPost]
        public IActionResult Booking(int service, int trainer, string? date, string? time, bool agree)
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

            var error = CheckBooking(service, trainer, date, time, agree);
            if (error != null)
            {
                ViewData["BookState"] = "error";
                ViewData["BookError"] = error;
                return View(model);
            }

            _db.Bookings.Add(new Booking
            {
                UserId = GetUserId(),
                ServiceId = service,
                TrainerId = trainer,
                TrainingDate = DateTime.Parse(date!).Date,
                TrainingTime = TimeSpan.Parse(time!),
                Status = BookingStatuses.Pending,
                CreatedAt = DateTime.Now
            });
            _db.SaveChanges();

            TempData["BookOk"] = true;
            return RedirectToAction("Booking");
        }

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

            var author = User.Identity?.Name ?? "Клиент";
            _db.Reviews.Add(new Review
            {
                AuthorName = author.Length > 100 ? author.Substring(0, 100) : author,
                Rating = rating,
                ReviewDate = DateTime.Today,
                Comment = comment,
                IsApproved = false
            });
            _db.SaveChanges();

            TempData["ReviewOk"] = true;
            return RedirectToAction("Reviews");
        }

        [HttpGet]
        public IActionResult Contacts()
        {
            if (TempData["FeedbackOk"] != null)
            {
                ViewData["FeedbackState"] = "ok";
            }
            return View(_db.Contacts.FirstOrDefault() ?? new Contact());
        }

        [HttpPost]
        public IActionResult Contacts(string? name, string? contact, string? message)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
            {
                ViewData["FeedbackError"] = "Укажите имя (минимум 2 символа).";
            }
            else if (string.IsNullOrWhiteSpace(contact))
            {
                ViewData["FeedbackError"] = "Укажите телефон или e-mail, чтобы мы могли ответить.";
            }
            else if (string.IsNullOrWhiteSpace(message) || message.Trim().Length < 5)
            {
                ViewData["FeedbackError"] = "Напишите сообщение (минимум 5 символов).";
            }

            if (ViewData["FeedbackError"] != null)
            {
                return View(_db.Contacts.FirstOrDefault() ?? new Contact());
            }

            _db.Feedbacks.Add(new Feedback
            {
                Name = name!.Trim(),
                Contact = contact!.Trim(),
                Message = message!.Trim(),
                CreatedAt = DateTime.Now
            });
            _db.SaveChanges();

            TempData["FeedbackOk"] = true;
            return RedirectToAction("Contacts");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string? email, string? password)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == (email ?? "").Trim());

            if (user == null || string.IsNullOrEmpty(user.PasswordHash)
                || hasher.VerifyHashedPassword(user, user.PasswordHash, password ?? "") == PasswordVerificationResult.Failed)
            {
                ViewData["Error"] = "Неверный e-mail или пароль.";
                return View();
            }

            await SignInUser(user);
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

            if (fullName.Length < 2 || fullName.Length > 150)
                ViewData["Error"] = "Введите имя (от 2 до 150 символов).";
            else if (mail.Length > 150 || !mail.Contains('@') || mail.Contains(' '))
                ViewData["Error"] = "Введите корректный e-mail.";
            else if (pass.Length < 8 || !pass.Any(char.IsDigit))
                ViewData["Error"] = "Пароль: минимум 8 символов и одна цифра.";
            else if (pass != password2)
                ViewData["Error"] = "Пароли не совпадают.";
            else if (_db.Users.Any(u => u.Email == mail))
                ViewData["Error"] = "Пользователь с таким e-mail уже зарегистрирован.";

            if (ViewData["Error"] != null)
            {
                return View();
            }

            var user = new User { FullName = fullName, Email = mail, Role = Roles.Client, CreatedAt = DateTime.Now };
            user.PasswordHash = hasher.HashPassword(user, pass);
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

        [Authorize]
        [HttpGet]
        public IActionResult Account()
        {
            var userId = GetUserId();
            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
            {
                return RedirectToAction("Logout");
            }

            var bookings = _db.Bookings.Where(b => b.UserId == userId).OrderBy(b => b.TrainingDate).ToList();
            var today = DateTime.Today;

            ViewBag.Upcoming = bookings.Where(b => b.TrainingDate >= today && b.Status == BookingStatuses.Confirmed).ToList();
            ViewBag.History = bookings.Where(b => b.TrainingDate < today || b.Status != BookingStatuses.Confirmed)
                .OrderByDescending(b => b.TrainingDate).ToList();
            ViewBag.Services = _db.Services.ToDictionary(s => s.Id, s => s.Name);
            ViewBag.Trainers = _db.Trainers.ToDictionary(t => t.Id, t => t.FullName);

            return View(user);
        }

        [Authorize]
        [HttpPost]
        public IActionResult UpdateProfile(string? fullName, string? phone)
        {
            var userId = GetUserId();
            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
            {
                return RedirectToAction("Logout");
            }

            if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2)
            {
                TempData["ProfileError"] = "Имя должно быть не короче 2 символов.";
                return RedirectToAction("Account");
            }

            user.FullName = fullName.Trim();
            if (!string.IsNullOrWhiteSpace(phone))
            {
                user.Phone = phone.Trim();
            }
            _db.SaveChanges();

            TempData["ProfileOk"] = true;
            return RedirectToAction("Account");
        }

        [Authorize]
        [HttpPost]
        public IActionResult CancelBooking(int cancelId)
        {
            var userId = GetUserId();
            var booking = _db.Bookings.FirstOrDefault(b => b.Id == cancelId && b.UserId == userId);
            if (booking == null)
            {
                return RedirectToAction("Account");
            }

            if ((booking.TrainingDate + booking.TrainingTime - DateTime.Now).TotalHours < 24)
            {
                TempData["BookingError"] = "Отменить запись можно не позднее чем за 24 часа до начала.";
                return RedirectToAction("Account");
            }

            booking.Status = BookingStatuses.Cancelled;
            _db.SaveChanges();

            TempData["BookingOk"] = true;
            return RedirectToAction("Account");
        }

        [Authorize(Roles = ManagerRoles)]
        [HttpGet]
        public IActionResult Manager()
        {
            var reviews = _db.Reviews.OrderByDescending(r => r.ReviewDate).ToList();

            ViewBag.Bookings = _db.Bookings.OrderByDescending(b => b.TrainingDate).ToList();
            ViewBag.Services = _db.Services.ToDictionary(s => s.Id, s => s.Name);
            ViewBag.Trainers = _db.Trainers.ToDictionary(t => t.Id, t => t.FullName);
            ViewBag.Users = _db.Users.ToDictionary(u => u.Id, u => (u.FullName, Email: u.Email, Phone: u.Phone ?? ""));
            ViewBag.Feedbacks = _db.Feedbacks.OrderByDescending(f => f.CreatedAt).ToList();
            ViewBag.ReviewsModeration = reviews.Where(r => !r.IsApproved).ToList();
            ViewBag.ReviewsPublished = reviews.Where(r => r.IsApproved).ToList();

            return View();
        }

        [Authorize(Roles = ManagerRoles)]
        [HttpPost]
        public IActionResult UpdateStatuses()
        {
            foreach (var booking in _db.Bookings.ToList())
            {
                var status = Request.Form["status_" + booking.Id].ToString();
                if (BookingStatuses.All.Contains(status))
                {
                    booking.Status = status;
                }
            }
            _db.SaveChanges();

            TempData["StatusesOk"] = true;
            return RedirectToAction("Manager");
        }

        [Authorize(Roles = ManagerRoles)]
        [HttpPost]
        public IActionResult ApproveReview(int id)
        {
            var review = _db.Reviews.Find(id);
            if (review != null)
            {
                review.IsApproved = true;
                _db.SaveChanges();
            }
            return RedirectToAction("Manager");
        }

        [Authorize(Roles = ManagerRoles)]
        [HttpPost]
        public IActionResult RejectReview(int id)
        {
            var review = _db.Reviews.Find(id);
            if (review != null)
            {
                _db.Reviews.Remove(review);
                _db.SaveChanges();
            }
            return RedirectToAction("Manager");
        }

        [Authorize(Roles = Roles.Admin)]
        public IActionResult Admin()
        {
            return RedirectToAction("Index", "Admin");
        }

        private int GetUserId()
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
            return id;
        }

        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
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

        private BookingViewModel BuildBookingModel(int? service, int? trainer, string? date, string? time)
        {
            var booked = new List<string>();
            if (trainer != null && DateTime.TryParse(date, out var day))
            {
                var bookingDay = day.Date;
                booked = _db.Bookings
                    .Where(b => b.TrainerId == trainer && b.TrainingDate == bookingDay && b.Status != BookingStatuses.Cancelled)
                    .Select(b => b.TrainingTime.ToString("hh\\:mm"))
                    .ToList();
            }

            return new BookingViewModel
            {
                Services = _db.Services.OrderBy(s => s.Id).ToList(),
                Trainers = _db.Trainers.OrderBy(t => t.Id).ToList(),
                Slots = _db.TimeSlots.OrderBy(s => s.SlotTime).ToList().Select(s => s.SlotTime.ToString("hh\\:mm")).ToList(),
                ClosedDays = _db.ClosedDays.Where(c => c.ClosedDate >= DateTime.Today).OrderBy(c => c.ClosedDate).Select(c => c.ClosedDate).ToList(),
                BookedTimes = booked,
                SelectedServiceId = service,
                SelectedTrainerId = trainer,
                SelectedDate = date ?? "",
                SelectedTime = time ?? ""
            };
        }

        private string? CheckBooking(int service, int trainer, string? date, string? time, bool agree)
        {
            if (!_db.Services.Any(s => s.Id == service) || !_db.Trainers.Any(t => t.Id == trainer))
                return "Выберите направление и тренера.";

            if (!DateTime.TryParse(date, out var parsedDay))
                return "Выберите дату занятия.";
            var day = parsedDay.Date;

            if (day < DateTime.Today)
                return "Нельзя записаться на прошедшую дату.";
            if (day.DayOfWeek == DayOfWeek.Sunday)
                return "По воскресеньям записи нет.";
            if (_db.ClosedDays.Any(c => c.ClosedDate == day))
                return "На эту дату запись закрыта. Выберите другой день.";

            if (!TimeSpan.TryParse(time, out var slot))
                return "Выберите время занятия.";
            if (!_db.TimeSlots.Any(s => s.SlotTime == slot))
                return "Такого времени для записи нет. Выберите время из списка.";
            if (!agree)
                return "Подтвердите согласие на обработку персональных данных.";

            var userId = GetUserId();
            if (_db.Bookings.Any(b => b.TrainerId == trainer && b.TrainingDate == day && b.TrainingTime == slot && b.Status != BookingStatuses.Cancelled))
                return "Это время у тренера уже занято. Выберите другое.";
            if (_db.Bookings.Any(b => b.UserId == userId && b.TrainingDate == day && b.TrainingTime == slot && b.Status != BookingStatuses.Cancelled))
                return "Вы уже записаны на это время. Отмените старую запись в личном кабинете.";

            return null;
        }
    }
}
