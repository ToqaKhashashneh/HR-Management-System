//using System.Security.Cryptography;
using System.Net.Mail;
using System.Net;
using HRManagementSystem.Models;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using iTextSharp.text.pdf;
using iTextSharp.text;

namespace HRManagementSystem.Controllers
{
    public class ManagerController : Controller
    {

        private readonly MyDbContext _context;

        public ManagerController(MyDbContext context)
        {
            _context = context;
        }

        public IActionResult Dashboard()
        {
            int managerId = 0;
            if (HttpContext.Session.GetInt32("UserID") != null)
            {
                managerId = HttpContext.Session.GetInt32("UserID") ?? 0;
            }
            else if (Request.Cookies["UserID"] != null)
            {
                managerId = int.Parse(Request.Cookies["UserID"]);
            }

            int totalEmployees = _context.Employees.Count(e => e.ManagerId == managerId);

            int pendingVacationsAndLeaves = _context.VaccationRequests
                .Count(v => v.Status == "Pending" && _context.Employees.Any(e => e.Id == v.EmployeeId && e.ManagerId == managerId))
                +
                _context.LeaveRequests
                .Count(l => l.Status == "Pending" && _context.Employees.Any(e => e.Id == l.EmployeeId && e.ManagerId == managerId));

            int totalAbsences = _context.Employees
                .Where(e => e.ManagerId == managerId &&
                !_context.Attendances.Any(a => a.EmployeeId == e.Id &&
                                               a.Date == DateOnly.FromDateTime(DateTime.Today) &&
                                               a.PunchInTime != null &&
                                               a.PunchInTime != TimeOnly.MinValue))
                .Count();




            ViewData["TotalEmployees"] = totalEmployees > 0 ? totalEmployees : 0;
            ViewData["PendingVacations_Leaves"] = pendingVacationsAndLeaves > 0 ? pendingVacationsAndLeaves : 0;
            ViewData["totalAbsences"] = totalAbsences > 0 ? totalAbsences : 0;

            var employees = _context.Employees
             .Where(e => e.ManagerId == managerId)
             .Select(e => new EmployeeViewModel
             {
                 ID = e.Id,
                 Name = e.Name,
                 Email = e.Email,
                 Position = e.Position,
                 Address = e.Address,
                 IsAbsent = !_context.Attendances.Any(a => a.EmployeeId == e.Id
                                                  && a.Date == DateOnly.FromDateTime(DateTime.Today)
                                                  && a.PunchInTime != null
                                                  && a.PunchInTime != TimeOnly.MinValue),
                 EvaluationResult = _context.Evaluations
                     .Where(ev => ev.EmployeeId == e.Id)
                     .OrderByDescending(ev => ev.DateEvaluate)
                     .Select(ev => ev.Status)
                     .FirstOrDefault()
             })
             .ToList();

            return View(employees);
        }

        public IActionResult ViewEmployees()
        {
            string? userIdCookie = Request.Cookies["UserID"];
            int? userIdSession = HttpContext.Session.GetInt32("UserID");
            int managerId = int.TryParse(userIdCookie, out int tempId) ? tempId : userIdSession ?? 0;

            var Employees = _context.Employees.Where(e => e.ManagerId == managerId).ToList();
            return View(Employees);
        }


