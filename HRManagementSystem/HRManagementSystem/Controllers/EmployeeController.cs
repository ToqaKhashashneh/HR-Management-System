using HRManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace HRManagementSystem.Controllers
{
    public class EmployeeController : Controller
    {

        private readonly MyDbContext _context;

        public EmployeeController(MyDbContext context)
        {
            _context = context;
        }

        //Dashboard
        public ActionResult Dashboard()
        {
            string? userIdCookie = Request.Cookies["UserID"];
            int? userIdSession = HttpContext.Session.GetInt32("UserID");
            int employeeId = int.TryParse(userIdCookie, out int tempId) ? tempId : userIdSession ?? 0;
            var employee = _context.Employees.Find(employeeId);
            ViewData["toDoCount"] = _context.Tasks.Count(t => t.Status == "To Do" && t.AssignedToEmployeeId == employeeId);
            ViewData["doingCount"] = _context.Tasks.Count(t => t.Status == "Doing" && t.AssignedToEmployeeId == employeeId);
            ViewData["doneCount"] = _context.Tasks.Count(t => t.Status == "Done" && t.AssignedToEmployeeId == employeeId);

            var evaluation = _context.Evaluations.Where(t => t.EmployeeId == employeeId).OrderByDescending(t => t.DateEvaluate).FirstOrDefault();
            if (evaluation != null)
            {
                ViewData["Status"] = evaluation.Status;
                ViewData["Comment"] = evaluation.Comment;
                ViewData["DateEvaluate"] = evaluation.DateEvaluate.HasValue ? evaluation.DateEvaluate.Value.ToString("yyyy-MM-dd") : "N/A";
                //ViewData["DateEvaluate"] = evaluation.DateEvaluate.Value.ToString("yyyy-MM-dd");
            }
            else
            {
                ViewData["Status"] = "No evaluation found";
                ViewData["Comment"] = "N/A";
                ViewData["DateEvaluate"] = "N/A";
            }

            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var attendanceThisMonth = _context.Attendances
            .Where(a => a.EmployeeId == employeeId && a.Date.Month == currentMonth && a.Date.Year == currentYear)
            .ToList();


            int presentDays = attendanceThisMonth.Count(a => a.PunchInTime != null && a.PunchOutTime.HasValue);
            int totalDaysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);
            int daysPassed = DateTime.Now.Day;
            int absentDays = daysPassed - presentDays;


            var leavesThisMonth = _context.LeaveRequests
                .Where(l => l.EmployeeId == employeeId
                && l.CreatedAt.HasValue
                && l.CreatedAt.Value.Month == currentMonth
                && l.CreatedAt.Value.Year == currentYear
                && l.Status == "Approved")
                .ToList();

            int leaveDays = leavesThisMonth.Select(l => l.CreatedAt.Value.Date).Distinct().Count();

            double totalWorkingHours = attendanceThisMonth
                .Where(a => a.PunchInTime != null && a.PunchOutTime.HasValue)
                .Sum(a =>
                {
                    DateTime punchInDateTime = a.Date.ToDateTime(a.PunchInTime);
                    DateTime punchOutDateTime = a.Date.ToDateTime(a.PunchOutTime.GetValueOrDefault(TimeOnly.MinValue));
                    return (punchOutDateTime - punchInDateTime).TotalHours;
                });

            double averageWorkingHours = presentDays > 0 ? totalWorkingHours / presentDays : 0;

            double overtimeHours = attendanceThisMonth
                .Where(a => a.PunchInTime != null && a.PunchOutTime.HasValue)
                .Sum(a =>
                {
                    DateTime punchInDateTime = a.Date.ToDateTime(a.PunchInTime);
                    DateTime punchOutDateTime = a.Date.ToDateTime(a.PunchOutTime.GetValueOrDefault(TimeOnly.MinValue));
                    return Math.Max(0, (punchOutDateTime - punchInDateTime).TotalHours - 8);
                });

            TempData["PresentDays"] = presentDays;
            TempData["AbsentDays"] = absentDays;
            TempData["LeaveDays"] = leaveDays;
            TempData["TotalWorkingHours"] = totalWorkingHours.ToString("0.##");
            TempData["AverageWorkingHours"] = averageWorkingHours.ToString("0.##");
            TempData["OvertimeHours"] = overtimeHours.ToString("0.##");

            return View(employee);
        }

        //Attendance
        public ActionResult Attendance()
        {
            string? userIdCookie = Request.Cookies["UserID"];
            int? userIdSession = HttpContext.Session.GetInt32("UserID");
            int employeeId = int.TryParse(userIdCookie, out int tempId) ? tempId : userIdSession ?? 0;

            var attendance = _context.Attendances.Where(e => e.EmployeeId == employeeId).ToList();
            return View(attendance);
        }

        [HttpPost]
        public IActionResult PunchIn_PunchOut()
        {
            string? userIdCookie = Request.Cookies["UserID"];
            int? userIdSession = HttpContext.Session.GetInt32("UserID");
            int employeeId = int.TryParse(userIdCookie, out int tempId) ? tempId : userIdSession ?? 0;

            var employee = _context.Employees.FirstOrDefault(e => e.Id == employeeId);

            var today = DateOnly.FromDateTime(DateTime.Now);
            var attendance = _context.Attendances
                .FirstOrDefault(a => a.EmployeeId == employee.Id && a.Date == today);

            if (attendance == null)
            {
                attendance = new Attendance
                {
                    EmployeeId = employee.Id,
                    Date = today,
                    PunchInTime = TimeOnly.FromDateTime(DateTime.Now),
                    PunchOutTime = null
                };
                _context.Attendances.Add(attendance);
            }
            else if (attendance.PunchOutTime == null)
            {
                attendance.PunchOutTime = TimeOnly.FromDateTime(DateTime.Now);
            }

            _context.SaveChanges();

            return RedirectToAction("Attendance");
        }

        //EmployeeTasks
        public ActionResult EmployeeTasks(string query)
        {
            string? userIdCookie = Request.Cookies["UserID"];
            int? userIdSession = HttpContext.Session.GetInt32("UserID");
            int employeeId = int.TryParse(userIdCookie, out int tempId) ? tempId : userIdSession ?? 0;

            var tasks = _context.Tasks.Where(t => t.AssignedToEmployeeId == employeeId).ToList();
            if (!string.IsNullOrEmpty(query))
            {
                tasks = tasks.Where(t => t.Title.Contains(query) || t.Description.Contains(query)).ToList();
            }
            return View(tasks);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var task = await _context.Tasks.FindAsync(id);

            task.Status = status;

            _context.Update(task);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(EmployeeTasks));
        }


        //VaccationRequests
        public IActionResult EmpVacRequest()
        {
            string? userIdCookie = Request.Cookies["UserID"];
            int? userIdSession = HttpContext.Session.GetInt32("UserID");
            int employeeId = int.TryParse(userIdCookie, out int tempId) ? tempId : userIdSession ?? 0;

            var Vac = _context.VaccationRequests.Where(v => v.EmployeeId == employeeId).ToList();

            return View(Vac);
        }

        public IActionResult CreateVacReq()
        {
            return View(new VaccationRequest());
        }

        [HttpPost]
        public IActionResult CreateVacReq(VaccationRequest VacRequest)
        {
            string? userIdCookie = Request.Cookies["UserID"];
            int? userIdSession = HttpContext.Session.GetInt32("UserID");
            VacRequest.EmployeeId = int.TryParse(userIdCookie, out int tempId) ? tempId : userIdSession ?? 0;

            if (VacRequest.StartDate.HasValue && VacRequest.EndDate.HasValue)
            {
                VacRequest.VaccDays = VacRequest.EndDate.Value.ToDateTime(TimeOnly.MinValue)
                           .Subtract(VacRequest.StartDate.Value.ToDateTime(TimeOnly.MinValue)).Days;

                if (VacRequest.VaccDays <= 0)
                {
                    ModelState.AddModelError("EndDate", "End date must be after start date.");
                    return View(VacRequest);
                }

            }
            else
            {
                ModelState.AddModelError("StartDate", "Both Start Date and End Date are required.");
                return View(VacRequest);
            }

            if (VacRequest.VaccType == "Other" && string.IsNullOrEmpty(VacRequest.Reason))
            {
                ModelState.AddModelError("Reason", "Reason is required when Vacation Type is 'Other'.");
                return View(VacRequest);
            }

            VacRequest.Status = "Pending";
            VacRequest.CreatedAt = DateTime.Now;

            _context.VaccationRequests.Add(VacRequest);
            _context.SaveChanges();

            return RedirectToAction("EmpVacRequest");

        }


        //LeaveRequests
        public IActionResult EmpLeaveRequest()
        {
            string? userIdCookie = Request.Cookies["UserID"];
            int? userIdSession = HttpContext.Session.GetInt32("UserID");
            int employeeId = int.TryParse(userIdCookie, out int tempId) ? tempId : userIdSession ?? 0;

            var leaves = _context.LeaveRequests.Where(v => v.EmployeeId == employeeId).ToList();

            return View(leaves);
        }

        public IActionResult CreateLeaveReq()
        {
            return View(new LeaveRequest());
        }

        [HttpPost]
        public IActionResult CreateLeaveReq(LeaveRequest LeaveRequest)
        {
            string? userIdCookie = Request.Cookies["UserID"];
            int? userIdSession = HttpContext.Session.GetInt32("UserID");
            LeaveRequest.EmployeeId = int.TryParse(userIdCookie, out int tempId) ? tempId : userIdSession ?? 0;

            if (LeaveRequest.StartTime.HasValue && LeaveRequest.EndTime.HasValue)
            {
                TimeSpan timeDifference = LeaveRequest.EndTime.Value.ToTimeSpan()
                                        - LeaveRequest.StartTime.Value.ToTimeSpan();

                LeaveRequest.LeaveHours = (decimal)timeDifference.TotalHours;

                if (LeaveRequest.LeaveHours <= 0)
                {
                    ModelState.AddModelError("EndTime", "End time must be after start time.");
                    return View(LeaveRequest);
                }

            }
            else
            {
                ModelState.AddModelError("StartTime", "Both Start Time and End Time are required.");
                return View(LeaveRequest);
            }

            if (LeaveRequest.LeaveType == "Other" && string.IsNullOrEmpty(LeaveRequest.Reason))
            {
                ModelState.AddModelError("Reason", "Reason is required when Vacation Type is 'Other'.");
                return View(LeaveRequest);
            }

            LeaveRequest.Status = "Pending";
            LeaveRequest.CreatedAt = DateTime.Now;

            _context.LeaveRequests.Add(LeaveRequest);
            _context.SaveChanges();

            return RedirectToAction("EmpLeaveRequest");

        }
    }
}
