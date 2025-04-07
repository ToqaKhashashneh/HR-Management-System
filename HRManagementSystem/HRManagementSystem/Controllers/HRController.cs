using HRManagementSystem.Models;
using iTextSharp.text.pdf;
using iTextSharp.text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HRManagementSystem.Controllers
{
    public class HRController : Controller
    {
        private readonly MyDbContext _context;

        public HRController(MyDbContext context)
        {
            _context = context;
        }
        public ActionResult Dashboard()
        {
            int totalEmployees = _context.Employees.Count();
            int totalManagers = _context.Managers.Count();
            int totalDepartments = _context.Departments.Count();
            int pendingVacationsAndLeaves = _context.VaccationRequests.Count(v => v.Status == "Pending") + _context.LeaveRequests.Count(v => v.Status == "Pending");
            int totalFeedbacks = _context.Feedbacks.Count();
            int totalEvaluations = _context.Evaluations.Count();

            ViewData["TotalEmployees"] = totalEmployees > 0 ? totalEmployees : 0;
            ViewData["TotalManagers"] = totalManagers > 0 ? totalManagers : 0;
            ViewData["TotalDepartments"] = totalDepartments > 0 ? totalDepartments : 0;
            ViewData["PendingVacations_Leaves"] = pendingVacationsAndLeaves > 0 ? pendingVacationsAndLeaves : 0;
            ViewData["TotalFeedbacks"] = totalFeedbacks > 0 ? totalFeedbacks : 0;
            ViewData["TotalEvaluations"] = totalEvaluations > 0 ? totalEvaluations : 0;

            var departments = _context.Departments
                .Include(d => d.Manager)
                .Select(d => new
                {
                    DepartmentName = d.Name,
                    ManagerName = d.Manager != null ? d.Manager.Name : "No Manager",
                    EmployeesCount = d.Employees.Count()
                }).ToList();

            var leaveRequests = _context.LeaveRequests
                .Include(l => l.Employee)
                .Select(l => new
                {
                    EmployeeName = l.Employee.Name,
                    LeaveType = l.LeaveType,
                    StartTime = l.StartTime,
                    EndTime = l.EndTime,
                    LeaveDate = l.LeaveDate,
                    Status = l.Status
                }).ToList();

            var evaluations = _context.Evaluations
                .Include(e => e.Employee)
                .Select(e => new
                {
                    EmployeeName = e.Employee.Name,
                    Status = e.Status,
                    Comment = e.Comment,
                    DateEvaluate = e.DateEvaluate
                }).ToList();

            var feedbacks = _context.Feedbacks
                .Select(f => new
                {
                    Name = f.Name,
                    Email = f.Email,
                    Date = f.ReceivedAt,
                    Message = f.Message
                }).ToList();

            ViewData["Departments"] = departments;
            ViewData["LeaveRequests"] = leaveRequests;
            ViewData["Evaluations"] = evaluations;
            ViewData["Feedbacks"] = feedbacks;


            return View();
        }

        //Department
        public ActionResult ViewDepartment()
        {
            var Department = _context.Departments.ToList();
            return View(Department);
        }

        public async Task<IActionResult> DownloadDepartmentsDataPDF()
        {
            var departments = await _context.Departments.OrderBy(d => d.CreatedAt).ToListAsync();

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4);
                PdfWriter.GetInstance(document, stream);
                document.Open();

                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.White);
                Paragraph title = new Paragraph("Departments Data", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                PdfPTable table = new PdfPTable(3)
                {
                    WidthPercentage = 100
                };

                float[] columnWidths = { 30f, 50f, 30f };
                table.SetWidths(columnWidths);

                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                string[] headers = { "Department Name", "Description", "Created At" };

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
                foreach (var dept in departments)
                {
                    table.AddCell(new PdfPCell(new Phrase(dept.Name ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(dept.Description ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(dept.CreatedAt.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", "DepartmentsData.pdf");
            }
        }

        public ActionResult CreateDepartment()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateDepartment(Department department)
        {
            department.CreatedAt = System.DateTime.Now;
            _context.Departments.Add(department);
            _context.SaveChanges();

            return RedirectToAction(nameof(ViewDepartment));
        }

        //Maneger
        public ActionResult ViewManager()

        {
            var manegers = _context.Managers.Include(e => e.Department).ToList();

            return View(manegers);
        }

        public async Task<IActionResult> DownloadManagersDataPDF()
        {
            var managers = await _context.Managers
                                         .Include(m => m.Department)
                                         .OrderBy(m => m.CreatedAt)
                                         .ToListAsync();

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4);
                PdfWriter.GetInstance(document, stream);
                document.Open();

                // العنوان الرئيسي
                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                Paragraph title = new Paragraph("Managers Data", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                // إنشاء الجدول بـ 5 أعمدة
                PdfPTable table = new PdfPTable(5)
                {
                    WidthPercentage = 100
                };

                // تعيين عرض الأعمدة
                float[] columnWidths = { 25f, 35f, 40f, 25f, 30f };
                table.SetWidths(columnWidths);

                // عناوين الأعمدة
                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                string[] headers = { "Manager Name", "Email", "Address", "Created At", "Department" };

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

                // إضافة البيانات
                Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                foreach (var manager in managers)
                {
                    table.AddCell(new PdfPCell(new Phrase(manager.Name ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(manager.Email ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(manager.Address ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(manager.CreatedAt.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(manager.Department?.Name ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", "ManagersData.pdf");
            }
        }

        public ActionResult CreateManager()
        {
            ViewBag.Department = new SelectList(_context.Departments, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateManager(Manager manager, int DepartmentId)
        {
            bool departmentHasManager = await _context.Managers.AnyAsync(m => m.DepartmentId == DepartmentId);

            if (departmentHasManager)
            {
                ModelState.AddModelError("DepartmentId", "This department already has a manager. Please choose another one.");
                ViewBag.Department = new SelectList(_context.Departments, "Id", "Name");
                return View(manager);
            }

            var file = Request.Form.Files.FirstOrDefault();
            if (file != null && file.Length > 0)
            {
                // تحديد مسار التخزين
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img");

                string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // حفظ الصورة في المجلد
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                // حفظ المسار في قاعدة البيانات
                manager.ProfileImage = uniqueFileName;
            }

            manager.DepartmentId = DepartmentId;
            manager.CreatedAt = DateTime.Now;

            _context.Managers.Add(manager);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ViewManager));
        }

        //Employee
        public ActionResult ViewEmployee()
        {
            var Employees = _context.Employees.Include(e => e.Department).Include(e => e.Manager).ToList();
            return View(Employees);
        }

        public async Task<IActionResult> DownloadEmployeesDataPDF()
        {
            var employees = await _context.Employees
                                          .Include(e => e.Department)
                                          .Include(e => e.Manager)
                                          .OrderBy(e => e.CreatedAt)
                                          .ToListAsync();

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4);
                PdfWriter.GetInstance(document, stream);
                document.Open();

                // العنوان الرئيسي
                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                Paragraph title = new Paragraph("Employees Data", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                // إنشاء الجدول بـ 6 أعمدة
                PdfPTable table = new PdfPTable(6)
                {
                    WidthPercentage = 100
                };

                // تعيين عرض الأعمدة
                float[] columnWidths = { 25f, 35f, 30f, 35f, 35f, 35f };
                table.SetWidths(columnWidths);

                // عناوين الأعمدة
                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                string[] headers = { "Employee Name", "Email", "Position", "Address", "Department", "Manager" };

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

                // إضافة البيانات
                Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                foreach (var employee in employees)
                {
                    table.AddCell(new PdfPCell(new Phrase(employee.Name ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(employee.Email ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(employee.Position ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(employee.Address ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(employee.Department?.Name ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(employee.Manager?.Name ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", "EmployeesData.pdf");
            }
        }


        //Evaluation
        public ActionResult ViewEvaluation()
        {
            var evaluations = _context.Evaluations.Include(e => e.Employee).ToList();

            return View(evaluations);
        }

        public async Task<IActionResult> DownloadEvaluationsDataPDF()
        {
            var evaluations = await _context.Evaluations
                                            .Include(e => e.Employee)
                                            .OrderBy(e => e.DateEvaluate)
                                            .ToListAsync();

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4);
                PdfWriter.GetInstance(document, stream);
                document.Open();

                // العنوان الرئيسي
                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                Paragraph title = new Paragraph("Evaluations Data", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                // إنشاء الجدول بـ 4 أعمدة
                PdfPTable table = new PdfPTable(4)
                {
                    WidthPercentage = 100
                };

                // تعيين عرض الأعمدة
                float[] columnWidths = { 30f, 40f, 20f, 30f };
                table.SetWidths(columnWidths);

                // عناوين الأعمدة
                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                string[] headers = { "Employee", "Comment", "Status", "Date of Evaluation" };

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

                // إضافة البيانات
                Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                foreach (var evaluation in evaluations)
                {
                    table.AddCell(new PdfPCell(new Phrase(evaluation.Employee?.Name ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(evaluation.Comment ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(evaluation.Status ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(evaluation.DateEvaluate.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", "EvaluationsData.pdf");
            }
        }


        //Vaccation
        public ActionResult ViewVaccation()
        {
            var Vaccations = _context.VaccationRequests.Include(e => e.Employee).ToList();
            return View(Vaccations);
        }

        public async Task<IActionResult> DownloadVaccationRequestsPDF()
        {
            var requests = await _context.VaccationRequests
                                        .Include(v => v.Employee)
                                        .Where(v => v.Status == "Approved") 
                                        .OrderBy(v => v.StartDate)
                                        .ToListAsync();

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4);
                PdfWriter.GetInstance(document, stream);
                document.Open();

                // العنوان الرئيسي
                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                Paragraph title = new Paragraph("Approved Vaccation Requests", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                // إنشاء جدول بـ 7 أعمدة
                PdfPTable table = new PdfPTable(7)
                {
                    WidthPercentage = 100
                };

                // تعيين عرض الأعمدة
                float[] columnWidths = { 15f, 15f, 15f, 10f, 25f, 10f, 15f };
                table.SetWidths(columnWidths);

                // عناوين الأعمدة
                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                string[] headers = { "Type", "Start Date", "End Date", "Days", "Reason", "Status", "Employee" };

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

                // إضافة البيانات إلى الجدول
                Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                foreach (var request in requests)
                {
                    table.AddCell(new PdfPCell(new Phrase(request.VaccType ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.StartDate.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.EndDate.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.VaccDays.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.Reason ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.Status ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.Employee?.Name ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", "VaccationRequests.pdf");
            }
        }


        //Leave
        public ActionResult ViewLeave()
        {
            var Leaves = _context.LeaveRequests.Include(e => e.Employee).ToList();
            return View(Leaves);
        }

        public async Task<IActionResult> DownloadLeaveRequestsPDF()
        {
            var requests = await _context.LeaveRequests
                                        .Include(l => l.Employee)
                                        .Where(l => l.Status == "Approved") // تحميل الطلبات الموافق عليها فقط
                                        .OrderBy(l => l.LeaveDate)
                                        .ToListAsync();

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4);
                PdfWriter.GetInstance(document, stream);
                document.Open();

                // العنوان الرئيسي
                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                Paragraph title = new Paragraph("Approved Leave Requests", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                // إنشاء جدول بـ 8 أعمدة
                PdfPTable table = new PdfPTable(8)
                {
                    WidthPercentage = 100
                };

                // تعيين عرض الأعمدة
                float[] columnWidths = { 12f, 12f, 12f, 12f, 20f, 12f, 10f, 10f };
                table.SetWidths(columnWidths);

                // عناوين الأعمدة
                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                string[] headers = { "Type", "Start Time", "End Time", "Leave Date", "Reason", "Status", "Created At", "Employee" };

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

                // إضافة البيانات إلى الجدول
                Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                foreach (var request in requests)
                {
                    table.AddCell(new PdfPCell(new Phrase(request.LeaveType ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.StartTime.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.EndTime.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.LeaveDate.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.Reason ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.Status ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.CreatedAt.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(request.Employee?.Name ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", "LeaveRequests.pdf");
            }
        }


        public ActionResult ViewFeedback()
        {
            var Leaves = _context.Feedbacks.ToList();
            return View(Leaves);
        }

        public async Task<IActionResult> DownloadFeedbacksPDF()
        {
            var feedbacks = await _context.Feedbacks
                                          .OrderByDescending(f => f.ReceivedAt)
                                          .ToListAsync();

            using (MemoryStream stream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4);
                PdfWriter.GetInstance(document, stream);
                document.Open();

                // العنوان الرئيسي
                Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                Paragraph title = new Paragraph("Feedback List", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                // إنشاء جدول بـ 4 أعمدة
                PdfPTable table = new PdfPTable(4)
                {
                    WidthPercentage = 100
                };

                // تعيين عرض الأعمدة
                float[] columnWidths = { 20f, 30f, 40f, 20f };
                table.SetWidths(columnWidths);

                // عناوين الأعمدة
                Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.White);
                string[] headers = { "Name", "Email", "Message", "Received At" };

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

                // إضافة البيانات إلى الجدول
                Font dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                foreach (var feedback in feedbacks)
                {
                    table.AddCell(new PdfPCell(new Phrase(feedback.Name ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(feedback.Email ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(feedback.Message ?? "N/A", dataFont)) { HorizontalAlignment = Element.ALIGN_LEFT });
                    table.AddCell(new PdfPCell(new Phrase(feedback.ReceivedAt.ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER });
                }

                document.Add(table);
                document.Close();

                return File(stream.ToArray(), "application/pdf", "Feedbacks.pdf");
            }
        }


        public ActionResult ViewFeedbackSorted(string sortOrder)
        {
            if (string.IsNullOrEmpty(sortOrder))
            {
                sortOrder = "desc";
            }

            ViewData["ReceivedAtSortOrder"] = sortOrder;

            var feedbacks = _context.Feedbacks.AsQueryable();

            if (sortOrder == "asc")
            {
                feedbacks = feedbacks.OrderBy(f => f.ReceivedAt);
            }
            else
            {
                feedbacks = feedbacks.OrderByDescending(f => f.ReceivedAt);
            }

            return View("ViewFeedback", feedbacks.ToList());
        }
    }
}