        public IActionResult AddEmployee()
        {
            int? sessionUserId = HttpContext.Session?.GetInt32("UserID");
            string cookieUserId = Request.Cookies["UserID"];

            if (sessionUserId == null && string.IsNullOrEmpty(cookieUserId))
            {
                return BadRequest("UserID not found in session or cookies.");
            }

            int managerId;
            if (sessionUserId.HasValue)
            {
                managerId = sessionUserId.Value;
            }
            else if (!int.TryParse(cookieUserId, out managerId))
            {
                return BadRequest("Invalid UserID in cookies.");
            }

            var manager = _context.Managers
                .Include(m => m.Department)
                .FirstOrDefault(m => m.Id == managerId);

            if (manager == null || manager.Department == null)
            {
                return NotFound("Manager or department not found.");
            }

            ViewBag.DepartmentName = manager.Department.Name;
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmployee(Employee employee, IFormFile ImageFile)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserID");
            string cookieUserId = Request.Cookies["UserID"];

            if (sessionUserId == null && string.IsNullOrEmpty(cookieUserId))
            {
                return BadRequest("UserID not found in session or cookies.");
            }

            int managerId;
            if (sessionUserId.HasValue)
            {
                managerId = sessionUserId.Value;
            }
            else if (!int.TryParse(cookieUserId, out managerId))
            {
                return BadRequest("Invalid UserID in cookies.");
            }

            var manager = _context.Managers.Include(m => m.Department).FirstOrDefault(m => m.Id == managerId);

            if (manager == null || manager.Department == null)
            {
                return NotFound("Manager or department not found.");
            }

            employee.DepartmentId = manager.Department.Id;
            employee.ManagerId = managerId;

            //var passwordHasher = new PasswordHasher<Employee>();
            //employee.PasswordHash = passwordHasher.HashPassword(employee, employee.PasswordHash);

            if (ImageFile != null && ImageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var fileExtension = System.IO.Path.GetExtension(ImageFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("ProfileImage", "Only .jpg, .jpeg, and .png formats are allowed.");
                    return View(employee);
                }

                if (ImageFile.Length > 2 * 1024 * 1024) 
                {
                    ModelState.AddModelError("ProfileImage", "File size must be less than 2MB.");
                    return View(employee);
                }

                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var filePath = System.IO.Path.Combine("wwwroot/img", fileName);

                using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                {
                    await ImageFile.CopyToAsync(stream);
                }

                employee.ProfileImage = fileName;
            }

            if (_context.Employees.Any(e => e.Email == employee.Email))
            {
                ModelState.AddModelError("Email", "This email is already in use.");
                return View(employee);
            }

            employee.CreatedAt = DateTime.Now;

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return RedirectToAction("ViewEmployees");
        }


