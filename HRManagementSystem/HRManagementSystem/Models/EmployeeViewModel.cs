using System;
using System.Collections.Generic;

namespace HRManagementSystem.Models;
public class EmployeeViewModel
{
    public int ID { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Position { get; set; }
    public string Address { get; set; }
    public bool IsAbsent { get; set; }
    public string EvaluationResult { get; set; }
}

