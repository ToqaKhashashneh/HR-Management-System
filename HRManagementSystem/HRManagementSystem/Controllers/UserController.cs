using HRManagementSystem.Models;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace HRManagementSystem.Controllers
{
    public class UserController : Controller
    {
        private readonly MyDbContext _context;

        public UserController(MyDbContext context)
        {
            _context = context;
        }

        //RoleSelection
        public ActionResult RoleSelection()
        {
            return View();
        }

        //Login
        public ActionResult Login(string role)
        {
            if (!string.IsNullOrEmpty(role))
            {
                HttpContext.Session.SetString("UserRole", role);
            }
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password, bool rememberMe)
        {
            var role = HttpContext.Session.GetString("UserRole");
            object? user = null;

            if (role == "Employee")
                user = _context.Employees.FirstOrDefault(e => e.Email == email && e.PasswordHash == password);
            else if (role == "HR")
                user = _context.Hrs.FirstOrDefault(h => h.Email == email && h.PasswordHash == password);
            else if (role == "Manager")
                user = _context.Managers.FirstOrDefault(m => m.Email == email && m.PasswordHash == password);

            if (user != null)
            {
                SetUserSessionAndCookies(user, role, rememberMe);
                return RedirectToAction("Dashboard", role);
            }

            ViewBag.Message = $"Invalid email or password for {role}.";
            return View();
        }

        private void SetUserSessionAndCookies(object user, string role, bool rememberMe)
        {
            HttpContext.Session.SetString("UserRole", role);
            HttpContext.Session.SetString("UserRole", role);

            if (user is Employee employee)
            {
                HttpContext.Session.SetInt32("UserID", employee.Id);
                HttpContext.Session.SetString("UserName", employee.Name);
                HttpContext.Session.SetString("UserEmail", employee.Email);
                HttpContext.Session.SetString("UserAddress", employee.Address);
                HttpContext.Session.SetString("UserPosition", value: employee.Position);
                HttpContext.Session.SetString("UserProfileImage", string.IsNullOrEmpty(employee.ProfileImage) ? "~/img/default.jpg" : employee.ProfileImage);
            }
            else if (user is Hr hr)
            {
                HttpContext.Session.SetInt32("UserID", hr.Id);
                HttpContext.Session.SetString("UserName", hr.Name);
                HttpContext.Session.SetString("UserEmail", hr.Email);
                HttpContext.Session.SetString("UserAddress", hr.Address);
                HttpContext.Session.SetString("UserProfileImage", string.IsNullOrEmpty(hr.ProfileImage) ? "~/img/default.jpg" : hr.ProfileImage);
            }
            else if (user is Manager manager)
            {
                HttpContext.Session.SetInt32("UserID", manager.Id);
                HttpContext.Session.SetString("UserName", manager.Name);
                HttpContext.Session.SetString("UserEmail", manager.Email);
                HttpContext.Session.SetString("UserAddress", manager.Address);
                HttpContext.Session.SetString("UserProfileImage", string.IsNullOrEmpty(manager.ProfileImage) ? "~/img/default.jpg" : manager.ProfileImage);
            }

            if (rememberMe)
            {
                CookieOptions option = new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(7),
                    HttpOnly = true
                };
                Response.Cookies.Append("UserRole", role, option);
                Response.Cookies.Append("UserID", HttpContext.Session.GetInt32("UserID").ToString(), option);
                Response.Cookies.Append("UserName", HttpContext.Session.GetString("UserName"), option);
                Response.Cookies.Append("UserEmail", HttpContext.Session.GetString("UserEmail"), option);
                Response.Cookies.Append("UserAddress", HttpContext.Session.GetString("UserAddress"), option);
                Response.Cookies.Append("UserProfileImage", HttpContext.Session.GetString("UserProfileImage"), option);
            }
        }


        //ResetPasswordRequest
        [HttpGet]
        public IActionResult ResetPasswordRequest()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ResetPasswordRequest(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Please enter your email address.";
                return View();
            }

            var role = HttpContext.Session.GetString("UserRole");
            object? user = FindUserByEmail(email);

            if (user == null)
            {
                ViewBag.Error = "No account found with this email.";
                return View();
            }

            string resetToken = Guid.NewGuid().ToString();
            string resetLink = Url.Action("ResetPassword", "User", new { token = resetToken, email = email }, Request.Scheme);

            if (!SendResetEmail(email, resetLink))
            {
                ViewBag.Error = "An error occurred while sending the email.";
                return View();
            }

            ViewBag.Message = "A password reset link has been sent to your email.";
            return View();
        }

        private bool SendResetEmail(string email, string resetLink)
        {
            try
            {
                MailMessage mail = new MailMessage
                {
                    From = new MailAddress("nadaqdesat@gmail.com"),
                    Subject = "Password Reset Request",
                    Body = $"Click the following link to reset your password: <a href='{resetLink}'>Reset Password</a>",
                    IsBodyHtml = true
                };

                mail.To.Add(email);

                SmtpClient smtp = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("nadaqdesat@gmail.com", "aibl avfx vyag dxgn"),
                    EnableSsl = true
                };

                smtp.Send(mail);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending email: " + ex.Message);
                return false;
            }
        }

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                return BadRequest("Invalid password reset link.");
            }

            object? user = FindUserByEmail(email);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(string email, string token, string newPassword)
        {
            object? user = FindUserByEmail(email);

            if (user == null)
            {
                ViewBag.Error = "Invalid request.";
                return View();
            }

            if (user is Employee employee)
                employee.PasswordHash = newPassword;
            else if (user is Hr hr)
                hr.PasswordHash = newPassword;
            else if (user is Manager manager)
                manager.PasswordHash = newPassword;

            _context.SaveChanges();

            ViewBag.Message = "Password has been successfully reset.";
            return View();
        }



        private object? FindUserByEmail(string email)
        {
            return (object?)_context.Employees.FirstOrDefault(e => e.Email == email)
                ?? (object?)_context.Hrs.FirstOrDefault(h => h.Email == email)
                ?? (object?)_context.Managers.FirstOrDefault(m => m.Email == email);
        }

        public IActionResult Profile()
        {
            return View();
        }

        public IActionResult EditProfile()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(IFormFile ProfileImage, string UserName, string UserAddress)
        {
            var userId = HttpContext.Session.GetInt32("UserID") ?? int.Parse(Request.Cookies["UserID"]);
            var role = HttpContext.Session.GetString("UserRole") ?? Request.Cookies["UserRole"];

            object? user = null;

            if (role == "Employee")
                user = await _context.Employees.FindAsync(userId);
            else if (role == "HR")
                user = await _context.Hrs.FindAsync(userId);
            else if (role == "Manager")
                user = await _context.Managers.FindAsync(userId);

            string? fileName = null;

            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/img");
                fileName = $"{Guid.NewGuid()}{Path.GetExtension(ProfileImage.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(stream);
                }
            }

            if (user is Employee employee)
            {
                employee.Name = UserName;
                employee.Address = UserAddress;
                if (!string.IsNullOrEmpty(fileName))
                {
                    employee.ProfileImage = fileName;
                }
            }
            else if (user is Hr hr)
            {
                hr.Name = UserName;
                hr.Address = UserAddress;
                if (!string.IsNullOrEmpty(fileName))
                {
                    hr.ProfileImage = fileName;
                }
            }
            else if (user is Manager manager)
            {
                manager.Name = UserName;
                manager.Address = UserAddress;
                if (!string.IsNullOrEmpty(fileName))
                {
                    manager.ProfileImage = fileName;
                }
            }

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("UserName", UserName);
            HttpContext.Session.SetString("UserAddress", UserAddress);
            if (!string.IsNullOrEmpty(fileName))
            {
                HttpContext.Session.SetString("UserProfileImage", fileName);
            }

            CookieOptions option = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(7),
                HttpOnly = true
            };
            Response.Cookies.Append("UserName", UserName, option);
            Response.Cookies.Append("UserAddress", UserAddress, option);
            if (!string.IsNullOrEmpty(fileName))
            {
                Response.Cookies.Append("UserProfileImage", fileName, option);
            }

            return RedirectToAction("Profile");
        }

        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var userId = HttpContext.Session.GetInt32("UserID") ?? int.Parse(Request.Cookies["UserID"]);
            var role = HttpContext.Session.GetString("UserRole") ?? Request.Cookies["UserRole"];

            object? user = null;

            if (role == "Employee")
                user = _context.Employees.FirstOrDefault(e => e.Id == userId);
            else if (role == "HR")
                user = _context.Hrs.FirstOrDefault(e => e.Id == userId);
            else if (role == "Manager")
                user = _context.Managers.FirstOrDefault(e => e.Id == userId);

            if (user == null)
            {
                ViewBag.ErrorMessage = "User not found.";
                return View();
            }

            var userEntity = (dynamic)user;

            if (userEntity.PasswordHash != currentPassword)
            {
                ViewBag.ErrorMessage = "Current password is incorrect.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.ErrorMessage = "New password and confirmation do not match.";
                return View();
            }

            userEntity.PasswordHash = newPassword;
            _context.SaveChanges();

            ViewBag.SuccessMessage = "Password successfully updated!";
            return View();
        }



        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("UserRole");
            Response.Cookies.Delete("UserEmail");
            Response.Cookies.Delete("UserID");
            Response.Cookies.Delete("UserName");
            Response.Cookies.Delete("UserProfileImage");

            return RedirectToAction("Index", "Home");
        }
    }
}
