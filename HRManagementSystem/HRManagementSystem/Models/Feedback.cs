﻿using System;
using System.Collections.Generic;

namespace HRManagementSystem.Models;

public partial class Feedback
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Message { get; set; } = null!;

    public DateTime? ReceivedAt { get; set; }
}