        public async Task<IActionResult> DownloadEmployeesDataPDF()
        {
            string? userIdCookie = Request.Cookies["UserID"];
            int? userIdSession = HttpContext.Session.GetInt32("UserID");
            int managerId = int.TryParse(userIdCookie, out int tempId) ? tempId : userIdSession ?? 0;

            var employees = _context.Employees.Where(e => e.ManagerId == managerId).ToList();

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4);
                PdfWriter writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                Paragraph title = new Paragraph("Employees Data", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                PdfPTable table = new PdfPTable(5)
                {
                    WidthPercentage = 100
                };

                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                string[] headers = { "Employee ID", "Name", "Email", "Position", "Address" };
                foreach (var header in headers)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, headerFont))
                    {
                        BackgroundColor = new BaseColor(50, 50, 50),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5
                    };
                    table.AddCell(cell);
                }

                Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                foreach (var emp in employees)
                {
                    table.AddCell(new PdfPCell(new Phrase(emp.Id.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(emp.Name, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(emp.Email, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(emp.Position, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(emp.Address, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", "EmployeesData.pdf");
            }
        }

        public IActionResult ViewTasks(int id)
        {
            var employee = _context.Employees.Find(id);
            var employeeTasks = _context.Tasks.Where(t => t.AssignedToEmployeeId == id).ToList();

            ViewBag.EmployeeName = employee?.Name ?? "Employee";
            return View(employeeTasks);
        }

        [HttpPost]
        public async Task<IActionResult> EditTask(HRManagementSystem.Models.Task task)
        {
            if (task == null || task.Id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid task.";
                return RedirectToAction("ViewTasks", new { id = task.AssignedToEmployeeId });
            }

            var existingTask = await _context.Tasks.FindAsync(task.Id);
            if (existingTask == null)
            {
                TempData["ErrorMessage"] = "Task not found.";
                return RedirectToAction("ViewTasks", new { id = task.AssignedToEmployeeId });
            }

            existingTask.Title = task.Title;
            existingTask.Description = task.Description;
            existingTask.StartDate = task.StartDate;
            existingTask.DueDate = task.DueDate;

            try
            {
                _context.Tasks.Update(existingTask);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Task updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating task: " + (ex.InnerException?.Message ?? ex.Message);
            }


            return RedirectToAction("ViewTasks", new { id = existingTask.AssignedToEmployeeId });
        }



        [HttpPost]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null)
            {
                TempData["ErrorMessage"] = "Task not found.";
                return RedirectToAction("ViewTasks");
            }

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            return RedirectToAction("ViewTasks", new { id = task.AssignedToEmployeeId });
        }

        public IActionResult ViewAttendance(int id)
        {
            var employee = _context.Employees.Find(id);
            var employeeAttendance = _context.Attendances.Where(t => t.EmployeeId == id).ToList();

            ViewBag.EmployeeName = employee?.Name ?? "Employee";

            return View(employeeAttendance);
        }

        public IActionResult AssignTask()
        {
            int managerId = 0;

            if (Request.Cookies.TryGetValue("UserID", out string? userIdCookie) && int.TryParse(userIdCookie, out int cookieManagerId))
            {
                managerId = cookieManagerId;
            }
            else if (HttpContext.Session.GetInt32("UserID") is int sessionManagerId)
            {
                managerId = sessionManagerId;
            }

            var employees = _context.Employees
                .Where(e => e.ManagerId == managerId)
                .Select(e => new { e.Id, e.Name })
                .ToList();

            ViewBag.Employees = employees.Any()
                ? new SelectList(employees, "Id", "Name")
                : new SelectList(new List<object>(), "Id", "Name");

            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTask(HRManagementSystem.Models.Task task)
        {
            if (task == null)
            {
                ModelState.AddModelError("", "Invalid task data.");
                return View(new HRManagementSystem.Models.Task());
            }

            if (string.IsNullOrEmpty(task.Title) || task.StartDate == default || task.DueDate == default || string.IsNullOrEmpty(task.Description) || task.AssignedToEmployeeId == 0)
            {
                ModelState.AddModelError("", "All fields are required.");
                return View(task);
            }

            int managerId = 0;

            if (Request.Cookies.TryGetValue("UserID", out string? userIdCookie) && int.TryParse(userIdCookie, out int cookieManagerId))
            {
                managerId = cookieManagerId;
            }
            else if (HttpContext.Session.GetInt32("UserID") is int sessionManagerId)
            {
                managerId = sessionManagerId;
            }

            if (managerId == 0)
            {
                ModelState.AddModelError("", "Manager ID is not found. Please log in again.");
                return View(task);
            }

            var employees = _context.Employees
                .Where(e => e.ManagerId == managerId)
                .Select(e => new { e.Id, e.Name, e.Email })
                .ToList();

            ViewBag.Employees = employees.Any()
                ? new SelectList(employees, "Id", "Name")
                : new SelectList(new List<object>(), "Id", "Name");

            task.CreatedAt = DateTime.Now;
            task.Status = "To Do";
            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            var assignedEmployee = employees.FirstOrDefault(e => e.Id == task.AssignedToEmployeeId);
            if (assignedEmployee != null && !string.IsNullOrEmpty(assignedEmployee.Email))
            {
                SendEmailNotification(assignedEmployee.Email, task, assignedEmployee.Name);
            }

            TempData["SuccessMessage"] = "Task assigned successfully and email sent!";

            return View();
        }

        private void SendEmailNotification(string recipientEmail, HRManagementSystem.Models.Task task, string recipientName)
        {
            try
            {
                using (SmtpClient client = new SmtpClient("smtp.gmail.com")) 
                {
                    client.Port = 587; 
                    client.Credentials = new NetworkCredential("nadaqdesat@gmail.com", "aibl avfx vyag dxgn");
                    client.EnableSsl = true;

                    MailMessage mailMessage = new MailMessage
                    {
                        From = new MailAddress("nadaqdesat@gmail.com"),
                        Subject = "New Task Assigned: " + task.Title,
                        Body = $"Hello {recipientName},\n\n" +
                        $"You have been assigned a new task:\n\n" +
                        $"Title: {task.Title}\n" +
                        $"Start Date: {task.StartDate.ToString("dd/MM/yyyy")}\n" +
                        $"Due Date: {task.DueDate.ToString("dd/MM/yyyy")}\n" +
                        $"Description: {task.Description}\n\n" +
                        $"Please check your task dashboard for more details.\n\n" +
                        $"Best regards,\nYour Management Team",
                        IsBodyHtml = false
                    };
                    mailMessage.To.Add(recipientEmail);

                    client.Send(mailMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending email: " + ex.Message);
            }
        }



        public IActionResult EvaluateEmployee()
        {
            int managerId = 0;

            if (Request.Cookies.TryGetValue("UserID", out string? userIdCookie) && int.TryParse(userIdCookie, out int cookieManagerId))
            {
                managerId = cookieManagerId;
            }
            else if (HttpContext.Session.GetInt32("UserID") is int sessionManagerId)
            {
                managerId = sessionManagerId;
            }

            var employees = _context.Employees
                .Where(e => e.ManagerId == managerId)
                .Select(e => new { e.Id, e.Name })
                .ToList();

            ViewBag.Employees = employees.Any()
                ? new SelectList(employees, "Id", "Name")
                : new SelectList(new List<object>(), "Id", "Name");
            return View();
        }

        [HttpPost]
        public IActionResult EvaluateEmployee(Evaluation evaluation, List<int> Scores)
        {
            int managerId = 0;

            if (Request.Cookies.TryGetValue("UserID", out string? userIdCookie) && int.TryParse(userIdCookie, out int cookieManagerId))
            {
                managerId = cookieManagerId;
            }
            else if (HttpContext.Session.GetInt32("UserID") is int sessionManagerId)
            {
                managerId = sessionManagerId;
            }

            var employee = _context.Employees.FirstOrDefault(e => e.Id == evaluation.EmployeeId && e.ManagerId == managerId);
            if (employee == null)
            {
                return RedirectToAction("Error", "Home");
            }

            var averageScore = Scores.Average();
            string status;

            if (averageScore >= 4.5)
                status = "Excellent";
            else if (averageScore >= 3.5)
                status = "Very Good";
            else if (averageScore >= 2.5)
                status = "Good";
            else if (averageScore >= 1.5)
                status = "Fair";
            else
                status = "Bad";

            var newEvaluation = new Evaluation
            {
                EmployeeId = evaluation.EmployeeId,
                Status = status,  
                Comment = evaluation.Comment,
                DateEvaluate = DateTime.Now
            };

            _context.Evaluations.Add(newEvaluation);
            _context.SaveChanges();

            TempData["SuccessMessage2"] = "Evaluation submitted successfully!";
            return View();
        }

        public async Task<IActionResult> LeaveRequests()
        {
            int managerId = 0;

            if (Request.Cookies.TryGetValue("UserID", out string? userIdCookie) && int.TryParse(userIdCookie, out int cookieManagerId))
            {
                managerId = cookieManagerId;
            }
            else if (HttpContext.Session.GetInt32("UserID") is int sessionManagerId)
            {
                managerId = sessionManagerId;
            }

            var leaveRequests = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Employee.ManagerId == managerId)
                .OrderBy(l => l.Status == "Pending" ? 0 : 1)
                .ThenByDescending(l => l.StartTime)
                .ToListAsync();

            return View(leaveRequests);
        }

        public async Task<IActionResult> DownloadLeaveRequestsPDF()
        {
            int managerId = 0;

            if (Request.Cookies.TryGetValue("UserID", out string? userIdCookie) && int.TryParse(userIdCookie, out int cookieManagerId))
            {
                managerId = cookieManagerId;
            }
            else if (HttpContext.Session.GetInt32("UserID") is int sessionManagerId)
            {
                managerId = sessionManagerId;
            }

            var leaveRequests = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Employee.ManagerId == managerId)
                .OrderBy(l => l.Status == "Pending" ? 0 : 1)
                .ThenByDescending(l => l.StartTime)
                .ToListAsync();

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4.Rotate()); 
                PdfWriter writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                Paragraph title = new Paragraph("Leave Requests", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                PdfPTable table = new PdfPTable(8)
                {
                    WidthPercentage = 100
                };

                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                string[] headers = { "Employee Name", "Leave Type", "Start Time", "End Time", "Leave Date", "Leave Hours", "Reason", "Status" };
                foreach (var header in headers)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, headerFont))
                    {
                        BackgroundColor = new BaseColor(50, 50, 50),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5
                    };
                    table.AddCell(cell);
                }

                Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                foreach (var leave in leaveRequests)
                {
                    table.AddCell(new PdfPCell(new Phrase(leave.Employee?.Name, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(leave.LeaveType, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(leave.StartTime?.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(leave.EndTime?.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(leave.LeaveDate?.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER }); 
                    table.AddCell(new PdfPCell(new Phrase(leave.LeaveHours?.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(leave.Reason, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(leave.Status, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", "LeaveRequests.pdf");
            }
        }


        [HttpPost]
        public async Task<IActionResult> ApproveLeave(int id, int employeeId)
        {
            var leaveRequest = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
            if (leaveRequest != null)
            {
                leaveRequest.Status = "Approved";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("LeaveRequests");
        }

        [HttpPost]
        public async Task<IActionResult> RejectLeave(int id, int employeeId)
        {
            var leaveRequest = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
            if (leaveRequest != null)
            {
                leaveRequest.Status = "Rejected";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("LeaveRequests");
        }

        public async Task<IActionResult> VacRequests()
        {
            int managerId = 0;

            if (Request.Cookies.TryGetValue("UserID", out string? userIdCookie) && int.TryParse(userIdCookie, out int cookieManagerId))
            {
                managerId = cookieManagerId;
            }
            else if (HttpContext.Session.GetInt32("UserID") is int sessionManagerId)
            {
                managerId = sessionManagerId;
            }

            var VacRequests = await _context.VaccationRequests
                .Include(l => l.Employee)
                .Where(l => l.Employee.ManagerId == managerId)
                .OrderBy(l => l.Status == "Pending" ? 0 : 1)
                .ThenByDescending(l => l.StartDate)
                .ToListAsync();

            return View(VacRequests);
        }

        public async Task<IActionResult> DownloadVaccationRequestsPDF()
        {
            int managerId = 0;

            if (Request.Cookies.TryGetValue("UserID", out string? userIdCookie) && int.TryParse(userIdCookie, out int cookieManagerId))
            {
                managerId = cookieManagerId;
            }
            else if (HttpContext.Session.GetInt32("UserID") is int sessionManagerId)
            {
                managerId = sessionManagerId;
            }

            var VaccationRequests = await _context.VaccationRequests
                .Include(l => l.Employee)
                .Where(l => l.Employee.ManagerId == managerId)
                .OrderBy(l => l.Status == "Pending" ? 0 : 1)
                .ThenByDescending(l => l.StartDate)
                .ToListAsync();

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4.Rotate());
                PdfWriter writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                Paragraph title = new Paragraph("Vaccation Requests", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                PdfPTable table = new PdfPTable(7)
                {
                    WidthPercentage = 100
                };

                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                string[] headers = { "Employee Name", "Vaccation Type", "Start Date", "End Date", "Vaccation Days", "Reason", "Status" };
                foreach (var header in headers)
                {
                    PdfPCell cell = new PdfPCell(new Phrase(header, headerFont))
                    {
                        BackgroundColor = new BaseColor(50, 50, 50),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5
                    };
                    table.AddCell(cell);
                }

                Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                foreach (var Vaccation in VaccationRequests)
                {
                    table.AddCell(new PdfPCell(new Phrase(Vaccation.Employee?.Name, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(Vaccation.VaccType, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(Vaccation.StartDate.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(Vaccation.EndDate.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(Vaccation.VaccDays.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(Vaccation.Reason, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(Vaccation.Status, dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", "VaccationRequests.pdf");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveVacc(int id, int employeeId)
        {
            var VaccRequest = await _context.VaccationRequests.FirstOrDefaultAsync(l => l.Id == id);
            if (VaccRequest != null)
            {
                VaccRequest.Status = "Approved";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("VacRequests");
        }

        [HttpPost]
        public async Task<IActionResult> RejectVacc(int id, int employeeId)
        {
            var VaccRequest = await _context.VaccationRequests.FirstOrDefaultAsync(l => l.Id == id);
            if (VaccRequest != null)
            {
                VaccRequest.Status = "Rejected";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("VacRequests");
        }
    }
}

